### CommandLinePathResolver

![FileSelection Structure](FileSelectionView.svg)

#### Purpose

`CommandLinePathResolver` turns the path and pattern arguments a person types on a command line into a search
directory and validated portable patterns for `FileSelector`. Patterns in a settings file are strict on purpose
(forward slashes, no absolute paths), so one file selects the same files on every platform. A command line is
different: PowerShell completes `.\docs\guide.md`, an editor passes `C:\work\project\README.md`, and the
user expects the output to show paths in the form they typed. This unit is the one place that bridges the
two, so every program built on the library reads command-line paths identically and the strict rules of the
settings file stay untouched. It also defines the supporting result type `CommandLineSelection`.

#### Data Model

`CommandLinePathResolver` is a public static class with no state. Named constants hold the separators, the
current-directory and parent-directory segments, the `**` pattern that selects a whole directory, the drive
prefix length, and the characters that make a path segment a pattern (`*`, `?`, `{`, `}`).

**CommandLineSelection** is a public immutable class with three properties: `RootDirectory`, the absolute search
directory in the form of the current operating system; `Patterns`, the validated patterns relative to that
directory (an immutable copy, never empty); and `ReportsAbsolutePaths`, which is true exactly when any argument
was absolute. Its method `GetDisplayPath` returns the root-relative path unchanged when relative paths are
reported, and otherwise the absolute path of the file. Only the resolver creates it.

Two private record types support the algorithm. `PathParts` holds a root prefix (a drive such as `C:`, a network
root such as `//server/share`, or empty) and the directory segments beneath it, and renders itself with forward
slashes. `ParsedArgument` holds the argument as typed, the literal directory of an absolute argument (absent for
a relative one), and the pattern text.

#### Key Methods

**Resolve (public)**: Resolves arguments for the current operating system.

- _Parameters_: `string rootDirectory` - the directory relative arguments are relative to, which may itself be
  relative to the current directory; `IReadOnlyList<string> arguments` - the arguments as typed.
- _Returns_: a `CommandLineSelection`.
- _Preconditions_: the root is not null, empty, or white space; the list is not null, not empty, and has no null.
- _Postconditions_: every pattern has passed the same validation as a settings-file pattern.

It makes the root absolute with the platform's own path logic, calls the internal overload with the Windows rules
when running on Windows and the Unix rules otherwise, and converts the portable search directory it returns to
the platform form. It reads the current directory only when the root is relative and touches no other file
system entry; the existence of the root is checked later by `FileSelector`.

**ResolveWithStyle (internal)**: Resolves arguments under a chosen set of path rules. It is the test seam that
lets both rule sets be proven on every operating system. Steps:

1. Parse the root into a prefix and segments.
2. Parse each argument. On Windows rules a backslash is first converted to `/`. An argument is absolute when,
   under Windows rules, it starts with a drive letter and `/` or with `//` and a server and share, and under the
   other rules when it starts with `/`. A relative argument is kept as a pattern. An absolute argument is
   split at its first segment that contains a wildcard character: the literal segments before it form the
   directory (with `.` and empty segments dropped and `..` removing the previous segment) and the rest, as typed,
   is the pattern. Without a wildcard the argument names a directory when it ends with a separator or names no
   segment, and the pattern is `**`; otherwise its last segment is a file name and the earlier segments are the
   directory.
3. Choose the search directory. When every absolute argument lies inside the root (same prefix and the root's
   segments as a leading run, compared ignoring case on Windows rules and exactly otherwise), the root is the
   search directory, so an absolute argument means exactly what its relative form means and ignore patterns
   apply to it. Otherwise the search directory is the deepest directory that contains every absolute argument.
4. Express each argument as a pattern. A relative argument is used as it is. An absolute one becomes its path
   below the search directory, joined to its pattern and anchored with a leading `/`, so that a file name matches
   only there and not at any depth.
5. Validate each pattern with `GlobPatternExpander` and, on rejection, report the same reason against the
   argument as typed.

The two rules that look absolute but are not well defined on Windows are rejected: a drive-relative path
(`C:x`) and a path that starts with a single separator but names no drive. A network path needs a literal server
and share. A tilde is never expanded; it stays part of a relative pattern.

**GetDisplayPath (CommandLineSelection)**: Returns the relative path as given when `ReportsAbsolutePaths` is
false, and otherwise the full path of the root joined to the relative path, with the platform's separators. The
argument must not be null.

#### Error Handling

Programming errors throw `ArgumentNullException` (a null root, list, or `GetDisplayPath` argument) and
`ArgumentException` (an empty root, an empty list, a null element, or a root that is not absolute). Input that a
person typed wrongly throws `InvalidGlobPatternException`, which names the argument as typed and the reason: the
reasons of `GlobPatternExpander` (empty, negated, `?`, backslash outside Windows, `..` after a wildcard, empty
segment, unbalanced braces), a drive-relative path, a rooted path without a drive, an incomplete network path, or
a `..` that climbs above the root of the file system. Two combinations that cannot be searched from one
directory throw `ArgumentException` with an explanation: a relative argument together with an absolute argument
outside the root, and absolute arguments on different drives or network shares. Nothing is caught and swallowed,
and nothing is logged.

#### Dependencies

- **GlobPatternExpander** - validates each resulting pattern and supplies the reason for a rejection.
- **InvalidGlobPatternException** - the error for a rejected argument; its internal reason is reused so the
  message names the typed argument.
- **.NET BCL** - strings, collections, and `Path` for the platform form of the search directory.

#### Callers

- The command-line tool (through the library's public API) - resolves the positional arguments of a run.
- Any other program that accepts paths from a person at a shell.
