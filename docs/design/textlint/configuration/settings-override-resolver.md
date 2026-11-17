### SettingsOverrideResolver

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`SettingsOverrideResolver` gives the `overrides` and `enabled` settings their meaning. For a file it works out which
overrides apply, whether the file is checked at all, and the effective settings the spell checker must use for it.
It is the one place where overrides are merged, so the spell checker only has to ask, and the checker of a single
text (`TextChecker`) needs to know nothing about overrides: the merged result is an ordinary `TextLintSettings`.
It also defines the supporting type `FileResolution`, which has no unit of its own.

#### Data Model

`SettingsOverrideResolver` is an internal sealed class. It holds the settings it refines, the language given by the
caller (the command line) or `null`, one compiled `OrderedGlobList` per override (the `filename` list, compiled with
the settings' `EnableGlobDot`), and a concurrent dictionary of lazily merged settings keyed by the combination of
overrides.
Apart from that cache, it is immutable, performs no file system access, and is thread-safe once created.

**FileResolution** is an internal sealed record produced by the resolver:

- `Enabled`: whether the file is checked.
- `MatchedOverrides`: the zero-based indices of every override that matches the file, ascending.
- `SettingsOverrides`: the subset of the matched overrides that set something for the checker (a word list, a
  pattern, a language, a number, or a switch). An override that only sets `enabled` is in the first list but not in
  this one.
- `SettingsKey`: the indices in `SettingsOverrides` joined with commas; empty when the top-level settings apply
  as they are. It is the cache key and the name of the checker combination.

The two lists are kept apart because every matching override counts as used (so it is not reported as matching no
file), whereas only those with checker settings need a differently configured checker.

#### Key Methods

**Constructor**: Compiles the overrides.

- *Parameters*: `TextLintSettings settings`; `string? languageOverride`, the language that wins over every override
  and over the top-level language (the command-line language), or `null`.
- *Preconditions*: `settings` is not null.
- *Postconditions*: every override's `filename` list is compiled, so an unusable override fails here, before any file
  is considered.

Each override is checked in order: an empty `filename` list throws `ArgumentException`; a `Language` is passed to
`SpellingDictionary.ForLanguages`, which throws `ArgumentException` for an unsupported language, even when
`languageOverride` hides it from the checker; and the `filename` list is compiled with `OrderedGlobList.Parse`,
which throws `InvalidGlobPatternException` for an invalid pattern.

**Resolve**: Finds the overrides that match a file and whether the file is checked.

- *Parameters*: `string settingsRelativePath`, the path relative to the settings root, using forward slashes.
- *Returns*: a `FileResolution`.
- *Postconditions*: none; the method is pure.

Every override is tested in the order written with `OrderedGlobList.IsMatch`, so `!` and last-match-wins work as in
`files`, and matching is case-sensitive. `Enabled` starts as the top-level `Enabled`, and each matching override that
sets `enabled` replaces it, so the last matching override that sets it wins. An override with `enabled: false` still
counts as matched.

**ResolveUnmatched**: Returns the resolution of a file that is outside the settings root, to which no override applies.
Only the top-level `Enabled` decides.

**GetEffectiveSettings**: Returns the merged `TextLintSettings` for a resolution.

- *Parameters*: a `FileResolution`.
- *Returns*: the effective settings, which carry no overrides.
- *Postconditions*: the merged settings of each `SettingsKey` are built once and shared; the call is thread-safe.

**GetEffectiveSettingsForOverride**: Returns the effective settings for a file that only one override matches. The
spell checker uses it to build a checker for every override before any file is read, which validates the override's
words, patterns, and language even when no file matches it.

**HasCheckerSettings**: Reports whether an override sets anything for a checker. An override that only sets `enabled`
changes no checker and needs none.

**GetOverride**, **OverrideCount**: Give access to the override at an index and the number of overrides.

#### Merge Rules

The matching overrides that carry checker settings apply in the order written, so general rules come first and
specific ones last. The merge starts from the top-level settings:

- *Lists* (`Words`, `IgnoreWords`, `FlagWords`, `IgnoreRegExpList`): the top-level list followed by each override's
  list, so an override adds to the values and does not replace them.
- *Patterns*: the top-level definitions, then for each override in order, any existing definition whose name equals,
  ignoring letter case, a name that the override defines is removed and the override's definitions are appended. A
  definition of a name replaces an earlier one for the files the override matches, so the result has no duplicate
  names. The names that an `ignoreRegExpList` entry uses are resolved afterwards by the checker, from the merged
  definitions, so the order of the lists does not matter.
- *Single values* (`MinWordLength`, `IgnoreRandomStrings`, `MinRandomLength`): the value of the last override that sets
  it, otherwise the top-level value.
- *Language* (precedence): the language given by the caller wins over everything; otherwise the language of the last
  override that sets one; otherwise the top-level language. So the command-line language replaces the languages of
  overrides as well as the top-level one.
- *Enabled*: decided by `Resolve`, not by the merge, as described above.
- Everything else (`Files`, `IgnorePaths`, `EnableGlobDot`, `MaxFileSize`, `Version`) is copied from the top level, and
  `Overrides` is cleared in the result, because the overrides have been applied. With no matching override and a
  language given by the caller, the effective settings are the top-level settings with that language.

#### Caching

Most files match no override that has checker settings, so they share the effective settings of the top level, and a
combination of overrides is merged only once however many files match it. The cache is keyed by `SettingsKey`, the
index set (for example `1,3`), and not by the settings value, because the settings hold their lists as immutable arrays
that are compared by reference. The dictionary is concurrent and holds a `Lazy<TextLintSettings>` for each key, created
with `LazyThreadSafetyMode.ExecutionAndPublication`. When several threads ask for the same new key together, the
dictionary publishes one `Lazy`, and exactly one thread runs the merge inside it while the others wait and share the
result, so a combination is merged once and never more. Should a merge throw, the `Lazy` keeps the exception and
every later request for that combination throws it again; merging reads only validated settings and is not expected to
throw. The spell checker nevertheless builds every combination during its sequential pre-pass, before the parallel
phase.

#### Error Handling

The constructor throws `ArgumentNullException` for null settings, `ArgumentException` for an override with no `filename`
pattern or an unsupported language, and `InvalidGlobPatternException` (an `ArgumentException`) for an invalid pattern,
in each case before any file is considered. `GetOverride`, `HasCheckerSettings`, and `GetEffectiveSettingsForOverride`
throw `ArgumentOutOfRangeException` for an index outside the overrides, and a null argument throws
`ArgumentNullException`; these are programming errors. Settings loaded from a file have already been validated by
`SettingsLoader`, so these exceptions only come from settings built in code. Nothing is logged.

#### Dependencies

- **TextLintSettings** and **SettingsOverride** - the values that are merged.
- **OrderedGlobList** (FileSelection subsystem) - compiles and matches the `filename` lists. This is a documented
  dependency of the Configuration subsystem on the FileSelection subsystem.
- **SpellingDictionary** (Dictionaries subsystem) - checks the language of each override. This is a documented
  dependency of the Configuration subsystem on the Dictionaries subsystem.
- **.NET BCL** - immutable arrays, collections, and a concurrent dictionary.

#### Callers

- **SpellChecker** (SpellChecking subsystem) - creates the resolver, builds a checker for each override, resolves every
  selected file, and uses the resolutions to drop disabled files, choose the checker, and report notes.
