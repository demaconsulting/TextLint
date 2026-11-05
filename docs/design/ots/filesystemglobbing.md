## FileSystemGlobbing

This document describes the integration and usage design for the `Microsoft.Extensions.FileSystemGlobbing`
OTS software item.

### Purpose

Microsoft.Extensions.FileSystemGlobbing matches include and exclude glob patterns against a directory
tree. It is chosen so TextLint does not have to write and maintain its own glob matcher: it supports `*`
and `**`, exclusion that takes precedence over inclusion, and skipping of fully excluded directories during
the walk, and it works through a directory abstraction that can be wrapped. It is a runtime dependency of
the library and flows to consumers of the package.

### Features Used

- `Matcher`, constructed with an explicit string comparison so matching is case-sensitive on every
  platform (the default is case-insensitive), with `AddIncludePatterns` and `AddExcludePatterns`
- `Matcher.Execute` over a `DirectoryInfoBase`, and the result's matched file paths
- `DirectoryInfoWrapper` over `DirectoryInfo` for real directories
- `DirectoryInfoBase`, `FileSystemInfoBase`, and `FileInfoBase` as the abstraction that the library
  wraps to hide directories from the walk and that tests wrap to record enumeration

Behaviors the library relies on: comparison is controlled by the caller, and an exclude pattern that
covers a whole directory prevents that directory from being enumerated (both proven by tests); and
the matcher is not aware of hidden names. The matcher has no brace alternatives, no negation, and no
`?` wildcard, and it does not treat a bare name as matching at any depth; the library therefore expands
and validates patterns itself (see the _GlobPatternExpander_ design) and implements the hidden-name
rule itself (see the _HiddenPathPolicy_ design).

### Integration Pattern

FileSystemGlobbing is consumed as a package reference of the library. `FileSelector` is the only unit
that uses the matcher: each selection creates a new `Matcher`, adds the expanded patterns, and executes it
over the root directory, wrapped in a hidden-name filter when hidden matching is off. No state is
shared between selections and nothing needs disposal or initialization. FileSystemGlobbing types are
confined to `FileSelector` and its directory view and appear in no public signature, so the package
can be upgraded without changing the library's public API. Symbolic links and junctions are handled
however the package's directory wrapper handles them.
