### LintReporter

#### Verification Approach

`LintReporter` is verified with unit tests in `LintReporterTests.cs`. A `SpellCheckResult` can only be produced by
the library, so each test runs the library's spell checker over a small temporary tree to obtain a real result,
which is the unit's documented dependency, and reports it through a real `Context`. No test double is used. What
was written is captured through the log of a silent context.

#### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory. The
unreadable-file test holds a file open with exclusive sharing, which the library reports as unreadable on every
platform.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Findings come first, then file diagnostics, then one summary line, in the library's order.
- The names shown are the ones the mapping gives.
- The summary counts are right, including the files that findings are in.
- The reporter does not set the exit status, and rejects missing arguments.

#### Test Scenarios

**LintReporter_Write_IssuesAndDiagnostics_WritesInOrderThenSummary**: Two misspelled files and a binary file give
three finding lines in file and position order, then the skipped file as information, then the summary. This
scenario is tested by `LintReporter_Write_IssuesAndDiagnostics_WritesInOrderThenSummary`.

**LintReporter_Write_DisplayPath_NamesFilesAsMapped**: With a mapping that adds a prefix, findings and file
diagnostics are both shown under the mapped names. This scenario is tested by
`LintReporter_Write_DisplayPath_NamesFilesAsMapped`.

**LintReporter_Write_UnreadableFile_ShownAsErrorAndCounted**: A file held open exclusively is shown as an error
line, and the summary counts one file that could not be checked. This scenario is tested by
`LintReporter_Write_UnreadableFile_ShownAsErrorAndCounted`.

**LintReporter_Write_CleanResult_WritesOnlySummary**: A clean result gives exactly one line, the summary. This
scenario is tested by `LintReporter_Write_CleanResult_WritesOnlySummary`.

**LintReporter_Write_SeveralIssuesInOneFile_CountsFilesOnce**: Two findings in one file are summarized as two
issues in one file. This scenario is tested by `LintReporter_Write_SeveralIssuesInOneFile_CountsFilesOnce`.

**LintReporter_Write_ResultWithIssues_LeavesExitCodeToCaller**: Reporting a result with a finding leaves the exit
code at 0, so the decision stays with the caller. This scenario is tested by
`LintReporter_Write_ResultWithIssues_LeavesExitCodeToCaller`.

**LintReporter_Write_NullArguments_ThrowsArgumentNullException**: A null context, result, or name mapping each
throws `ArgumentNullException`. This scenario is tested by
`LintReporter_Write_NullArguments_ThrowsArgumentNullException`.
