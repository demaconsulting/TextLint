# Introduction

## Purpose

TextLint is a fast text linter for source code and documentation, delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET tool
(command `textlint`), which is a thin command-line front-end over the library.

**Status**: under active development. TextLint is being developed to provide cspell-compatible
spell-checking, configured by a `.textlint.yaml` file, using embedded English and technical-term
dictionaries, and to check files in parallel across all processor cores. Spell-checking is not yet
implemented. The library can already load a `.textlint.yaml` settings file and select the files to
check from it (see _Configuration File_ and _Library Usage_ below); the tool does not read the settings
file yet and provides only the command-line scaffolding (version, help, and self-validation).

## Scope

This user guide covers:

- Installation instructions
- Usage examples for common tasks
- Command-line options reference
- Practical examples for various scenarios

# Continuous Compliance

TextLint follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.TextLint.Tool
```

The reusable library is published separately as the `DemaConsulting.TextLint` NuGet package, for
applications that want to embed TextLint rather than run the command-line tool. It currently loads
settings and selects files; its API will grow as the spell-checking capabilities are added.

```bash
dotnet add package DemaConsulting.TextLint
```

# Usage

## Display Version

Display the tool version:

```bash
textlint --version
```

## Default Behavior

When invoked with no arguments, the tool displays the banner and copyright notice:

```bash
textlint
```

## Display Help

Display usage information:

```bash
textlint --help
```

## Self-Validation

Self-validation produces a report demonstrating that TextLint is functioning
correctly. This is useful in regulated industries where tool validation evidence is required.

### Running Validation

To perform self-validation:

```bash
textlint --validate
```

To save validation results to a file:

```bash
textlint --validate --results results.trx
```

The `--result` option is an accepted alias for `--results`.

The results file format is determined by the file extension: `.trx` for TRX (MSTest) format,
or `.xml` for JUnit format.

### Heading Depth

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding the validation report into a larger markdown document:

```bash
# Embed validation at heading level 2
textlint --validate --depth 2
```

### Validation Report

The validation report contains the tool version, machine name, operating system version,
.NET runtime version, timestamp, and test results.

Example validation report:

```text
# DEMA Consulting TextLint Tool

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | 1.0.0                                              |
| Machine Name        | BUILD-SERVER                                       |
| OS Version          | Ubuntu 22.04.3 LTS                                 |
| DotNet Runtime      | .NET 10.0.0                                        |
| Time Stamp          | 2024-01-15 10:30:00 UTC                            |

✓ TextLintTool_VersionDisplay - Passed
✓ TextLintTool_HelpDisplay - Passed

Total Tests: 2
Passed: 2
Failed: 0
```

### Validation Tests

Each test proves specific functionality works correctly:

- **`TextLintTool_VersionDisplay`** - `--version` outputs a valid version string.
- **`TextLintTool_HelpDisplay`** - `--help` outputs usage and options information.

## Silent Mode

Suppress console output:

```bash
textlint --silent
```

## Logging

Write output to a log file:

```bash
textlint --log output.log
```

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

This behavior enables automated scripts and CI/CD pipelines to detect and surface
misconfiguration failures automatically.

# Configuration File

TextLint is configured by a `.textlint.yaml` file whose key names follow cspell, so an existing cspell
configuration can be carried over key by key. The library loads the file from an explicit path that
the application supplies; it does not search for it. **The `textlint` tool does not read the file yet.**

## Example

```yaml
version: "0.2"
files:
  - "**/*.{md,cs}"
  - .github/**/*.yml
ignorePaths:
  - node_modules
  - bin/
  - obj/
