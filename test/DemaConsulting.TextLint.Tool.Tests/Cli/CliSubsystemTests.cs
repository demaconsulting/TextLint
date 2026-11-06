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

// cspell:ignore wrold favourite

using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tool.Tests.Cli;

/// <summary>
///     Subsystem tests for the CLI subsystem covering Context and Program integration.
/// </summary>
[Collection("Sequential")]
public class CliSubsystemTests
{
    /// <summary>
    ///     Test that Context and Program work together to handle version flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits()
    {
        // Arrange: command line arguments with version flag; capture console output
        var args = new[] { "--version" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: version flag is parsed, version text is displayed, and exit code is success
            Assert.True(context.Version, "Context should parse version flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains(Program.Version, capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -v short version flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag()
    {
        // Arrange: command line arguments with -v short version flag; capture console output
        var args = new[] { "-v" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: version flag is parsed, version text is displayed, and exit code is success
            Assert.True(context.Version, "Context should parse -v flag as version");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains(Program.Version, capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle help flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits()
    {
        // Arrange: command line arguments with help flag; capture console output
        var args = new[] { "--help" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse help flag");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -? short help flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag()
    {
        // Arrange: command line arguments with -? short help flag; capture console output
        var args = new[] { "-?" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse -? flag as help");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the -h short help flag.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag()
    {
        // Arrange: command line arguments with -h short help flag; capture console output
        var args = new[] { "-h" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: help flag is parsed, usage text is displayed, and exit code is success
            Assert.True(context.Help, "Context should parse -h flag as help");
            Assert.Equal(0, context.ExitCode);
            var output = capturedOut.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle validation flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits()
    {
        // Arrange: command line arguments with validate flag; capture console output
        var args = new[] { "--validate" };
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: validate flag is parsed, summary is output, and exit code is success
            Assert.True(context.Validate, "Context should parse validate flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Contains("Total Tests:", capturedOut.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle silent flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput()
    {
        // Arrange: command line arguments with version and silent flags; capture console streams
        var args = new[] { "--version", "--silent" };
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var capturedOut = new StringWriter();
        using var capturedError = new StringWriter();

        try
        {
            Console.SetOut(capturedOut);
            Console.SetError(capturedError);

            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: silent flag is parsed, exit code is success, and no console output is produced
            Assert.True(context.Silent, "Context should parse silent flag");
            Assert.Equal(0, context.ExitCode);
            Assert.Equal(string.Empty, capturedOut.ToString());
            Assert.Equal(string.Empty, capturedError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle results flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile()
    {
        // Arrange: temporary results file path and validation command with results output
        var tempDir = Path.GetTempPath();
        var resultsFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.trx");
        var args = new[] { "--validate", "--silent", "--results", resultsFile };

        try
        {
            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: results flag is parsed, validation runs, and results file is written
            Assert.Equal(resultsFile, context.ResultsFile);
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile), "Results file should be written to specified path");
        }
        finally
        {
            // Cleanup
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle log flag workflow.
    /// </summary>
    [Fact]
    public void CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile()
    {
        // Arrange: temporary log file path and version command with log output
        var tempDir = Path.GetTempPath();
        var logFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.log");
        var args = new[] { "--version", "--log", logFile };

        try
        {
            // Act: create context and run program logic
            using (var context = Context.Create(args))
            {
                Program.Run(context);

                // Assert: version flag is parsed and exit code is success
                Assert.True(context.Version, "Context should parse version flag");
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: log file is written with version output
            Assert.True(File.Exists(logFile), "Log file should be created at specified path");
            var logContent = File.ReadAllText(logFile);
            Assert.False(string.IsNullOrWhiteSpace(logContent), "Log file should contain version output");
        }
        finally
        {
            // Cleanup
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Program rejects unknown arguments, writes an error to stderr, and exits non-zero.
    /// </summary>
    [Fact]
    public void CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero()
    {
        // Arrange: unknown command-line argument
        var args = new[] { "--unknown-flag" };

        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke the actual CLI entry point with an unknown flag
            var result = Program.Main(args);

            // Assert: invalid arguments produce a non-zero exit code and an error on stderr
            Assert.Equal(1, result);
            var errorOutput = errWriter.ToString();
            Assert.False(string.IsNullOrWhiteSpace(errorOutput), "Program should write an error to stderr for unknown arguments");
            Assert.Contains("--unknown-flag", errorOutput);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context writes error messages to stderr.
    /// </summary>
    [Fact]
    public void CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr()
    {
        // Arrange: redirect stderr to capture error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: write an error message through the context
            context.WriteError("Test error message");

            // Assert: error is written to stderr and exit code reflects failure
            var errorOutput = errWriter.ToString();
            Assert.Contains("Test error message", errorOutput);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle the --result legacy alias for results.
    /// </summary>
    [Fact]
    public void CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile()
    {
        // Arrange: temporary results file path and validation command with legacy --result alias
        var tempDir = Path.GetTempPath();
        var resultsFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.trx");
        var args = new[] { "--validate", "--silent", "--result", resultsFile };

        try
        {
            // Act: create context and run program logic
            using var context = Context.Create(args);
            Program.Run(context);

            // Assert: legacy --result alias is parsed, validation runs, and results file is written
            Assert.Equal(resultsFile, context.ResultsFile);
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile), "Results file should be written to specified path");
        }
        finally
        {
            // Cleanup
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to handle depth flag with self-validation.
    /// </summary>
    [Fact]
    public void CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth()
    {
        // Arrange: command line with --validate, --depth 2, and a log file to capture output
        var tempDir = Path.GetTempPath();
        var logFile = Path.Combine(tempDir, $"cli_test_{Guid.NewGuid()}.log");
        var args = new[] { "--validate", "--silent", "--depth", "2", "--log", logFile };

        try
        {
            // Act: create context and run program logic
            using (var context = Context.Create(args))
            {
                Program.Run(context);

                // Assert: depth is parsed correctly
                Assert.Equal(2, context.HeadingDepth);
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: log contains level-2 heading
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("## DEMA Consulting", logContent);
        }
        finally
        {
            // Cleanup
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that Context and Program work together to pass the patterns to the check.
    /// </summary>
    [Fact]
    public void CliSubsystem_PatternFlow_ContextAndProgram_ChecksNamedFiles()
    {
        // Arrange: a misspelled file and a clean file in a private root directory
        using var temp = new TempDirectory();
        temp.CreateFile("bad.md", "Hello wrold\n");
        temp.CreateFile("good.md", "Hello world.\n");

        // Act: name only the misspelled file
        var result = ContextRun.RunProgram("--root", temp.FullPath, "bad.md");

        // Assert: the parsed pattern reached the check, which reported the file and failed
        Assert.Multiple(
            () => Assert.Contains("bad.md(1,7): error: Unknown word 'wrold'", result.Lines),
            () => Assert.DoesNotContain("good.md", result.Output),
            () => Assert.Equal(1, result.ExitCode));
    }

    /// <summary>
    ///     Test that Context and Program work together to apply every check option.
    /// </summary>
    [Fact]
    public void CliSubsystem_OptionFlow_ContextAndProgram_AppliesCheckOptions()
    {
        // Arrange: a settings file outside the root that selects British English, and a file with both spellings
        using var temp = new TempDirectory();
        using var other = new TempDirectory();
        var config = other.CreateFile("c.yaml", "language: en-GB\n");
        temp.CreateFile("a.md", "favorite favourite\n");

        // Act: name the settings file, override its language, and limit the parallelism
        var result = ContextRun.RunProgram(
            "--root", temp.FullPath, "--config", config, "--language", "en-US", "--max-parallelism", "2", "a.md");

        // Assert: the root, the settings file, the language override, and the limit all took effect
        Assert.Multiple(
            () => Assert.Contains("a.md(1,10): error: Unknown word 'favourite'", result.Lines),
            () => Assert.DoesNotContain("Unknown word 'favorite'", result.Output),
            () => Assert.Equal(1, result.ExitCode));
    }

    /// <summary>
    ///     Test that Context and Program work together to turn the allow-empty option into an exit code.
    /// </summary>
    [Fact]
    public void CliSubsystem_ExitCodeFlow_ContextAndProgram_FailsUnlessEmptyAllowed()
    {
        // Arrange: a directory with a clean file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check a clean file, then a pattern that matches nothing without and with the option
        var clean = ContextRun.RunProgram("--root", temp.FullPath, "a.md");
        var empty = ContextRun.RunProgram("--root", temp.FullPath, "*.nomatch");
        var allowed = ContextRun.RunProgram("--root", temp.FullPath, "--allow-empty", "*.nomatch");

        // Assert: only the empty selection without the option fails
        Assert.Multiple(
            () => Assert.Equal(0, clean.ExitCode),
            () => Assert.Equal(1, empty.ExitCode),
            () => Assert.Equal(0, allowed.ExitCode));
    }

    /// <summary>
    ///     Test that Context and Program work together to reject malformed options with a failure code.
    /// </summary>
    [Fact]
    public void CliSubsystem_InvalidOptionFlow_ContextAndProgram_RejectsMalformedOptions()
    {
        // Arrange: capture standard error, because Main reports argument errors there
        var originalError = Console.Error;
        try
        {
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            // Act: a missing value, a repeated option, an out-of-range number, and an unknown option
            var missing = Program.Main(["--config"]);
            var duplicate = Program.Main(["--language", "en-US", "--language", "en-GB"]);
            var range = Program.Main(["--max-parallelism", "0"]);
            var unknown = Program.Main(["a.md", "--bogus"]);

            // Assert: each is a failure with a message that names the problem
            var errors = errorWriter.ToString();
            Assert.Multiple(
                () => Assert.Equal(1, missing),
                () => Assert.Equal(1, duplicate),
                () => Assert.Equal(1, range),
                () => Assert.Equal(1, unknown),
                () => Assert.Contains("--config requires", errors),
                () => Assert.Contains("--language was specified more than once", errors),
                () => Assert.Contains("--max-parallelism requires an integer between 1 and", errors),
                () => Assert.Contains("Unsupported argument '--bogus'", errors));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
