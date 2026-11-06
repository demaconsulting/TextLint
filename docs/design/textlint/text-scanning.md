## TextScanning

![TextScanning Structure](TextScanningView.svg)

### Overview

The `TextScanning` subsystem decides what the checkable parts of a text are. It answers five questions about a
string: where are the words, tokens, and case-change pieces; which parts are not prose and must never be checked
(URLs, email addresses, hashes, encoded data, the text that the project's own patterns match, and the link targets of
a Markdown document); which tokens look like random data, such as keys, and which hexadecimal sequences lie inside a
token; which parts the author has switched off or added words or patterns for with in-document directives; and how an
entry of a
word list is to be read so that it can be matched. It contains seven units: `WordTokenizer`, which decides what a word
is and interprets list entries; `DefaultPatterns`, which finds the text that is not prose; `DocumentDirectives`, which
interprets directives such as the disable directive and finds the patterns of `ignoreRegExp` directives;
`PatternParser`, which turns a pattern written the way cspell
writes it into a .NET regular expression that has its JavaScript meaning (with `JavaScriptRegexTranslator`, which does
the translation, and `InvalidPatternException`, which reports an unusable pattern); `PatternCatalog`, which knows the
predefined pattern names and turns the `ignoreRegExpList` and `patterns` settings into regular expressions
(with `NamedPattern`); `MarkdownPatterns`, which finds the link targets, labels, anchors, and entities of a Markdown
document; and `RandomStringDetector`, which recognizes tokens that look random and hexadecimal sequences. The
supporting types `TextRange`, `ListEntry` (with `WordTokenizer`) and `DirectiveScan` (with `DocumentDirectives`) are
documented with the unit that produces them.

The subsystem is pure computation over strings. It reads no files, writes no output, holds no state, and uses no
operating-system service. It does not know the dictionaries, the settings, or the word rules; it only reports ranges
and words, and the `SpellChecking` subsystem decides what to do with them. All members are `internal` except two
types: `NamedPattern`, the pair of a name and a pattern that `TextLintSettings` holds, and `InvalidPatternException`,
which a caller that builds settings in code receives when a pattern cannot be used. Both are public because they appear
in the public API of the library.

### Interfaces

All members are in-process .NET static methods, so every unit is stateless and every call is independent.

**WordTokenizer.FindTokens, FindWords, SplitCase, TrimToken**: Find ranges in a text.

- _Role_: Provider. Each method takes the text and the part of it to scan, and an out-buffer, a `List<TextRange>`,
  that receives the result.
- _Contract_: Results are `TextRange` values (UTF-16 offset and length into the original text), appended to the
  buffer, which is never cleared. `FindTokens` reports maximal runs of letters, digits, and the punctuation of tokens;
  `FindWords` reports runs of letters; `SplitCase` splits one word at case changes; `TrimToken` removes edge punctuation
  from one token and returns a range.
- _Constraints_: Throw `ArgumentNullException` for a null text or buffer and `ArgumentOutOfRangeException` for a part
  outside the text. Nothing else can fail, whatever the text holds.

**WordTokenizer.TryParseListEntry**: Interprets one entry of a word list.

- _Contract_: Returns `true` and a `ListEntry` (the normalized word, whether it is a single word, and its suggestions),
  or returns `false` and a reason in words. Suggestions are accepted only when the caller allows them.
- _Constraints_: Never throws; a null entry is reported as blank.

**WordTokenizer.IsWordLetter, CountCharacters**: Classify a code point and measure a text in characters.

**DefaultPatterns.FindExcludedRanges**: Finds the text that is not prose.

- _Contract_: Appends the ranges of every match of every default pattern to a buffer. The ranges are reported pattern
  by pattern, so they are neither sorted nor free of overlaps; the caller merges them.
- _Constraints_: Throws `ArgumentNullException` for a null argument and `RegexMatchTimeoutException` when a pattern
  exceeds its time limit on the text. `DefaultPatterns.All` and `MatchTimeoutMilliseconds` expose the patterns and their
  time limit so that the limit can be verified in one place.

**DocumentDirectives.Scan**: Interprets the directives of a text.

- _Contract_: Returns a `DirectiveScan` with the excluded ranges (not sorted, possibly overlapping), the words added by
  `ignore` and by `words` directives, the patterns written by `ignoreRegExp` directives (`PatternDirectives`: the range
  and text of each pattern as written, read from the line the way cspell reads it, and whether an unusable pattern
  there is to be reported), and the ranges of the names of directives that are recognized but not supported, which
  include an `ignoreRegExp` directive with nothing after its name on its line. A text without directives returns the
  shared `DirectiveScan.Empty`.
- _Constraints_: Throws `ArgumentNullException` for a null text and `RegexMatchTimeoutException` when a pattern
  exceeds its time limit.

**PatternParser.Parse**: Reads one pattern entry.

- _Contract_: Accepts an entry, `/body/flags` or a bare expression, and returns a .NET regular expression that has the
  JavaScript meaning of the pattern, built with the invariant culture and a five-second match time limit. The flags
  `g`, `i`, `m`, `s` (the dot also matches line terminators), and `u` are accepted and every other flag (`x`, `y`, `d`,
  `v`) is rejected by name. An internal overload takes the time limit, so that tests can prove the time-out behavior
  in milliseconds; production code never passes one.
