## Linting

### Verification Approach

The `Linting` subsystem is verified by integration tests in `LintingSubsystemTests.cs`. Each test drives
`LintRunner.Run` and `LintReporter` together through a real `Context` (the documented dependency from the `Cli`
subsystem) against the real `TextLint` library and a real temporary directory, and asserts on the exit code and
on what was written. No test double is used: the library is the dependency whose behavior the subsystem must
combine correctly, and a double would hide that. What was written is captured through the log of a silent
context, which receives everything written to standard output and standard error in order. Every test selects
its directory with `--root`, so none depends on or changes the current directory. The sample misspellings
(`wrold`, `xyzzyq`, and the British spelling `favourite`) are words that the American dictionary does not hold.

### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test uses
its own uniquely named directory, so tests share no state. They run on Windows, Linux, and macOS.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- The explicit settings file beats the root settings file, which beats the defaults, and a run on defaults says so.
- A settings problem is reported with its position and nothing is checked.
- Command-line patterns replace the `files` setting, the ignore patterns still apply, and an absolute path is
  reported by that path.
- A run with nothing to check, and one whose selection matches nothing, fail unless the empty selection is allowed.
- The language option beats the settings, and the parallelism limit does not change the output.
- Findings, skipped files, and the summary are all written, in that order.
- Only a clean run succeeds; failures a user can cause are reported and do not escape as exceptions; a cancelled
  run reports the cancellation and no result.

### Test Scenarios

**LintingSubsystem_Run_ExplicitSettings_BeatRootSettings**: A root settings file accepts one word, a named file
outside the root accepts the other; checking with the named file reports only the word that file does not accept.
This scenario is tested by `LintingSubsystem_Run_ExplicitSettings_BeatRootSettings`.

**LintingSubsystem_Run_RootSettingsFile_IsUsedWithoutOption**: A root settings file selects markdown files and
accepts a word; a run with no options and no patterns checks the file and is clean. This scenario is tested by
`LintingSubsystem_Run_RootSettingsFile_IsUsedWithoutOption`.

**LintingSubsystem_Run_NoSettingsFile_UsesDefaults**: With no settings file, a word that only a settings file
could have accepted is reported and the notice that no settings file was found is written. This scenario is
tested by `LintingSubsystem_Run_NoSettingsFile_UsesDefaults`.

**LintingSubsystem_Run_InvalidSettings_ReportsErrorsAndChecksNothing**: A settings file with an unsupported key
is reported at its line and column, nothing is checked, and the exit code is 1. This scenario is tested by
`LintingSubsystem_Run_InvalidSettings_ReportsErrorsAndChecksNothing`.

**LintingSubsystem_Run_GlobsAndSettings_GlobsReplaceFiles**: Settings name one file and ignore a directory;
naming every markdown file on the command line checks the files outside the ignored directory, including the one
the settings did not name, and not the ignored one. This scenario is tested by
`LintingSubsystem_Run_GlobsAndSettings_GlobsReplaceFiles`.

**LintingSubsystem_Run_NoPatterns_ReportsUsageError**: A settings file without a file list and no patterns give a
usage error that says what to add and exit code 1. This scenario is tested by
`LintingSubsystem_Run_NoPatterns_ReportsUsageError`.

**LintingSubsystem_Run_NoFilesMatched_FailsUnlessAllowed**: A pattern that matches nothing fails with an
explanation; the same pattern with `--allow-empty` succeeds. This scenario is tested by
`LintingSubsystem_Run_NoFilesMatched_FailsUnlessAllowed`.

**LintingSubsystem_Run_AbsolutePath_ReportsAbsolutePath**: A file named by its absolute path is reported under
that path. This scenario is tested by `LintingSubsystem_Run_AbsolutePath_ReportsAbsolutePath`.

**LintingSubsystem_Run_AbsoluteDirectory_ChecksEveryFileBelow**: A directory named by its absolute path, without a
trailing separator, checks the misspelled file two levels below it; the path of a directory that does not exist is
a file name that matches nothing and fails with "No files matched". This scenario is tested by
`LintingSubsystem_Run_AbsoluteDirectory_ChecksEveryFileBelow`.

**LintingSubsystem_Run_InvalidRoot_IsReportedBeforeAnythingElse**: A root that does not exist and a root that is a
file, each with a misspelled file named by its absolute path, give exit code 1 and an error that names the root;
there is no line about missing settings and nothing is checked. This scenario is tested by
`LintingSubsystem_Run_InvalidRoot_IsReportedBeforeAnythingElse`.

**LintingSubsystem_Run_LanguageOption_OverridesSettings**: Settings for American English and a file with the
British spelling report the word; the language option for British English accepts it. This scenario is tested by
`LintingSubsystem_Run_LanguageOption_OverridesSettings`.

**LintingSubsystem_Run_MaxParallelism_GivesSameResult**: Eight files are checked with one worker and with four;
the output and the exit code are identical. This scenario is tested by
`LintingSubsystem_Run_MaxParallelism_GivesSameResult`.

**LintingSubsystem_Run_MixedOutcomes_ReportsAllAndSummary**: A misspelled file, a clean file, and a binary file
give the finding first, the skipped file as information next, and the summary last. This scenario is tested by
`LintingSubsystem_Run_MixedOutcomes_ReportsAllAndSummary`.

**LintingSubsystem_Run_CleanAndDirty_SetExitCode**: A clean file gives exit code 0 and a misspelled file gives 1.
This scenario is tested by `LintingSubsystem_Run_CleanAndDirty_SetExitCode`.

**LintingSubsystem_Run_ExpectedFailures_AreReportedNotThrown**: A missing root directory, an unsupported pattern,
and an unsupported language each give exit code 1 and a message that names the cause, without an exception. This
scenario is tested by `LintingSubsystem_Run_ExpectedFailures_AreReportedNotThrown`.

**LintingSubsystem_Run_CancelledToken_ReportsCancellation**: A token that is already cancelled gives exit code 1,
a message that says the check was cancelled, and no finding. This scenario is tested by
`LintingSubsystem_Run_CancelledToken_ReportsCancellation`.

<!-- cspell:ignore wrold xyzzyq favourite -->
