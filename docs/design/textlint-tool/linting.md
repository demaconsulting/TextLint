## Linting

![Linting Structure](LintingView.svg)

### Overview

The `Linting` subsystem runs one spell check as the command line asked for it. It finds and loads the settings,
applies the command-line overrides, calls the `TextLint` library, reports the result, and decides the exit
status. It is policy and glue only: reading and validating settings, interpreting command-line paths, selecting
files, and checking text are all library calls, and the subsystem must not grow linting logic of its own. It
contains two units: `LintRunner`, which performs the run and reports failures, and `LintReporter`, which writes
the result.

### Interfaces

**LintRunner.Run**: Runs the check the command line describes.

- _Type_: In-process .NET static method.
- _Role_: Provider.
- _Contract_: Accepts a `Context` (the parsed command line and the output channels) and a cancellation token.
  Writes the result to the context, marks the context failed when the result has problems, and writes an error
  for every failure the user can cause. Returns nothing.
- _Constraints_: Throws `ArgumentNullException` for a null context. Does not catch defects, such as a damaged
  embedded dictionary, which propagate. The call blocks until the check ends.

**LintReporter.Write**: Writes the result of a check.

- _Type_: In-process .NET static method.
- _Role_: Provider.
- _Contract_: Accepts a `Context`, a `SpellCheckResult`, and a function that maps a path relative to the search
  directory to the name to show. Writes one line per finding, one per skipped or unchecked file, and a summary.
- _Constraints_: Throws `ArgumentNullException` for a null argument. Does not set the exit status.

**Dependencies on other subsystems**: The `Cli` subsystem's `Context` for the parsed command line and the
output channels, and the `TextLint` library (the settings loader, the command-line path resolver, the spell
checker, and its result types). No other tool subsystem is used.

### Design

`Program` calls `LintRunner.Run` for everything that is not a request for the version, the help text, or
self-validation. The runner works in this order:

1. It makes the root directory absolute: the `--root` directory, or the current directory. It then checks that the
   directory exists, is a directory, and can be read (its attributes, then one listed entry), so that a missing root
   ("does not exist") is told apart from a file ("is not a directory") and from one that cannot be read, without
   asking `Exists`. This happens before anything else, so that an absolute path cannot bypass an invalid root and no
   misleading line about missing settings is written.
2. It finds the settings. A file named by `--config` is loaded as typed. Otherwise `.textlint.yaml` in the root is
   loaded when it exists, and nothing else is searched, so a file from an unrelated directory is never applied by
   accident. When there is none, the default settings apply and one information line says so. Whether the file
   exists is decided by reading its attributes and treating only "not found" as absence, so a file that exists
   but cannot be examined is an error and not a silent fall back to the defaults. The library loads and validates
   the file; every problem it reports is written as an error and nothing is checked.
3. It selects the files. When the command line has patterns, the library's command-line path resolver turns them
   into a search directory and portable patterns, they replace the `files` setting, and the ignore patterns of
   the settings still apply. A command-line path that names an existing directory selects every file below it;
   the library makes that decision. When it has none, the `files` setting is used. When there are none anywhere,
   that is a usage error with an example. The resolver also says whether files are to be named by absolute path.
4. It applies the language option over the settings, and the parallelism limit when one was given.
5. It calls the library's spell checker and waits for the result once, at the boundary of the synchronous entry
   point.
6. A result in which no file was checked and none was skipped or failed means the selection matched nothing,
   which is an error unless `--allow-empty` was given.
7. `LintReporter` writes the result, and the runner marks the context failed when the result has problems: a
   finding, or a file that was meant to be checked and could not be. A skipped binary or oversized file does not
   fail the run.

Failures the user can cause (a missing or unreadable directory, an invalid path, an unsupported pattern or
language) are caught, written as errors, and fail the run. A cancelled token ends the run with a message and a
failure instead of a partial result. Results are data about files and go to standard output so that
`textlint > report.txt` captures them; failures of the tool itself go to standard error.
