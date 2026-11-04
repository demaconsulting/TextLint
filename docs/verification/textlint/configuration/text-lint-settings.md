### TextLintSettings

#### Verification Approach

`TextLintSettings` is verified with unit tests in `TextLintSettingsTests.cs`. The type uses only .NET
base class library types, so no mocking or test doubles are needed; tests construct settings, derive copies, and
assert on the exposed values and thrown exceptions.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- New settings have no version, empty lists, and hidden-file matching disabled.
- A copy made with `with` does not change the original.
- Changing a supplied list after assignment does not change the settings.
- A null list is rejected, and the exposed list cannot be mutated.

#### Test Scenarios

**TextLintSettings_New_NoValues_HasDocumentedDefaults**: Settings are created without assigning anything; the
version is null, both lists are empty (never null), and `EnableGlobDot` is false. This scenario is tested by
`TextLintSettings_New_NoValues_HasDocumentedDefaults`.

**TextLintSettings_With_ChangedFiles_LeavesOriginalUnchanged**: A copy is derived with different include
patterns; the original keeps its patterns. This scenario is tested by
`TextLintSettings_With_ChangedFiles_LeavesOriginalUnchanged`.

**TextLintSettings_Init_ListMutatedAfterAssignment_DoesNotAffectSettings**: A mutable list is assigned and
then changed; the settings keep the original contents. This scenario is tested by
`TextLintSettings_Init_ListMutatedAfterAssignment_DoesNotAffectSettings`.

**TextLintSettings_Init_NullFiles_ThrowsArgumentNullException**: Assigning null to `Files` throws. This
scenario is tested by `TextLintSettings_Init_NullFiles_ThrowsArgumentNullException`.

**TextLintSettings_Init_NullIgnorePaths_ThrowsArgumentNullException**: Assigning null to `IgnorePaths` throws.
This scenario is tested by `TextLintSettings_Init_NullIgnorePaths_ThrowsArgumentNullException`.

**TextLintSettings_Files_ExposedList_CannotBeMutatedThroughInterface**: The exposed list is cast to the mutable
collection interface and an add is attempted; it throws and the settings are unchanged. This scenario is tested
by `TextLintSettings_Files_ExposedList_CannotBeMutatedThroughInterface`.
