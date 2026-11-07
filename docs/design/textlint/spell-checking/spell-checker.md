### SpellChecker

![SpellChecking Structure](SpellCheckingView.svg)

#### Purpose

`SpellChecker` is the top-level operation of the library: it selects the files that a settings value names under a
root directory, reads and checks each in parallel, and returns one deterministic result. It owns the concurrency, the
ordering, the handling of per-file problems, and the cancellation, so that no caller has to. It also defines the
public result and option types `SpellCheckOptions`, `SpellCheckResult`, `FileDiagnostic`, and `FileDiagnosticKind`.

#### Data Model

`SpellChecker` is a public static class with no state; every call builds its own checker and its own result.

**SpellCheckOptions** is a public immutable record with `MaxDegreeOfParallelism`, the largest number of files that
are checked at the same time. It defaults to the number of logical processors and is validated when the options value
is created: a value below one throws `ArgumentOutOfRangeException` from the initializer, so a bad value fails where it
is written, also in a `with` expression, and cannot reach the checker. Options are a separate type, not method
parameters, so that later options can be added without changing method signatures, and a settings value stays a
statement of what to check rather than how to run.

**SpellCheckResult** is a public immutable class with `Issues` (the findings), `FileDiagnostics`, `FilesChecked`,
`FilesSkipped`, and `HasProblems`. Both lists are copied into immutable arrays when the result is created, so no
consumer can change them, even by casting a list to a mutable collection interface, and the caller's collections
may change afterwards without affecting the result. `FilesChecked` counts the files that were read and checked
completely, including the ones that have findings. `FilesSkipped` counts the binary and too-large files; unreadable
and timed-out files are in neither count, because they were not skipped by design but failed. `HasProblems` is true
when there is a finding or a diagnostic that is an error, so skipped files alone are not a problem; an exit code or a
CI gate should be based on it.

**FileDiagnostic** is a public immutable class with `FilePath` (relative to the root, with forward slashes), `Kind`,
`Message`, and `IsError`. `ToString` formats `file: severity: message`, with `error` or `info` as `IsError` says;
`Format(filePath)` does the same under a file name chosen by the caller (it rejects null), and `ToString` is
`Format` applied to the recorded `FilePath`;
there is no line or column because the problem concerns the whole file.

**FileDiagnosticKind** is an enumeration: `Unreadable`, `Binary`, `TooLarge`, and `TimedOut`. `Unreadable` and
`TimedOut` are errors, because the file was meant to be checked and was not; `Binary` and `TooLarge` are information,
because the file was skipped on purpose.

**FileOutcome** is a private record struct of the findings, the diagnostic (or null), and whether the file was checked
completely; it is the content of one slot of the result array.

#### Key Methods

**CheckAsync (public)**: Checks the files that the settings select.

- _Parameters_: `TextLintSettings settings`; `string rootDirectory`; `SpellCheckOptions? options` (null means the
  defaults); `CancellationToken cancellationToken`.
- _Returns_: a `SpellCheckResult`.
- _Preconditions_: the settings are not null, the root is not null, empty, or white space, and it exists.
- _Postconditions_: nothing was written; every selected file was read once.

Steps: (1) validate the arguments; (2) call `CheckWithSelectionAsync`, passing a selection that calls the file
selector with the settings' patterns and the caller's token; that method creates the `TextChecker`, which validates
the language and the word lists, so
unusable settings fail before any file or directory is touched; (3) select the files, on a
thread-pool thread, because the selector is synchronous and the call should not block its caller while it walks the
directory tree, with the token passed to the selector so that canceling stops the walk itself and not only the
wait for it; (4) call `CheckFilesAsync` with the selection, the maximum
file size of the settings, the checker's method, and the options.

**CheckWithSelectionAsync (internal)**: Selects the files with a given function and checks them.

It is a test seam: the selection is a parameter, so tests can prove that the caller's token reaches the file selection,
which a real directory tree cannot show deterministically. The public method passes the file selector. A further
optional parameter, `TimeSpan? matchTimeout`, is a second seam: null (the public method's value) makes the checker with
the production limit of 5 seconds, and a value makes it with that limit, so that a test can prove the time-out
diagnostic for a catastrophic pattern of the settings or of a directive in milliseconds.

**CheckFilesAsync (internal)**: Checks the given files and assembles the result.

It is a test seam: the function that checks a text is a parameter, so tests can prove how a time-out or a defect in
checking is handled without hostile input, and can observe the degree of parallelism and the cancellation without
timing assumptions. The public method passes the checker's own method.

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

- _Null settings or root, empty or white-space root_: `ArgumentNullException` or `ArgumentException` from
  `CheckAsync`.
- _Unsupported language or unusable word-list entry_: `ArgumentException` from the checker's constructor, before any
  file is touched.
- _Root directory missing or unreadable_: `DirectoryNotFoundException` for a missing root; `UnauthorizedAccessException`
  or another `IOException` for a root that cannot be read, each from the file selector and unchanged.
- _Invalid file or ignore pattern_: `InvalidGlobPatternException` from the file selector.
- _File unreadable, binary, or too large_: a diagnostic in the result; the run continues.
- _Pattern time-out while checking a file_: a timed-out diagnostic in the result; the run continues.
- _Any other exception while checking, such as a damaged dictionary_: propagates to the caller and is never reported
  as a clean file.
- _Cancellation, before or during the run_: `OperationCanceledException`; no result is returned.

When one worker throws, the parallel loop stops starting new files and the exception reaches the caller, after the
workers that are already running have finished, so no work continues in the background. Nothing is logged.

#### Dependencies

- **TextChecker** and **FileTextReader** - checking a text and reading a file; both in the same subsystem.
- **TextLintSettings** (Configuration subsystem) - what to check and how.
- **FileSelector** (FileSelection subsystem) - the ordered list of files, with their relative and full paths.
- **.NET BCL** - the parallel loop and tasks.

#### Callers

- **Library consumers** - run a check of a directory with `SpellChecker.CheckAsync(settings, root)`.
- **The textlint tool** - will run the check and print the findings in a later change.

#### Design Notes

**Thread safety.** The class is stateless and safe to call from several threads at once, also over the same
directory. The workers of one call share only the immutable `TextChecker`, the immutable selection, and the
caller's token; each writes its own slot of the outcome array.

**Memory is bounded by workers times file size.** A worker holds one file as bytes and as text at a time, so the
memory in use is the degree of parallelism times the size of the files being checked (bounded in turn by the
maximum file size of the settings and by the 1 GiB ceiling of the reader), not the size of the repository.

**Cancellation has no partial result.** A result assembled from the files that happened to finish would look like a
complete answer for the whole selection, and a build that trusted it could pass with files unchecked, so a canceled
run throws instead. The token is passed to the selection, to the loop, and to every read.

**Why per-file problems are data and defects are exceptions.** A run over a repository must survive the files it
cannot read and must say which they were; it must not survive a bug, because a checker that swallows its own
failures reports a clean repository that was never checked. The reader's list of expected failures is therefore
short and explicit, and the checker's time-out is the only exception from checking that is turned into data.
