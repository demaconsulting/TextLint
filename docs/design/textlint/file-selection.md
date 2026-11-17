## FileSelection

![FileSelection Structure](FileSelectionView.svg)

### Overview

The `FileSelection` subsystem applies glob include and ignore patterns to a root directory and returns
the ordered list of files to check. It owns the glob pattern rules: which pattern forms are accepted, how
brace alternatives and bare names are interpreted, case sensitivity, the hidden-file rule, the ordered-list rule
(`!` excludes, the last match wins), and the
order of the result. The subsystem contains five units: `GlobPatternExpander`, which validates
and rewrites patterns; `HiddenPathPolicy`, which decides which hidden paths the patterns may
reach; `OrderedGlobList`, which gives a list of patterns the ordered meaning of `!`; `FileSelector`, which walks the
directory and produces the result; and `CommandLinePathResolver`,
which turns the path arguments a person types on a command line into a search directory and
validated patterns. The supporting types
`SelectedFile`, `InvalidGlobPatternException`, and the internal directory views are documented with
the unit that uses them, and `CommandLineSelection` with `CommandLinePathResolver`.

The subsystem reads directory listings but never opens a file, and it does no console output. It
does not read settings files; that belongs to the `Configuration` subsystem.

### Interfaces

**FileSelector.Select**: Selects files.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts a root directory path, a list of include patterns, a list of ignore patterns, a
  hidden-file switch (`enableGlobDot`, default `false`), and an optional cancellation token (default none), and
  returns the selected files as a
  read-only list of `SelectedFile`, each with a root-relative path using forward slashes and the full
  operating-system path. The list is ordered by ordinal relative path. Empty include patterns select
  nothing. The include patterns are an ordered list: a pattern with a leading `!` excludes, the last pattern that
  matches a file decides, so a later pattern can include again what an earlier one removed, and a list without any
  `!` selects exactly what its patterns match as plain includes. The ignore patterns cannot be negated and always
  win.
- *Constraints*: Throws `ArgumentNullException` or `ArgumentException` for a null or empty argument,
  `DirectoryNotFoundException` when the root does not exist (an unreadable root throws its own
  `UnauthorizedAccessException` or `IOException`), and `InvalidGlobPatternException` when
  a pattern is unsupported or malformed, when the first include pattern is negated, or when an ignore pattern is
  negated. Throws `OperationCanceledException`, with no partial selection, when the
  token is canceled before the call or while the directory tree is read; the walk stops at the next directory or
  directory entry. Matching is case-sensitive on every platform. The pattern
  lists are only read during the call.

**CommandLinePathResolver.Resolve**: Turns command-line path arguments into a search directory and patterns.

- *Type*: In-process .NET static method returning a `CommandLineSelection`.
- *Role*: Provider.
- *Contract*: Accepts a root directory path and a non-empty list of arguments as typed, and returns the absolute
  search directory, the validated patterns relative to it, whether files are to be named by absolute path, and
  `GetDisplayPath`, which gives the name under which a selected file is reported. Relative arguments are patterns
  relative to the root; on Windows a backslash is a separator. Absolute arguments are split at their first wildcard
  into a literal directory and a pattern, and one inside the root means the same as its relative form. An absolute
  argument that names an existing directory selects everything beneath it.
- *Constraints*: Throws `ArgumentNullException` or `ArgumentException` for null, empty, or invalid input, and
  `InvalidGlobPatternException`, naming the argument as typed, for anything the pattern rules or the absolute-path
  rules reject. Throws `ArgumentException` for a relative argument combined with an absolute one outside the root
  and for absolute arguments on different drives. Throws `UnauthorizedAccessException` or `IOException` when the
  file system cannot say whether an absolute argument is a directory; a missing path is not an error and is
  treated as a file name. The settings-file rules are not affected: they still reject
  absolute paths and backslashes.

