### SettingsLoader

#### Verification Approach

`SettingsLoader` is verified with unit tests in `SettingsLoaderTests.cs`. Tests of file loading write real
files into a per-test temporary directory; tests of mapping and errors call the internal `Parse` method with
YAML text so no file is needed. YamlDotNet is a real dependency (an OTS item with its own requirements, not a
test double), and the unit's documented dependencies `TextLintSettings` and `GlobPatternExpander` are real.
Nothing is mocked: the loader's behavior is defined by how it maps real YAML, so a double would prove nothing.
Positions asserted in the tests are 1-based lines and columns.

#### Test Environment

File-loading tests need permission to create and delete files under the operating system's temporary
directory; each test uses its own uniquely named directory. All other tests need nothing beyond the standard
test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A file loads asynchronously, honors cancellation and a byte-order mark, and a missing file is an error that
  names it, with no position.
- Every supported key maps to its setting; empty and comment-only documents and null values are absent.
- Wrong types and shapes, a non-mapping root, more than one document, and every bad item are reported at their
  positions, all of them, and no settings are returned.
- Syntax errors, duplicate keys, tab indentation, and unknown aliases are reported with a position.
- Every unsupported key is reported by name at its position, case-sensitively, and no settings are returned.
- Invalid patterns in `files` and `ignorePaths` are reported at the item, naming the pattern and the reason;
  valid brace and bare-name patterns are accepted.
- Diagnostics carry the source, line, and column, format as `file(line,column): error: message`, and cannot be
  changed by the caller once returned.

#### Test Scenarios

**SettingsLoader_LoadAsync_ValidFile_ReturnsSettings**: A valid file is loaded; the result succeeds with the
file's include pattern. This scenario is tested by `SettingsLoader_LoadAsync_ValidFile_ReturnsSettings`.

**SettingsLoader_LoadAsync_MissingFile_ReturnsErrorNamingFile**: A missing file is loaded; no settings are
returned and one error names the file. This scenario is tested by
`SettingsLoader_LoadAsync_MissingFile_ReturnsErrorNamingFile`.

**SettingsLoader_LoadAsync_CancelledToken_ThrowsOperationCanceled**: A file is loaded with an already cancelled
token; the load is cancelled. This scenario is tested by
`SettingsLoader_LoadAsync_CancelledToken_ThrowsOperationCanceled`.

**SettingsLoader_LoadAsync_NullPath_ThrowsArgumentNullException**: A null path throws. This scenario is tested
by `SettingsLoader_LoadAsync_NullPath_ThrowsArgumentNullException`.

**SettingsLoader_LoadAsync_EmptyPath_ThrowsArgumentException**: An empty path throws `ArgumentException` rather
than the null variant. This scenario is tested by `SettingsLoader_LoadAsync_EmptyPath_ThrowsArgumentException`.

**SettingsLoader_LoadAsync_Utf8BomFile_ReturnsSettings**: A file that starts with a UTF-8 byte-order mark loads
normally. This scenario is tested by `SettingsLoader_LoadAsync_Utf8BomFile_ReturnsSettings`.

**SettingsLoader_LoadAsync_MissingFile_DiagnosticHasNoPosition**: The diagnostic for a missing file has no line
or column, and its text is the file followed by `: error:`. This scenario is tested by
`SettingsLoader_LoadAsync_MissingFile_DiagnosticHasNoPosition`.

**SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue**: Text that sets every supported key is parsed; every
value is mapped. This scenario is tested by `SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue`.

**SettingsLoader_Parse_EmptyDocument_ReturnsDefaultSettings**: Empty text yields default settings. This
scenario is tested by `SettingsLoader_Parse_EmptyDocument_ReturnsDefaultSettings`.

**SettingsLoader_Parse_CommentOnlyDocument_ReturnsDefaultSettings**: Text holding only a comment yields default
settings. This scenario is tested by `SettingsLoader_Parse_CommentOnlyDocument_ReturnsDefaultSettings`.

**SettingsLoader_Parse_NullListValue_TreatedAsAbsent**: A list key with no items and a list key set to `~`
yield empty lists. This scenario is tested by `SettingsLoader_Parse_NullListValue_TreatedAsAbsent`.

**SettingsLoader_Parse_NumericVersion_MapsToText**: An unquoted `0.2` version is carried as the text `0.2`.
This scenario is tested by `SettingsLoader_Parse_NumericVersion_MapsToText`.

