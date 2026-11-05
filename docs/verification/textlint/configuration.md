## Configuration

### Verification Approach

The `Configuration` subsystem is verified by integration tests in `ConfigurationSubsystemTests.cs`. They
write real settings files into a per-test temporary directory and load them through `SettingsLoader`,
then assert on the resulting `TextLintSettings` or diagnostics, so `SettingsLoader` and
`TextLintSettings` are exercised together. The subsystem's documented dependencies are real:
YamlDotNet (an OTS item) parses the file and `GlobPatternExpander` from the `FileSelection` subsystem
validates patterns. Nothing is mocked.

### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each
test uses its own uniquely named directory, so tests share no state and may run in parallel.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A valid file loads into settings, and a missing file returns an error diagnostic instead of throwing.
- Every supported key populates its setting, and an empty file yields the defaults.
- Settings built in code are immutable, and a copy of loaded settings leaves the original unchanged.
- Malformed YAML and several wrongly typed values are reported with file, line, and column, all of them.
- An unsupported key fails the load with an error naming the key and its position.
- An invalid glob pattern fails the load with an error at the pattern.

### Test Scenarios

**ConfigurationSubsystem_LoadAsync_ValidFile_ReturnsSettings**: A valid file is loaded; the result succeeds
with no diagnostics. This scenario is tested by `ConfigurationSubsystem_LoadAsync_ValidFile_ReturnsSettings`.

**ConfigurationSubsystem_LoadAsync_MissingFile_ReturnsErrorDiagnostic**: A path that does not exist is
loaded; no exception is thrown, no settings are returned, and the single diagnostic names the file. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_MissingFile_ReturnsErrorDiagnostic`.

**ConfigurationSubsystem_LoadAsync_AllSupportedKeys_PopulatesSettings**: A file that sets `version`,
`files`, `ignorePaths`, and `enableGlobDot` is loaded; every value is present. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_AllSupportedKeys_PopulatesSettings`.

**ConfigurationSubsystem_LoadAsync_EmptyFile_ReturnsDefaults**: An empty file is loaded; the settings hold
the defaults. This scenario is tested by `ConfigurationSubsystem_LoadAsync_EmptyFile_ReturnsDefaults`.

**ConfigurationSubsystem_Settings_ConstructedInCode_IsImmutable**: Settings are built in code from a mutable
list that is then cleared; the settings keep their values. This scenario is tested by
`ConfigurationSubsystem_Settings_ConstructedInCode_IsImmutable`.

**ConfigurationSubsystem_Settings_LoadedThenCopied_LeavesLoadedUnchanged**: Loaded settings are copied with
different include patterns; the loaded settings are unchanged. This scenario is tested by
`ConfigurationSubsystem_Settings_LoadedThenCopied_LeavesLoadedUnchanged`.

**ConfigurationSubsystem_LoadAsync_MalformedYaml_ReportsPosition**: A file with an unclosed list is loaded;
one error carries the file, line, and column. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_MalformedYaml_ReportsPosition`.

**ConfigurationSubsystem_LoadAsync_MultipleTypeErrors_ReportsAll**: A file with two wrongly typed values is
loaded; both errors are reported, on lines 1 and 2. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_MultipleTypeErrors_ReportsAll`.

**ConfigurationSubsystem_LoadAsync_UnsupportedKey_ReportsErrorWithKeyAndPosition**: A file with the unsupported
key `words` on line 2 is loaded; no settings are returned and the error names the key at line 2, column 1. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_UnsupportedKey_ReportsErrorWithKeyAndPosition`.

**ConfigurationSubsystem_LoadAsync_InvalidPattern_ReportsErrorWithPosition**: A file whose include pattern is
negated is loaded; no settings are returned and the error is at the pattern's line and names the pattern. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_InvalidPattern_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_AbsolutePathPattern_ReportsErrorWithPosition**: A file whose include pattern is
the network path `//server/share/**` is loaded; no settings are returned and the error is at line 2 column 5,
names the file, and says absolute paths are not supported. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_AbsolutePathPattern_ReportsErrorWithPosition`.
