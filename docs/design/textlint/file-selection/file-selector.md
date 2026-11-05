### FileSelector

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`FileSelector` applies include and ignore glob patterns to a root directory and returns the files to check. It is
the one place that turns glob patterns into a file list, so every consumer selects files identically. It takes
its inputs as plain parameters, so it has no dependency on the Configuration subsystem. It also defines the
supporting type `SelectedFile` and an internal directory view, `HiddenFilteringDirectory`, which has no unit of
its own.

#### Data Model

`FileSelector` is a public static class with no state; each call builds its own matcher.

**SelectedFile** is an immutable result entry with `RelativePath` (root-relative, always using forward
slashes) and `FullPath` (the absolute operating-system path). Only the selector creates it.

**HiddenFilteringDirectory** is an internal view over the matcher's directory abstraction. It holds the
wrapped directory, its root-relative path, and the expanded include patterns. When enumerated, it drops
a hidden subdirectory unless `HiddenPathPolicy` says an include pattern can reach beneath it, and wraps every
subdirectory it keeps so the filter applies at every depth. Files and non-hidden directories pass
through unchanged. The matcher only descends into what a directory enumerates, so a dropped
directory is never read from disk.

#### Key Methods

**Select (public)**: Selects files beneath a directory path.

- _Parameters_: `string rootDirectory`; `IReadOnlyList<string> includePatterns`; `IReadOnlyList<string>
  ignorePatterns`; `bool enableGlobDot` (default `false`).
- _Returns_: a read-only list of `SelectedFile`, ordered by ordinal relative path.
- _Preconditions_: no argument is null, the directory name is not empty, and the directory exists. The
  pattern lists are read once during the call and are not retained, so the caller may change them afterwards.
- _Postconditions_: the list is a snapshot; no file has been opened.

**Select (internal)**: The same selection over an abstract directory. It is a test seam: tests pass a
recording directory to prove which directories are enumerated, which is deterministic where a timing test
would not be. The public method validates the path and calls it.

Steps: (1) expand every include and ignore pattern with `GlobPatternExpander`, so an invalid pattern
fails first, even when nothing is included; (2) return an empty list when there are no include patterns,
without reading any directory; (3) build a matcher with ordinal comparison and add the expanded include and
exclude patterns; (4) unless `enableGlobDot` is set, walk through a `HiddenFilteringDirectory`; (5) run the
matcher; (6) unless `enableGlobDot` is set, keep only the paths `HiddenPathPolicy` permits; (7) sort the
paths by ordinal comparison and create the `SelectedFile` entries, computing each full path from the root.

The result is ordered with the ordinal comparer, not the file system's order, so it is identical on every
platform. Matching is case-sensitive because the matcher is constructed with ordinal comparison; its default is
case-insensitive. Excluded directories are skipped by the matcher itself, because the expansion of a bare name
yields an exclude form that covers the whole subtree.

#### Error Handling

A null argument throws `ArgumentNullException`; an empty or white-space root throws
`ArgumentException`; a root that does not exist throws `DirectoryNotFoundException`; an unsupported pattern
propagates `InvalidGlobPatternException` from the expander. Input/output errors raised by the file system
while walking, such as a permission failure, propagate unchanged. Nothing is logged.

#### Dependencies

- **GlobPatternExpander** and **HiddenPathPolicy** - pattern rules and the hidden-name rule.
- **Microsoft.Extensions.FileSystemGlobbing** - the matcher and the directory abstraction. See
  _FileSystemGlobbing Integration Design_. No type from it appears in a public signature.
- **.NET BCL** - paths and directory information.

No unit in the Configuration subsystem is used.

#### Callers

- **Library consumers** - select the files to check, typically passing the values of a `TextLintSettings`:
  `FileSelector.Select(root, settings.Files, settings.IgnorePaths, settings.EnableGlobDot)`.
- **The textlint tool** - will select files in a later change.
