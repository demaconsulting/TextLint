## FileSelection

![FileSelection Structure](FileSelectionView.svg)

### Overview

The `FileSelection` subsystem applies glob include and ignore patterns to a root directory and returns
the ordered list of files to check. It owns the glob pattern rules: which pattern forms are accepted, how
brace alternatives and bare names are interpreted, case sensitivity, the hidden-file rule, and the
order of the result. The subsystem contains three units: `GlobPatternExpander`, which validates
and rewrites patterns; `HiddenPathPolicy`, which decides which hidden paths the patterns may
reach; and `FileSelector`, which walks the directory and produces the result. The supporting types
`SelectedFile`, `InvalidGlobPatternException`, and an internal directory view are documented with
the unit that uses them.

The subsystem reads directory listings but never opens a file, and it does no console output. It
does not read settings files; that belongs to the `Configuration` subsystem.

### Interfaces

**FileSelector.Select**: Selects files.

- _Type_: In-process .NET static method.
- _Role_: Provider.
- _Contract_: Accepts a root directory path, a list of include patterns, a list of ignore patterns, and
  a hidden-file switch (`enableGlobDot`, default `false`), and returns the selected files as a
  read-only list of `SelectedFile`, each with a root-relative path using forward slashes and the full
  operating-system path. The list is ordered by ordinal relative path. Empty include patterns select
  nothing.
- _Constraints_: Throws `ArgumentNullException` or `ArgumentException` for a null or empty argument,
  `DirectoryNotFoundException` when the root does not exist (an unreadable root throws its own
  `UnauthorizedAccessException` or `IOException`), and `InvalidGlobPatternException` when
  a pattern is unsupported or malformed. Matching is case-sensitive on every platform. The pattern
  lists are only read during the call.

**Supported pattern forms**: Patterns use `/` as the separator and `*` and `**` as wildcards.
`**` matches any number of directory levels. Brace alternatives such as `*.{md,cs}` expand to one
pattern per alternative. A pattern with no `/` names a file or directory at any depth, so
`node_modules` excludes every such directory; a leading `/` or `./` anchors a pattern at the root
directory (so `/build/` is the `build` directory under the root, not a file system path); a
trailing `/` limits it to directories. Negation (`!`), backslashes, the `?` wildcard (the error suggests `*`),
absolute paths (a drive letter such as `C:/x`, or a `//` network path), empty and `..` segments, unbalanced
braces, and expansions beyond a fixed limit are rejected.

**Dependencies on other subsystems**: None. The subsystem takes its inputs as plain parameters
rather than a configuration type, so it does not depend on the `Configuration` subsystem. The
dependency between the two subsystems runs one way only: `Configuration` uses this subsystem's
`GlobPatternExpander` to validate patterns while loading a settings file. A caller that holds
`TextLintSettings` maps it to the parameters with
`FileSelector.Select(root, settings.Files, settings.IgnorePaths, settings.EnableGlobDot)`. Plain
parameters were chosen over a subsystem-owned options type because such a type would only duplicate
the list-copying and immutability of `TextLintSettings` while adding a unit with no behavior of its
own; the pattern lists are not retained, so no copy is needed.

### Design

Selection is a pipeline. `FileSelector` first expands every include and ignore pattern with
`GlobPatternExpander`, which also validates them, so a bad pattern fails before any directory is read
and even when no files would be selected. It then builds a matcher from Microsoft.Extensions.FileSystemGlobbing
(see _FileSystemGlobbing Integration Design_) configured for ordinal, case-sensitive comparison,
adds the expanded patterns, and walks the root directory.

Ignored directories are never read: the ignore patterns become exclude patterns that the matcher
uses to skip whole subtrees. Hidden directories are skipped the same way by wrapping the root in a
view that consults `HiddenPathPolicy` before exposing a hidden subdirectory. A plain exclude pattern
cannot do this, because excluding every dot-prefixed path would also suppress a pattern such as
`.github/**` that names a hidden directory literally. After the matcher returns its candidates,
`HiddenPathPolicy` makes the exact decision for each path that contains a hidden segment, pattern by
pattern, so one pattern's literal hidden name never excuses another pattern's wildcard. Finally the
paths are sorted by ordinal comparison, which makes the result identical on every platform and run.

Case sensitivity is requested explicitly because the matcher is case-insensitive by default. Symbolic
links and junctions are handled however the matcher's directory abstraction handles them; the
subsystem adds no special treatment.
