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

// cspell:ignore wrold xyzzyq

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Linting;
using DemaConsulting.TextLint.Tool.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tool.Tests.Linting;

/// <summary>
///     Unit tests for the <see cref="LintReporter"/> class.
/// </summary>
/// <remarks>
///     A <see cref="SpellCheckResult"/> can only be produced by the library, so each test runs the library's
///     spell checker over a small temporary tree to obtain a real result and then reports it. That is the
///     documented dependency of the reporter. <c>wrold</c> and <c>xyzzyq</c> are misspellings that no
///     dictionary holds.
/// </remarks>
[Collection("Sequential")]
public class LintReporterTests
{
    /// <summary>
    ///     Runs the library's spell checker over every file of a temporary tree.
    /// </summary>
    /// <param name="temp">The tree to check.</param>
    /// <returns>The library's result.</returns>
    private static async Task<SpellCheckResult> CheckAsync(TempDirectory temp) =>
        await SpellChecker.CheckAsync(new TextLintSettings { Files = ["**/*"] }, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>
    ///     Reports a result through a real context and returns what was written.
    /// </summary>
    /// <param name="result">The result to report.</param>
    /// <param name="displayPath">Maps a relative path to the name to show.</param>
    /// <returns>The exit code and everything written.</returns>
    private static ContextRunResult Report(SpellCheckResult result, Func<string, string> displayPath) =>
        ContextRun.Execute([], context => LintReporter.Write(context, result, displayPath));

    /// <summary>
    ///     Test that findings come first, then file diagnostics, then the summary.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_IssuesAndDiagnostics_WritesInOrderThenSummary()
    {
        // Arrange: two misspelled files and a binary file, checked by the library
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold xyzzyq\n");
        temp.CreateFile("b.md", "Hello wrold\n");
        temp.CreateBinaryFile("c.bin", [0x00, 0x01]);
        var result = await CheckAsync(temp);

        // Act: report it with relative names
        var reported = Report(result, path => path);

        // Assert: the findings, then the skipped file as information, then the totals
        Assert.Multiple(
            () => Assert.Equal(5, reported.Lines.Length),
            () => Assert.Equal("a.md(1,7): error: Unknown word 'wrold'", reported.Lines[0]),
            () => Assert.Equal("a.md(1,13): error: Unknown word 'xyzzyq'", reported.Lines[1]),
            () => Assert.Equal("b.md(1,7): error: Unknown word 'wrold'", reported.Lines[2]),
            () => Assert.StartsWith("c.bin: info: The file looks like binary data", reported.Lines[3]),
            () => Assert.Equal("Checked 2 file(s): 3 issue(s) in 2 file(s), 1 skipped, 0 could not be checked.", reported.Lines[4]));
    }

    /// <summary>
    ///     Test that the names shown are the ones the mapping gives.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_DisplayPath_NamesFilesAsMapped()
    {
        // Arrange: a misspelled file and a binary file, checked by the library
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        temp.CreateBinaryFile("c.bin", [0x00]);
        var result = await CheckAsync(temp);

        // Act: report it under names that carry a prefix
        var reported = Report(result, path => "shown/" + path);

        // Assert: findings and diagnostics both use the mapped names
        Assert.Multiple(
            () => Assert.Contains("shown/a.md(1,7): error: Unknown word 'wrold'", reported.Lines),
            () => Assert.Contains(reported.Lines, line => line.StartsWith("shown/c.bin: info: ", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that a file that could not be read is shown as an error and counted.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_UnreadableFile_ShownAsErrorAndCounted()
    {
        // Arrange: one file held open exclusively while the library checks the tree
        using var temp = new TempDirectory();
        var locked = temp.CreateFile("locked.md", "Hello world.\n");
        temp.CreateFile("a.md", "Hello world.\n");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = await CheckAsync(temp);

        // Act: report it
        var reported = Report(result, path => path);

        // Assert: the locked file is an error line, and the summary counts it as not checked
        Assert.Multiple(
            () => Assert.Contains(reported.Lines, line => line.StartsWith("locked.md: error: ", StringComparison.Ordinal)),
            () => Assert.Contains("Checked 1 file(s): 0 issue(s) in 0 file(s), 0 skipped, 1 could not be checked.", reported.Lines));
    }

    /// <summary>
    ///     Test that a clean result produces only the summary.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_CleanResult_WritesOnlySummary()
    {
        // Arrange: a correctly spelled file
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");
        var result = await CheckAsync(temp);

        // Act: report it
        var reported = Report(result, path => path);

        // Assert: exactly one line, the summary
        Assert.Equal(["Checked 1 file(s): 0 issue(s) in 0 file(s), 0 skipped, 0 could not be checked."], reported.Lines);
    }

    /// <summary>
    ///     Test that the summary counts the files that findings are in, not the findings.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_SeveralIssuesInOneFile_CountsFilesOnce()
    {
        // Arrange: one file with two findings
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "wrold xyzzyq\n");
        var result = await CheckAsync(temp);

        // Act: report it
        var reported = Report(result, path => path);

        // Assert: two issues in one file
        Assert.Contains("Checked 1 file(s): 2 issue(s) in 1 file(s), 0 skipped, 0 could not be checked.", reported.Lines);
    }

    /// <summary>
    ///     Test that reporting does not decide the exit code.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_ResultWithIssues_LeavesExitCodeToCaller()
    {
        // Arrange: a result with a finding
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello wrold\n");
        var result = await CheckAsync(temp);

        // Act: report it
        var reported = Report(result, path => path);

        // Assert: the reporter only writes; the caller sets the failure
        Assert.Equal(0, reported.ExitCode);
    }

    /// <summary>
    ///     Test that missing arguments are rejected.
    /// </summary>
    [Fact]
    public async Task LintReporter_Write_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange: a valid result and context to vary one argument at a time
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world.\n");
        var result = await CheckAsync(temp);
        using var context = Context.Create(["--silent"]);

        // Act / Assert: each missing argument is a programming error
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => LintReporter.Write(null!, result, path => path)),
            () => Assert.Throws<ArgumentNullException>(() => LintReporter.Write(context, null!, path => path)),
            () => Assert.Throws<ArgumentNullException>(() => LintReporter.Write(context, result, null!)));
    }
}
