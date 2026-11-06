## Program

### Verification Approach

`Program` is verified with unit tests defined in `ProgramTests.cs`. Because `Program` directly
instantiates `Context` from real arguments and calls `Validation.Run` and `LintRunner.Run` when needed, no mocking
is required. Tests pass controlled argument arrays and assert on captured console output, the log of a silent
context, and exit codes. Tests that reach the check use `--root` with a private temporary directory, so nothing
depends on the current directory; the behavior of the check itself is verified in the _Linting_ verification.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Exit code 0 is returned for all valid flag combinations that request information or validation, and for a check
  that finds nothing.
- Exit code 1 is returned when invalid arguments are supplied to `Program.Main`, and when a check has nothing to
  check or is cancelled.
- `Program.Version` returns a non-empty, non-null string.

### Test Scenarios

**Program_Run_WithVersionFlag_DisplaysVersionOnly**: `Program.Run` is called with a context
created from `["--version"]`; the output contains the version string, "Copyright" does not
appear, the banner prefix does not appear, and exit code is 0, confirming version-only output
with no banner. This scenario is tested by `Program_Run_WithVersionFlag_DisplaysVersionOnly`.

**Program_Run_WithHelpFlag_DisplaysUsageInformation**: `Program.Run` is called with a context
from `["--help"]`; the output contains "Usage:", "Options:", "--version", and "--help" and exit
code is 0. This scenario is tested by `Program_Run_WithHelpFlag_DisplaysUsageInformation`.

**Program_Run_WithValidateFlag_RunsValidation**: `Program.Run` is called with a context from
`["--validate"]`; the output contains "Total Tests:" and exit code is 0, confirming the
self-validation suite is invoked and completes successfully. This scenario is tested by
`Program_Run_WithValidateFlag_RunsValidation`.

**Program_Run_NoPatterns_DisplaysBannerAndUsageError**: `Program.Run` is called with a context from `--root` naming
an empty directory; the output contains the tool name, the copyright notice, and a usage error that says what to
add, and exit code is 1. This scenario is tested by `Program_Run_NoPatterns_DisplaysBannerAndUsageError`.

**Program_Run_Patterns_RunsCheck**: `Program.Run` is called with a context from `--root` and the name of a file
that has a misspelling; the first line is the banner, the finding follows in the compiler style, and exit code is
1, confirming that the check is run. This scenario is tested by `Program_Run_Patterns_RunsCheck`.

**Program_Run_HelpFlag_ListsAllOptionsAndExitCodes**: `Program.Run` is called with `["--help"]`; the output
contains the usage line with the pattern arguments, every option of the tool, and the exit codes. This scenario is
tested by `Program_Run_HelpFlag_ListsAllOptionsAndExitCodes`.

**Program_RunCancellable_CancelledToken_ReportsCancellationAndFails**: `Program.RunCancellable` is called with a
token that is already cancelled and a file with a misspelling; the output says the check was cancelled, contains
no finding, and exit code is 1. This scenario is tested by
`Program_RunCancellable_CancelledToken_ReportsCancellationAndFails`.

**Program_Main_DuplicateOption_ReturnsNonZeroExitCode**: `Program.Main` is invoked with `--root` twice; exit code
is 1 and standard error names the repeated option. This scenario is tested by
`Program_Main_DuplicateOption_ReturnsNonZeroExitCode`.

**Program_Version_Read_ReturnsNonEmptyString**: The `Program.Version` static property is read; the
returned string is non-empty and non-null, confirming the version is resolvable from the
assembly attributes. This scenario is tested by `Program_Version_Read_ReturnsNonEmptyString`.

**Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode**: `Program.Main` is invoked with
`["--invalid-argument"]`; the exit code is 1, confirming that invalid arguments are caught and
handled without re-throwing. This scenario is tested by
`Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode`.

**Program_Run_WithShortVersionFlag_DisplaysVersion**: `Program.Run` is called with a context
from `["-v"]`; the output contains the version string and exit code is 0. This scenario is
tested by `Program_Run_WithShortVersionFlag_DisplaysVersion`.

**Program_Run_WithShortHelpFlag_DisplaysUsage**: `Program.Run` is called with a context from
`["-h"]`; the output contains "Usage:" and "Options:" and exit code is 0. This scenario is
tested by `Program_Run_WithShortHelpFlag_DisplaysUsage`.

**Program_Run_WithQuestionMarkFlag_DisplaysUsage**: `Program.Run` is called with a context from
`["-?"]`; the output contains "Usage:" and "Options:" and exit code is 0. This scenario is
tested by `Program_Run_WithQuestionMarkFlag_DisplaysUsage`.
