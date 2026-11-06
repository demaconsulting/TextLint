// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Immutable;
using System.Globalization;

namespace DemaConsulting.TextLint.Tool.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
internal sealed class Context : IDisposable
{
    /// <summary>
    ///     Log file stream writer (if logging is enabled).
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the heading depth for markdown output (default is 1).
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the file patterns given on the command line, in the order they were typed.
    /// </summary>
    /// <remarks>
    ///     These are the positional arguments, exactly as typed: interpreting them (separators, absolute
    ///     paths) belongs to the library, not to the argument parser. When any are given they replace the
    ///     <c>files</c> setting. The list is an immutable copy.
    /// </remarks>
    public IReadOnlyList<string> Globs { get; private init; } = ImmutableArray<string>.Empty;

    /// <summary>
    ///     Gets the settings file named by <c>--config</c>, or <see langword="null"/> when none was named.
    /// </summary>
    public string? ConfigFile { get; private init; }

    /// <summary>
    ///     Gets the directory named by <c>--root</c>, or <see langword="null"/> for the current directory.
    /// </summary>
    public string? Root { get; private init; }

    /// <summary>
    ///     Gets the language selection named by <c>--language</c>, or <see langword="null"/> to keep the settings' language.
    /// </summary>
    /// <remarks>
    ///     The text is passed on unchanged and is validated by the library when the check starts, so the
    ///     list of supported languages is defined in one place.
    /// </remarks>
    public string? Language { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether a selection that matches no files is accepted as success.
    /// </summary>
    public bool AllowEmpty { get; private init; }

    /// <summary>
    ///     Gets the maximum number of files checked at the same time, or <see langword="null"/> for the library default.
    /// </summary>
    public int? MaxParallelism { get; private init; }

    /// <summary>
    ///     Gets the proposed exit code for the application: 0 for success, 1 for anything else.
    /// </summary>
    /// <remarks>
    ///     A single failure code, as in cspell, keeps scripts simple: every reason a run did not end clean
    ///     (findings, unreadable files, a bad setting, a bad argument, or nothing to check) is 1.
    /// </remarks>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>
    ///     Private constructor - use Create factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a Context instance from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            ResultsFile = parser.ResultsFile,
            HeadingDepth = parser.HeadingDepth,
            Globs = parser.Globs.ToImmutableArray(),
            ConfigFile = parser.ConfigFile,
            Root = parser.Root,
            Language = parser.Language,
            AllowEmpty = parser.AllowEmpty,
            MaxParallelism = parser.MaxParallelism
        };

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Opens the log file for writing
    /// </summary>
    /// <param name="logFile">Log file path</param>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, UnauthorizedAccessException, ArgumentException,
        // NotSupportedException, and other file system-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Helper class for parsing command-line arguments
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Gets the positional file patterns, exactly as typed, in order.
        /// </summary>
        public List<string> Globs { get; } = [];

        /// <summary>
        ///     Gets the settings file path given by <c>--config</c>.
        /// </summary>
        public string? ConfigFile { get; private set; }

        /// <summary>
        ///     Gets the root directory given by <c>--root</c>.
        /// </summary>
        public string? Root { get; private set; }

