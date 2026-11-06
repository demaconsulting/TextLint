### SettingsLoader

#### Verification Approach

`SettingsLoader` is verified with unit tests in `SettingsLoaderTests.cs`. Tests of file loading write real
files into a per-test temporary directory; tests of mapping and errors call the internal `Parse` method with
YAML text so no file is needed. YamlDotNet is a real dependency (an OTS item with its own requirements, not a
test double), and the unit's documented dependencies are real: `TextLintSettings`, `GlobPatternExpander`,
`SpellingDictionary`, `WordTokenizer`, and `PatternCatalog` (with `PatternParser`), which judges every pattern entry
and definition by the rules of the spell check so that a pattern accepted at load is never rejected later.
Nothing is mocked: the loader's behavior is defined by how it maps real YAML, so a double would prove nothing.
Positions asserted in the tests are 1-based lines and columns. Messages of the spelling errors are asserted
exactly, because they are the user's only explanation of what to fix.

#### Test Environment

File-loading tests need permission to create and delete files under the operating system's temporary
directory; each test uses its own uniquely named directory. All other tests need nothing beyond the standard
test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A file loads asynchronously, honors cancellation and a byte-order mark, and a missing file is an error that
  names it, with no position.
- Every supported key maps to its setting; empty and comment-only documents and null values are absent.
- Absent or null spelling keys leave language `en`, minimum word length 4, empty lists, and no size limit.
- Word lists map in flow, block, and quoted forms, entries are kept as written, and suggestion forms are
  accepted for flagged words only.
- A size maps in bytes for every unit and letter case, with fractions, rounded down, with 0 meaning no limit,
  up to the largest representable size.
- Wrong types and shapes, a non-mapping root, more than one document, and every bad item are reported at their
  positions, all of them, and no settings are returned.
- Syntax errors, duplicate keys, tab indentation, and unknown aliases are reported with a position.
- Every unsupported key is reported by name at its position, case-sensitively, and no settings are returned.
- Invalid patterns in `files` and `ignorePaths` are reported at the item, naming the pattern and the reason;
  valid brace and bare-name patterns are accepted.
- An unsupported language, every kind of unusable word-list entry, every invalid form of `minWordLength` and
  `maxFileSize` are reported at their value with the exact message, and several of them are all reported.
- Diagnostics carry the source, line, and column, format as `file(line,column): error: message`, and cannot be
  changed by the caller once returned.
- The ignoreRegExpList entries map in order and a name may come before its definition; the patterns key maps names
  and expressions in order, whatever the order of the keys of a definition and with a description allowed; the
  random-string keys map; null values leave the defaults.
- Every ignoreRegExpList entry or definition that cannot be used (unsupported flag or construct, invalid expression,
  unknown, repeated, predefined, or malformed name, wrong shape or type, blank) is reported at the offending node with
  the entry and the reason, all of them in source order among the other errors, and a definition with a problem still
  defines its name.
- An ignoreRandomStrings that is not a boolean and a minRandomLength that is not a whole number of at least 1 are
  reported at the value with the exact message, and `includeRegExpList` remains an unsupported key.

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

**SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue**: Text that sets all ten supported keys is parsed; every
value is mapped, including the language `en-GB`, a word in each list, a minimum word length of 3, and a size of
`2K` as 2048 bytes. This scenario is tested by `SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue`.

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

**SettingsLoader_Parse_UnsupportedKey_ReportsErrorNamingKey**: A `caseSensitive` key on line 2 is the single
error `'caseSensitive' is not a supported setting.` at line 2 column 1. This scenario is tested by
`SettingsLoader_Parse_UnsupportedKey_ReportsErrorNamingKey`.

**SettingsLoader_Parse_UnsupportedKey_ReturnsNoSettings**: An otherwise valid file with one unsupported key
(`allowCompoundWords`) yields no settings. This scenario is tested by
`SettingsLoader_Parse_UnsupportedKey_ReturnsNoSettings`.

**SettingsLoader_Parse_SeveralUnsupportedKeys_ReportsEach**: Three unsupported keys (`caseSensitive`, `import`,
and `allowCompoundWords`) produce three errors that name them, in source order. This scenario is tested by
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

