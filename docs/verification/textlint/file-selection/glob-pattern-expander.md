### GlobPatternExpander

#### Verification Approach

`GlobPatternExpander` is verified with unit tests in `GlobPatternExpanderTests.cs`. The unit is pure string
logic with no dependencies, so no mocks, doubles, or file system are used: each test calls `Expand` with a
pattern and asserts on the returned patterns or the thrown exception.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Brace groups expand per alternative, including nested and multiple groups, and groups without alternatives
  stay literal.
- Bare names, trailing slashes, leading slashes, and `./` prefixes anchor as documented.
- Every unsupported form is rejected with an exception that names the pattern and the reason.

#### Test Scenarios

**GlobPatternExpander_Expand_SingleBraceGroup_ReturnsOnePatternPerAlternative**: `src/*.{md,cs}` expands to
`src/*.md` and `src/*.cs`. This scenario is tested by
`GlobPatternExpander_Expand_SingleBraceGroup_ReturnsOnePatternPerAlternative`.

**GlobPatternExpander_Expand_MultipleBraceGroups_ReturnsCartesianProduct**: `{a,b}/{x,y}.md` expands to the four
combinations. This scenario is tested by `GlobPatternExpander_Expand_MultipleBraceGroups_ReturnsCartesianProduct`.

**GlobPatternExpander_Expand_NestedBraceGroups_ExpandsAllCombinations**: `src/*.{a,b{c,d}}` expands to `a`,
`bc`, and `bd` extensions. This scenario is tested by
`GlobPatternExpander_Expand_NestedBraceGroups_ExpandsAllCombinations`.

**GlobPatternExpander_Expand_EmptyAlternative_ReturnsPatternWithoutIt**: `docs/file{,2}.md` expands to
`docs/file.md` and `docs/file2.md`. This scenario is tested by
`GlobPatternExpander_Expand_EmptyAlternative_ReturnsPatternWithoutIt`.

**GlobPatternExpander_Expand_BraceWithoutComma_LeavesGroupLiteral**: `src/{name}.md` is left as written. This
scenario is tested by `GlobPatternExpander_Expand_BraceWithoutComma_LeavesGroupLiteral`.

**GlobPatternExpander_Expand_AlternativesOfDifferentShape_AreAnchoredIndependently**: `{src/*.md,README.md}`
keeps the first alternative anchored and makes the second match at any depth. This scenario is tested by
`GlobPatternExpander_Expand_AlternativesOfDifferentShape_AreAnchoredIndependently`.

**GlobPatternExpander_Expand_BareName_MatchesFileOrDirectoryAtAnyDepth**: `node_modules` expands to the name at
any depth and its subtree at any depth. This scenario is tested by
`GlobPatternExpander_Expand_BareName_MatchesFileOrDirectoryAtAnyDepth`.

**GlobPatternExpander_Expand_PatternWithSeparator_StaysAnchored**: `docs/build` is used as written. This
scenario is tested by `GlobPatternExpander_Expand_PatternWithSeparator_StaysAnchored`.

**GlobPatternExpander_Expand_TrailingSlashOnly_MatchesDirectoryAtAnyDepth**: `build/` expands to the directory
subtree at any depth. This scenario is tested by
`GlobPatternExpander_Expand_TrailingSlashOnly_MatchesDirectoryAtAnyDepth`.

**GlobPatternExpander_Expand_TrailingSlashOnAnchoredPattern_SelectsSubtree**: `docs/build/` expands to
`docs/build/**`. This scenario is tested by
`GlobPatternExpander_Expand_TrailingSlashOnAnchoredPattern_SelectsSubtree`.

**GlobPatternExpander_Expand_LeadingSlash_AnchorsToRoot**: `/README.md` expands to `README.md`. This scenario is
tested by `GlobPatternExpander_Expand_LeadingSlash_AnchorsToRoot`.

