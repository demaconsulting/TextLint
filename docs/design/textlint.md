# TextLint

![TextLint Structure](TextLintView.svg)

## Architecture

TextLint is the `DemaConsulting.TextLint` .NET class library. It is the reusable core of the
TextLint product: the `textlint` command-line tool (see the _TextLintTool_ design) consumes it,
and any other .NET application may reference it directly.

The library is composed of three subsystems and consumes two OTS items:

- **Configuration** turns a `.textlint.yaml` file, or values built in code, into one immutable
  settings value, and reports every configuration problem as a positioned error. It contains the
  `TextLintSettings` and `SettingsLoader` units. See _Configuration Design_.
- **FileSelection** applies include and ignore glob patterns to a root directory and returns the ordered list of files to
  check. It contains the `GlobPatternExpander`, `HiddenPathPolicy`, and `FileSelector` units.
  See _FileSelection Design_.
- **Dictionaries** answers whether a word is known to the selected English language (American, British, or
  both) and the technical vocabulary, from dictionaries embedded in the assembly. It contains the
  `SpellingDictionary` and `DictionaryImage` units. See _Dictionaries Design_.
- **YamlDotNet** (OTS) parses the settings file and **Microsoft.Extensions.FileSystemGlobbing**
  (OTS) matches glob patterns against the file system. See _OTS Software Integration Design_.

The dependency between the Configuration and FileSelection subsystems runs in one direction only, so
there is no cycle at
any level. `SettingsLoader` (Configuration) validates glob patterns with `GlobPatternExpander`
(FileSelection) so that a bad pattern is reported at load time with its line and column, and
sharing the one expander guarantees that a pattern accepted when the file is loaded is never
rejected when files are selected. FileSelection depends on nothing in Configuration: `FileSelector`
takes the include patterns, the ignore patterns, and the hidden-file switch as plain parameters,
and a caller that holds `TextLintSettings` passes its `Files`, `IgnorePaths`, and `EnableGlobDot`
values. The dependency is documented in the design of the Configuration subsystem. The Dictionaries
subsystem depends on neither of them: it has no settings and selects files of nothing, and a later
change connects a language setting to it.

The library is also the consumer of a build-time system, the **DictionaryBuilder** (see the
_DictionaryBuilder_ design). Its build runs the builder to convert the plain-text word lists under
`dictionaries/` into four binary dictionary files, which it embeds in the assembly, and it compiles
one source file of the builder, `DictionaryFormat`, so that the program that writes the dictionaries and
the Dictionaries subsystem that reads them share one definition of the layout, the case fold, and the
hash. The builder is not shipped and the library has no run-time dependency on it.

**Layering.** The library owns **all linting logic**: settings and configuration loading, file
discovery and globbing, dictionaries, tokenization, spell checking, and results. The settings,
file selection, and word lookup capabilities described above are in place; the remaining
capabilities are added in
later changes as further subsystems of this system, each with its own requirements, design, and
verification documents. The library is self-contained: it never depends on the tool, so any .NET
application can lint without it. The command-line tool is only a thin front-end over this library
and is not permitted to carry linting logic of its own; it is unchanged by the dictionaries.

## External Interfaces

**NuGet Package**: The distribution interface for consumers.

- _Type_: NuGet package.
- _Role_: Provider.
- _Contract_: The package is named `DemaConsulting.TextLint`, carries the README, icon, MIT license
  expression, third-party notices file, and source-link metadata, and ships a `net10.0` assembly
  with XML documentation and a symbols package. The assembly embeds the four dictionaries.
- _Constraints_: The package contains no native assets and has exactly two runtime package
  dependencies, YamlDotNet and Microsoft.Extensions.FileSystemGlobbing. The notices file, named
  `THIRD-PARTY-NOTICES.txt`, is at the package root and reproduces the license notices of the
  third-party word lists that the assembly embeds.

**Public API**: The programmatic interface for consuming applications.

- _Type_: .NET assembly.
- _Role_: Provider.
- _Contract_: In the `DemaConsulting.TextLint.Configuration` namespace, `TextLintSettings` (an
  immutable record), `SettingsLoader` (asynchronous loading of a settings file), `SettingsLoadResult`,
  and `SettingsDiagnostic`. In the `DemaConsulting.TextLint.FileSelection` namespace, `FileSelector`
  (selecting files), `SelectedFile`, and `InvalidGlobPatternException`. In the
  `DemaConsulting.TextLint.Dictionaries` namespace, `SpellingDictionary` (selecting a language and asking
  whether a word is known).
