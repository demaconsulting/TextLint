## SpellChecking

![SpellChecking Structure](SpellCheckingView.svg)

### Overview

The `SpellChecking` subsystem checks the spelling of text. It does so for a string of text, with no file involved,
and for the files that a settings value selects under a root directory. Every unknown word, every flagged word,
and every directive that has no effect is returned as data with its file, line, and column, and every file that
could not be checked completely is returned as data with the reason. The subsystem contains three units:
`TextChecker`, which checks one string and owns every word-level rule; `FileTextReader`, which turns a file into
text or into the reason it will not be checked; and `SpellChecker`, which selects the files, runs the other two over
them in parallel, and assembles one deterministic result. The supporting types are documented with the unit that
uses them: `SpellingIssue`, `SpellingIssueKind`, and the internal word-list types with `TextChecker`;
`FileTextContent` with `FileTextReader`; and `SpellCheckOptions`, `SpellCheckResult`, `FileDiagnostic`, and
`FileDiagnosticKind` with `SpellChecker`.

The subsystem reads directories and files but never writes anything and does no console output. It does not read
settings files (that belongs to the `Configuration` subsystem) and it does not decide how findings are presented
or what the exit code of a tool is; it only supplies the data, with `SpellCheckResult.HasProblems` as the one
verdict a gate should use.

### Interfaces

**TextChecker.Check**: Checks a string.

- _Type_: In-process .NET instance method on a checker that is created from a `TextLintSettings` value.
- _Role_: Provider.
- _Contract_: Accepts the text and an optional source name, and returns a read-only list of `SpellingIssue`, each
  with the source name, a 1-based line, a 1-based column in UTF-16 code units, the text of the finding, its kind
  (unknown word, flagged word, or unsupported directive), and its suggestions. The list is in text order.
- _Constraints_: Creating the checker throws `ArgumentNullException` for null settings and `ArgumentException` for
  an unsupported language or a word-list entry that could never match; both messages name what is wrong.
  `Check` throws `ArgumentNullException` for a null text. A checker is immutable and safe for any number of
  concurrent calls.

**SpellChecker.CheckAsync**: Checks the files that the settings select.

- _Type_: In-process .NET static asynchronous method.
- _Role_: Provider.
- _Contract_: Accepts the settings, a root directory, optional `SpellCheckOptions` (the degree of parallelism,
  which defaults to the number of processors), and a cancellation token. It returns a `SpellCheckResult` with the
  findings ordered by file path (ordinal), then line, then column; the `FileDiagnostic` entries ordered by file
  path; the number of files checked completely; the number skipped because they were binary or too large; and
  `HasProblems`, which is true when there is a finding or an error diagnostic.
- _Constraints_: Throws `ArgumentNullException` and `ArgumentException` for bad arguments or unusable settings
  (before any file is touched), `DirectoryNotFoundException` for a missing root (a root that cannot be read throws
  `UnauthorizedAccessException` or `IOException` instead), `InvalidGlobPatternException` for
  a bad pattern, and `OperationCanceledException` when cancelled. Problems with a single file are never thrown.

**Dependencies on other subsystems**: The subsystem consumes four others and none consumes it.

- `Configuration` supplies `TextLintSettings`, the immutable value that carries the file patterns, the ignore
  patterns, the hidden-file switch, the language, the three word lists, the minimum word length, and the maximum
  file size. The subsystem never reads or writes a settings file.
- `Dictionaries` supplies `SpellingDictionary`, which answers whether a word is known in the chosen language.
- `FileSelection` supplies `FileSelector`, which turns the patterns into the ordered list of files to check.
- `TextScanning` supplies the word tokenizer, the default ignore patterns, and the document directives, which
  decide what text is checked and how it is cut into words.

These are the documented dependencies for the subsystem and unit tests: tests may use the settings value, the file
selector, the spelling dictionary, and the text scanning units for real, and no test double replaces any of them.
The only seam is an internal overload of the file-checking method that takes the function that checks a text, so
that tests can prove the handling of a time-out, a defect, a cancellation, and the degree of parallelism without
hostile input or timing assumptions.

### Design

The subsystem is three layers with one direction of dependency. `TextChecker` knows nothing about files: it turns a
string into findings, so an editor, a build step, or a test can use it directly, and every word-level rule lives in
exactly one place. `FileTextReader` knows nothing about spelling: it turns a path into text or into a reason, so
the encoding rules and the file problems can be tested with bytes and temporary files. `SpellChecker` knows about
neither detail: it selects files, calls the reader, calls the checker, and merges. Keeping the layers apart is what
allows a file problem to be data in the result while a defect in the checker remains an exception (see _Failures_
below).

