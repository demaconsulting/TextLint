---
# textlint:ignore wrold
title: What is checked
summary: How text is split into words, what is skipped, and the comment directives.
aliases:
  - spelling
  - rules
  - checks
  - directives
  - ignore
tags:
  - reference
order: 5
see-also:
  - dictionaries
  - configuration
examples:
  - name: findings
    kind: command
    language: shell
    description: Check a file with a typo and a flagged word. Exits 1.
    content: '{{tool}} notes.txt'
    exit: 1
    expect: "Unknown word 'wrold'"
---
**Words.** A word is a run of letters, with an apostrophe allowed between
two letters. An unknown word is split at case changes, so `fooBar` is
checked as `foo` and `Bar`, and `snake_case` is two words. A word that is
known as a whole is never split. Words shorter than `minWordLength` (4)
are not checked, but a flagged word is reported at any length. The
possessive of a known word is accepted.

**Not checked.**

- URLs, email addresses, hex values, commit hashes, UUIDs, Base64 data,
  SSH keys, and certificates.
- Text that an `ignoreRegExpList` entry matches: `/re/flags` in the
  JavaScript style, a bare expression (read with the flags `i` and `m`),
  or a name from `patterns`.
- Tokens of at least `minRandomLength` (40) characters that look random.
- In Markdown files: link targets, reference labels, and HTML entities.

**Directives.** A comment can switch checking off or add words. Write
the prefix, a colon, and the name, such as `<prefix>:disable-next-line`.
The prefixes are `cspell`, `spell`, `spellchecker`, `spell-checker`, and
`textlint`.

| Directive | Effect |
| --- | --- |
| `disable` ... `enable` | Hides the text between them. |
| `disable-line` | Hides the whole line. |
| `disable-next-line` | Hides the rest of its line and the next line. |
| `ignore w1 w2` or `words w1 w2` | Accepts the words in the whole document. |
| `ignoreRegExp /re/` | Hides the matches in the whole document. |

A misspelled directive, or one that only cspell has, is reported, so a
directive never fails silently.

**Findings.** Each finding is one line,
`file(line,column): error: message`, with a 1-based line and column, as
[[#findings]] shows. The kinds are unknown word, flagged word,
unsupported directive, and invalid directive. Findings are sorted by
file, line, and column, and a summary line ends the run.

**Skipped files.** A binary file (a NUL character in the first 1,024
characters) and a file larger than `maxFileSize` are reported as `info`
and do not fail the run. A file that cannot be read is reported as an
`error` and fails it. Files are read as UTF-8, or as UTF-16 with a byte
order mark.
