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
- Settings with an ordered `files` list, `overrides`, and `enabled` are applied through the library; overrides are
  matched against the settings root even when an absolute path moves the search directory, and a file outside the
  settings root matches none.
- The notes of the library are printed: the language note, the all-disabled information, and the unmatched-override
  warning only on a run without command-line patterns; none of them changes the exit code. A run whose selected files
  are all disabled succeeds, and a selection that matches no file still fails unless it is allowed to be empty.
- A problem in an override is reported at its position and nothing is checked.
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

**LintRunner_Run_AbsoluteDirectoryArgument_ChecksEveryFileBelow**: A directory named by its absolute path, without a
trailing separator, checks the files at every depth below it and none outside it; the finding is reported under the
file's absolute path. This scenario is tested by `LintRunner_Run_AbsoluteDirectoryArgument_ChecksEveryFileBelow`.

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
it and exit code 1, with no line about missing settings. This scenario is tested by
`LintRunner_Run_MissingRootDirectory_ReportsErrorAndFails`.

**LintRunner_Run_RootIsFile_ReportsErrorAndFails**: A root that is a file gives an error that says the root is not a
directory (the same on every operating system), no line about missing settings, and exit code 1. This scenario is
tested by `LintRunner_Run_RootIsFile_ReportsErrorAndFails`.

**LintRunner_Run_UnreadableRoot_ReportsCannotBeRead**: On Unix, as a user other than the super-user, a root whose
permissions forbid reading gives an error that says it cannot be read (not that it does not exist) and exit code 1;
the explanation that follows is the operating system's and is not compared. The scenario is skipped on Windows and
for the super-user, where the condition cannot be produced; because a skipped test is not evidence on those
systems, it is not listed against the requirement. This scenario is tested by
`LintRunner_Run_UnreadableRoot_ReportsCannotBeRead`.

**LintRunner_Run_InvalidRootWithAbsolutePath_ReportsErrorAndFails**: A missing root combined with a misspelled
file named by its absolute path reports the root, checks nothing, and gives exit code 1. This scenario is tested
by `LintRunner_Run_InvalidRootWithAbsolutePath_ReportsErrorAndFails`.

**LintRunner_Run_InvalidRootPath_ReportsErrorAndFails**: A root that contains a character no path may contain
gives a message and exit code 1 and does not throw. This scenario is tested by
`LintRunner_Run_InvalidRootPath_ReportsErrorAndFails`.

**LintRunner_Run_CanceledToken_ReportsCancellationAndFails**: A token that is already canceled gives the
cancellation message, no finding, no summary, and exit code 1. This scenario is tested by
`LintRunner_Run_CanceledToken_ReportsCancellationAndFails`.

**LintRunner_Run_MaxParallelism_GivesSameResult**: Eight files are checked with one worker and with four; the
output and the exit code are identical. This scenario is tested by `LintRunner_Run_MaxParallelism_GivesSameResult`.

**LintRunner_Run_NullContext_ThrowsArgumentNullException**: A null context throws `ArgumentNullException`. This
scenario is tested by `LintRunner_Run_NullContext_ThrowsArgumentNullException`.

**LintRunner_Run_LanguageOptionAndOverrideLanguage_OptionWinsAndNoteIsPrinted**: An override sets British English and
a file holds an American and a British spelling; without the option the American one is reported and no note is
printed, and with `--language en-US` the British one is reported and the line `info: --language en-US replaces en-GB
for the override at` the settings file, line 4, and column 5 is printed. This scenario is tested by
`LintRunner_Run_LanguageOptionAndOverrideLanguage_OptionWinsAndNoteIsPrinted`.

**LintRunner_Run_LanguageNoteOnCleanRun_DoesNotFail**: The language note is printed for a clean file and the exit code
is 0. This scenario is tested by `LintRunner_Run_LanguageNoteOnCleanRun_DoesNotFail`.

**LintRunner_Run_LanguageOptionSameDictionaryAsOverride_PrintsNoNote**: An override for `en` and the option `en-US`
select the same dictionary; the run succeeds and prints no note. This scenario is tested by
`LintRunner_Run_LanguageOptionSameDictionaryAsOverride_PrintsNoNote`.

**LintRunner_Run_UnmatchedOverrideOnFullRun_PrintsWarningAndSucceeds**: A run with no patterns and an override that
matches nothing prints the warning in compiler style at the override's position, before the summary, and the exit code
is 0. This scenario is tested by `LintRunner_Run_UnmatchedOverrideOnFullRun_PrintsWarningAndSucceeds`.

**LintRunner_Run_UnmatchedOverrideWithCommandLinePatterns_PrintsNoWarning**: The same settings with a pattern on the
command line print no warning. This scenario is tested by
`LintRunner_Run_UnmatchedOverrideWithCommandLinePatterns_PrintsNoWarning`.

**LintRunner_Run_UnmatchedOverrideWithExplicitConfig_NamesTheConfigFile**: With `--config`, the warning starts with
the settings file as it was typed. This scenario is tested by
`LintRunner_Run_UnmatchedOverrideWithExplicitConfig_NamesTheConfigFile`.

**LintRunner_Run_AllFilesDisabled_SucceedsWithInformationNote**: With `enabled: false` and two misspelled files, the
exit code is 0, the line `info: Spell checking is disabled by the settings for all 2 selected file(s).` is printed,
and there is neither a "No files matched" error nor a finding. This scenario is tested by
`LintRunner_Run_AllFilesDisabled_SucceedsWithInformationNote`.

**LintRunner_Run_DisabledFileNamedOnCommandLine_IsSkipped**: A misspelled file that an override disables and that is
named on the command line is not reported, the exit code is 0, and the information note says one file was disabled.
This scenario is tested by `LintRunner_Run_DisabledFileNamedOnCommandLine_IsSkipped`.

**LintRunner_Run_NothingMatchesAndSettingsDisableFiles_StillFailsWithoutAllowEmpty**: With checking switched off, a
pattern that matches no file gives exit code 1 and "No files matched"; with `--allow-empty` it gives 0 and no note
about disabled files. This scenario is tested by
`LintRunner_Run_NothingMatchesAndSettingsDisableFiles_StillFailsWithoutAllowEmpty`.

**LintRunner_Run_AbsolutePathChangesSearchRoot_MatchesOverridesRelativeToSettingsRoot**: An override for `docs/**`
accepts a word; a file in `docs` named by its absolute path is clean, and a file elsewhere named the same way still
reports the word. This scenario is tested by
`LintRunner_Run_AbsolutePathChangesSearchRoot_MatchesOverridesRelativeToSettingsRoot`.

**LintRunner_Run_AbsolutePathOutsideSettingsRoot_MatchesNoOverride**: A file in a directory beside the settings root,
named by its absolute path, matches no override for `**`, so its word is reported and the exit code is 1. This
scenario is tested by `LintRunner_Run_AbsolutePathOutsideSettingsRoot_MatchesNoOverride`.

**LintRunner_Run_OrderedFilesList_ExcludesAndSelectsAgain**: A `files` list of `**/*.md`, `!skip/**`, and `skip/keep/**`
leaves the file in `skip` unchecked and checks the re-included file, which is reported; two files are checked. This
scenario is tested by `LintRunner_Run_OrderedFilesList_ExcludesAndSelectsAgain`.

**LintRunner_Run_InvalidOverride_ReportsPositionedErrorAndChecksNothing**: An override with a `files` key gives the
error with the settings file, line 3, and column 5 that overrides cannot change which files are selected, exit code
1, and no summary. This scenario is tested by `LintRunner_Run_InvalidOverride_ReportsPositionedErrorAndChecksNothing`.
