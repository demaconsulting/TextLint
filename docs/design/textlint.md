# TextLint

![TextLint Structure](TextLintView.svg)

## Architecture

TextLint is the `DemaConsulting.TextLint` .NET class library. It is the reusable core of the
TextLint product: the `textlint` command-line tool (see the _TextLintTool_ design) consumes it,
and any other .NET application may reference it directly.

The library is currently a package skeleton. It contains no subsystems or units and exposes no
public types; its purpose at this stage is to establish the package identity, the build and
packaging configuration, and the compatibility constraints that every later addition must keep.

**Layering.** The library owns **all linting logic**: settings and configuration loading, file
discovery and globbing, dictionaries, tokenization, spell checking, and results. Each of these is
added in later changes as a subsystem of this system with its own requirements, design, and
verification documents. The library is self-contained: it never depends on the tool, so any .NET
application can lint without it. The command-line tool is only a thin front-end over this
library and is not permitted to carry linting logic of its own.

## External Interfaces

**NuGet Package**: The distribution interface for consumers.

- _Type_: NuGet package.
- _Role_: Provider.
- _Contract_: The package is named `DemaConsulting.TextLint`, carries the README, icon, MIT license
  expression, and source-link metadata, and ships a `net10.0` assembly with XML documentation and
  a symbols package.
- _Constraints_: The package contains no native assets and has no runtime package dependencies.

**Public API**: The programmatic interface for consuming applications.

- _Type_: .NET assembly.
- _Role_: Provider.
- _Contract_: None yet - the library exposes no public types in this release.
- _Constraints_: Every public type added later must be trim and AOT safe, because the tool is
  planned to be published as a Native AOT executable.

## Dependencies

- **Runtime**: none. The library has no OTS runtime dependencies.
- **Build time**: SourceLink and the SBOM targets package (both private to the build), and the
  .NET and Sonar analyzers. None of these flow to consumers of the package.
- **Consumers**: the `DemaConsulting.TextLint.Tool` project depends on this library, never the
  reverse. The tool currently uses none of its types; the reference establishes the dependency
  direction that later changes build on.

## Risk Control Measures

N/A - not a safety-classified software item.

## Data Flow

N/A - the library exposes no operations yet, so there is no input-to-output data flow to describe.

## Design Constraints

- Platform: targets net10.0 only; the platform requirements apply on Windows, Linux, and macOS.
- Trimming and Native AOT: the library enables the trim, AOT, and single-file analyzers
  (`IsAotCompatible`) with warnings treated as errors, so reflection-dependent or otherwise
  AOT-unsafe code cannot be added without failing the build. The assembly is marked trimmable.
  The tool is planned to be published as a Native AOT executable in a later change; that change
  will add the real AOT-publish verification, and this library only guarantees the analyzer
  configuration until then.
- Packaging: the library and the tool are packed and published as separate NuGet packages from the
  same repository and version.
- Dependencies: runtime dependencies must be kept minimal, because the tool is planned to be
  published as a self-contained Native AOT executable.
- Layering: all linting logic belongs in this library. The library must not reference the tool,
  and must not depend on console output, command-line parsing, or process exit codes, which are
  the tool's concerns.
