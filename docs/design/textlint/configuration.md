## Configuration

![Configuration Structure](ConfigurationView.svg)

### Overview

The `Configuration` subsystem turns a `.textlint.yaml` file, or values an application builds in
code, into one immutable settings value, and reports every configuration problem as data. It owns
the settings schema: the supported keys are `version`, `files`, `ignorePaths`, `enableGlobDot`,
`language`, `words`, `ignoreWords`, `flagWords`, `minWordLength`, and `maxFileSize`, named as cspell
names them so that configurations migrate with little change. The subsystem
contains two units: `TextLintSettings`, the immutable settings value, and `SettingsLoader`, which
reads a file into settings. The result and diagnostic types that `SettingsLoader` returns are
documented with that unit.

The subsystem does no glob matching, no spell checking, and no console output. It never searches for
a settings file; the caller supplies the path. Choosing which files to check from the settings belongs
to the `FileSelection` subsystem, and checking text against the language, word lists, and limits
belongs to the `SpellChecking` subsystem.

### Interfaces

**SettingsLoader.LoadAsync**: Loads a settings file.

- _Type_: In-process .NET static asynchronous method.
- _Role_: Provider.
- _Contract_: Accepts the path of a settings file and an optional cancellation token, and returns a
  `SettingsLoadResult`. The result holds either the settings or the list of errors, never both:
  settings are present exactly when there are no errors. Each error carries a message, the file
  path, and the 1-based line and column when the problem has a position.
- _Constraints_: Content problems (an unreadable file, malformed YAML, a value of the wrong type,
  an unsupported key, an invalid glob pattern, an unsupported language, an unusable word-list entry,
  an invalid minimum word length or file size) are returned as errors, not thrown. A null or empty
  path throws, and a cancelled token cancels the load.

**TextLintSettings**: The immutable settings value.

- _Type_: .NET record with `init` properties.
- _Role_: Provider: applications read it, and may construct it directly. The `FileSelection`
  subsystem does not consume it; an application passes the `Files`, `IgnorePaths`, and
  `EnableGlobDot` values to `FileSelector`, and the `SpellChecking` subsystem reads the spelling
  settings (`Language`, `Words`, `IgnoreWords`, `FlagWords`, `MinWordLength`, and `MaxFileSize`)
  from it.
- _Contract_: Exposes `Version`, `Files`, `IgnorePaths`, `EnableGlobDot`, `Language`, `Words`,
  `IgnoreWords`, `FlagWords`, `MinWordLength`, and `MaxFileSize` with documented defaults, and
  supports `with` expressions for deriving modified copies.
- _Constraints_: Immutable; list properties are copied on assignment; a null list or language, a
  minimum word length below 1, and a negative maximum file size are rejected on assignment.

### Design

`TextLintSettings` has no dependencies. `SettingsLoader` depends on `TextLintSettings` to build its
result, on YamlDotNet to parse the file (see _YamlDotNet Integration Design_), and on three units of
other subsystems to validate values: the `GlobPatternExpander` unit of the `FileSelection`
subsystem validates glob patterns, the `SpellingDictionary` unit of the `Dictionaries` subsystem
validates the language, and the `WordTokenizer` unit of the `TextScanning` subsystem validates
word-list entries. These dependencies are deliberate and documented here because they cross subsystem
boundaries: validating values while the file is loaded lets the error carry the line and column of the
offending node, and using the very units that file selection and spell checking use guarantees that a
value accepted at load time is never rejected later. They run one way: none of those subsystems
depends on anything in this subsystem, so there is no cycle. The subsystem's tests use all three
units for real.

Problems are returned as diagnostics rather than thrown for two reasons. First, a user fixing a
configuration wants to see every problem in one run, which an exception at the first problem
prevents. Second, the command-line tool can then print all problems in a uniform compiler-style
format. The result type snapshots its diagnostics into an immutable array when it is created, so
the invariant that settings are present exactly when there are no errors cannot be broken by a
caller. A single settings value serves both the file route and the code route.

The supported key set is deliberately limited to the keys whose behavior exists: the keys that
file selection consumes, the six spelling keys that the spell checker consumes, plus `version`,
which is carried but not validated so that migrated cspell configurations, which almost always
declare it, load without complaint. A key outside the set (for example `caseSensitive` or
`overrides`) is an error that names it, because silently ignoring a setting the user believes is in
force is a worse failure than refusing to load; keys are added to the set only in the change that
implements their behavior.

Values that the loader has validated can also be built in code, and the loader's checks are not
repeated by `TextLintSettings`, which stays free of dependencies: the setters reject only values that
are never meaningful (a null, a length below 1, a negative size). Settings built in code that carry an
unsupported language or an unusable word-list entry are rejected later, when the `SpellChecking`
subsystem creates a checker from them, using the same rules, so no route lets such a value take effect
silently.
