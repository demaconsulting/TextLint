# TextLint

## Verification Approach

System-level verification combines three kinds of evidence:

- **Package inspection**: after packing the library, the build pipeline runs FileAssert
  (`.fileassert.yaml`, tag `packages`) against the real `DemaConsulting.TextLint.<version>.nupkg`
  and its `.snupkg`. The checks assert the package id, the `lib/net10.0` assembly and XML
  documentation, and the publisher, license, README, and icon metadata. This is the evidence for
  the NuGet package requirement, because it inspects the artifact that is actually published.
  The library package id is a prefix of the tool package id, so the build asserts the library
  package after packing only the library and before packing the tool.
- **Project configuration**: FileAssert (tag `library-project`) checks that the library project
  file enables `IsAotCompatible` and `TreatWarningsAsErrors`, which is the source of the
  trim/AOT analyzer requirement.
- **Assembly inspection**: unit-test-style checks in `TextLintTests.cs` load the compiled
  `DemaConsulting.TextLint` assembly by name and inspect its identity, metadata, target framework,
  trim-compatibility marker, and referenced assemblies through reflection. The library exposes no
  public types yet, so there is no behavior to exercise. No mocking is used.

The trim and AOT analyzers run on every build with warnings treated as errors, so any trim- or
AOT-unsafe construct fails the build; the assembly-level test confirms the resulting assembly
carries the trim-compatibility marker. These checks verify analyzer configuration only. The tool
is not yet published as a Native AOT executable; the later change that introduces that publish is
responsible for adding verification that it actually works.

## Test Environment

Package and project checks run in the build job on Windows, Linux, and macOS, against the package
produced by `dotnet pack` in that job; FileAssert is the only tool required. Assembly tests run
under xUnit on .NET 10.0 on the same platforms. The test project references the library project,
so the library assembly is present beside the test assembly. No services or special configuration
are required.

## Acceptance Criteria

- All FileAssert package and project checks pass on every build.
- The library package contains exactly one `net10.0` assembly with its XML documentation, and its
  nuspec names the package `DemaConsulting.TextLint` with the publisher, MIT license, README, and
  icon metadata.
- A symbols package is produced alongside the library package.
- The library project enables `IsAotCompatible` and `TreatWarningsAsErrors`.
- All system-level tests pass on every supported platform with zero failures.
- The library assembly name equals the NuGet package name.
- The product and company metadata identify the package and DEMA Consulting.
- The assembly targets .NET 10.
- The assembly declares itself trimmable.
- The assembly does not reference the tool assembly.

## Test Scenarios

**TextLint_Package_ContainsNet10Library**: FileAssert inspects the produced library package and
finds exactly one `lib/net10.0/DemaConsulting.TextLint.dll` (and no other framework folder), its
XML documentation file, and the companion `.snupkg`. This scenario is tested by
`TextLint_Package_ContainsNet10Library`.

**TextLint_Package_HasPackageMetadata**: FileAssert inspects the nuspec inside the produced library
package and finds the id `DemaConsulting.TextLint`, the author, the MIT license expression, and the
README and icon references, with `README.md` and `Icon.png` present in the package. This scenario
is tested by `TextLint_Package_HasPackageMetadata`.

**TextLint_Project_EnablesTrimAotAnalyzersAsErrors**: FileAssert inspects the library project file
and finds `IsAotCompatible` and `TreatWarningsAsErrors` both set to `true`. This scenario is tested
by `TextLint_Project_EnablesTrimAotAnalyzersAsErrors`.

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
assembly metadata set to `True`, supporting the trim/AOT analyzer requirement: the marker is
emitted only when the project enables `IsAotCompatible`.
This scenario is tested by `TextLint_Assembly_Loaded_IsMarkedTrimCompatible`.

**TextLint_Assembly_Loaded_DoesNotReferenceTool**: The loaded assembly's referenced-assembly list
does not include `DemaConsulting.TextLint.Tool`, proving the library is usable without the tool
and that the dependency direction (tool depends on library) is not inverted. This scenario is
tested by `TextLint_Assembly_Loaded_DoesNotReferenceTool`.