**SettingsLoader_Parse_SpellingKeys_MapsEveryValue**: Text that sets the six spelling keys, with two entries in
each list, a minimum word length of 7, and a size of `1MB`, is parsed; every value is mapped in order. This
scenario is tested by `SettingsLoader_Parse_SpellingKeys_MapsEveryValue`.

**SettingsLoader_Parse_SpellingKeysAbsent_UsesCspellDefaults**: Text that sets no spelling key yields language
`en`, minimum word length 4, empty lists, and size 0. This scenario is tested by
`SettingsLoader_Parse_SpellingKeysAbsent_UsesCspellDefaults`.

**SettingsLoader_Parse_NullSpellingValues_TreatedAsAbsent**: Every spelling key given an empty, `~`, or `null`
value loads successfully with the defaults. This scenario is tested by
`SettingsLoader_Parse_NullSpellingValues_TreatedAsAbsent`.

**SettingsLoader_Parse_WordListFlowBlockAndQuotedForms_AllMap**: A flow list with plain, double-quoted, and
single-quoted entries and a block list map in order without their quotes. This scenario is tested by
`SettingsLoader_Parse_WordListFlowBlockAndQuotedForms_AllMap`.

**SettingsLoader_Parse_WordEntriesWithDigitsAndPunctuation_AreKeptAsWritten**: The entries `k8s`, `Foo-Bar`, and
`don't` are stored exactly as written. This scenario is tested by
`SettingsLoader_Parse_WordEntriesWithDigitsAndPunctuation_AreKeptAsWritten`.

**SettingsLoader_Parse_FlagWordsSuggestionForms_AreAccepted**: The arrow form `utilize->use, employ` and the
colon form `leverage: use` are accepted in `flagWords` and kept as written. This scenario is tested by
`SettingsLoader_Parse_FlagWordsSuggestionForms_AreAccepted`.

**SettingsLoader_Parse_LanguageCaseAndListForms_AreAccepted**: The language `EN-gb , en-US` is accepted and
stored as written. This scenario is tested by `SettingsLoader_Parse_LanguageCaseAndListForms_AreAccepted`.

**SettingsLoader_Parse_MaxFileSizeWholeNumber_MapsToBytes**: A size of `100000` without a unit is 100000 bytes.
This scenario is tested by `SettingsLoader_Parse_MaxFileSizeWholeNumber_MapsToBytes`.

**SettingsLoader_Parse_MaxFileSizeUnits_MapToBinaryMultiples**: `B`, `K`, `KB`, `M`, `MB`, `G`, and `GB`, in
upper and lower case, map to 1, 1024, 1024 squared, and 1024 cubed bytes times the number. This scenario is
tested by `SettingsLoader_Parse_MaxFileSizeUnits_MapToBinaryMultiples`.

**SettingsLoader_Parse_MaxFileSizeFractions_MapToBytes**: `1.5MB`, `.5K`, `0.5M`, and `2.50K` map to the exact
numbers of bytes. This scenario is tested by `SettingsLoader_Parse_MaxFileSizeFractions_MapToBytes`.

**SettingsLoader_Parse_MaxFileSizeFractionalBytes_RoundsDown**: `1.9`, `2.7B`, and `1.9999K` map to 1, 2, and
2047 bytes. This scenario is tested by `SettingsLoader_Parse_MaxFileSizeFractionalBytes_RoundsDown`.

**SettingsLoader_Parse_MaxFileSizeZero_MeansNoLimit**: `0`, `0K`, and `0.0MB` all map to 0, which means no
limit. This scenario is tested by `SettingsLoader_Parse_MaxFileSizeZero_MeansNoLimit`.

**SettingsLoader_Parse_MaxFileSizeQuotedText_IsAccepted**: Sizes written in double and single quotes are
accepted. This scenario is tested by `SettingsLoader_Parse_MaxFileSizeQuotedText_IsAccepted`.

**SettingsLoader_Parse_MaxFileSizeLargestRepresentable_IsAccepted**: `8589934591GB`, the largest whole number of
gigabytes that fits a 64-bit integer, maps exactly. This scenario is tested by
`SettingsLoader_Parse_MaxFileSizeLargestRepresentable_IsAccepted`.

**SettingsLoader_Parse_MinWordLengthOne_IsAccepted**: The boundary length 1 is accepted. This scenario is tested
by `SettingsLoader_Parse_MinWordLengthOne_IsAccepted`.

