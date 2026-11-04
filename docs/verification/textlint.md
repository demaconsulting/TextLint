# TextLint

## Verification Approach

System-level verification uses unit-test-style checks in `TextLintTests.cs` that load the compiled
`DemaConsulting.TextLint` assembly by name and inspect its identity, metadata, target framework,
and trim-compatibility marker through reflection. The library exposes no public types yet, so
there is no behavior to exercise; the tests prove the package-level properties that the system
requirements state. No mocking is used.

Native AOT safety is additionally enforced at build time rather than by a test: the library
enables the trim, AOT, and single-file analyzers (`IsAotCompatible`) and builds with warnings
treated as errors, so any AOT-unsafe construct fails the build. The assembly-level test proves the
trim-compatibility declaration is present; the build proves the code honors it.

Packaging itself (the `.nupkg` and `.snupkg` produced by `dotnet pack`) is verified by the build
pipeline, which packs both packages on every build; it is not re-run from inside the test suite.

## Test Environment

Tests run under xUnit on .NET 10.0 on Windows, Linux, and macOS. The test project references the
library project, so the library assembly is present beside the test assembly. No files, services,
or special configuration are required.

## Acceptance Criteria

- All system-level tests pass on every supported platform with zero failures.
- The library assembly name equals the NuGet package name.
- The product and company metadata identify the package and DEMA Consulting.
- The assembly targets .NET 10.
- The assembly declares itself trimmable.
- The assembly does not reference the tool assembly.

## Test Scenarios

**TextLint_Assembly_Loaded_HasPackageName**: The library assembly is loaded by name and its simple
name is `DemaConsulting.TextLint`. This scenario also serves as the per-platform evidence that the
library builds and runs on Windows, Linux, and macOS. This scenario is tested by
`TextLint_Assembly_Loaded_HasPackageName`.

**TextLint_Assembly_Loaded_HasPackageMetadata**: The loaded assembly's product metadata is
`DemaConsulting.TextLint` and its company metadata is `DEMA Consulting`. This scenario is tested by
`TextLint_Assembly_Loaded_HasPackageMetadata`.

**TextLint_Assembly_Loaded_TargetsNet10**: The loaded assembly's target framework attribute names
.NET 10, proving the library supports the .NET 10 runtime. This scenario is tested by
`TextLint_Assembly_Loaded_TargetsNet10`.

**TextLint_Assembly_Loaded_IsMarkedTrimCompatible**: The loaded assembly carries the `IsTrimmable`
assembly metadata set to `True`, proving the library declares trim and Native AOT compatibility.
This scenario is tested by `TextLint_Assembly_Loaded_IsMarkedTrimCompatible`.

**TextLint_Assembly_Loaded_DoesNotReferenceTool**: The loaded assembly's referenced-assembly list
does not include `DemaConsulting.TextLint.Tool`, proving the library is usable without the tool
and that the dependency direction (tool depends on library) is not inverted. This scenario is
tested by `TextLint_Assembly_Loaded_DoesNotReferenceTool`.
