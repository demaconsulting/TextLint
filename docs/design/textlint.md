# TextLint

![TextLint Structure](TextLintView.svg)

## Architecture

TextLint is the `DemaConsulting.TextLint` .NET class library. It is the reusable core of the
TextLint product: the `textlint` command-line tool (see the _TextLintTool_ design) consumes it,
and any other .NET application may reference it directly.

The library is composed of two subsystems and consumes two OTS items:

- **Configuration** turns a `.textlint.yaml` file, or values built in code, into one immutable
  settings value, and reports every configuration problem as a positioned error. It contains the
  `TextLintSettings` and `SettingsLoader` units. See _Configuration Design_.
- **FileSelection** applies include and ignore glob patterns to a root directory and returns the ordered list of files to
  check. It contains the `GlobPatternExpander`, `HiddenPathPolicy`, and `FileSelector` units.
  See _FileSelection Design_.
- **YamlDotNet** (OTS) parses the settings file and **Microsoft.Extensions.FileSystemGlobbing**
  (OTS) matches glob patterns against the file system. See _OTS Software Integration Design_.

The dependency between the two subsystems runs in one direction only, so there is no cycle at
any level. `SettingsLoader` (Configuration) validates glob patterns with `GlobPatternExpander`
(FileSelection) so that a bad pattern is reported at load time with its line and column, and
sharing the one expander guarantees that a pattern accepted when the file is loaded is never
rejected when files are selected. FileSelection depends on nothing in Configuration: `FileSelector`
takes the include patterns, the ignore patterns, and the hidden-file switch as plain parameters,
and a caller that holds `TextLintSettings` passes its `Files`, `IgnorePaths`, and `EnableGlobDot`
values. The dependency is documented in the design of the Configuration subsystem.

**Layering.** The library owns **all linting logic**: settings and configuration loading, file
discovery and globbing, dictionaries, tokenization, spell checking, and results. The settings and
file selection capabilities described above are in place; the remaining capabilities are added in
later changes as further subsystems of this system, each with its own requirements, design, and
verification documents. The library is self-contained: it never depends on the tool, so any .NET
application can lint without it. The command-line tool is only a thin front-end over this library
and is not permitted to carry linting logic of its own.

## External Interfaces

**NuGet Package**: The distribution interface for consumers.

- _Type_: NuGet package.
- _Role_: Provider.
- _Contract_: The package is named `DemaConsulting.TextLint`, carries the README, icon, MIT license
  expression, and source-link metadata, and ships a `net10.0` assembly with XML documentation and
  a symbols package.
- _Constraints_: The package contains no native assets and has exactly two runtime package
  dependencies, YamlDotNet and Microsoft.Extensions.FileSystemGlobbing.

**Public API**: The programmatic interface for consuming applications.

- _Type_: .NET assembly.
- _Role_: Provider.
- _Contract_: In the `DemaConsulting.TextLint.Configuration` namespace, `TextLintSettings` (an
  immutable record), `SettingsLoader` (asynchronous loading of a settings file), `SettingsLoadResult`,
  and `SettingsDiagnostic`. In the `DemaConsulting.TextLint.FileSelection` namespace, `FileSelector`
  (selecting files), `SelectedFile`, and `InvalidGlobPatternException`.
- _Constraints_: No public signature exposes a type from YamlDotNet or
  Microsoft.Extensions.FileSystemGlobbing, so upgrading those packages can never break consumers.
  Every public type is trim and AOT safe.

**Configuration file**: The settings file read by the library.

- _Type_: YAML file, conventionally named `.textlint.yaml`, supplied to the loader by explicit path.
- _Role_: Consumer.
- _Contract_: A single YAML document of cspell-compatible keys. The supported keys are `version`,
  `files`, `ignorePaths`, and `enableGlobDot`; an empty file or a file of only comments is valid and
  yields default settings.
- _Constraints_: Any other key is an error that names the key. A file with more than one YAML
  document is an error. Glob patterns must be valid under the file selection rules. The library never
  searches for the file; the caller supplies its path.

## Dependencies

- **Runtime**: YamlDotNet and Microsoft.Extensions.FileSystemGlobbing. Both are runtime package
  dependencies that flow to consumers of the package. See _OTS Software Integration Design_ for how
  each is used.
- **Build time**: SourceLink and the SBOM targets package (both private to the build), and the
  .NET and Sonar analyzers. None of these flow to consumers of the package.
- **Consumers**: the `DemaConsulting.TextLint.Tool` project depends on this library, never the
  reverse. The tool currently uses none of its types; the reference establishes the dependency
  direction that later changes build on.

## Risk Control Measures

N/A - not a safety-classified software item.

## Data Flow

Settings reach the rest of the library by one of two routes, and from there a single route selects files:

1. **File route**: the caller passes the path of a `.textlint.yaml` file to `SettingsLoader`, which
   reads it, parses it with YamlDotNet, maps the supported keys onto an immutable `TextLintSettings`,
   and validates each glob pattern. The outcome is a `SettingsLoadResult` holding either the
   settings or the errors, each with file, line, and column.
2. **Code route**: the caller builds a `TextLintSettings` directly, optionally deriving it from
   loaded settings with a `with` expression, for example to replace the include patterns with
   patterns given on a command line.
3. **Selection**: the caller passes the root directory and the patterns and hidden-file switch (the
   `Files`, `IgnorePaths`, and `EnableGlobDot` values of the settings, or values obtained any other
   way) to `FileSelector`, which
   expands the patterns, walks the root directory with the matcher while skipping excluded and
   hidden directories, and returns the matching files ordered by ordinal relative path using
   forward slashes. An invalid pattern passed to selection is reported there as an exception.

## Design Constraints

- Platform: targets net10.0 only; the platform requirements apply on Windows, Linux, and macOS.
- Trimming and Native AOT: the tool is planned to be published as a Native AOT executable, so the
  library must stay trim and AOT safe. The library enables the trim, AOT, and single-file
  analyzers (`IsAotCompatible`) with warnings treated as errors, so reflection-dependent or
  otherwise AOT-unsafe code cannot be added without failing the build, and it reads YAML through
  YamlDotNet's node model rather than its reflection-based serializer. The assembly is marked
  trimmable. This is a design constraint rather than a requirement: a Native AOT consumer is not
  exercised by a published sample in this release, and Native AOT support for the tool is deferred
  until its startup cost has been measured.
- Case sensitivity: glob patterns are matched case-sensitively on every operating system, so a
  configuration selects the same files everywhere.
- Root-relative patterns: glob patterns are relative to the root directory searched. A pattern that
  begins with a single `/` is anchored to that root directory, as in ignore files and cspell, and is
  not a file system path; true absolute paths (a drive letter or a `//` network path) are rejected
  both when a settings file is loaded and when files are selected.
- Hidden files: wildcards never match a name that starts with a dot unless `enableGlobDot` is set,
  but a pattern segment that itself starts with a literal dot does, following cspell.
- Packaging: the library and the tool are packed and published as separate NuGet packages from the
  same repository and version.
- Dependencies: runtime dependencies are kept minimal, because the tool is planned to be
  published as a self-contained Native AOT executable; the library has exactly two.
- Layering: all linting logic belongs in this library. The library must not reference the tool,
  and must not depend on console output, command-line parsing, or process exit codes, which are
  the tool's concerns.