**SettingsLoader_Parse_UnsupportedLanguage_ReportsErrorAtValue**: `language: fr` is one error at line 1 column 11
with the exact message `The language 'fr' is not supported. Supported languages: en, en-GB, en-US.`, naming
the source and without a parameter name. This scenario is tested by
`SettingsLoader_Parse_UnsupportedLanguage_ReportsErrorAtValue`.

**SettingsLoader_Parse_LanguageListWithUnsupportedName_NamesTheUnsupportedName**: In `en-US, en-AU` only `en-AU`
is named as unsupported. This scenario is tested by
`SettingsLoader_Parse_LanguageListWithUnsupportedName_NamesTheUnsupportedName`.

**SettingsLoader_Parse_LanguageListWithEmptyName_ReportsError**: `en-US,` is an error saying the list contains
an empty language name. This scenario is tested by `SettingsLoader_Parse_LanguageListWithEmptyName_ReportsError`.

**SettingsLoader_Parse_EmptyQuotedLanguage_ReportsError**: A quoted empty language is text, not null, and is
reported as an empty language name. This scenario is tested by
`SettingsLoader_Parse_EmptyQuotedLanguage_ReportsError`.

**SettingsLoader_Parse_LanguageNotText_ReportsErrorAtValue**: A language given as a list is the error
`Expected a text value for 'language'.` at line 2 column 3. This scenario is tested by
`SettingsLoader_Parse_LanguageNotText_ReportsErrorAtValue`.

**SettingsLoader_Parse_WordsBlankEntry_ReportsErrorAtItem**: A `words` entry of white space is the error
`Cannot use the 'words' entry '  ': it is blank.` at line 2 column 5, naming the source. This scenario is
tested by `SettingsLoader_Parse_WordsBlankEntry_ReportsErrorAtItem`.

**SettingsLoader_Parse_WordsEntryWithWhiteSpace_ReportsError**: The entry `foo bar` is rejected with the white
space reason and the advice to list each word separately. This scenario is tested by
`SettingsLoader_Parse_WordsEntryWithWhiteSpace_ReportsError`.

**SettingsLoader_Parse_WordsEntryWithDisallowedCharacter_ReportsError**: The entry `foo@bar` is rejected, naming
the character `@`. This scenario is tested by
`SettingsLoader_Parse_WordsEntryWithDisallowedCharacter_ReportsError`.

**SettingsLoader_Parse_WordsForbiddenMarkerEntry_ReportsError**: The entry `!word` is rejected with the
forbidden-word marker reason that points to `flagWords`. This scenario is tested by
`SettingsLoader_Parse_WordsForbiddenMarkerEntry_ReportsError`.

**SettingsLoader_Parse_WordsEntryWithoutLetterOrDigit_ReportsError**: The entry `...` is rejected because it has
no letter or digit. This scenario is tested by `SettingsLoader_Parse_WordsEntryWithoutLetterOrDigit_ReportsError`.

**SettingsLoader_Parse_WordsSuggestionForm_ReportsError**: The colon suggestion form in `words` is rejected,
naming the colon, because only flagged words take suggestions. This scenario is tested by
`SettingsLoader_Parse_WordsSuggestionForm_ReportsError`.

**SettingsLoader_Parse_IgnoreWordsBadEntry_NamesIgnoreWordsKey**: A bad `ignoreWords` entry is reported under the
key name `ignoreWords`. This scenario is tested by `SettingsLoader_Parse_IgnoreWordsBadEntry_NamesIgnoreWordsKey`.

**SettingsLoader_Parse_FlagWordsBadEntry_NamesFlagWordsKey**: A bad `flagWords` entry (two words before its
suggestion) is reported under the key name `flagWords`. This scenario is tested by
`SettingsLoader_Parse_FlagWordsBadEntry_NamesFlagWordsKey`.

**SettingsLoader_Parse_FlagWordsEntryWithoutWord_ReportsError**: The entry `->color` is rejected because there is
no word before its suggestions. This scenario is tested by
`SettingsLoader_Parse_FlagWordsEntryWithoutWord_ReportsError`.

**SettingsLoader_Parse_WordListWithGoodAndBadEntries_ReportsOnlyTheBadEntry**: In a list of three entries only
the middle, unusable one is reported, at line 3 column 5. This scenario is tested by
`SettingsLoader_Parse_WordListWithGoodAndBadEntries_ReportsOnlyTheBadEntry`.

