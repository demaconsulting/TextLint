### OrderedGlobList

#### Verification Approach

`OrderedGlobList` is verified with unit tests in `OrderedGlobListTests.cs`. The unit decides about relative path
strings, so no file system is used: each test compiles a list with `Parse` and asks `IsMatch` about paths or
`IsSubtreeExcluded` about directories. Its documented dependencies, `GlobPatternExpander` and `HiddenPathPolicy`,
and the real matcher (an OTS item) are pure and are used as they are, so nothing is mocked. The pruning decision is
additionally verified by a differential test: for several lists and a fixed tree of paths, no file beneath a
directory that `IsSubtreeExcluded` prunes may be selected by `IsMatch`, which proves the safety of pruning
exhaustively where individual examples could miss a pattern shape.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The last matching pattern decides: a negation removes what an earlier pattern selected, a later include brings it
  back, and a later negation removes it again.
- Brace groups in a negation remove every alternative, a bare name removes the name at any depth, a leading slash
  anchors at the root, and a trailing slash limits the negation to a directory's subtree.
- Matching is case-sensitive for both plain patterns and negations.
- Hidden names follow the hidden-file rule for each pattern, and are matched by wildcards when hidden names are
  enabled.
- A pattern list that starts with a negation, a double negation, a lone negation, and every unusable pattern are
  rejected with an exception that names the entry as written and, for a leading negation, says what to do.
- A directory is reported as excluded only when it holds no selected file; a later include that can reach beneath it,
  a negation of particular files, and an anchored negation without a trailing slash never exclude it.
- Null arguments throw, and a compiled list gives the same answers from several threads.

#### Test Scenarios

**OrderedGlobList_IsMatch_PlainPatterns_MatchesAnyPattern**: A list without negation selects a path that either
pattern matches, selects nothing else, and reports no negation. This scenario is tested by
`OrderedGlobList_IsMatch_PlainPatterns_MatchesAnyPattern`.

**OrderedGlobList_IsMatch_NegationAfterInclude_RemovesFiles**: With `**/*.md` followed by `!docs/**`, a root file
stays selected, a file beneath `docs` is removed, and the list reports a negation. This scenario is tested by
`OrderedGlobList_IsMatch_NegationAfterInclude_RemovesFiles`.

**OrderedGlobList_IsMatch_IncludeAfterNegation_SelectsFilesAgain**: With `docs/keep/**` after the negation of `docs`,
a file in `docs/keep` is selected again while another file beneath `docs` stays removed. This scenario is tested by
`OrderedGlobList_IsMatch_IncludeAfterNegation_SelectsFilesAgain`.

**OrderedGlobList_IsMatch_NegationAfterReinclude_RemovesAgain**: A final negation of `**/draft.md` removes a file
that the re-include selected, while another file in the same directory stays selected. This scenario is tested by
`OrderedGlobList_IsMatch_NegationAfterReinclude_RemovesAgain`.

**OrderedGlobList_IsMatch_NegationWithBraceExpansion_RemovesEveryAlternative**: `!{docs,legacy}/**` removes both
directories and leaves `src` selected. This scenario is tested by
`OrderedGlobList_IsMatch_NegationWithBraceExpansion_RemovesEveryAlternative`.

**OrderedGlobList_IsMatch_BareNameNegation_RemovesAtAnyDepth**: `!vendor` removes a root `vendor` directory and a
nested one, and does not remove `vendors`. This scenario is tested by
`OrderedGlobList_IsMatch_BareNameNegation_RemovesAtAnyDepth`.

**OrderedGlobList_IsMatch_AnchoredNegation_OnlyRemovesAtRoot**: `!/build/**` removes the root `build` directory and
not `src/build`. This scenario is tested by `OrderedGlobList_IsMatch_AnchoredNegation_OnlyRemovesAtRoot`.

**OrderedGlobList_IsMatch_TrailingSlashNegation_RemovesDirectorySubtree**: `!obj/` removes the files of an `obj`
directory and does not remove a file named `obj`. This scenario is tested by
`OrderedGlobList_IsMatch_TrailingSlashNegation_RemovesDirectorySubtree`.

