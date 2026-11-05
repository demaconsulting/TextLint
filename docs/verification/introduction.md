# Introduction

This document provides the verification design for TextLint, which is delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET command-line
tool, and provides fast text linting for code and documentation.

## Purpose

The purpose of this document is to describe how each requirement for TextLint is
verified. For every software item — system, subsystem, and unit — this document names the
verification approach, identifies the test scenarios (including boundary conditions and error
paths), describes what is mocked or stubbed, and maps each requirement to at least one named
test scenario. The document does not restate design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items:

**Local items:**

- **TextLint** — system, subsystem, and unit verification for the `DemaConsulting.TextLint` library:
  - **Configuration** subsystem
    - **TextLintSettings** — immutable settings value
    - **SettingsLoader** — settings-file loader
  - **FileSelection** subsystem
    - **GlobPatternExpander** — glob pattern validation and rewriting
    - **HiddenPathPolicy** — hidden-name rule
    - **FileSelector** — file selection
- **TextLintTool** — system, subsystem, and unit verification:
  - **Program** — entry point and execution orchestrator
  - **Cli** subsystem
    - **Context** — argument parser and I/O owner
  - **SelfTest** subsystem
    - **Validation** — self-validation test runner
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities
- **DictionaryBuilder** — system and unit verification for the build-time dictionary builder:
  - **BuilderProgram** — command line, orchestration, and error reporting
  - **WordListReader** — word-list reading, validation, and merging
  - **DictionaryWriter** — dictionary file serialization
  - **DictionaryFormat** — shared format definitions

**OTS items:**

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **FileSystemGlobbing** — glob pattern matching library (runtime)
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model validation and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework
- **YamlDotNet** — YAML parsing library (runtime)

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it. This includes the shared test support types
  (a temporary-directory helper, a recording directory used to observe enumeration, and for the
  dictionary builder a process runner and an independent reader of the dictionary file format).
- Build pipeline CI configuration is excluded
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
├── DemaConsulting.TextLint.Tests/       — library tests
│   ├── Configuration/                   — Configuration subsystem and unit tests
│   ├── FileSelection/                   — FileSelection subsystem and unit tests
│   └── TestSupport/                     — shared test helpers (temporary directory, recording directory)
├── DemaConsulting.TextLint.DictionaryBuilder.Tests/  — dictionary builder system and unit tests
│   └── TestSupport/                     — shared test helpers (temporary directory, process runner,
│                                          independent dictionary file reader)
└── DemaConsulting.TextLint.Tool.Tests/  — unit and integration tests
```

## Companion Artifact Structure

In-house items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- TextLint Software Design Document
- TextLint releases (<https://github.com/demaconsulting/TextLint/releases>)
