### TextChecker

![SpellChecking Structure](SpellCheckingView.svg)

#### Purpose

`TextChecker` checks the spelling of one string of text against the dictionary of the configured language and the
word lists of a settings value, and returns every finding with its line and column. It is the single place that
holds the word-level rules, so a string checked by an editor and a file checked by a run give the same answers. It
also defines the finding types `SpellingIssue` and `SpellingIssueKind`, and the internal word-list types
`WordRules`, `WordSet`, and `FlagSet`, which have no unit of their own.

#### Data Model

**TextChecker** is a public sealed class. Its only field is the shared `WordRules` built from the settings in the
constructor. The checker keeps no reference to the settings, copies the word lists into sets, and never changes
after construction, so it is immutable and safe to share between threads.

**SpellingIssue** is an immutable public class with `FilePath` (the source name, or null), `Line` and `Column`
(1-based; the column counts UTF-16 code units), `Text` (the word as written, the whole token for a flagged entry
that contains digits or punctuation, or the name of an unsupported directive), `Kind`, and `Suggestions` (an
immutable list, empty unless the finding is a flagged word whose entry gave suggestions). Its constructor is
internal because only the checker produces findings. `ToString` formats `file(line,column): error: message` in the
compiler style, with the suggestions appended to the message, and just `(line,column)` when there is no file name.
Findings are values rather than exceptions so that a consumer can print or process every one the same way.

**SpellingIssueKind** is an enumeration of the three kinds: `UnknownWord`, `FlaggedWord`, and
`UnsupportedDirective`.

**WordRules** is an internal immutable class that answers the word-level questions: is a word flagged, is it
accepted, is it the capital run of an acronym with an ending, is it a repeated letter. It holds the
dictionary, the minimum length, and three sets. An instance can be layered over another with the words that a
document's directives add (`WithDocumentWords`), sharing the parent's dictionary, minimum length, and flag set and
chaining the lookups, so a document with a directive does not copy the project's lists.

**WordSet** is an internal immutable set of accepted or ignored entries, compared without regard to letter case. It
has one table for single words and one for entries that contain digits or punctuation, and both are looked up with a
span of the checked text, so the lookup does not allocate.

**FlagSet** is an internal immutable set of flagged entries with their suggestions. An entry written entirely in lower
case matches in any letter case; an entry with a capital letter matches only that exact spelling, as in cspell. When
an entry is repeated, the first is kept.

**CheckRun** is a private class created for each call to `Check`. It holds the text, the rules in force, the
findings so far, and three scratch lists (tokens, words, pieces) that are reused across the call to avoid
allocating for each word. It never leaves the call that created it.

**Finding** is a private record struct of an offset, a length, a kind, and suggestions; it is converted to a
`SpellingIssue` only at the end, when the offsets are turned into lines and columns.

#### Key Methods

**Constructor (public)**: Builds a checker from settings.

- _Parameters_: `TextLintSettings settings`.
- _Preconditions_: the settings are not null, the language is supported, and every entry of `words`,
  `ignoreWords`, and `flagWords` can match something.
- _Postconditions_: the checker is immutable. The dictionaries of the language are loaded lazily, when the first word
  is looked up, not here.

Steps: (1) resolve the language with the spelling dictionary, converting its exception into an `ArgumentException`
that names the language and lists the supported names, so an unsupported language is reported before any list
entry; (2) interpret every entry of the three lists (the flag list also accepts the suggestion forms `word->a, b`
and `word: a`) and reject one that could never match, naming the setting and the entry; (3) build the sets and the
rules. Settings built in code bypass the settings file loader's checks, which is why the constructor repeats them.

**Check (public)**: Checks a text and returns its findings.

- _Parameters_: `string text`, `string? sourceName` (default null).
- _Returns_: a read-only list of `SpellingIssue` in text order; never null; empty for an empty text.
- _Preconditions_: the text is not null.
- _Postconditions_: the text is not modified; nothing is read or written; the list is immutable.

Steps: (1) scan the text for directives, which yields the regions that are switched off, the text of the directives
that add words, the words they add, and the unsupported directives; (2) add the ranges that are never checked (URLs,
email addresses, hexadecimal values, hashes, encoded data) and sort all excluded ranges by position; (3) when the
directives added words, layer them over the shared rules; (4) check each stretch of text between excluded ranges as a
separate segment, so a word that an excluded range cuts in two is checked as two pieces; (5) add one finding for
each unsupported directive; (6) convert offsets to lines and columns.

**CheckRun.CheckToken / CheckWord / CheckPiece (private)**: The word-level algorithm for one segment.

A segment is cut into tokens, the units that the entries with digits or punctuation are matched against and within
which words are found (see _TextScanning Design_). If any list
has an entry with digits or punctuation, the token with its edge punctuation trimmed is first matched as a whole
against those entries: ignored or accepted ends the token, flagged reports the whole token. Otherwise the token is cut
into words. For each word: (1) a flagged word is reported whatever its length, unless it is also ignored; (2) a word
shorter than the minimum length, or accepted, passes; accepted means in the dictionary or a list, as the possessive
of such a word, without a backslash escape or without the escape letter after a backslash, or as a run of capitals
with an ending whose capitals are accepted or too short to check; (3) an unknown word is split at case changes and
each piece is checked on its own, being reported when it is flagged, or long enough, unknown, and not one repeated
letter. The precedence is therefore ignored, then flagged, then accepted words, then dictionary, and a flagged word
shorter than the minimum is still reported. Checking the whole word before splitting it is what keeps a known
compound identifier from being taken apart; splitting only unknown words, at case changes only, is the deliberate
simplification of cspell's heuristic splitter.

**ToIssues (private)**: Converts findings to issues.

The findings are in text order except that unsupported directives are appended last, so they are sorted (stably)
only when there is one. The conversion walks the text once, counting line feeds (a carriage return alone is not a
line break, as in cspell) and measuring the column in UTF-16 code units from the last line feed.

**InterpretEntries (private)**: Interprets one list. It uses the tokenizer's entry parser and throws an
`ArgumentException` that names the setting and the entry for any entry that is blank, contains white space or a
character that cannot be part of a word, begins with `!` (the cspell forbidden-word marker, which would otherwise be
mistaken for an accepted word), or has no letter or digit.

#### Error Handling

A null settings value or text throws `ArgumentNullException`. An unusable language or word-list entry throws
`ArgumentException` from the constructor, naming the language, or the setting and the entry. Checking itself has no
expected failure. Two failures can still occur and are not caught here, because only the caller knows what to do with
them: a `RegexMatchTimeoutException` when an ignore pattern exceeds its time limit on hostile text (the caller should
treat the text as not checked), and an `InvalidDataException` when an embedded dictionary is missing or damaged,
which means the build of the library is defective. Findings are never thrown. Nothing is logged.

#### Dependencies

- **TextLintSettings** (Configuration subsystem) - the language, the three word lists, and the minimum word length.
- **SpellingDictionary** (Dictionaries subsystem) - the language selection and the word lookup.
- **WordTokenizer**, **DefaultPatterns**, and **DocumentDirectives** (TextScanning subsystem) - the tokens, words, and
  pieces; the text that is never checked; and the directives. See _TextScanning Design_.
- **.NET BCL** - collections and spans.

#### Callers

- **SpellChecker** - creates one checker for a run and calls it from every worker for the text of each file.
- **Library consumers** - editors, build steps, and tests that check a string, usually with
  `new TextChecker(settings).Check(text, name)`.