**OrderedGlobList_IsMatch_DifferentCase_DoesNotMatch**: `Docs/a.md` is not selected by `docs/**`, and
`docs/Private/a.md` is not removed by `!docs/private/**`, while `docs/private/a.md` is. This scenario is tested by
`OrderedGlobList_IsMatch_DifferentCase_DoesNotMatch`.

**OrderedGlobList_IsMatch_HiddenName_FollowsHiddenRulePerPattern**: `**/*.md` does not select a file in `.github`,
`.github/**/*.md` does, and the latter does not select a file in another hidden directory. This scenario is tested
by `OrderedGlobList_IsMatch_HiddenName_FollowsHiddenRulePerPattern`.

**OrderedGlobList_IsMatch_HiddenNameAndGlobDot_Matches**: With hidden names enabled, `**/*.md` selects a file in
`.github`, and `!.cache/**` still removes a file in `.cache`. This scenario is tested by
`OrderedGlobList_IsMatch_HiddenNameAndGlobDot_Matches`.

**OrderedGlobList_IsMatch_HiddenNameNotReachedByInclude_IsNotSelected**: A later re-include with the same wildcard
does not select a file in `.git`, because the hidden-file rule applies to that pattern, and does select a visible
file beneath the negated `docs`. This scenario is tested by
`OrderedGlobList_IsMatch_HiddenNameNotReachedByInclude_IsNotSelected`.

**OrderedGlobList_Parse_EmptyList_IsEmptyAndMatchesNothing**: An empty list is empty, has no negation and no
positive patterns, and selects nothing. This scenario is tested by
`OrderedGlobList_Parse_EmptyList_IsEmptyAndMatchesNothing`.

**OrderedGlobList_PositivePatterns_PlainAndNegated_ListsOnlyPlainExpansions**: For `{a,b}/**`, `!c/**`, and `a/**`,
the positive patterns are `a/**` and `b/**`, each once, and the negation is absent. This scenario is tested by
`OrderedGlobList_PositivePatterns_PlainAndNegated_ListsOnlyPlainExpansions`.

**OrderedGlobList_Parse_LeadingNegation_ThrowsNamingPattern**: A list whose first pattern is `!docs/**` throws an
exception that exposes the pattern as written and says that the negation must follow a pattern. This scenario is
tested by `OrderedGlobList_Parse_LeadingNegation_ThrowsNamingPattern`.

**OrderedGlobList_Parse_UnusableNegatedPattern_ThrowsNamingPattern**: A second entry that is a double negation, a
lone `!`, a negated `?` wildcard, a negated backslash path, a negated unclosed brace, or a negated `..` path throws
an exception that exposes the entry as written, including the leading `!`. This scenario is tested by
`OrderedGlobList_Parse_UnusableNegatedPattern_ThrowsNamingPattern`.

**OrderedGlobList_Parse_UnusablePlainPattern_ThrowsNamingPattern**: A plain pattern with the unsupported `?`
wildcard throws an exception that exposes the pattern. This scenario is tested by
`OrderedGlobList_Parse_UnusablePlainPattern_ThrowsNamingPattern`.

**OrderedGlobList_Parse_NullList_ThrowsArgumentNullException**: A null list throws. This scenario is tested by
`OrderedGlobList_Parse_NullList_ThrowsArgumentNullException`.

**OrderedGlobList_ValidateEntry_FirstAndLaterPositions_AppliesLeadingNegationRule**: Validating one entry accepts
a negation in a later position and a plain pattern in the first position, and rejects a negation in the first
position naming it. This scenario is tested by
`OrderedGlobList_ValidateEntry_FirstAndLaterPositions_AppliesLeadingNegationRule`.

**OrderedGlobList_ValidateEntry_UnusableAndNullEntries_Throw**: An entry with the unsupported `?` wildcard throws
the pattern exception, and a null entry throws the null-argument exception. This scenario is tested by
`OrderedGlobList_ValidateEntry_UnusableAndNullEntries_Throw`.

