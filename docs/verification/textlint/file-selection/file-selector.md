### FileSelector

#### Verification Approach

`FileSelector` is verified with unit tests in `FileSelectorTests.cs`. Tests build real directory trees in a
per-test temporary directory and select from them, so the selector runs with the real matcher (an OTS item with
its own requirements) and its documented dependencies, `GlobPatternExpander`, `HiddenPathPolicy`, and
`OrderedGlobList`, real. The
patterns and the hidden-file switch are passed as plain parameters, since the unit does not depend on any
configuration type. The
internal `Select` overload that accepts an abstract directory is tested with a recording directory, the only
test double: it wraps the real directory and records which directories are enumerated, which proves pruning
deterministically where a timing test would be flaky. It can also run a callback as each directory starts to be
read, which the cancellation tests use to cancel the token at a chosen point of the walk, so that stopping the walk is
proven without timing. Case sensitivity is tested with files whose names differ
from the pattern only in case, which works on case-insensitive file systems because the selector, not the file
system, compares names.

#### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test
uses its own uniquely named directory, so tests share no state and may run in parallel.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Include patterns select, ignore patterns remove and win over includes, and no includes select nothing.
- The include list is ordered: a `!` pattern removes the files an earlier pattern selected, a later pattern brings
  them back, a list without `!` selects what it always did, and the walk selects exactly the files the exact
  per-file rule selects.
- Argument and root-directory errors throw the documented exceptions; a missing root is distinguished from an
  unreadable one.
- Matching is case-sensitive.
- Hidden entries follow the hidden-file rule, including literal hidden names, also when the list has a `!` pattern.
- Excluded and unreachable hidden directories are never enumerated; literally named hidden directories are; a
  directory excluded by `!` is not enumerated unless a later include can reach beneath it or the `!` pattern names
  only particular files.
- Results are in ordinal order with forward-slash paths and expose the full path.
- Invalid patterns in either list, a `!` in the ignore list, and an include list that starts with `!` are rejected
  naming the pattern, before the root directory is read.
- A canceled token stops the selection with a cancellation before the call and during the walk, with and without
  hidden-name wildcards, and a token that is not canceled does not change the selection.

#### Test Scenarios

**FileSelector_Select_IncludePatterns_ReturnsMatchingFiles**: Markdown files are selected from a tree holding
markdown and C# files. This scenario is tested by `FileSelector_Select_IncludePatterns_ReturnsMatchingFiles`.

**FileSelector_Select_IgnorePatterns_ExcludesMatches**: An ignored file name is removed from the selection.
This scenario is tested by `FileSelector_Select_IgnorePatterns_ExcludesMatches`.

**FileSelector_Select_NoIncludePatterns_ReturnsEmpty**: An empty include list selects nothing. This scenario is
tested by `FileSelector_Select_NoIncludePatterns_ReturnsEmpty`.

**FileSelector_Select_IgnoreAndIncludeOverlap_IgnoreWins**: A file matched by both an include and an ignore
pattern is not selected. This scenario is tested by `FileSelector_Select_IgnoreAndIncludeOverlap_IgnoreWins`.

**FileSelector_Select_NestedFiles_AreFound**: A file four directories deep is selected by a recursive pattern.
This scenario is tested by `FileSelector_Select_NestedFiles_AreFound`.

**FileSelector_Select_NamesWithSpaces_AreSelected**: A file and directory whose names contain spaces are
selected. This scenario is tested by `FileSelector_Select_NamesWithSpaces_AreSelected`.

**FileSelector_Select_BareDirectoryNameIgnored_ExcludesNestedSubtree**: A bare `node_modules` ignore pattern
removes a nested `node_modules` subtree. This scenario is tested by
`FileSelector_Select_BareDirectoryNameIgnored_ExcludesNestedSubtree`.

**FileSelector_Select_RootDirectoryMissing_ThrowsDirectoryNotFound**: A root that does not exist throws. This
scenario is tested by `FileSelector_Select_RootDirectoryMissing_ThrowsDirectoryNotFound`.

**FileSelector_Select_TokenAlreadyCanceled_ThrowsOperationCanceled**: A token that is canceled before the call
makes the call throw a cancellation. This scenario is tested by
`FileSelector_Select_TokenAlreadyCanceled_ThrowsOperationCanceled`.

**FileSelector_Select_CanceledDuringWalk_StopsWalkAndThrows**: With and without hidden-name wildcards, a token
canceled as the first subdirectory starts to be read ends the call with a cancellation; only the root and that
subdirectory were read. This scenario is tested by `FileSelector_Select_CanceledDuringWalk_StopsWalkAndThrows`.

