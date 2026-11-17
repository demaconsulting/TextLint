## Configuration

![Configuration Structure](ConfigurationView.svg)

### Overview

The `Configuration` subsystem turns a `.textlint.yaml` file, or values an application builds in
code, into one immutable settings value, and reports every configuration problem as data. It owns
the settings schema: the supported keys are `version`, `enabled`, `files`, `ignorePaths`, `enableGlobDot`,
`language`, `words`, `ignoreWords`, `flagWords`, `minWordLength`, `maxFileSize`, `ignoreRegExpList`, `patterns`,
`ignoreRandomStrings`, `minRandomLength`, and `overrides`, named as cspell
names them so that configurations migrate with little change. The subsystem
contains three units: `TextLintSettings`, the immutable settings value (with its `SettingsOverride` type);
`SettingsLoader`, which reads a file into settings; and `SettingsOverrideResolver`, which works out which overrides
apply to a file and merges them into the effective settings for it. The result and diagnostic types that
`SettingsLoader` returns are documented with that unit, and `FileResolution` with `SettingsOverrideResolver`.

The subsystem does no spell checking and no console output, and it never searches for
a settings file; the caller supplies the path. It does no file selection either: choosing which files to check
from the settings belongs to the `FileSelection` subsystem, and checking text against the language, word lists,
patterns, and limits belongs to the `SpellChecking` subsystem. The one piece of glob matching it does is the
matching of an override's `filename` patterns against a path, which it delegates to `OrderedGlobList`.

### Interfaces

**SettingsLoader.LoadAsync**: Loads a settings file.

- *Type*: In-process .NET static asynchronous method.
- *Role*: Provider.
- *Contract*: Accepts the path of a settings file and an optional cancellation token, and returns a
  `SettingsLoadResult`. The result holds either the settings or the list of errors, never both:
  settings are present exactly when there are no errors. Each error carries a message, the file
  path, and the 1-based line and column when the problem has a position.
- *Constraints*: Content problems (an unreadable file, malformed YAML, a value of the wrong type,
  an unsupported key, an invalid glob pattern, an unsupported language, an unusable word-list entry,
  an invalid minimum word length or file size, an `ignoreRegExpList` entry or `patterns` definition that cannot be
  used, an invalid `ignoreRandomStrings`, `minRandomLength`, or `enabled`, and a mistake in an override such as a
  missing `filename` or a setting that an override may not hold) are returned as errors, not thrown. A null or empty
  path throws, and a canceled token cancels the load.

**TextLintSettings**: The immutable settings value.

- *Type*: .NET record with `init` properties.
- *Role*: Provider: applications read it, and may construct it directly. The `FileSelection`
  subsystem does not consume it; an application passes the `Files`, `IgnorePaths`, and
  `EnableGlobDot` values to `FileSelector`, and the `SpellChecking` subsystem reads the spelling
  settings (`Language`, `Words`, `IgnoreWords`, `FlagWords`, `MinWordLength`, `MaxFileSize`, `IgnoreRegExpList`,
  `Patterns`, `IgnoreRandomStrings`, and `MinRandomLength`) and the `Enabled` and `Overrides` settings from it.
- *Contract*: Exposes `Version`, `Enabled`, `Files`, `IgnorePaths`, `EnableGlobDot`, `Language`, `Words`,
  `IgnoreWords`, `FlagWords`, `MinWordLength`, `MaxFileSize`, `IgnoreRegExpList`, `Patterns` (a list of `NamedPattern`
  values), `IgnoreRandomStrings`, `MinRandomLength`, and `Overrides` (a list of `SettingsOverride` values) with
  documented defaults (enabled, no patterns, no overrides, random strings ignored, and a minimum random length of 40,
  as in cspell), and supports `with` expressions for deriving modified copies.
- *Constraints*: Immutable; list properties are copied on assignment; a null list, a list with a null entry, a null
  language, a minimum word length or minimum random length below 1, and a negative maximum file size are rejected on
  assignment.

**SettingsOverride**: One entry of `Overrides`.

- *Type*: .NET record with `init` properties.
- *Role*: Provider: applications read it, and may construct it directly.
- *Contract*: Exposes `Filename` (the ordered glob list that selects its files) and the settings it may change: the
  word lists, `IgnoreRegExpList`, `Patterns`, `Language`, `MinWordLength`, `IgnoreRandomStrings`, `MinRandomLength`,
  and `Enabled`. A setting that is not set is `null` (or an empty list), which is how an override that does not set a
  value differs from one that sets it to the default. It also carries where it was read from: `SourceName`, `Line`,
  and `Column`.
- *Constraints*: Immutable, with the same assignment checks as `TextLintSettings`.