**GlobPatternExpander_Expand_LeadingDotSlash_IsStrippedAndAnchors**: `./README.md` expands to `README.md`. This
scenario is tested by `GlobPatternExpander_Expand_LeadingDotSlash_IsStrippedAndAnchors`.

**GlobPatternExpander_Expand_CurrentDirectorySegment_IsRemoved**: `docs/./guide.md` expands to
`docs/guide.md`. This scenario is tested by `GlobPatternExpander_Expand_CurrentDirectorySegment_IsRemoved`.

**GlobPatternExpander_Expand_RecursivePattern_IsUnchanged**: `**/*.md` is used as written. This scenario is
tested by `GlobPatternExpander_Expand_RecursivePattern_IsUnchanged`.

**GlobPatternExpander_Expand_RepeatedRecursiveSegments_AreMerged**: `src/**/**/*.cs` expands to `src/**/*.cs`.
This scenario is tested by `GlobPatternExpander_Expand_RepeatedRecursiveSegments_AreMerged`.

**GlobPatternExpander_Expand_LoneRecursiveSegment_IsUnchanged**: `**` is used as written. This scenario is
tested by `GlobPatternExpander_Expand_LoneRecursiveSegment_IsUnchanged`.

**GlobPatternExpander_Expand_EmptyPattern_ThrowsInvalidGlobPattern**: An empty pattern is rejected. This
scenario is tested by `GlobPatternExpander_Expand_EmptyPattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_WhitespacePattern_ThrowsInvalidGlobPattern**: A pattern of spaces is rejected.
This scenario is tested by `GlobPatternExpander_Expand_WhitespacePattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_NegatedPattern_ThrowsInvalidGlobPattern**: A leading `!` is rejected. This
scenario is tested by `GlobPatternExpander_Expand_NegatedPattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_BackslashPattern_ThrowsInvalidGlobPattern**: A backslash separator is rejected.
This scenario is tested by `GlobPatternExpander_Expand_BackslashPattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_QuestionMarkPattern_ThrowsInvalidGlobPattern**: The unsupported `?` wildcard is
rejected. This scenario is tested by `GlobPatternExpander_Expand_QuestionMarkPattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_DriveRootedPattern_ThrowsInvalidGlobPattern**: A Windows drive prefix is rejected.
This scenario is tested by `GlobPatternExpander_Expand_DriveRootedPattern_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_DoubleSlashRoot_ThrowsInvalidGlobPattern**: A network-style `//` root is rejected
as an empty segment. This scenario is tested by
`GlobPatternExpander_Expand_DoubleSlashRoot_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_ParentSegment_ThrowsInvalidGlobPattern**: A `..` segment is rejected. This
scenario is tested by `GlobPatternExpander_Expand_ParentSegment_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_ParentSegmentFromBraces_ThrowsInvalidGlobPattern**: A `..` that only appears
through a brace alternative is rejected. This scenario is tested by
`GlobPatternExpander_Expand_ParentSegmentFromBraces_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_UnclosedBrace_ThrowsInvalidGlobPattern**: A missing closing brace is rejected. This
scenario is tested by `GlobPatternExpander_Expand_UnclosedBrace_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_UnopenedBrace_ThrowsInvalidGlobPattern**: A missing opening brace is rejected.
This scenario is tested by `GlobPatternExpander_Expand_UnopenedBrace_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_ExcessiveExpansion_ThrowsInvalidGlobPattern**: Nine two-way groups, which expand
to more patterns than allowed, are rejected. This scenario is tested by
`GlobPatternExpander_Expand_ExcessiveExpansion_ThrowsInvalidGlobPattern`.

**GlobPatternExpander_Expand_ExceptionMessage_NamesPattern**: The exception exposes the pattern and its message
names the pattern and the reason. This scenario is tested by
`GlobPatternExpander_Expand_ExceptionMessage_NamesPattern`.

**GlobPatternExpander_Expand_NullPattern_ThrowsArgumentNullException**: A null pattern throws. This scenario is
tested by `GlobPatternExpander_Expand_NullPattern_ThrowsArgumentNullException`.
