## Cli

![Cli Structure](CliView.svg)

### Overview

The `Cli` subsystem is the boundary between the host environment's command-line invocation and
the tool's internal logic. It owns argument parsing, output channel management, and exit-code
tracking. All other subsystems receive a `Context` object from this subsystem to read parsed
flags and write output. It also answers the built-in `help` command. The `Cli` subsystem contains two units:
`Context` and `HelpCommand`. It also owns the options of a check
(`--config`, `--root`, `--language`, `--allow-empty`, `--max-parallelism`) and the positional file patterns, which
it exposes exactly as typed; what they mean is decided by the `Linting` subsystem and the library.

### Interfaces

**Context.Create**: Factory method that constructs a `Context` from a command-line argument array.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Parses `string[] args` into flag properties and opens the log file if `--log` is
  present. Returns a fully initialized `Context`. Accepts `--result` as a legacy alias for
  `--results`. Every argument that is not a known option is a file pattern and is kept as typed, in order.
- *Constraints*: Throws `ArgumentException` for unknown or malformed arguments: an unknown option, an option
  that needs a value and has none, an empty or white-space value or pattern, a value that looks like another
  option, an out-of-range number, or a value option that is given twice (an option and its alias count as one);
  throws `InvalidOperationException` when the log file cannot be opened.

**Context.WriteLine**: Writes a message to stdout and to the log file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Writes `message` to `Console.Out` and to the log file if one is open. Stdout
  output is suppressed when `Silent` is true; the log file always receives the message.
- *Constraints*: None.

**Context.WriteError**: Writes an error message and sets the error exit code.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets `_hasErrors` to true, writes `message` in red to `Console.Error`, and writes
  to the log file if one is open. Stderr output is suppressed when `Silent` is true, but
  `ExitCode` is set to 1 regardless.
- *Constraints*: Once set, `ExitCode` cannot return to 0 within the same invocation.

**Context.MarkFailed**: Marks the run as failed without output.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets the error state so that `ExitCode` is 1, and writes nothing. Used by a check that has
  already printed its findings as result lines.
- *Constraints*: Once set, `ExitCode` cannot return to 0 within the same invocation.

**Context.ExitCode**: Derived property returning 0 or 1.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns 1 if `WriteError` or `MarkFailed` has been called at least once; returns 0 otherwise.
  There is a single failure code, as in cspell.
- *Constraints*: Read-only.

**Context.Dispose**: Releases the log file `StreamWriter`.

- *Type*: In-process .NET method (`IDisposable`).
- *Role*: Provider.
- *Contract*: Disposes `_logWriter` and sets it to null; flushes any buffered content. Callers
  must use a `using` statement to guarantee disposal.
- *Constraints*: Safe to call multiple times (idempotent after first call).

**HelpCommand.TryRun**: Answers the `help` command from the built-in help cards.

- *Type*: In-process .NET instance method (on the shared `HelpCommand.Default` instance).
- *Role*: Provider.
- *Contract*: Returns `false`, writing nothing, when the first argument is not `help`. Otherwise writes the
  answer to the console, returns `true`, and sets the exit code (see *Help Command* in the system design).
- *Constraints*: Does not throw for user input; does not use `Context`.

**HelpCommand.Footer**: The sentence that points a reader of the usage text to the help command.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns the one-line pointer that HelpCards supplies.
- *Constraints*: Reading it never builds the help catalog.

### Design

The `Cli` subsystem has no subsystem-level code of its own; its behavior is provided by the
`Context` and `HelpCommand` units, which do not call each other. `Program.Main` offers the
arguments to `HelpCommand.TryRun` first. When the help command answers, the invocation ends
with its exit code and no `Context` is created. Otherwise `Program` creates a `Context` and
passes it to all other units that produce output.

`HelpCommand` writes its answers to the console itself. It is the one exception to `Context`
owning all output, because the `help` command is answered before a `Context` exists.

The subsystem has no dependencies on other tool subsystems. `Context` uses only .NET BCL types
(`Console`, `StreamWriter`); `HelpCommand` uses the HelpCards OTS library and reads the
tool version from `Program.Version`.

Error handling flows from `Context.Create` to `Program.Main`: argument parsing errors propagate
as `ArgumentException`; log-file errors propagate as `InvalidOperationException`. Both are
caught and handled in `Program.Main`, which writes the error to stderr and returns exit code 1.

When `--log` is active, `Context` holds an open `StreamWriter` for the duration of the
invocation. The `Program.Main` call site wraps the `Context` in a `using` statement to ensure
the file handle is released and buffered content is flushed on exit.
