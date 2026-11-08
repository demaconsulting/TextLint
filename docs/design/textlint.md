# TextLint

![TextLint Structure](TextLintView.svg)

## Architecture

TextLint is the `DemaConsulting.TextLint` .NET class library. It is the reusable core of the
TextLint product: the `textlint` command-line tool (see the _TextLintTool_ design) consumes it,
and any other .NET application may reference it directly.

The library is composed of five subsystems and consumes two OTS items:

- **Configuration** turns a `.textlint.yaml` file, or values built in code, into one immutable
  settings value, and reports every configuration problem as a positioned error. It contains the
  `TextLintSettings` and `SettingsLoader` units. See _Configuration Design_.
- **FileSelection** applies include and ignore glob patterns to a root directory and returns the ordered list of files to
  check. It contains the `GlobPatternExpander`, `HiddenPathPolicy`, `FileSelector`, and `CommandLinePathResolver`
  units. See _FileSelection Design_.
- **Dictionaries** answers whether a word is known to the selected English language (American, British, or
  both) and the technical vocabulary, from dictionaries embedded in the assembly. It contains the
  `SpellingDictionary` and `DictionaryImage` units. See _Dictionaries Design_.
- **TextScanning** is the pure text logic of spell checking: it decides what a word is, finds the text that is
  never checked (URLs, hashes, similar text, the text that the project's own patterns match, and the link targets of
  Markdown files), recognizes tokens that look like random data, and reads the directives that comments use to control
  checking. It reads no file and no setting. It contains the `WordTokenizer`, `DefaultPatterns`, `DocumentDirectives`,
  `PatternParser`, `PatternCatalog`, `MarkdownPatterns`, and `RandomStringDetector` units. See _TextScanning Design_.
- **SpellChecking** puts the other subsystems together: `TextChecker` checks a string against the dictionary and
  the word lists of the settings, `FileTextReader` turns a file into text, and `SpellChecker` selects the files
  of a directory, checks them in parallel, and returns one deterministic result. See _SpellChecking Design_.
- **YamlDotNet** (OTS) parses the settings file and **Microsoft.Extensions.FileSystemGlobbing**
  (OTS) matches glob patterns against the file system. See _OTS Software Integration Design_.

The dependencies between the subsystems form a directed graph without cycles, and they are the reason for the
split into five subsystems. `TextScanning` and `Dictionaries` depend on nothing else in the library.
`FileSelection` depends on nothing either: `FileSelector` takes the include patterns, the ignore patterns, and
the hidden-file switch as plain parameters. `Configuration` depends on three of them, because a settings file is
validated while it is loaded: `SettingsLoader` validates glob patterns with `GlobPatternExpander`
(FileSelection), the language with `SpellingDictionary` (Dictionaries), and the word-list entries and the patterns
with `WordTokenizer`, `PatternParser`, and `PatternCatalog` (TextScanning), and `TextLintSettings` holds the
`NamedPattern` values of TextScanning, so that a bad value is reported with its line and column and so that a value
accepted when the file is loaded is never rejected later. `SpellChecking` depends on all four others: it
consumes `TextLintSettings` (Configuration), selects files with `FileSelector` (FileSelection), looks words up
with `SpellingDictionary` (Dictionaries), and uses the units of `TextScanning`. If `TextScanning` had
instead been placed inside `SpellChecking`, `Configuration` would depend on `SpellChecking` while `SpellChecking`
depends on `Configuration`, which is the cycle the separate subsystem avoids. A caller that holds
`TextLintSettings` passes its `Files`, `IgnorePaths`, and `EnableGlobDot` values to `FileSelector` and its other
values to the checker; `SpellChecker` does this itself. The dependencies are documented in the design of each
subsystem.

The library is also the consumer of a build-time system, the **DictionaryBuilder** (see the
_DictionaryBuilder_ design). Its build runs the builder to convert the plain-text word lists under
`dictionaries/`, and the reviewed exclusions list beside them, into four binary dictionary files, which it
embeds in the assembly, and it compiles
one source file of the builder, `DictionaryFormat`, so that the program that writes the dictionaries and
the Dictionaries subsystem that reads them share one definition of the layout, the case fold, and the
hash. The builder is not shipped and the library has no run-time dependency on it.

