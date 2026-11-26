# OTS Software Integration Design

This document provides the integration and usage design for all Off-The-Shelf (OTS) software
items used by TextLint: the runtime dependencies of the `DemaConsulting.TextLint` library, which
flow to consumers of its package, the runtime dependency of the `textlint` tool, and the build, test,
and documentation tooling used by the repository.

## Scope

OTS items are third-party tools and libraries consumed by the project. This document covers the
integration pattern and usage design for each item. The internal design of OTS items is out of
scope; only how this project integrates and uses each item is documented.

The **runtime** OTS items are YamlDotNet and Microsoft.Extensions.FileSystemGlobbing, which are
package references of the library, and HelpCards, which is a package reference of the tool; all three
are modeled in the SysML2 architecture model. All other items are build-time, test-time, or pipeline
tooling that does not ship to consumers.

## OTS Items

The following OTS items have integration design documentation:

- BuildMark (*buildmark.md*) — build-notes documentation tool
- FileAssert (*fileassert.md*) — document assertion tool
- FileSystemGlobbing (*filesystemglobbing.md*) — glob pattern matching library (runtime)
- HelpCards (*help-cards.md*) — built-in help library (runtime, tool)
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

## References

N/A
