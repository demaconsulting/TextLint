### Context

#### Verification Approach

`Context` is verified with unit tests defined in `ContextTests.cs`. Because `Context` depends
only on .NET BCL types (`Console`, `StreamWriter`), no mocking or test doubles are required.
Tests call `Context.Create` with controlled argument arrays, inspect the resulting properties
and exit codes, and verify output written to captured streams.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- All flag properties are correctly set for each recognized argument.
- `ArgumentException` is thrown for all unknown or malformed arguments, including missing, empty, repeated, and
  out-of-range option values.
- `ExitCode` is 1 after `WriteError` or `MarkFailed` is called and 0 otherwise; `MarkFailed` writes nothing.
- Positional patterns are kept exactly as typed, in order, as an immutable list.
- Silent mode suppresses console output but does not affect the log file.

#### Test Scenarios

**Context_Create_NoArguments_ReturnsDefaultContext**: `Context.Create` is called with an empty
argument array; all boolean flags are false, `ResultsFile` is null, `HeadingDepth` is 1, and
exit code is 0. This scenario is tested by `Context_Create_NoArguments_ReturnsDefaultContext`.

**Context_Create_VersionFlag_SetsVersionTrue**: `Context.Create` is called with `["--version"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_VersionFlag_SetsVersionTrue`.

**Context_Create_ShortVersionFlag_SetsVersionTrue**: `Context.Create` is called with `["-v"]`;
the `Version` property is true. This scenario is tested by
`Context_Create_ShortVersionFlag_SetsVersionTrue`.

**Context_Create_HelpFlag_SetsHelpTrue**: `Context.Create` is called with `["--help"]`; the
`Help` property is true. This scenario is tested by `Context_Create_HelpFlag_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_H_SetsHelpTrue**: `Context.Create` is called with `["-h"]`; the
`Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_H_SetsHelpTrue`.

**Context_Create_ShortHelpFlag_Question_SetsHelpTrue**: `Context.Create` is called with `["-?"]`;
the `Help` property is true. This scenario is tested by
`Context_Create_ShortHelpFlag_Question_SetsHelpTrue`.

**Context_Create_SilentFlag_SetsSilentTrue**: `Context.Create` is called with `["--silent"]`;
the `Silent` property is true. This scenario is tested by
`Context_Create_SilentFlag_SetsSilentTrue`.

**Context_Create_ValidateFlag_SetsValidateTrue**: `Context.Create` is called with
`["--validate"]`; the `Validate` property is true. This scenario is tested by
`Context_Create_ValidateFlag_SetsValidateTrue`.

**Context_Create_ResultsFlag_SetsResultsFile**: `Context.Create` is called with
`["--results", "output.trx"]`; `ResultsFile` equals `"output.trx"`. This scenario is tested by
`Context_Create_ResultsFlag_SetsResultsFile`.

**Context_Create_LogFlag_OpensLogFile**: `Context.Create` is called with
`["--log", "<tmp>.log"]` and then `WriteLine` is called with a test message; the log file is
created and contains the test message. This scenario is tested by
`Context_Create_LogFlag_OpensLogFile`.

**Context_Create_UnknownArgument_ThrowsArgumentException**: `Context.Create` is called with
`["--unknown"]`; an `ArgumentException` containing "Unsupported argument" is thrown. This
scenario is tested by `Context_Create_UnknownArgument_ThrowsArgumentException`.

**Context_Create_LogFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--log"]` (value missing); an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_LogFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--results"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_ResultAliasFlag_SetsResultsFile**: `Context.Create` is called with
`["--result", "output.trx"]` (legacy alias); `ResultsFile` equals `"output.trx"`, identical to
the `--results` flag behavior. This scenario is tested by
`Context_Create_ResultAliasFlag_SetsResultsFile`.

**Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is
called with `["--result"]` (value missing); an `ArgumentException` is thrown. This scenario is
tested by `Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_SetsHeadingDepth**: `Context.Create` is called with
`["--depth", "3"]`; `HeadingDepth` equals 3. This scenario is tested by
`Context_Create_DepthFlag_SetsHeadingDepth`.

**Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth**: `Context.Create` is called with an
empty argument array; `HeadingDepth` equals 1 (the default; valid range is 1–6). This scenario
is tested by `Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth`.

**Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException**: `Context.Create` is called
with `["--depth"]` (value missing); an `ArgumentException` is thrown. This scenario is tested
by `Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException**: `Context.Create` is
called with `["--depth", "abc"]`; an `ArgumentException` is thrown. This scenario is tested by
`Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException**: `Context.Create` is called with
`["--depth", "0"]` (below the minimum of 1); an `ArgumentException` is thrown. This scenario
is tested by `Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException`.

**Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException**: `Context.Create` is
called with `["--depth", "7"]` (above the maximum of 6); an `ArgumentException` is thrown.
This scenario is tested by `Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException`.

**Context_WriteLine_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteLine` with
a test message; the message appears on standard output. This scenario is tested by
`Context_WriteLine_NotSilent_WritesToConsole`.

**Context_WriteLine_Silent_DoesNotWriteToConsole**: A silent `Context` (created with
`["--silent"]`) calls `WriteLine`; standard output receives nothing, confirming `--silent`
suppresses normal output. This scenario is tested by
`Context_WriteLine_Silent_DoesNotWriteToConsole`.

**Context_WriteError_Silent_DoesNotWriteToConsole**: A silent `Context` calls `WriteError`;
standard error receives nothing, confirming `--silent` also suppresses error output. This
scenario is tested by `Context_WriteError_Silent_DoesNotWriteToConsole`.

