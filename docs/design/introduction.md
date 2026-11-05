# Introduction

This document provides the detailed design for TextLint, which is delivered as the
`DemaConsulting.TextLint` .NET library and the `DemaConsulting.TextLint.Tool` .NET command-line
tool, and provides fast text linting for code and documentation. It covers local
software items (systems, subsystems, and units) and the OTS software items they consume.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises TextLint. It captures data models, algorithms, key methods, and
inter-unit interactions at a level of detail sufficient for formal code review, compliance
verification, and future maintenance. A reviewer should be able to understand how each item
satisfies its requirements without reading source code. The document does not restate
requirements; it explains how they are realized.

## Scope

Local items:

- **TextLint**: system, subsystem, and unit design for the `DemaConsulting.TextLint` library:
  - **Configuration** subsystem: **TextLintSettings** (the immutable settings value) and
    **SettingsLoader** (reads a `.textlint.yaml` file, with its result and diagnostic types)
  - **FileSelection** subsystem: **GlobPatternExpander** (pattern validation and rewriting),
    **HiddenPathPolicy** (the hidden-name rule), and **FileSelector** (selects files, with its
    result and exception types)
  - **Dictionaries** subsystem: **SpellingDictionary** (the public word lookup and language
    selection) and **DictionaryImage** (loads, validates, and probes one embedded binary dictionary)
  - **TextScanning** subsystem: **WordTokenizer** (what a word is: words, tokens, case-change pieces, and
    word-list entries), **DefaultPatterns** (the text that is never checked), and **DocumentDirectives** (the
    directives that comments use to control checking, with its result type)
  - **SpellChecking** subsystem: **TextChecker** (checks a string against the dictionary and the word lists, with
    its finding types and word rules), **FileTextReader** (reads a file into text), and **SpellChecker** (checks the
    files of a directory in parallel, with its options and result types)
- **TextLintTool**: system, subsystem, and unit design for all local components of the
  command-line tool.
- **DictionaryBuilder**: system and unit design for the build-time program that converts the word
  lists under `dictionaries/` into the binary dictionary files embedded in the library. It is not
  packaged. Its units are **BuilderProgram** (command line and orchestration), **WordListReader**
  (reads and validates word lists, with its result and diagnostic types), **VariantFilter** (the
  spelling-variant rule for the technical lists, with its result type), **DictionaryWriter**
  (serializes a dictionary file), and **DictionaryFormat** (the definitions shared with the library).

OTS items:

- **BuildMark**: integration and usage design.
- **FileAssert**: integration and usage design.
- **FileSystemGlobbing** (Microsoft.Extensions.FileSystemGlobbing): integration and usage design.
- **Pandoc**: integration and usage design.
- **ReqStream**: integration and usage design.
- **ReviewMark**: integration and usage design.
- **SarifMark**: integration and usage design.
- **SonarMark**: integration and usage design.
- **SysML2Tools**: integration and usage design.
- **VersionMark**: integration and usage design.
- **WeasyPrint**: integration and usage design.
- **xUnit**: integration and usage design.
- **YamlDotNet**: integration and usage design.

The following topics are out of scope:

- Design documents are not produced for the test projects or build pipeline CI configuration
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The model captures the shipped systems' runtime composition: the local
items of the library and the tool and the runtime OTS items the library depends on (YamlDotNet and
Microsoft.Extensions.FileSystemGlobbing), and also the build-time DictionaryBuilder system, which is
not shipped but produces data that ships. The remaining OTS items listed in Scope above are
build-time and pipeline tooling, documented separately (see `docs/design/ots.md`) rather than
modeled as SysML2 parts.

![Software Structure](SoftwareStructureView.svg)

### Layering

The TextLint library and the TextLintTool tool form a strict two-layer architecture, and every later
change must preserve it:

- **TextLint (library)** owns **all linting logic**: settings and configuration loading, file
  discovery and globbing, dictionaries, tokenization, spell checking, and results. It is usable
  by any .NET application and never depends on the tool.
- **TextLintTool (command-line tool)** is a **thin front-end only**: argument parsing,
  console and log output, exit codes, self-validation, and calling the library. It contains no
  linting logic. The tool depends on the library, as shown by the dependency in the SysML2
  model; the dependency never runs in the other direction.

New linting capability is therefore added to the library as subsystems and units with their own
requirements, design, and verification, and the tool only gains the command-line options and
output needed to invoke it.

The third system, **DictionaryBuilder**, sits outside this layering: it is a build-time program that
neither the library nor the tool calls at run time. The library's build runs it to produce the
binary dictionary files that the library embeds, and the library compiles one of its source files
(`DictionaryFormat`) so that the writer and the reader of those files share their definitions. It depends
on nothing but the .NET base class library.

## Folder Layout

- **dictionaries/** - plain-text word lists, one sub-folder per dictionary (the technical dictionary's
  folder is split into `language/` and `general/` lists plus a reviewed `variant-keep.txt`), and the script
  that regenerates the imported lists (data, not a software item)
- **third-party/** - license notices for the third-party data in the word lists and the rules ported from cspell,
  packed into both NuGet packages
- **src/** - source files and projects
  - **DemaConsulting.TextLint/** - library source
    - **Configuration/** - settings and settings-file loading subsystem
    - **FileSelection/** - glob pattern and file selection subsystem
    - **Dictionaries/** - word lookup subsystem (the dictionary format source is linked from the
      DictionaryBuilder project)
    - **TextScanning/** - word, ignored-text, and directive subsystem (pure text logic; no file or setting access)
    - **SpellChecking/** - text checking, file reading, and parallel file checking subsystem
  - **DemaConsulting.TextLint.DictionaryBuilder/** - build-time dictionary builder source (five units)
  - **DemaConsulting.TextLint.Tool/** - main application source
    - **Cli/** - command-line interface subsystem
    - **SelfTest/** - self-validation subsystem
    - **Utilities/** - shared utilities subsystem

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- TextLint User Guide
- TextLint Repository (<https://github.com/demaconsulting/TextLint>)
