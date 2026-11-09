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

namespace DemaConsulting.TextLint.Tool.Tests;

/// <summary>
///     Unit tests for the Context class.
/// </summary>
[Collection("Sequential")]
public class ContextTests
{
    /// <summary>
    ///     Test creating a context with no arguments.
    /// </summary>
    [Fact]
    public void Context_Create_NoArguments_ReturnsDefaultContext()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.False(context.Help);
        Assert.False(context.Silent);
        Assert.False(context.Validate);
        Assert.Null(context.ResultsFile);
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the version flag.
    /// </summary>
    [Fact]
    public void Context_Create_VersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--version"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short version flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortVersionFlag_SetsVersionTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-v"]);

        // Assert: verify expected behavior
        Assert.True(context.Version);
        Assert.False(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_HelpFlag_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--help"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -h.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_H_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-h"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the short help flag -?.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_Question_SetsHelpTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["-?"]);

        // Assert: verify expected behavior
        Assert.False(context.Version);
        Assert.True(context.Help);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the silent flag.
    /// </summary>
    [Fact]
    public void Context_Create_SilentFlag_SetsSilentTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--silent"]);

        // Assert: verify expected behavior
        Assert.True(context.Silent);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the validate flag.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateFlag_SetsValidateTrue()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--validate"]);

        // Assert: verify expected behavior
        Assert.True(context.Validate);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the results flag.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_SetsResultsFile()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--results", "test.trx"]);

        // Assert: verify expected behavior
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with the log flag.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_OpensLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act: execute the operation being tested
            using (var context = Context.Create(["--log", logFile]))
            {
                context.WriteLine("Test message");
                Assert.Equal(0, context.ExitCode);
            }

            // Assert: verify expected behavior
            // Verify log file was written
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test message", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test creating a context with an unknown argument throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownArgument_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--unknown"]));
        Assert.Contains("Unsupported argument", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --log flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--log"]));
        Assert.Contains("--log", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --results flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--results"]));
        Assert.Contains("--results", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the --result alias flag (legacy alias for --results).
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_SetsResultsFile()
    {
        // Act: execute the operation using the legacy --result alias
        using var context = Context.Create(["--result", "test.trx"]);

        // Assert: verify --result sets ResultsFile identically to --results
        Assert.Equal("test.trx", context.ResultsFile);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --result flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--result"]));
        Assert.Contains("--result", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with the depth flag.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_SetsHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create(["--depth", "3"]);

        // Assert: verify expected behavior
        Assert.Equal(3, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with no depth flag returns default heading depth of 1.
    /// </summary>
    [Fact]
    public void Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth()
    {
        // Act: execute the operation being tested
        using var context = Context.Create([]);

        // Assert: verify default depth is 1
        Assert.Equal(1, context.HeadingDepth);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test creating a context with --depth flag but no value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and non-integer value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "abc"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and zero value throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "0"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test creating a context with --depth flag and value exceeding maximum throws exception.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "7"]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test WriteLine writes to console output when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteLine does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteLine("Test message");

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.DoesNotContain("Test message", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test WriteError does not write to console when silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_Silent_DoesNotWriteToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["--silent"]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert - error output should be suppressed in silent mode
            var output = errWriter.ToString();
            Assert.DoesNotContain("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError sets exit code to 1.
    /// </summary>
    [Fact]
    public void Context_WriteError_Called_SetsErrorExitCode()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to console when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_NotSilent_WritesToConsole()
    {
        // Arrange: setup test conditions
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            context.WriteError("Test error message");

            // Assert: verify expected behavior
            var output = errWriter.ToString();
            Assert.Contains("Test error message", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test WriteError writes message to log file when logging is enabled.
    /// </summary>
    [Fact]
    public void Context_WriteError_WithLogFile_WritesToLogFile()
    {
        // Arrange: setup test conditions
        var logFile = Path.GetTempFileName();
        try
        {
            // Act - use silent to avoid console output; verify the error still goes to the log
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteError("Test error in log");
                Assert.Equal(1, context.ExitCode);
            }

            // Assert - log file should contain the error message
            Assert.True(File.Exists(logFile));
            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Test error in log", logContent);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test creating a context with --log flag pointing to an invalid path throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException()
    {
        // Arrange: a path that cannot be opened as a file (directory or invalid characters)
        // Use a directory path so it cannot be opened as a file
        var invalidLogPath = Path.GetTempPath(); // temp directory itself, not a file

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => Context.Create(["--log", invalidLogPath]));
    }

    /// <summary>
    ///     Test that the options of a check default to unset.
    /// </summary>
    [Fact]
    public void Context_Create_NoCheckOptions_ReturnsCheckDefaults()
    {
        // Act: create a context with no arguments
        using var context = Context.Create([]);

        // Assert: no patterns, no overrides, and an empty selection is not accepted
        Assert.Multiple(
            () => Assert.Empty(context.Globs),
            () => Assert.Null(context.ConfigFile),
            () => Assert.Null(context.Root),
            () => Assert.Null(context.Language),
            () => Assert.False(context.AllowEmpty),
            () => Assert.Null(context.MaxParallelism));
    }

    /// <summary>
    ///     Test that positional arguments become the patterns, exactly as typed and in order.
    /// </summary>
    [Fact]
    public void Context_Create_PositionalArguments_SetsGlobsInOrder()
    {
        // Act: create a context with a relative pattern, a backslash path, and an absolute path
        using var context = Context.Create(["docs/*.md", @".\src\a.cs", "/abs/path/b.md"]);

        // Assert: nothing is interpreted or rewritten by the parser
        Assert.Equal(["docs/*.md", @".\src\a.cs", "/abs/path/b.md"], context.Globs);
    }

    /// <summary>
    ///     Test that the patterns of a context cannot be changed through a mutable interface.
    /// </summary>
    [Fact]
    public void Context_Create_Globs_AreImmutable()
    {
        // Arrange: a context with one pattern
        using var context = Context.Create(["a.md"]);

        // Act: try to change the patterns through a mutable interface
        var globs = Assert.IsType<IList<string>>(context.Globs, exactMatch: false);

        // Assert: the collection refuses changes
        Assert.Throws<NotSupportedException>(() => globs.Add("b.md"));
    }

    /// <summary>
    ///     Test that options and patterns can be mixed in any order.
    /// </summary>
    [Fact]
    public void Context_Create_OptionsAndGlobsMixed_ParsesBoth()
    {
        // Act: create a context with a pattern before, between, and after options
        using var context = Context.Create(["a.md", "--config", "c.yaml", "b.md", "--silent", "c.md"]);

        // Assert: the option values are not mistaken for patterns
        Assert.Multiple(
            () => Assert.Equal(["a.md", "b.md", "c.md"], context.Globs),
            () => Assert.Equal("c.yaml", context.ConfigFile),
            () => Assert.True(context.Silent));
    }

    /// <summary>
    ///     Test creating a context with the config flag.
    /// </summary>
    [Fact]
    public void Context_Create_ConfigFlag_SetsConfigFile()
    {
        // Act: create a context with the config flag
        using var context = Context.Create(["--config", "my settings.yaml"]);

        // Assert: the value is kept as typed
        Assert.Equal("my settings.yaml", context.ConfigFile);
    }

    /// <summary>
    ///     Test creating a context with the root flag.
    /// </summary>
    [Fact]
    public void Context_Create_RootFlag_SetsRoot()
    {
        // Act: create a context with the root flag
        using var context = Context.Create(["--root", "some/dir"]);

        // Assert: the value is kept as typed
        Assert.Equal("some/dir", context.Root);
    }

    /// <summary>
    ///     Test creating a context with the language flag.
    /// </summary>
    [Fact]
    public void Context_Create_LanguageFlag_SetsLanguage()
    {
        // Act: create a context with a list of languages
        using var context = Context.Create(["--language", "en-US,en-GB"]);

        // Assert: the text is passed on unchanged for the library to validate
        Assert.Equal("en-US,en-GB", context.Language);
    }

    /// <summary>
    ///     Test creating a context with the allow-empty flag.
    /// </summary>
    [Fact]
    public void Context_Create_AllowEmptyFlag_SetsAllowEmptyTrue()
    {
        // Act: create a context with the allow-empty flag
        using var context = Context.Create(["--allow-empty"]);

        // Assert: the flag is set
        Assert.True(context.AllowEmpty);
    }

    /// <summary>
    ///     Test creating a context with the max-parallelism flag.
    /// </summary>
    [Fact]
    public void Context_Create_MaxParallelismFlag_SetsMaxParallelism()
    {
        // Act: create a context with a degree of parallelism
        using var context = Context.Create(["--max-parallelism", "4"]);

        // Assert: the number is parsed
        Assert.Equal(4, context.MaxParallelism);
    }

    /// <summary>
    ///     Test that an option that takes a value is rejected when the value is missing.
    /// </summary>
    [Theory]
    [InlineData("--config")]
    [InlineData("--root")]
    [InlineData("--language")]
    [InlineData("--max-parallelism")]
    public void Context_Create_ValueOptionWithoutValue_ThrowsArgumentException(string option)
    {
        // Act & Assert: the option is last on the command line
        var exception = Assert.Throws<ArgumentException>(() => Context.Create([option]));
        Assert.Contains($"{option} requires", exception.Message);
    }

    /// <summary>
    ///     Test that a value that is empty, white space, or looks like the next option counts as missing.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("--silent")]
    public void Context_Create_ValueOptionWithBlankOrOptionValue_ThrowsArgumentException(string value)
    {
        // Act & Assert: the value is a parameter, and the message names the option
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--config", value]));
        Assert.Contains("--config requires", exception.Message);
    }

    /// <summary>
    ///     Test that a value of the max-parallelism flag outside 1 to the largest integer is rejected.
    /// </summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    public void Context_Create_MaxParallelismFlag_InvalidValue_ThrowsArgumentException(string value)
    {
        // Act & Assert: the value is a parameter, and the message states the allowed range
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--max-parallelism", value]));
        Assert.Contains("--max-parallelism requires an integer between 1 and 2147483647", exception.Message);
    }

    /// <summary>
    ///     Test that an option that takes a value is rejected when it is given twice.
    /// </summary>
    [Theory]
    [InlineData("--config", "a.yaml", "b.yaml")]
    [InlineData("--root", "a", "b")]
    [InlineData("--language", "en-US", "en-GB")]
    [InlineData("--max-parallelism", "1", "2")]
    [InlineData("--log", "a.log", "b.log")]
    [InlineData("--depth", "1", "2")]
    public void Context_Create_DuplicateValueOption_ThrowsArgumentException(string option, string first, string second)
    {
        // Act & Assert: the second occurrence is reported by name
        var exception = Assert.Throws<ArgumentException>(() => Context.Create([option, first, option, second]));
        Assert.Contains($"{option} was specified more than once", exception.Message);
    }

    /// <summary>
    ///     Test that the results option and its alias count as one option.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsAndAlias_ThrowsArgumentException()
    {
        // Act & Assert: giving both spellings is a duplicate
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--results", "a.trx", "--result", "b.trx"]));
        Assert.Contains("--result was specified more than once", exception.Message);
    }

    /// <summary>
    ///     Test that an unknown option after patterns is still rejected, and not taken for a pattern.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownOptionAfterGlob_ThrowsArgumentException()
    {
        // Act & Assert: the dash makes it an option, so it must be a known one
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["a.md", "--bogus"]));
        Assert.Contains("Unsupported argument '--bogus'", exception.Message);
    }

    /// <summary>
    ///     Test that an empty or white-space pattern is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Context_Create_BlankGlob_ThrowsArgumentException(string glob)
    {
        // Act & Assert: the pattern is a parameter
        var exception = Assert.Throws<ArgumentException>(() => Context.Create([glob]));
        Assert.Contains("must not be empty", exception.Message);
    }

    /// <summary>
    ///     Test that marking a context failed sets the exit code without writing anything.
    /// </summary>
    [Fact]
    public void Context_MarkFailed_Called_SetsErrorExitCodeSilently()
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
            using var context = Context.Create([]);

            // Act: mark the run as failed
            context.MarkFailed();

            // Assert: the exit code is 1 and nothing was written
            Assert.Equal(1, context.ExitCode);
            Assert.Equal(string.Empty, outWriter.ToString());
            Assert.Equal(string.Empty, errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}


