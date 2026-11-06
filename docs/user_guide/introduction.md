# Introduction

## Purpose

TextLint is a fast text linter for source code and documentation, delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET tool
(command `textlint`), which is a thin command-line front-end over the library.

**Status**: under active development. TextLint is being developed to provide cspell-compatible
spell-checking, configured by a `.textlint.yaml` file, using embedded English and technical-term
dictionaries, and to check files in parallel across all processor cores. Both the `textlint` tool
(see _Checking Files_ below) and the library (see _Configuration File_, _Spell Checking_, and _Library
Usage_ below) can already do this: they load a `.textlint.yaml` settings file, select the files to check,
check their spelling in parallel, and look words up in the embedded dictionaries.

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
applications that want to embed TextLint rather than run the command-line tool. It loads settings, selects
files, checks spelling, and looks words up; its API will grow as further capabilities are added.

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

When invoked with no arguments, the tool displays the banner and copyright notice and then checks the files that
the `files` setting of `.textlint.yaml` in the current directory selects. If there is no such setting, nothing
would be checked, and the tool says so and exits with a failure code instead of succeeding silently:

```bash
textlint
```

```text
No files to check: give file patterns on the command line, or a 'files' setting in a settings file. Example: textlint "**/*.md"
```

## Checking Files

Name the files to check as arguments. A name can be a file, a glob pattern, or a directory pattern such as
`docs/**`. Quote glob patterns so that your shell does not expand them before the tool sees them:

```bash
# Every markdown file below the current directory
textlint "**/*.md"

# Specific files
textlint README.md docs/guide.md

# The files selected by the 'files' setting of ./.textlint.yaml
textlint
```

The files named on the command line replace the `files` setting; the `ignorePaths` and `enableGlobDot` settings
still apply to them. The pattern rules are those of _Glob Patterns_ below, with the additions in _Paths on the
Command Line_.

### Paths on the Command Line

A person typing a command works differently from a settings file, so the command line is more forgiving than
`.textlint.yaml`:

- **Backslashes on Windows.** `.\docs\guide.md` and `docs\*.md` work as PowerShell completes them. On other
  operating systems a backslash is an ordinary file-name character, and a pattern that contains one is rejected
  with a message that says to use `/`.
- **Absolute paths.** `C:\work\project\README.md`, `C:/work/project/**/*.md`, `\\server\share\docs\*.md`, and
  `/home/me/project/README.md` are accepted. The part of the path before the first wildcard is the directory to
  search, and the rest is the pattern within it. A path without a wildcard names one file, unless it names an
  existing directory, which selects everything below it (just as a relative `docs` does); a trailing separator
  also selects everything below a directory. A path that does not exist is treated as a file name and matches
  nothing.
- **Absolute paths inside the root directory** mean exactly what the equivalent relative path means, so the
  `ignorePaths` of your settings still apply to them. An absolute path outside the root directory searches below
  its own directory, and relative paths cannot be combined with it in one run. Paths on different drives cannot be
  combined either.
- **Reported paths.** When any path you typed was absolute, files are reported by their absolute path; otherwise by
  their path relative to the root directory, always with `/` separators.
