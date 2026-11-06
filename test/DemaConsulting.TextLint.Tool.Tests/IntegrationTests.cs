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

// cspell:ignore wrold xyzzyq favourite

using DemaConsulting.TextLint.Tool.Tests.TestSupport;
using DemaConsulting.TextLint.Tool.Utilities;

namespace DemaConsulting.TextLint.Tool.Tests;

/// <summary>
///     System-level integration tests that run the TextLint Tool application via dotnet.
/// </summary>
[Collection("Sequential")]
public class IntegrationTests
{
    private readonly string _dllPath;

    /// <summary>
    ///     Initialize test by locating the TextLint Tool DLL.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the main project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = PathHelpers.SafePathCombine(baseDir, "DemaConsulting.TextLint.Tool.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find TextLint Tool DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void TextLintTool_VersionFlag_Provided_OutputsVersion()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with version flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag outputs usage information.
    /// </summary>
    [Fact]
    public void TextLintTool_HelpFlag_Provided_OutputsUsageInformation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--help");

        // Assert: usage text contains required sections and every option, so users can learn the interface
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
        Assert.Contains("--config", output);
        Assert.Contains("--root", output);
        Assert.Contains("--language", output);
        Assert.Contains("--allow-empty", output);
        Assert.Contains("--max-parallelism", output);
        Assert.Contains("Exit codes:", output);
    }

    /// <summary>
    ///     Test that no arguments displays the banner and reports a usage error when nothing is selected.
    /// </summary>
    [Fact]
    public void TextLintTool_NoArguments_Invoked_DisplaysBannerAndUsageError()
    {
        // Arrange: an empty working directory, so that no settings file can name files
        using var temp = new TempDirectory();

        // Act: run the tool with no arguments in that directory
        var exitCode = Runner.RunIn(
            out var output,
            temp.FullPath,
            "dotnet",
            _dllPath);

        // Assert: the banner is displayed, the usage error says what to add, and the exit code is failure
        Assert.Equal(1, exitCode);
        Assert.Contains("TextLint Tool version", output);
        Assert.Contains("Copyright", output);
        Assert.Contains("No files to check", output);
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs summary.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateFlag_Provided_RunsValidation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate");

        // Assert: validation summary is present; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --result (legacy alias) flag generates a results file.
    /// </summary>
    [Fact]
    public void TextLintTool_ResultAlias_LegacyFlag_WritesResultsFile()
    {
        // Arrange: temporary TRX results file path; use legacy --result alias
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with legacy --result alias
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--result",
                resultsFile);

            // Assert: results file is created; exit code is success
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created for legacy --result alias");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with an unsupported results extension returns a non-zero exit code.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero()
    {
        // Arrange: unsupported results file extension triggers WriteError → ExitCode 1
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.bad");

        try
        {
            // Act: run validation with unsupported extension
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: non-zero exit code indicates the error path was triggered
            Assert.NotEqual(0, exitCode);
            Assert.False(File.Exists(resultsFile), "No file should be created for unsupported extension");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void TextLintTool_SilentFlag_Provided_SuppressesOutput()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version",
            "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    [Fact]
    public void TextLintTool_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag (help is used because it needs nothing to check)
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--help",
                "--log",
                logFile);

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("TextLint Tool version", logContent);
            Assert.Contains("TextLint Tool version", output);
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
    ///     Test that validate with --results flag generates a JUnit XML file.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile()
    {
        // Arrange: temporary XML results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.xml");

        try
        {
            // Act: run validation with JUnit XML results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid JUnit structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var xmlContent = File.ReadAllText(resultsFile);
            Assert.Contains("<testsuites", xmlContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unknown argument causes an error message and non-zero exit code.
    /// </summary>
    [Fact]
    public void TextLintTool_UnknownArgument_Provided_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with an unknown argument
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized flag
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that validate with depth flag outputs headings at the specified depth.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run validation with heading depth 3
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate",
            "--depth",
            "3");

        // Assert: output contains level-3 markdown headings
        Assert.Equal(0, exitCode);
        Assert.Contains("###", output);
    }

    /// <summary>
    ///     Runs the tool in a directory with the given arguments.
    /// </summary>
    /// <param name="directory">The working directory of the process.</param>
    /// <param name="output">Receives the combined standard output and standard error.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The exit code.</returns>
    private int RunTool(string directory, out string output, params string[] arguments) =>
        Runner.RunIn(out output, directory, "dotnet", [_dllPath, .. arguments]);

    /// <summary>
    ///     Test that a misspelled word is reported in compiler style and fails the run.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_MisspelledFile_ReportsIssueAndExitsOne()
    {
        // Arrange: a file with a misspelled word
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: check the file
        var exitCode = RunTool(temp.FullPath, out var output, "a.md");

        // Assert: the issue is reported as file(line,column): error: message, and the exit code is 1
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md(1,7): error: Unknown word 'wrold'", output);
    }

    /// <summary>
    ///     Test that a clean file succeeds and the summary reports the files checked.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_CleanFile_ExitsZero()
    {
        // Arrange: a correctly spelled file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check the file
        var exitCode = RunTool(temp.FullPath, out var output, "a.md");

        // Assert: exit code 0 and a summary of the files checked
        Assert.Equal(0, exitCode);
        Assert.Contains("Checked 1 file(s): 0 issue(s) in 0 file(s), 0 skipped, 0 could not be checked.", output);
    }

    /// <summary>
    ///     Test that the settings file in the working directory is found and applied.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_DefaultSettingsFile_AppliesSettings()
    {
        // Arrange: a settings file in the working directory that selects markdown and flags a word
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - \"*.md\"\nflagWords:\n  - \"utilize->use\"\n");
        temp.CreateFile("a.md", "We utilize it\n");

        // Act: run with no arguments, so that the default settings file is the only source of files
        var exitCode = RunTool(temp.FullPath, out var output);

        // Assert: the settings selected the file and flagged the word with its suggestion
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md(1,4): error: Flagged word 'utilize'; suggestions: use", output);
    }

    /// <summary>
    ///     Test that the settings file named by the config option is used.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_ConfigOption_UsesNamedFile()
    {
        // Arrange: a settings file with another name that accepts the misspelled word
        using var temp = new TempDirectory();
        temp.CreateFile("custom.yaml", "words:\n  - wrold\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: name the settings file on the command line
        var exitCode = RunTool(temp.FullPath, out var output, "--config", "custom.yaml", "a.md");

        // Assert: the word is accepted and the run succeeds
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Unknown word", output);
    }

    /// <summary>
    ///     Test that a missing named settings file is an error.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_ConfigOption_MissingFile_ReportsErrorAndExitsOne()
    {
        // Arrange: a file to check and a settings file name that does not exist
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: name the missing settings file
        var exitCode = RunTool(temp.FullPath, out var output, "--config", "missing.yaml", "a.md");

        // Assert: the error names the file, nothing is checked, and the exit code is 1
        Assert.Equal(1, exitCode);
        Assert.Contains("missing.yaml: error: Cannot read settings file", output);
        Assert.DoesNotContain("Checked", output);
    }

    /// <summary>
    ///     Test that every settings error is reported with its position and nothing is checked.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_InvalidSettings_ReportsErrorAndExitsOne()
    {
        // Arrange: a settings file with an unsupported key
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "bogusKey: true\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: check a file
        var exitCode = RunTool(temp.FullPath, out var output, "a.md");

        // Assert: the error is reported at its line and column, nothing is checked, and the exit code is 1
        Assert.Equal(1, exitCode);
        Assert.Contains(".textlint.yaml(1,1): error: ", output);
        Assert.DoesNotContain("Checked", output);
    }

    /// <summary>
    ///     Test that patterns on the command line replace the files setting.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_GlobArguments_ReplaceConfiguredFiles()
    {
        // Arrange: settings that name b.md, and two misspelled files
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - b.md\n");
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("b.md", "Hello xyzzyq\n");

        // Act: name a.md on the command line
        var exitCode = RunTool(temp.FullPath, out var output, "a.md");

        // Assert: only a.md is checked
        Assert.Equal(1, exitCode);
        Assert.Contains("a.md(1,7)", output);
        Assert.DoesNotContain("b.md", output);
    }

    /// <summary>
    ///     Test that the root option sets where patterns and the default settings file are found.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_RootOption_ResolvesPathsFromRoot()
    {
        // Arrange: a project directory with a settings file, and an unrelated working directory
        using var project = new TempDirectory();
        using var elsewhere = new TempDirectory();
        project.CreateFile(".textlint.yaml", "words:\n  - wrold\n");
        project.CreateFile("docs/a.md", "Hello wrold xyzzyq\n");

        // Act: run from the other directory with the project as the root
        var exitCode = RunTool(elsewhere.FullPath, out var output, "--root", project.FullPath, "docs/*.md");

        // Assert: the root's settings were applied and the path is relative to the root
        Assert.Equal(1, exitCode);
        Assert.Contains("docs/a.md(1,13): error: Unknown word 'xyzzyq'", output);
        Assert.DoesNotContain("'wrold'", output);
    }

    /// <summary>
    ///     Test that a file named by an absolute path is reported by that absolute path.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_AbsolutePath_ReportsAbsolutePath()
    {
        // Arrange: a misspelled file and a working directory elsewhere
        using var project = new TempDirectory();
        using var elsewhere = new TempDirectory();
        var file = project.CreateFile("docs/a.md", "Hello wrold\n");

        // Act: name the file by its absolute path from the other directory
        var exitCode = RunTool(elsewhere.FullPath, out var output, file);

        // Assert: the issue is reported under the absolute path
        Assert.Equal(1, exitCode);
        Assert.Contains($"{file}(1,7): error: Unknown word 'wrold'", output);
    }

    /// <summary>
    ///     Test that a relative path written with the platform's own separator is accepted.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_PlatformRelativePath_ChecksFile()
    {
        // Arrange: a misspelled file in a subdirectory, named the way the shell of this platform completes it
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", "Hello wrold\n");
        var argument = OperatingSystem.IsWindows() ? @".\docs\a.md" : "./docs/a.md";

        // Act: check it
        var exitCode = RunTool(temp.FullPath, out var output, argument);

        // Assert: the file is found and reported by its portable relative path
        Assert.Equal(1, exitCode);
        Assert.Contains("docs/a.md(1,7): error: Unknown word 'wrold'", output);
    }

    /// <summary>
    ///     Test that the language option selects the dictionary.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_LanguageOption_SelectsDictionary()
    {
        // Arrange: a file with the British spelling
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "favourite\n");

        // Act: check it as American English and as British English
        var americanExit = RunTool(temp.FullPath, out var americanOutput, "--language", "en-US", "a.md");
        var britishExit = RunTool(temp.FullPath, out var britishOutput, "--language", "en-GB", "a.md");

        // Assert: only the American run reports the word
        Assert.Equal(1, americanExit);
        Assert.Contains("Unknown word 'favourite'", americanOutput);
        Assert.Equal(0, britishExit);
        Assert.DoesNotContain("Unknown word", britishOutput);
    }

    /// <summary>
    ///     Test that a selection that matches no files fails.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_NoFilesMatched_ExitsOne()
    {
        // Arrange: a directory with a file that the pattern does not match
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check a pattern that matches nothing
        var exitCode = RunTool(temp.FullPath, out var output, "*.nomatch");

        // Assert: the failure explains itself and names the way to accept an empty selection
        Assert.Equal(1, exitCode);
        Assert.Contains("No files matched the file selection", output);
        Assert.Contains("--allow-empty", output);
    }

    /// <summary>
    ///     Test that an empty selection succeeds when it is allowed.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_NoFilesMatchedAllowEmpty_ExitsZero()
    {
        // Arrange: a directory with a file that the pattern does not match
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check a pattern that matches nothing, allowing that
        var exitCode = RunTool(temp.FullPath, out var output, "--allow-empty", "*.nomatch");

        // Assert: the run succeeds and the summary shows that nothing was checked
        Assert.Equal(0, exitCode);
        Assert.Contains("Checked 0 file(s)", output);
    }

    /// <summary>
    ///     Test that a binary file is reported as information and does not fail the run.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_BinaryFile_ReportsInfoAndExitsZero()
    {
        // Arrange: a binary file beside a clean text file
        using var temp = new TempDirectory();
        temp.CreateBinaryFile("data.bin", [0x00, 0x01]);
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: select everything
        var exitCode = RunTool(temp.FullPath, out var output, "*");

        // Assert: the skipped file is reported as information, counted, and does not fail the run
        Assert.Equal(0, exitCode);
        Assert.Contains("data.bin: info: ", output);
        Assert.Contains("1 skipped", output);
    }

    /// <summary>
    ///     Test that the degree of parallelism does not change the result.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_MaxParallelism_GivesSameResult()
    {
        // Arrange: several misspelled files
        using var temp = new TempDirectory();
        for (var index = 0; index < 8; index++)
        {
            temp.CreateFile($"f{index}.md", $"Hello wrold {index}\n");
        }

        // Act: check with one worker and with several
        var singleExit = RunTool(temp.FullPath, out var single, "--max-parallelism", "1", "*.md");
        var manyExit = RunTool(temp.FullPath, out var many, "--max-parallelism", "4", "*.md");

        // Assert: identical findings and exit code (the banner is identical too)
        Assert.Equal(singleExit, manyExit);
        Assert.Equal(single, many);
    }

    /// <summary>
    ///     Test that a malformed option is rejected with an error and a failure code.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_DuplicateOption_ReportsErrorAndExitsOne()
    {
        // Arrange: a working directory with a file, and an option given twice
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: name the root twice
        var exitCode = RunTool(temp.FullPath, out var output, "--root", ".", "--root", ".", "a.md");

        // Assert: the error names the option and nothing is checked
        Assert.Equal(1, exitCode);
        Assert.Contains("--root was specified more than once", output);
        Assert.DoesNotContain("Checked", output);
    }

    /// <summary>
    ///     Test that a root directory that does not exist is an error and not an unhandled exception.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_MissingRoot_ReportsErrorAndExitsOne()
    {
        // Arrange: a root directory that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "nowhere");

        // Act: check against it
        var exitCode = RunTool(temp.FullPath, out var output, "--root", missing, "*.md");

        // Assert: the error names the directory, there is no stack trace, and the exit code is 1
        Assert.Equal(1, exitCode);
        Assert.Contains(missing, output);
        Assert.DoesNotContain("Unhandled exception", output);
    }

    /// <summary>
    ///     Test that an unsupported pattern is an error that names the pattern.
    /// </summary>
    [Fact]
    public void TextLintTool_Check_InvalidPattern_ReportsErrorAndExitsOne()
    {
        // Arrange: a working directory and a pattern that uses an unsupported wildcard
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check with the pattern
        var exitCode = RunTool(temp.FullPath, out var output, "a?.md");

        // Assert: the error names the pattern and the exit code is 1
        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid glob pattern 'a?.md'", output);
    }

    /// <summary>
    ///     Test that self-validation includes the spell-checking scenarios and that they pass.
    /// </summary>
    [Fact]
    public void TextLintTool_ValidateFlag_Provided_RunsLintScenarios()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run self-validation
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate");

        // Assert: every spell-checking scenario ran and passed
        Assert.Equal(0, exitCode);
        string[] scenarios =
        [
            "TextLintTool_CheckCleanFile",
            "TextLintTool_CheckMisspelling",
            "TextLintTool_CheckSettingsFile",
            "TextLintTool_CheckSettingsError",
            "TextLintTool_CheckGlobsReplaceFiles",
            "TextLintTool_CheckInlineDirective",
            "TextLintTool_CheckLanguageOption",
            "TextLintTool_CheckBinaryFileSkipped",
            "TextLintTool_CheckNoFilesMatched",
            "TextLintTool_CheckAbsolutePath"
        ];
        foreach (var scenario in scenarios)
        {
            Assert.Contains($"{scenario} - Passed", output);
        }
    }
}

