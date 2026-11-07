### RandomStringDetector

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`RandomStringDetector` recognizes the strings that look like random data rather than words, such as keys and tokens, and
the hexadecimal sequences inside a token, so that they are not reported as unknown words. cspell skips a long token that
looks random and ignores the unknown words that lie inside a hexadecimal sequence of a token, and the unit ports both
rules so that a project migrated from cspell stays quiet where it was quiet. The unit only judges and finds; what to do
with the answer belongs to the caller.

#### Data Model

`RandomStringDetector` is an `internal static partial` class with no state beyond constants and one generated regular
expression. Its constants are:

- `MinimumHexSequenceLength` (8): the shortest hexadecimal sequence, in characters, that is taken as data.
- The symbols that stand for the parts of a token in the categorized form: one for a run of digits, one for a word (a
  run of lower-case letters, optionally after one capital), one for a run of capitals, one for a run of separators, one
  for a combining mark that is not attached to a letter, and one each for a lower-case and an upper-case letter that
  was joined with its combining marks.
- The highest code point of the basic multilingual plane, which decides whether a symbol takes one UTF-16 code unit or
  two.
- The match time limit, the constant of `PatternParser`, for the hexadecimal pattern (see _PatternParser_).

**The score.** A token looks random when the structure of the token, reduced to one symbol per part, is at least half as
long as the token. The reduction follows cspell's `categorizeString`, in the same order, because each step sees the
result of the one before:

1. each run of ASCII digits becomes one digit symbol;
2. each letter that is followed by combining marks becomes one plain letter, lower case or capital, together with its
   marks, so the later steps see an ordinary letter;
3. each word, a run of lower-case letters optionally after one capital, becomes one word symbol; a capital that is not
   followed by a lower-case letter is left alone;
4. each run of capitals becomes one capitals symbol;
5. each combining mark that is left becomes one mark symbol;
6. underscores are removed, and then each run of hyphens, dots, and apostrophes becomes one separator symbol, so an
   underscore between two separators joins them into one run and an underscore alone vanishes.

Every other character stays as it is. The length of the result is counted in UTF-16 code units, so a code point outside
the basic plane counts as two, as the length of the token does. The token is random when twice the length of the result
is at least the length of the token, so an exact half counts as random.

Identifiers and prose are a few long words, so their reduced form is short and they score low; tokens and keys mix
digits, capitals, and punctuation, so they keep almost every character and score high. The score is a heuristic, so it
errs on both sides, exactly as cspell's does, and the library keeps the errors so that the same tokens are skipped. The
minimum length from which a token is judged at all is not part of the unit: it is the `minRandomLength` setting, and
the caller applies it.

**Hexadecimal sequences.** A sequence is a run of ASCII hexadecimal digits and hyphens that starts and ends with a
hexadecimal digit, starts at the start of the token or after a character that is not an ASCII letter or digit, and ends
at the end of the token or before one. A sequence is data, and is reported, only when it has at least eight characters,
neither follows a letter nor is followed by one, is written in one letter case (it does not hold both `A` to `F` and
`a` to `f`), and contains a digit or a hyphen. The last rule keeps a word made only of the letters `a` to `f`, such as
`deadbeef`, from being taken for data, and a partial UUID such as `3f2a9c10-7b` passes. The sequences are judged inside
the
token only: the text outside the token is not looked at, and the ranges are reported as offsets into the whole text.
A letter that precedes the sequence is judged by the single UTF-16 code unit before it, as cspell does, and a letter that
follows it by the whole character, so a letter outside the basic plane counts.

#### Key Methods

**IsRandom**: Determines whether a token looks like random data.

- _Parameters_: the token as a span of characters.
- _Returns_: `true` when twice the categorized length is at least the length of the token; `false` for an empty token.
- _Preconditions_: none; the caller decides whether the token is long enough to be judged.
- _Algorithm_: split the token into code points, keeping an unpaired surrogate as it is, apply the six steps above in
  order, and count the code units of the result. The categorization is hand-written over code points with no regular
  expression, each step is one pass, and the cost is linear in the length of the token even for a token of a million
  characters.
