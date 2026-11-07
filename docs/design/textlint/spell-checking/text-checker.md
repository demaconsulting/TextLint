### TextChecker

![SpellChecking Structure](SpellCheckingView.svg)

#### Purpose

`TextChecker` checks the spelling of one string of text against the dictionary of the configured language and the
word lists and patterns of a settings value, and returns every finding with its line and column. It is the single place
that holds the word-level rules, so a string checked by an editor and a file checked by a run give the same answers. It
also defines the finding types `SpellingIssue` and `SpellingIssueKind`, and the internal word-list types
`WordRules`, `WordSet`, and `FlagSet`, which have no unit of their own.

#### Data Model

**TextChecker** is a public sealed class. Its fields are the shared `WordRules` built from the settings in the
constructor; the regular expressions of the `ignoreRegExpList` setting, resolved once into an immutable array
(`_customPatterns`); the parsed definitions of the `patterns` setting by name (`_definitions`, an immutable
dictionary compared ignoring letter case), kept so that an in-document `ignoreRegExp` directive can name them; the match
time limit (`_matchTimeout`, exposed to tests as the internal `MatchTimeout` property); and the two random-string
settings (`_ignoreRandomStrings` and `_minRandomLength`). The internal constant `MaxDirectivePatterns` (32) is the
largest number of different patterns that the directives of one text may use. The
checker keeps no reference to the settings, copies the word lists into sets, compiles the patterns, and never changes
after construction, so it is immutable and safe to share between threads.

**SpellingIssue** is an immutable public class with `FilePath` (the source name, or null), `Line` and `Column`
(1-based; the column counts UTF-16 code units), `Text` (the word as written, the whole token for a flagged entry
that contains digits or punctuation, the name of an unsupported directive, or the pattern of an invalid directive as
written), `Kind`, `Suggestions` (an
immutable list, empty unless the finding is a flagged word whose entry gave suggestions), and `Detail` (the reason an
invalid directive cannot be used, as a sentence; null for the other kinds). Its constructor is
internal because only the checker produces findings. `ToString` formats `file(line,column): error: message` in the
compiler style, with the suggestions appended to the message, and just `(line,column)` when there is no file name. The
message of an invalid directive is `Cannot use the pattern '<text>' of the directive 'ignoreRegExp': <reason>`.
`Format(filePath)` does the same under a file name chosen by the caller, and `ToString` is `Format` applied to the
recorded `FilePath`; a caller that was given absolute paths uses it to show them without copying the message wording.
Findings are values rather than exceptions so that a consumer can print or process every one the same way.

**SpellingIssueKind** is an enumeration of the four kinds: `UnknownWord`, `FlaggedWord`, `UnsupportedDirective`, and
`InvalidDirective`. An invalid directive is a directive that the library supports but whose argument cannot be used:
an `ignoreRegExp` directive whose pattern is invalid, uses an unsupported flag or construct, or names no pattern.
It has no effect, and the `Detail` of the finding says why.

**WordRules** is an internal immutable class that answers the word-level questions: is a word flagged, is it
accepted, is it the capital run of an acronym with an ending, is it a contraction whose parts are acceptable, is it a
repeated letter. It holds the
dictionary, the minimum length, and three sets. An instance can be layered over another with the words that a
document's directives add (`WithDocumentWords`), sharing the parent's dictionary, minimum length, and flag set and
chaining the lookups, so a document with a directive does not copy the project's lists.

**WordSet** is an internal immutable set of accepted or ignored entries, compared without regard to letter case. It
has one table for single words and one for entries that contain digits or punctuation, and both are looked up with a
span of the checked text, so the lookup does not allocate.

**FlagSet** is an internal immutable set of flagged entries with their suggestions. An entry written entirely in lower
case matches in any letter case; an entry with a capital letter matches only that exact spelling, as in cspell. When
an entry is repeated, the first is kept.

**CheckRun** is a private class created for each call to `Check`. It holds the text, the rules in force, the random-string
settings, the findings so far, and four scratch lists (tokens, words, pieces, and the hexadecimal sequences of a token)
that are reused across the call to avoid allocating for each word. It never leaves the call that created it.

**Finding** is a private record struct of an offset, a length, a kind, suggestions, and the optional detail of an
invalid directive; it is converted to a
`SpellingIssue` only at the end, when the offsets are turned into lines and columns.

#### Key Methods

**Constructor (public)**: Builds a checker from settings.

