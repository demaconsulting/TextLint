### LintReporter

![Linting Structure](LintingView.svg)

#### Purpose

`LintReporter` writes the result of one check: each finding, each file that was skipped or could not be checked,
and a one-line summary. It decides the order, which file name is shown, and the summary, and nothing else: the
wording of each finding and each per-file line belongs to the library, so the tool never restates it.

#### Data Model

`LintReporter` is an internal static class with no state.

#### Key Methods

**Write**: Writes the whole result.

- _Parameters_: `Context context`; `SpellCheckResult result`; `Func<string, string> displayPath`, which maps a
  path relative to the search directory to the name to show.
- _Returns_: `void`.
- _Preconditions_: no argument is null.
- _Postconditions_: the lines have been written to the context; the exit status is unchanged.

It validates its arguments, then writes the findings, the file diagnostics, and the summary, in that order.

**WriteIssues (private)**: Writes one line per finding, in the order the library gives (file path, line,
column), formatted by `SpellingIssue.Format` under the display name of the finding's file. A finding without a
file name is formatted without one.

**WriteFileDiagnostics (private)**: Writes one line per file that was skipped or could not be checked, in the
order the library gives (file path), formatted by `FileDiagnostic.Format` under the display name. A deliberate
skip (a binary or oversized file) reads as information and a file that could not be checked as an error, so that
nothing is silently left out.

**WriteSummary (private)**: Writes the line
`Checked N file(s): M issue(s) in K file(s), S skipped, E could not be checked.` where `N` is the number of files
checked completely, `M` the number of findings, `K` the number of distinct files that have findings, `S` the
number of files skipped on purpose, and `E` the number of file diagnostics that are errors.

#### Error Handling

A null argument throws `ArgumentNullException` before anything is written. Writing does not otherwise fail; the
context's output channels do not throw. Nothing is caught.

#### Dependencies

- **Context** (Cli subsystem) - receives every line through `WriteLine`; results go to standard output, so that
  redirecting it captures them.
- **TextLint library** - `SpellCheckResult`, `SpellingIssue.Format`, and `FileDiagnostic.Format`.

#### Callers

- **LintRunner** - calls `Write` once per check.