**Determinism without locks.** Files are independent, so they are checked in parallel, but a result must not depend
on timing. `SpellChecker` allocates one slot per selected file in an array indexed by the file's position in the
ordinal-sorted selection, each worker writes only its own slot, and the result is assembled by walking the array
in order after every worker has finished. Nothing mutable is shared, so there is no lock to get wrong, and the order
of the result is the order of the selection whatever order the workers finish in. The alternatives were rejected:
appending to a concurrent collection and sorting at the end repeats work and still needs a total order on findings,
and a lock around a shared list would serialize exactly the step that must stay parallel. The only state the workers
share is the immutable `TextChecker`; each call to `Check` keeps its mutable state in a `CheckRun` object that is
created for that call and never leaves it, so concurrent calls cannot interfere.

**Whole word first, and cspell parity.** The goal of the checker is to replace cspell in existing repositories without
surprises, so its rules follow cspell's documented behavior and are checked against recorded cspell output by the
parity corpus of the system tests. A word is checked as a whole before it is split: a known compound such as an
identifier that the dictionary or a word list holds stays quiet, and only an unknown word is split at case changes
and checked piece by piece, so an identifier made of two known words around a misspelling reports the misspelling and
nothing else. Entries of the word lists that
contain digits or punctuation (`k8s`, `foo-bar`) are matched against whole tokens, because the word splitter would
otherwise cut them apart before they could match. The order of precedence is the one cspell uses: an ignored word is
never reported, a flagged word always is (whatever its length), then the accepted words and the dictionary decide.
The known word forms that cspell allows (the possessive, a word after a backslash escape, a run of capitals with a
plural or verb ending, and a piece that is one repeated letter) are part of the checker so that prose, string
literals, acronyms, and placeholders do not produce findings that cspell does not.

**Where the subsystem deliberately differs from cspell.** These differences are documented for users, and the
parity test compares the full list of them with the actual output so that a fixed or a new difference is noticed.

- cspell's heuristic splitter, which re-segments an unknown word that mixes capitals and endings, is not replicated;
  the checker splits at case changes only.
- The dictionaries are the library's own, so a word that cspell's bundled set knows may be unknown here.
- A file is binary when a NUL character appears among its first 1,024 decoded characters, whatever its extension
  and whatever the file type, where cspell decides by file type first.
- Findings for repeated occurrences of a word are not capped.
- A directive that cspell supports and the checker does not (`locale`, `dictionary`, `flag`, and others) is reported
  at its position instead of being ignored, because an author who writes a directive believes it is in force.
- UTF-32 files are not supported and are skipped as binary data.
- Language names are limited to the ones the dictionaries know; cspell also accepts forms such as `en_US`.
- Problems with a single file are returned as data, with a severity, instead of being printed by a tool.

These are the same as cspell, although they look odd, so that recorded positions stay valid: a line break is the
line feed character alone (a carriage return by itself is not a break), columns and the minimum word length count
UTF-16 code units (so a character outside the Basic Multilingual Plane counts as two), and a byte order mark
character inside text is an ordinary character.

**Failures: data versus defects.** A repository holds binary files, huge generated files, and files that another
program has locked. One such file must not fail a whole run, and it must not be silently dropped either, so
`FileTextReader` returns the expected problems (binary, too large, unreadable) as an outcome, and `SpellChecker`
turns them, and the time-out of an ignore pattern on hostile content, into a `FileDiagnostic` with a severity:
unreadable and timed-out files are errors, because they were meant to be checked, while binary and too-large files
are information, because they were skipped by design. Any other exception, such as a damaged embedded dictionary or
a bug, is a defect: it propagates to the caller, because swallowing it would turn a broken checker into a clean
report. Arguments and selection problems (a missing root, an invalid pattern, unusable settings) are exceptions as
well, because they are mistakes of the caller and an empty result would hide them.

**Cancellation.** The token is passed to the file selection, to the parallel loop, and to every read. A cancelled
run throws `OperationCanceledException` and returns no result, because a partial result that looks complete is
worse than none. A token that is already cancelled stops the run before any file is read.

**Memory.** Each worker holds one file as bytes and as text at a time, so the memory in use is bounded by the
degree of parallelism times the size of the largest files being checked, not by the size of the repository. A
caller who needs less lowers the degree of parallelism; a project can also set a maximum file size. Independent of
both, no file larger than 1 GiB is ever read, because a .NET string cannot hold more than about a billion characters
and the bytes and the text are in memory together.

**Settings are validated once, up front.** Settings built in code bypass the checks of the settings file loader, so
the checker validates the language and every word-list entry when it is created, and `SpellChecker` creates the
checker before it touches the file system. An entry that could never match (blank, containing white space, using the
forbidden-word marker, or with no letter or digit) is an error that names the setting and the entry, because a
setting the user believes is in force must not silently do nothing.
