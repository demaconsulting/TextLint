---
# textlint:ignore Zorblax
title: Dictionaries and project words
summary: Choose the language, and add the words of your project.
aliases:
  - dictionary
  - words
  - language
  - terms
  - custom-words
tags:
  - reference
order: 4
see-also:
  - configuration
  - spell-checking
examples:
  - name: project-word
    kind: command
    language: shell
    description: Zorblax is accepted, because .textlint.yaml lists it.
    content: '{{tool}} guide.md'
    expect: '0 issue(s)'
  - name: british
    kind: command
    language: shell
    description: Check American text with the British dictionary. Exits 1.
    content: '{{tool}} --language en-GB guide.md'
    exit: 1
    expect: "Unknown word 'favorite'"
  - name: flagged
    kind: command
    language: shell
    description: A flagged word is reported with its suggestion. Exits 1.
    content: '{{tool}} notes.txt'
    exit: 1
    expect: "Flagged word 'utilize'; suggestions: use"
---
**Built-in dictionaries.** The dictionaries are embedded in the tool, so
it needs no data files:

- American English: `en` or `en-US`, the default.
- British English: `en-GB`, with the -ise spellings only.
- Technical terms: software terms, companies, file types, licenses,
  fonts, programming languages, and tools. They are known in every
  language.

`language: en-US, en-GB` accepts both spellings. Names are not
case-sensitive, and any other language is an error. `--language` wins
over the settings and the overrides, as [[#british]] shows.

**Project words.** Add product names, people, and internal terms to
`words`, which matches any letter case; see [[#project-word]]. An entry
with digits or punctuation, such as `k8s`, matches the whole token.

- `ignoreWords` are never reported, even when they are flagged.
- `flagWords` are always reported. An entry in lower case matches any
  letter case, and `word->suggestion` adds a suggestion ([[#flagged]]).
- The order is: ignored, then flagged, then accepted by `words` or the
  dictionaries.
- Words for some folders only go in an override; see [[configuration]].
- A comment `textlint:words term1 term2` adds words for one document
  only; see [[spell-checking]].

**Invalid entries** fail the load: a blank entry, an entry with spaces,
an entry with no letter or digit, and an entry that starts with `!` (use
`flagWords` instead).

**Missing words.** Custom dictionary files (`dictionaries`) are not
supported; list the words in `words`. A general technical term that the
dictionaries lack can be proposed in an issue at
`https://github.com/demaconsulting/TextLint/issues`.
