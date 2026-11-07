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

using DemaConsulting.TextLint.Tool.Linting;
using DemaConsulting.TextLint.Tool.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tool.Tests.Linting;

/// <summary>
///     Subsystem tests for the Linting subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="LintRunner"/> and <see cref="LintReporter"/> together, driven through a
///     real command-line context, against the real library and real temporary directories. The documented
///     dependencies are the Cli subsystem's context and the TextLint library. Every test selects its own
///     temporary directory with the root option, so none depends on the current directory. <c>wrold</c> and
///     <c>xyzzyq</c> are misspellings that no dictionary holds.
/// </remarks>
[Collection("Sequential")]
public class LintingSubsystemTests
{
    /// <summary>
    ///     Runs the subsystem as the program would, with the temporary directory as the root.
    /// </summary>
    /// <param name="temp">The temporary directory, used as the root.</param>
    /// <param name="arguments">The command-line arguments after the root option.</param>
    /// <returns>The exit code and everything the run wrote.</returns>
    private static ContextRunResult Run(TempDirectory temp, params string[] arguments) =>
        ContextRun.Execute(["--root", temp.FullPath, .. arguments], context => LintRunner.Run(context, TestContext.Current.CancellationToken));

    /// <summary>
    ///     Test that an explicitly named settings file is used instead of the one in the root directory.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_ExplicitSettings_BeatRootSettings()
    {
        // Arrange: a root settings file that accepts one word, and a named one that accepts the other
        using var temp = new TempDirectory();
        using var other = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "words:\n  - wrold\n");
        var named = other.CreateFile("named.yaml", "words:\n  - xyzzyq\n");
        temp.CreateFile("a.md", "wrold xyzzyq\n");

        // Act: check with the named file
        var result = Run(temp, "--config", named, "a.md");