**Context_WriteError_Called_SetsErrorExitCode**: A `Context` calls `WriteError`; `ExitCode` is 1
after the call. This scenario is tested by `Context_WriteError_Called_SetsErrorExitCode`.

**Context_WriteError_NotSilent_WritesToConsole**: A non-silent `Context` calls `WriteError`
with a test message; the message appears on standard error. This scenario is tested by
`Context_WriteError_NotSilent_WritesToConsole`.

**Context_WriteError_WithLogFile_WritesToLogFile**: A `Context` created with
`["--silent", "--log", "<tmp>.log"]` calls `WriteError` with a test message; the message appears
in the log file, confirming errors are always written to the log regardless of `--silent`. This
scenario is tested by `Context_WriteError_WithLogFile_WritesToLogFile`.

**Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException**:
`Context.Create` is called with `["--log", "/invalid/\x00path.log"]`; an
`InvalidOperationException` is thrown because the log file cannot be opened. This
scenario is tested by `Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException`.

**Context_Create_NoCheckOptions_ReturnsCheckDefaults**: `Context.Create` is called with no arguments; `Globs` is
empty, `ConfigFile`, `Root`, `Language`, and `MaxParallelism` are null, and `AllowEmpty` is false. This scenario
is tested by `Context_Create_NoCheckOptions_ReturnsCheckDefaults`.

**Context_Create_PositionalArguments_SetsGlobsInOrder**: A relative pattern, a backslash path, and an absolute
path are passed; `Globs` holds them exactly as typed and in order. This scenario is tested by
`Context_Create_PositionalArguments_SetsGlobsInOrder`.

**Context_Create_Globs_AreImmutable**: The `Globs` of a context are cast to a mutable list interface and an item
is added; `NotSupportedException` is thrown. This scenario is tested by `Context_Create_Globs_AreImmutable`.

**Context_Create_OptionsAndGlobsMixed_ParsesBoth**: Patterns before, between, and after options, including an
option value, are passed; the patterns are kept in order and the option values are not taken for patterns. This
scenario is tested by `Context_Create_OptionsAndGlobsMixed_ParsesBoth`.

**Context_Create_ConfigFlag_SetsConfigFile**: `["--config", "my settings.yaml"]` sets `ConfigFile` as typed. This
scenario is tested by `Context_Create_ConfigFlag_SetsConfigFile`.

**Context_Create_RootFlag_SetsRoot**: `["--root", "some/dir"]` sets `Root` as typed. This scenario is tested by
`Context_Create_RootFlag_SetsRoot`.

**Context_Create_LanguageFlag_SetsLanguage**: `["--language", "en-US,en-GB"]` sets `Language` unchanged. This
scenario is tested by `Context_Create_LanguageFlag_SetsLanguage`.

**Context_Create_AllowEmptyFlag_SetsAllowEmptyTrue**: `["--allow-empty"]` sets `AllowEmpty`. This scenario is
tested by `Context_Create_AllowEmptyFlag_SetsAllowEmptyTrue`.

**Context_Create_MaxParallelismFlag_SetsMaxParallelism**: `["--max-parallelism", "4"]` sets `MaxParallelism` to 4.
This scenario is tested by `Context_Create_MaxParallelismFlag_SetsMaxParallelism`.

**Context_Create_MaxParallelismFlag_InvalidValue_ThrowsArgumentException**: Values that are not integers, are zero
or negative, exceed the largest integer, or are not whole numbers each throw an `ArgumentException` that states the
allowed range. This scenario is tested by
`Context_Create_MaxParallelismFlag_InvalidValue_ThrowsArgumentException`.

**Context_Create_ValueOptionWithoutValue_ThrowsArgumentException**: Each of `--config`, `--root`, `--language`, and
`--max-parallelism` given last without a value throws an `ArgumentException` that names the option. This scenario
is tested by `Context_Create_ValueOptionWithoutValue_ThrowsArgumentException`.

**Context_Create_ValueOptionWithBlankOrOptionValue_ThrowsArgumentException**: An empty value, a white-space value,
and a value that is the next option each throw an `ArgumentException` that names the option. This scenario is
tested by `Context_Create_ValueOptionWithBlankOrOptionValue_ThrowsArgumentException`.

**Context_Create_DuplicateValueOption_ThrowsArgumentException**: Each of `--config`, `--root`, `--language`,
`--max-parallelism`, `--log`, and `--depth` given twice throws an `ArgumentException` that names the option. This
scenario is tested by `Context_Create_DuplicateValueOption_ThrowsArgumentException`.

**Context_Create_ResultsAndAlias_ThrowsArgumentException**: `--results` followed by its alias `--result` is a
duplicate. This scenario is tested by `Context_Create_ResultsAndAlias_ThrowsArgumentException`.

**Context_Create_UnknownOptionAfterGlob_ThrowsArgumentException**: A pattern followed by an unknown option throws
an `ArgumentException` that names the option, so a misspelled option is never taken for a file. This scenario is
tested by `Context_Create_UnknownOptionAfterGlob_ThrowsArgumentException`.

**Context_Create_BlankGlob_ThrowsArgumentException**: An empty or white-space positional argument throws an
`ArgumentException`. This scenario is tested by `Context_Create_BlankGlob_ThrowsArgumentException`.

**Context_MarkFailed_Called_SetsErrorExitCodeSilently**: A `Context` calls `MarkFailed`; `ExitCode` is 1 and
nothing was written to standard output or standard error. This scenario is tested by
`Context_MarkFailed_Called_SetsErrorExitCodeSilently`.