- _Postconditions_: nothing is changed; the working storage, proportional to the token, is not kept.

**FindHexSequences**: Appends the ranges of the hexadecimal sequences of a token to a list.

- _Parameters_: the text, the range of the token inside it, and the list that receives the ranges.
- _Preconditions_: the text and list are not null and the token lies inside the text.
- _Algorithm_: enumerate the matches, on the span of the token, of the generated pattern for a run of hexadecimal digits
  and hyphens that starts and ends with a hexadecimal digit and is delimited by anything that is not an ASCII letter or
  digit; keep a match that satisfies the rules above; add its range, moved to the offsets of the text.
- _Postconditions_: the list holds its earlier items followed by the ranges, in text order; it is never cleared.

The pattern is the only regular expression of the unit. It is built case-sensitive with the invariant culture and uses an
explicit ASCII word boundary and ASCII classes instead of `\b` and `\w`, the translation rules of `DefaultPatterns` (see
_DefaultPatterns_).

**Where the checker uses it.** `TextChecker` uses the unit at two points of its word-level algorithm, both only when the
`ignoreRandomStrings` setting is on (see _TextChecker_):

- _The token skip._ At the start of the check of each token, before any list or word is looked at, a token that is at
  least `minRandomLength` characters long (UTF-16 code units) and for which `IsRandom` is true is skipped as a whole.
  Because this comes first, even a flagged word inside such a token is not reported, as in cspell, and a long token is
  never looked up word by word.
- _The hexadecimal filter._ After a token that was not skipped has been checked word by word, the findings of that token
  are filtered: `FindHexSequences` is run on the token, and each unknown-word finding that starts inside a sequence is
  removed. A flagged-word finding is always kept. The filter does not run for a token that was ignored, accepted, or
  flagged as a whole by an entry with digits or punctuation, because that ends the check of the token earlier. It
  applies whatever the length of the token.

Design choices and their reasons:

- **Port the heuristic, errors included.** The score is imperfect, and a better one would be simple to invent, but the
  goal is to skip the tokens cspell skips and to check the ones it checks, and the parity corpora hold recorded cspell
  output to prove it.
- **Hand-written categorization, not regular expressions.** The reduction is a sequence of replacement steps over
  Unicode classes. A scanner over code points does the same steps without a pattern that could backtrack, so judging
  a token has a cost linear in its length and no time limit is needed, which matters because a minified file can hold
  a single token of enormous length.
- **A regular expression only for the sequences.** The hexadecimal pattern runs only for tokens that produced findings,
  and its shape, delimited runs, is a pattern problem; it is generated at build time, so it is trim and ahead-of-time
  safe, and it has the match time limit.
- **The length decision belongs to the caller.** `IsRandom` only scores, so the setting that selects the length stays in
  the checker with the other settings, and the unit has no state to configure.

#### Error Handling

`IsRandom` cannot fail: an empty token is not random, a lone surrogate is an ordinary character of the category
surrogate, and any other text scores. `FindHexSequences` throws `ArgumentNullException` for a null text or list and
`ArgumentOutOfRangeException` for a token that does not lie inside the text, both programming errors. Hostile text that
makes the pattern exceed its time limit throws `RegexMatchTimeoutException`; the ranges found so far remain in the list,
but the caller must treat the text as not checked, and `SpellChecker` reports the file as timed out. The class is
stateless and thread-safe; the generated regular expression is immutable and safe for concurrent matching, and only the
list passed in is changed, which must not be shared between threads.

#### Dependencies

- **PatternParser** (same subsystem) - the constant `MatchTimeoutMilliseconds`, so that the hexadecimal pattern shares
  the time limit of every other pattern.
- **TextRange** (see _WordTokenizer_) - the type of the token and of the reported ranges.
- **.NET base class library** - `System.Text.RegularExpressions` (including the source generator), Unicode category
  and rune information, and collections. No other unit, subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - calls `IsRandom` for each token of at least `minRandomLength`
  characters, and `FindHexSequences` for a token that produced findings, when `ignoreRandomStrings` is on.