- _Constraints_: Throws `ArgumentNullException` for a null entry, `ArgumentOutOfRangeException` for a limit that is not
  positive, and `InvalidPatternException`, naming the entry and the
  reason, for a blank entry, an unsupported flag, a body that is too long (more than 4096 characters), nested too deep
  (more than 100 levels), or untranslatable. `JavaScriptRegexTranslator.TryTranslate` does the translation and returns
  the reason as data instead of throwing.

**PatternCatalog.Resolve, ParseDefinitions, ResolveEntries**: Turn the pattern settings into regular expressions.

- _Contract_: `ParseDefinitions` parses the definitions of `patterns` (`NamedPattern` values) into a dictionary by
  name, ignoring letter case. `ResolveEntries` accepts the entries of `ignoreRegExpList` and the parsed definitions, and
  returns an immutable array with one regular expression for each pattern that excludes text, in list order. `Resolve`
  does both with the production time limit. An entry that has the shape of a name is a defined name, one of the nine
  predefined patterns that apply only when listed, or one of the nineteen predefined names of patterns that always
  apply, which add nothing; any other entry is a pattern. The two newer methods take the match time limit, an internal
  seam for tests.
- _Constraints_: Throw `ArgumentNullException` for a null argument and `InvalidPatternException` for the first
  definition or entry that cannot be used: an invalid, predefined, or repeated definition name, a name that is not
  defined, or a pattern that cannot be parsed. Every definition is checked, used or not.

**PatternCatalog.TryResolveEntry**: Resolves one entry to a regular expression, with the failure as data.

- _Contract_: Accepts one entry, written in `ignoreRegExpList` or in an in-document directive, and the parsed
  definitions, and returns `true` with the regular expression (null for a predefined name that always applies), or
  `false` with the reason as a sentence. It resolves in the same way as `ResolveEntries`: a pattern is parsed, and a
  name is looked up among the definitions, then the predefined patterns.
- _Constraints_: Throws `ArgumentNullException` for a null argument; an unusable entry is never thrown, so that the
  caller can report it where it was written.

**PatternCatalog.IsNameShaped, IsPredefined, CheckDefinitionName, CheckReference**: Classify and check names.

- _Contract_: Report whether an entry has the shape of a name, whether a name is predefined (ignoring letter case), and,
  as a sentence or null, whether a name may be given to a definition or refers to a pattern. They let the settings
  loader report the same problems as `Resolve`, with a position. The sentence for a name that refers to no pattern is
  built in one place, which `TryResolveEntry` shares.
- _Constraints_: Throw `ArgumentNullException` for a null argument; nothing else can fail.

**PatternCatalog.FindExcludedRanges**: Finds the text that a set of patterns matches.

- _Contract_: Appends the ranges of every match of every given regular expression, skipping matches with no characters,
  to a buffer. The ranges are reported pattern by pattern, so they are neither sorted nor free of overlaps; the caller
  merges them. The same method runs the patterns of the settings and those of `MarkdownPatterns`.
- _Constraints_: Throws `ArgumentNullException` for a null argument and `RegexMatchTimeoutException` when a pattern
  exceeds its time limit on the text. `PredefinedNames` and `OptionalRegexes` expose the names and the nine optional
  patterns so that the list and the time limit can be verified in one place.

**MarkdownPatterns.AppliesTo, All**: Finds the parts of a Markdown document that are not prose.

- _Contract_: `AppliesTo` says from a source name alone whether it ends in one of the ten Markdown extensions, ignoring
  letter case, and is false for a null name. `All` holds the five regular expressions (link reference label, reference
  definition, link target, anchor id, and lower-case HTML entity), which `PatternCatalog.FindExcludedRanges` runs.
- _Constraints_: `AppliesTo` never throws. Every expression has the five-second time limit.

**RandomStringDetector.IsRandom, FindHexSequences**: Recognize random data.

- _Contract_: `IsRandom` says whether a token, reduced to one symbol per run of digits, word, run of capitals, and run of
  separators, is at least half as long as the token. `FindHexSequences` appends to a buffer the ranges, as offsets into
  the whole text, of the hexadecimal sequences of a token that have at least eight characters, one letter case, and a
  digit or a hyphen, and are not next to a letter.
- _Constraints_: `IsRandom` cannot fail and takes time linear in the token. `FindHexSequences` throws
  `ArgumentNullException` for a null text or buffer, `ArgumentOutOfRangeException` for a token outside the text, and
  `RegexMatchTimeoutException` when its pattern exceeds the time limit.

**Dependencies on other subsystems**: None. The `TextScanning` subsystem uses only the .NET base class library and
depends on nothing else in the library. It is used by two subsystems: `SpellChecking`, whose `TextChecker` combines
the units, and `Configuration`, whose `SettingsLoader` calls `WordTokenizer.TryParseListEntry` to validate word-list
settings and `PatternParser` and `PatternCatalog` to validate the pattern settings at load time, and whose
`TextLintSettings` holds `NamedPattern` values.

