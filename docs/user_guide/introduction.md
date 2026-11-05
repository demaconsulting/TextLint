# Introduction

## Purpose

TextLint is a fast text linter for source code and documentation, delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET tool
(command `textlint`), which is a thin command-line front-end over the library.

**Status**: under active development. TextLint is being developed to provide cspell-compatible
spell-checking, configured by a `.textlint.yaml` file, using embedded English and technical-term
dictionaries, and to check files in parallel across all processor cores. The library can already do
this (see _Configuration File_, _Spell Checking_, and _Library Usage_ below): it loads a
`.textlint.yaml` settings file, selects the files to check, checks their spelling in parallel, and looks
words up in its embedded dictionaries. The tool does not read the settings file or call the spell checker
yet and provides only the command-line scaffolding (version, help, and self-validation).

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
  comma-separated list such as `en-US, en-GB` accepts both. Names are not case-sensitive. Technical terms are known
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
- `disabled`, `disable-lines`, and a prefix inside a longer word (`xcspell:disable`) are not directives.
- A directive that cspell has and TextLint does not have is **reported**, at the position of its name, as an
  unsupported directive, so you do not believe it is in force: `flag` and `forbid`, `locale` and `language`,
  `dictionary` and `dictionaries`, `ignoreRegExp`, `includeRegExp`, and the switches for compound words and case
  sensitivity. A directive inside a disabled region is not reported, and unknown directive names are ignored.

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

`SpellChecker.CheckAsync` throws `DirectoryNotFoundException` when the root does not exist,
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
and `DirectoryNotFoundException` if the root does not exist. Selected files
are ordered by ordinal relative path, so the order is the same on every platform.

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

The word lists the dictionaries are built from are third-party data: SCOWL for English and the cspell dictionaries
for technical terms, and the default ignore patterns of the spell checker are ported from cspell-lib. Their license
notices are in the `THIRD-PARTY-NOTICES.txt` file at the root of both NuGet packages.

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
