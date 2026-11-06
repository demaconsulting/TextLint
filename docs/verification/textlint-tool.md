# TextLintTool

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, execution, and output — and validates both exit code and
combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed.

`Runner.Run` merges stdout and stderr into a single combined output string. Per-stream assertions
(e.g., "standard error is empty") are not possible at this level; all assertions are made
against the combined output. `Runner.RunIn` does the same in a chosen working directory, which the scenarios
about the default settings file and the root option need; the tests never change the working directory of the
test process itself. Every check scenario creates its own temporary directory with sample files.
The misspellings used in the samples (`wrold`, `xyzzyq`) are words that no dictionary holds.

The package requirement is verified differently, because the artifact to inspect is the produced
NuGet package and not a running process. After the build pipeline packs the tool, it runs FileAssert
(`.fileassert.yaml`, tag `tool-package`) against the real `DemaConsulting.TextLint.Tool.<version>.nupkg`.

## Test Environment

Integration tests run on .NET 10.0 across Windows, Linux, and macOS.
All scenarios are expected to produce identical results on all supported runtime and platform
combinations. Temporary files and directories are created and cleaned up within each test.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Exit code 0 is returned only when every selected file was checked and no issue was found, and for the version,
  help, and successful validation invocations.
- Exit code non-zero is returned for all invalid argument combinations, for findings, for files that could not be
  checked, for an empty selection, and for every failure a user can cause.
- Results files are created at the specified paths when `--results` is used with `--validate`.
- Silent mode (`--silent`) produces empty combined output.
- The tool package contains `THIRD-PARTY-NOTICES.txt` at its root with the notices of the third-party word
  lists in the bundled library.

## Test Scenarios

**TextLintTool_VersionFlag_Provided_OutputsVersion**: The `--version` flag is passed as
the sole argument; the tool outputs the version string and exits with code 0. This scenario is
tested by `TextLintTool_VersionFlag_Provided_OutputsVersion`.

**TextLintTool_HelpFlag_Provided_OutputsUsageInformation**: The `--help` flag is passed
as the sole argument; the combined output contains "Usage" and "Options", every option of the tool, and the exit
codes, and the tool exits with code 0. This scenario is tested by
`TextLintTool_HelpFlag_Provided_OutputsUsageInformation`.

**TextLintTool_ValidateFlag_Provided_RunsValidation**: The `--validate` flag is passed as
the sole argument; the combined output contains "Total Tests:" and the tool exits with code 0,
confirming the self-validation suite runs and completes successfully. This scenario is tested by
`TextLintTool_ValidateFlag_Provided_RunsValidation`.

**TextLintTool_ValidateWithTrxResults_Requested_GeneratesTrxFile**: The `--validate` flag
is combined with `--results <path>.trx`; a TRX file containing a `<TestRun` XML element is
created at the specified path and the tool exits with code 0. This scenario is tested by
`TextLintTool_ValidateWithTrxResults_Requested_GeneratesTrxFile`.

**TextLintTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: The `--validate` flag
is combined with `--results <path>.xml`; a JUnit XML file containing a `<testsuites` XML element
is created at the specified path and the tool exits with code 0. This scenario is tested by
`TextLintTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile`.

**TextLintTool_SilentFlag_Provided_SuppressesOutput**: The `--version` and `--silent`
flags are passed together; the combined output is empty or whitespace-only while the tool exits
with code 0, confirming silent mode suppresses all console output. This scenario is tested by
`TextLintTool_SilentFlag_Provided_SuppressesOutput`.

**TextLintTool_LogFlag_Provided_WritesOutputToFile**: The `--log <path>` flag is passed
pointing to a temporary file; the tool exits with code 0 and the log file is created containing
output that also appears in the combined console output. This scenario is tested by
`TextLintTool_LogFlag_Provided_WritesOutputToFile`.

**TextLintTool_UnknownArgument_Provided_ReturnsError**: An unrecognized argument
(`--unknown`) is passed; the tool exits with a non-zero code and the combined output contains
an error message identifying the unknown argument. This scenario is tested by
`TextLintTool_UnknownArgument_Provided_ReturnsError`.

**TextLintTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: The `--validate`
flag is combined with `--depth 3`; the combined output contains `###` (heading at depth 3) and
the tool exits with code 0. This scenario is tested by
`TextLintTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`.

**TextLintTool_NoArguments_Invoked_DisplaysBannerAndUsageError**: The tool is invoked with no arguments in an
empty working directory; the combined output contains the tool name and copyright notice and a usage error that
says what to add, and the exit code is 1. This scenario is tested by
`TextLintTool_NoArguments_Invoked_DisplaysBannerAndUsageError`.

**TextLintTool_ResultAlias_LegacyFlag_WritesResultsFile**: The `--validate` flag is
combined with `--result <path>.trx` (the legacy alias); a TRX file is created at the specified
path and the tool exits with code 0. This scenario is tested by
`TextLintTool_ResultAlias_LegacyFlag_WritesResultsFile`.

**TextLintTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: The `--validate`
flag is combined with `--results <path>.bad` (unsupported extension); the tool exits with a
non-zero code and no file is created at the specified path. This scenario is tested by
`TextLintTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`.

**TextLintTool_ValidateFlag_Provided_RunsLintScenarios**: The `--validate` flag is passed; the output reports each
of the ten spell-checking scenarios as passed and the tool exits with code 0, so the self-validation proves real
spell checking and not only the version and help output. This scenario is tested by
`TextLintTool_ValidateFlag_Provided_RunsLintScenarios`.

**TextLintTool_Check_MisspelledFile_ReportsIssueAndExitsOne**: A file containing a misspelled word is named on
the command line; the output contains `a.md(1,7): error: Unknown word 'wrold'` and the exit code is 1. This
scenario is tested by `TextLintTool_Check_MisspelledFile_ReportsIssueAndExitsOne`.

