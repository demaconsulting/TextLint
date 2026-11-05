### SettingsLoader

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`SettingsLoader` reads a `.textlint.yaml` file and maps it onto `TextLintSettings`. Its single
responsibility is to turn file content into either settings or a complete list of positioned
errors, so that callers handle every configuration failure through one path. It also defines the
supporting result types it returns, which have no unit of their own: `SettingsLoadResult` and
`SettingsDiagnostic`.

#### Data Model

`SettingsLoader` is a static class with no state.

**SettingsLoadResult** is the outcome of a load. `Settings` is non-null exactly when `Diagnostics`
is empty (the constructor discards the settings when there are errors, which enforces the invariant
in one place); `Succeeded` reports whether settings are present. `Diagnostics` lists the errors in
source order and is a snapshot: the constructor copies the supplied list into an immutable array, so
neither the producer's list nor a cast of the exposed list to a mutable interface can change it.
Instances are immutable.

**SettingsDiagnostic** is one error. It carries `Message`, `FilePath` (the path supplied to the
loader, or null), and `Line` and `Column` (1-based, null when the problem has no position, such
as an unreadable file). Every diagnostic is an error: there is no warning severity, because a file
with any problem produces no settings. `ToString` formats the compiler style
`file(line,column): error: message`, omitting parts that are unknown. Instances are immutable, and
only the loader creates them.

The supported keys and the rule for each are:

| Key             | Accepted value                                              | Absent or null  |
| :-------------- | :---------------------------------------------------------- | :-------------- |
| `version`       | Any scalar, carried as text                                 | No version      |
| `files`         | A list of text values, each a valid glob pattern            | Empty list      |
| `ignorePaths`   | A list of text values, each a valid glob pattern            | Empty list      |
| `enableGlobDot` | Unquoted `true` or `false` (lower, title, or upper case)    | `false`         |

#### Key Methods

**LoadAsync**: Reads a file and maps it.

- _Parameters_: `string path` - the settings file; `CancellationToken cancellationToken`.
- _Returns_: `Task<SettingsLoadResult>`.
- _Preconditions_: `path` is non-null and non-empty.
- _Postconditions_: the result holds settings or diagnostics, never both.

Steps: (1) reject a null or empty path; (2) read the whole file as text asynchronously, honoring
the cancellation token and a byte-order mark; (3) convert an input/output, access, or unsupported-path
failure into a single diagnostic that names the file and has no position; (4) otherwise pass the text
and the path to `Parse`.

**Parse** (internal, no file system): Maps text and a source name to a result. It exists so the core
of loading is testable without files and so the public surface stays minimal; a public text overload
would be a further feature with its own requirement.

Steps: (1) load the text with YamlDotNet into its node model, converting a syntax error into one
diagnostic at the position the parser reports (a syntax error stops parsing, so it is the only
diagnostic); (2) no document, as for an empty or comment-only file, yields default settings; more
than one document yields an error at the start of the second; (3) a null root yields defaults and a
root that is not a mapping yields an error at the root; (4) for each key and value of the root mapping,
in document order, apply the rule in the table above.

Mapping rules in detail:

- Keys are matched with exact (ordinal) comparison, so they are case-sensitive; `Files` is not `files`. A key that is not
  plain text is an error at the key.
- A key outside the supported set is an error at the key, `'words' is not a supported setting.`,
  naming the key; its value, including any nested content, is not inspected.
- A value is null when it is an unquoted empty value, `~`, or `null`; null is treated as absent for
  every key, which keeps a commented-out list from breaking a file.
- A list value that is not a sequence is one error at the value. Each item that is not text is an
  error at the item. Each text item is checked with `GlobPatternExpander`; if the expander rejects it,
  the error carries the expander's message, which names the pattern and the reason, at the item's
  position. Every bad item is reported, not just the first.
- `enableGlobDot` accepts only the unquoted spellings `true`/`True`/`TRUE` and `false`/`False`/`FALSE`;
  quoted values and YAML 1.1 spellings such as `yes` are errors rather than guesses.
- Errors are collected across the whole mapping. If any exist, no settings are returned.

Settings are built by applying each key to the previous settings value with a `with` expression, so
the mapping is a sequence of small pure steps rather than mutation of a builder.

#### Error Handling

Content problems are converted to diagnostics and never thrown: YamlDotNet exceptions for syntax
errors (including duplicate keys, bad indentation, and unknown aliases), input/output and access
failures when reading the file, wrong types or shapes, unsupported keys, and invalid glob patterns.
Only programming errors propagate: a null path throws `ArgumentNullException`, an empty path throws
`ArgumentException`, and a cancelled token throws `OperationCanceledException`. No logging is
performed; the caller decides how to present diagnostics.

#### Dependencies

- **YamlDotNet** - parses the text into a node model with source positions. Only the node model is used,
  never the reflection-based serializer, which keeps the library trim and AOT safe. No YamlDotNet type
  appears in any public signature. See _YamlDotNet Integration Design_.
- **TextLintSettings** - the value that is built.
- **GlobPatternExpander** (FileSelection subsystem) - validates glob patterns at load time. This is the
  documented dependency of the Configuration subsystem on the FileSelection subsystem.
- **.NET BCL** - file reading and text handling.

#### Callers

- **Library consumers** - load a settings file and print any diagnostics.
- **The textlint tool** - will load the settings file and print diagnostics in a later change.