**SettingsLoader_Parse_FlowAndBlockLists_BothMap**: One list in flow style and one in block style both map in
order. This scenario is tested by `SettingsLoader_Parse_FlowAndBlockLists_BothMap`.

**SettingsLoader_Parse_AnchorsAndAliases_AreResolved**: An alias to an anchored pattern resolves to the
anchored value. This scenario is tested by `SettingsLoader_Parse_AnchorsAndAliases_AreResolved`.

**SettingsLoader_Parse_FilesNotAList_ReportsErrorAtValue**: `files` given as text is one error at the value,
line 2 column 8. This scenario is tested by `SettingsLoader_Parse_FilesNotAList_ReportsErrorAtValue`.

**SettingsLoader_Parse_FilesItemNotText_ReportsErrorAtItem**: A list item that is a nested mapping is an error
at the item, line 3 column 5. This scenario is tested by `SettingsLoader_Parse_FilesItemNotText_ReportsErrorAtItem`.

**SettingsLoader_Parse_EnableGlobDotNotBoolean_ReportsError**: `enableGlobDot` set to ordinary text is an error
naming the key at its value. This scenario is tested by
`SettingsLoader_Parse_EnableGlobDotNotBoolean_ReportsError`.

**SettingsLoader_Parse_EnableGlobDotYes_ReportsError**: The YAML 1.1 spelling `yes` is rejected. This scenario
is tested by `SettingsLoader_Parse_EnableGlobDotYes_ReportsError`.

**SettingsLoader_Parse_VersionNotText_ReportsError**: A `version` given as a list is an error naming the key.
This scenario is tested by `SettingsLoader_Parse_VersionNotText_ReportsError`.

**SettingsLoader_Parse_RootNotMapping_ReportsError**: A document holding a list at the top level is an error at
line 1 column 1. This scenario is tested by `SettingsLoader_Parse_RootNotMapping_ReportsError`.

**SettingsLoader_Parse_NonScalarKey_ReportsError**: A mapping whose key is itself a list is an error at the
key. This scenario is tested by `SettingsLoader_Parse_NonScalarKey_ReportsError`.

**SettingsLoader_Parse_MultipleDocuments_ReportsError**: Two documents separated by a document marker are an
error at the start of the second, saying that multiple documents are not supported. This scenario is tested by
`SettingsLoader_Parse_MultipleDocuments_ReportsError`.

**SettingsLoader_Parse_MultipleTypeErrors_ReportsEveryError**: Three wrongly typed values produce three errors,
in source order. This scenario is tested by `SettingsLoader_Parse_MultipleTypeErrors_ReportsEveryError`.

**SettingsLoader_Parse_AnyError_ReturnsNoSettings**: Valid values alongside one error yield no settings. This
scenario is tested by `SettingsLoader_Parse_AnyError_ReturnsNoSettings`.

**SettingsLoader_Parse_MalformedYaml_ReportsLineAndColumn**: A flow list that is never closed is one error with
a line and column inside the text. This scenario is tested by
`SettingsLoader_Parse_MalformedYaml_ReportsLineAndColumn`.

**SettingsLoader_Parse_DuplicateKey_ReportsErrorAtDuplicate**: A key given twice is an error at the second
occurrence. This scenario is tested by `SettingsLoader_Parse_DuplicateKey_ReportsErrorAtDuplicate`.

**SettingsLoader_Parse_TabIndentation_ReportsError**: A list indented with a tab is an error with a position.
This scenario is tested by `SettingsLoader_Parse_TabIndentation_ReportsError`.

**SettingsLoader_Parse_UnknownAlias_ReportsError**: An alias with no matching anchor is an error with a
position. This scenario is tested by `SettingsLoader_Parse_UnknownAlias_ReportsError`.

**SettingsLoader_Parse_UnsupportedKey_ReportsErrorNamingKey**: A `words` key on line 2 is the single error
`'words' is not a supported setting.` at line 2 column 1. This scenario is tested by
`SettingsLoader_Parse_UnsupportedKey_ReportsErrorNamingKey`.

**SettingsLoader_Parse_UnsupportedKey_ReturnsNoSettings**: An otherwise valid file with one unsupported key
yields no settings. This scenario is tested by `SettingsLoader_Parse_UnsupportedKey_ReturnsNoSettings`.

**SettingsLoader_Parse_SeveralUnsupportedKeys_ReportsEach**: Three unsupported keys produce three errors that
name them, in source order. This scenario is tested by
`SettingsLoader_Parse_SeveralUnsupportedKeys_ReportsEach`.

