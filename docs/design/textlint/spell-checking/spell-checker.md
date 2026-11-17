### SpellChecker

![SpellChecking Structure](SpellCheckingView.svg)

#### Purpose

`SpellChecker` is the top-level operation of the library: it selects the files that a settings value names under a
root directory, applies the `enabled` and `overrides` settings, reads and checks each remaining file in parallel, and
returns one deterministic result. It owns the concurrency, the ordering, the handling of per-file problems, the
cancellation, and the application of the overrides, so that no caller has to. It also defines the
public result and option types `SpellCheckOptions`, `SpellCheckResult`, `FileDiagnostic`, and `FileDiagnosticKind`, and
the note types `SpellCheckNote`, `SpellCheckNoteKind`, and `SpellCheckNoteSeverity`.

#### Data Model

`SpellChecker` is a public static class with no state; every call builds its own checkers and its own result.

**SpellCheckOptions** is a public immutable record with `MaxDegreeOfParallelism`, the largest number of files that
are checked at the same time, `SettingsRootDirectory`, and `Language`. The degree of parallelism defaults to the
number of logical processors and is validated when the options value
is created: a value below one throws `ArgumentOutOfRangeException` from the initializer, so a bad value fails where it
is written, also in a `with` expression, and cannot reach the checker. `SettingsRootDirectory` is the directory that
the `filename` patterns of the overrides are relative to; it is not always the directory the files are searched in
(a command-line path can move the search elsewhere), and `null` means the root passed to `CheckAsync`. `Language` is
a language selection that wins over the language of the settings and of every override (the command-line language);
`null` leaves the languages as the settings say. It is validated when the run starts, like the settings' language.
Options are a separate type, not method
parameters, so that later options can be added without changing method signatures, and a settings value stays a
statement of what to check rather than how to run.

**SpellCheckResult** is a public immutable class with `Issues` (the findings), `FileDiagnostics`, `FilesChecked`,
`FilesSkipped`, `FilesDisabled`, `Notes`, and `HasProblems`. The lists are copied into immutable arrays when the result
is created, so no
consumer can change them, even by casting a list to a mutable collection interface, and the caller's collections
may change afterwards without affecting the result. `FilesChecked` counts the files that were read and checked
completely, including the ones that have findings. `FilesSkipped` counts the binary and too-large files; unreadable
and timed-out files are in neither count, because they were not skipped by design but failed. `FilesDisabled` counts
the selected files that the settings disabled; they are not read, reported, or included in the other counts, and the
number lets a consumer tell "nothing was selected" from "everything selected is disabled". `HasProblems` is true
when there is a finding or a diagnostic that is an error, so skipped files alone are not a problem, and neither are
notes; an exit code or a
CI gate should be based on it. An internal method creates a copy of a result that also carries the disabled count and
the notes, which only the spell checker can know.

**FileDiagnostic** is a public immutable class with `FilePath` (relative to the root, with forward slashes), `Kind`,
`Message`, and `IsError`. `ToString` formats `file: severity: message`, with `error` or `info` as `IsError` says;
`Format(filePath)` does the same under a file name chosen by the caller (it rejects null), and `ToString` is
`Format` applied to the recorded `FilePath`;
there is no line or column because the problem concerns the whole file.

**FileDiagnosticKind** is an enumeration: `Unreadable`, `Binary`, `TooLarge`, and `TimedOut`. `Unreadable` and
`TimedOut` are errors, because the file was meant to be checked and was not; `Binary` and `TooLarge` are information,
because the file was skipped on purpose.

**SpellCheckNote** is a public immutable class that reports something about how the settings were applied: `Kind`, a
`Severity`, `Message` (a sentence without location or severity word), and the `Source`, `Line`, and `Column` of the
override it is about, each `null` when unknown. Only the spell checker creates it. `Format` gives one line: an
informational note is `info: message`, and a warning is `file(line,column): warning: message`, with the parts of the
location that are unknown omitted (a warning with no location is `warning: message`); `ToString` is the same text.

**SpellCheckNoteKind** is an enumeration: `LanguageReplaced`, `AllFilesDisabled`, and `OverrideUnmatched`.
**SpellCheckNoteSeverity** is an enumeration: `Info` and `Warning`; the severity follows from the kind, and only
`OverrideUnmatched` is a warning.

**FileOutcome** is a private record struct of the findings, the diagnostic (or null), and whether the file was checked
completely; it is the content of one slot of the result array.

#### Key Methods

**CheckAsync (public)**: Checks the files that the settings select.

