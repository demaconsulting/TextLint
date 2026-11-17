# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items used by
TextLint: the runtime dependencies of the `DemaConsulting.TextLint` library and the build, test, and
documentation tooling used by the repository.

## Scope

Each OTS item is verified by one of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool
- Transitive evidence from the library test suite, for the runtime dependencies (YamlDotNet and
  Microsoft.Extensions.FileSystemGlobbing), which the library's tests exercise for real, without test doubles

Internal OTS tool design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- BuildMark (*buildmark.md*) — build-notes documentation tool
- FileAssert (*fileassert.md*) — document assertion tool
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library (runtime)
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- xUnit (*xunit.md*) — unit-testing framework
- YamlDotNet (*yamldotnet.md*) — YAML parsing library (runtime)
