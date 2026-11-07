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

using System.Reflection;
using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Linting;
using DemaConsulting.TextLint.Tool.SelfTest;

namespace DemaConsulting.TextLint.Tool;

/// <summary>
///     Main program entry point for the TextLint Tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    /// <remarks>
    ///     The version is read from the <see cref="AssemblyInformationalVersionAttribute"/> via
    ///     reflection on every access. There is no caching; callers that need the value more than
    ///     once should store the result locally.
    /// </remarks>
    public static string Version
    {
        get
        {
            // Get the assembly containing this program
            var assembly = typeof(Program).Assembly;

            // Try to get version from assembly attributes, fallback to AssemblyVersion, or default to 0.0.0
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }

    /// <summary>
    ///     Main entry point for the TextLint Tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    /// <exception cref="Exception">Thrown when an unexpected error occurs; re-thrown after writing to stderr.</exception>
    /// <remarks>
    ///     <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> are treated as
    ///     expected errors: their messages are written to stderr and exit code 1 is returned without
    ///     a stack trace. Any other exception is written to stderr and then re-thrown so that the
    ///     runtime can record it in event logs. These messages are written even when <c>--silent</c> is given, as
    ///     in the other DEMA command-line tools: they arise while the command line is being parsed, before the
    ///     output settings exist, and a command line that cannot be understood must never fail without a word.
    ///     Everything written through the <see cref="Context"/> honors <c>--silent</c>.
    /// </remarks>
    public static int Main(string[] args)
    {
        // Ctrl+C asks the running check to stop instead of killing the process, so the user gets a clear
        // message and a failure exit code, not a half-written result
        using var cancellation = new CancellationTokenSource();
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            // Create context from command-line arguments
            using var context = Context.Create(args);

            // Run the program logic
            RunCancelable(context, cancellation.Token);

            // Return the exit code from the context
            return context.ExitCode;
        }
        catch (ArgumentException ex)
        {
            // Print expected argument exceptions and return error code
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // Print expected operation exceptions and return error code
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            // Print unexpected exceptions and re-throw to generate event logs
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            throw;
        }
        finally
        {
            // The handler must not outlive the token source it cancels
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    /// <summary>
    ///     Runs the program logic based on the provided context, without a way to cancel it.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <remarks>
    ///     Equivalent to <see cref="RunCancelable"/> with a token that is never canceled; it is the form
    ///     used by self-validation, which runs short fixed checks.
    /// </remarks>
    public static void Run(Context context)
    {
        RunCancelable(context, CancellationToken.None);
    }

    /// <summary>
    ///     Runs the program logic based on the provided context and stops a running check when asked.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <param name="cancellationToken">Token that stops a running check, for example on Ctrl+C.</param>
    /// <remarks>
    ///     Dispatch is priority-ordered: version check first, then help, then self-validation,
    ///     then main tool logic. Only the highest-priority matching action is executed per invocation.
    ///     Self-validation runs short fixed checks and is not cancelable.
    /// </remarks>
    public static void RunCancelable(Context context, CancellationToken cancellationToken)
    {
        // Priority 1: Version query
        if (context.Version)
        {
            context.WriteLine(Version);
            return;
        }

        // Print application banner
        PrintBanner(context);

        // Priority 2: Help
        if (context.Help)
        {
            PrintHelp(context);
            return;
        }

        // Priority 3: Self-Validation
        if (context.Validate)
        {
            Validation.Run(context);
            return;
        }

        // Priority 4: Main tool functionality
        RunToolLogic(context, cancellationToken);
    }

    /// <summary>
    ///     Prints the application banner.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintBanner(Context context)
    {
        context.WriteLine($"TextLint Tool version {Version}");
        context.WriteLine("Copyright (c) DEMA Consulting");
        context.WriteLine("");
    }

    /// <summary>
    ///     Prints usage information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintHelp(Context context)
    {
        context.WriteLine("Usage: textlint [options] [globs...]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  -v, --version              Display version information");
        context.WriteLine("  -?, -h, --help             Display this help message");
        context.WriteLine("  --silent                   Suppress console output (a command line that cannot be understood");
        context.WriteLine("                             is still reported on standard error)");
        context.WriteLine("  --validate                 Run self-validation");
        context.WriteLine("  --results <file>           Write validation results to file (.trx or .xml)");
        context.WriteLine("  --depth <#>                Set heading depth for markdown output (default: 1)");
        context.WriteLine("  --log <file>               Write output to log file");
        context.WriteLine("  --config <file>            Settings file (default: .textlint.yaml in the root directory, if present)");
        context.WriteLine("  --root <dir>               Directory that patterns and the default settings file are relative to");
        context.WriteLine("                             (default: current directory)");
        context.WriteLine("  --language <languages>     Language of the dictionaries, e.g. en-US or en-GB (overrides the settings)");
        context.WriteLine("  --allow-empty              Accept patterns that match no files (default: exit code 1)");
        context.WriteLine("  --max-parallelism <#>      Maximum number of files checked at the same time (default: processor count)");
        context.WriteLine("");
        context.WriteLine("[globs...] are the files to check: paths or glob patterns, relative or absolute, replacing the");
        context.WriteLine("'files' setting; the 'ignorePaths' setting still applies. Quote globs so the shell does not");
        context.WriteLine("expand them.");
        context.WriteLine("");
        context.WriteLine("Exit codes: 0 = no issues; 1 = issues found, a file could not be checked, no files matched, or an error");
        context.WriteLine("occurred.");
    }

    /// <summary>
    ///     Runs the main tool logic.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <param name="cancellationToken">Token that stops the check.</param>
    /// <remarks>
    ///     The tool is a thin front-end, so the whole check is delegated to <see cref="LintRunner"/>,
    ///     which calls the library; nothing about linting is decided here.
    /// </remarks>
    private static void RunToolLogic(Context context, CancellationToken cancellationToken)
    {
        LintRunner.Run(context, cancellationToken);
    }
}