- _Parameters_: `TextLintSettings settings`.
- _Preconditions_: the settings are not null, the language is supported, every entry of `words`,
  `ignoreWords`, and `flagWords` can match something, and every entry of `ignoreRegExpList` and `patterns` can be used.
- _Postconditions_: the checker is immutable. The dictionaries of the language are loaded lazily, when the first word
  is looked up, not here. The match time limit is 5 seconds.

**Constructor (internal, with a match time limit)**: Builds a checker with a given time limit. The public constructor
calls it with the production limit of 5 seconds (`PatternParser.MatchTimeoutMilliseconds`), so the two differ only in
the limit. It is a test seam: tests pass a few milliseconds to prove the time-out behavior without waiting for the real
limit, and a test proves that a checker made by the public constructor reports 5 seconds through the internal
`MatchTimeout` property. The limit applies to every pattern that is built at run time, which are the patterns of the
settings and those of the directives; the generated patterns keep their own fixed limit of 5 seconds. A limit that is
not positive throws `ArgumentOutOfRangeException`.

Steps: (1) resolve the language with the spelling dictionary, converting its exception into an `ArgumentException`
that names the language and lists the supported names, so an unsupported language is reported before any list
entry; (2) interpret every entry of the three lists (the flag list also accepts the suggestion forms `word->a, b`
and `word: a`) and reject one that could never match, naming the setting and the entry; (3) build the sets and the
rules; (4) parse the definitions of `patterns` with `PatternCatalog.ParseDefinitions` and resolve the entries of
`ignoreRegExpList` with `PatternCatalog.ResolveEntries`, both with the time limit, which parse every pattern and check
every name, so an `InvalidPatternException` that names the entry and the reason is thrown for an unusable one, used or
not, after the word lists have been accepted; (5) keep the compiled patterns, the parsed definitions, and the two
random-string settings. Settings
built in code bypass the settings file loader's checks, which is why the constructor repeats them, with the same
rules, so a pattern accepted when a file is loaded is never rejected here.

**Check (public)**: Checks a text and returns its findings.

- _Parameters_: `string text`, `string? sourceName` (default null). The source name is put on each finding and also
  decides whether the Markdown patterns apply.
- _Returns_: a read-only list of `SpellingIssue` in text order; never null; empty for an empty text.
- _Preconditions_: the text is not null.
- _Postconditions_: the text is not modified; nothing is read or written; the list is immutable.

Steps: (1) scan the text for directives, which yields the regions that are switched off, the text of the directives
that add words or patterns, the words they add, the patterns of the `ignoreRegExp` directives, and the unsupported
directives; (2) add the ranges that are never checked: the
default patterns (URLs, email addresses, hexadecimal values, hashes, encoded data), the Markdown patterns when
`MarkdownPatterns.AppliesTo` accepts the source name (link targets, labels, anchors, and entities), the patterns of
the settings, and the patterns of the directives (see _ApplyDirectivePatterns_), each run over the whole text by
`PatternCatalog.FindExcludedRanges`, so a pattern can span lines and can cut a word in two; then sort all excluded
ranges by position; (3) when the directives added words, layer them over the
shared rules; (4) check each stretch of text between excluded ranges as a separate segment, so a word that an excluded
range cuts in two is checked as two pieces; (5) add one finding for each unsupported directive and one for each
invalid directive pattern; (6) convert offsets to lines and columns.

**ApplyDirectivePatterns (private)**: Applies the patterns of the `ignoreRegExp` directives of one text.

- _Parameters_: the text, the `PatternDirectives` of the scan, and the list of excluded ranges that receives the
  matches.
- _Returns_: the patterns that cannot be used and are to be reported, each with its range and reason, in text order.
- _Postconditions_: a pattern applies to the whole text, wherever its directive is, also when the directive is inside a
  disabled region (there it is applied but not reported, because `IsReportable` is false). The text of the directive
  itself is never checked, because the scan excludes it; a pattern that cannot be used hides nothing else.
