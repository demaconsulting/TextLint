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

using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Linting;
using DemaConsulting.TextLint.Tool.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tool.Tests.Linting;

/// <summary>
///     Unit tests for the <see cref="LintRunner"/> class.
/// </summary>
/// <remarks>
///     The runner is tested with the real library behind it, because its documented dependencies are the
///     library, the context, and the reporter, and because what it must prove (settings found, patterns
///     applied, failures reported) is only observable through them. Every test works in its own temporary
///     directory selected with the root option, so no test depends on or changes the current directory.
///     <c>wrold</c> and <c>xyzzyq</c> are misspellings that no dictionary holds.
/// </remarks>
[Collection("Sequential")]
public class LintRunnerTests
{
    /// <summary>
    ///     Runs the runner with the arguments, using the temporary directory as the root.
    /// </summary>
    /// <param name="temp">The temporary directory, used as the root.</param>
    /// <param name="arguments">The command-line arguments after the root option.</param>
    /// <returns>The exit code and everything the run wrote.</returns>
    private static ContextRunResult Run(TempDirectory temp, params string[] arguments) =>
        ContextRun.Execute(["--root", temp.FullPath, .. arguments], context => LintRunner.Run(context, TestContext.Current.CancellationToken));

    /// <summary>
    ///     Test that a misspelled file is reported at its position and fails the run.
    /// </summary>
    [Fact]
    public void LintRunner_Run_MisspelledFile_ReportsIssueAndFails()
    {
        // Arrange: a file whose second word is misspelled
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: check the file
        var result = Run(temp, "a.md");

        // Assert: the issue is in compiler style, the summary counts it, and the exit code is 1
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("a.md(1,7): error: Unknown word 'wrold'", result.Lines),
            () => Assert.Contains("Checked 1 file(s): 1 issue(s) in 1 file(s), 0 skipped, 0 could not be checked.", result.Lines));
    }

    /// <summary>
    ///     Test that a clean file succeeds with an empty issue count.
    /// </summary>
    [Fact]
    public void LintRunner_Run_CleanFile_Succeeds()
    {
        // Arrange: a correctly spelled file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check the file
        var result = Run(temp, "a.md");

        // Assert: exit code 0 and a summary without issues
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains("Checked 1 file(s): 0 issue(s) in 0 file(s), 0 skipped, 0 could not be checked.", result.Lines));
    }

    /// <summary>
    ///     Test that the settings file in the root directory is found and applied.
    /// </summary>
    [Fact]
    public void LintRunner_Run_DefaultSettingsFile_IsApplied()
    {
        // Arrange: a root settings file that selects markdown, accepts one word, and ignores one file
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - \"*.md\"\nignorePaths:\n  - old.md\nwords:\n  - wrold\n");
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("old.md", "Hello xyzzyq\n");

        // Act: run with no patterns on the command line
        var result = Run(temp);

        // Assert: the accepted word and the ignored file are silent, the run is clean, and no 'not found' notice appears
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.DoesNotContain("No settings file found", result.Output),
            () => Assert.Contains("Checked 1 file(s): 0 issue(s)", result.Output));
    }

    /// <summary>
    ///     Test that a missing default settings file leads to the default settings and a notice.
    /// </summary>
    [Fact]
    public void LintRunner_Run_NoSettingsFile_UsesDefaultsAndSaysSo()
    {
        // Arrange: a directory without a settings file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: check a file named on the command line
        var result = Run(temp, "a.md");

        // Assert: the run succeeds with the default settings and says that no file was found
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains($"info: No settings file found in '{temp.FullPath}'; using the default settings.", result.Lines));
    }

    /// <summary>
    ///     Test that the settings file named by the config option is used.
    /// </summary>
    [Fact]
    public void LintRunner_Run_ConfigOption_UsesNamedFile()
    {
        // Arrange: a settings file outside the root that accepts the misspelled word
        using var temp = new TempDirectory();
        using var other = new TempDirectory();
        var config = other.CreateFile("custom.yaml", "words:\n  - wrold\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: name the settings file explicitly
        var result = Run(temp, "--config", config, "a.md");

        // Assert: the word is accepted and no default-file notice is shown
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.DoesNotContain("No settings file found", result.Output));
    }

    /// <summary>
    ///     Test that a named settings file that does not exist is an error and nothing is checked.
    /// </summary>
    [Fact]
    public void LintRunner_Run_MissingConfigFile_ReportsErrorAndFails()
    {
        // Arrange: a name that points at nothing, and a file that would be reported if it were checked
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        var missing = Path.Combine(temp.FullPath, "missing.yaml");

        // Act: name the missing settings file
        var result = Run(temp, "--config", missing, "a.md");

        // Assert: the error names the file, nothing is checked, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains($"{missing}: error: Cannot read settings file", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that every settings error is reported with its position and nothing is checked.
    /// </summary>
    [Fact]
    public void LintRunner_Run_InvalidSettings_ReportsEachErrorAndChecksNothing()
    {
        // Arrange: a settings file with two unsupported keys, and a file that would be reported
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "bogusOne: 1\nbogusTwo: 2\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: run with the default settings file
        var result = Run(temp, "a.md");

        // Assert: both errors are reported at their lines, nothing is checked, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains(result.Lines, line => line.Contains(".textlint.yaml(1,1): error: ") && line.Contains("'bogusOne'")),
            () => Assert.Contains(result.Lines, line => line.Contains(".textlint.yaml(2,1): error: ") && line.Contains("'bogusTwo'")),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that a settings file that is present but cannot be read is an error and not an absent file.
    /// </summary>
    [Fact]
    public void LintRunner_Run_UnreadableSettingsFile_ReportsErrorNotAbsence()
    {
        // Arrange: the default settings file name is taken by a directory, which exists but cannot be read as a file
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.FullPath, ".textlint.yaml"));
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run with the default settings file
        var result = Run(temp, "a.md");

        // Assert: the problem is reported as an error, and the defaults are not silently used
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains(".textlint.yaml: error: Cannot read settings file", result.Output),
            () => Assert.DoesNotContain("No settings file found", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that only a missing path counts as an absent settings file.
    /// </summary>
    [Fact]
    public void LintRunner_ProbeSettingsFile_FileSystemFailures_AreNotAbsence()
    {
        // Arrange: file system reads that fail in each way a real one can
        Func<string, FileAttributes> missing = path => throw new FileNotFoundException("missing", path);
        Func<string, FileAttributes> missingDirectory = _ => throw new DirectoryNotFoundException("missing directory");
        Func<string, FileAttributes> denied = _ => throw new UnauthorizedAccessException("denied");
        Func<string, FileAttributes> broken = _ => throw new IOException("broken");
        Func<string, FileAttributes> present = _ => FileAttributes.Normal;

        // Act: probe with each read
        var missingResult = LintRunner.ProbeSettingsFile("x", missing, out _);
        var missingDirectoryResult = LintRunner.ProbeSettingsFile("x", missingDirectory, out _);
        var deniedResult = LintRunner.ProbeSettingsFile("x", denied, out var deniedProblem);
        var brokenResult = LintRunner.ProbeSettingsFile("x", broken, out var brokenProblem);
        var presentResult = LintRunner.ProbeSettingsFile("x", present, out _);

        // Assert: only a missing file or directory is absence; refusal and other failures are failures with their reason
        Assert.Multiple(
            () => Assert.Equal(LintRunner.SettingsFileProbe.NotFound, missingResult),
            () => Assert.Equal(LintRunner.SettingsFileProbe.NotFound, missingDirectoryResult),
            () => Assert.Equal(LintRunner.SettingsFileProbe.Failed, deniedResult),
            () => Assert.Equal("denied", deniedProblem),
            () => Assert.Equal(LintRunner.SettingsFileProbe.Failed, brokenResult),
            () => Assert.Equal("broken", brokenProblem),
            () => Assert.Equal(LintRunner.SettingsFileProbe.Found, presentResult));
    }

    /// <summary>
    ///     Test that patterns on the command line replace the files setting.
    /// </summary>
    [Fact]
    public void LintRunner_Run_GlobArguments_ReplaceFilesSetting()
    {
        // Arrange: settings that name b.md, and two misspelled files
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - b.md\n");
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("b.md", "Hello xyzzyq\n");

        // Act: name a.md on the command line
        var result = Run(temp, "a.md");

        // Assert: only a.md is checked
        Assert.Multiple(
            () => Assert.Contains("a.md(1,7)", result.Output),
            () => Assert.DoesNotContain("b.md", result.Output));
    }

    /// <summary>
    ///     Test that the ignore setting still applies to command-line patterns.
    /// </summary>
    [Fact]
    public void LintRunner_Run_GlobArguments_HonorIgnorePaths()
    {
        // Arrange: settings that ignore a directory, and misspelled files in and out of it
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "ignorePaths:\n  - generated/**\n");
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("generated/b.md", "Hello xyzzyq\n");

        // Act: select every markdown file
        var result = Run(temp, "**/*.md");

        // Assert: the ignored directory is not checked
        Assert.Multiple(
            () => Assert.Contains("a.md(1,7)", result.Output),
            () => Assert.DoesNotContain("generated", result.Output));
    }

    /// <summary>
    ///     Test that a file named by an absolute path is reported by that path.
    /// </summary>
    [Fact]
    public void LintRunner_Run_AbsolutePathArgument_ReportsAbsolutePath()
    {
        // Arrange: a misspelled file named by its absolute path
        using var temp = new TempDirectory();
        var file = temp.CreateFile("docs/a.md", "Hello wrold\n");

        // Act: check it
        var result = Run(temp, file);

        // Assert: the issue is reported under the absolute path the user typed
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains($"{file}(1,7): error: Unknown word 'wrold'", result.Lines));
    }

    /// <summary>
    ///     Test that a run with no patterns anywhere is a usage error with guidance.
    /// </summary>
    [Fact]
    public void LintRunner_Run_NoPatterns_ReportsUsageError()
    {
        // Arrange: a directory with no settings and no patterns on the command line
        using var temp = new TempDirectory();

        // Act: run
        var result = Run(temp);

        // Assert: the error says what to add, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("No files to check: give file patterns on the command line", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that a selection that matches no files fails with an explanation.
    /// </summary>
    [Fact]
    public void LintRunner_Run_NoFilesMatched_FailsWithExplanation()
    {
        // Arrange: a pattern that matches nothing
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run
        var result = Run(temp, "*.nomatch");

        // Assert: the run fails and names the way to accept an empty selection
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("No files matched the file selection", result.Output),
            () => Assert.Contains("--allow-empty", result.Output));
    }

    /// <summary>
    ///     Test that an empty selection succeeds when it is allowed.
    /// </summary>
    [Fact]
    public void LintRunner_Run_NoFilesMatchedAllowEmpty_Succeeds()
    {
        // Arrange: a pattern that matches nothing
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run with the option that accepts an empty selection
        var result = Run(temp, "--allow-empty", "*.nomatch");

        // Assert: the run succeeds and the summary shows that nothing was checked
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains("Checked 0 file(s): 0 issue(s) in 0 file(s), 0 skipped, 0 could not be checked.", result.Lines));
    }

    /// <summary>
    ///     Test that a binary file is reported as information and does not fail the run.
    /// </summary>
    [Fact]
    public void LintRunner_Run_BinaryFile_ReportsInfoAndSucceeds()
    {
        // Arrange: a binary file and a clean text file
        using var temp = new TempDirectory();
        temp.CreateBinaryFile("data.bin", [0x00, 0x01]);
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: select everything
        var result = Run(temp, "*");

        // Assert: the binary file is an info line and a skip, and the run succeeds
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains(result.Lines, line => line.StartsWith("data.bin: info: ", StringComparison.Ordinal)),
            () => Assert.Contains("1 skipped", result.Output));
    }

    /// <summary>
    ///     Test that a file that cannot be read is reported as an error, the others are still checked, and the run fails.
    /// </summary>
    [Fact]
    public void LintRunner_Run_LockedFile_ReportsErrorAndFails()
    {
        // Arrange: one file held open exclusively, and one clean file
        using var temp = new TempDirectory();
        var locked = temp.CreateFile("locked.md", "Hello world.\n");
        temp.CreateFile("a.md", "Hello world.\n");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act: select both
        var result = Run(temp, "*.md");

        // Assert: the locked file is an error line, the other file was checked, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains(result.Lines, line => line.StartsWith("locked.md: error: ", StringComparison.Ordinal)),
            () => Assert.Contains("Checked 1 file(s): 0 issue(s) in 0 file(s), 0 skipped, 1 could not be checked.", result.Lines));
    }

    /// <summary>
    ///     Test that the language option overrides the language of the settings.
    /// </summary>
    [Fact]
    public void LintRunner_Run_LanguageOption_OverridesSettings()
    {
        // Arrange: settings for British English and a file with the American and the British spelling
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "language: en-GB\n");
        temp.CreateFile("a.md", "favorite favourite\n");

        // Act: check with the settings' language, then with American English given on the command line
        var british = Run(temp, "a.md");
        var american = Run(temp, "--language", "en-US", "a.md");

        // Assert: the option wins over the settings
        Assert.Multiple(
            () => Assert.Contains("Unknown word 'favorite'", british.Output),
            () => Assert.DoesNotContain("Unknown word 'favourite'", british.Output),
            () => Assert.Contains("Unknown word 'favourite'", american.Output),
            () => Assert.DoesNotContain("Unknown word 'favorite'", american.Output));
    }

    /// <summary>
    ///     Test that an unsupported language is reported as an error.
    /// </summary>
    [Fact]
    public void LintRunner_Run_InvalidLanguage_ReportsErrorAndFails()
    {
        // Arrange: a file and a language that does not exist
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run with the invalid language
        var result = Run(temp, "--language", "xx-YY", "a.md");

        // Assert: the error names the language, nothing is reported as checked, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("xx-YY", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that an unsupported pattern is reported with the pattern as typed.
    /// </summary>
    [Fact]
    public void LintRunner_Run_InvalidGlobPattern_ReportsErrorNamingPattern()
    {
        // Arrange: a pattern that uses a wildcard the library does not support
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run
        var result = Run(temp, "a?.md");

        // Assert: the error names the pattern and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("Invalid glob pattern 'a?.md'", result.Output));
    }

    /// <summary>
    ///     Test that a root directory that does not exist is reported as an error.
    /// </summary>
    [Fact]
    public void LintRunner_Run_MissingRootDirectory_ReportsErrorAndFails()
    {
        // Arrange: a root that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "nowhere");

        // Act: run against it
        var result = ContextRun.Execute(["--root", missing, "*.md"], context => LintRunner.Run(context, TestContext.Current.CancellationToken));

        // Assert: the error names the directory and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains(missing, result.Output));
    }

    /// <summary>
    ///     Test that a root that is not a valid path is reported as an error and not as an unhandled exception.
    /// </summary>
    [Fact]
    public void LintRunner_Run_InvalidRootPath_ReportsErrorAndFails()
    {
        // Arrange: a root that contains a character no path may contain
        var invalid = "bad\0root";

        // Act: run against it
        var result = ContextRun.Execute(["--root", invalid, "*.md"], context => LintRunner.Run(context, TestContext.Current.CancellationToken));

        // Assert: the run fails with a message instead of throwing
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.False(string.IsNullOrWhiteSpace(result.Output)));
    }

    /// <summary>
    ///     Test that a cancelled token ends the run with a message and a failure instead of a result.
    /// </summary>
    [Fact]
    public async Task LintRunner_Run_CancelledToken_ReportsCancellationAndFails()
    {
        // Arrange: a file to check and a token that is already cancelled
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act: run with the cancelled token
        var result = ContextRun.Execute(["--root", temp.FullPath, "a.md"], context => LintRunner.Run(context, source.Token));

        // Assert: the run says it was cancelled, reports no result, and fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("The check was cancelled", result.Output),
            () => Assert.DoesNotContain("Unknown word", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that the parallelism option does not change the result.
    /// </summary>
    [Fact]
    public void LintRunner_Run_MaxParallelism_GivesSameResult()
    {
        // Arrange: several misspelled files
        using var temp = new TempDirectory();
        for (var index = 0; index < 8; index++)
        {
            temp.CreateFile($"f{index}.md", $"Hello wrold {index}\n");
        }

        // Act: run with one worker and with several
        var single = Run(temp, "--max-parallelism", "1", "*.md");
        var many = Run(temp, "--max-parallelism", "4", "*.md");

        // Assert: identical output and exit code
        Assert.Multiple(
            () => Assert.Equal(single.Output, many.Output),
            () => Assert.Equal(single.ExitCode, many.ExitCode));
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public void LintRunner_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a missing context is a programming error
        Assert.Throws<ArgumentNullException>(() => LintRunner.Run(null!, TestContext.Current.CancellationToken));
    }
}
