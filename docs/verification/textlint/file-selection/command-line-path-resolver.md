### CommandLinePathResolver

#### Verification Approach

`CommandLinePathResolver` is verified with unit tests in `CommandLinePathResolverTests.cs`. The unit is pure string
logic over its arguments, so no mocks, doubles, or file system entries are used. Most tests call the internal
overload that takes the path rules as a parameter, so every scenario proves both the Windows rules and the
other rules on whatever operating system runs the tests; a few tests call the public method with paths of the
running operating system and assert per platform where the rules differ. The supporting type `CommandLineSelection`
is verified through the same tests.

#### Test Environment

N/A - standard test environment. No test touches the file system; the public-method tests only read the current
directory and the path of the temporary directory.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Backslashes are separators under the Windows rules and are rejected, naming the argument, under the others.
- Absolute arguments are split at their first wildcard; those inside the root become anchored root-relative
  patterns, those outside make their literal directory the search directory, and a trailing separator selects a
  whole directory.
- Windows comparisons ignore case and the others are exact.
- Combinations that cannot be searched from one directory are rejected with an explanation.
- Every rejection names the argument as typed.
- The patterns of a selection are an immutable copy.

#### Test Scenarios

**CommandLinePathResolver_ResolveWithStyle_WindowsBackslashRelative_ConvertsSeparators**: `.\docs\guide.md` becomes
`./docs/guide.md` and is reported relative. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsBackslashRelative_ConvertsSeparators`.

**CommandLinePathResolver_ResolveWithStyle_UnixBackslashRelative_IsRejected**: `docs\guide.md` under the other
rules is rejected naming the argument and the separator. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_UnixBackslashRelative_IsRejected`.

**CommandLinePathResolver_Resolve_BackslashRelativePath_FollowsPlatformRules**: Through the public method, Windows
converts the path and every other system rejects it. This scenario is tested by
`CommandLinePathResolver_Resolve_BackslashRelativePath_FollowsPlatformRules`.

**CommandLinePathResolver_ResolveWithStyle_WindowsAbsoluteFileInsideRoot_AnchorsBeneathRoot**: A drive path to a
file below the root becomes `/docs/guide.md` with the root kept and absolute reporting on. This scenario is
tested by `CommandLinePathResolver_ResolveWithStyle_WindowsAbsoluteFileInsideRoot_AnchorsBeneathRoot`.

**CommandLinePathResolver_ResolveWithStyle_AbsoluteFileDirectlyInRoot_IsAnchored**: A file directly in the root
becomes `/README.md`, so it does not match at any depth. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_AbsoluteFileDirectlyInRoot_IsAnchored`.

**CommandLinePathResolver_ResolveWithStyle_WindowsDifferentCase_IsInsideRoot**: A drive path that differs from the
root only in case is inside the root under the Windows rules. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsDifferentCase_IsInsideRoot`.

**CommandLinePathResolver_ResolveWithStyle_UnixDifferentCase_IsOutsideRoot**: The same difference under the other
rules is a different directory, which becomes the search directory. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_UnixDifferentCase_IsOutsideRoot`.

**CommandLinePathResolver_ResolveWithStyle_AbsoluteWildcardInsideRoot_KeepsWildcardPart**: The wildcard part of
`C:/work/proj/docs/**/*.{md,txt}` is kept after the literal directory. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_AbsoluteWildcardInsideRoot_KeepsWildcardPart`.

