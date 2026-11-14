### OrderedGlobList

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`OrderedGlobList` gives a list of glob patterns the ordered meaning that users know from ignore files: a
pattern that starts with `!` excludes, and the last pattern that matches a path decides, so a later pattern can
include again what an earlier one removed. It is the one place that implements this, for the `files` setting
and for the `filename` list of a settings override, so both behave identically. The matcher of the
FileSystemGlobbing package has only unordered include and exclude sets, and `GlobPatternExpander` deliberately
rejects `!`, so the ordering needs its own logic.

#### Data Model

`OrderedGlobList` is an internal sealed class. An instance is immutable and thread-safe; it is created by
`Parse`, which compiles every pattern once, so matching a path only evaluates precompiled matchers.

Each pattern is kept as an internal `Entry` that holds:

- whether the pattern is negated (it was written with a leading `!`);
- the expanded matcher patterns, which `GlobPatternExpander` produces from the pattern without its `!`;
- a matcher with ordinal (case-sensitive) comparison that holds all the expanded patterns;
- for a negated pattern, a second matcher that holds only its _subtree forms_: the expanded patterns that equal
  `**` or end in `/**`, which every negated pattern has because of the extension described under `Parse`;
- for a plain pattern, the expanded patterns split into segments, used to test whether a directory could lead to
  a match.

The list also keeps the `enableGlobDot` switch. Properties derived at creation are `IsEmpty`, `HasNegation`
(some pattern is negated), and `PositivePatterns` (the expanded patterns of every plain pattern, without
duplicates, in the order written). `PositivePatterns` is a superset of what the list can select, so the file
selector uses it to prefilter the directory walk and to decide which hidden directories may be entered.

#### Key Methods

**Parse**: Compiles a list of patterns.

- _Parameters_: `IReadOnlyList<string> patterns`, in order, each optionally prefixed with `!`; `bool
  enableGlobDot`.
- _Returns_: the compiled list; an empty list when there are no patterns.
- _Preconditions_: `patterns` is not null.
- _Postconditions_: every pattern is valid; nothing has touched the file system.

Each pattern is compiled in order. A pattern that starts with `!` is negated, and the remainder is expanded with
`GlobPatternExpander`, so every pattern keeps the rules of that unit: brace alternatives, bare names at any depth,
a trailing `/` for directories, a leading `/` anchor, and no `?`. Only one `!` is stripped, so `!!x` is rejected
by the expander. When the expander rejects a pattern, the exception is rethrown against the pattern as written,
including its `!`, so a message shows what the user typed. The first pattern of a list may not be negated,
because there is nothing yet to remove files from; it fails with the reason
`'!' must follow a pattern it can remove files from; put it after the pattern it excludes`.

A negation has the meaning that the same pattern has in `ignorePaths`, which is an exclude pattern: a pattern with a
path, such as `docs/a`, removes a file of that name and also a directory of that name with everything below it,
while the same pattern as a plain include selects only a file. So for a negated pattern, each expanded form that
does not already end in `/**` (or equal `**`) is followed by the same form with `/**` added, and the matchers see
both. A bare name (`!node_modules`) and a trailing `/` (`!build/`) already expand to a subtree form, so they are
unchanged. A plain pattern is never extended, which keeps `files` selecting exactly what it always has.

**ValidateEntry**: Validates one pattern of a list without keeping it.

- _Parameters_: `string pattern`, as written; `bool isFirst`, whether it is the first of its list.
- _Postconditions_: returns normally when the pattern is valid in that position; otherwise throws the same
  exception `Parse` would throw.

This lets the settings loader report each bad pattern at its own position instead of only the first bad pattern
of the list.

**IsMatch**: Decides whether the list selects a file path.

- _Parameters_: the root-relative path, using forward slashes.
- _Returns_: `true` when the last pattern that matches the path is a plain pattern.
- _Postconditions_: none; the method is pure.

The patterns are examined from the last to the first, and the first one that matches decides. A negated pattern
that matches rejects the path, even when the path contains a hidden name, because a pattern that excludes
something should not be defeated by a dot. A plain pattern that matches accepts the path when wildcards may
match hidden names, or when `HiddenPathPolicy.IsPathPermitted` allows the path under that pattern alone; a plain
pattern that could only reach the path through a hidden name does not count as matching, and the search
continues with the earlier patterns. When no pattern decides, the path is not selected. Matching is ordinal.

**IsSubtreeExcluded**: Decides whether no file beneath a directory can be selected, so the directory need not be
read.

- _Parameters_: the root-relative directory path, using forward slashes.
- _Returns_: `true` only when every file beneath the directory is certainly excluded.
- _Postconditions_: none; the method is pure.

The decision is deliberately conservative: answering `false` only costs time, while answering `true` wrongly
would silently drop files. Without a negation the answer is always `false`. Otherwise the patterns are examined
from the last to the first:

- a plain pattern that could match beneath the directory vetoes the pruning at once, because it might bring back
  a file that an earlier negation removed; whether it could is a prefix alignment of the directory's segments
  with the pattern's segments, computed as in `HiddenPathPolicy` but without the hidden-name rule, so the answer
  is a superset of the truth;
- a negated pattern that covers the subtree decides that the directory is excluded: it covers it when one of its
  subtree forms matches a probe file named with a control character inside the directory, which only a trailing
  `**` can swallow;
- any other pattern is ignored.

When no pattern decides, the answer is `false`. Every negated pattern has a subtree form, so a negation that names
a directory covers it whether it is written as a bare name (`!node_modules`), as a directory (`!build/`), with a
path (`!docs/a`), or with a trailing recursive segment (`!docs/**`). A pattern that names particular files, such as
`!**/*.md` or `!docs/**/*.md`, covers no directory that can hold a selected file, so it never prunes one; the
directory stays in the walk and the files are decided exactly after the match. The alignment table has one cell
per pair of directory and pattern segments, so the effort is bounded by the product of the two lengths however many
recursive segments a pattern has.

#### Error Handling

`Parse` and `ValidateEntry` throw `InvalidGlobPatternException` for an unsupported or malformed pattern and for a
negated first pattern, naming the pattern as written. A null argument throws `ArgumentNullException`. `IsMatch`
and `IsSubtreeExcluded` are total for non-null input and report `true` or `false`. Duplicate or ineffective
negations are not errors. Nothing is logged.

#### Dependencies

- **GlobPatternExpander** - validates and expands the body of each pattern.
- **HiddenPathPolicy** - decides whether a plain pattern may reach a hidden name.
- **Microsoft.Extensions.FileSystemGlobbing** - the matcher. See _FileSystemGlobbing Integration Design_. No type
  from it appears in a signature of this unit.
- **.NET BCL** - strings, collections, and paths.

#### Callers

- **FileSelector** - parses the include patterns; uses `PositivePatterns`, `HasNegation`, and `IsMatch`, and
  hands the list to `OrderedPruningDirectory`.
- **OrderedPruningDirectory** (part of FileSelector) - asks `IsSubtreeExcluded` about each subdirectory.
- **SettingsOverrideResolver** (Configuration subsystem) - compiles the `filename` list of every override and
  asks `IsMatch` for each file.
- **SettingsLoader** (Configuration subsystem) - validates each entry of `files` and of an override's `filename`
  with `ValidateEntry`.
