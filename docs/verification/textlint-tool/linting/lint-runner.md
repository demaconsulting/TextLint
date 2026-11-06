### LintRunner

#### Verification Approach

`LintRunner` is verified with unit tests in `LintRunnerTests.cs`. Its documented dependencies are the `Context`, the
reporter, and the `TextLint` library, and what it must prove (settings found, patterns applied, failures reported)
is only observable through them, so the tests use the real library and real temporary directories and no test
double. Output is captured through the log of a silent context. Every test selects its directory with `--root`.
The one test double is a function in place of `File.GetAttributes` for the settings-file probe: the ways a file
system can refuse (access denied, a general I/O failure) cannot be produced on every platform with a real
directory, so the probe takes the read as a parameter and the tests supply each failure.

#### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory. The
locked-file test holds a file open with exclusive sharing, which the library reports as unreadable on every
platform.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Settings are found, applied, and reported on as documented, and a file that exists but cannot be read is never
  mistaken for an absent file.
- Command-line patterns, the language option, and the parallelism limit act as documented.
- Every failure a user can cause is reported and fails the run; cancellation is reported and fails the run.

#### Test Scenarios

**LintRunner_Run_MisspelledFile_ReportsIssueAndFails**: A misspelled word is reported in the compiler style, the
summary counts it, and the exit code is 1. This scenario is tested by
`LintRunner_Run_MisspelledFile_ReportsIssueAndFails`.

**LintRunner_Run_CleanFile_Succeeds**: A correct file gives exit code 0 and a summary without issues. This
scenario is tested by `LintRunner_Run_CleanFile_Succeeds`.

**LintRunner_Run_DefaultSettingsFile_IsApplied**: A settings file in the root selects files, accepts a word, and
ignores a file; a run with no patterns is clean and writes no "not found" notice. This scenario is tested by
`LintRunner_Run_DefaultSettingsFile_IsApplied`.

**LintRunner_Run_NoSettingsFile_UsesDefaultsAndSaysSo**: With no settings file the run succeeds on a clean file
and writes the information line that names the root. This scenario is tested by
`LintRunner_Run_NoSettingsFile_UsesDefaultsAndSaysSo`.

**LintRunner_Run_ConfigOption_UsesNamedFile**: A settings file outside the root that accepts the misspelled word
is named with `--config`; the run succeeds without the "not found" notice. This scenario is tested by
`LintRunner_Run_ConfigOption_UsesNamedFile`.

**LintRunner_Run_MissingConfigFile_ReportsErrorAndFails**: A named settings file that does not exist gives an
error that names it, nothing is checked, and the exit code is 1. This scenario is tested by
`LintRunner_Run_MissingConfigFile_ReportsErrorAndFails`.

**LintRunner_Run_InvalidSettings_ReportsEachErrorAndChecksNothing**: A settings file with two unsupported keys
gives one error per key at its line, nothing is checked, and the exit code is 1. This scenario is tested by
`LintRunner_Run_InvalidSettings_ReportsEachErrorAndChecksNothing`.

**LintRunner_Run_UnreadableSettingsFile_ReportsErrorNotAbsence**: The default settings file name is taken by a
directory, which exists but cannot be read as a file; the run reports an error, does not say that no settings file
was found, and checks nothing. This scenario is tested by
`LintRunner_Run_UnreadableSettingsFile_ReportsErrorNotAbsence`.

**LintRunner_ProbeSettingsFile_FileSystemFailures_AreNotAbsence**: With reads that throw a missing-file error, a
missing-directory error, an access-denied error, and a general I/O error, only the first two mean absence, the
others are failures that carry the file system's explanation, and a read that succeeds means the file is found.
This scenario is tested by `LintRunner_ProbeSettingsFile_FileSystemFailures_AreNotAbsence`.

**LintRunner_Run_GlobArguments_ReplaceFilesSetting**: Settings name `b.md` and the command line names `a.md`;
only `a.md` is checked. This scenario is tested by `LintRunner_Run_GlobArguments_ReplaceFilesSetting`.

