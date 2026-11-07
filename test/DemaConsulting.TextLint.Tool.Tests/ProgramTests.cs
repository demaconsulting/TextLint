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

// cspell:ignore wrold

using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tool.Tests;

/// <summary>
///     Unit tests for the Program class.
/// </summary>
[Collection("Sequential")]
public class ProgramTests
{
    /// <summary>
    ///     Test that Run with version flag displays version only.
    /// </summary>
    [Fact]
    public void Program_Run_WithVersionFlag_DisplaysVersionOnly()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--version"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.DoesNotContain("Copyright", output);
            Assert.DoesNotContain("TextLint Tool version", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with help flag displays usage information.
    /// </summary>
    [Fact]
    public void Program_Run_WithHelpFlag_DisplaysUsageInformation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--help"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Contains("--version", output);
            Assert.Contains("--help", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with validate flag runs validation.
    /// </summary>
    [Fact]
    public void Program_Run_WithValidateFlag_RunsValidation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--validate"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Total Tests:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with nothing to check displays the banner and reports a usage error.
    /// </summary>
    [Fact]
    public void Program_Run_NoPatterns_DisplaysBannerAndUsageError()
    {
        // Arrange: an empty root directory, so that neither the command line nor a settings file names files
        using var temp = new TempDirectory();

        // Act: run with no patterns
        var result = ContextRun.RunProgram("--root", temp.FullPath);

        // Assert: the banner is shown, the usage error says what to add, and the exit code is failure
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("TextLint Tool version", result.Output),
            () => Assert.Contains("Copyright", result.Output),
            () => Assert.Contains("No files to check", result.Output));
    }

    /// <summary>
    ///     Test that Run with a pattern dispatches to the check and reports its findings.
    /// </summary>
    [Fact]
    public void Program_Run_Patterns_RunsCheck()
    {
        // Arrange: a misspelled file named by a pattern
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: run with the pattern
        var result = ContextRun.RunProgram("--root", temp.FullPath, "a.md");

        // Assert: the banner comes first, then the finding, and the exit code is failure
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.StartsWith("TextLint Tool version", result.Lines[0]),
            () => Assert.Contains("a.md(1,7): error: Unknown word 'wrold'", result.Lines));
    }

    /// <summary>
    ///     Test that Run with the help flag lists every option and the exit codes.
    /// </summary>
    [Fact]
    public void Program_Run_HelpFlag_ListsAllOptionsAndExitCodes()
    {
        // Act: run with the help flag
        var result = ContextRun.RunProgram("--help");

        // Assert: the usage line, every option, the pattern note, and the exit codes are present
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains("Usage: textlint [options] [globs...]", result.Output),
            () => Assert.Contains("--version", result.Output),
            () => Assert.Contains("--help", result.Output),
            () => Assert.Contains("--silent", result.Output),
            () => Assert.Contains("--validate", result.Output),
            () => Assert.Contains("--results <file>", result.Output),
            () => Assert.Contains("--depth <#>", result.Output),
            () => Assert.Contains("--log <file>", result.Output),
            () => Assert.Contains("--config <file>", result.Output),
            () => Assert.Contains("--root <dir>", result.Output),
            () => Assert.Contains("--language <languages>", result.Output),
            () => Assert.Contains("--allow-empty", result.Output),
            () => Assert.Contains("--max-parallelism <#>", result.Output),
            () => Assert.Contains("Exit codes:", result.Output));
    }

    /// <summary>
    ///     Test that RunCancelable with a cancelled token reports the cancellation and fails.
    /// </summary>
    [Fact]
    public async Task Program_RunCancelable_CancelledToken_ReportsCancellationAndFails()
    {
        // Arrange: a misspelled file and a token that is already cancelled
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act: run the program logic with the cancelled token
        var result = ContextRun.Execute(["--root", temp.FullPath, "a.md"], context => Program.RunCancelable(context, source.Token));

        // Assert: the run fails with the cancellation message and reports no finding
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("cancelled", result.Output),
            () => Assert.DoesNotContain("Unknown word", result.Output));
    }

    /// <summary>
    ///     Test that Main reports malformed options as an error and returns a failure code.
    /// </summary>
    [Fact]
    public void Program_Main_DuplicateOption_ReturnsNonZeroExitCode()
    {
        // Arrange: capture standard error and name a value option twice
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: run Main
            var exitCode = Program.Main(["--root", "a", "--root", "b"]);

            // Assert: the failure code and a message that names the duplicated option
            Assert.Equal(1, exitCode);
            Assert.Contains("--root was specified more than once", errorWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Main still reports an argument error on standard error when the silent flag is given.
    /// </summary>
    [Fact]
    public void Program_Main_SilentUnknownArgument_ReportsErrorAndFails()
    {
        // Arrange: capture both console streams
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            // Act: run Main with the silent flag and an unknown option, which fails while parsing
            var exitCode = Program.Main(["--silent", "--bogus"]);

            // Assert: the error is still reported on standard error (no output settings exist yet), nothing is on standard output
            Assert.Equal(1, exitCode);
            Assert.Contains("Unsupported argument '--bogus'", errorWriter.ToString());
            Assert.Equal(string.Empty, outWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that version property returns non-empty version string.
    /// </summary>
    [Fact]
    public void Program_Version_Read_ReturnsNonEmptyString()
    {
        // Act: execute the operation being tested
        var version = Program.Version;

        // Assert: verify expected behavior
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    /// <summary>
    ///     Test that Main with invalid arguments returns non-zero exit code.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode()
    {
        // Arrange: redirect stderr to suppress error output during test
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an invalid argument
            var result = Program.Main(["--invalid-argument"]);

            // Assert: invalid arguments produce a non-zero exit code
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Run with short version flag -v displays version.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortVersionFlag_DisplaysVersion()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-v"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -h displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortHelpFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-h"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -? displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithQuestionMarkFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-?"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