**SettingsLoader_Parse_WordsNotAList_ReportsErrorAtValue**: `words: apple` is the error
`Expected a list of text values.` at line 1 column 8. This scenario is tested by
`SettingsLoader_Parse_WordsNotAList_ReportsErrorAtValue`.

**SettingsLoader_Parse_WordsItemNotText_ReportsErrorAtItem**: A `flagWords` item that is a nested mapping is the
error `Expected a text value in the list.` at line 2 column 5. This scenario is tested by
`SettingsLoader_Parse_WordsItemNotText_ReportsErrorAtItem`.

**SettingsLoader_Parse_WordsNullItem_ReportsErrorAtItem**: A null item in a word list is the same error at its
line. This scenario is tested by `SettingsLoader_Parse_WordsNullItem_ReportsErrorAtItem`.

**SettingsLoader_Parse_MinWordLengthZero_ReportsErrorAtValue**: `minWordLength: 0` is one error with the exact
message, the source, line 1, column 16, and the compiler-style text. This scenario is tested by
`SettingsLoader_Parse_MinWordLengthZero_ReportsErrorAtValue`.

**SettingsLoader_Parse_MinWordLengthNegative_ReportsError**: `-1` is rejected. This scenario is tested by
`SettingsLoader_Parse_MinWordLengthNegative_ReportsError`.

**SettingsLoader_Parse_MinWordLengthFraction_ReportsError**: `2.5` is rejected. This scenario is tested by
`SettingsLoader_Parse_MinWordLengthFraction_ReportsError`.

**SettingsLoader_Parse_MinWordLengthQuoted_ReportsError**: The quoted `"4"` is rejected rather than guessed at.
This scenario is tested by `SettingsLoader_Parse_MinWordLengthQuoted_ReportsError`.

**SettingsLoader_Parse_MinWordLengthOverflow_ReportsError**: `99999999999`, which does not fit a 32-bit integer,
is rejected. This scenario is tested by `SettingsLoader_Parse_MinWordLengthOverflow_ReportsError`.

**SettingsLoader_Parse_MinWordLengthNotANumber_ReportsError**: Ordinary text is rejected. This scenario is
tested by `SettingsLoader_Parse_MinWordLengthNotANumber_ReportsError`.

**SettingsLoader_Parse_MaxFileSizeInvalidForms_ReportEachAtValue**: A negative size, an unknown unit, a unit
without a number, text, an exponent, two dots, a space before the unit, and an empty quoted value are each one
error with the exact size message at line 1 column 14, and no settings. This scenario is tested by
`SettingsLoader_Parse_MaxFileSizeInvalidForms_ReportEachAtValue`.

**SettingsLoader_Parse_MaxFileSizeBelowOneByte_ReportsError**: `0.5` and `0.0004K`, which would round down to
zero and so silently mean no limit, are rejected. This scenario is tested by
`SettingsLoader_Parse_MaxFileSizeBelowOneByte_ReportsError`.

**SettingsLoader_Parse_MaxFileSizeOverflow_ReportsError**: A huge number, the largest 64-bit integer, and
`8589934592GB` (the first gigabyte count above the largest representable) are rejected. This scenario is tested
by `SettingsLoader_Parse_MaxFileSizeOverflow_ReportsError`.

**SettingsLoader_Parse_MaxFileSizeNotText_ReportsErrorAtValue**: A size given as a list is the size message at
line 2 column 3. This scenario is tested by `SettingsLoader_Parse_MaxFileSizeNotText_ReportsErrorAtValue`.

**SettingsLoader_Parse_MultipleSpellingErrors_ReportsEveryError**: An unsupported language, an unusable word, a
zero length, and a negative size give four errors on lines 1, 3, 4, and 5, and no settings. This scenario is
tested by `SettingsLoader_Parse_MultipleSpellingErrors_ReportsEveryError`.

**SettingsLoader_Parse_IgnoreRegExpList_MapsEntriesInOrder**: A slash expression with a flag, a bare expression, a
predefined name, and a name defined below the list are mapped as written, in order, and the load succeeds. This
scenario is tested by `SettingsLoader_Parse_IgnoreRegExpList_MapsEntriesInOrder`.