enableGlobDot: false
```

## Supported Keys

| Key             | Type             | Default | Meaning                                                           |
| :-------------- | :--------------- | :------ | :---------------------------------------------------------------- |
| `version`       | text             | none    | Schema version; carried but not validated.                        |
| `files`         | list of patterns | empty   | Glob patterns that select the files to check. Empty selects none. |
| `ignorePaths`   | list of patterns | empty   | Glob patterns for files and directories to exclude.               |
| `enableGlobDot` | `true`/`false`   | `false` | Let wildcards match hidden (dot-prefixed) files and directories.  |

An empty file, or one holding only comments, is valid and gives the defaults. A key with no value is
treated as absent.

Any other key, such as `words` or `language`, is an **error**: loading fails, and the message names the
file, the line and column, and the key, for example
`.textlint.yaml(3,1): error: 'words' is not a supported setting.` Keys are added only as the features that
use them are implemented. Key names are case-sensitive, and a file must contain a single YAML document.
Every problem found is reported, not just the first, as `file(line,column): error: message`.

## Glob Patterns

- Use `/` as the path separator on every platform; a backslash is rejected.
- `*` matches within a path segment and `**` matches any number of directory levels. The `?`
  wildcard is not supported and is rejected.
- Brace alternatives are expanded: `**/*.{md,cs}` matches both extensions.
- A pattern without a `/` names a file or directory at any depth, so `node_modules` excludes every
  directory of that name. A leading `/` or `./` anchors a pattern at the root, and a trailing `/`
  limits it to directories.
- Matching is case-sensitive on every operating system.
- Negation (`!`), absolute and drive-rooted paths, `..` segments, and unbalanced braces are rejected,
  and an invalid pattern in the file is reported at its line and column.
- Wildcards do not match hidden names (those starting with a dot) unless `enableGlobDot` is `true`.
  A hidden name written literally in a pattern does match, so `.github/**/*.yml` selects workflow files
  without enabling hidden matching everywhere.
- Directories excluded by `ignorePaths`, and hidden directories no pattern names, are not scanned.

# Library Usage

The `DemaConsulting.TextLint` library loads settings from a file or from code, and selects files from
them. The sample below loads `.textlint.yaml`, reports any errors, and lists the files selected.

```csharp
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;

// Load the settings file; problems are returned, not thrown
var result = await SettingsLoader.LoadAsync(".textlint.yaml");
if (!result.Succeeded)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        // Prints, for example: .textlint.yaml(3,1): error: 'words' is not a supported setting.
        Console.Error.WriteLine(diagnostic);
    }

    return;
}

// Select the files beneath a root directory, passing the settings' values to the selector
var settings = result.Settings!;
foreach (var file in FileSelector.Select(".", settings.Files, settings.IgnorePaths, settings.EnableGlobDot))
{
    // RelativePath always uses forward slashes; FullPath is the operating-system path
    Console.WriteLine(file.RelativePath);
}
```

Settings can also be built in code, without any file, and copied with modifications. For example, to
replace the include patterns with patterns given elsewhere:

```csharp
var settings = new TextLintSettings
{
    Files = ["**/*.md"],
    IgnorePaths = ["node_modules"],
};

var withOtherFiles = settings with { Files = ["docs/**/*.md"] };
var files = FileSelector.Select(
    "/path/to/repo",
    withOtherFiles.Files,
    withOtherFiles.IgnorePaths,
    withOtherFiles.EnableGlobDot);
```

`FileSelector` does not depend on the settings type: it takes the root directory, the include patterns, the
ignore patterns, and an optional hidden-file switch, so the lists can come from a settings file, from code, or
from a command line. `TextLintSettings` is immutable: lists are copied when assigned (later changes to the
list you passed do not affect the settings), and `with` produces a modified copy. `SettingsLoadResult` is also
immutable: its list of errors cannot be changed after it is returned.
`FileSelector.Select` throws `InvalidGlobPatternException`, naming the pattern, if a pattern is unsupported,
and `DirectoryNotFoundException` if the root does not exist. Selected files
are ordered by ordinal relative path, so the order is the same on every platform.

# Command-Line Options

The following command-line options are supported:

| Option                                | Description                                                  |
| ------------------------------------- | ------------------------------------------------------------ |
| `-v`, `--version`                     | Display version information                                  |
| `-?`, `-h`, `--help`                  | Display help message                                         |
| `--silent`                            | Suppress console output                                      |
| `--validate`                          | Run self-validation                                          |
| `--results <file>`, `--result <file>` | Write validation results to file (TRX or JUnit format)       |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)           |
| `--log <file>`                        | Write output to log file                                     |

# Examples

## Example 1: Basic Usage

```bash
textlint
```

## Example 2: Self-Validation with Results

```bash
textlint --validate --results validation-results.trx
```

## Example 3: Silent Mode with Logging

```bash
textlint --silent --log tool-output.log
```

## References

N/A