**TextLintTool_Check_CleanFile_ExitsZero**: A correctly spelled file is named; the output contains the summary
with zero issues and the exit code is 0. This scenario is tested by `TextLintTool_Check_CleanFile_ExitsZero`.

**TextLintTool_Check_DefaultSettingsFile_AppliesSettings**: The tool runs with no arguments in a directory whose
`.textlint.yaml` selects markdown files and flags a word; the flagged word is reported with its suggestion at the
right column and the exit code is 1. This scenario is tested by
`TextLintTool_Check_DefaultSettingsFile_AppliesSettings`.

**TextLintTool_Check_ConfigOption_UsesNamedFile**: A settings file with another name accepts the misspelled word;
naming it with `--config` makes the run succeed. This scenario is tested by
`TextLintTool_Check_ConfigOption_UsesNamedFile`.

**TextLintTool_Check_ConfigOption_MissingFile_ReportsErrorAndExitsOne**: `--config` names a file that does not
exist; the output names the file, nothing is checked, and the exit code is 1. This scenario is tested by
`TextLintTool_Check_ConfigOption_MissingFile_ReportsErrorAndExitsOne`.

**TextLintTool_Check_InvalidSettings_ReportsErrorAndExitsOne**: A settings file with an unsupported key is in the
working directory; the output reports the error at its line and column, nothing is checked, and the exit code is

1. This scenario is tested by `TextLintTool_Check_InvalidSettings_ReportsErrorAndExitsOne`.

**TextLintTool_Check_GlobArguments_ReplaceConfiguredFiles**: The settings name `b.md` and the command line names
`a.md`; only `a.md` is checked. This scenario is tested by
`TextLintTool_Check_GlobArguments_ReplaceConfiguredFiles`.

**TextLintTool_Check_RootOption_ResolvesPathsFromRoot**: The tool runs in an unrelated directory with `--root`
naming a project; the project's settings apply and the reported path is relative to the project. This scenario is
tested by `TextLintTool_Check_RootOption_ResolvesPathsFromRoot`.

**TextLintTool_Check_AbsolutePath_ReportsAbsolutePath**: A file is named by its absolute path from an unrelated
directory; the finding is reported under that absolute path. This scenario is tested by
`TextLintTool_Check_AbsolutePath_ReportsAbsolutePath`.

**TextLintTool_Check_PlatformRelativePath_ChecksFile**: A relative path is written the way the platform's shell
completes it (with backslashes on Windows); the file is found and reported by its portable relative path. This
scenario is tested by `TextLintTool_Check_PlatformRelativePath_ChecksFile`.

**TextLintTool_Check_LanguageOption_SelectsDictionary**: A file with the British spelling of a word is reported
with `--language en-US` and accepted with `--language en-GB`. This scenario is tested by
`TextLintTool_Check_LanguageOption_SelectsDictionary`.

**TextLintTool_Check_NoFilesMatched_ExitsOne**: A pattern that matches nothing gives exit code 1 and an
explanation that names `--allow-empty`. This scenario is tested by `TextLintTool_Check_NoFilesMatched_ExitsOne`.

**TextLintTool_Check_NoFilesMatchedAllowEmpty_ExitsZero**: The same pattern with `--allow-empty` gives exit code
0 and a summary of zero files. This scenario is tested by `TextLintTool_Check_NoFilesMatchedAllowEmpty_ExitsZero`.

**TextLintTool_Check_BinaryFile_ReportsInfoAndExitsZero**: A binary file and a clean text file are selected; the
binary file is reported as information, counted as skipped, and the exit code is 0. This scenario is tested by
`TextLintTool_Check_BinaryFile_ReportsInfoAndExitsZero`.

**TextLintTool_Check_MaxParallelism_GivesSameResult**: The same files are checked with one worker and with
four; the output and the exit code are identical. This scenario is tested by
`TextLintTool_Check_MaxParallelism_GivesSameResult`.

**TextLintTool_Check_DuplicateOption_ReportsErrorAndExitsOne**: `--root` is given twice; the output names the
option, nothing is checked, and the exit code is 1. This scenario is tested by
`TextLintTool_Check_DuplicateOption_ReportsErrorAndExitsOne`.

**TextLintTool_Check_MissingRoot_ReportsErrorAndExitsOne**: `--root` names a directory that does not exist; the
output names the directory, contains no unhandled-exception report, and the exit code is 1. This scenario is
tested by `TextLintTool_Check_MissingRoot_ReportsErrorAndExitsOne`.

**TextLintTool_Check_InvalidPattern_ReportsErrorAndExitsOne**: A pattern that uses an unsupported wildcard gives
an error that names the pattern and exit code 1. This scenario is tested by
`TextLintTool_Check_InvalidPattern_ReportsErrorAndExitsOne`.

Interruption by the user (Ctrl+C) cannot be delivered to a child process on every platform, so the cancellation
that `TextLintTool-System-FailureReporting` also requires is verified in-process: see
`Program_RunCancellable_CancelledToken_ReportsCancellationAndFails` in the _Program_ verification and the
cancellation scenarios of the _Linting_ verification.

**TextLintTool_Package_Packed_ContainsThirdPartyNotices**: FileAssert inspects the produced tool package and
finds exactly one `THIRD-PARTY-NOTICES.txt` at its root whose text names the author of the English word lists,
carries the permission sentence of their license verbatim, and names the publisher of the technical word lists.
This scenario is tested by `TextLintTool_Package_Packed_ContainsThirdPartyNotices`.

<!-- cspell:ignore wrold xyzzyq -->