- **No `~` expansion.** The shell expands `~` before the tool sees it; the tool never does.
- A path that starts with `\` or `/` but names no drive is ambiguous on Windows and is rejected, as is a
  drive-relative path such as `C:docs`.

Patterns in the settings file stay strict: they use `/` and are never absolute.

### Settings File

The tool reads settings from the file named by `--config`, otherwise from `.textlint.yaml` in the root directory,
which is the current directory unless `--root` names another. **No parent directory is searched**, so a settings file
from an unrelated folder can never be applied by accident; run the tool in the project folder, or name the file or
folder. When no settings file is found, the default settings apply and the tool prints one line to say so:

```text
info: No settings file found in '/work/project'; using the default settings.
```

The root directory is checked before anything else, so a root that does not exist, is not a directory, or cannot be
read is reported as such and stops the run, whatever paths were typed (an absolute path cannot bypass it).

An explicit `--config` file that does not exist, a settings file that cannot be read, and a settings file with
problems are errors: each problem is reported as `file(line,column): error: message` and nothing is checked.
`--language` overrides the `language` setting for one run, for example `textlint --language en-GB "**/*.md"`.

### Output

Results go to standard output, and errors of the tool itself (a bad argument or settings file, a missing
directory) go to standard error, so `textlint "**/*.md" > report.txt` captures the report. Each issue is one line
in the compiler style that editors and CI systems link to, in order of file path, line, and column, followed by one
line for each file that was skipped or could not be checked, and a summary:

<!-- cspell:ignore wrold -->

```text
info: No settings file found in '/work/project'; using the default settings.
docs/guide.md(12,5): error: Unknown word 'wrold'
src/Program.cs(40,9): error: Flagged word 'utilize'; suggestions: use
assets/logo.dat: info: The file looks like binary data: it contains a NUL character in its first 1,024 characters.
locked.md: error: The file cannot be read: The process cannot access the file because it is being used by another process.
Checked 120 file(s): 2 issue(s) in 2 file(s), 1 skipped, 1 could not be checked.
```

A binary file or a file over the `maxFileSize` setting is skipped on purpose and reported as `info`; a file that
could not be read or checked is reported as `error`. The banner and the information line are printed on every run
and are removed by `--silent`.

### Exit Codes

| Code | Meaning                                                                                       |
| :--- | :-------------------------------------------------------------------------------------------- |
| `0`  | Every selected file was checked and no issue was found; skipped binary files do not count.    |
| `1`  | Anything else: issues found, a file that could not be checked, no file selected, or an error. |

As in cspell, there is a single failure code. A selection that matches no files fails (a mistyped pattern is
almost always the reason) unless `--allow-empty` is given. `--allow-empty` applies only to patterns that were given
and matched nothing: a run that was given no patterns at all, on the command line or in the settings, has nothing
to check and always fails with a usage error. A script that checks a list of changed files should therefore call the
tool only when the list is not empty:

```bash
changed=$(git diff --name-only -- '*.md'); [ -z "$changed" ] || textlint --allow-empty $changed
```

Interrupting the tool with Ctrl+C stops the check, prints a message, and exits with code 1 without a partial result.

### Limiting Parallelism

Files are checked on all processor cores. Use `--max-parallelism <#>` to limit the number of files checked at
the same time, for example on a shared build machine; the result is the same whatever the limit.

### Differences from the cspell Command Line

- The output format is `file(line,column): error: message` and not cspell's `file:line:column - message`.
- The settings file is not searched for in parent directories, and its patterns are relative to the root
  directory and not to the directory of the settings file.
- A run that names no files fails instead of checking everything.
- Options such as `--no-progress`, `--no-summary`, `--show-suggestions`, and `--locale` are not available;
  `--language` selects the dictionaries.

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
✓ TextLintTool_CheckCleanFile - Passed
✓ TextLintTool_CheckMisspelling - Passed
✓ TextLintTool_CheckSettingsFile - Passed
✓ TextLintTool_CheckSettingsError - Passed
✓ TextLintTool_CheckGlobsReplaceFiles - Passed
✓ TextLintTool_CheckInlineDirective - Passed
✓ TextLintTool_CheckLanguageOption - Passed
✓ TextLintTool_CheckBinaryFileSkipped - Passed
✓ TextLintTool_CheckNoFilesMatched - Passed
✓ TextLintTool_CheckAbsolutePath - Passed

Total Tests: 12
Passed: 12
Failed: 0
```

### Validation Tests

Each test proves specific functionality works correctly. The spell-checking tests run the real tool against
sample files in a private temporary directory, so passing proves that the tool checks spelling correctly on the
operating system and .NET runtime that ran them:

- **`TextLintTool_VersionDisplay`** - `--version` outputs a valid version string.
- **`TextLintTool_HelpDisplay`** - `--help` outputs usage and options information.
- **`TextLintTool_CheckCleanFile`** - A correctly spelled file is accepted with exit code 0.
- **`TextLintTool_CheckMisspelling`** - A misspelling is reported at its line and column with exit code 1.
- **`TextLintTool_CheckSettingsFile`** - The settings file in the root directory is found, and its file list,
  ignore list, accepted words, and flagged words are applied.
- **`TextLintTool_CheckSettingsError`** - An invalid settings file is reported with its position, and nothing is
  checked.
- **`TextLintTool_CheckGlobsReplaceFiles`** - A pattern on the command line replaces the `files` setting.
- **`TextLintTool_CheckInlineDirective`** - In-document directives with the `cspell:` and `textlint:` prefixes
  suppress findings.
- **`TextLintTool_CheckLanguageOption`** - `--language` selects the American or the British dictionary.
- **`TextLintTool_CheckBinaryFileSkipped`** - A binary file is reported as information and does not fail the run.
- **`TextLintTool_CheckNoFilesMatched`** - A selection that matches no files fails unless `--allow-empty` is given.
- **`TextLintTool_CheckAbsolutePath`** - A file named by an absolute path is reported by that path.

## Silent Mode

Suppress console output; the exit code still reports the result. An argument error (for example a misspelled
option) is still written to standard error, because it is found before the output settings are known:

```bash
textlint --silent "**/*.md"
```

## Logging

Write output to a log file; the log receives everything, including what `--silent` hides from the console:

```bash
textlint --log output.log "**/*.md"
```

## Error Handling

Unrecognized, missing, empty, or repeated arguments cause the tool to print an error message to standard error and
exit with a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
Error: --config was specified more than once; give --config a single time
Error: --max-parallelism requires an integer between 1 and 2147483647 for a maximum number of files
```