**SettingsOverrideResolver.Resolve**: Works out which overrides apply to a file and the effective settings for it.

- *Type*: In-process .NET method of an internal sealed class.
- *Role*: Provider to the `SpellChecking` subsystem; not part of the public API.
- *Contract*: Is created from a `TextLintSettings` and an optional language that wins over every other; given a path
  relative to the settings root it returns a `FileResolution` (whether the file is checked and which overrides
  match), and given a resolution it returns the merged `TextLintSettings` for the checker.
- *Constraints*: Throws for an override that cannot be used (no `filename`, an unsupported language, an invalid glob
  pattern) when it is created, before any file is considered.

### Design

`SettingsLoader` depends on `TextLintSettings` to build its
result, on YamlDotNet to parse the file (see *YamlDotNet Integration Design*), and on four units of
other subsystems to validate values: the `GlobPatternExpander` and `OrderedGlobList` units of the `FileSelection`
subsystem validate glob patterns (`OrderedGlobList` the ordered `files` and `filename` lists, `GlobPatternExpander`
the `ignorePaths` list), the `SpellingDictionary` unit of the `Dictionaries` subsystem
validates the language, the `WordTokenizer` unit of the `TextScanning` subsystem validates
word-list entries, and the `PatternParser` and `PatternCatalog` units of the same subsystem validate the entries of
`ignoreRegExpList` and the definitions of `patterns`. `TextLintSettings` itself depends only on the `NamedPattern` type
of `TextScanning`, which carries a name and a pattern in its `Patterns` property, and does no validation with it.
`SettingsOverrideResolver` depends on `TextLintSettings` and on `OrderedGlobList`, which compiles and matches the
`filename` lists, and on `SpellingDictionary`, which it asks to check the language of each override. These
dependencies are deliberate and documented here because they cross subsystem
boundaries: validating values while the file is loaded lets the error carry the line and column of the
offending node, and using the very units that file selection and spell checking use guarantees that a
value accepted at load time is never rejected later. They run one way: none of those subsystems
depends on anything in this subsystem, so there is no cycle. The subsystem's tests use all of these
units for real.

Problems are returned as diagnostics rather than thrown for two reasons. First, a user fixing a
configuration wants to see every problem in one run, which an exception at the first problem
prevents. Second, the command-line tool can then print all problems in a uniform compiler-style
format. The result type snapshots its diagnostics into an immutable array when it is created, so
the invariant that settings are present exactly when there are no errors cannot be broken by a
caller. A single settings value serves both the file route and the code route.

Overrides are applied by merging, not by checking each file against rules at check time. For each file, the
`SettingsOverrideResolver` finds the overrides whose `filename` matches, merges them over the top-level settings in
the order written (general first, specific last), and returns an ordinary `TextLintSettings`, so the checker
needs to know nothing about overrides. Because most files match no override, and the merged value depends only on
which overrides matched, the merged settings of each combination are built once and shared. Matching uses the path
relative to the settings root, so a file is matched the same way whatever directory the file selection started from.

The supported key set is deliberately limited to the keys whose behavior exists: the keys that
file selection consumes, the ten spelling keys that the spell checker consumes (the six for language, word lists, and
limits, and `ignoreRegExpList`, `patterns`, `ignoreRandomStrings`, and `minRandomLength` for the text that is not
checked), `enabled` and `overrides`, which the spell checker applies per file, plus `version`,
which is carried but not validated so that migrated cspell configurations, which almost always
declare it, load without complaint. A key outside the set (for example `caseSensitive` or
`includeRegExpList`) is an error that names it, because silently ignoring a setting the user believes is in
force is a worse failure than refusing to load; keys are added to the set only in the change that
implements their behavior. An override holds a subset of the keys: the spelling keys except `maxFileSize`, which
applies to the whole run, plus `enabled` and `filename`. The cspell keys `languageId` and `dictionaries`, a nested
`overrides`, and the keys that select files (`files`, `ignorePaths`, `enableGlobDot`) are errors there that say why.

Values that the loader has validated can also be built in code, and the loader's checks are not
repeated by `TextLintSettings`, which does no validation that needs another subsystem: the setters reject only values
that are never meaningful (a null, a null list entry, a length below 1, a negative size). Settings built in code that
carry an unsupported language, an unusable word-list entry, or an unusable pattern are rejected later, when the
`SpellChecking` subsystem creates a checker from them, using the same rules, so no route lets such a value take effect
silently. The same holds for an override built in code: `SettingsOverrideResolver` rejects an override with no
`filename`, an unsupported language, or an invalid glob when it is created, and the spell checker builds a checker for
every override that has settings before any file is read, even one that no file will match.
