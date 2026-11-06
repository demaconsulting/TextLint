### Context

![Cli Structure](CliView.svg)

#### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its
single responsibility is to parse the argument list, expose the parsed flags as read-only
properties, own the two output channels (console and log file), and derive the exit code from
whether any errors were reported.

#### Data Model

**_logWriter**: `StreamWriter?` — Log file writer; `null` when logging is not active.

**_hasErrors**: `bool` — Set to `true` on the first `WriteError` call; once set, cannot return
to `false` within the same invocation.

**Version**: `bool` — `true` when `-v` or `--version` was present in the argument list.

**Help**: `bool` — `true` when `-?`, `-h`, or `--help` was present in the argument list.

**Silent**: `bool` — `true` when `--silent` was present in the argument list.

**Validate**: `bool` — `true` when `--validate` was present in the argument list.

**ResultsFile**: `string?` — Path supplied after `--results` or `--result`, or `null` if
neither flag was present.

**HeadingDepth**: `int` — Heading depth for markdown output; valid range 1–6, default 1;
supplied via `--depth`.

**Globs**: `IReadOnlyList<string>` — The positional file patterns exactly as typed, in order; an immutable
copy; empty by default. Whether a pattern is relative or absolute, and how backslashes are read, is decided
by the library, not here.

**ConfigFile**: `string?` — Path supplied after `--config`, or `null`.

**Root**: `string?` — Directory supplied after `--root`, or `null` for the current directory.

**Language**: `string?` — Text supplied after `--language`, or `null`; validated by the library when the check
starts, so the list of languages is defined in one place.

**AllowEmpty**: `bool` — `true` when `--allow-empty` was present.

**MaxParallelism**: `int?` — Integer supplied after `--max-parallelism` (1 to the largest integer), or `null`
for the library default.

**ExitCode**: `int` (derived) — Returns 1 if `_hasErrors` is true; returns 0 otherwise.

#### Key Methods

**Create**: Factory method that parses arguments and returns a fully initialized `Context`.

- *Parameters*: `string[] args` — raw command-line argument array.
- *Returns*: `Context` — a new instance with all flags set.
- *Preconditions*: `args` is not null.
- *Postconditions*: All flag properties reflect the parsed argument state; the log file is open
  if `--log` was supplied.

Delegates to the private `ArgumentParser` helper to parse flags, then opens the log file by
calling `OpenLogFile` if `--log` was present. Throws `ArgumentException` for unknown or
malformed arguments; throws `InvalidOperationException` if the log file cannot be opened.

The parser reads the arguments left to right. A known option is handled by name. A value option takes the next
argument as its value, which must not be missing, empty, white space, or start with a dash, so that a forgotten
value does not swallow the next option; the number options are read with the invariant culture and checked
against their range, and a negative number is reported by the range check. A value option is recorded by
canonical name (`--result` and `--results` share one) and a second occurrence is an error. Any other argument that
starts with a dash is an unsupported option; every remaining argument is a file pattern, which must not be empty
or white space. A pattern that starts with a dash can be written with a leading `./`.

**MarkFailed**: Marks the run failed without writing output.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true, so `ExitCode` is 1; nothing was written.

**WriteLine**: Writes a message to standard output and to the log file.

- *Parameters*: `string message` — the message to write.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: Message is on stdout (unless `Silent`) and in the log file (if open).

**WriteError**: Writes an error message, sets the error state, and records to the log file.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true; message is on stderr in red (unless `Silent`) and in
  the log file (if open).

**Dispose**: Disposes the log file writer.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_logWriter` is disposed and set to null; any buffered log content is
  flushed.

#### Error Handling

`Create` throws `ArgumentException` for any unrecognized option ("Unsupported argument '{arg}'"), for an option
that needs a value and has none ("{option} requires {description}"), for an out-of-range number
("{option} requires an integer between {min} and {max} ..."), for a value option given twice
("{option} was specified more than once ..."), and for an empty pattern. It throws `InvalidOperationException`
("Failed to open log file '{path}': {detail}") when the `--log` file cannot be opened. Both
exceptions propagate to `Program.Main`.

`WriteLine` and `WriteError` do not throw; they write to whichever output channels are
available.

`Dispose` does not throw; any disposal errors are silently ignored.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, and `Path` are the only dependencies. No other
  tool units are used.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
- **LintRunner** — reads the options and patterns, calls `WriteLine` and `WriteError`, and calls `MarkFailed`.
- **LintReporter** — calls `WriteLine`.