**FileSelector_Select_TokenNotCanceled_ReturnsSelection**: A live token does not change the selection. This
scenario is tested by `FileSelector_Select_TokenNotCanceled_ReturnsSelection`.

**FileSelector_Select_RootDirectoryMissingNoIncludes_ThrowsDirectoryNotFound**: A missing root throws even when
the include list is empty. This scenario is tested by
`FileSelector_Select_RootDirectoryMissingNoIncludes_ThrowsDirectoryNotFound`.

**FileSelector_Select_RootIsFile_ThrowsIOException**: A root that names a file throws an `IOException` and is not
silently treated as empty. This scenario is tested by `FileSelector_Select_RootIsFile_ThrowsIOException`.

**FileSelector_Select_EmptyRootDirectory_ReturnsEmpty**: An existing empty root selects nothing without failing.
This scenario is tested by `FileSelector_Select_EmptyRootDirectory_ReturnsEmpty`.

**FileSelector_ProbeRoot_DirectoryNotFound_ThrowsDirectoryNotFoundNamingRoot**: A directory-not-found failure of
the root listing becomes a `DirectoryNotFoundException` that names the root and keeps the original failure. This
scenario is tested by `FileSelector_ProbeRoot_DirectoryNotFound_ThrowsDirectoryNotFoundNamingRoot`.

**FileSelector_ProbeRoot_AccessDenied_PropagatesUnauthorizedAccess**: An access failure of the root listing
reaches the caller as the same `UnauthorizedAccessException`, not as a missing root. This scenario is tested by
`FileSelector_ProbeRoot_AccessDenied_PropagatesUnauthorizedAccess`.

**FileSelector_ProbeRoot_LazyListingFails_PropagatesIOException**: A failure raised only when the listing is
stepped through reaches the caller as an `IOException`, not as a missing root. This scenario is tested by
`FileSelector_ProbeRoot_LazyListingFails_PropagatesIOException`.

**FileSelector_ProbeRoot_NullArguments_ThrowsArgumentNullException**: A null root or null listing throws. This
scenario is tested by `FileSelector_ProbeRoot_NullArguments_ThrowsArgumentNullException`.

**FileSelector_Select_InvalidPatternAndMissingRoot_ThrowsInvalidGlobPattern**: An invalid pattern is reported
even when the root directory is also missing, because patterns are validated before any directory is read. This
scenario is tested by `FileSelector_Select_InvalidPatternAndMissingRoot_ThrowsInvalidGlobPattern`.

**FileSelector_Select_NullIncludePatterns_ThrowsArgumentNullException**: Null include patterns throw. This
scenario is tested by `FileSelector_Select_NullIncludePatterns_ThrowsArgumentNullException`.

**FileSelector_Select_NullIgnorePatterns_ThrowsArgumentNullException**: Null ignore patterns throw. This
scenario is tested by `FileSelector_Select_NullIgnorePatterns_ThrowsArgumentNullException`.

**FileSelector_Select_NullRoot_ThrowsArgumentNullException**: A null root throws. This scenario is tested by
`FileSelector_Select_NullRoot_ThrowsArgumentNullException`.

**FileSelector_Select_EmptyRoot_ThrowsArgumentException**: An empty root throws `ArgumentException` rather than
the null variant. This scenario is tested by `FileSelector_Select_EmptyRoot_ThrowsArgumentException`.

**FileSelector_Select_Result_ExposesFullPath**: A selected file's full path equals the absolute path of the
created file. This scenario is tested by `FileSelector_Select_Result_ExposesFullPath`.

**FileSelector_Select_LeadingSlashPattern_MatchesOnlyAtRoot**: With `README.md` at the root and another in a
subdirectory, the pattern `/README.md` selects only the one in the root directory. This scenario is tested by
`FileSelector_Select_LeadingSlashPattern_MatchesOnlyAtRoot`.

**FileSelector_Select_DifferentCaseExtension_IsNotSelected**: `README.MD` is not selected by `**/*.md`. This
scenario is tested by `FileSelector_Select_DifferentCaseExtension_IsNotSelected`.

**FileSelector_Select_DifferentCaseDirectory_IsNotSelected**: `Docs/guide.md` is not selected by `docs/*.md`.
This scenario is tested by `FileSelector_Select_DifferentCaseDirectory_IsNotSelected`.

**FileSelector_Select_HiddenFile_SkippedByDefault**: A hidden file is not selected by `*.md`. This scenario is
tested by `FileSelector_Select_HiddenFile_SkippedByDefault`.