- *Parameters*: `TextLintSettings settings`; `string rootDirectory`; `SpellCheckOptions? options` (null means the
  defaults); `CancellationToken cancellationToken`.
- *Returns*: a `SpellCheckResult`.
- *Preconditions*: the settings are not null, the root is not null, empty, or white space, and it exists.
- *Postconditions*: nothing was written; every selected, enabled file was read once.

Steps: (1) validate the arguments; (2) call `CheckWithSelectionAsync`, passing a selection that calls the file
selector with the settings' patterns and the caller's token, and the root directory as the search root; that method
creates the resolver and the checkers, which validate the language, the word lists, and the overrides, so
unusable settings fail before any file or directory is touched; (3) select the files, on a
thread-pool thread, because the selector is synchronous and the call should not block its caller while it walks the
directory tree, with the token passed to the selector so that canceling stops the walk itself and not only the
wait for it; (4) resolve the files against the overrides (the pre-pass, below); (5) call `CheckFilesAsync` with the
enabled files, the maximum file size of the settings, the checker lookup, and the options; (6) add the disabled count
and the notes to the result.

**CheckWithSelectionAsync (internal)**: Selects the files with a given function, applies the overrides, and checks the
enabled files.

It is a test seam: the selection is a parameter, so tests can prove that the caller's token reaches the file selection,
which a real directory tree cannot show deterministically. The public method passes the file selector. A further
optional parameter, `TimeSpan? matchTimeout`, is a second seam: null (the public method's value) makes the checker with
the production limit of 5 seconds, and a value makes it with that limit, so that a test can prove the time-out
diagnostic for a catastrophic pattern of the settings or of a directive in milliseconds. A third optional parameter,
the search root directory, says what the selected files' relative paths are relative to, which is needed to match the
overrides.

Steps: (1) create a `SettingsOverrideResolver` from the settings and the options' language; (2) build the checker of
the top-level settings (the effective settings of a file that matches no override), and eagerly build one checker for
each override that has checker settings, from that override alone, so that an unusable override fails the run before
any file is touched, even one that no file will match; (3) select the files; (4) run the pre-pass over them;
(5) check the enabled files in parallel; (6) build the notes.

**The pre-pass**: A sequential pass over the selected files, cheap because it only matches paths. For each file it
computes the path relative to the settings root (the settings root is the option, or else the search root): when the
settings root is the search root, that is the selection's relative path; otherwise it is recomputed from the file's full
path, and a result that is rooted or climbs out of the root means the file is outside the settings root, so no override
matches and `ResolveUnmatched` gives the resolution. Then it asks the resolver for the resolution and:

- records, for every matched override, that it matched;
- drops the file when it is disabled, so it is not read, reported, or counted;
- otherwise adds it to the enabled files, records for the matched overrides that they were used on a checked file, and,
  when some matching override has checker settings, looks up the checker of that combination of overrides (the
  resolution's `SettingsKey`), building and caching it when it is new, and remembers it for the file's path.

Files with no such override use the checker of the top-level settings. The lookup is complete when the pre-pass ends and
is only read from then on, so the parallel phase needs no lock; the check function picks the checker of the file by its
relative path.

**Notes**: Built after the check, in a fixed order so that results are deterministic: first the information notes, then
the warnings.

- `LanguageReplaced`, by override in order: when the options carry a language, and an override that was used on a
  checked file sets a language, and the two languages select different dictionaries (`en` equals `en-US`, because the
  dictionary type returns shared instances, so the comparison is of dictionaries and not of text). The message is
  `--language {requested} replaces {overrideLanguage} for the override at {source}({line},{column}).`; an override
  without a position says `at number N.` in place of the location.
- `AllFilesDisabled`: when at least one file was selected and the settings disabled every one of them. The message is
  `Spell checking is disabled by the settings for all {n} selected file(s).`
- `OverrideUnmatched` (a warning), by override in order: for each override that matched none of the selected files,
  but only when at least one file was selected, because with nothing selected every override would be unmatched for a
  reason that has nothing to do with it. An override with `enabled: false` that matched a file counts as matched. The
  message is `The override matches none of the selected files.`, with the override's position.

**CheckFilesAsync (internal)**: Checks the given files and assembles the result.

It is a test seam: the function that checks a text is a parameter, so tests can prove how a time-out or a defect in
checking is handled without hostile input, and can observe the degree of parallelism and the cancellation without
timing assumptions. The public method passes a function that selects the checker for the file and calls it.

Steps: (1) allocate one `FileOutcome` slot per file; (2) run `Parallel.ForEachAsync` over the file indexes with the
degree of parallelism and the token, each worker reading and checking its file and writing only its own slot;
(3) walk the slots in order and build the result: the findings and diagnostics in file order, and the counts.
Because each slot belongs to one file and the selection is sorted by ordinal relative path, the result is ordered by
path and then, as each file's findings are already in text order, by line and column. No lock is taken.
`Parallel.ForEachAsync` was chosen over starting one task per file with a semaphore because it bounds the
parallelism by construction, stops starting new work when the token is canceled, and surfaces the first exception.

**CheckFileAsync (private)**: Checks one file.

It reads the file with `FileTextReader`; when the outcome has no text, the file becomes a diagnostic of the kind the
reader gave. Otherwise it calls the check function with the text and the relative path. A
`RegexMatchTimeoutException`, which only hostile content can cause, becomes a timed-out diagnostic. Any other
exception propagates.

#### Error Handling

The handling of each condition is:

- *Null settings or root, empty or white-space root*: `ArgumentNullException` or `ArgumentException` from
  `CheckAsync`.
- *Unsupported language or unusable word-list entry*: `ArgumentException` from the checker's constructor, before any
  file is touched.
- *Unusable override (no `filename`, an unsupported language, an unusable word or pattern), or an unsupported language
  in the options*: `ArgumentException` (an invalid glob is `InvalidGlobPatternException`) from the resolver or a
  checker's constructor, before any file is touched, also for an override that no file matches.
- *Root directory missing or unreadable*: `DirectoryNotFoundException` for a missing root; `UnauthorizedAccessException`
  or another `IOException` for a root that cannot be read, each from the file selector and unchanged.
- *Invalid file or ignore pattern*: `InvalidGlobPatternException` from the file selector.
- *File unreadable, binary, or too large*: a diagnostic in the result; the run continues.
- *Pattern time-out while checking a file*: a timed-out diagnostic in the result; the run continues.
- *Any other exception while checking, such as a damaged dictionary*: propagates to the caller and is never reported
  as a clean file.
- *Cancellation, before or during the run*: `OperationCanceledException`; no result is returned.

When one worker throws, the parallel loop stops starting new files and the exception reaches the caller, after the
workers that are already running have finished, so no work continues in the background. Nothing is logged.

#### Dependencies

- **TextChecker** and **FileTextReader** - checking a text and reading a file; both in the same subsystem.
- **TextLintSettings** and **SettingsOverrideResolver** (Configuration subsystem) - what to check and how, and the
  merge of the overrides for a file.
- **SpellingDictionary** (Dictionaries subsystem) - compares the dictionaries of two languages for the language note.
- **FileSelector** (FileSelection subsystem) - the ordered list of files, with their relative and full paths.
- **.NET BCL** - the parallel loop, tasks, and path handling.

#### Callers

- **Library consumers** - run a check of a directory with `SpellChecker.CheckAsync(settings, root)`.
- **The textlint tool** - runs the check, passing the settings root and the command-line language in the options, and
  prints the notes and the findings.

#### Design Notes

**Thread safety.** The class is stateless and safe to call from several threads at once, also over the same
directory. The workers of one call share only the immutable `TextChecker` instances, the immutable selection, the
checker lookup (which is complete before they start), and the caller's token; each writes its own slot of the outcome
array.

**Memory is bounded by workers times file size.** A worker holds one file as bytes and as text at a time, so the
memory in use is the degree of parallelism times the size of the files being checked (bounded in turn by the
maximum file size of the settings and by the 1 GiB ceiling of the reader), not the size of the repository.

**Cancellation has no partial result.** A result assembled from the files that happened to finish would look like a
complete answer for the whole selection, and a build that trusted it could pass with files unchecked, so a canceled
run throws instead. The token is passed to the selection, to the loop, and to every read.

**Why overrides are resolved in a pre-pass.** The checkers of all combinations of overrides must exist before the
workers start, so that nothing mutable is shared; resolving by path needs no file content, so doing it in one cheap
sequential pass keeps the parallel phase unchanged. Dropping disabled files in the same pass means a disabled file costs
no read at all. Most files match no override with settings and share one checker, so the number of checkers is the
number of combinations that occur, not the number of files.

**Why per-file problems are data and defects are exceptions.** A run over a repository must survive the files it
cannot read and must say which they were; it must not survive a bug, because a checker that swallows its own
failures reports a clean repository that was never checked. The reader's list of expected failures is therefore
short and explicit, and the checker's time-out is the only exception from checking that is turned into data.
