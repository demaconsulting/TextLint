# TextLint

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

TextLint is a fast, cspell-compatible text linter for source code and documentation. It is delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET tool (command `textlint`), which is
a thin command-line front-end over the library.

**Status**: under active development. Both the library and the tool can already select files from a `.textlint.yaml`
settings file, check their spelling in parallel with cspell's rules, and look words up in embedded dictionaries.

The workflow below shows how to use the tool: configure the files to check, the project words, and the language
in `.textlint.yaml`, run `textlint` locally or in a CI pipeline, and use the compiler-style findings, summary, and
exit code as a CI quality gate, in lint scripts, and in editors to fix the source.

![TextLint workflow: configure .textlint.yaml with files, ignorePaths, words, flagWords, and language, run textlint
with paths or globs and options such as --config, --root, and --language, get file(line,col) findings, skipped
files, a summary, and an exit code, then use them as a CI quality gate, in lint scripts, and in editors to fix the
source][workflow]

## Features

TextLint provides:

- 🧾 **cspell-Compatible Settings**: Configures checks with a familiar `.textlint.yaml` file
- 🗂️ **Glob File Selection**: Selects files with globs and ordered `!` exclusions
- 🎛️ **Per-File Overrides**: Gives some files their own settings, or switches them off with `enabled`
- ⚡ **Parallel Spell Checking**: Checks files on all processor cores with deterministic results
- 🐪 **Identifier Splitting**: Splits camelCase and snake_case words before checking them
- 🙈 **Ignore Patterns**: Skips URLs, hashes, random strings, and text matching your own patterns
- 💬 **Inline Directives**: Adjusts or disables checks from comments in the checked files
- 📖 **Embedded Dictionaries**: Ships US and UK English plus technical terms; no data files needed
- 🚦 **Compiler-Style Output**: Reports `file(line,col): error:` lines, a summary, and exit codes
- 📦 **Reusable Library**: Embeds the same checker in your own .NET applications
- ✅ **Self-Validation**: Built-in tests that spell-check sample files end to end and assert the exact findings,
  with TRX and JUnit output
- 🖥️ **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- ⚙️ **Multi-Runtime Support**: The tool needs .NET 10; the library supports .NET 10 and 11
- 🔁 **Comprehensive CI/CD**: GitHub Actions workflows for quality checks, builds, and tests
- 🛡️ **Continuous Compliance**: CI generates evidence using [Continuous Compliance][link-continuous-compliance]
- 📚 **Documentation Tooling**: Automates build notes, user guide, and compliance reports

## Installation

Install the tool globally using the .NET CLI (installing requires the [.NET SDK][dotnet-download]; running the
tool requires the .NET 10 runtime):

```bash
dotnet tool install -g DemaConsulting.TextLint.Tool
```

Add the reusable library to a project with:

```bash
dotnet add package DemaConsulting.TextLint
```

## Tool Quick Start

```bash
# Check the files that match the globs (quote them so that your shell does not expand them)
textlint "**/*.md"

# Check the files selected by the 'files' setting of ./.textlint.yaml
textlint

# Use another settings file, another project folder, or another language
textlint --config ci/textlint.yaml "**/*.md"
textlint --root ../other-project "docs/**/*.md"
textlint --language en-GB "**/*.md"

# Run self-validation and save the results
textlint --validate --results results.trx
```

A minimal `.textlint.yaml`:

```yaml
files:
  - "**/*.md"
  - "!CHANGELOG.md"
ignorePaths:
  - node_modules
words:
  - TextLint
overrides:
  - filename: docs/uk/**
    language: en-GB
```

## Library Quick Start

Load a `.textlint.yaml` settings file, check the files it selects, and print every finding:

<!-- cspell:ignore wrold -->

```csharp
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;

// Problems in the settings file are returned, not thrown
var result = await SettingsLoader.LoadAsync(".textlint.yaml");
if (!result.Succeeded)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    return 1;
}

// Check every selected file in parallel; findings are ordered by file, line, and column
var check = await SpellChecker.CheckAsync(result.Settings!, Directory.GetCurrentDirectory());
foreach (var issue in check.Issues)
{
    Console.WriteLine(issue); // docs/guide.md(12,5): error: Unknown word 'wrold'
}

// Files that were skipped or could not be read are reported separately
foreach (var problem in check.FileDiagnostics)
{
    Console.WriteLine(problem);
}

Console.WriteLine($"{check.FilesChecked} checked, {check.FilesSkipped} skipped, problems: {check.HasProblems}");
return check.HasProblems ? 1 : 0;
```

Skipped or unreadable files are reported in `FileDiagnostics` and never stop the run, and `HasProblems` is the
value to base an exit code on: it is true for any finding or any file that could not be checked, but not for
skipped binary or oversized files alone.

## More Information

The [User Guide][link-guide] covers everything else:

- [Command-Line Options][link-guide-options], [Output][link-guide-output], and [Exit Codes][link-guide-exit-codes]
- [Settings File][link-guide-config], [Glob Patterns][link-guide-globs], and [Overrides][link-guide-overrides]
- [Comment Directives][link-guide-directives] and [Differences from cspell][link-guide-differences]
- [Library Usage][link-guide-library] and [Self-Validation][link-guide-validation]

See also [CONTRIBUTING.md][link-contributing] for how to report bugs, suggest features, and submit pull requests, and
the [releases][link-releases] and the [NuGet package][link-nuget] for downloads.

## Third-Party Data

The library embeds English and technical-term dictionaries built from third-party data (SCOWL, and the MIT-licensed
cspell-dicts packages), and ports default ignore patterns from cspell-lib (MIT license). The required license notices
are in [THIRD-PARTY-NOTICES.txt][link-third-party-notices], which is also included at the root of both NuGet
packages, and the provenance of every list is described in the [dictionaries README][link-dictionaries].

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/TextLint?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/TextLint?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/TextLint?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/TextLint?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/TextLint/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_TextLint&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_TextLint&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.TextLint.Tool?style=plastic

<!-- Link References -->
[workflow]: https://raw.githubusercontent.com/demaconsulting/TextLint/main/docs/images/workflow.png
[link-forks]: https://github.com/demaconsulting/TextLint/network/members
[link-stars]: https://github.com/demaconsulting/TextLint/stargazers
[link-contributors]: https://github.com/demaconsulting/TextLint/graphs/contributors
[link-license]: https://github.com/demaconsulting/TextLint/blob/main/LICENSE
[link-contributing]: https://github.com/demaconsulting/TextLint/blob/main/CONTRIBUTING.md
[link-third-party-notices]: https://github.com/demaconsulting/TextLint/blob/main/third-party/THIRD-PARTY-NOTICES.txt
[link-dictionaries]: https://github.com/demaconsulting/TextLint/blob/main/dictionaries/README.md
[link-build]: https://github.com/demaconsulting/TextLint/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_TextLint
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_TextLint
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.TextLint.Tool
[link-releases]: https://github.com/demaconsulting/TextLint/releases
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[dotnet-download]: https://dotnet.microsoft.com/download
[link-guide]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md
[link-guide-options]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#command-line-options
[link-guide-output]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#output
[link-guide-exit-codes]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#exit-codes
[link-guide-config]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#configuration-file
[link-guide-globs]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#glob-patterns
[link-guide-overrides]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#overrides
[link-guide-directives]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#directives-in-comments
[link-guide-differences]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#differences-from-cspell
[link-guide-library]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#library-usage
[link-guide-validation]: https://github.com/demaconsulting/TextLint/blob/main/docs/user_guide/introduction.md#self-validation