A value that is missing, empty, or looks like the next option is reported as missing, so a forgotten value does
not swallow the option after it. A file pattern that starts with a dash can be written with a leading `./`.

This behavior enables automated scripts and CI/CD pipelines to detect and surface
misconfiguration failures automatically.

# Configuration File

TextLint is configured by a `.textlint.yaml` file whose key names follow cspell, so an existing cspell
configuration can be carried over key by key. The `textlint` tool reads `.textlint.yaml` from the root directory
(see _Settings File_ above), or the file named by `--config`. The library loads the file from an explicit path that
the application supplies; it does not search for it.

## Example

<!-- cspell:ignore zorbs -->

```yaml
version: "0.2"
language: en-GB
files:
  - "**/*.{md,cs}"
  - .github/**/*.yml
ignorePaths:
  - node_modules
  - bin/
  - obj/
enableGlobDot: false
words:
  - TextLint
  - k8s
ignoreWords:
  - zorbs
flagWords:
  - "utilize->use"
minWordLength: 4
maxFileSize: 1MB
```

## Supported Keys

| Key             | Type             | Default  | Meaning                                                           |
| :-------------- | :--------------- | :------- | :---------------------------------------------------------------- |
| `version`       | text             | none     | Schema version; carried but not validated.                        |
| `files`         | list of patterns | empty    | Glob patterns that select the files to check. Empty selects none. |
| `ignorePaths`   | list of patterns | empty    | Glob patterns for files and directories to exclude.               |
| `enableGlobDot` | `true`/`false`   | `false`  | Let wildcards match hidden (dot-prefixed) files and directories.  |
| `language`      | text             | `en`     | The English dictionaries: `en`, `en-US`, `en-GB`, or a list.      |
| `words`         | list of words    | empty    | Words that are accepted as correct.                               |
| `ignoreWords`   | list of words    | empty    | Words that are never reported, even if flagged.                   |
| `flagWords`     | list of words    | empty    | Words that are always reported, even if they are known.           |
| `minWordLength` | whole number     | `4`      | Words shorter than this are not checked (at least 1).             |
| `maxFileSize`   | size             | no limit | Files larger than this are skipped and reported.                  |

An empty file, or one holding only comments, is valid and gives the defaults. A key with no value is
treated as absent. The spelling keys are described under _Spell Checking_ below.

Any other key, such as `caseSensitive` or `overrides`, is an **error**: loading fails, and the message names the
file, the line and column, and the key, for example
`.textlint.yaml(3,1): error: 'caseSensitive' is not a supported setting.` Keys are added only as the features that
use them are implemented. Key names are case-sensitive, and a file must contain a single YAML document.
Every problem found is reported, not just the first, as `file(line,column): error: message`. A value that is
not valid is reported at the value: an unsupported language, a word-list entry that could never match, a
`minWordLength` that is not a whole number of at least 1, or a `maxFileSize` that is not a size.

## Glob Patterns

- Use `/` as the path separator on every platform; a backslash is rejected.
- `*` matches within a path segment and `**` matches any number of directory levels. The `?`
  wildcard is not supported: a pattern containing `?` is rejected with the message
  `the '?' wildcard is not supported; use '*' to match any run of characters`. Use `*` instead; for
  example, write `file*.md` rather than `file?.md`.