        // Assert: only the named file's word is accepted
        Assert.Multiple(
            () => Assert.Contains("Unknown word 'wrold'", result.Output),
            () => Assert.DoesNotContain("Unknown word 'xyzzyq'", result.Output));
    }

    /// <summary>
    ///     Test that the settings file in the root directory is used when none is named.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_RootSettingsFile_IsUsedWithoutOption()
    {
        // Arrange: a root settings file that selects the files and accepts a word
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - \"*.md\"\nwords:\n  - wrold\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: run with no options and no patterns
        var result = Run(temp);

        // Assert: the settings selected the file and accepted the word
        Assert.Multiple(
            () => Assert.Equal(0, result.ExitCode),
            () => Assert.Contains("Checked 1 file(s): 0 issue(s)", result.Output));
    }

    /// <summary>
    ///     Test that the default settings apply, with a notice, when there is no settings file.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_NoSettingsFile_UsesDefaults()
    {
        // Arrange: no settings file, and a file whose word only a settings file could have accepted
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: run with a pattern
        var result = Run(temp, "a.md");

        // Assert: the default settings report the word, and the notice says why
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("No settings file found", result.Output),
            () => Assert.Contains("Unknown word 'wrold'", result.Output));
    }

    /// <summary>
    ///     Test that a settings error is reported with its position and nothing is checked.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_InvalidSettings_ReportsErrorsAndChecksNothing()
    {
        // Arrange: a settings file with an unsupported key
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "bogusKey: true\n");
        temp.CreateFile("a.md", "Hello wrold\n");

        // Act: run
        var result = Run(temp, "a.md");

        // Assert: the error is positioned, nothing is checked, and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains(".textlint.yaml(1,1): error: ", result.Output),
            () => Assert.DoesNotContain("Checked", result.Output));
    }

    /// <summary>
    ///     Test that command-line patterns replace the files setting while the ignore setting still applies.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_GlobsAndSettings_GlobsReplaceFiles()
    {
        // Arrange: settings that select one file and ignore a directory, and misspelled files in several places
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "files:\n  - b.md\nignorePaths:\n  - skipped/**\n");
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("b.md", "Hello xyzzyq\n");
        temp.CreateFile("skipped/c.md", "Hello xyzzyq\n");

        // Act: name every markdown file on the command line
        var result = Run(temp, "**/*.md");

        // Assert: the settings' file list is replaced and the ignored directory is still ignored
        Assert.Multiple(
            () => Assert.Contains("a.md(1,7)", result.Output),
            () => Assert.Contains("b.md(1,7)", result.Output),
            () => Assert.DoesNotContain("skipped/", result.Output));
    }

    /// <summary>
    ///     Test that a run with no patterns anywhere is a usage error.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_NoPatterns_ReportsUsageError()
    {
        // Arrange: a settings file that selects no files
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "words:\n  - wrold\n");

        // Act: run with no patterns
        var result = Run(temp);

        // Assert: the error says what to add and the run fails
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("No files to check", result.Output));
    }

    /// <summary>
    ///     Test that an empty selection fails unless it is allowed.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_NoFilesMatched_FailsUnlessAllowed()
    {
        // Arrange: a directory with one file and a pattern that matches none
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");

        // Act: run without and with the option that allows an empty selection
        var failing = Run(temp, "*.nomatch");
        var allowed = Run(temp, "--allow-empty", "*.nomatch");

        // Assert: the first fails with an explanation and the second succeeds
        Assert.Multiple(
            () => Assert.Equal(1, failing.ExitCode),
            () => Assert.Contains("No files matched", failing.Output),
            () => Assert.Equal(0, allowed.ExitCode));
    }

    /// <summary>
    ///     Test that a file named by absolute path is checked and reported under that path.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_AbsolutePath_ReportsAbsolutePath()
    {
        // Arrange: a misspelled file, named by its absolute path
        using var temp = new TempDirectory();
        var file = temp.CreateFile("docs/a.md", "Hello wrold\n");

        // Act: run
        var result = Run(temp, file);

        // Assert: the finding is reported under the absolute path
        Assert.Contains($"{file}(1,7): error: Unknown word 'wrold'", result.Lines);
    }

    /// <summary>
    ///     Test that the language option overrides the settings.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_LanguageOption_OverridesSettings()
    {
        // Arrange: settings for American English and a file with the British spelling
        using var temp = new TempDirectory();
        temp.CreateFile(".textlint.yaml", "language: en-US\n");
        temp.CreateFile("a.md", "favourite\n");

        // Act: check with the settings' language, then with British English given on the command line
        var american = Run(temp, "a.md");
        var british = Run(temp, "--language", "en-GB", "a.md");

        // Assert: the option wins over the settings
        Assert.Multiple(
            () => Assert.Contains("Unknown word 'favourite'", american.Output),
            () => Assert.Equal(0, british.ExitCode));
    }

    /// <summary>
    ///     Test that findings, skipped files, and the summary are all reported.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_MixedOutcomes_ReportsAllAndSummary()
    {
        // Arrange: a misspelled file, a clean file, and a binary file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateFile("b.md", "Hello world.\n");
        temp.CreateBinaryFile("c.bin", [0x00]);

        // Act: select everything
        var result = Run(temp, "*");

        // Assert: the finding, the skip, and the totals are all present, in that order
        var lines = result.Lines.Where(line => !line.StartsWith("info: No settings", StringComparison.Ordinal)).ToArray();
        Assert.Multiple(
            () => Assert.Equal("a.md(1,7): error: Unknown word 'wrold'", lines[0]),
            () => Assert.StartsWith("c.bin: info: ", lines[1]),
            () => Assert.Equal("Checked 2 file(s): 1 issue(s) in 1 file(s), 1 skipped, 0 could not be checked.", lines[2]));
    }

    /// <summary>
    ///     Test that the parallelism limit does not change the result.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_MaxParallelism_GivesSameResult()
    {
        // Arrange: several misspelled files
        using var temp = new TempDirectory();
        for (var index = 0; index < 8; index++)
        {
            temp.CreateFile($"f{index}.md", $"Hello wrold {index}\n");
        }

        // Act: check with one worker and with several
        var single = Run(temp, "--max-parallelism", "1", "*.md");
        var many = Run(temp, "--max-parallelism", "4", "*.md");

        // Assert: identical output and exit code
        Assert.Multiple(
            () => Assert.Equal(single.Output, many.Output),
            () => Assert.Equal(single.ExitCode, many.ExitCode));
    }

    /// <summary>
    ///     Test that an invalid root is reported first and cannot be bypassed by an absolute path.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_InvalidRoot_IsReportedBeforeAnythingElse()
    {
        // Arrange: a misspelled file that exists, a root that does not, and a root that is a file
        using var temp = new TempDirectory();
        using var other = new TempDirectory();
        var file = other.CreateFile("a.md", "Hello wrold\n");
        var missing = Path.Combine(temp.FullPath, "nowhere");
        var fileRoot = temp.CreateFile("root.txt", "x");

        // Act: run with each invalid root, naming the file by its absolute path
        var missingResult = ContextRun.Execute(["--root", missing, file], context => LintRunner.Run(context, TestContext.Current.CancellationToken));
        var fileResult = ContextRun.Execute(["--root", fileRoot, file], context => LintRunner.Run(context, TestContext.Current.CancellationToken));

        // Assert: each run fails naming the root, with no settings notice and nothing checked
        Assert.Multiple(
            () => Assert.Equal(1, missingResult.ExitCode),
            () => Assert.Contains($"The root directory '{missing}' does not exist.", missingResult.Output),
            () => Assert.DoesNotContain("No settings file found", missingResult.Output),
            () => Assert.DoesNotContain("Unknown word", missingResult.Output),
            () => Assert.Equal(1, fileResult.ExitCode),
            () => Assert.Contains($"The root directory '{fileRoot}' is not a directory.", fileResult.Output),
            () => Assert.DoesNotContain("Unknown word", fileResult.Output));
    }

    /// <summary>
    ///     Test that an absolute directory argument checks everything below it, and a missing path matches nothing.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_AbsoluteDirectory_ChecksEveryFileBelow()
    {
        // Arrange: a directory with a nested misspelled file, and the path of a directory that does not exist
        using var temp = new TempDirectory();
        var file = temp.CreateFile("docs/deep/a.md", "Hello wrold\n");
        var missing = Path.Combine(temp.FullPath, "nowhere");

        // Act: name the existing directory, then the missing one
        var existing = Run(temp, Path.Combine(temp.FullPath, "docs"));
        var absent = Run(temp, missing);

        // Assert: the existing directory is searched; the missing path is a file name that matches nothing
        Assert.Multiple(
            () => Assert.Contains($"{file}(1,7): error: Unknown word 'wrold'", existing.Lines),
            () => Assert.Equal(1, absent.ExitCode),
            () => Assert.Contains("No files matched", absent.Output));
    }

    /// <summary>
    ///     Test that only a clean run exits with success.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_CleanAndDirty_SetExitCode()
    {
        // Arrange: one clean file and one misspelled file
        using var temp = new TempDirectory();
        temp.CreateFile("clean.md", "Hello world.\n");
        temp.CreateFile("dirty.md", "Hello wrold\n");

        // Act: check each on its own
        var clean = Run(temp, "clean.md");
        var dirty = Run(temp, "dirty.md");

        // Assert: success only for the clean file
        Assert.Multiple(
            () => Assert.Equal(0, clean.ExitCode),
            () => Assert.Equal(1, dirty.ExitCode));
    }

    /// <summary>
    ///     Test that failures a user can cause are reported and do not escape as exceptions.
    /// </summary>
    [Fact]
    public void LintingSubsystem_Run_ExpectedFailures_AreReportedNotThrown()
    {
        // Arrange: a directory with a file, and three kinds of bad input
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");
        var missingRoot = Path.Combine(temp.FullPath, "nowhere");

        // Act: a missing root directory, an unsupported pattern, and an unsupported language
        var missing = ContextRun.Execute(["--root", missingRoot, "*.md"], context => LintRunner.Run(context, TestContext.Current.CancellationToken));
        var pattern = Run(temp, "a?.md");
        var language = Run(temp, "--language", "xx-YY", "a.md");

        // Assert: each run fails with a message instead of throwing
        Assert.Multiple(
            () => Assert.Equal(1, missing.ExitCode),
            () => Assert.Contains(missingRoot, missing.Output),
            () => Assert.Equal(1, pattern.ExitCode),
            () => Assert.Contains("Invalid glob pattern 'a?.md'", pattern.Output),
            () => Assert.Equal(1, language.ExitCode),
            () => Assert.Contains("xx-YY", language.Output));
    }

    /// <summary>
    ///     Test that canceling stops the run with a message and no result.
    /// </summary>
    [Fact]
    public async Task LintingSubsystem_Run_CanceledToken_ReportsCancellation()
    {
        // Arrange: a misspelled file and a token that is already canceled
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act: run with the canceled token
        var result = ContextRun.Execute(["--root", temp.FullPath, "a.md"], context => LintRunner.Run(context, source.Token));

        // Assert: the run fails with the cancellation message and reports no findings
        Assert.Multiple(
            () => Assert.Equal(1, result.ExitCode),
            () => Assert.Contains("canceled", result.Output),
            () => Assert.DoesNotContain("Unknown word", result.Output));
    }
}