**Supported pattern forms**: Patterns use `/` as the separator and `*` and `**` as wildcards.
`**` matches any number of directory levels. Brace alternatives such as `*.{md,cs}` expand to one
pattern per alternative. A pattern with no `/` names a file or directory at any depth, so
`node_modules` excludes every such directory; a leading `/` or `./` anchors a pattern at the root
directory (so `/build/` is the `build` directory under the root, not a file system path); a
trailing `/` limits it to directories. A leading `!` is accepted only on the entries of an ordered list (the include
patterns and an override's `filename`, see `OrderedGlobList`), where it excludes; it is rejected in the ignore
patterns and in command-line patterns, and the first entry of an ordered list may not carry it. Backslashes, the `?`
wildcard (the error suggests `*`),
absolute paths (a drive letter such as `C:/x`, or a `//` network path), empty and `..` segments, unbalanced
braces, and expansions beyond a fixed limit are rejected everywhere.

**Dependencies on other subsystems**: None. The subsystem takes its inputs as plain parameters
rather than a configuration type, so it does not depend on the `Configuration` subsystem. The
dependency between the two subsystems runs one way only: `Configuration` uses this subsystem's
`GlobPatternExpander` and `OrderedGlobList` to validate and match patterns. A caller that holds
`TextLintSettings` maps it to the parameters with
`FileSelector.Select(root, settings.Files, settings.IgnorePaths, settings.EnableGlobDot)`. Plain
parameters were chosen over a subsystem-owned options type because such a type would only duplicate
the list-copying and immutability of `TextLintSettings` while adding a unit with no behavior of its
own; the pattern lists are not retained, so no copy is needed.

### Design

Selection is a pipeline. `FileSelector` first parses the include patterns into an `OrderedGlobList` and expands every
ignore pattern with
`GlobPatternExpander`, which also validates them, so a bad pattern fails before any directory is read
and even when no files would be selected. It then builds a matcher from Microsoft.Extensions.FileSystemGlobbing
(see *FileSystemGlobbing Integration Design*) configured for ordinal, case-sensitive comparison,
adds the expanded patterns, and walks the root directory.

Ignored directories are never read: the ignore patterns become exclude patterns that the matcher
uses to skip whole subtrees. Hidden directories are skipped the same way by wrapping the root in a
view that consults `HiddenPathPolicy` before exposing a hidden subdirectory. A plain exclude pattern
cannot do this, because excluding every dot-prefixed path would also suppress a pattern such as
`.github/**` that names a hidden directory literally. After the matcher returns its candidates,
`HiddenPathPolicy` makes the exact decision for each path that contains a hidden segment, pattern by
pattern, so one pattern's literal hidden name never excuses another pattern's wildcard. Finally the
paths are sorted by ordinal comparison, which makes the result identical on every platform and run.

An ordered include list needs two more things, and only when it contains a `!`; a list without one takes the
unchanged path above. The matcher has no notion of order, so it is given the plain patterns as a prefilter and
`OrderedGlobList.IsMatch` makes the exact decision for every candidate, with the last matching pattern winning.
And a directory that the ordered rules exclude entirely, such as `node_modules` in
`["**/*.md", "!node_modules"]`, must still not be read, which a matcher exclude cannot express when a later pattern
may include something under it again, so a further directory view, `OrderedPruningDirectory`, hides each
subdirectory that `OrderedGlobList.IsSubtreeExcluded` proves excluded. That test is conservative: a later pattern
that could reach beneath the directory keeps it in the walk, and a negation that names particular files, such as
`!docs/**/*.md`, never prunes. A negation has the meaning of the same pattern in `ignorePaths`, so `!docs/a`
excludes a directory `docs/a` and everything below it, exactly as `ignorePaths: [docs/a]` does.

Case sensitivity is requested explicitly because the matcher is case-insensitive by default. Symbolic
links and junctions are handled however the matcher's directory abstraction handles them; the
subsystem adds no special treatment.

Command-line input takes a separate, forgiving path in front of the same pipeline. `CommandLinePathResolver`
rewrites what a person typed (backslashes on Windows, absolute paths) into a search directory and patterns that
satisfy the strict rules, validates them with `GlobPatternExpander`, and hands the result to `FileSelector`
unchanged, so selection itself has one implementation and the settings-file rules stay strict. The resolver
is string logic with one exception: it asks the file system whether a literal absolute argument names an
existing directory, through a replaceable look-up so that both the Windows and the other path rules are tested
on every operating system.