- Brace alternatives are expanded: `**/*.{md,cs}` matches both extensions.
- A pattern without a `/` names a file or directory at any depth, so `node_modules` excludes every
  directory of that name. A trailing `/` limits a pattern to directories.
- A pattern that begins with a single `/` (or `./`) is anchored to the root directory being searched, as in
  `.gitignore` and cspell: `/build/` and `/README.md` mean the entries directly under the root, not a path on
  the file system.
- Absolute paths are rejected: a drive letter (`C:/docs/**`, `c:docs/**`, or `C:\docs\**`) and a
  double-slash network path (`//server/share/**`). Write the path relative to the root directory instead.
  (Patterns typed on the command line are more forgiving; see _Paths on the Command Line_.)
- Matching is case-sensitive on every operating system.
- Negation (`!`), `..` segments, and unbalanced braces are rejected, and an invalid pattern in the file is
  reported at its line and column.
- Wildcards do not match hidden names (those starting with a dot) unless `enableGlobDot` is `true`.
  A hidden name written literally in a pattern does match, so `.github/**/*.yml` selects workflow files
  without enabling hidden matching everywhere.
- Directories excluded by `ignorePaths`, and hidden directories no pattern names, are not scanned.

# Spell Checking

The library checks the spelling of text and of files with the rules of cspell, so a project that uses cspell can
move to TextLint with its configuration and its comments. This section describes what is checked, the settings,
and the comment directives; _Differences from cspell_ lists where the two tools deliberately differ.

## What Is Checked

Text is split into words, and each word is checked against the dictionary of the chosen language, the technical
terms, and the word lists of the settings:

- A word is a run of letters; an apostrophe (straight or typographic) is allowed between two letters, so
  `don't` is one word. Digits, underscores, hyphens, dots, and other punctuation end a word, so `snake_case` is
  two words and `version2` is one.
- A word that is not known is split at case changes and each piece is checked: `fooBar` is `foo` and `Bar`,
  `XMLParser` is `XML` and `Parser`. A capital that begins an English ending stays with its acronym, so `APIs` and
  `URLs` are not split. A word that is known as a whole is never split.
- Words and pieces shorter than `minWordLength` (4 by default, counted in UTF-16 code units) are not checked, so
  abbreviations and short variable names are quiet. A flagged word is reported whatever its length.
- The possessive of a known word is accepted (`parser's`), and so is a known word after a backslash escape
  (`\nfoo` in a string literal is checked as `foo`). A run of capitals with an ending (`URLs`, `ZIPped`) is
  accepted when the capitals are known or shorter than the minimum length, and a piece that is one repeated letter
  (`xxxx`) is never reported.
- Han, Hiragana, Katakana, and Hangul characters are never checked. Other scripts are checked, but the
  dictionaries contain Latin-script words only, so every word in Cyrillic or Greek is unknown.
- These are not checked at all: URLs, email addresses, C and CSS hexadecimal values, commit hashes that contain a
  digit, UUIDs, Unicode references such as `U+1F600`, SHA digests and hash strings, Base64 data, SSH keys, and PEM
  certificate and key blocks.

Each finding has a kind: an **unknown word**, a **flagged word**, or an **unsupported directive**. Positions are
1-based; the line is counted by line feed characters only, and the column counts UTF-16 code units from the start
of the line, as cspell does. Files are read as UTF-8 with or without a byte order mark, or as UTF-16 with a byte
order mark; a file that contains a NUL character in its first 1,024 characters is binary and is skipped.

## Spelling Settings

- **`language`**: `en` and `en-US` choose American English (the default), `en-GB` chooses British English, and a
  comma-separated list such as `en-US, en-GB` accepts both. Names are not case-sensitive. British English is
  -ise only (`organise`, not `organize`). Technical terms are known
  in every language. Anything else, such as `fr` or `en-AU`, is an error that names the language.