- _Algorithm_: for each directive in text order: (1) when its pattern text was already seen, reuse the earlier outcome,
  so each DISTINCT pattern (see the key below) is resolved and matched once, however many directives
  repeat it, while every repetition is still reported at its own position. The key is the one the lookup uses: an
  entry that has the shape of a pattern name is keyed by its trimmed text compared ignoring letter case (the catalog
  resolves a name ignoring letter case, so `Name`, `name`, and `NAME` are one pattern and use one of the 32), and any
  other entry is keyed by its exact text, because a literal or bare expression is case-significant; the trim is the
  JavaScript trim, not the .NET one; (2) otherwise, when 32 different patterns
  were already seen (`MaxDirectivePatterns`), the pattern is rejected without being read, with the reason `a text may
  use at most 32 different ignoreRegExp patterns.`; (3) otherwise resolve it with `PatternCatalog.TryResolveEntry`, like
  an `ignoreRegExpList` entry: a literal `/body/flags` with the flags as written, a bare expression read with `i` and
  `m`, the name of a definition of the `patterns` setting, or a predefined name; a regular expression that results
  is run over the text with `PatternCatalog.FindExcludedRanges`, and a failure gives the reason; (4) when the outcome is
  a reason and the directive is reportable, add the range and the reason to the result.
- _Constraints_: a pattern that exceeds the time limit throws `RegexMatchTimeoutException` (see _Error Handling_).

The rule for patterns differs from cspell in three deliberate ways: a pattern that cannot be used is reported (cspell
drops it silently); a plain name that is neither defined nor predefined is an error (cspell reads it as a literal
regular expression); and an `ignoreRegExp` directive with nothing after its name is reported as an unsupported directive.
A pattern that cspell would also use is applied in the same way, to the whole document. The number of patterns is
bounded because a directive is written by whoever wrote the text.

**CheckRun.CheckToken / CheckWord / CheckPiece (private)**: The word-level algorithm for one segment.

A segment is cut into tokens, the units that the entries with digits or punctuation are matched against and within
which words are found (see _TextScanning Design_). For each token:

1. When `ignoreRandomStrings` is on and the token has at least `minRandomLength` characters (UTF-16 code units) and
   `RandomStringDetector.IsRandom` judges it random, the token is skipped before anything else, as in cspell, so even a
   flagged word inside it is not reported and no word of it is looked up.
2. If any list has an entry with digits or punctuation, the token with its edge punctuation trimmed is matched as a whole
   against those entries: ignored or accepted ends the token, flagged reports the whole token.
3. Otherwise the token is cut into words. For each word: (a) a flagged word is reported whatever its length, unless it is
   also ignored; (b) a word shorter than the minimum length, or accepted, passes; accepted means in the dictionary or a
   list, as the possessive of such a word, without a backslash escape or without the escape letter after a backslash, or
   as a run of capitals with an ending whose capitals are accepted or too short to check; (c) an unknown word is split at
   case changes and each piece is checked on its own: a piece that is not flagged passes when it is too short, accepted,
   or a contraction whose parts are acceptable, and otherwise, unless it is one repeated letter, it is reported, as a
   flagged word when it is flagged and as an unknown word when it is not.
4. When `ignoreRandomStrings` is on and the token produced findings, `RandomStringDetector.FindHexSequences` finds the
   hexadecimal sequences of the token, and every unknown-word finding that starts inside one is removed. A flagged-word
   finding is always kept. This step does not run for a token that ended in step 2, and it applies whatever the length of
   the token.

The precedence is therefore ignored, then flagged, then accepted words, then dictionary, and a flagged word shorter than
the minimum is still reported. Checking the whole word before splitting it is what keeps a known compound identifier
from being taken apart; splitting only unknown words, at case changes only, is the deliberate simplification of cspell's
heuristic splitter.

**Contractions.** The pieces of a word that has apostrophes keep them (`exec'd`, `OR'd`, `rock'n'roll`), and a piece that
is neither too short nor accepted as a whole is tested with `WordRules.IsContractionAccepted`. The method splits the
piece at each straight or typographic apostrophe; a backslash that ends a part belongs to the escaped apostrophe, not to
the part, and is removed; an empty part is ignored; and the first part is tested as following a backslash only when the
word does. A part is acceptable when it is not flagged and is too short to be checked, accepted, or a run of capitals
with an accepted ending. The word is accepted when it has at least one apostrophe and every part is acceptable; one
unacceptable part, including a flagged one, leaves the word unknown, and it is reported whole, apostrophes included.
The test comes after the whole-word checks, so a word that is known as a whole never reaches it.

**ToIssues (private)**: Converts findings to issues.

