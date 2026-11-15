### TextLintSettings

#### Verification Approach

`TextLintSettings` is verified with unit tests in `TextLintSettingsTests.cs`. The type uses only .NET
base class library types, so no mocking or test doubles are needed; tests construct settings, derive copies, and
assert on the exposed values and thrown exceptions. Boundary values (a length of 0 and 1, a size of -1, 0, and
the largest 64-bit integer, and a minimum random length of -1, 0, and 1) are tested on both sides. The `NamedPattern`
type of the `TextScanning` subsystem is used as a plain value in the pattern list; the validity of the patterns is not
judged by the settings, so it is not tested here. The `SettingsOverride` record, which is part of this unit, is
verified in the same file in the same way, with no mocks: values are assigned and read back, and the lists are
mutated afterwards to prove they were copied.

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
- New settings have no ignoreRegExpList entries and no patterns, ignore random strings, and have a minimum random
  length of 40.
- The pattern lists are copied on assignment and cannot be mutated through their interfaces, and a null list or entry
  is rejected.
- A minimum random length below 1 is rejected and 1 is accepted.
- A copy made with `with` keeps the pattern settings, and changing the copy does not change the original.
- New settings are enabled and have no overrides; `Enabled` and `Overrides` are stored, the overrides are copied on
  assignment, a copy keeps them, and a null list or entry is rejected.
- A new `SettingsOverride` has empty lists and every single value unset, stores every value assigned, copies its
  lists on assignment, rejects null lists and entries, and rejects a length below 1 while accepting 1 and unset.

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

**TextLintSettings_New_NoValues_HasDocumentedPatternDefaults**: Settings are created without assigning anything; the
ignore list and the pattern list are empty (never null), random strings are ignored, and the minimum random length is
40. This scenario is tested by `TextLintSettings_New_NoValues_HasDocumentedPatternDefaults`.

**TextLintSettings_Init_PatternLists_AreCopied**: Mutable lists of entries and definitions are assigned and then
changed; the settings keep the original items, and an add through the mutable interface of each exposed list throws.
This scenario is tested by `TextLintSettings_Init_PatternLists_AreCopied`.

**TextLintSettings_Init_NullPatternListOrEntry_ThrowsArgumentNullException**: A null ignore list, a null pattern list,
a null entry in the first, and a null definition in the second each throw. This scenario is tested by
`TextLintSettings_Init_NullPatternListOrEntry_ThrowsArgumentNullException`.

**TextLintSettings_Init_MinRandomLength_IsValidatedAtOne**: The boundary length 1 is stored, and 0 and -1 throw. This
scenario is tested by `TextLintSettings_Init_MinRandomLength_IsValidatedAtOne`.

**TextLintSettings_With_PatternSettings_AreCopiedAndIndependent**: A copy is derived that changes the ignore list and
the minimum random length; the copy has the new values and keeps the definitions and the random-string switch, and the
original keeps every value. This scenario is tested by `TextLintSettings_With_PatternSettings_AreCopiedAndIndependent`.

**TextLintSettings_New_NoValues_IsEnabledWithNoOverrides**: Settings are created without assigning anything; they
are enabled and the list of overrides is empty (never null). This scenario is tested by
`TextLintSettings_New_NoValues_IsEnabledWithNoOverrides`.

**TextLintSettings_Init_EnabledAndOverrides_AreStoredAndCopied**: Disabled settings are created from a mutable list
of one override, which is then added to; the settings keep the one override, and a copy that is enabled keeps the
overrides and changes only the switch. This scenario is tested by
`TextLintSettings_Init_EnabledAndOverrides_AreStoredAndCopied`.

**TextLintSettings_Init_NullOverrides_ThrowsArgumentNullException**: A null overrides list and a list holding a null
override each throw. This scenario is tested by `TextLintSettings_Init_NullOverrides_ThrowsArgumentNullException`.

**SettingsOverride_New_NoValues_HasEmptyListsAndUnsetValues**: An override is created without assigning anything;
every list is empty (never null) and the language, lengths, random-string switch, `Enabled`, source name, line, and
column are unset. This scenario is tested by `SettingsOverride_New_NoValues_HasEmptyListsAndUnsetValues`.

**SettingsOverride_Init_AllValues_AreStored**: An override that sets every value, including an ordered filename list
with a negation and its source position, holds each value as assigned. This scenario is tested by
`SettingsOverride_Init_AllValues_AreStored`.

**SettingsOverride_Init_ListMutatedAfterAssignment_DoesNotAffectOverride**: A mutable list assigned to `Words` is
added to afterwards; the override keeps the original contents. This scenario is tested by
`SettingsOverride_Init_ListMutatedAfterAssignment_DoesNotAffectOverride`.

**SettingsOverride_Init_NullListOrEntry_ThrowsArgumentNullException**: A null `Filename`, `Words`, or
`IgnoreRegExpList` list and a null entry in `IgnoreWords`, `FlagWords`, or `Patterns` each throw. This scenario is
tested by `SettingsOverride_Init_NullListOrEntry_ThrowsArgumentNullException`.

**SettingsOverride_Init_LengthBelowOne_ThrowsArgumentOutOfRangeException**: A minimum word length of 0 and a minimum
random length of 0 each throw, while 1 is stored and an unset length is accepted. This scenario is tested by
`SettingsOverride_Init_LengthBelowOne_ThrowsArgumentOutOfRangeException`.