- _Constraints_: No public signature exposes a type from YamlDotNet or
  Microsoft.Extensions.FileSystemGlobbing, so upgrading those packages can never break consumers.
  Every public type is trim and AOT safe.

**Embedded dictionaries**: The data the library carries.

- _Type_: Four manifest resources of the assembly (`en-common`, `en-US`, `en-GB`, and `technical`, each
  with the extension `.tld`, under the logical-name prefix `DemaConsulting.TextLint.Dictionaries.`).
- _Role_: Provider to the library's own Dictionaries subsystem; not a public interface.
- _Contract_: Binary files in the layout defined by the _DictionaryFormat_ design.
- _Constraints_: Produced by the build from the word lists, never committed; the resource names are stable
  because the code loads them by name.

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
  .NET and Sonar analyzers. None of these flow to consumers of the package. The library's build also
  depends on the DictionaryBuilder system, which is built and run first; it is not shipped and does not
  flow to consumers.
- **Data**: the English word lists derive from SCOWL, and the technical word lists from the cspell
  dictionaries; both are permissively licensed third-party data, embedded in the assembly in binary form.
  Their notices are in `third-party/THIRD-PARTY-NOTICES.txt`, which the build packs into the package.
  They are data, not software items, and the provenance of each list is recorded with the lists.
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
4. **Word lookup**: the caller chooses a language with `SpellingDictionary.ForLanguages` (or takes
   `SpellingDictionary.Default`) and passes words to `IsKnown`. The first lookup loads the selection's
   embedded dictionaries once; each word is then folded to a key, hashed, and probed in each dictionary of
   the selection, and the answer is returned. This route is independent of the other two: nothing in the
   settings or the file selection feeds it yet.

At build time a fourth flow produces the data: the build runs the DictionaryBuilder over the word lists
and embeds the four resulting files in the assembly; see _Design Constraints_.

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
- Embedded dictionaries: the dictionaries are data inside the assembly, so word lookup needs no files at
  run time. The build produces them and no binary dictionary is committed. The library project references
  the DictionaryBuilder project without referencing its assembly, so the builder is built first; a build
  target then runs the builder with the same .NET host that runs the build, passing the word-list folder,
  an output folder under the project's intermediate folder (separate for each configuration and target
  framework), the four dictionary names, and the declaration of the technical dictionary and of the two regional
  dictionaries whose words are spelling variants. The target is incremental: its inputs are every word list, a manifest of the list of word lists, and the
  builder's assembly, and its outputs are the four dictionary files, so an unchanged build does not run the builder
  again, and an edited, added, deleted, or renamed list, or a rebuilt builder, runs it once. The manifest exists
  because a deleted file leaves no timestamp to compare: it is rewritten, only when its content differs, before each
  conversion check, so a change in the set of lists makes it newer than the outputs. After the builder succeeds the
  outputs are touched (the builder leaves a file whose content did not change alone), which can never hide a failure
  because the builder's non-zero exit code stops the target first and touching a missing output is itself an error. The files are then embedded as resources under
  stable logical names that do not depend on the folder or the namespace. The builder reports each problem in the
  compiler's error format and ends with a non-zero exit code, which fails the library build with its diagnostics,
  and the target must run before compilation and before the package is packed from the build output, so a package
  can never be produced without its data. The same recipe works for building, testing, and packing on Windows,
  Linux, and macOS, because it needs only the .NET host that is already running the build.
- Safe code only: the library contains no unsafe code, no unsafe compiler option, and no analyzer suppression
  for unsafe code. The embedded dictionaries are read once into managed arrays and accessed through
  bounds-checked spans, as described in the _Dictionaries Design_, which also explains why a precomputed binary
  format was chosen over building a set at run time. Safety is chosen over speed when they conflict.
- Third-party notices: the third-party word lists require their notices to accompany the data, so the
  package carries `THIRD-PARTY-NOTICES.txt` at its root. The command-line tool package carries the library
  assembly and therefore the same notice file, which the tool project also packs.
- No speed requirement: the library states no requirement on lookup speed or start-up time. The design
  choices that favor speed are justified in the design only, and no requirement or test depends on them.
