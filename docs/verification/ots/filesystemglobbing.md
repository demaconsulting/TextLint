## FileSystemGlobbing Verification

This document provides the verification evidence for the Microsoft.Extensions.FileSystemGlobbing OTS software
item. Requirements for this OTS item are defined in the FileSystemGlobbing OTS Software Requirements document.

### Required Functionality

Microsoft.Extensions.FileSystemGlobbing matches include and exclude glob patterns against a directory tree
with caller-selected case sensitivity, gives exclusion precedence over inclusion, and does not enumerate
directories that an exclude pattern excludes in full. The file selector depends on all of this to choose files
identically on every platform and to avoid scanning large excluded trees.

### Verification Approach

FileSystemGlobbing is a runtime dependency on the critical path of every file selection, so it is verified by
transitive evidence from the library test suite rather than by a dedicated OTS test project: the file selector
tests run the real matcher over real temporary directory trees, and the pruning tests wrap the real directory
in a recording directory that observes which directories the matcher enumerates. Each scenario below names a
specific test whose outcome can only be correct if the matcher behaves as required. A passing run of those tests
on every supported platform is the evidence that both requirements are satisfied. The matcher is not mocked
anywhere in the library tests.

### Test Scenarios

#### FileSelector_Select_IncludePatterns_ReturnsMatchingFiles

**Scenario**: A recursive include pattern is applied to a tree holding markdown and C# files.

**Expected**: Only the matching files are returned.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-MatchPatterns`.

#### FileSelector_Select_IgnorePatterns_ExcludesMatches

**Scenario**: A file matches an include pattern and an exclude pattern.

**Expected**: The excluded file is not returned.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-MatchPatterns`.

#### FileSelector_Select_DifferentCaseExtension_IsNotSelected

**Scenario**: A file whose extension differs from the pattern only in case is matched with ordinal comparison.

**Expected**: The file is not returned, on every platform.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-MatchPatterns`.

#### FileSelector_Select_NestedFiles_AreFound

**Scenario**: A file four directories deep is matched by a recursive pattern.

**Expected**: The nested file is returned.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-MatchPatterns`.

#### FileSelector_Select_IgnoredDirectory_IsNeverEnumerated

**Scenario**: A directory is excluded in full and the walk is observed through a recording directory.

**Expected**: Neither the directory nor anything beneath it is enumerated.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-ExcludePruning`.

#### FileSelector_Select_NestedIgnoredDirectory_IsNeverEnumerated

**Scenario**: A directory nested two levels down is excluded in full and the walk is observed.

**Expected**: The nested directory is not enumerated while its parent is.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-ExcludePruning`.

#### TextLint_Files_IgnoredDirectory_IsNotEnumerated

**Scenario**: The same property is observed through the public API with settings, as a consuming application
would use it.

**Expected**: The ignored directory is never enumerated and the visible file is still found.

**Requirement coverage**: `TextLint-OTS-FileSystemGlobbing-ExcludePruning`.