**FileSelector_Select_HiddenDirectory_SkippedByDefault**: A file inside `.git` is not selected by `**/*.md`.
This scenario is tested by `FileSelector_Select_HiddenDirectory_SkippedByDefault`.

**FileSelector_Select_HiddenEntries_SelectedWhenGlobDotEnabled**: With `EnableGlobDot`, hidden files and files
in hidden directories are selected, in ordinal order. This scenario is tested by
`FileSelector_Select_HiddenEntries_SelectedWhenGlobDotEnabled`.

**FileSelector_Select_LiteralHiddenDirectoryPattern_SelectsFilesBeneath**: `.github/**/*.yml` selects the
workflow file and not a file in `.cache`. This scenario is tested by
`FileSelector_Select_LiteralHiddenDirectoryPattern_SelectsFilesBeneath`.

**FileSelector_Select_LiteralHiddenFilePattern_SelectsFileAtAnyDepth**: The bare name `.editorconfig` selects
the root and nested copies and no other hidden file. This scenario is tested by
`FileSelector_Select_LiteralHiddenFilePattern_SelectsFileAtAnyDepth`.

**FileSelector_Select_WildcardBeneathLiteralHidden_SkipsNestedHidden**: Beneath a literal `.github`, a nested
hidden directory is not searched. This scenario is tested by
`FileSelector_Select_WildcardBeneathLiteralHidden_SkipsNestedHidden`.

**FileSelector_Select_LiteralInOtherPattern_DoesNotLetWildcardReachHidden**: With `**/*.md` and
`.github/*.yml`, the yaml file is selected and the markdown file in `.github` is not. This scenario is tested by
`FileSelector_Select_LiteralInOtherPattern_DoesNotLetWildcardReachHidden`.

**FileSelector_Select_IgnoredDirectory_IsNeverEnumerated**: Through a recording directory, `node_modules` and
the directory beneath it are never enumerated. This scenario is tested by
`FileSelector_Select_IgnoredDirectory_IsNeverEnumerated`.

**FileSelector_Select_HiddenDirectory_IsNeverEnumeratedByDefault**: `.git` and the directory beneath it are
never enumerated, while a visible directory is. This scenario is tested by
`FileSelector_Select_HiddenDirectory_IsNeverEnumeratedByDefault`.

**FileSelector_Select_NestedIgnoredDirectory_IsNeverEnumerated**: A directory ignored by `build/` two levels
down is not enumerated while its parent is. This scenario is tested by
`FileSelector_Select_NestedIgnoredDirectory_IsNeverEnumerated`.

**FileSelector_Select_LiteralHiddenDirectory_IsEnumerated**: A pattern naming `.github` causes it to be
enumerated while another hidden directory is not. This scenario is tested by
`FileSelector_Select_LiteralHiddenDirectory_IsEnumerated`.

**FileSelector_Select_FilesCreatedOutOfOrder_ReturnsOrdinalOrder**: Files created out of order, including mixed
case, are returned in ordinal order. This scenario is tested by
`FileSelector_Select_FilesCreatedOutOfOrder_ReturnsOrdinalOrder`.

**FileSelector_Select_NestedFiles_UseForwardSlashes**: A nested file's relative path uses forward slashes on
every platform. This scenario is tested by `FileSelector_Select_NestedFiles_UseForwardSlashes`.

**FileSelector_Select_InvalidIncludePattern_ThrowsInvalidGlobPattern**: An include list that starts with a negated
pattern throws an exception that exposes the pattern. This scenario is tested by
`FileSelector_Select_InvalidIncludePattern_ThrowsInvalidGlobPattern`.

**FileSelector_Select_InvalidIgnorePattern_ThrowsInvalidGlobPattern**: A backslash ignore pattern throws even
when nothing is included. This scenario is tested by
`FileSelector_Select_InvalidIgnorePattern_ThrowsInvalidGlobPattern`.

**FileSelector_Select_AbsolutePathPattern_ThrowsInvalidGlobPattern**: A drive path as an include pattern and a
`//` network path as an ignore pattern each throw an exception that exposes the pattern and says absolute paths
are not supported. This scenario is tested by `FileSelector_Select_AbsolutePathPattern_ThrowsInvalidGlobPattern`.

**FileSelector_Select_NegationAfterInclude_ExcludesFiles**: With `**/*.md` followed by `!docs/**`, the markdown
files in the root and in `src` are selected and the one in `docs` is not. This scenario is tested by
`FileSelector_Select_NegationAfterInclude_ExcludesFiles`.

