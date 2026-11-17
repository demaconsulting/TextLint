### FileSelector

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`FileSelector` applies include and ignore glob patterns to a root directory and returns the files to check. It is
the one place that turns glob patterns into a file list, so every consumer selects files identically. It takes
its inputs as plain parameters, so it has no dependency on the Configuration subsystem. The include patterns are an
ordered list (see `OrderedGlobList`): a pattern that starts with `!` excludes, and the last pattern that matches a
file decides. It also defines the supporting type `SelectedFile` and three internal directory views,
`HiddenFilteringDirectory`, `OrderedPruningDirectory`, and `CancelableDirectory`, which have no unit of their own.

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

**OrderedPruningDirectory** is an internal view over the matcher's directory abstraction, used only when the include
list has a negation. It holds the wrapped directory, its root-relative path, and the `OrderedGlobList`. When
enumerated, it drops each subdirectory for which `OrderedGlobList.IsSubtreeExcluded` is true, that is, a directory
whose every file the ordered rules exclude and that no later pattern can bring back, and wraps every subdirectory it
keeps so the rule applies at every depth. `GetDirectory` applies the same test and reports a pruned directory as
missing, and files pass through unchanged, because the exact decision for a file is made after the match. The
matcher can only exclude a directory natively through a plain exclude pattern, which cannot express "excluded unless a
later pattern includes it again", so the pruning has to happen in the directory view. A directory that the list
is not certain about stays in the walk, which only costs time.

**CancelableDirectory** is an internal view over the matcher's directory abstraction. It holds the wrapped
directory and the caller's cancellation token. Before it lists a directory, and before it hands each entry of the
listing to the matcher, it checks the token and throws `OperationCanceledException` when it is canceled; it wraps
every subdirectory so the check applies at every depth. The matcher is synchronous and has no cancellation of
its own, so the only place the walk can be interrupted is inside the directory abstraction it reads.

#### Key Methods

**Select (public)**: Selects files beneath a directory path.

- *Parameters*: `string rootDirectory`; `IReadOnlyList<string> includePatterns`, an ordered list in which a leading `!`
  excludes, the last matching pattern decides, and the first pattern may not be negated; `IReadOnlyList<string>
  ignorePatterns`, which cannot be negated and always win; `bool enableGlobDot` (default `false`); `CancellationToken
  cancellationToken` (default none).
- *Returns*: a read-only list of `SelectedFile`, ordered by ordinal relative path.
- *Preconditions*: no argument is null, the directory name is not empty, and the directory exists and can be
  read. The
  pattern lists are read once during the call and are not retained, so the caller may change them afterwards.
- *Postconditions*: the list is a snapshot; no file has been opened. When the token is canceled the call throws
  and returns no list.

**Select (internal)**: The same selection over an abstract directory. It is a test seam: tests pass a
recording directory to prove which directories are enumerated, which is deterministic where a timing test
would not be, and a recording directory that cancels the token at a chosen point of the walk, to prove that a
cancellation stops the walk. The public method validates the path and calls it.

**ProbeRoot (internal)**: Reads the first entry of the root directory, after the patterns have been validated
and before the walk, so a root that is missing or unreadable is reported instead of selecting nothing.

- *Parameters*: `string rootDirectory` (for the message); `Func<IEnumerable<FileSystemInfo>> enumerateRoot`, the
  listing of the real root directory. Passing the listing in is the test seam, so tests supply each failure on
  every platform.
- *Postconditions*: returns normally when the first entry can be read or the directory is empty; a
  directory-not-found failure becomes `DirectoryNotFoundException` naming the root; every other failure
  propagates unchanged.

Steps: (1) parse the include patterns into an `OrderedGlobList` and expand every ignore pattern with
`GlobPatternExpander`, so an invalid pattern
fails first, even when nothing is included; (2) for the public method, throw when the token is already canceled and
probe the root directory with
`ProbeRoot`; (3) return an empty list when there are no include patterns, without walking any directory; (4) build a
matcher with ordinal comparison and add the expanded patterns of the plain (non-negated) include patterns, which are a
superset of what the list can select, and the expanded ignore patterns as excludes; (5) unless `enableGlobDot` is
set, wrap the root in a `HiddenFilteringDirectory`, when the list has a negation wrap the result in an
`OrderedPruningDirectory`, and always wrap the result in a `CancelableDirectory`, then walk it; (6) run the
matcher; (7) decide each candidate exactly: when the list has a negation, keep the paths for which
`OrderedGlobList.IsMatch` is true (last match wins, with the hidden-name rule applied per pattern); otherwise, unless
`enableGlobDot` is set, keep only the paths `HiddenPathPolicy` permits; (8) sort the
paths by ordinal comparison and create the `SelectedFile` entries, computing each full path from the root.

A list without any `!` takes exactly the walk it always had (steps 4 to 7 without the pruning view and the ordered
decision), so it selects exactly what the same patterns select as plain includes. The matcher ignores the order of
patterns, so with a negation it is only a prefilter, and the ordered decision of step 7 is what makes the last match
win. The ignore patterns stay a hard fence: they are matcher excludes, they prune natively, they always win, and they
cannot be negated.

The result is ordered with the ordinal comparer, not the file system's order, so it is identical on every
platform. Matching is case-sensitive because the matcher is constructed with ordinal comparison; its default is
case-insensitive. Excluded directories are skipped by the matcher itself, because the expansion of a bare name
yields an exclude form that covers the whole subtree. Directories that only the ordered include rules exclude are
skipped by the `OrderedPruningDirectory`.

#### Error Handling

A null argument throws `ArgumentNullException`; an empty or white-space root throws
`ArgumentException`; a root that does not exist throws `DirectoryNotFoundException`; an unsupported pattern
propagates `InvalidGlobPatternException` from the expander (for an include pattern, from `OrderedGlobList`, which
also throws it for a negated first pattern; an ignore pattern that starts with `!` is rejected by the expander).
The root is probed by reading its first entry, not by
asking whether it exists, because the existence check reports an unreadable directory as missing. Only a
directory-not-found failure from that read becomes `DirectoryNotFoundException` (with the original failure as its
inner exception); an access failure (`UnauthorizedAccessException`) or any other `IOException`, such as a root that
names a file, propagates with its own type. Below the root, the matcher's directory wrapper treats a subdirectory
that no longer exists when it is listed (removed while the walk runs) as empty; every other failure to list a
subdirectory, such as a permission failure, propagates unchanged. A canceled token throws
`OperationCanceledException`, from the check before the probe or from the `CancelableDirectory` during the walk;
no partial selection is returned. Nothing is logged.

#### Dependencies

- **OrderedGlobList** - parses the include patterns and makes the ordered decision; **GlobPatternExpander** expands
  the ignore patterns; **HiddenPathPolicy** - the hidden-name rule.
- **Microsoft.Extensions.FileSystemGlobbing** - the matcher and the directory abstraction. See
  *FileSystemGlobbing Integration Design*. No type from it appears in a public signature.
- **.NET BCL** - paths and directory information.

No unit in the Configuration subsystem is used.

#### Callers

- **Library consumers** - select the files to check, typically passing the values of a `TextLintSettings`:
  `FileSelector.Select(root, settings.Files, settings.IgnorePaths, settings.EnableGlobDot)`.
- **The textlint tool** - selects files through the library's `SpellChecker`, which calls this unit.
