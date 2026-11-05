## Configuration

### Verification Approach

The `Configuration` subsystem is verified by integration tests in `ConfigurationSubsystemTests.cs`. They
write real settings files into a per-test temporary directory and load them through `SettingsLoader`,
then assert on the resulting `TextLintSettings` or diagnostics, so `SettingsLoader` and
`TextLintSettings` are exercised together. The subsystem's documented dependencies are real:
YamlDotNet (an OTS item) parses the file, and the units the loader uses to validate values are real:
`GlobPatternExpander` from the `FileSelection` subsystem validates patterns, `SpellingDictionary` from the
`Dictionaries` subsystem validates the language, and `WordTokenizer` from the `TextScanning` subsystem
validates word-list entries. Nothing is mocked.

### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each
test uses its own uniquely named directory, so tests share no state and may run in parallel.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A valid file loads into settings, and a missing file returns an error diagnostic instead of throwing.
- Every supported key populates its setting, and an empty file yields the defaults.
- The six spelling keys populate their settings, with a size converted to bytes, and absent spelling keys
  leave language `en`, minimum word length 4, empty word lists, and no size limit.
- Word lists supplied in code are copied and cannot be changed afterwards, and out-of-range spelling values
  are rejected when settings are built in code.
- Settings built in code are immutable, and a copy of loaded settings leaves the original unchanged.
- Malformed YAML and several wrongly typed values are reported with file, line, and column, all of them.
- An unsupported key fails the load with an error naming the key and its position.
- An invalid glob pattern fails the load with an error at the pattern.
- An unsupported language, an unusable word-list entry, an invalid minimum word length, and an invalid
  maximum file size each fail the load with an error at the value, and several of them are all reported.

### Test Scenarios

**ConfigurationSubsystem_LoadAsync_ValidFile_ReturnsSettings**: A valid file is loaded; the result succeeds
with no diagnostics. This scenario is tested by `ConfigurationSubsystem_LoadAsync_ValidFile_ReturnsSettings`.

**ConfigurationSubsystem_LoadAsync_MissingFile_ReturnsErrorDiagnostic**: A path that does not exist is
loaded; no exception is thrown, no settings are returned, and the single diagnostic names the file. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_MissingFile_ReturnsErrorDiagnostic`.

**ConfigurationSubsystem_LoadAsync_AllSupportedKeys_PopulatesSettings**: A file that sets `version`,
`files`, `ignorePaths`, `enableGlobDot`, `language`, `words`, `ignoreWords`, `flagWords`, `minWordLength`,
and `maxFileSize` is loaded; every value is present. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_AllSupportedKeys_PopulatesSettings`.

**ConfigurationSubsystem_LoadAsync_EmptyFile_ReturnsDefaults**: An empty file is loaded; the settings hold
the defaults, including language `en`, minimum word length 4, and no size limit. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_EmptyFile_ReturnsDefaults`.

**ConfigurationSubsystem_LoadAsync_SpellingKeys_PopulatesSettings**: A file that sets the six spelling keys,
with a size of `1.5MB`, is loaded; every value is present and the size is 1572864 bytes. This scenario is
tested by `ConfigurationSubsystem_LoadAsync_SpellingKeys_PopulatesSettings`.

**ConfigurationSubsystem_LoadAsync_SpellingKeysAbsent_UsesCspellDefaults**: A file that sets no spelling key is
loaded; the language is `en`, the minimum word length is 4, the lists are empty, and there is no size limit.
This scenario is tested by `ConfigurationSubsystem_LoadAsync_SpellingKeysAbsent_UsesCspellDefaults`.

**ConfigurationSubsystem_Settings_ConstructedInCode_IsImmutable**: Settings are built in code from a mutable
list that is then cleared; the settings keep their values. This scenario is tested by
`ConfigurationSubsystem_Settings_ConstructedInCode_IsImmutable`.

**ConfigurationSubsystem_Settings_WordListsConstructedInCode_AreImmutable**: Settings are built in code from
mutable lists that are then changed, and an exposed list is cast to the mutable interface; the settings keep
their words and refuse the change. This scenario is tested by
`ConfigurationSubsystem_Settings_WordListsConstructedInCode_AreImmutable`.

**ConfigurationSubsystem_Settings_SpellingValuesOutOfRange_AreRejected**: Settings are built in code with a
zero minimum word length, a negative size, and a null language; each is refused. This scenario is tested by
`ConfigurationSubsystem_Settings_SpellingValuesOutOfRange_AreRejected`.

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
key `caseSensitive` on line 2 is loaded; no settings are returned and the error names the key at line 2,
column 1. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_UnsupportedKey_ReportsErrorWithKeyAndPosition`.

**ConfigurationSubsystem_LoadAsync_InvalidPattern_ReportsErrorWithPosition**: A file whose include pattern is
negated is loaded; no settings are returned and the error is at the pattern's line and names the pattern. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_InvalidPattern_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_AbsolutePathPattern_ReportsErrorWithPosition**: A file whose include pattern is
the network path `//server/share/**` is loaded; no settings are returned and the error is at line 2 column 5,
names the file, and says absolute paths are not supported. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_AbsolutePathPattern_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_UnsupportedLanguage_ReportsErrorWithPosition**: A file with the language
`fr` on line 2 is loaded; no settings are returned and the error is at line 2 column 11, names the file, and
says that `fr` is not supported. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_UnsupportedLanguage_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_UnusableWordEntry_ReportsErrorWithPosition**: A file whose `words` list holds
the entry `foo bar` on line 3 is loaded; no settings are returned and the error is at line 3 column 5, names
the file, the list, the entry, and says it contains white space. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_UnusableWordEntry_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_InvalidMinWordLength_ReportsErrorWithPosition**: A file with
`minWordLength: 0` is loaded; no settings are returned and the error names the key at line 1 column 16. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_InvalidMinWordLength_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_InvalidMaxFileSize_ReportsErrorWithPosition**: A file with
`maxFileSize: 5TB` is loaded; no settings are returned and the error names the key at line 1 column 14. This
scenario is tested by `ConfigurationSubsystem_LoadAsync_InvalidMaxFileSize_ReportsErrorWithPosition`.

**ConfigurationSubsystem_LoadAsync_MultipleSpellingErrors_ReportsAll**: A file with an unsupported language,
an unusable flagged word, a zero minimum word length, and a negative size is loaded; all four errors are
reported, on lines 1, 3, 4, and 5, and no settings are returned. This scenario is tested by
`ConfigurationSubsystem_LoadAsync_MultipleSpellingErrors_ReportsAll`.