- **`words`**: words that are accepted as correct. Entries match in any letter case: `TextLint` accepts `textlint`
  and `TEXTLINT`. An entry that contains digits or punctuation, such as `k8s` or `foo-bar`, matches the whole
  token it appears in (a token is a run of letters, digits, and `_ ' ’` . + -`), ignoring punctuation at its
  edges; if the token does not match, its words are checked one by one.
- **`ignoreWords`**: words that are never reported, even when they are also in `flagWords`. Entries are matched like
  `words`.
- **`flagWords`**: words that are always reported, even when they are known words, with the kind _flagged_. An entry
  written entirely in lower case flags the word in any letter case; an entry with a capital letter flags only that
  exact spelling. The forms `word->suggestion, other` and `word: suggestion` attach suggestions to the finding. The
  order of precedence is: ignored, then flagged, then accepted, then the dictionaries.
- **`minWordLength`**: a whole number of at least 1.
- **`maxFileSize`**: a number with an optional unit `B`, `K`, `KB`, `M`, `MB`, `G`, or `GB` (binary multiples, any
  letter case), such as `500K` or `1.5MB`. A larger file is skipped and reported. `0`, or no setting, means no limit,
  except that a file larger than 1 GiB is never read because a string cannot hold it.

An entry in a word list that could never match is an error when the file is loaded, rather than an entry that
silently has no effect: a blank entry, an entry with white space, an entry with a character that cannot be part of
a word, an entry with no letter or digit, and an entry that starts with `!` (the forbidden-word marker of cspell;
use `flagWords` instead).

## Directives in Comments

A comment can switch checking off, or add words, for the text around it. A directive is a prefix, a colon (or two),
and the directive name. The prefixes are `cspell`, `spell`, `spellchecker`, `spell-checker`, and `textlint`, in any
letter case, and the prefix must not follow a letter, digit, or underscore. The examples below use `textlint`.

| Directive                                  | Effect                                                              |
| :----------------------------------------- | :------------------------------------------------------------------ |
| `textlint:disable` ... `textlint:enable`   | Hides the text between them; with no `enable`, to the end of text   |
| `textlint:disable-line`                    | Hides the whole line, including text before the directive           |
| `textlint:disable-next-line` (or `-next`)  | Hides the rest of its line and the next line                        |
| `textlint:ignore word1 word2` (or `words`) | Adds the words, separated by spaces, commas, or semicolons          |

- A block is closed only by an `enable` written with the same prefix as the `disable` that opened it (letter case
  aside), so one prefix does not close another.
- Text on the directive's line before `disable`, and after `enable`, is still checked. A line break is a line feed;
  with Windows line endings a blank line after a next-line directive is the only line hidden, while with line feeds
  the blank line and the line after it are hidden, as in cspell.
- `ignore` and `words` apply to the whole document, in any letter case, and the directive's own text is not checked.
- A prefix inside a longer word (`xcspell:disable`), a prefix with no name, and a name that does not begin like a
  directive (`mystery`) are not directives and are ignored.
- The forms `disable-next-foo`, `disable-next-line-foo`, `disable-line-foo`, and `enable-foo` (a hyphen after the
  keyword) work as the keyword, as they do in cspell.
- A name that only begins like a directive (`disabled`, `disable-lines`, `disable-foo`, `ignored`, `wordsmith`,
  `words-x`) hides nothing and adds no words, and is **reported**, at the position of its name, so a misspelled
  directive does not go unnoticed. cspell does the same for the `disable` forms, but for `ignore`, `word`, and
  `ignoreRegExp` forms it hides the rest of the line, which TextLint does not.
- Only the first prefix of a line is read for words and unsupported directives, as in cspell.
- A directive that cspell has and TextLint does not have is **reported**, at the position of its name, as an
  unsupported directive, so you do not believe it is in force: `flag` and `forbid`, `locale` (or its cspell alias
  `local`) and `language`,
  `dictionary` and `dictionaries`, `ignoreRegExp`, `includeRegExp`, and the switches for compound words and case
  sensitivity. A directive inside a disabled region is not reported, and names that do not begin like a directive
  are ignored.

## Differences from cspell

<!-- cspell:ignore quixs xcspell -->

TextLint follows cspell's documented rules, and the test suite compares it with the real cspell on a set of
corpora (see the verification of TextLint). These differences are deliberate:

- **No heuristic splitter.** After the case split, cspell re-splits an unknown word with a dictionary-driven search
  and keeps the result when it finds fewer problems. TextLint does not, so an unknown acronym with an ending such as
  `QUIXs` is reported, a word glued to a short neighbor may be reported by one tool and not the other, and
  Han characters between two Latin words give two findings instead of one.
- **Different dictionaries.** TextLint embeds its own English and technical word lists, so a real project shows
  words that cspell knows and TextLint does not, and the reverse. Add such words to `words`.
- **Unsupported settings are errors,** and unsupported directives are reported; cspell has more settings (for
  example `caseSensitive`, `allowCompoundWords`, `overrides`, `dictionaries`, and `ignoreRegExpList`), which are
  not available yet.
- **No limit on repeats.** cspell stops reporting a word after five repeats and a file after 100 problems by
  default; TextLint reports every occurrence.
- **Stricter language names.** Only `en`, `en-US`, and `en-GB` are accepted; cspell also accepts forms such as
  `en_US`.
- **Default ignore patterns are the same list, but not the per-language additions** (for example the Markdown link
  patterns), and cspell's random-string detection is not available. The patterns are .NET regular expressions with
  the ASCII meaning of a word character and a word boundary, as in JavaScript.
- **Binary files** are recognized by a NUL character in the first 1,024 characters, whatever the file extension; UTF-32
  files and UTF-16 files without a byte order mark are therefore skipped as binary.
- **Per-file problems are results.** A file that cannot be read, is binary, is too large, or takes too long to check
  is listed in the result and does not stop the run.
- **`LocalWords:`** comments are not recognized.
- **Directive names that only begin like a directive** are reported (see Directives in Comments), and an `enable`
  keyword whose prefix directly follows a letter does not close a block.

# Library Usage

The `DemaConsulting.TextLint` library loads settings from a file or from code, selects files from
them, checks their spelling, and looks words up in its dictionaries. The sample below loads `.textlint.yaml`,
reports any errors, and checks every selected file under the current directory.

<!-- cspell:ignore wrold -->

```csharp
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;

// Load the settings file; problems are returned, not thrown
var result = await SettingsLoader.LoadAsync(".textlint.yaml");
if (!result.Succeeded)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        // Prints, for example: .textlint.yaml(3,1): error: 'caseSensitive' is not a supported setting.
        Console.Error.WriteLine(diagnostic);
    }

    return;
}