The findings are in text order except that unsupported directives and invalid directives are appended last, so they are
sorted (stably) only when there is one of either; the invalid ones are therefore merged in text order with the other
findings. The conversion walks the text once, counting line feeds (a carriage return alone is not a
line break, as in cspell) and measuring the column in UTF-16 code units from the last line feed.

**InterpretEntries (private)**: Interprets one list. It uses the tokenizer's entry parser and throws an
`ArgumentException` that names the setting and the entry for any entry that is blank, contains white space or a
character that cannot be part of a word, begins with `!` (the cspell forbidden-word marker, which would otherwise be
mistaken for an accepted word), or has no letter or digit.

**CheckRun.RemoveFindingsInHexSequences (private)**: Applies the hexadecimal filter to one token.

- _Parameters_: the token and the index of the first finding that the token produced.
- _Algorithm_: ask `RandomStringDetector.FindHexSequences` for the sequences of the token; when there are none, stop;
  otherwise keep each finding of the token that is a flagged word or that starts outside every sequence, and drop the
  rest. Findings of earlier tokens are never touched.

#### Error Handling

A null settings value or text throws `ArgumentNullException`, and a match time limit that is not positive throws
`ArgumentOutOfRangeException` from the internal constructor. An unusable language or word-list entry throws
`ArgumentException` from the constructor, naming the language, or the setting and the entry. An unusable pattern, in
`ignoreRegExpList` or `patterns`, throws `InvalidPatternException` (a kind of `ArgumentException`) from the constructor,
naming the entry and the reason. An unusable pattern in an `ignoreRegExp` directive of a text is not an exception: it is
data that is reported as an `InvalidDirective` finding at the pattern, with the reason in `Detail`, and the rest of the
text is checked as usual (the finding is omitted inside a region where checking is switched off). The reasons are those
of `PatternParser` and `PatternCatalog` (an invalid expression, an unsupported flag or construct, or a name that is
neither defined nor predefined), or the sentence that the text uses more than 32 different patterns, which rejects the
excess without reading it. Checking itself has no other expected failure. Two failures can still occur and are not
caught here, because only the caller knows what to do with them: a `RegexMatchTimeoutException` when any ignore pattern,
whether a default pattern, a Markdown pattern, a pattern of the settings, a pattern of a directive, or the hexadecimal
pattern, exceeds its time limit
on hostile text (the caller should treat the text as not checked; `SpellChecker` reports the file as timed out,
and the findings found so far are discarded with the exception), and an `InvalidDataException` when an embedded
dictionary is missing or damaged, which means the build of the library is defective. Findings are never thrown. Nothing
is logged.

The time limit is a safety net, not a guarantee of speed. Every pattern that is built at run time may cost up to the
limit (5 seconds in production) on one text before the file is reported as timed out, and the first time-out aborts the
check of that file. A file is bounded by the patterns that apply to it: the patterns of the settings, and at most 32
different patterns that its own directives add, so in the very worst case a file built to trigger them costs up to
(the number of patterns of the settings + 32) x 5 seconds. A pattern repeated by several directives is run once.
The patterns that are generated at build time keep a fixed limit of 5 seconds and are not covered by the seam.

#### Dependencies

- **TextLintSettings** (Configuration subsystem) - the language, the three word lists, the minimum word length, the
  `ignoreRegExpList` and `patterns` lists (the definitions also serve the names that a directive writes), and the two
  random-string settings.
- **SpellingDictionary** (Dictionaries subsystem) - the language selection and the word lookup.
- **WordTokenizer**, **DefaultPatterns**, and **DocumentDirectives** (TextScanning subsystem) - the tokens, words, and
  pieces; the text that is never checked; and the directives, including the patterns of `ignoreRegExp` directives. See
  _TextScanning Design_.
- **PatternCatalog**, **PatternParser**, **MarkdownPatterns**, and **RandomStringDetector** (TextScanning subsystem) - the
  resolved patterns of the settings and of the directives and the code that runs them; the match time limit; the
  patterns that apply to Markdown sources; and the judgement
  of random-looking tokens and hexadecimal sequences. `InvalidPatternException` and `NamedPattern` come with them.
- **.NET BCL** - collections, spans, and regular expressions (the compiled patterns are held as `Regex` values).

#### Callers

- **SpellChecker** - creates one checker for a run and calls it from every worker for the text of each file.
- **Library consumers** - editors, build steps, and tests that check a string, usually with
  `new TextChecker(settings).Check(text, name)`.
- **Tests** - use the internal constructor with a short time limit.