**FileSelector_Select_IncludeAfterNegation_SelectsFilesAgain**: With `!docs` followed by `docs/keep/**`, the file in
`docs/keep` and the root file are selected, and the other files beneath `docs` are not. This scenario is tested by
`FileSelector_Select_IncludeAfterNegation_SelectsFilesAgain`.

**FileSelector_Select_ListWithoutNegation_SelectsSameFilesAsBefore**: A list of `**/*.md` and a literal
`.github/*.md` selects the root and `docs` files and the file in `.github`, and not the file in `.cache`, as plain
includes always did. This scenario is tested by `FileSelector_Select_ListWithoutNegation_SelectsSameFilesAsBefore`.

**FileSelector_Select_NegatedBareDirectory_IsNotEnumerated**: Through a recording directory, `!node_modules` leaves
`node_modules` at the root and in `src` unread while `src` is read. This scenario is tested by
`FileSelector_Select_NegatedBareDirectory_IsNotEnumerated`.

**FileSelector_Select_NegatedDirectoryWithLaterInclude_IsEnumerated**: With `!docs` followed by `docs/keep/**`,
`docs` and `docs/keep` are read and `docs/other` is not. This scenario is tested by
`FileSelector_Select_NegatedDirectoryWithLaterInclude_IsEnumerated`.

**FileSelector_Select_NegatedFilePattern_StillEnumeratesDirectory**: With `!docs/**/*.md`, `docs` is still read and
only the text file in it is selected. This scenario is tested by
`FileSelector_Select_NegatedFilePattern_StillEnumeratesDirectory`.

**FileSelector_Select_NegationAndHiddenDirectory_HiddenIsNotEnumerated**: With a `!docs/skip/` negation, `.git` and
`docs/skip` are not read and `docs` is. This scenario is tested by
`FileSelector_Select_NegationAndHiddenDirectory_HiddenIsNotEnumerated`.

**FileSelector_Select_NegationAndGlobDot_SelectsHiddenExceptNegated**: With hidden matching enabled and `!.cache`,
the file in `.github` is selected and the file in `.cache` is not. This scenario is tested by
`FileSelector_Select_NegationAndGlobDot_SelectsHiddenExceptNegated`.

**FileSelector_Select_IgnorePatternWithOrderedInclude_IgnoreStillWins**: A file that a re-include selects is removed
by an ignore pattern, and an ignore pattern that starts with `!` throws an exception that exposes it. This scenario
is tested by `FileSelector_Select_IgnorePatternWithOrderedInclude_IgnoreStillWins`.

**FileSelector_Select_LeadingNegationAndMissingRoot_ThrowsInvalidGlobPattern**: A list that starts with `!` is
reported with a message saying the negation must follow a pattern, even though the root does not exist, because
patterns are validated before any directory is read. This scenario is tested by
`FileSelector_Select_LeadingNegationAndMissingRoot_ThrowsInvalidGlobPattern`.

**FileSelector_Select_NegatedAnchoredDirectory_MatchesIgnorePattern**: With `!docs/a`, `!docs/a/`, and `!docs/a/**`,
the files of `docs/a` and below are not selected, the files of `docs/ab` and `docs` are, the selection equals the
one that the same pattern gives as an ignore pattern, and `docs/a` is not read while `docs/ab` is. This scenario is
tested by `FileSelector_Select_NegatedAnchoredDirectory_MatchesIgnorePattern`.

**FileSelector_Select_NegatedAnchoredDirectoryThenInclude_SelectsFileAgain**: With `!docs/a` followed by
`docs/a/keep.md`, only that file of `docs/a` is selected, besides the file in `docs`. This scenario is tested by
`FileSelector_Select_NegatedAnchoredDirectoryThenInclude_SelectsFileAgain`.

**FileSelector_Select_AnchoredDirectoryAsInclude_SelectsNothingBeneathIt**: The plain include `docs/a` selects no
file beneath a directory `docs/a`, while `docs/a/` selects it. This scenario is tested by
`FileSelector_Select_AnchoredDirectoryAsInclude_SelectsNothingBeneathIt`.

**FileSelector_Select_OrderedLists_MatchesExactRuleForEveryFile**: For ten ordered lists over a tree of thirteen
files, including hidden ones, the files the walk selects equal the files that `OrderedGlobList.IsMatch` selects, so
pruning loses and adds nothing. This scenario is tested by
`FileSelector_Select_OrderedLists_MatchesExactRuleForEveryFile`.