**SettingsLoader_Parse_UnsupportedKeyWithNestedValue_ReportsSingleError**: An unsupported key whose value is a
nested mapping produces one error for the key and none for its content. This scenario is tested by
`SettingsLoader_Parse_UnsupportedKeyWithNestedValue_ReportsSingleError`.

**SettingsLoader_Parse_KeyDifferentCase_TreatedAsUnsupported**: `Files` is not `files` and is reported as
unsupported. This scenario is tested by `SettingsLoader_Parse_KeyDifferentCase_TreatedAsUnsupported`.

**SettingsLoader_Parse_InvalidFilesPattern_ReportsErrorAtItem**: A `files` item that uses a backslash is an
error at that item, line 3 column 5. This scenario is tested by
`SettingsLoader_Parse_InvalidFilesPattern_ReportsErrorAtItem`.

**SettingsLoader_Parse_InvalidIgnorePathsPattern_ReportsErrorAtItem**: An `ignorePaths` item with a parent
segment is an error at that item. This scenario is tested by
`SettingsLoader_Parse_InvalidIgnorePathsPattern_ReportsErrorAtItem`.

**SettingsLoader_Parse_InvalidPattern_NamesPatternAndReason**: A negated pattern's error names the pattern and
says it is negated. This scenario is tested by `SettingsLoader_Parse_InvalidPattern_NamesPatternAndReason`.

**SettingsLoader_Parse_AbsolutePathPattern_ReportsErrorAtItem**: A drive path in `files` (line 3) and a network
path in `ignorePaths` (line 5) are each reported at their own item as absolute paths, and no settings are
returned. This scenario is tested by `SettingsLoader_Parse_AbsolutePathPattern_ReportsErrorAtItem`.

**SettingsLoader_Parse_QuestionMarkPattern_ReportsErrorSuggestingStar**: `docs/file?.md` is an error at its item
that names the pattern and suggests using `*`. This scenario is tested by
`SettingsLoader_Parse_QuestionMarkPattern_ReportsErrorSuggestingStar`.

**SettingsLoader_Parse_LeadingSlashPattern_IsAccepted**: `/README.md` and `/build/` load successfully and are
kept as written, because a leading slash means the root directory. This scenario is tested by
`SettingsLoader_Parse_LeadingSlashPattern_IsAccepted`.

**SettingsLoader_Parse_ValidBraceAndBareNamePatterns_AreAccepted**: Brace and bare-name patterns load and are
kept as written. This scenario is tested by `SettingsLoader_Parse_ValidBraceAndBareNamePatterns_AreAccepted`.

**SettingsLoader_Parse_ErrorDiagnostic_CarriesSourceNameLineAndColumn**: An error on line 2 carries the source
name, line 2, and column 16. This scenario is tested by
`SettingsLoader_Parse_ErrorDiagnostic_CarriesSourceNameLineAndColumn`.

**SettingsLoader_Parse_Diagnostic_ToString_FormatsFileLineColumn**: The diagnostic formats as
`cfg.yaml(2,16): error: Expected true or false for 'enableGlobDot'.`. This scenario is tested by
`SettingsLoader_Parse_Diagnostic_ToString_FormatsFileLineColumn`.

**SettingsLoader_Parse_Diagnostic_ToStringWithoutSource_OmitsFileName**: With no source name, only the
position, the severity word, and the message appear. This scenario is tested by
`SettingsLoader_Parse_Diagnostic_ToStringWithoutSource_OmitsFileName`.

**SettingsLoader_Parse_Result_DiagnosticsCannotBeMutatedThroughInterface**: The diagnostics of a failed result
are cast to the mutable collection interface; clearing and adding are refused and the error and the absence of
settings remain. This scenario is tested by `SettingsLoader_Parse_Result_DiagnosticsCannotBeMutatedThroughInterface`.

**SettingsLoader_Parse_Result_CallerListMutatedAfterCreation_DoesNotAffectResult**: A result is created from an
empty list that the caller then adds to; the result still has settings and no diagnostics. This scenario is
tested by `SettingsLoader_Parse_Result_CallerListMutatedAfterCreation_DoesNotAffectResult`.

**SettingsLoader_Parse_Diagnostic_ToStringWithNothingKnown_ShowsOnlyMessage**: With neither source nor
position, only the severity word and the message appear. This scenario is tested by
`SettingsLoader_Parse_Diagnostic_ToStringWithNothingKnown_ShowsOnlyMessage`.
