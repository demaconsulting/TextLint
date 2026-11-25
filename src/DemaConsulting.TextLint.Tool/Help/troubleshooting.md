---
title: Errors and exit codes
summary: What the exit codes mean, and how to fix the common error messages.
aliases:
  - errors
  - exit-codes
  - problems
tags:
  - troubleshooting
order: 7
see-also:
  - command-line
  - configuration
examples:
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument. Exits 1.
    content: '{{tool}} --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: silent-error
    kind: command
    language: shell
    description: An argument error is printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: no-match
    kind: command
    language: shell
    description: A glob that matches no files. Exits 1.
    content: '{{tool}} "**/*.rst"'
    exit: 1
    expect: 'No files matched the file selection'
  - name: missing-config
    kind: command
    language: shell
    description: A settings file that does not exist. Exits 1.
    content: '{{tool}} --config missing.yaml guide.md'
    exit: 1
    expect: 'Cannot read settings file'
  - name: missing-root
    kind: command
    language: shell
    description: A root folder that does not exist. Exits 1.
    content: '{{tool}} --root missing guide.md'
    exit: 1
    expect: 'does not exist'
  - name: unknown-topic
    kind: command
    language: shell
    description: A help topic that does not exist. Exits 2.
    content: '{{tool}} help no-such-topic'
    exit: 2
    expect: "Unknown help card 'no-such-topic'"
---
**Exit codes.**

| Code | Meaning |
| --- | --- |
| 0 | Every selected file was checked and nothing was found. |
| 1 | Findings, a file that could not be checked, no file selected, an error, or a failed self-test. |
| 2 | A wrong `help` argument or an unknown help topic. |

Skipped binary files and files that the settings disable do not fail a
run. Ctrl+C stops the check with exit code 1. A line
`Unexpected error: ...` means a defect in the tool; please report it.

Errors are printed to standard error and set the exit code. Errors found
during a run, such as a bad settings file, are hidden by `--silent` and
kept in the `--log` file. Errors in the arguments, and a log file that
cannot be opened, are found before the run starts: they are always
printed and never reach the log file, as [[#silent-error]] shows.

**Common messages.**

- `No files to check`: give globs, or a `files` setting.
- `No files matched the file selection`: check the glob and its quotes,
  or add `--allow-empty` ([[#no-match]]).
- `info: No settings file found`: run the tool in the project folder, or
  use `--root` or `--config`.
- `Cannot read settings file`: the file does not exist or cannot be read
  ([[#missing-config]]).
- `is not a supported setting`: an unknown or misspelled key; see
  [[configuration]].
- `Invalid glob pattern`: the message gives the reason, such as a `?` or
  a leading `!`.
- `The root directory ... does not exist`: check `--root`
  ([[#missing-root]]).
- `Unsupported argument`: check the spelling against [[command-line]].
  `help` must come first ([[#unknown-option]]).
- `requires ...` or `was specified more than once`: give each option
  that takes a value once, with its value.
- `Unknown word` or `Flagged word`: fix the text, or see [[dictionaries]].
- `The check was canceled before it finished.`: the run was stopped with
  Ctrl+C.
- `Unknown help card`: the topic does not exist. The message lists the
  topics ([[#unknown-topic]]).