**Layering.** The library owns **all linting logic**: settings and configuration loading, file
discovery and globbing, dictionaries, tokenization, spell checking, and results. The settings,
file selection, word lookup, spell-checking, and custom-pattern capabilities described above are in place; further
capabilities (the remaining cspell settings) are added in
later changes as further units or subsystems of this system, each with its own requirements, design, and
verification documents. The library is self-contained: it never depends on the tool, so any .NET
application can lint without it. The command-line tool is only a thin front-end over this library
and is not permitted to carry linting logic of its own. Even the interpretation of paths typed on a command line
belongs to the library (see `CommandLinePathResolver`), so every front-end reads them the same way.

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
  (selecting files), `SelectedFile`, `InvalidGlobPatternException`, and `CommandLinePathResolver` (turning
  command-line path arguments into a search directory and patterns) with its result `CommandLineSelection`. In the
  `DemaConsulting.TextLint.Dictionaries` namespace, `SpellingDictionary` (selecting a language and asking
  whether a word is known). In the `DemaConsulting.TextLint.SpellChecking` namespace, `TextChecker` (checking a
  string), `SpellChecker` (checking the files of a directory) with `SpellCheckOptions` and `SpellCheckResult`,
  and the finding types `SpellingIssue` (which can be formatted under a caller-chosen file name),
  `SpellingIssueKind`, `FileDiagnostic` (likewise), and `FileDiagnosticKind`. In the
  `DemaConsulting.TextLint.TextScanning` namespace, `NamedPattern` (a name and a pattern of the `patterns` setting) and
  `InvalidPatternException` (thrown when settings built in code hold an unusable pattern); every other type of the
  `TextScanning` subsystem is internal.
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
  `files`, `ignorePaths`, `enableGlobDot`, `language`, `words`, `ignoreWords`, `flagWords`, `minWordLength`,
  `maxFileSize`, `ignoreRegExpList`, `patterns`, `ignoreRandomStrings`, and `minRandomLength`; an empty file or a file
  of only comments is valid and yields default settings.
- _Constraints_: Any other key is an error that names the key. A file with more than one YAML
  document is an error. Glob patterns, the language, word-list entries, patterns and pattern names, the minimum word
  length, the minimum random length, and the
  maximum file size must be valid. The library never
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
  The default list of text that is never checked, the directive patterns, the predefined patterns, the Markdown
  patterns, and the random-string rules are ported from cspell-lib, which is
  likewise permissively licensed. Their notices are in `third-party/THIRD-PARTY-NOTICES.txt`, which the build packs
  into the package. The word lists are data, not software items, and the provenance of each list is recorded with the
  lists.
- **Consumers**: the `DemaConsulting.TextLint.Tool` project depends on this library, never the
  reverse. The tool currently uses none of its types; the reference establishes the dependency
  direction that later changes build on.

## Risk Control Measures

N/A - not a safety-classified software item.

## Data Flow

Settings reach the rest of the library by one of two routes, and from there the routes below select files, look
words up, and check text and files:

1. **File route**: the caller passes the path of a `.textlint.yaml` file to `SettingsLoader`, which
   reads it, parses it with YamlDotNet, maps the supported keys onto an immutable `TextLintSettings`,
   and validates each glob pattern, the language, each word-list entry, each pattern and pattern name, and the numbers.
   The outcome is a
   `SettingsLoadResult` holding either the settings or the errors, each with file, line, and column.
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
   the selection, and the answer is returned. This route is also what the spell checker uses for every word.
5. **Text checking**: the caller creates a `TextChecker` from settings (which validates the language, the word
   lists, and the patterns once) and passes a string, and optionally a source name, to `Check`. The checker finds the
   directives and the text that is never checked (the default patterns, the patterns of the settings, the patterns that
   `ignoreRegExp` directives write, and for a Markdown
   source name the Markdown patterns), splits what remains into tokens, skips a token that looks like random data,
   splits the others into words, and for each word applies the word rules (flagged words,
   minimum length, dictionary, word lists, the allowances for possessives, escapes, and contractions, and the split at
   case changes for unknown words, whose adjacent pieces may join into an accepted word), and drops the unknown words
   that lie inside a hexadecimal sequence of a token. The
   findings are returned in text order with a line and column each.
6. **File checking**: the caller passes settings and a root directory to `SpellChecker.CheckAsync`. The checker
   creates a `TextChecker`, selects the files as in route 3 on a thread-pool thread, and checks them in parallel:
   each file is read by `FileTextReader` (encoding, binary and size rules) and its text is checked as in route 5.
   Each file's outcome goes to its own slot, and the slots are assembled in file order into one
   `SpellCheckResult` with the findings, the per-file diagnostics, and the counts.