**LintRunner_Run_GlobArguments_HonorIgnorePaths**: Settings ignore a directory and `**/*.md` is named; the ignored
directory is not checked. This scenario is tested by `LintRunner_Run_GlobArguments_HonorIgnorePaths`.

**LintRunner_Run_AbsolutePathArgument_ReportsAbsolutePath**: A file named by its absolute path is reported under
that path. This scenario is tested by `LintRunner_Run_AbsolutePathArgument_ReportsAbsolutePath`.

**LintRunner_Run_NoPatterns_ReportsUsageError**: With no settings and no patterns, the run writes the usage error
and fails without checking anything. This scenario is tested by `LintRunner_Run_NoPatterns_ReportsUsageError`.

**LintRunner_Run_NoFilesMatched_FailsWithExplanation**: A pattern that matches nothing gives exit code 1 and an
explanation that names `--allow-empty`. This scenario is tested by
`LintRunner_Run_NoFilesMatched_FailsWithExplanation`.

**LintRunner_Run_NoFilesMatchedAllowEmpty_Succeeds**: The same pattern with `--allow-empty` gives exit code 0 and
a summary of zero files. This scenario is tested by `LintRunner_Run_NoFilesMatchedAllowEmpty_Succeeds`.

**LintRunner_Run_BinaryFile_ReportsInfoAndSucceeds**: A binary file is an information line and a skip, and the
run succeeds. This scenario is tested by `LintRunner_Run_BinaryFile_ReportsInfoAndSucceeds`.

**LintRunner_Run_LockedFile_ReportsErrorAndFails**: A file held open exclusively is reported as an error, the
other file is still checked, the summary counts one file that could not be checked, and the exit code is 1. This
scenario is tested by `LintRunner_Run_LockedFile_ReportsErrorAndFails`.

**LintRunner_Run_LanguageOption_OverridesSettings**: Settings for British English and a file with both spellings;
the settings' language accepts the British spelling, and `--language en-US` accepts the American one instead.
This scenario is tested by `LintRunner_Run_LanguageOption_OverridesSettings`.

**LintRunner_Run_InvalidLanguage_ReportsErrorAndFails**: An unsupported language gives an error that names it and
exit code 1, and nothing is reported as checked. This scenario is tested by
`LintRunner_Run_InvalidLanguage_ReportsErrorAndFails`.

**LintRunner_Run_InvalidGlobPattern_ReportsErrorNamingPattern**: A pattern with the unsupported `?` wildcard gives
an error that names the pattern and exit code 1. This scenario is tested by
`LintRunner_Run_InvalidGlobPattern_ReportsErrorNamingPattern`.

**LintRunner_Run_MissingRootDirectory_ReportsErrorAndFails**: A root that does not exist gives an error that names
it and exit code 1. This scenario is tested by `LintRunner_Run_MissingRootDirectory_ReportsErrorAndFails`.

**LintRunner_Run_InvalidRootPath_ReportsErrorAndFails**: A root that contains a character no path may contain
gives a message and exit code 1 and does not throw. This scenario is tested by
`LintRunner_Run_InvalidRootPath_ReportsErrorAndFails`.

**LintRunner_Run_CancelledToken_ReportsCancellationAndFails**: A token that is already cancelled gives the
cancellation message, no finding, no summary, and exit code 1. This scenario is tested by
`LintRunner_Run_CancelledToken_ReportsCancellationAndFails`.

**LintRunner_Run_MaxParallelism_GivesSameResult**: Eight files are checked with one worker and with four; the
output and the exit code are identical. This scenario is tested by `LintRunner_Run_MaxParallelism_GivesSameResult`.

**LintRunner_Run_NullContext_ThrowsArgumentNullException**: A null context throws `ArgumentNullException`. This
scenario is tested by `LintRunner_Run_NullContext_ThrowsArgumentNullException`.
