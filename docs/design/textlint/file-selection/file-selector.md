### FileSelector

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`FileSelector` applies include and ignore glob patterns to a root directory and returns the files to check. It is
the one place that turns glob patterns into a file list, so every consumer selects files identically. It takes
its inputs as plain parameters, so it has no dependency on the Configuration subsystem. It also defines the
supporting type `SelectedFile` and two internal directory views, `HiddenFilteringDirectory` and
`CancellableDirectory`, which have no unit of their own.

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

**CancellableDirectory** is an internal view over the matcher's directory abstraction. It holds the wrapped
directory and the caller's cancellation token. Before it lists a directory, and before it hands each entry of the
listing to the matcher, it checks the token and throws `OperationCanceledException` when it is cancelled; it wraps
every subdirectory so the check applies at every depth. The matcher is synchronous and has no cancellation of
its own, so the only place the walk can be interrupted is inside the directory abstraction it reads.

#### Key Methods

**Select (public)**: Selects files beneath a directory path.

- _Parameters_: `string rootDirectory`; `IReadOnlyList<string> includePatterns`; `IReadOnlyList<string>
  ignorePatterns`; `bool enableGlobDot` (default `false`); `CancellationToken cancellationToken` (default none).
- _Returns_: a read-only list of `SelectedFile`, ordered by ordinal relative path.
- _Preconditions_: no argument is null, the directory name is not empty, and the directory exists and can be
  read. The
  pattern lists are read once during the call and are not retained, so the caller may change them afterwards.
- _Postconditions_: the list is a snapshot; no file has been opened. When the token is cancelled the call throws
  and returns no list.

**Select (internal)**: The same selection over an abstract directory. It is a test seam: tests pass a
recording directory to prove which directories are enumerated, which is deterministic where a timing test
would not be, and a recording directory that cancels the token at a chosen point of the walk, to prove that a
cancellation stops the walk. The public method validates the path and calls it.

**ProbeRoot (internal)**: Reads the first entry of the root directory, after the patterns have been validated
and before the walk, so a root that is missing or unreadable is reported instead of selecting nothing.

- _Parameters_: `string rootDirectory` (for the message); `Func<IEnumerable<FileSystemInfo>> enumerateRoot`, the
  listing of the real root directory. Passing the listing in is the test seam, so tests supply each failure on
  every platform.
- _Postconditions_: returns normally when the first entry can be read or the directory is empty; a
  directory-not-found failure becomes `DirectoryNotFoundException` naming the root; every other failure
  propagates unchanged.

Steps: (1) expand every include and ignore pattern with `GlobPatternExpander`, so an invalid pattern
fails first, even when nothing is included; (2) for the public method, throw when the token is already cancelled and
probe the root directory with
`ProbeRoot`; (3) return an empty list when there are no include patterns, without walking any directory; (4) build a
matcher with ordinal comparison and add the expanded include and
exclude patterns; (5) unless `enableGlobDot` is set, wrap the root in a `HiddenFilteringDirectory`, and always wrap
the result in a `CancellableDirectory`, then walk it; (6) run the
matcher; (7) unless `enableGlobDot` is set, keep only the paths `HiddenPathPolicy` permits; (8) sort the
paths by ordinal comparison and create the `SelectedFile` entries, computing each full path from the root.

The result is ordered with the ordinal comparer, not the file system's order, so it is identical on every
platform. Matching is case-sensitive because the matcher is constructed with ordinal comparison; its default is
case-insensitive. Excluded directories are skipped by the matcher itself, because the expansion of a bare name
yields an exclude form that covers the whole subtree.

#### Error Handling

A null argument throws `ArgumentNullException`; an empty or white-space root throws
`ArgumentException`; a root that does not exist throws `DirectoryNotFoundException`; an unsupported pattern
propagates `InvalidGlobPatternException` from the expander. The root is probed by reading its first entry, not by
asking whether it exists, because the existence check reports an unreadable directory as missing. Only a
directory-not-found failure from that read becomes `DirectoryNotFoundException` (with the original failure as its
inner exception); an access failure (`UnauthorizedAccessException`) or any other `IOException`, such as a root that
names a file, propagates with its own type. Below the root, the matcher's directory wrapper treats a subdirectory
that no longer exists when it is listed (removed while the walk runs) as empty; every other failure to list a
subdirectory, such as a permission failure, propagates unchanged. A cancelled token throws
`OperationCanceledException`, from the check before the probe or from the `CancellableDirectory` during the walk;
no partial selection is returned. Nothing is logged.

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
