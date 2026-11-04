### HiddenPathPolicy

#### Verification Approach

`HiddenPathPolicy` is verified with unit tests in `HiddenPathPolicyTests.cs`. The unit is pure logic over
strings with no dependencies, so no mocks, doubles, or file system are used: each test passes a relative path
(or directory path) and a set of expanded include patterns and asserts on the boolean result.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Wildcards never permit a hidden file or directory, including beneath a literal hidden directory.
- A pattern segment with a literal leading dot permits hidden names, case-sensitively.
- One pattern's literal hidden name does not excuse another pattern's wildcard.
- A hidden directory is entered only when some pattern can reach beneath it.

#### Test Scenarios

**HiddenPathPolicy_IsPathPermitted_NoHiddenSegment_IsPermitted**: A path with no hidden segment is permitted.
This scenario is tested by `HiddenPathPolicy_IsPathPermitted_NoHiddenSegment_IsPermitted`.

**HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenFile_IsNotPermitted**: `**/*.md` does not permit
`.hidden.md`. This scenario is tested by `HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenFile_IsNotPermitted`.

**HiddenPathPolicy_IsPathPermitted_LeadingStarAndHiddenFile_IsNotPermitted**: `*` does not permit `.env` in the
root. This scenario is tested by `HiddenPathPolicy_IsPathPermitted_LeadingStarAndHiddenFile_IsNotPermitted`.

**HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenDirectory_IsNotPermitted**: `**/*.md` does not permit a
file inside `.git`. This scenario is tested by
`HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenDirectory_IsNotPermitted`.

**HiddenPathPolicy_IsPathPermitted_LiteralHiddenDirectory_IsPermitted**: `.github/**/*.yml` permits a workflow
file beneath `.github`. This scenario is tested by
`HiddenPathPolicy_IsPathPermitted_LiteralHiddenDirectory_IsPermitted`.

**HiddenPathPolicy_IsPathPermitted_LiteralHiddenFileName_IsPermitted**: `**/.eslintrc.json` permits a nested
`.eslintrc.json`. This scenario is tested by `HiddenPathPolicy_IsPathPermitted_LiteralHiddenFileName_IsPermitted`.

**HiddenPathPolicy_IsPathPermitted_DotPrefixedWildcardSegment_IsPermitted**: `.*rc` permits `.bashrc`. This
scenario is tested by `HiddenPathPolicy_IsPathPermitted_DotPrefixedWildcardSegment_IsPermitted`.

**HiddenPathPolicy_IsPathPermitted_WildcardBeneathLiteralHidden_SkipsNestedHidden**: `.github/**/*.yml` does not
permit a file inside a second hidden directory beneath `.github`. This scenario is tested by
`HiddenPathPolicy_IsPathPermitted_WildcardBeneathLiteralHidden_SkipsNestedHidden`.

**HiddenPathPolicy_IsPathPermitted_LiteralInOtherPattern_DoesNotExcuseWildcard**: With `**/*.md` and
`.github/x`, `.github/foo.md` is not permitted, because neither pattern permits it on its own. This scenario is
tested by `HiddenPathPolicy_IsPathPermitted_LiteralInOtherPattern_DoesNotExcuseWildcard`.

**HiddenPathPolicy_IsPathPermitted_LiteralHiddenDifferentCase_IsNotPermitted**: `.github/**` does not permit
`.GitHub/ci.yml`. This scenario is tested by
`HiddenPathPolicy_IsPathPermitted_LiteralHiddenDifferentCase_IsNotPermitted`.

**HiddenPathPolicy_CanContainPermittedPaths_WildcardOnlyAndHiddenDirectory_IsFalse**: With only wildcard
patterns, `.git` need not be entered. This scenario is tested by
`HiddenPathPolicy_CanContainPermittedPaths_WildcardOnlyAndHiddenDirectory_IsFalse`.

**HiddenPathPolicy_CanContainPermittedPaths_LiteralHiddenDirectory_IsTrue**: A pattern naming `.github` makes
that directory enterable. This scenario is tested by
`HiddenPathPolicy_CanContainPermittedPaths_LiteralHiddenDirectory_IsTrue`.

**HiddenPathPolicy_CanContainPermittedPaths_NonHiddenDirectory_IsTrue**: An ordinary directory is always
enterable. This scenario is tested by `HiddenPathPolicy_CanContainPermittedPaths_NonHiddenDirectory_IsTrue`.

**HiddenPathPolicy_CanContainPermittedPaths_HiddenBeneathLiteralPrefix_FollowsPattern**: With `a/.b/**`,
`a/.b` is enterable and `a/.c` is not. This scenario is tested by
`HiddenPathPolicy_CanContainPermittedPaths_HiddenBeneathLiteralPrefix_FollowsPattern`.

**HiddenPathPolicy_IsHidden_DotPrefixedName_IsTrueOtherwiseFalse**: `.git` is hidden; `git` and an empty name
are not. This scenario is tested by `HiddenPathPolicy_IsHidden_DotPrefixedName_IsTrueOtherwiseFalse`.
