### TextLintSettings

#### Verification Approach

`TextLintSettings` is verified with unit tests in `TextLintSettingsTests.cs`. The type uses only .NET
base class library types, so no mocking or test doubles are needed; tests construct settings, derive copies, and
assert on the exposed values and thrown exceptions. Boundary values (a length of 0 and 1, a size of -1, 0, and
the largest 64-bit integer) are tested on both sides.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- New settings have no version, empty lists, hidden-file matching disabled, language `en`, minimum word length 4,
  and no size limit.
- A copy made with `with` does not change the original, for the file-selection and the spelling settings.
- Changing a supplied list after assignment does not change the settings.
- A null list or language is rejected, the exposed lists cannot be mutated, and changes to a supplied list or
  array after assignment do not change the settings.
- A minimum word length below 1 and a negative maximum file size are rejected; 1 and 0 (and the largest size)
  are accepted; an unsupported language is accepted, because it is validated where it is consumed.

#### Test Scenarios

**TextLintSettings_New_NoValues_HasDocumentedDefaults**: Settings are created without assigning anything; the
version is null, both lists are empty (never null), and `EnableGlobDot` is false. This scenario is tested by
`TextLintSettings_New_NoValues_HasDocumentedDefaults`.

**TextLintSettings_New_NoValues_HasSpellingDefaults**: Settings are created without assigning anything; the
language is `en`, the minimum word length is 4, the maximum file size is 0, and the three word lists are
empty (never null). This scenario is tested by `TextLintSettings_New_NoValues_HasSpellingDefaults`.

**TextLintSettings_With_ChangedFiles_LeavesOriginalUnchanged**: A copy is derived with different include
patterns; the original keeps its patterns. This scenario is tested by
`TextLintSettings_With_ChangedFiles_LeavesOriginalUnchanged`.

**TextLintSettings_With_ChangedSpellingValues_LeavesOriginalUnchanged**: A copy is derived that changes the
language, the three word lists, the minimum word length, and the maximum file size; the original keeps every
value and the copy has the new ones. This scenario is tested by
`TextLintSettings_With_ChangedSpellingValues_LeavesOriginalUnchanged`.

**TextLintSettings_Init_ListMutatedAfterAssignment_DoesNotAffectSettings**: A mutable list is assigned and
then changed; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_ListMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_IgnorePathsMutatedAfterAssignment_DoesNotAffectSettings**: A mutable list assigned to
`IgnorePaths` is changed afterwards; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_IgnorePathsMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_ArrayMutatedAfterAssignment_DoesNotAffectSettings**: An array assigned to `Files` has an
element overwritten afterwards; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_ArrayMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_NullFiles_ThrowsArgumentNullException**: Assigning null to `Files` throws. This
scenario is tested by `TextLintSettings_Init_NullFiles_ThrowsArgumentNullException`.

**TextLintSettings_Init_NullIgnorePaths_ThrowsArgumentNullException**: Assigning null to `IgnorePaths` throws.
This scenario is tested by `TextLintSettings_Init_NullIgnorePaths_ThrowsArgumentNullException`.

**TextLintSettings_Files_ExposedList_CannotBeMutatedThroughInterface**: The exposed list is cast to the mutable
collection interface and an add is attempted; it throws and the settings are unchanged. This scenario is tested
by `TextLintSettings_Files_ExposedList_CannotBeMutatedThroughInterface`.

**TextLintSettings_Init_UnsupportedLanguage_IsAcceptedWithoutValidation**: The language `fr` is assigned; it is
stored as given, because validation belongs to the consumers. This scenario is tested by
`TextLintSettings_Init_UnsupportedLanguage_IsAcceptedWithoutValidation`.

**TextLintSettings_Init_WordsMutatedAfterAssignment_DoesNotAffectSettings**: A mutable list assigned to `Words`
is added to afterwards; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_WordsMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_IgnoreWordsMutatedAfterAssignment_DoesNotAffectSettings**: A mutable list assigned to
`IgnoreWords` is cleared afterwards; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_IgnoreWordsMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_FlagWordsArrayMutatedAfterAssignment_DoesNotAffectSettings**: An array assigned to
`FlagWords` has an element overwritten afterwards; the settings keep the original contents. This scenario is
tested by `TextLintSettings_Init_FlagWordsArrayMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_NullWords_ThrowsArgumentNullException**: Assigning null to `Words` throws. This scenario
is tested by `TextLintSettings_Init_NullWords_ThrowsArgumentNullException`.

**TextLintSettings_Init_NullIgnoreWords_ThrowsArgumentNullException**: Assigning null to `IgnoreWords` throws.
This scenario is tested by `TextLintSettings_Init_NullIgnoreWords_ThrowsArgumentNullException`.

**TextLintSettings_Init_NullFlagWords_ThrowsArgumentNullException**: Assigning null to `FlagWords` throws. This
scenario is tested by `TextLintSettings_Init_NullFlagWords_ThrowsArgumentNullException`.

**TextLintSettings_Init_NullLanguage_ThrowsArgumentNullException**: Assigning null to `Language` throws. This
scenario is tested by `TextLintSettings_Init_NullLanguage_ThrowsArgumentNullException`.

**TextLintSettings_WordLists_ExposedLists_CannotBeMutatedThroughInterface**: The three exposed word lists are
cast to the mutable collection interface and an add is attempted on each; every add throws and the lists are
unchanged. This scenario is tested by
`TextLintSettings_WordLists_ExposedLists_CannotBeMutatedThroughInterface`.

**TextLintSettings_Init_MinWordLengthZero_ThrowsArgumentOutOfRangeException**: Assigning 0 to `MinWordLength`
throws. This scenario is tested by `TextLintSettings_Init_MinWordLengthZero_ThrowsArgumentOutOfRangeException`.

**TextLintSettings_Init_MinWordLengthNegative_ThrowsArgumentOutOfRangeException**: Assigning -1 to
`MinWordLength` throws. This scenario is tested by
`TextLintSettings_Init_MinWordLengthNegative_ThrowsArgumentOutOfRangeException`.

**TextLintSettings_Init_MinWordLengthOne_IsAccepted**: The boundary length 1 is stored. This scenario is tested
by `TextLintSettings_Init_MinWordLengthOne_IsAccepted`.

**TextLintSettings_Init_MaxFileSizeNegative_ThrowsArgumentOutOfRangeException**: Assigning -1 to `MaxFileSize`
throws. This scenario is tested by `TextLintSettings_Init_MaxFileSizeNegative_ThrowsArgumentOutOfRangeException`.

**TextLintSettings_Init_MaxFileSizeZeroAndLargest_AreAccepted**: The sizes 0 (no limit) and the largest 64-bit
integer are stored. This scenario is tested by `TextLintSettings_Init_MaxFileSizeZeroAndLargest_AreAccepted`.
