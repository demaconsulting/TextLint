## TextScanning

![TextScanning Structure](TextScanningView.svg)

### Overview

The `TextScanning` subsystem decides what the checkable parts of a text are. It answers four questions about a
string: where are the words, tokens, and case-change pieces; which parts are not prose and must never be checked
(URLs, email addresses, hashes, encoded data); which parts the author has switched off or added words for with
in-document directives; and how an entry of a word list is to be read so that it can be matched. It contains three
units: `WordTokenizer`, which decides what a word is and interprets list entries; `DefaultPatterns`, which finds the
text that is not prose; and `DocumentDirectives`, which interprets directives such as the disable directive. The
supporting
types `TextRange`, `ListEntry` (with `WordTokenizer`) and `DirectiveScan` (with `DocumentDirectives`) are documented
with the unit that produces them.

The subsystem is pure computation over strings. It reads no files, writes no output, holds no state, and uses no
operating-system service. It does not know the dictionaries, the settings, or the word rules; it only reports ranges
and words, and the `SpellChecking` subsystem decides what to do with them. All members are `internal`, so the
subsystem is not part of the public API of the library.

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
  `ignore` and by `words` directives, and the ranges of the names of directives that are recognized but not supported.
  A text without directives returns the shared `DirectiveScan.Empty`.
- _Constraints_: Throws `ArgumentNullException` for a null text and `RegexMatchTimeoutException` when a pattern
  exceeds its time limit.

**Dependencies on other subsystems**: None. The `TextScanning` subsystem uses only the .NET base class library and
depends on nothing else in the library. It is used by two subsystems: `SpellChecking`, whose `TextChecker` combines
the three units, and `Configuration`, whose `SettingsLoader` calls `WordTokenizer.TryParseListEntry` to validate
word-list settings.

### Design

The units are independent of each other except for one shared constant. A caller that checks a text asks
`DocumentDirectives` and `DefaultPatterns` for ranges that are not to be checked, merges them, and asks
`WordTokenizer` for the words in what remains. Nothing in the subsystem merges the ranges or removes the excluded
words, because the owner of that decision, the word rules in `SpellChecking`, also owns the order of precedence
between ignored text, flagged words, and accepted words.

Two implementation styles are used, each where it fits. Deciding what a word is runs for every character of every
file, so `WordTokenizer` is a hand-written scanner over the string, with no regular expressions and no allocation
per word; see its design for the reasons. Recognizing a URL, a hash, or a directive is a question about a pattern that
cspell already answers with a regular expression, and the library must find the same text. Those units therefore port
the cspell patterns to .NET regular expressions generated at build time, once per file rather than once per word,
with a match time limit. Keeping the cspell patterns recognizable, instead of rewriting them as scanners, makes the
port reviewable pattern by pattern against the cspell originals and keeps a migrated project quiet where it was quiet.

Results are reported as offsets into the original string and written into buffers that the caller supplies. A caller
that scans many files can reuse one buffer, no result needs a copy of the text, and the same offsets give the line and
column of a finding. Offsets count UTF-16 code units, which is how .NET strings are indexed and how cspell reports
columns, so a character outside the basic multilingual plane counts as two.

The subsystem differs from cspell in two ways on purpose. It has no counterpart to cspell's heuristic word splitter,
which re-segments an unknown word with the help of the dictionary: words are split only at case changes, so the
subsystem stays a pure function of the text that does not depend on a dictionary. And it treats a word-list entry as
something to be validated: an entry that could never match is rejected with a reason when settings are read, where
cspell silently keeps it.

Every unit is thread-safe because every unit is stateless. The generated regular expressions are immutable and safe
for concurrent matching, the shared `DirectiveScan.Empty` and all other results are immutable, and the only mutable
objects are the buffers that callers pass in, which a caller must not share between threads. Errors are of two kinds: a
caller that violates a precondition gets an argument exception at once, and hostile text gets either a safe result or,
for the units that use regular expressions, a `RegexMatchTimeoutException` that the caller turns into a finding
for the file. Malformed text, such as a lone surrogate, is never an error.