// Check every selected file on all processor cores; the findings are ordered by file, line, and column
var check = await SpellChecker.CheckAsync(result.Settings!, Directory.GetCurrentDirectory());
foreach (var issue in check.Issues)
{
    // Prints, for example: docs/guide.md(12,5): error: Unknown word 'wrold'
    Console.WriteLine(issue);
}

foreach (var problem in check.FileDiagnostics)
{
    // Prints, for example: build/data.bin: info: The file looks like binary data: ...
    Console.WriteLine(problem);
}

Console.WriteLine($"{check.FilesChecked} checked, {check.FilesSkipped} skipped, problems: {check.HasProblems}");
```

`SpellChecker.CheckAsync` throws `DirectoryNotFoundException` when the root does not exist (a root the process cannot
read throws `UnauthorizedAccessException` or `IOException` instead, so a permission problem is not mistaken for a
missing directory),
`InvalidGlobPatternException` for an invalid pattern, `ArgumentException` for settings it cannot use (built in code;
a loaded file was validated already), and `OperationCanceledException` when the token is cancelled; nothing is
returned in that case. Everything about a single file is data in the result: `FileDiagnostic.Kind` is `Unreadable`,
`Binary`, `TooLarge`, or `TimedOut`, and `IsError` is true for the first and the last, because those files were meant
to be checked and were not. Base an exit code on `HasProblems`. Pass
`new SpellCheckOptions { MaxDegreeOfParallelism = 2 }` to limit the number of files checked at the same time; the
result is the same at every setting. The result is immutable, so it can be shared between threads.

To check text without files, create a `TextChecker` once and call it from any thread:

```csharp
var settings = new TextLintSettings { Language = "en-GB", Words = ["TextLint"] };
var checker = new TextChecker(settings);

foreach (var issue in checker.Check("The wrold of TextLint", "greeting.txt"))
{
    // issue.Line, issue.Column, issue.Text, issue.Kind, and issue.Suggestions describe the finding
    Console.WriteLine(issue); // greeting.txt(1,5): error: Unknown word 'wrold'
}
```

The `TextChecker` constructor throws `ArgumentException` for a language that is not supported or an unusable word
list entry, and `Check` throws `RegexMatchTimeoutException` only when hostile text defeats one of the ignore
patterns within its time limit.

## Selecting Files

File selection is available on its own. It takes the patterns directly, so the lists can come from a settings file,
from code, or from a command line:

```csharp
using DemaConsulting.TextLint.FileSelection;

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
and `DirectoryNotFoundException` if the root does not exist (a root that cannot be read throws
`UnauthorizedAccessException` or `IOException`). Selected files
are ordered by ordinal relative path, so the order is the same on every platform.

