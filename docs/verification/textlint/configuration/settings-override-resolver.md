### SettingsOverrideResolver

#### Verification Approach

`SettingsOverrideResolver` is verified with unit tests in `SettingsOverrideResolverTests.cs`. The unit is pure: it
decides about settings built in code and relative path strings, so no file system is used. Its documented
dependencies, `OrderedGlobList` from the `FileSelection` subsystem, `TextLintSettings` and `SettingsOverride`, and
the language selection of `SpellingDictionary`, are real, so nothing is mocked. Each test builds settings with the
overrides it needs, resolves paths, and asserts on the returned `FileResolution` or on the merged
`TextLintSettings`. The `enabled` rule is verified with a table of combinations of the top-level value and the
values of several matching overrides.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A file is matched against the `filename` list of every override, in the order written, with last-match-wins
  negation, case-sensitive matching, and hidden names only when hidden matching is enabled.
- The enabled value of a file is that of the last matching override that sets it, otherwise the top-level value; a
  file outside the settings root gets the top-level value and no overrides.
- List settings are concatenated (top level first, then each matching override in order), single values come from
  the last override that sets them, patterns with the same name (ignoring case) are replaced by the later
  definition, and the other settings are copied unchanged.
- The language of the command line wins over every override and the top level, also for files that no override
  matches.
- The merged settings of one combination of overrides are built once and shared.
- An override with no filename, an unusable filename pattern, or an unsupported language is rejected when the
  resolver is created, naming the override, pattern, or language.
- Null arguments throw, and the resolver gives the same answers from several threads.

#### Test Scenarios

**SettingsOverrideResolver_Resolve_NoOverrideMatches_IsEnabledWithNoMatches**: A file outside the only override's
`filename` is enabled, matches no override, and has an empty settings key. This scenario is tested by
`SettingsOverrideResolver_Resolve_NoOverrideMatches_IsEnabledWithNoMatches`.

**SettingsOverrideResolver_Resolve_SeveralOverridesMatch_ReportsAllInOrder**: Of three overrides, the first and the
third match a documentation file; both are reported in order, and only the first, which sets words, changes the
checker and forms the settings key. This scenario is tested by
`SettingsOverrideResolver_Resolve_SeveralOverridesMatch_ReportsAllInOrder`.

**SettingsOverrideResolver_Resolve_OrderedFilenameWithNegation_LastMatchWins**: An override with `**/*.md`,
`!docs/**`, and `docs/keep/**` matches a root file, does not match a file in `docs`, and matches a file in
`docs/keep`. This scenario is tested by `SettingsOverrideResolver_Resolve_OrderedFilenameWithNegation_LastMatchWins`.

**SettingsOverrideResolver_Resolve_DifferentCase_DoesNotMatch**: `Docs/a.md` does not match an override for
`docs/**`. This scenario is tested by `SettingsOverrideResolver_Resolve_DifferentCase_DoesNotMatch`.

**SettingsOverrideResolver_Resolve_HiddenFile_FollowsEnableGlobDot**: A file in `.github` matches an override for
`**/*.md` only when hidden matching is enabled. This scenario is tested by
`SettingsOverrideResolver_Resolve_HiddenFile_FollowsEnableGlobDot`.

**SettingsOverrideResolver_Resolve_EnabledCombinations_LastSettingWins**: For eight combinations of a top-level
value and one or two matching overrides that set `enabled` or leave it, the result is the value of the last
override that sets it, otherwise the top-level value. This scenario is tested by
`SettingsOverrideResolver_Resolve_EnabledCombinations_LastSettingWins`.

**SettingsOverrideResolver_ResolveUnmatched_UsesTopLevelEnabled**: With the top level disabled and an override that
would enable every file, a file with no settings-relative path is disabled and matches no override. This scenario is
tested by `SettingsOverrideResolver_ResolveUnmatched_UsesTopLevelEnabled`.

**SettingsOverrideResolver_GetEffectiveSettings_Lists_AreConcatenatedInOrder**: The words, ignore words, flag words,
and `ignoreRegExpList` entries of two matching overrides follow the top-level entries in order, and the merged
settings hold no overrides. This scenario is tested by
`SettingsOverrideResolver_GetEffectiveSettings_Lists_AreConcatenatedInOrder`.

