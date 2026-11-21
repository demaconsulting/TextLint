# Project Overview

- **project-name**: TextLint
- **organization**: DEMA Consulting
- **project-tagline**: Fast text linting for code and documentation
- **description**: TextLint is a fast text linter for source code and documentation,
  delivered as the `DemaConsulting.TextLint` .NET library and the
  `DemaConsulting.TextLint.Tool` .NET tool (command `textlint`) built on it. It is being
  developed to provide cspell-compatible spell-checking using embedded English and
  technical-term dictionaries, configured by a `.textlint.yaml` file, checking files in
  parallel across all processor cores.
- **languages**: C#
- **technologies**: .NET

# Architecture Layering (ALL Agents)

ALL linting logic - settings/config loading, file discovery/globbing, dictionaries,
tokenization, spell-checking, and results - belongs in the `DemaConsulting.TextLint` library.
`DemaConsulting.TextLint.Tool` is a thin CLI front-end only (argument parsing, console/log
output, exit codes, self-validation, and calling the library). The tool depends on the library;
the library never depends on the tool. Never add linting logic to the tool.

# Project Structure

```text
├── dictionaries/
├── docs/
│   ├── build_notes/
│   ├── code_quality/
│   ├── code_review_plan/
│   ├── code_review_report/
│   ├── design/
│   ├── requirements_doc/
│   ├── requirements_report/
│   ├── reqstream/
│   ├── sysml2/
│   ├── user_guide/
│   └── verification/
├── src/
│   ├── DemaConsulting.TextLint/
│   ├── DemaConsulting.TextLint.DictionaryBuilder/
│   └── DemaConsulting.TextLint.Tool/
├── test/
│   ├── DemaConsulting.TextLint.Tests/
│   ├── DemaConsulting.TextLint.DictionaryBuilder.Tests/
│   └── DemaConsulting.TextLint.Tool.Tests/
├── third-party/
└── tools/
    └── javascript-oracles/
```

# Language and Spelling (ALL Agents)

Always use **US English** spelling in all output (code, comments, documentation,
commit messages, and reports).

# Reference Template

This repository follows a reference template for structure and file conventions.

- **template-url**: `https://github.com/demaconsulting/Agents/raw/refs/heads/template`
- **Repository map**: `{template-url}/repository-map.md`
- **Template files**: `{template-url}/{file-path}` for files described in the map

# Codebase Navigation (ALL Agents)

When working with source code, design, or requirements artifacts, query the SysML2
architecture model under `docs/sysml2/` first (see the `sysml2tools-query` skill) to
understand software structure, purpose, and relationships. Fall back to
`docs/design/introduction.md` for the human-facing narrative, folder layout, and
companion artifact locations, and use it as the primary map when the model doesn't
yet cover something.

# Key Configuration Files

- **`.config/dotnet-tools.json`** - Local tool manifest for Continuous Compliance tools
- **`.editorconfig`** - Code formatting rules
- **`.clang-format`** - C/C++ formatting (if applicable)
- **`.textlint.yaml`** - Spell-check configuration and technical term dictionary (TextLint)
- **`.lintmark.yaml`** - Markdown formatting rules (LintMark)
- **`.yamllint.yaml`** - YAML formatting configuration
- **`.reviewmark.yaml`** - File review definitions and tracking
- **`nuget.config`** - NuGet package sources (if .NET)
- **`package.json`** - Node.js dependency (cspell) used only by the cspell parity recorder
  (`test/DemaConsulting.TextLint.Tests/SpellChecking/Parity/record-cspell.ps1`); not used by lint or CI
- **`requirements.yaml`** - Root requirements file with includes
- **`pip-requirements.txt`** - Python dependencies for yamllint and yamlfix
- **`docs/sysml2/`** - SysML2 architecture model; authoritative source for software structure
- **`fix.ps1`** - Applies all auto-fixers silently (dotnet format, LintMark, YAML). Always exits 0.
- **`build.ps1`** - Builds the solution and runs all tests.

# Standards Application (ALL Agents Must Follow)

Before performing any work, agents must read and apply the relevant standards
from `.github/standards/`. Use this matrix to determine which to load:

- **Any code**: `coding-principles.md`
- **C# code**: `coding-principles.md`, `csharp-language.md`
- **Any tests**: `testing-principles.md`
- **C# tests**: `testing-principles.md`, `csharp-testing.md`
- **Requirements**: `requirements-principles.md`, `software-items.md`, `reqstream-usage.md`
- **Design docs**: `software-items.md`, `design-documentation.md`, `technical-documentation.md`
- **Verification docs**: `software-items.md`, `verification-documentation.md`, `technical-documentation.md`
- **Review configuration**: `software-items.md`, `reviewmark-usage.md`
- **Software structure**: `sysml2-modeling.md`
- **Diagrams in documentation**: `diagram-documentation.md` (and the `canvasnet` skill for tooling)
- **Any documentation**: `technical-documentation.md`
- **Reading PDF or Office documents**: the `docdown` skill