## Command-Line Paths

Paths and patterns that a person types on a command line are more forgiving than patterns in a settings file:
they may be absolute, and on Windows they may use backslashes. `CommandLinePathResolver` is the single place that
interprets them, so every program built on the library reads them the same way as the `textlint` tool does (see
_Paths on the Command Line_). It turns the arguments into a search directory and validated patterns, and says how
the files found are to be named:

```csharp
using DemaConsulting.TextLint.FileSelection;

// For example: textlint C:\work\project\docs\*.md   (or ./docs/*.md, or .\docs\*.md on Windows)
var selection = CommandLinePathResolver.Resolve(Directory.GetCurrentDirectory(), args);
var files = FileSelector.Select(selection.RootDirectory, selection.Patterns, settings.IgnorePaths);

foreach (var file in files)
{
    // The absolute path when any argument was absolute, otherwise the path relative to the root
    Console.WriteLine(selection.GetDisplayPath(file.RelativePath));
}
```

An absolute argument inside the root directory is turned into the equivalent root-relative pattern, so the ignore
patterns apply to it as usual; one outside it makes the search directory its own literal leading directory.
An absolute argument that names an existing directory selects everything beneath it; the resolver looks the path
up in the file system to decide, and a look-up that fails for any reason other than the path being missing
(for example a denied permission) throws `UnauthorizedAccessException` or `IOException`.
`Resolve` throws `InvalidGlobPatternException`, naming the argument exactly as it was typed, for anything the
pattern rules reject, and `ArgumentException` for a combination that cannot be searched from one directory.
To report findings under the same names, format them with `SpellingIssue.Format` and `FileDiagnostic.Format`,
which take the file name to show:

```csharp
foreach (var issue in check.Issues)
{
    // For example: C:\work\project\docs\a.md(1,7): error: Unknown word 'wrold'
    Console.WriteLine(issue.Format(selection.GetDisplayPath(issue.FilePath!)));
}
```

## Word Lookup

<!-- cspell:ignore favourite cafè -->

The library also answers whether a word is known, using dictionaries embedded in the assembly: the English words
that American and British English share, the American-only and the British-only spellings, and a dictionary of
technical terms (programming languages, tools, companies, file types, and licenses). A selection always includes
the common words and the technical terms, and adds the spellings of the languages you choose.

```csharp
using DemaConsulting.TextLint.Dictionaries;

// American English plus technical terms: the default
var american = SpellingDictionary.Default;

// Choose by language name: en and en-US (American), en-GB (British), or a comma-separated list
var british = SpellingDictionary.ForLanguages("en-GB");
var both = SpellingDictionary.ForLanguages("en-US, en-GB");

Console.WriteLine(american.IsKnown("favorite"));   // True
Console.WriteLine(american.IsKnown("favourite"));  // False: a British spelling
Console.WriteLine(british.IsKnown("favourite"));   // True
Console.WriteLine(both.IsKnown("favourite"));      // True
Console.WriteLine(british.IsKnown("kubernetes"));  // True: technical terms are known in every language
```

The language names are `en` (the same as `en-US`), `en-US`, and `en-GB`, in any letter case with optional spaces
around names; `SpellingDictionary.SupportedLanguages` lists them. Asking for anything else, such as `en-AU`,
throws an `ArgumentException` that names the language. Asking twice for the same selection returns the same
object.

The lookup follows these rules:

- Letter case is ignored: `Table`, `TABLE`, and `table` are the same word.
- The typographic apostrophe is the same as the straight one: `don’t` is known because `don't` is.
- Accents matter: `café` and `cafe` are both known, but `cafè` is not.
- Other punctuation is not removed, so `table.` is not known; splitting text into words is done by the spell
  checker, which looks each word up with this lookup.
- Empty text, text longer than 64 characters, and text containing characters outside Latin-1, such as other
  scripts, are not known. The lookup never throws for any text.
- A word that is a programming term and also an American or British spelling, such as `tokenizer`, is known in
  every language. Other spellings that belong to one language, such as `favorite`, follow the language you choose.

