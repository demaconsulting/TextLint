# Introduction

This document contains the requirements for the TextLint project.

## Purpose

TextLint is a fast text linter for source code and documentation, delivered as a .NET library and a
.NET command-line tool. The requirements cover the library (settings, file selection, dictionaries, and
spell checking), the `textlint` command-line tool (checking files, settings discovery, options, output and exit
codes, and self-validation), and the supporting documentation generation.

## Scope

This requirements document covers:

- Spell checking of files from the command line, with its options, output, and exit codes
- Command-line interface and options
- Self-validation framework
- Test result output formats
- Logging capabilities
- Multi-platform support
- Documentation generation
- CI/CD integration

## Audience

This document is intended for:

- Software developers working on TextLint
- Quality assurance teams validating requirements
- Project stakeholders reviewing project scope
- Users understanding the tool's capabilities