        /// <summary>
        ///     Gets the language selection given by <c>--language</c>.
        /// </summary>
        public string? Language { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether <c>--allow-empty</c> was specified.
        /// </summary>
        public bool AllowEmpty { get; private set; }

        /// <summary>
        ///     Gets the degree of parallelism given by <c>--max-parallelism</c>.
        /// </summary>
        public int? MaxParallelism { get; private set; }

        /// <summary>
        ///     The options that take a value and have been seen so far, by their canonical name.
        /// </summary>
        /// <remarks>
        ///     Giving a value option twice is almost always a mistake in a script, and silently keeping
        ///     the last value hides it, so the second occurrence is reported instead.
        /// </remarks>
        private readonly HashSet<string> _seenValueOptions = new(StringComparer.Ordinal);

        /// <summary>
        ///     Parses command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument
        /// </summary>
        /// <param name="arg">Argument to parse</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Updated index</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--log":
                    RegisterValueOption(arg, "--log");
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    // The legacy alias shares the canonical name, so giving both is a duplicate
                    RegisterValueOption(arg, "--results");
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    RegisterValueOption(arg, "--depth");
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                case "--config":
                    RegisterValueOption(arg, "--config");
                    ConfigFile = GetRequiredStringArgument(arg, args, index, "a settings filename argument");
                    return index + 1;

                case "--root":
                    RegisterValueOption(arg, "--root");
                    Root = GetRequiredStringArgument(arg, args, index, "a directory argument");
                    return index + 1;

                case "--language":
                    RegisterValueOption(arg, "--language");
                    Language = GetRequiredStringArgument(arg, args, index, "a language argument such as en-US or en-GB");
                    return index + 1;

                case "--allow-empty":
                    AllowEmpty = true;
                    return index;

                case "--max-parallelism":
                    RegisterValueOption(arg, "--max-parallelism");
                    MaxParallelism = GetRequiredIntArgument(arg, args, index, "a maximum number of files to check at the same time");
                    return index + 1;

                default:
                    return ParsePositionalArgument(arg, index);
            }
        }

        /// <summary>
        ///     Treats an argument that is not a known option as a file pattern.
        /// </summary>
        /// <remarks>
        ///     Anything that starts with a dash and is not a known option is reported as unsupported, so a
        ///     misspelled option is never silently taken for a file name. Every other argument is a file
        ///     pattern and is kept exactly as typed; what it means is decided by the library. A pattern
        ///     that starts with a dash can be written with a leading <c>./</c>. Side effects: appends to
        ///     <see cref="Globs"/>.
        /// </remarks>
        /// <param name="arg">The argument, which is not a known option.</param>
        /// <param name="index">The index of the argument after <paramref name="arg"/>.</param>
        /// <returns>The index of the next argument, which is unchanged because a pattern takes no value.</returns>
        /// <exception cref="ArgumentException">Thrown when the argument starts with a dash, or is empty or white space.</exception>
        private int ParsePositionalArgument(string arg, int index)
        {
            // A leading dash marks an option, so an unknown one is an error rather than a file name
            if (arg.StartsWith('-'))
            {
                throw new ArgumentException($"Unsupported argument '{arg}'", nameof(arg));
            }

            // An empty pattern can only be a scripting mistake, such as an unset variable
            if (string.IsNullOrWhiteSpace(arg))
            {
                throw new ArgumentException("A file pattern argument must not be empty", nameof(arg));
            }

            Globs.Add(arg);
            return index;
        }

        /// <summary>
        ///     Records that an option that takes a value has been seen.
        /// </summary>
        /// <remarks>
        ///     Side effects: updates the set of seen options.
        /// </remarks>
        /// <param name="arg">The option as typed, used in the message.</param>
        /// <param name="canonicalName">The option's canonical name, shared with its aliases.</param>
        /// <exception cref="ArgumentException">Thrown when the option was already given.</exception>
        private void RegisterValueOption(string arg, string canonicalName)
        {
            if (!_seenValueOptions.Add(canonicalName))
            {
                throw new ArgumentException($"{arg} was specified more than once; give {canonicalName} a single time");
            }
        }

        /// <summary>
        ///     Gets a required string argument value
        /// </summary>
        /// <remarks>
        ///     A value that starts with a dash is taken to be the next option, so a forgotten value is
        ///     reported as missing instead of swallowing the option that follows it. A value that is empty
        ///     or white space is also reported as missing, because it can only come from an unset variable.
        /// </remarks>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <returns>Argument value</returns>
        /// <exception cref="ArgumentException">Thrown when the value is missing, empty, or looks like an option.</exception>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith('-'))
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>
        ///     Gets a required integer argument value
        /// </summary>
        /// <remarks>
        ///     Unlike a string value, an integer value may start with a dash (a negative number), so it is
        ///     read as it is and rejected by the range check, which gives the user the allowed range instead
        ///     of the less helpful "requires a value". The number is parsed with the invariant culture, so
        ///     the result does not depend on the language of the machine.
        /// </remarks>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <param name="min">Minimum valid value (inclusive)</param>
        /// <param name="max">Maximum valid value (inclusive)</param>
        /// <returns>Argument value as an integer in [min, max]</returns>
        /// <exception cref="ArgumentException">Thrown when the value is missing, not an integer, or out of range.</exception>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            if (index >= args.Length ||
                !int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ||
                result < min ||
                result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when one is open.
    /// </remarks>
    public void WriteLine(string message)
    {
        // Write to console unless silent mode is enabled
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Marks the run as failed without printing anything.
    /// </summary>
    /// <remarks>
    ///     A check that finds spelling issues has already printed each of them as a result line on stdout,
    ///     so reporting the failure again as an error message would only add noise; the run still has to
    ///     exit with a failure code, and this is the one way to do that without output. Side effects:
    ///     makes <see cref="ExitCode"/> 1.
    /// </remarks>
    public void MarkFailed()
    {
        _hasErrors = true;
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors
        _hasErrors = true;

        // Write to error console unless silent mode is enabled
        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Disposes resources used by the Context.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