**SettingsOverrideResolver_GetEffectiveSettings_SingleValues_LastSettingWins**: Of two matching overrides, the
language, random-string switch, and minimum random length come from the first, and the minimum word length from the
later one that sets only it. This scenario is tested by
`SettingsOverrideResolver_GetEffectiveSettings_SingleValues_LastSettingWins`.

**SettingsOverrideResolver_GetEffectiveSettings_OtherSettings_AreCopied**: The version, files, ignore paths,
hidden-file switch, and maximum file size of the settings are unchanged in the merged settings. This scenario is
tested by `SettingsOverrideResolver_GetEffectiveSettings_OtherSettings_AreCopied`.

**SettingsOverrideResolver_GetEffectiveSettings_PatternWithSameName_IsReplaced**: Definitions of `Mine` in two
overrides replace the top-level one, compared ignoring case, in the position the later definition takes; `Kept` and
a new `Extra` remain; merging for the first override alone keeps its own definition. This scenario is tested by
`SettingsOverrideResolver_GetEffectiveSettings_PatternWithSameName_IsReplaced`.

**SettingsOverrideResolver_GetEffectiveSettings_LanguagePrecedence_CommandLineWins**: With a top-level language and
an override for British English, a command-line language wins over both, and without one the override's language is
used. This scenario is tested by `SettingsOverrideResolver_GetEffectiveSettings_LanguagePrecedence_CommandLineWins`.

**SettingsOverrideResolver_GetEffectiveSettings_NoOverrideMatches_AppliesCommandLineLanguage**: A file that no
override matches still gets the command-line language. This scenario is tested by
`SettingsOverrideResolver_GetEffectiveSettings_NoOverrideMatches_AppliesCommandLineLanguage`.

**SettingsOverrideResolver_GetEffectiveSettings_SameCombination_ReturnsSameInstance**: Two files with the same
matching override share one merged settings instance, which differs from the instance for a file with no override
and holds no override words. This scenario is tested by
`SettingsOverrideResolver_GetEffectiveSettings_SameCombination_ReturnsSameInstance`.

**SettingsOverrideResolver_HasCheckerSettings_ReportsWhetherCheckerIsAffected**: An override that only sets
`enabled` has no checker settings, and an override that sets any one of the words, ignore words, flag words,
`ignoreRegExpList`, patterns, language, minimum word length, random-string switch, or minimum random length has. This
scenario is tested by `SettingsOverrideResolver_HasCheckerSettings_ReportsWhetherCheckerIsAffected`.

**SettingsOverrideResolver_GetOverride_Index_ReturnsOverride**: The overrides are counted and returned by index as
given, and an index past the end throws. This scenario is tested by
`SettingsOverrideResolver_GetOverride_Index_ReturnsOverride`.

**SettingsOverrideResolver_New_EmptyFilename_ThrowsArgumentException**: An override without a filename throws an
exception that names it as override 1. This scenario is tested by
`SettingsOverrideResolver_New_EmptyFilename_ThrowsArgumentException`.

**SettingsOverrideResolver_New_UnusableFilenamePattern_ThrowsInvalidGlobPattern**: A filename of `a?`, `!docs/**`
as the first pattern, or `../x` throws an exception that exposes the pattern. This scenario is tested by
`SettingsOverrideResolver_New_UnusableFilenamePattern_ThrowsInvalidGlobPattern`.

**SettingsOverrideResolver_New_UnsupportedOverrideLanguage_ThrowsArgumentException**: An override language of
`klingon` throws an exception that names it, even when a valid command-line language would hide it. This scenario
is tested by `SettingsOverrideResolver_New_UnsupportedOverrideLanguage_ThrowsArgumentException`.

**SettingsOverrideResolver_NullArguments_ThrowArgumentNullException**: Null settings, a null path, and a null
resolution each throw. This scenario is tested by `SettingsOverrideResolver_NullArguments_ThrowArgumentNullException`.

**SettingsOverrideResolver_ConcurrentUse_GivesTheSameAnswers**: Eight parallel callers resolve and merge four
paths against overlapping overrides and each gets the settings keys of a single-threaded caller. This scenario is
tested by `SettingsOverrideResolver_ConcurrentUse_GivesTheSameAnswers`.
