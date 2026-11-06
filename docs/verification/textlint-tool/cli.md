## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`. Each
test exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, and exit codes. `Validation` (part of the `SelfTest` subsystem)
executes its real logic in scenarios that exercise the `--validate` path, and the `Linting` subsystem executes its
real logic in the scenarios that exercise a check; no mocking is applied at any level. Check scenarios capture
what is written through the log of a silent context and use `--root` with a private temporary directory, so
they never depend on or change the current directory.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All integration tests pass with zero failures.
- All supported flags produce the correct observable behavior when passed through `Context.Create`
  and `Program.Run`.
- Invalid arguments produce exit code 1 and an error message.
- Positional patterns and the check options reach the check unchanged, and a check that fails or has nothing to
  check gives exit code 1.
- Log files and results files are created at the specified paths when the corresponding flags
  are used.

### Test Scenarios

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits**: Arguments `["--version"]`
are passed through `Context.Create` and `Program.Run`; standard output contains the version
string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits`.

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag**: Arguments
`["-v"]` are passed through `Context.Create` and `Program.Run`; standard output contains the
version string and exit code is 0. This scenario is tested by
`CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits**: Arguments `["--help"]` are
passed through `Context.Create` and `Program.Run`; standard output contains help text and exit
code is 0. This scenario is tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag**: Arguments
`["-?"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by
`CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag**: Arguments
`["-h"]` are passed; standard output contains help text and exit code is 0. This scenario is
tested by `CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag`.

**CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits**: Arguments `["--validate"]`
are passed; standard output contains `"Total Tests:"` and exit code is 0. This scenario is
tested by `CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits`.

**CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput**: Arguments `["--version", "--silent"]`
are passed; standard output is empty and exit code is 0, confirming `--silent` suppresses all
console output of a command line that was understood (one that cannot be parsed has no `Context` yet; its error is
still written to standard error, as verified by the _Program_ unit). This scenario is tested by
`CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput`.

**CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--results", "<tmp>.trx"]` are passed; a results file is created at the
specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile**: Arguments `["--log", "<tmp>.log"]` are
passed; a log file is created at the specified path and exit code is 0. This scenario is tested
by `CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile`.

**CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero**: Arguments
`["--unknown-flag"]` are passed directly to `Program.Main`; exit code is 1 and standard error
contains an error message including the unknown flag. This scenario is tested by
`CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero`.

**CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr**: A `Context` is created with
no arguments and `WriteError` is called with a known message; standard error receives the
message and exit code is non-zero. This scenario is tested by
`CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr`.

**CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile**: Arguments
`["--validate", "--result", "<tmp>.trx"]` (legacy alias) are passed; a results file is created
at the specified path and exit code is 0. This scenario is tested by
`CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile`.

**CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth**: Arguments `["--depth", "2"]`
are passed; the heading depth is set to 2 and exit code is 0, confirming the `--depth` flag is
correctly parsed and propagated through `Context`. This scenario is tested by
`CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth`.

**CliSubsystem_PatternFlow_ContextAndProgram_ChecksNamedFiles**: Arguments `--root <tmp> bad.md` are passed with a
clean file beside the named one; the finding of the named file is reported, the other file is not, and exit code
is 1, confirming the positional pattern reaches the check. This scenario is tested by
`CliSubsystem_PatternFlow_ContextAndProgram_ChecksNamedFiles`.

**CliSubsystem_OptionFlow_ContextAndProgram_AppliesCheckOptions**: Arguments with `--root`, `--config` (a file
outside the root that selects British English), `--language en-US`, and `--max-parallelism 2` are passed; the
British spelling is reported at its column and the American one is not, confirming that each option took effect and
that the language option wins over the settings. This scenario is tested by
`CliSubsystem_OptionFlow_ContextAndProgram_AppliesCheckOptions`.

**CliSubsystem_ExitCodeFlow_ContextAndProgram_FailsUnlessEmptyAllowed**: A clean file gives exit code 0; a pattern
that matches nothing gives 1; the same pattern with `--allow-empty` gives 0. This scenario is tested by
`CliSubsystem_ExitCodeFlow_ContextAndProgram_FailsUnlessEmptyAllowed`.

**CliSubsystem_InvalidOptionFlow_ContextAndProgram_RejectsMalformedOptions**: `Program.Main` is called with a
missing option value, a repeated option, an out-of-range number, and an unknown option after a pattern; each gives
exit code 1 and an error that names the problem. This scenario is tested by
`CliSubsystem_InvalidOptionFlow_ContextAndProgram_RejectsMalformedOptions`.
