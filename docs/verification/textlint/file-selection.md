## FileSelection

### Verification Approach

The `FileSelection` subsystem is verified by integration tests in `FileSelectionSubsystemTests.cs`. They build
real directory trees in a per-test temporary directory and call `FileSelector.Select`, so `FileSelector`,
`GlobPatternExpander`, and `HiddenPathPolicy` are exercised together with the real matcher (an OTS item).
`TextLintSettings`, the subsystem's one documented dependency, is the input type and is used as is. The only
test double is a recording directory that wraps the real directory and records which directories are
enumerated; it proves that excluded directories are never read, deterministically, where a timing-based test
would be flaky.

### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test
uses its own uniquely named directory, so tests share no state and may run in parallel. They run on Windows,
Linux, and macOS, whose file systems differ in case sensitivity; the case-sensitivity scenario is written so
that it holds on all of them.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Include patterns select files and ignore patterns remove them, relative to the root.
- A path that differs only in case is not selected.
- Brace alternatives select every alternative, and a bare name matches at any depth.
- A negated pattern is rejected with an error that names it.
- Hidden entries are skipped by wildcards unless enabled, and hidden names written literally are selected.
- Ignored and hidden directories are not enumerated.
- Results are in ordinal order with forward-slash paths.

### Test Scenarios

**FileSelectionSubsystem_Select_IncludeAndIgnorePatterns_SelectsExpectedFiles**: Markdown files are included
while generated output is ignored; only the non-ignored markdown files remain. This scenario is tested by
`FileSelectionSubsystem_Select_IncludeAndIgnorePatterns_SelectsExpectedFiles`.

**FileSelectionSubsystem_Select_DifferentCase_IsNotSelected**: Files whose names differ from the patterns only
in case are not selected; the exact-case match is. This scenario is tested by
`FileSelectionSubsystem_Select_DifferentCase_IsNotSelected`.

**FileSelectionSubsystem_Select_BraceAlternatives_SelectsAllAlternatives**: A brace group selects both
extensions it names and not a third. This scenario is tested by
`FileSelectionSubsystem_Select_BraceAlternatives_SelectsAllAlternatives`.

**FileSelectionSubsystem_Select_BareName_MatchesAtAnyDepth**: A bare file name selects the copy at the root and
the nested copy. This scenario is tested by `FileSelectionSubsystem_Select_BareName_MatchesAtAnyDepth`.

**FileSelectionSubsystem_Select_NegatedPattern_ThrowsNamingPattern**: A negated pattern throws an exception whose
message names the pattern. This scenario is tested by
`FileSelectionSubsystem_Select_NegatedPattern_ThrowsNamingPattern`.

**FileSelectionSubsystem_Select_HiddenEntries_SkippedUnlessEnabled**: A wildcard skips hidden files and
directories by default and selects them when hidden matching is enabled. This scenario is tested by
`FileSelectionSubsystem_Select_HiddenEntries_SkippedUnlessEnabled`.

**FileSelectionSubsystem_Select_LiteralHiddenPattern_SelectsHiddenEntries**: A pattern that names `.github`
selects the file beneath it and not a file in another hidden directory. This scenario is tested by
`FileSelectionSubsystem_Select_LiteralHiddenPattern_SelectsHiddenEntries`.

**FileSelectionSubsystem_Select_IgnoredDirectory_IsNotEnumerated**: With an ignored directory, a hidden
directory, and a visible directory, only the root and the visible directory are enumerated. This scenario is
tested by `FileSelectionSubsystem_Select_IgnoredDirectory_IsNotEnumerated`.

**FileSelectionSubsystem_Select_UnorderedTree_ReturnsOrdinalOrder**: Files created out of order across
directories are returned in ordinal order. This scenario is tested by
`FileSelectionSubsystem_Select_UnorderedTree_ReturnsOrdinalOrder`.
