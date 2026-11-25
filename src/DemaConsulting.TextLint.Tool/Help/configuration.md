---
# textlint:ignore Zorblax
title: Configuration file
summary: Write .textlint.yaml, which selects the files and sets the spelling rules.
aliases:
  - config
  - settings
  - textlint-yaml
  - globs
  - overrides
tags:
  - reference
order: 3
see-also:
  - dictionaries
  - spell-checking
  - troubleshooting
examples:
  - name: check
    kind: command
    language: shell
    description: Check with .textlint.yaml in this folder.
    content: '{{tool}}'
    expect: 'Checked 1 file(s): 0 issue(s)'
  - name: invalid
    kind: command
    language: shell
    description: Use a settings file with an unknown key. Exits 1.
    content: '{{tool}} --config bad.yaml guide.md'
    exit: 1
    expect: "'caseSensitive' is not a supported setting."
---
The tool reads `.textlint.yaml` in the root folder, which is the current
folder or the folder that `--root` names. No parent folder is searched.
`--config` names any other file. Without a settings file the defaults
apply, and an `info:` line says so.

A settings file for a documentation project:

```yaml
---
version: "0.2"
language: en-US
files:
  - "**/*.md"
  - "!drafts/**"
ignorePaths:
  - node_modules
  - bin/
  - obj/
words:
  - Zorblax
flagWords:
  - "utilize->use"
minWordLength: 4
maxFileSize: 1MB
ignoreRegExpList:
  - /\bTICKET-\d+\b/
overrides:
  - filename: CHANGELOG.md
    enabled: false
```

It checks every Markdown file except those below `drafts`, never looks in
`node_modules`, `bin`, or `obj`, accepts the product name `Zorblax`,
reports `utilize` with the suggestion `use`, does not check ticket
numbers such as `TICKET-42`, and skips `CHANGELOG.md`. The command that
[[#check]] prints checks with it.

**Keys.**

| Key | Default | Meaning |
| --- | --- | --- |
| `version` | none | Schema version; not checked. |
| `files` | empty | Ordered globs that select the files; empty selects none. |
| `ignorePaths` | empty | Globs that are never checked; `!` is not allowed. |
| `enableGlobDot` | `false` | Let wildcards match hidden names. |
| `enabled` | `true` | `false` skips the files; mostly used in an override. |
| `language` | `en` | `en-US`, `en-GB`, or both; see [[dictionaries]]. |
| `words` | empty | Words that are accepted. |
| `ignoreWords` | empty | Words that are never reported. |
| `flagWords` | empty | Words that are always reported. |
| `minWordLength` | `4` | Shorter words are not checked. |
| `maxFileSize` | no limit | Larger files are skipped, such as `500K` or `1MB`. |
| `ignoreRegExpList` | empty | Expressions or pattern names whose matches are not checked. |
| `patterns` | empty | Named expressions for `ignoreRegExpList`. |
| `ignoreRandomStrings` | `true` | Skip long tokens that look random. |
| `minRandomLength` | `40` | Length from which a token may be random. |
| `overrides` | empty | Other settings for the files that a `filename` selects. |

**Globs.** Use `/` on every platform. `*` matches within a folder, `**`
any number of folders, and `{md,txt}` either name; `?` is not supported.
A name without `/` matches at any depth, and a trailing `/` matches only
folders. A leading `/` anchors to the root folder. Matching is
case-sensitive. Wildcards skip hidden names unless `enableGlobDot` is
`true`, but a hidden name written out, such as `.github/**`, matches.
In `files`, a glob that starts with `!` excludes, and the last glob that
matches a file decides; the first glob cannot start with `!`.

**Overrides.** Each entry needs a `filename`: a glob or a list of globs.
Every entry that matches a file applies, in the order written. Lists such
as `words` add to the top-level values, and single values such as
`language` replace them. An entry with `enabled: false` may hold no other
settings. Settings that choose the files or apply to the whole run, such
as `files`, `ignorePaths`, `enableGlobDot`, and `maxFileSize`, are not
allowed in an override. On a full run, an override that matches none of
the selected files gives a warning.

**Errors.** An unknown key, a wrong type, or a bad glob or expression
fails the load. Each problem is printed as
`file(line,column): error: message`, and nothing is checked, as
[[#invalid]] shows. Keys are case-sensitive, and the file holds one YAML
document. An empty file gives the defaults.