`IsKnown` takes a `ReadOnlySpan<char>`, so a word can be checked inside larger text without copying, and it does
not allocate. The dictionaries are loaded the first time a word is looked up (a few milliseconds) and are shared by
the whole process, and a selection can be used from many threads at once.

The word lists the dictionaries are built from are third-party data, and the default ignore patterns of the spell
checker are ported from cspell-lib. Their license notices are in the `THIRD-PARTY-NOTICES.txt` file at the root of
both NuGet packages.

<!-- cspell:ignore colourize glamourize colourise organise practise licence -->

### Dictionary Contents

The dictionaries are generated from these sources, with their versions and license terms listed in
`dictionaries/README.md` and `THIRD-PARTY-NOTICES.txt`:

- **English**: SCOWL (the Spell Checker Oriented Word Lists) word lists and Hunspell dictionaries, the English
  word lists and Hunspell-expanded word forms of the cspell dictionaries, and a hand-added list of developer terms.
  Each word is routed to the common, the American, or the British list by one documented rule, so a spelling that
  belongs to one dialect never lands in the common list.
- **Technical terms**: the cspell dictionaries for software terms, companies, file types, licenses, and the
  programming languages and tools in common use.
- **Exclusions**: a reviewed list of words that imported lists contain but that are almost always a mistyped
  common word. An excluded word is unknown in every language, in every letter case, whichever list it came from.
  The build fails when an exclusion is not a word of any list, so a stale entry is noticed.

Two rules of the English data are worth knowing:

- **British English is -ise only.** `organise` and `colourise` are British; `organize` is American only, and a
  hybrid such as `colourize` or `glamourize` is unknown in both. The same holds for a capitalized form: the lookup
  ignores letter case, so `Honor` is American just as `honor` is. The shared technical dictionary is the one
  exception: it keeps the American spelling of a word that is also a programming term (`color`, `center`,
  `organize`), so those spellings are accepted under `en-GB` as well.
- **The check does not know the part of speech.** British English spells the verb `license` and the noun `licence`
  differently, and the verb `practise` and the noun `practice`; one of the two forms of each is common to both
  dialects, so British English accepts both forms of such a word wherever it appears. This is inherent in a
  spelling check and is not a leak of American spellings.

# Command-Line Options

The following command-line options are supported:

| Option                                | Description                                                              |
| ------------------------------------- | ------------------------------------------------------------------------ |
| `[globs...]`                          | Files to check: paths or glob patterns, replacing the `files` setting    |
| `--config <file>`                     | Settings file (default: `.textlint.yaml` in the root directory)          |
| `--root <dir>`                        | Directory that patterns and the default settings file are relative to    |
| `--language <languages>`              | Dictionary language such as `en-US` or `en-GB`; overrides the settings   |
| `--allow-empty`                       | Accept patterns that match no files (default: exit code 1)               |
| `--max-parallelism <#>`               | Maximum number of files checked at the same time (default: processors)   |
| `-v`, `--version`                     | Display version information                                              |
| `-?`, `-h`, `--help`                  | Display help message                                                     |
| `--silent`                            | Suppress console output (usage errors still go to stderr)                |
| `--validate`                          | Run self-validation                                                      |
| `--results <file>`, `--result <file>` | Write validation results to file (TRX or JUnit format)                   |
| `--depth <#>`                         | Set heading depth for markdown output (default: 1)                       |
| `--log <file>`                        | Write output to log file                                                 |

Each option that takes a value may be given only once. `[globs...]` may be mixed with the options in any order.
See _Checking Files_ for the meaning of the patterns, the settings file, the output, and the exit codes.

# Examples

## Example 1: Basic Usage

Check every markdown file below the current directory, using `.textlint.yaml` if there is one:

```bash
textlint "**/*.md"
```

## Example 2: Check the Files a Settings File Selects, in a CI Job

With `files` and `ignorePaths` in `.textlint.yaml`, the job needs only the command, and fails on any finding:

```bash
textlint
```

## Example 3: Another Project, Another Language

```bash
textlint --root ../other-project --language en-GB "docs/**/*.md"
```

## Example 4: Self-Validation with Results

```bash
textlint --validate --results validation-results.trx
```

## Example 5: Silent Mode with Logging

```bash
textlint --silent --log tool-output.log "**/*.md"
```

## References

N/A