**SettingsLoader_Parse_Patterns_MapsNamesAndExpressions**: Two definitions, one with its keys in the other order and a
description, are mapped in order with their names and expressions. This scenario is tested by
`SettingsLoader_Parse_Patterns_MapsNamesAndExpressions`.

**SettingsLoader_Parse_IgnoreRegExpListUnsupportedFlag_ReportsErrorAtItem**: An expression with the `x` flag is the
single error, which names the setting, the entry, and the flag, at line 2 and the column of the list item. This
scenario is tested by `SettingsLoader_Parse_IgnoreRegExpListUnsupportedFlag_ReportsErrorAtItem`.

**SettingsLoader_Parse_IgnoreRegExpListInvalidExpression_ReportsEveryBadItem**: A good entry between an unclosed group
and an unsupported escape gives two errors, on lines 2 and 4, with the reasons, and no settings. This scenario is
tested by `SettingsLoader_Parse_IgnoreRegExpListInvalidExpression_ReportsEveryBadItem`.

**SettingsLoader_Parse_IgnoreRegExpListUnknownName_ReportsErrorAtItem**: A misspelled predefined name is the single
error, which says the name is not defined and how to write literal text, at the list item. This scenario is tested by
`SettingsLoader_Parse_IgnoreRegExpListUnknownName_ReportsErrorAtItem`.

**SettingsLoader_Parse_IgnoreRegExpListBlankEntry_ReportsError**: An entry of two spaces is reported as a blank
pattern. This scenario is tested by `SettingsLoader_Parse_IgnoreRegExpListBlankEntry_ReportsError`.

**SettingsLoader_Parse_IgnoreRegExpListNotAListOfText_ReportsErrors**: A text instead of a list, and a list that holds
a mapping, are reported with the messages of the shared list rules. This scenario is tested by
`SettingsLoader_Parse_IgnoreRegExpListNotAListOfText_ReportsErrors`.

**SettingsLoader_Parse_InvalidPatternDefinition_ReportsErrorAtTheProblem**: Each row, a settings text with one invalid
definition, gives the line, the column, and the wording of the one error. The rows cover a name that is predefined,
starts with a digit, holds a space, or repeats another in other letter case; a missing name or pattern; an invalid
expression and an unsupported flag, naming the definition; a list of patterns, written in block or flow form; a
mapping or list where text is expected; an unsupported key of a definition; a definition that is not a mapping; a
`patterns` value that is not a list; and a name with no value. This scenario is tested by
`SettingsLoader_Parse_InvalidPatternDefinition_ReportsErrorAtTheProblem`.

**SettingsLoader_Parse_DefinitionWithBadPattern_StillDefinesItsName**: A list that refers to a name whose definition has
an invalid expression gives the one error for the expression and none for the reference. This scenario is tested by
`SettingsLoader_Parse_DefinitionWithBadPattern_StillDefinesItsName`.

**SettingsLoader_Parse_PatternErrorsAmongOtherKeys_AreReportedInSourceOrder**: A bad list entry, a bad definition, and
a bad minimum random length give three errors on lines 2, 5, and 6. This scenario is tested by
`SettingsLoader_Parse_PatternErrorsAmongOtherKeys_AreReportedInSourceOrder`.

**SettingsLoader_Parse_NullPatternValues_LeaveDefaults**: The four new keys, written with no value, with `~`, and with
`null`, leave no patterns, random strings ignored, and a minimum random length of 40. This scenario is tested by
`SettingsLoader_Parse_NullPatternValues_LeaveDefaults`.

**SettingsLoader_Parse_RandomStringKeys_AreMapped**: `ignoreRandomStrings: false` and `minRandomLength: 25` are mapped.
This scenario is tested by `SettingsLoader_Parse_RandomStringKeys_AreMapped`.

**SettingsLoader_Parse_InvalidRandomStringValue_ReportsErrorAtValue**: A switch written as `yes`, as quoted text, or as
a number, and a length that is zero, negative, fractional, quoted, or too large, are each the single error with the
exact message, on line 1. This scenario is tested by `SettingsLoader_Parse_InvalidRandomStringValue_ReportsErrorAtValue`.

**SettingsLoader_Parse_IncludeRegExpList_RemainsUnsupported**: The key `includeRegExpList`, which cspell supports and
the loader does not, is reported as an unsupported setting. This scenario is tested by
`SettingsLoader_Parse_IncludeRegExpList_RemainsUnsupported`.