At build time a further flow produces the data: the build runs the DictionaryBuilder over the word lists
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
  dictionaries whose words are spelling variants. The target is incremental: its inputs are every word list, a
  manifest of the list of word lists, and the
  builder's assembly, and its outputs are the four dictionary files, so an unchanged build does not run the builder
  again, and an edited, added, deleted, or renamed list, or a rebuilt builder, runs it once. The manifest exists
  because a deleted file leaves no timestamp to compare: it is rewritten, only when its content differs, before each
  conversion check, so a change in the set of lists makes it newer than the outputs. After the builder succeeds the
  outputs are touched (the builder leaves a file whose content did not change alone), which can never hide a failure
  because the builder's non-zero exit code stops the target first and touching a missing output is itself an
  error. The files are then embedded as resources under
  stable logical names that do not depend on the folder or the namespace. The builder reports each problem in the
  compiler's error format and ends with a non-zero exit code, which fails the library build with its diagnostics,
  and the target must run before compilation and before the package is packed from the build output, so a package
  can never be produced without its data. The same recipe works for building, testing, and packing on Windows,
  Linux, and macOS, because it needs only the .NET host that is already running the build.
- Safe code only: the library contains no unsafe code, no unsafe compiler option, and no analyzer suppression
  for unsafe code. The embedded dictionaries are read once into managed arrays and accessed through
  bounds-checked spans, as described in the _Dictionaries Design_, which also explains why a precomputed binary
  format was chosen over building a set at run time. Safety is chosen over speed when they conflict.
- Third-party notices: the third-party word lists and the rules ported from cspell-lib require their notices to
  accompany the data and the code, so the
  package carries `THIRD-PARTY-NOTICES.txt` at its root. The command-line tool package carries the library
  assembly and therefore the same notice file, which the tool project also packs.
- Bounded regular expressions: every regular expression of the spell checker has a match timeout of five seconds,
  so hostile content cannot hang a run. The expressions that are known when the library is built (the default
  patterns, the directive patterns, the predefined and Markdown patterns, and the hexadecimal pattern) are generated at
  build time, which keeps the library trim and Native AOT safe; the patterns of a settings file, and those that
  `ignoreRegExp` directives write in a text, are translated from the
  JavaScript syntax by `PatternParser`, which rejects what it cannot translate faithfully and limits a pattern to 4096
  characters and 100 levels of nesting, and are constructed at run time with the same timeout. A
  timeout surfaces as an exception from `TextChecker` and as a `TimedOut` file diagnostic from `SpellChecker`. The
  timeout stops one match but does not prevent the processor from being busy until it fires, so a hostile file can
  cost up to five seconds for each pattern, and the first timeout ends the check of that file. A text may add at most
  32 different patterns through its directives, so a file costs at most (the patterns of the settings + 32) x 5
  seconds in the very worst case: a safety net, not a speed guarantee. Internal test seams (an overload of
  `PatternParser.Parse`, an internal `TextChecker` constructor, and an optional parameter of the internal file-checking
  method) let the tests use a limit of milliseconds; production code always uses five seconds. The expressions are
  ports of cspell's with explicit ASCII classes, because
  JavaScript and .NET differ in what the word class and the word boundary mean; see the _DefaultPatterns Design_ and
  the _PatternParser Design_.
- Deterministic parallelism: files are checked on all processor cores, and the result is the same whatever the
  degree of parallelism. Shared state is immutable (the checker, the word sets, the dictionaries, and the
  generated expressions); the per-file outcomes are written to separate array slots and assembled in the
  order of the ordinal-sorted file selection. See the _SpellChecking Design_.
- Per-file problems are data: a file that cannot be read, looks binary, is too large, or times out is reported in
  the result and never stops the run; argument errors, a missing root directory, defects, and cancellation are
  exceptions. There is no default file size limit beyond a fixed ceiling of 1 GiB that a string cannot
  exceed anyway.
- Documented differences from cspell: the checker follows cspell's documented word-splitting, ignore, directive,
  word-list, pattern, Markdown, random-string, and contraction rules and proves it with a parity corpus (see the
  _TextLint Verification_ document), but it does
  not port cspell's heuristic splitter, uses its own dictionaries, reports unsupported directives and the patterns of
  `ignoreRegExp` directives that cannot be used (which cspell drops silently), rejects the pattern
  flags and constructs that have no safe .NET equivalent and the names that no pattern defines (cspell reads an unknown
  name as a literal expression), bounds every pattern with
  a time limit, and has no
  duplicate-problem cap. The differences are listed in the user guide and, for the corpora, in a file that the
  parity test compares exactly.
- No speed requirement: the library states no requirement on lookup speed, checking speed, or start-up time. The
  design choices that favor speed are justified in the design only, and no requirement or test depends on them.
