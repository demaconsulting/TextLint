## Configuration

![Configuration Structure](ConfigurationView.svg)

### Overview

The `Configuration` subsystem turns a `.textlint.yaml` file, or values an application builds in
code, into one immutable settings value, and reports every configuration problem as data. It owns
the settings schema: the supported keys are `version`, `files`, `ignorePaths`, and `enableGlobDot`,
named as cspell names them so that configurations migrate with little change. The subsystem
contains two units: `TextLintSettings`, the immutable settings value, and `SettingsLoader`, which
reads a file into settings. The result and diagnostic types that `SettingsLoader` returns are
documented with that unit.

The subsystem does no glob matching and no console output. It never searches for a settings
file; the caller supplies the path. Choosing which files to check from the settings belongs to the
`FileSelection` subsystem.

### Interfaces

**SettingsLoader.LoadAsync**: Loads a settings file.

- _Type_: In-process .NET static asynchronous method.
- _Role_: Provider.
- _Contract_: Accepts the path of a settings file and an optional cancellation token, and returns a
  `SettingsLoadResult`. The result holds either the settings or the list of errors, never both:
  settings are present exactly when there are no errors. Each error carries a message, the file
  path, and the 1-based line and column when the problem has a position.
- _Constraints_: Content problems (an unreadable file, malformed YAML, a value of the wrong type,
  an unsupported key, an invalid glob pattern) are returned as errors, not thrown. A null or empty
  path throws, and a cancelled token cancels the load.

**TextLintSettings**: The immutable settings value.

- _Type_: .NET record with `init` properties.
- _Role_: Provider: applications read it, and may construct it directly. The `FileSelection`
  subsystem does not consume it; an application passes the `Files`, `IgnorePaths`, and
  `EnableGlobDot` values to `FileSelector`.
- _Contract_: Exposes `Version`, `Files`, `IgnorePaths`, and `EnableGlobDot` with documented
  defaults, and supports `with` expressions for deriving modified copies.
- _Constraints_: Immutable; list properties are copied on assignment.

### Design

`TextLintSettings` has no dependencies. `SettingsLoader` depends on `TextLintSettings` to build its
result, on YamlDotNet to parse the file (see _YamlDotNet Integration Design_), and on the
`GlobPatternExpander` unit of the `FileSelection` subsystem to validate glob patterns. That last
dependency is deliberate and documented here because it crosses a subsystem boundary: validating
patterns while the file is loaded lets the error carry the line and column of the offending list
item, and using the very expander that file selection uses guarantees that a pattern accepted at
load time is never rejected later. It is the only dependency between the two subsystems and runs
one way: `FileSelection` depends on nothing in this subsystem, so there is no cycle. The subsystem's
tests use `GlobPatternExpander` for real.

Problems are returned as diagnostics rather than thrown for two reasons. First, a user fixing a
configuration wants to see every problem in one run, which an exception at the first problem
prevents. Second, the command-line tool can then print all problems in a uniform compiler-style
format. The result type snapshots its diagnostics into an immutable array when it is created, so
the invariant that settings are present exactly when there are no errors cannot be broken by a
caller. A single settings value serves both the file route and the code route.

The supported key set is deliberately small: the keys that file selection consumes, plus `version`,
which is carried but not validated so that migrated cspell configurations, which almost always
declare it, load without complaint. A key outside the set is an error that names it, because
silently ignoring a setting the user believes is in force is a worse failure than refusing to
load; keys are added to the set only in the change that implements their behavior.