Load only the standards relevant to your specific task scope.

# Agent Delegation Guidelines

The default agent should handle simple, straightforward tasks directly.
Delegate to specialized agents only for specific scenarios:

- **Pre-PR lint cleanup** (fix all lint issues before pull request) → Call the lint-fix agent
- **Scoped fixes with no new user-visible behavior** (PR review comments, doc
  corrections, known bug fixes with defined root cause) → Call the developer agent
- **Light quality checking** (basic validation) → Call the quality agent
- **Any change introducing new user-visible behavior** (features, enhancements,
  new commands or options) → Call the implementation agent
- **Formal bug resolution** (complex debugging, unknown root cause) → Call the implementation agent
- **Formal reviews** (compliance verification, detailed analysis) → Call the formal-review agent
- **Structural audit**: (repository layout vs. template) → Call the template-sync agent
- **Implementation planning only** (review a plan before committing to implementation) → Call the planning agent

# Agent Reporting (Specialized Agents Must Follow)

Specialized agents MUST generate a completion report:

1. Save to `.agent-logs/{agent-name}-{subject}-{unique-id}.md`
   where `{subject}` is a kebab-case task summary (max 5 words) and
   `{unique-id}` is a short unique suffix (e.g., 8-char hex or timestamp)
2. Start with `**Result**: (SUCCEEDED|FAILED|INCOMPLETE)` as the first metadata field
3. Include the agent-specific report sections defined in each agent's prompt
4. Return the summary to the caller

Result semantics for orchestrator decision-making:

- **SUCCEEDED**: Work completed and all quality gates applicable to that agent's scope met
- **FAILED**: Work could not be completed or quality gates not met
- **INCOMPLETE**: Work cannot proceed without information only the user can
  provide (implementation, planning, and template-sync agents)

# Formatting (After Making Changes)

After making changes, run the auto-fix pass. This applies all available fixers
silently and **always exits 0** - agents do not need to respond to its output.

```pwsh
pwsh ./fix.ps1
```

This automatically handles: `dotnet format`, Markdown formatting (LintMark), and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1` which checks: lintmark, textlint, yamllint, dotnet format,
reqstream, versionmark, reviewmark, and sysml2tools.

# Scope Discipline (ALL Agents Must Follow)

- **No generated file access**: Files inside any `generated/` folder are build
  outputs - do not read, lint, or modify them
- **Minimum necessary changes**: Only modify files directly required by the task
- **No speculative refactoring**: Do not refactor code adjacent to the change
  unless the task explicitly requests it
- **No drive-by fixes**: If you discover pre-existing issues in files you are
  reading but not modifying, document them in the report but do not fix them
- **Declare scope upfront**: Before making changes, determine which files will be
  modified. Any file outside this scope requires explicit justification.

# Protected Configuration Files

These files contain carefully designed configuration with documented intent
in header comments. Agents MUST NOT modify them unless the task explicitly
requires it and the modification preserves the documented design intent:

- `.reviewmark.yaml`, `.textlint.yaml`, `.editorconfig`
- `.lintmark.yaml`, `.yamllint.yaml`
- `requirements.yaml`

# Project Scripts

`build.ps1`, `lint.ps1`, and `fix.ps1` belong to the project: extend them as it grows. Add project-specific
steps, update tool versions, and add optional parameters that focus a run (for example one library in a
multi-library repository, or headless tests without GUI automation). Two rules keep them trustworthy:

- Run with no arguments, each script still does its full job (CI and the agents rely on that); slow or
  environment-dependent test tiers may be opt-in switches of `build.ps1`.
- Never remove or weaken a check to make lint or tests pass.

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).

# Repository-Specific Standards Exceptions

## SysML2 package structure (legacy layout, migration pending)

The model under `docs/sysml2/` predates the one-package-per-file rule: `package TextLint` is declared in 28 files
(including `model/ots.sysml` and `views/design-views.sysml`), `package TextLintTool` in 12, and
`package DictionaryBuilder` in 8. OTS parts live in `TextLint` (not `OtsDependencies`) and the views reopen the three
system packages (no `TextLintDesignViews` package) and still use the unparenthesized
`filter not @SysML::Documentation;`. The
Package Structure rules in `sysml2-modeling.md` and the qualified-name examples in the `sysml2tools-query` skill apply
once the model is migrated in a dedicated PR; until then, agents use the existing package names and must not partially
restructure the model.