**OrderedGlobList_IsMatchAndIsSubtreeExcluded_NullArgument_ThrowArgumentNullException**: A null path to `IsMatch`
and a null directory to `IsSubtreeExcluded` each throw. This scenario is tested by
`OrderedGlobList_IsMatchAndIsSubtreeExcluded_NullArgument_ThrowArgumentNullException`.

**OrderedGlobList_IsSubtreeExcluded_NegatedBareName_ExcludesDirectoryAtAnyDepth**: With `!node_modules`, a
`node_modules` directory at the root, one level down, and two levels down is excluded, and `src` is not. This
scenario is tested by `OrderedGlobList_IsSubtreeExcluded_NegatedBareName_ExcludesDirectoryAtAnyDepth`.

**OrderedGlobList_IsSubtreeExcluded_DirectoryBeneathExcludedSubtree_IsExcluded**: With `!docs/**`, `docs` and a
directory several levels beneath it are excluded, and a sibling directory is not. This scenario is tested by
`OrderedGlobList_IsSubtreeExcluded_DirectoryBeneathExcludedSubtree_IsExcluded`.

**OrderedGlobList_IsSubtreeExcluded_LaterIncludeCanReach_IsNotExcluded**: With `!docs` followed by `docs/keep/**`,
`docs` and `docs/keep` are not excluded because the later include can reach them, and `docs/other` is excluded.
This scenario is tested by `OrderedGlobList_IsSubtreeExcluded_LaterIncludeCanReach_IsNotExcluded`.

**OrderedGlobList_IsSubtreeExcluded_FileFormNegation_IsNotExcluded**: A negation of `docs/**/*.md` never excludes
`docs`, because the directory can still hold selected files. This scenario is tested by
`OrderedGlobList_IsSubtreeExcluded_FileFormNegation_IsNotExcluded`.

**OrderedGlobList_IsSubtreeExcluded_UnrelatedLaterInclude_StillExcluded**: A later include limited to another
directory does not veto pruning: `docs/a` is excluded while `x` and `docs` are not. This scenario is tested by
`OrderedGlobList_IsSubtreeExcluded_UnrelatedLaterInclude_StillExcluded`.

**OrderedGlobList_IsSubtreeExcluded_AnchoredNegationWithoutSlash_IsNotExcluded**: The anchored negation `!docs/a`
names only a file, so `docs/a` is not excluded and a file beneath a directory `docs/a` stays selected. This
scenario is tested by `OrderedGlobList_IsSubtreeExcluded_AnchoredNegationWithoutSlash_IsNotExcluded`.

**OrderedGlobList_IsSubtreeExcluded_LaterRecursiveInclude_IsNotExcluded**: A later include with a leading `**`
can reach anywhere, so the negated `docs` is not excluded. This scenario is tested by
`OrderedGlobList_IsSubtreeExcluded_LaterRecursiveInclude_IsNotExcluded`.

**OrderedGlobList_IsSubtreeExcluded_NoNegation_IsNotExcluded**: A list without negation never excludes a
directory. This scenario is tested by `OrderedGlobList_IsSubtreeExcluded_NoNegation_IsNotExcluded`.

**OrderedGlobList_IsSubtreeExcluded_AgreesWithIsMatchForEveryFile**: For eight lists covering bare names, subtree
negations, re-includes, brace groups, file-form negations, and anchored directory negations, over ten directories
and thirteen files, no file beneath an excluded directory is selected by `IsMatch`. This scenario is tested by
`OrderedGlobList_IsSubtreeExcluded_AgreesWithIsMatchForEveryFile`.

**OrderedGlobList_IsMatch_ConcurrentUse_GivesTheSameAnswers**: Eight parallel callers ask the same questions of
one compiled list and each gets the answers of a single-threaded caller. This scenario is tested by
`OrderedGlobList_IsMatch_ConcurrentUse_GivesTheSameAnswers`.
