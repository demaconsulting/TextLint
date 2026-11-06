## Program

![TextLintTool Structure](TextLintToolView.svg)

### Purpose

`Program` is the entry point and execution orchestrator for the TextLint Tool. Its single
responsibility is to create a `Context` from the command-line arguments, dispatch to the
appropriate handler based on the parsed flags, and return the exit code.

### Data Model

**Version**: `string` (static property) — The tool version read from
`AssemblyInformationalVersionAttribute` on every access, falling back to `AssemblyVersion`, then
`"0.0.0"`. No caching is applied; callers that need the value more than once should store it
locally.

### Key Methods

**Main**: Entry point for the tool process.

- *Parameters*: `string[] args` — command-line arguments from the host environment.
- *Returns*: `int` — exit code; 0 for success, 1 for expected errors.
- *Preconditions*: None.
- *Postconditions*: Exit code reflects whether any errors were reported during execution.

Registers a handler for Ctrl+C that cancels a token (and cancels the console's default termination, so the check
can stop cleanly), creates a `Context` using `Context.Create(args)`, calls `RunCancellable(context, token)`,
and returns `context.ExitCode`; the handler is removed again in a `finally`. Catches `ArgumentException` and
`InvalidOperationException` — writes
`"Error: {message}"` to stderr and returns 1. Catches any other `Exception` — writes
`"Unexpected error: {message}"` to stderr and re-throws so the runtime can record it.

**Run**: Runs the program logic without a way to cancel it; it is `RunCancellable` with a token that is never
cancelled, and is the form used by self-validation.

- *Parameters*: `Context context` — the parsed context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Exactly one handler has been called.

**RunCancellable**: Dispatches execution based on parsed flags.

- *Parameters*: `Context context` — the parsed context; `CancellationToken cancellationToken` — stops a running
  check.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Exactly one handler has been called.

Inspects flags in priority order: (1) if `context.Version` is true, calls
`context.WriteLine(Version)` and returns; (2) calls `PrintBanner`; (3) if `context.Help` is
true, calls `PrintHelp` and returns; (4) if `context.Validate` is true, calls
`Validation.Run(context)`; (5) otherwise calls `RunToolLogic(context, cancellationToken)`.

**PrintBanner**: Writes the tool name, version, and copyright line to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**PrintHelp**: Writes the usage synopsis, the options table, the meaning of the pattern arguments, and the exit
codes to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**RunToolLogic**: Runs the check by calling `LintRunner.Run(context, cancellationToken)`. The program holds no
linting logic.

- *Parameters*: `Context context` — the parsed context; `CancellationToken cancellationToken` — stops the check.
- *Returns*: `void`.

### Error Handling

`Main` handles errors at two levels. Expected errors (`ArgumentException` and
`InvalidOperationException`) are written to stderr as `"Error: {message}"` and cause exit
code 1 without a stack trace. These two are written to stderr even when `--silent` was given, because the
command line could not be understood and no `Context` exists to honor the option; this is also what the
other tools of the family do. Unexpected errors (`Exception`) are written to stderr as
`"Unexpected error: {message}"` and re-thrown so the runtime can record them in event logs.
`Run`, `RunCancellable`, `PrintBanner`, `PrintHelp`, and `RunToolLogic` do not catch exceptions; all errors
propagate to `Main`. Failures a user can cause during a check are handled inside `LintRunner`, which reports
them as errors; cancellation is reported there too.

### Dependencies

- **Context** — `Program` reads parsed flags from `Context` and calls `Context.WriteLine` and
  `Context.WriteError` for all output.
- **Validation** — `Program.RunCancellable` calls `Validation.Run(context)` when the `--validate` flag is
  set.
- **LintRunner** — `Program.RunCancellable` calls `LintRunner.Run` for everything that is not version, help, or
  validation.

### Callers

N/A - entry point, called by the host environment.
