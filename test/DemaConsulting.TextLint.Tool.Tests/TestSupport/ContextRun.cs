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

using DemaConsulting.TextLint.Tool.Cli;

namespace DemaConsulting.TextLint.Tool.Tests.TestSupport;

/// <summary>
///     The exit code and the complete output of one in-process run.
/// </summary>
/// <param name="ExitCode">The exit code the run proposes to the operating system.</param>
/// <param name="Output">Everything the run wrote to standard output and standard error, in order.</param>
internal sealed record ContextRunResult(int ExitCode, string Output)
{
    /// <summary>
    ///     Gets the output as separate lines, without line terminators.
    /// </summary>
    /// <value>One entry per written line, in order.</value>
    public string[] Lines => Output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
///     Runs code against a real <see cref="Context"/> and captures everything it writes.
/// </summary>
/// <remarks>
///     The context's own log file is the capture mechanism: it receives every line written to standard output
///     and to standard error, in order, and the context is silent, so tests neither touch the console nor
///     depend on how other tests redirect it. The log lives in its own temporary file, so it can never be
///     selected as a file to check.
/// </remarks>
internal static class ContextRun
{
    /// <summary>
    ///     Creates a context from the arguments, runs an action with it, and returns what was written.
    /// </summary>
    /// <param name="arguments">The command-line arguments, without the silent and log options this helper adds.</param>
    /// <param name="action">The code to run against the context.</param>
    /// <returns>The exit code of the context and everything written to it.</returns>
    public static ContextRunResult Execute(string[] arguments, Action<Context> action)
    {
        var logFile = Path.Combine(Path.GetTempPath(), $"TextLintToolTests-{Guid.NewGuid():N}.log");
        try
        {
            // The context must be disposed before the log is read, so that the log is complete
            int exitCode;
            using (var context = Context.Create(["--silent", "--log", logFile, .. arguments]))
            {
                action(context);
                exitCode = context.ExitCode;
            }

            return new ContextRunResult(exitCode, File.ReadAllText(logFile));
        }
        finally
        {
            File.Delete(logFile);
        }
    }

    /// <summary>
    ///     Runs the whole program logic with the arguments and returns what was written.
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns>The exit code and everything written, including the banner.</returns>
    public static ContextRunResult RunProgram(params string[] arguments) =>
        Execute(arguments, context => Program.Run(context));
}
