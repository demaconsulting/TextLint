### HiddenPathPolicy

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`HiddenPathPolicy` decides which hidden (dot-prefixed) files and directories the include patterns may
reach. The rule is the one cspell users know: a wildcard (`*` or `**`) never matches a name that starts
with a dot, but a pattern segment that itself starts with a literal dot, such as `.github` or `.*rc`,
does. The matcher knows nothing about hidden names, and excluding every dot-prefixed path with an exclude
pattern would also suppress literal hidden includes, so the rule needs its own logic.

#### Data Model

`HiddenPathPolicy` is an internal static class with no state. Paths and patterns are strings that use
`/` as the separator; a name is hidden when its first character is a dot. Within a pattern segment,
`*` is the only wildcard, matching the matcher's own behavior.

#### Key Methods

**IsHidden**: Reports whether a single name starts with a dot.

**IsPathPermitted**: Decides whether the include patterns may select a file path.

- _Parameters_: the root-relative path and the expanded include patterns.
- _Returns_: `true` when the path has no hidden segment, or when some single pattern matches the whole
  path with every hidden segment consumed by a pattern segment that starts with a literal dot.
- _Preconditions_: the path is relative and uses forward slashes.
- _Postconditions_: none; the method is pure.

**CanContainPermittedPaths**: Decides whether a directory should be entered.

- _Parameters_: the root-relative directory path and the expanded include patterns.
- _Returns_: `true` when the directory has no hidden segment, or when some pattern could continue
  beneath it under the same rule.
- _Postconditions_: a hidden directory that no pattern can reach is not entered, so it is never read.

Both operations use one alignment search of the path segments against the pattern segments. A
recursive pattern segment (`**`) consumes zero segments, or one segment that is not hidden and then tries
again; any other pattern segment consumes exactly one path segment that it matches, where a hidden name is
matched only by a pattern segment that starts with a dot. For a file the whole pattern must be consumed
(the rest may only be `**`); for a directory it is enough that the path is consumed, because the pattern may
continue beneath it. Each pattern is checked on its own: this is what prevents one pattern's literal hidden
name from excusing another pattern's wildcard. Comparison is ordinal. Patterns are short, so plain
backtracking is sufficient, and the common case of a path with no hidden segment returns immediately
without any search.

The directory test is deliberately looser than the file test, because it exists only to avoid reading
directories that can never contribute; the file test makes the exact decision afterwards.

#### Error Handling

None. The methods are pure and total: they accept any strings and report `true` or `false`. Null
arguments are programming errors and surface as the underlying null reference exception; the
callers never pass null.

#### Dependencies

- **.NET BCL** - strings and sequence operations. No other unit, subsystem, or OTS item is used.

#### Callers

- **FileSelector** - applies `IsPathPermitted` to the matched files.
- **HiddenFilteringDirectory** (part of FileSelector) - applies `CanContainPermittedPaths` and
  `IsHidden` to decide which subdirectories to expose to the matcher.
