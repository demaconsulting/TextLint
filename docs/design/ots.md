# OTS Software Integration Design

This document provides the integration and usage design for all Off-The-Shelf (OTS) software
items used by TextLint: the runtime dependencies of the `DemaConsulting.TextLint` library, which
flow to consumers of its package, and the build, test, and documentation tooling used by the
repository.

## Scope

OTS items are third-party tools and libraries consumed by the project. This document covers the
integration pattern and usage design for each item. The internal design of OTS items is out of
scope; only how this project integrates and uses each item is documented.

The **runtime** OTS items are YamlDotNet and Microsoft.Extensions.FileSystemGlobbing; both are
package references of the library and are modeled in the SysML2 architecture model. All other
items are build-time, test-time, or pipeline tooling that does not ship to consumers.

## OTS Items

The following OTS items have integration design documentation:

- BuildMark (_buildmark.md_) — build-notes documentation tool
- FileAssert (_fileassert.md_) — document assertion tool
- FileSystemGlobbing (_filesystemglobbing.md_) — glob pattern matching library (runtime)
- Pandoc (_pandoc.md_) — Markdown-to-HTML conversion tool
- ReqStream (_reqstream.md_) — requirements traceability tool
- ReviewMark (_reviewmark.md_) — file review enforcement tool
- SarifMark (_sarifmark.md_) — SARIF report conversion tool
- SonarMark (_sonarmark.md_) — SonarCloud quality report tool
- SysML2Tools (_sysml2tools.md_) — architecture model validation and diagram rendering tool
- VersionMark (_versionmark.md_) — tool-version documentation tool
- WeasyPrint (_weasyprint.md_) — HTML-to-PDF conversion tool
- xUnit (_xunit.md_) — unit-testing framework
- YamlDotNet (_yamldotnet.md_) — YAML parsing library (runtime)

## References

N/A
