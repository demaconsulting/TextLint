### LintRunner

![Linting Structure](LintingView.svg)

#### Purpose

`LintRunner` runs one spell check as the command line asked for it: it finds and loads the settings, applies the
command-line overrides, calls the `TextLint` library, hands the result to `LintReporter`, and sets the exit
status. It exists so that `Program` stays a dispatcher and so that the policy of the tool (where settings are
looked for, which option beats which setting, how each failure is reported) lives in one place. It contains no
linting logic.

#### Data Model

`LintRunner` is an internal static class with no state. A named constant holds the default settings file name
(`.textlint.yaml`). An internal enumeration, `SettingsFileProbe`, has the values `Found`, `NotFound`, and `Failed`,
which say what a look for the default settings file found.

#### Key Methods

**Run**: Runs the check and reports its outcome through the context.

- _Parameters_: `Context context`; `CancellationToken cancellationToken`.
- _Returns_: `void`.
- _Preconditions_: `context` is not null.
- _Postconditions_: The result or the reason there is none has been written; the context is marked failed when
  the check found anything to act on or could not be completed.

It calls `RunCheck` inside a handler. A cancellation that was requested through the token is written as
`The check was cancelled before it finished.` and fails the run. An `IOException`,
`UnauthorizedAccessException`, `NotSupportedException`, or `ArgumentException` is written as an error with its own
message: these are the ways a user's input can be wrong or the file system can refuse, and each message names the
cause. Nothing else is caught.

**RunCheck (private)**: Performs the steps of the check, in the order given by the _Linting Design_.

- _Parameters_: `Context context`; `CancellationToken cancellationToken`.
- _Returns_: `void`.

It makes the root absolute, checks it through `IsRootUsable`, and stops if that reported a problem; only then does it
load the settings through `LoadSettings`, and stops if that reported a problem. With
command-line patterns it calls `CommandLinePathResolver.Resolve`, replaces the `Files` of the settings with the
resolved patterns, searches the resolved directory, and uses the selection's display-path function; without them
it uses the `files` setting and shows relative paths; with neither it writes the usage error. It applies the
language option with a `with` expression, builds the `SpellCheckOptions` (the library default unless
`--max-parallelism` was given), calls `SpellChecker.CheckAsync(...).GetAwaiter().GetResult()` once, writes the
empty-selection error when nothing was checked, skipped, or failed and `--allow-empty` was not given, calls
`LintReporter.Write`, and calls `Context.MarkFailed` when the result has problems.

**IsRootUsable (private)**: Checks that the root directory can be used before anything else is done with it.

- _Parameters_: `Context context`; `string root` - the absolute root.
- _Returns_: `bool` - true when the root exists, is a directory, and can be read.

It enumerates the first entry of the directory. A `DirectoryNotFoundException` is written as
`The root directory '<root>' does not exist.`; any other `IOException` (including a root that is a file) or an
`UnauthorizedAccessException` is written as `The root directory '<root>' cannot be read: <explanation>`. Asking
`Directory.Exists` instead would answer "no" for a directory the process may not read and so blame the wrong
thing.

**LoadSettings (private)**: Finds and loads the settings, or reports why that was not possible.

- _Parameters_: `Context context`; `string root`; `CancellationToken cancellationToken`.
- _Returns_: the settings, or `null` when a problem was written and nothing is to be checked.

An explicit `--config` path is loaded as typed and must exist. Otherwise the default path is `.textlint.yaml`, or
that name combined with `--root` when one was given (so diagnostics read `.textlint.yaml(3,1)` and not a long
absolute path). `ProbeSettingsFile` decides whether it exists: `Failed` is written as an error, `NotFound` yields
the default settings and the information line `info: No settings file found in '<root>'; using the default
settings.`, and `Found` goes on to the loader. Every diagnostic of the loader is written as an error.

**ProbeSettingsFile (internal)**: Finds out whether the default settings file exists without mistaking a failure
for absence.

- _Parameters_: `string path`; `Func<string, FileAttributes> getAttributes`; `out string problem`.
- _Returns_: a `SettingsFileProbe`.

It reads the attributes of the path through the delegate (which is `File.GetAttributes` in production and a
faulting function in tests). A `FileNotFoundException` or `DirectoryNotFoundException` is `NotFound`. An
`IOException`, `UnauthorizedAccessException`, `NotSupportedException`, or `ArgumentException` is `Failed` with the
file system's explanation. Asking `File.Exists` instead would answer "no" for a file the process may not examine
and would silently discard the user's settings.

#### Error Handling

Failures the user can cause are written with `Context.WriteError` and fail the run: an unusable settings file (one
error per problem, with file, line, and column), a settings file that cannot be examined, a usage error (no
files to check), an empty selection, a missing, unreadable, or non-directory root directory (reported first), an
unsupported pattern (the library's
`InvalidGlobPatternException` names the argument as typed), an unsupported language, and a combination of paths that
cannot be searched from one directory. Cancellation is written and fails the run. Defects (for example
`InvalidDataException` from a damaged embedded dictionary, or any exception type not listed) are not caught and
reach `Program.Main`, which reports them as unexpected errors and rethrows. A null context throws
`ArgumentNullException`.

#### Dependencies

- **Context** (Cli subsystem) - the parsed command line and the output channels, and `MarkFailed`.
- **LintReporter** - writes the result.
- **TextLint library** - `SettingsLoader`, `TextLintSettings`, `CommandLinePathResolver`, `SpellChecker`,
  `SpellCheckOptions`, and `SpellCheckResult`.

#### Callers

- **Program** - calls `Run` for everything that is not version, help, or self-validation.