**CommandLinePathResolver_ResolveWithStyle_AbsoluteOutsideRoot_UsesLiteralPartAsBase**: `D:\other\notes\*.md` makes
`D:/other/notes` the search directory. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_AbsoluteOutsideRoot_UsesLiteralPartAsBase`.

**CommandLinePathResolver_ResolveWithStyle_AbsoluteDirectory_SelectsEverythingBeneath**: A directory written with a
trailing backslash becomes `/docs/**`. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_AbsoluteDirectory_SelectsEverythingBeneath`.

**CommandLinePathResolver_ResolveWithStyle_FileSystemRootDirectory_SelectsEverything**: `/` is the search directory
with the pattern `/**`. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_FileSystemRootDirectory_SelectsEverything`.

**CommandLinePathResolver_ResolveWithStyle_TwoAbsoluteOutsideRoot_UsesCommonAncestor**: Two files in sibling
directories outside the root share their parent as the search directory. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_TwoAbsoluteOutsideRoot_UsesCommonAncestor`.

**CommandLinePathResolver_ResolveWithStyle_DifferentDrives_ThrowsArgumentException**: Files on two drives are
rejected with a message about different drives. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_DifferentDrives_ThrowsArgumentException`.

**CommandLinePathResolver_ResolveWithStyle_RelativeAndOutsideAbsolute_ThrowsArgumentException**: A relative name with
an absolute path outside the root is rejected, and the message names both. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_RelativeAndOutsideAbsolute_ThrowsArgumentException`.

**CommandLinePathResolver_ResolveWithStyle_RelativeAndInsideAbsolute_AreCombined**: A relative name and an absolute
path inside the root give two patterns and absolute reporting. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_RelativeAndInsideAbsolute_AreCombined`.

**CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPath_UsesServerAndShareAsRoot**: `\\server\share\docs\*.md`
makes `//server/share/docs` the search directory. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPath_UsesServerAndShareAsRoot`.

**CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPathWithoutShare_IsRejected**: `\\server` is rejected naming
the argument and the missing share. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPathWithoutShare_IsRejected`.

**CommandLinePathResolver_ResolveWithStyle_WindowsDriveRelative_IsRejected**: `C:docs\a.md` is rejected as
drive-relative, naming the argument as typed. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsDriveRelative_IsRejected`.

**CommandLinePathResolver_ResolveWithStyle_WindowsRootedWithoutDrive_IsRejected**: `\docs\a.md` and `/docs/a.md`
are rejected as ambiguous under the Windows rules. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_WindowsRootedWithoutDrive_IsRejected`.

**CommandLinePathResolver_ResolveWithStyle_UnixDriveLetterPath_IsRejected**: `C:/work/a.md` under the other rules is
not absolute and the strict rules reject it. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_UnixDriveLetterPath_IsRejected`.

**CommandLinePathResolver_ResolveWithStyle_Tilde_IsNotExpanded**: `~/docs/*.md` stays a relative pattern under both
rule sets. This scenario is tested by `CommandLinePathResolver_ResolveWithStyle_Tilde_IsNotExpanded`.

**CommandLinePathResolver_ResolveWithStyle_ParentSegmentsInLiteralPart_AreNormalized**: `.` and `..` segments before
the first wildcard are resolved lexically. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_ParentSegmentsInLiteralPart_AreNormalized`.

**CommandLinePathResolver_ResolveWithStyle_ParentAboveFileSystemRoot_IsRejected**: `/../a.md` is rejected naming the
argument. This scenario is tested by `CommandLinePathResolver_ResolveWithStyle_ParentAboveFileSystemRoot_IsRejected`.

**CommandLinePathResolver_ResolveWithStyle_PatternRuleViolation_NamesTypedArgument**: A `?` wildcard, a `..` after a
wildcard, and unbalanced braces in an absolute argument are rejected with the reason and the argument exactly as
typed, not the rewritten pattern. This scenario is tested by
`CommandLinePathResolver_ResolveWithStyle_PatternRuleViolation_NamesTypedArgument`.

**CommandLinePathResolver_ResolveWithStyle_BlankArgument_IsRejected**: An empty or white-space argument is rejected as
an empty pattern. This scenario is tested by `CommandLinePathResolver_ResolveWithStyle_BlankArgument_IsRejected`.

**CommandLinePathResolver_Resolve_InvalidInput_Throws**: A null or blank root, a null or empty list, a null element,
and a relative root given to the internal overload each throw the documented exception. This scenario is tested by
`CommandLinePathResolver_Resolve_InvalidInput_Throws`.

**CommandLinePathResolver_Resolve_RelativeRoot_BecomesAbsolute**: A relative root is made absolute against the
current directory in the platform's form. This scenario is tested by
`CommandLinePathResolver_Resolve_RelativeRoot_BecomesAbsolute`.

**CommandLinePathResolver_Resolve_PlatformAbsolutePath_IsAnchoredInsideRoot**: A path built with the platform's own
separators below the root is recognized, anchored, and reported absolute. This scenario is tested by
`CommandLinePathResolver_Resolve_PlatformAbsolutePath_IsAnchoredInsideRoot`.

**CommandLinePathResolver_GetDisplayPath_RelativeAndAbsoluteReporting_FollowsSelection**: A relative selection leaves
the path alone, an absolute one gives the platform path, and a null path throws. This scenario is tested by
`CommandLinePathResolver_GetDisplayPath_RelativeAndAbsoluteReporting_FollowsSelection`.

**CommandLinePathResolver_Resolve_Patterns_AreImmutableCopy**: Changing the caller's list afterwards does not
change the selection, and casting the patterns to a mutable interface refuses changes. This scenario is tested by
`CommandLinePathResolver_Resolve_Patterns_AreImmutableCopy`.
