### DefaultPatterns

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`DefaultPatterns` finds the text that is never spell-checked because it is not prose: URLs, email addresses,
certificates and keys, encoded data, hashes, hexadecimal values, Unicode references, and UUIDs. Reporting the
fragments of such strings as unknown words would bury real findings. The set of patterns is the default ignore list of
cspell, ported so that a project migrated from cspell stays quiet where it was quiet.

#### Data Model

`DefaultPatterns` is an `internal static partial` class. Its state is fixed for the life of the process: an array of
generated `Regex` objects in the order cspell lists them, exposed read-only as `All`, and the constant
`MatchTimeoutMilliseconds` (5000), the longest time one pattern may spend on one text. A match is reported as a
`TextRange` (see _WordTokenizer_). The fourteen patterns are, with the letter case they use in brackets:

- **URLs** (insensitive): `http`, `https`, or `ftp`, then `://`, then everything up to white space or a double quote.
  `www.` alone is not matched.
- **Email** (insensitive): an address, optionally in angle brackets, with a local part of up to 128 characters and two
  to five domain labels of up to 63 characters.
- **PEM block** (sensitive): a certificate or a public or private key from its BEGIN line to the END line of the same
  kind.
- **SSH key** (insensitive): `ssh-rsa`, white space, and a key of at least 28 Base64 characters with up to three
  padding characters.
- **Base64, several lines** (sensitive): lines of at least 40 Base64 characters, optionally quoted, with an optional
  shorter last line.
- **Base64, one line** (sensitive): a line of at least 40 Base64 characters that looks encoded rather than like words:
  a digit, plus, or slash early, a capital between lower-case letters, a digit between letters, and a run of three
  letters of the same case.
- **Commit hash** (insensitive): seven or more hexadecimal digits, optionally after `0x`, that are not all letters.
- **Commit hash link** (insensitive): seven or more hexadecimal digits in square brackets.
- **C-style hex literal** (insensitive): `0x`, hexadecimal digits and underscores, and an optional `n`.
- **CSS color** (insensitive): a number sign and three to eight hexadecimal digits.
- **SHA digest** (insensitive): `sha`, a number, a hyphen, and at least 25 Base64 characters.
- **Hash string** (insensitive): a scheme name (`sha256`, `md5`, `base64`, `crypt`, `bcrypt`, `scrypt`,
  `security-token`, `assertion`) and a separator, or `#code/`, then at least 25 data characters, possibly continued in
  concatenated quoted strings.
- **Unicode reference** (insensitive): `U+` and four or five hexadecimal digits, optionally a range.
- **UUID** (insensitive): the 8-4-4-4-12 form, where `x` may stand for a digit.

#### Key Methods

**FindExcludedRanges**: Appends the ranges of a text that match any default pattern to a list.

- _Parameters_: the text and the list that receives the ranges.
- _Algorithm_: run every pattern over the whole text with the allocation-free match enumeration, and append the
  index and length of each match. A pattern cannot match the empty string, so every match has a length.
- _Postconditions_: the list holds its earlier items followed by the ranges, pattern by pattern. They are neither
  sorted nor free of overlaps; the caller merges them, because only the caller knows what else is excluded.

**All**: The generated patterns, so that the time limit that bounds every one of them can be verified in one place.

The patterns are ports of the JavaScript originals, and the two regular expression engines differ. The translation
rules, each covered by a unit test, are:

- In JavaScript, without the Unicode flag, `\w`, `\d`, and `\b` are ASCII-only, while in .NET they are
  Unicode-aware. Every pattern therefore spells out ASCII classes (`[A-Za-z0-9_]`, `[0-9]`) and an explicit ASCII word
  boundary built from look-around assertions. A hash next to an accented letter is matched, as in cspell, and a hash
  next to an
  ASCII letter is not.
- `^` in JavaScript multiline mode starts a line after any of `\n`, `\r`, U+2028, and U+2029, and `.` excludes all four.
  The patterns spell out an explicit line start and use negated classes instead of the dot.
- Patterns that cspell applies case-insensitively use the case-insensitive option with the invariant culture, so the
  result does not depend on the culture of the machine. The encoded-data patterns stay case-sensitive, as in cspell.

Design choices and their reasons:

- **Generated regular expressions, not scanners.** The patterns are cspell's own and were designed as regular
  expressions with look-around assertions. Porting each to a hand-written scanner would make the port impossible to
  review against
  the original and would risk subtle differences in what is matched. The patterns run once per file, not once per
  word, so the cost of a regular expression is acceptable. The source generator compiles them at build time, which
  keeps the library safe for trimming and ahead-of-time compilation and avoids the cost of building them at start-up.
- **One pattern at a time, not one combined alternation.** A combined pattern would stop at the first alternative that
  matches at a position and would make the time limit apply to all patterns together. Running each pattern separately
  keeps every match the pattern finds, and the caller merges the ranges.
- **A match time limit on every pattern.** A repository can contain a file built to make a pattern backtrack
  catastrophically, and a run that never finishes is worse than a file reported as not checked. Five seconds is far
  above the time any legitimate file needs and far below the time a build would be held up. Where the pattern allows
  it, repetition counts are also bounded (the length limits of an email address, for example), which limits the work
  before the time limit is reached.
- **Ranges appended to a list.** The same reasons as for the other units of the subsystem: no allocation per call,
  and the caller combines several sources into one list.

#### Error Handling

A null text or list is a programming error and throws `ArgumentNullException`. Hostile text that makes a pattern
exceed its time limit throws `RegexMatchTimeoutException`; the ranges found so far remain in the list, but the caller
must treat the text as not checked, and `SpellChecker` reports the file as timed out. No other error is possible:
the patterns cannot fail on any text. The class is stateless and thread-safe; the generated regular expressions are
immutable and safe for concurrent matching, and only the list passed in is changed, which must not be shared between
threads.

#### Dependencies

- **.NET base class library** - `System.Text.RegularExpressions`, including the source generator. No other unit,
  subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - calls `FindExcludedRanges` once per text and merges the ranges with
  those of the directives.
- **DocumentDirectives** (same subsystem) - reads `MatchTimeoutMilliseconds`, so the directive patterns share the
  time limit of the default patterns.
- **PatternParser** (same subsystem) - reads `MatchTimeoutMilliseconds` as its own limit, which `PatternCatalog`,
  `MarkdownPatterns`, and `RandomStringDetector` in turn read from it, so one constant bounds every regular expression
  of the subsystem.
