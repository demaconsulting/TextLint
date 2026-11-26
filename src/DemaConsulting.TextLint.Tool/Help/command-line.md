---
title: Command-line options
summary: Every option of the tool, and the order in which a run handles them.
aliases:
  - options
  - flags
  - usage
  - arguments
tags:
  - reference
order: 2
see-also:
  - configuration
  - self-validation
  - troubleshooting
examples:
  - name: usage
    kind: command
    language: shell
    description: Print the short usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: globs
    kind: command
    language: shell
    description: Check the files that a quoted glob selects.
    content: '{{tool}} "**/*.md"'
    expect: 'Checked 1 file(s)'
  - name: language
    kind: command
    language: shell
    description: Check with the British dictionary. Exits 1.
    content: '{{tool}} --language en-GB guide.md'
    exit: 1
    expect: "Unknown word 'favorite'"
  - name: allow-empty
    kind: command
    language: shell
    description: A glob that matches no files still passes.
    content: '{{tool}} --allow-empty "**/*.rst"'
    expect: 'Checked 0 file(s)'
  - name: parallel
    kind: command
    language: shell
    description: Check one file at a time.
    content: '{{tool}} --max-parallelism 1 "**/*.md"'
    expect: 'Checked 1 file(s)'
  - name: silent-log
    kind: command
    language: shell
    description: Hide the console output and keep it in a log file.
    content: '{{tool}} --silent --log output.log guide.md'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument is an error, printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
---
Usage: `{{tool}} [options] [globs...]`. Options and globs may be given in
any order.

| Option | Meaning |
| --- | --- |
| `[globs...]` | Files or quoted glob patterns to check; they replace `files`. |
| `--config <file>` | Settings file; default `.textlint.yaml` in the root folder. |
| `--root <dir>` | Folder that globs and the default settings file are relative to; default the current one. |
| `--language <list>` | `en-US`, `en-GB`, or both; wins over the settings. |
| `--allow-empty` | Globs that match no files do not fail the run. |
| `--max-parallelism <n>` | Files checked at the same time; default the processor count. |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the short usage text. |
| `--silent` | Hide console output. The exit code still reports the result. |
| `--log <file>` | Also write the output to a file. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Write the self-test results as `.trx` or `.xml`. `--result` also works. |
| `--depth <n>` | First heading level of the self-test report, 1 to 6; default 1. |

**Globs.** Globs on the command line replace the `files` setting, while
`ignorePaths`, `enableGlobDot`, and the overrides still apply. Quote a
glob so that the shell does not expand it, as [[#globs]] shows.
A glob that matches no files fails the run unless `--allow-empty` is
given ([[#allow-empty]]).
See [[configuration]] for the glob rules.

**Order.** `--version`, when all arguments are valid, prints only the
version and skips everything else. Without it, the tool prints a banner,
then handles the first of `--help`, `--validate`, or the check.

**Errors.** An unknown option, a missing or repeated value, or a number
out of range prints `Error: ...` and exits 1 ([[#unknown-option]]).
These errors are found before `--silent` and `--log` take effect, so they
are always printed and never logged. Each option that takes a value may
be given once. Write a file name that starts with `-` as `./-name`.

**Output.** Findings, notes, and the summary go to standard output, and
errors of the tool go to standard error. The command that
[[#silent-log]] prints keeps the output in the log file only.

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it, and an unknown topic
exits 2. To check a file named `help`, write `./help`, and for a folder,
write `./help/`.