### Design

The units are independent of each other except for the dependencies of the pattern units. A caller that checks a text asks
`DocumentDirectives`, `DefaultPatterns`, the patterns of the settings (resolved once by `PatternCatalog` from
`ignoreRegExpList` and `patterns`), and, for a Markdown source, `MarkdownPatterns` for ranges that are not to be
checked, merges them, and asks `WordTokenizer` for the words in what remains. `RandomStringDetector` is then asked about
each token: whether it looks random and is to be skipped, and where its hexadecimal sequences are. The pattern units form
a small chain: `PatternCatalog` parses with `PatternParser`, which uses `JavaScriptRegexTranslator` and reads the match
time limit from `DefaultPatterns`, and `MarkdownPatterns` and `RandomStringDetector` read the same limit from
`PatternParser`, so one constant bounds every regular expression of the subsystem. Nothing in the subsystem merges the
ranges or removes the excluded words, because the owner of that decision, the word rules in `SpellChecking`, also owns
the order of precedence between ignored text, flagged words, and accepted words.

Three implementation styles are used, each where it fits. Deciding what a word is runs for every character of every
file, so `WordTokenizer` is a hand-written scanner over the string, with no regular expressions and no allocation
per word; see its design for the reasons. Judging whether a token looks random is also hand-written, over code points,
because it runs for every long token and a scanner has a cost linear in the token with no pattern that could backtrack.
Recognizing a URL, a hash, a directive, a Markdown link target, or one of the predefined patterns is a question about a
pattern that cspell already answers with a regular expression, and the library must find the same text. Those units
therefore port the cspell patterns to .NET regular expressions generated at build time, once per file rather than once
per word, with a match time limit. Keeping the cspell patterns recognizable, instead of rewriting them as scanners,
makes the port reviewable pattern by pattern against the cspell originals and keeps a migrated project quiet where it
was quiet. The patterns of a settings file are the third style: they are not known when the library is built, so
`PatternParser` translates each from the JavaScript syntax to .NET, giving every construct its JavaScript meaning or
rejecting it with a reason, and constructs the regular expression at run time with the same match time limit. The
translation is strict on purpose, because a pattern passed through unchanged would silently exclude different text than
in cspell.

Results are reported as offsets into the original string and written into buffers that the caller supplies. A caller
that scans many files can reuse one buffer, no result needs a copy of the text, and the same offsets give the line and
column of a finding. Offsets count UTF-16 code units, which is how .NET strings are indexed and how cspell reports
columns, so a character outside the basic multilingual plane counts as two.

Every directive that cspell supports and this library does not is reported instead of being silently ignored. The
subsystem differs from cspell in four ways on purpose. It has no counterpart to cspell's heuristic word splitter,
which re-segments an unknown word with the help of the dictionary: words are split only at case changes, so the
subsystem stays a pure function of the text that does not depend on a dictionary. It treats a word-list entry as
something to be validated: an entry that could never match is rejected with a reason when settings are read, where
cspell silently keeps it. It treats a pattern as something to be validated in the same way: an unsupported flag or
construct, an invalid regular expression, or a name that is not defined is an error, where cspell either drops the flag,
lets the JavaScript engine decide, or reads the unknown name as a literal regular expression; this holds for the
patterns of in-document `ignoreRegExp` directives too, which cspell drops without a word when they are invalid and
which the library finds and reports at their position (the directive itself is read exactly as cspell reads it, and its
pattern applies to the whole document). And it bounds every regular expression with a match time limit, where cspell
has none.

The time limit is a safety net with stated limits. It stops one match operation of one pattern on one text after five
seconds with a `RegexMatchTimeoutException`, so a pattern that backtracks catastrophically on a hostile file cannot hang
a run, but it does not stop the processor being busy for those five seconds, so a file built to trigger every pattern can
cost up to five seconds for each of them. The first time-out aborts the file, and the number of patterns that can apply
to a file is bounded (the patterns of the settings, and at most 32 different patterns written by the directives of the
file, which `TextChecker` enforces), so the worst case for one file is (settings patterns + 32) x 5 seconds. The length
and nesting limits of `PatternParser` bound the work of reading a pattern, which the time limit does not cover. The
limit is 5 seconds in production; internal overloads let the tests use a short one, and the generated patterns keep
their fixed limit.

Every unit is thread-safe because every unit is stateless. The generated and constructed regular expressions are
immutable and safe for concurrent matching, the shared `DirectiveScan.Empty` and all other results are immutable, and
the only mutable objects are the buffers that callers pass in, which a caller must not share between threads. Errors are
of three kinds: a caller that violates a precondition gets an argument exception at once, a pattern that cannot be used
gets an `InvalidPatternException` that names the entry and the reason (which the settings loader reports as a positioned
diagnostic instead of throwing), and hostile text gets either a safe result or, for the units that use regular
expressions, a `RegexMatchTimeoutException` that the caller turns into a finding for the file. Malformed text, such as a
lone surrogate, is never an error.
