# Dictionaries

This folder holds the plain-text word lists from which the TextLint dictionaries are built. The lists are
data, not code: they are reviewed as text, edited by hand where noted below, and converted into compact binary
dictionary files at build time by the dictionary builder (`src/DemaConsulting.TextLint.DictionaryBuilder`). No
binary dictionary is ever committed.

## Layout

The folder is the tag; there is no manifest. Each top-level folder is one dictionary, and the folder name is the
dictionary name that the build passes to the builder.

| Folder | Content |
| ------ | ------- |
| `en-common/` | English words spelled the same in American and British (-ise) English |
| `en-US/` | American-only spellings |
| `en-GB/` | British (-ise)-only spellings |
| `technical/language/` | Programming-language keyword and API lists (7 files) |
| `technical/general/` | General technical lists and the project-owned additions (8 files) |
| `technical/variant-keep.txt` | Reviewed spelling variants that are genuine programming terms |

The language lists are csharp, dotnet, typescript, node, html, css, and powershell. The general lists are
software-terms, companies, filetypes, public-licenses, fullstack, npm, k8s, and additions.

An ordinary dictionary reads every `.txt` file beneath its folder. The technical dictionary reads only the three
places above and fails the build for a word list anywhere else in its folder. The builder also fails the build if
a folder holds word lists but is not declared to it, so a mistyped folder name cannot silently drop a list.

## Spelling Variants in the Technical Lists

The language setting (American or British English) must decide ordinary spelling variants, so the technical
dictionary must not make them known in every language. When it combines the technical lists, the builder
removes from the `general` lists every word that is in the American-only or the British-only dictionary,
unless the word is also in a `language` list or in `variant-keep.txt`. For example `favorite`, `flavors`, and
`aluminum` are removed, while `color` and `grey` stay because the CSS list has them, and `tokenizer` stays because
it is in the reviewed list. The imported lists themselves are never edited to apply this rule.

`variant-keep.txt` is curated by hand. Add a word only after a reviewer has confirmed that it is a programming term
and a variant spelling that would otherwise be removed; the builder fails the build for a word in it that is not a
spelling variant, which keeps the list short, and the build output states how many variants were kept through the
language lists, kept through `variant-keep.txt`, and removed.

## File Format

The format is enforced by the builder, which reports every violation with its file and line:

- UTF-8 text with LF line endings (a byte order mark and CRLF are tolerated).
- One entry per line. Spaces and tabs around an entry are trimmed. Blank lines and lines whose first
  non-blank character is `#` are ignored. A comment cannot follow an entry on the same line.
- An entry is 1 to 64 characters: ASCII letters, Latin-1 letters (for example `é` and `ß`), and the straight
  apostrophe `'`. It must start and end with a letter and must not contain two apostrophes in a row. Digits,
  hyphens, dots, spaces, the typographic apostrophe, and letters of other scripts make the build fail.
- Order and repetition do not matter. Entries that differ only in letter case are the same word, and
  lookups ignore case. Capitalization is still recorded, so write names as names (`Paris`).
- Accents matter. The English lists contain both the accented and the unaccented spelling of accented
  words (`café` and `cafe`), so unaccented text is accepted and a wrong accent is still an error.

## Adding a Word

Add genuine project-specific technical terms to `technical/general/additions.txt`, one per line. That file is
owned by the project and is never overwritten by the refresh script; a spelling variant added there is removed
by the rule above, so a genuine programming term in a variant spelling belongs in `technical/variant-keep.txt`
instead. Do not edit the imported files by hand, because the next refresh replaces them, and never add a
misspelling to hide a spelling error.

## Provenance and Licenses

Every list file starts with comment lines that state its source, pinned version, license, and the command that
produced it. The upstream sources and their licenses are:

| Lists | Source | Pinned to | License |
| ----- | ------ | --------- | ------- |
| `en-common`, `en-US`, `en-GB` | SCOWL, <https://github.com/en-wl/wordlist>, size 70, variant level 1 | tag `rel-2026.02.25` (commit `7e99edab8e32f9f9ea2b15f249ca8d4d67237410`) | SCOWL permissive notice |
| `technical/general/software-terms.txt` | `@cspell/dict-software-terms` (default-on lists) | 5.4.8 | MIT |
| `technical/general/companies.txt` | `@cspell/dict-companies` | 3.2.13 | MIT |
| `technical/general/filetypes.txt` | `@cspell/dict-filetypes` | 3.0.18 | MIT |
| `technical/general/public-licenses.txt` | `@cspell/dict-public-licenses` | 2.0.17 | MIT |
| `technical/general/fullstack.txt` | `@cspell/dict-fullstack` | 3.2.9 | MIT |
| `technical/general/npm.txt` | `@cspell/dict-npm` | 5.2.55 | MIT |
| `technical/general/k8s.txt` | `@cspell/dict-k8s` | 1.0.13 | MIT |
| `technical/language/dotnet.txt` | `@cspell/dict-dotnet` | 5.0.15 | MIT |
| `technical/language/csharp.txt` | `@cspell/dict-csharp` | 4.0.8 | MIT |
| `technical/language/node.txt` | `@cspell/dict-node` | 5.0.9 | MIT |
| `technical/language/typescript.txt` | `@cspell/dict-typescript` | 3.2.3 | MIT |
| `technical/language/html.txt` | `@cspell/dict-html` | 4.0.16 | MIT |
| `technical/language/css.txt` | `@cspell/dict-css` | 4.1.2 | MIT |
| `technical/language/powershell.txt` | `@cspell/dict-powershell` | 5.0.17 | MIT |

The license notices are in `third-party/THIRD-PARTY-NOTICES.txt` at the root of the repository. Packages
with other licenses (for example the LGPL cspell English dictionary and the CC BY-SA misspellings list) are
deliberately not used. The cspell `markdown` package is not imported because it contains no words.

The English lists keep the common and the regional spellings apart: American is SCOWL spelling code `A`,
British (-ise) is code `B`, and the three lists are the intersection and the two differences of those two
sets. The technical lists are imported with their own filter and are not de-duplicated against the English
lists, so a list stays faithful to its source; the spelling-variant rule above is applied by the builder.

## Refreshing the Lists

The imported lists are regenerated by `refresh.py`. It is a manual maintenance tool and is never run by the
build or by CI.

Prerequisites:

<!-- cspell:ignore PYTHONUTF -->

- Python 3.7 or later with SQLite 3.33.0 or later, git, and npm on the path.
- Network access to GitHub and the npm registry.
- UTF-8 mode for Python, enabled by setting the environment variable `PYTHONUTF8` to `1` (on Windows, set it
  for the session, because the SCOWL importer fails on accented words otherwise).

Run it from the repository root:

```bash
python dictionaries/refresh.py --workdir <scratch-folder>
```

Creating the SCOWL database takes about five minutes the first time; pass the same `--workdir` again to reuse it.
Use `--only english` or `--only technical` to refresh one family. The script prints the number of entries per
file and the number of entries that were dropped, which belongs in the pull request description. To move to a
new upstream release, change the pinned versions at the top of the script and in the table above.
