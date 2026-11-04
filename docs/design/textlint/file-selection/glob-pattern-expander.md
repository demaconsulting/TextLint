### GlobPatternExpander

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`GlobPatternExpander` validates one user-written glob pattern and rewrites it into the patterns the
matcher understands. The matcher has no brace alternatives, no notion that a bare name matches at any
depth, and silently mishandles absolute, parent-escaping, and backslash patterns; this unit closes
those gaps with pure string logic so the behavior can be tested exhaustively without a file system.
It also defines the supporting exception `InvalidGlobPatternException`.

#### Data Model

`GlobPatternExpander` is an internal static class with no state. Named constants hold the
recursive segment (`**`), the separators, and the maximum number of patterns a single pattern may
expand to (256), which guards against short patterns such as `{a,b}{a,b}{a,b}...` exhausting memory.

**InvalidGlobPatternException** is a public sealed exception deriving from `ArgumentException`. It
carries `Pattern`, the original text as supplied, and its message always has the form
`Invalid glob pattern '<pattern>': <reason>`. Only the library creates it.

#### Key Methods

**Expand**: Validates and rewrites a pattern.

- _Parameters_: `string pattern` - the user pattern.
- _Returns_: the matcher patterns, without duplicates, in a stable order.
- _Preconditions_: `pattern` is not null.
- _Postconditions_: every returned pattern is safe to give to the matcher.

Steps: (1) validate the raw pattern and reject it if it is empty or white space, starts with `!`,
contains a backslash or `?`, begins with a drive specifier, or has unbalanced braces; (2) expand
brace groups; (3) anchor and clean each alternative on its own; (4) remove duplicates.

Brace expansion follows shell rules. The leftmost brace group that has a top-level comma expands
into one alternative per comma-separated part, spliced between the surrounding text, and expansion
repeats on the result. A group without a comma stays literal; nested groups expand once the outer
group is spliced in; several groups give the cartesian product; an empty alternative yields the
pattern without it. Exceeding the expansion limit is an error.

Anchoring and cleaning, per alternative: leading `./` and `/` are removed and mark the pattern as
anchored at the root; a trailing `/` marks it as directory-only; the text is split into segments,
`.` segments are dropped, repeated `**` segments are merged, and an empty segment, a `..` segment,
or no remaining segment is an error. Because this runs after brace expansion, a `..` that only
appears through an alternative is also rejected. Then:

| Input           | Output                                  | Why                                             |
| :-------------- | :-------------------------------------- | :---------------------------------------------- |
| `node_modules`  | `**/node_modules`, `**/node_modules/**` | A bare name is a file or directory at any depth |
| `build/`        | `**/build/**`                           | Directory only, at any depth                    |
| `/README.md`    | `README.md`                             | Anchored at the root                            |
| `./README.md`   | `README.md`                             | Anchored at the root                            |
| `docs/build/`   | `docs/build/**`                         | Anchored directory subtree                      |
| `src/*.{md,cs}` | `src/*.md`, `src/*.cs`                  | Brace alternatives                              |
| `**/*.md`       | `**/*.md`                               | Already anchored by its separator               |

The second output for a bare name makes the name select, or exclude, the whole subtree, which is what
ignore-style patterns are expected to do, and it lets the matcher skip an excluded directory without
reading it.

#### Error Handling

Every unsupported pattern throws `InvalidGlobPatternException` naming the pattern and the reason: empty,
negated, backslash, `?`, drive-rooted, empty segment, parent segment, unbalanced braces, over-expansion,
or no segments. A null pattern throws `ArgumentNullException`. Callers either let the exception
propagate (`FileSelector`) or convert it into a positioned diagnostic (`SettingsLoader`). Nothing is
logged.

#### Dependencies

- **.NET BCL** - strings and collections. No other unit, subsystem, or OTS item is used.

#### Callers

- **FileSelector** - expands every include and ignore pattern before selecting files.
- **SettingsLoader** (Configuration subsystem) - validates patterns while loading a settings file.
