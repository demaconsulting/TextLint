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

// cspell:ignore wrold speling xqzv klingon

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.SpellChecking;

/// <summary>
///     Unit tests for the <see cref="SpellChecker"/> class and the options, result, and diagnostic types it uses.
/// </summary>
/// <remarks>
///     The checker is tested with real temporary directories and its documented dependencies real: the
///     settings value, the file selector, the file reader, and the text checker. The internal
///     <c>CheckFilesAsync</c> method is a test seam that takes the function that checks a text; the tests use
///     it to prove how a time-out, a defect, a cancellation, and the degree of parallelism are handled without
///     needing hostile input or timing. Words are chosen to be unambiguous: <c>wrold</c> and <c>speling</c> are
///     misspellings that no dictionary holds.
/// </remarks>
public class SpellCheckerTests
{
    /// <summary>
    ///     A misspelled word that is unknown to every dictionary.
    /// </summary>
    private const string Unknown = "wrold";

    /// <summary>
    ///     A second misspelled word that is unknown to every dictionary.
    /// </summary>
    private const string OtherUnknown = "speling";

    /// <summary>
    ///     The number of files in the trees that compare results across degrees of parallelism.
    /// </summary>
    private const int FileCount = 30;

    /// <summary>
    ///     The degree of parallelism that the comparison tests use besides one.
    /// </summary>
    private const int HighParallelism = 8;

    /// <summary>
    ///     The number of milliseconds that a deliberately slow check waits.
    /// </summary>
    private const int SlowCheckMilliseconds = 100;

    /// <summary>
    ///     The number of milliseconds that a check waits so that concurrent checks overlap.
    /// </summary>
    private const int OverlapMilliseconds = 20;

    /// <summary>
    ///     A size limit in bytes that a two-word file exceeds.
    /// </summary>
    private const long SmallSizeLimit = 10;

    /// <summary>
    ///     A size limit in bytes that small files stay under and a hundred-byte file exceeds.
    /// </summary>
    private const long MediumSizeLimit = 50;

    /// <summary>
    ///     The length of the file that exceeds <see cref="MediumSizeLimit"/>.
    /// </summary>
    private const int OversizeFileLength = 100;

    /// <summary>
    ///     A small degree of parallelism that is neither one nor high.
    /// </summary>
    private const int TwoWorkers = 2;

    /// <summary>
    ///     Creates settings that select the given file patterns.
    /// </summary>
    /// <param name="files">The include patterns.</param>
    /// <returns>The settings.</returns>
    private static TextLintSettings Select(params string[] files) => new() { Files = files };

    /// <summary>
    ///     Formats a result as one string so that two results can be compared exactly.
    /// </summary>
    /// <param name="result">The result to format.</param>
    /// <returns>The findings, diagnostics, and counts, one per line.</returns>
    private static string Format(SpellCheckResult result) =>
        string.Join(
            '\n',
            result.Issues.Select(issue => issue.ToString())
                .Concat(result.FileDiagnostics.Select(diagnostic => diagnostic.ToString()))
                .Append($"{result.FilesChecked}/{result.FilesSkipped}/{result.HasProblems}"));

    /// <summary>
    ///     Selects every file of a temporary tree, in the order the selector returns them.
    /// </summary>
    /// <param name="temp">The tree.</param>
    /// <returns>The selected files.</returns>
    private static IReadOnlyList<SelectedFile> SelectAll(TempDirectory temp) =>
        FileSelector.Select(temp.FullPath, ["**/*"], [], true, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Builds a function that checks text with a default checker, for use with the seam.
    /// </summary>
    /// <returns>The function.</returns>
    private static Func<string, string?, IReadOnlyList<SpellingIssue>> DefaultCheck()
    {
        var checker = new TextChecker(new TextLintSettings());
        return checker.Check;
    }

    /// <summary>
    ///     Creates the files of a tree with a misspelling in each, spread over directories.
    /// </summary>
    /// <param name="temp">The directory to create the tree in.</param>
    private static void CreateManyFiles(TempDirectory temp)
    {
        string[] directories = ["a", "b/c", "."];
        for (var index = 0; index < FileCount; index++)
        {
            var directory = directories[index % directories.Length];
            temp.CreateFile($"{directory}/file{index:D2}.md", $"Hello {Unknown}\nline {index}\n{OtherUnknown} {Unknown}\n");
        }
    }

    /// <summary>
    ///     Writes bytes to a file in a temporary directory.
    /// </summary>
    /// <param name="temp">The directory to write in.</param>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The content.</param>
    private static void WriteBytes(TempDirectory temp, string name, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(temp.FullPath, name), bytes);

    /// <summary>
    ///     Blocks the calling thread for a number of milliseconds so that checks on other threads can overlap it.
    /// </summary>
    /// <param name="milliseconds">The time to wait.</param>
    private static void Pause(int milliseconds) => SpinWait.SpinUntil(static () => false, milliseconds);

    /// <summary>
    ///     Test that only the files that match the file patterns are checked.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_FilePatterns_ChecksOnlyMatchingFiles()
    {
        // Arrange: a markdown and a text file that both hold a misspelling
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("b.txt", Unknown);

        // Act: check with a pattern that selects markdown only
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: only the markdown file is checked and reported
        Assert.Multiple(
            () => Assert.Equal(["a.md"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.Equal(1, result.FilesChecked));
    }

    /// <summary>
    ///     Test that files that match an ignore pattern are not checked.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_IgnorePaths_SkipsIgnoredFiles()
    {
        // Arrange: two files that hold a misspelling, one under a directory that the settings ignore
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("generated/b.md", Unknown);
        var settings = new TextLintSettings { Files = ["**/*.md"], IgnorePaths = ["generated"] };

        // Act: check
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: only the file that is not ignored is checked
        Assert.Multiple(
            () => Assert.Equal(["a.md"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.Equal(1, result.FilesChecked));
    }

    /// <summary>
    ///     Test that a hidden file is not checked unless hidden matching is enabled.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_HiddenFile_IsNotCheckedByDefault()
    {
        // Arrange: a hidden file that holds a misspelling and a pattern with a wildcard
        using var temp = new TempDirectory();
        temp.CreateFile(".hidden.md", Unknown);

        // Act: check with the defaults
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the hidden file is not checked
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.Equal(0, result.FilesChecked));
    }

    /// <summary>
    ///     Test that a hidden file is checked when hidden matching is enabled.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_HiddenFileWithGlobDot_IsChecked()
    {
        // Arrange: the same hidden file and settings that enable hidden matching
        using var temp = new TempDirectory();
        temp.CreateFile(".hidden.md", Unknown);
        var settings = new TextLintSettings { Files = ["**/*.md"], EnableGlobDot = true };

        // Act: check
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the hidden file is checked and reported
        Assert.Equal([".hidden.md"], result.Issues.Select(issue => issue.FilePath));
    }

    /// <summary>
    ///     Test that findings carry the path relative to the root with forward slashes.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NestedFile_ReportsRelativeForwardSlashPath()
    {
        // Arrange: a file two directories deep
        using var temp = new TempDirectory();
        temp.CreateFile("docs/guide/a.md", Unknown);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the finding names the relative path with forward slashes
        Assert.Equal("docs/guide/a.md", Assert.Single(result.Issues).FilePath);
    }

    /// <summary>
    ///     Test that no file patterns select nothing and give an empty result.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NoFilePatterns_ReturnsEmptyResult()
    {
        // Arrange: a directory with a file and settings with no file patterns
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);

        // Act: check
        var result = await SpellChecker.CheckAsync(new TextLintSettings(), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing is checked, found, or skipped
        AssertEmpty(result);
    }

    /// <summary>
    ///     Test that patterns that match no file give an empty result.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NoMatchingFiles_ReturnsEmptyResult()
    {
        // Arrange: a directory whose only file the pattern does not match
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", Unknown);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing is checked, found, or skipped
        AssertEmpty(result);
    }

    /// <summary>
    ///     Test that clean files give no findings and are counted as checked.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_CleanFiles_ReportsNothingAndCountsFiles()
    {
        // Arrange: three files made of known words, one of them empty
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world");
        temp.CreateFile("b.md", "This is a spelling test.");
        temp.CreateFile("empty.md", string.Empty);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: no findings, three files checked, none skipped, no problem
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.Equal(3, result.FilesChecked),
            () => Assert.Equal(0, result.FilesSkipped),
            () => Assert.False(result.HasProblems));
    }

    /// <summary>
    ///     Test that the spelling settings are applied to the text of the files.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_SpellingSettings_AreApplied()
    {
        // Arrange: a file with a word that the settings flag, one they accept, and one they do not know
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", $"obsolete {Unknown} {OtherUnknown}");
        var settings = new TextLintSettings { Files = ["**/*.md"], FlagWords = ["obsolete"], Words = [Unknown] };

        // Act: check
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the flagged and the unknown word are reported and the accepted word is not
        Assert.Equal(
            [("obsolete", SpellingIssueKind.FlaggedWord), (OtherUnknown, SpellingIssueKind.UnknownWord)],
            result.Issues.Select(issue => (issue.Text, issue.Kind)));
    }

    /// <summary>
    ///     Test that omitting the options uses the defaults.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NullOptions_UsesDefaults()
    {
        // Arrange: a file with a misspelling
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);

        // Act: check with null options
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, null, TestContext.Current.CancellationToken);

        // Assert: the file is checked
        Assert.Single(result.Issues);
    }

    /// <summary>
    ///     Test that the default degree of parallelism is the number of processors.
    /// </summary>
    [Fact]
    public void SpellChecker_Options_Default_UsesProcessorCount()
    {
        // Arrange: options with no value assigned
        var options = new SpellCheckOptions();

        // Act / Assert: the default is the processor count
        Assert.Equal(Environment.ProcessorCount, options.MaxDegreeOfParallelism);
    }

    /// <summary>
    ///     Test that a degree of parallelism of zero is rejected when the options are created.
    /// </summary>
    [Fact]
    public void SpellChecker_Options_ZeroParallelism_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: creating options with zero throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpellCheckOptions { MaxDegreeOfParallelism = 0 });
    }

    /// <summary>
    ///     Test that a negative degree of parallelism is rejected when the options are created.
    /// </summary>
    [Fact]
    public void SpellChecker_Options_NegativeParallelism_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: creating options with a negative value throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpellCheckOptions { MaxDegreeOfParallelism = -1 });
    }

    /// <summary>
    ///     Test that a with-expression derives a changed copy and leaves the original unchanged.
    /// </summary>
    [Fact]
    public void SpellChecker_Options_WithExpression_CopiesAndKeepsOriginal()
    {
        // Arrange: options with a degree of parallelism of two
        var original = new SpellCheckOptions { MaxDegreeOfParallelism = TwoWorkers };

        // Act: derive a copy with another value
        var copy = original with { MaxDegreeOfParallelism = HighParallelism };

        // Assert: the copy has the new value and the original keeps its own
        Assert.Multiple(
            () => Assert.Equal(HighParallelism, copy.MaxDegreeOfParallelism),
            () => Assert.Equal(TwoWorkers, original.MaxDegreeOfParallelism));
    }

    /// <summary>
    ///     Test that a with-expression is validated like the initializer.
    /// </summary>
    [Fact]
    public void SpellChecker_Options_WithExpressionInvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: valid options
        var original = new SpellCheckOptions();

        // Act / Assert: deriving a copy with zero throws
        Assert.Throws<ArgumentOutOfRangeException>(() => original with { MaxDegreeOfParallelism = 0 });
    }

    /// <summary>
    ///     Test that the results of one degree of parallelism and of many are identical and in the same order.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults()
    {
        // Arrange: many files spread over directories
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: check with one worker and with many
        var sequential = await SpellChecker.CheckAsync(
            settings,
            temp.FullPath,
            new SpellCheckOptions { MaxDegreeOfParallelism = 1 },
            TestContext.Current.CancellationToken);
        var parallel = await SpellChecker.CheckAsync(
            settings,
            temp.FullPath,
            new SpellCheckOptions { MaxDegreeOfParallelism = HighParallelism },
            TestContext.Current.CancellationToken);

        // Assert: the formatted results are equal, and there is something to compare
        Assert.Multiple(
            () => Assert.NotEmpty(sequential.Issues),
            () => Assert.Equal(Format(sequential), Format(parallel)));
    }

    /// <summary>
    ///     Test that the number of files checked at the same time never exceeds the option.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_Parallelism_NeverExceedsOption()
    {
        // Arrange: many files and a check that records how many checks overlap
        const int limit = TwoWorkers;
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var gate = new object();
        var running = 0;
        var peak = 0;
        var inner = DefaultCheck();
        IReadOnlyList<SpellingIssue> Check(string text, string? name)
        {
            lock (gate)
            {
                running++;
                peak = Math.Max(peak, running);
            }

            Pause(OverlapMilliseconds);
            lock (gate)
            {
                running--;
            }

            return inner(text, name);
        }

        // Act: check with a limit of two
        await SpellChecker.CheckFilesAsync(
            SelectAll(temp),
            0,
            Check,
            new SpellCheckOptions { MaxDegreeOfParallelism = limit },
            TestContext.Current.CancellationToken);

        // Assert: never more than two checks ran at once
        Assert.InRange(peak, 1, limit);
    }

    /// <summary>
    ///     Test that a degree of parallelism of one checks the files one at a time.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_ParallelismOne_ChecksOneFileAtATime()
    {
        // Arrange: many files and a check that records how many checks overlap
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var gate = new object();
        var running = 0;
        var peak = 0;
        var inner = DefaultCheck();
        IReadOnlyList<SpellingIssue> Check(string text, string? name)
        {
            lock (gate)
            {
                running++;
                peak = Math.Max(peak, running);
            }

            lock (gate)
            {
                running--;
            }

            return inner(text, name);
        }

        // Act: check with a limit of one
        await SpellChecker.CheckFilesAsync(
            SelectAll(temp),
            0,
            Check,
            new SpellCheckOptions { MaxDegreeOfParallelism = 1 },
            TestContext.Current.CancellationToken);

        // Assert: the checks never overlapped
        Assert.Equal(1, peak);
    }

    /// <summary>
    ///     Test that the result follows the order of the files even when an early file finishes last.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_SlowFirstFile_KeepsFileOrder()
    {
        // Arrange: three files whose first is checked slowly
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("b.md", Unknown);
        temp.CreateFile("c.md", Unknown);
        var inner = DefaultCheck();
        IReadOnlyList<SpellingIssue> Check(string text, string? name)
        {
            if (name == "a.md")
            {
                Pause(SlowCheckMilliseconds);
            }

            return inner(text, name);
        }

        // Act: check with several workers
        var result = await SpellChecker.CheckFilesAsync(
            SelectAll(temp),
            0,
            Check,
            new SpellCheckOptions { MaxDegreeOfParallelism = HighParallelism },
            TestContext.Current.CancellationToken);

        // Assert: the findings are in file order, not completion order
        Assert.Equal(["a.md", "b.md", "c.md"], result.Issues.Select(issue => issue.FilePath));
    }

    /// <summary>
    ///     Test that findings are ordered by file path, then line, then column.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_Findings_AreOrderedByPathLineAndColumn()
    {
        // Arrange: files created out of order, with several findings each; an upper-case name sorts before lower case
        using var temp = new TempDirectory();
        temp.CreateFile("b.md", $"{Unknown}\n{OtherUnknown} {Unknown}");
        temp.CreateFile("Z.md", Unknown);
        temp.CreateFile("a/c.md", Unknown);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: ordinal path order, then line, then column
        Assert.Equal(
            ["Z.md:1:1", "a/c.md:1:1", "b.md:1:1", "b.md:2:1", "b.md:2:9"],
            result.Issues.Select(issue => $"{issue.FilePath}:{issue.Line}:{issue.Column}"));
    }

    /// <summary>
    ///     Test that file diagnostics are ordered by file path.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_Diagnostics_AreOrderedByPath()
    {
        // Arrange: binary files created out of order
        using var temp = new TempDirectory();
        WriteBytes(temp, "c.bin", [0x00]);
        WriteBytes(temp, "a.bin", [0x00]);
        WriteBytes(temp, "b.bin", [0x00]);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.bin"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the diagnostics are in path order
        Assert.Equal(["a.bin", "b.bin", "c.bin"], result.FileDiagnostics.Select(diagnostic => diagnostic.FilePath));
    }

    /// <summary>
    ///     Test that a locked file is reported as unreadable and the other files are still checked.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_LockedFile_IsReportedUnreadableAndRunContinues()
    {
        // Arrange: a file that this test holds open exclusively, and a readable file with a misspelling
        using var temp = new TempDirectory();
        var lockedPath = temp.CreateFile("a-locked.md", "hello");
        temp.CreateFile("b.md", Unknown);
        using var holder = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the locked file is an unreadable error, the other file is checked, and the counts say so
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("a-locked.md", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.Unreadable, diagnostic.Kind),
            () => Assert.True(diagnostic.IsError),
            () => Assert.Equal(["b.md"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.Equal(1, result.FilesChecked),
            () => Assert.Equal(0, result.FilesSkipped),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that a binary file is skipped and reported.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_BinaryFile_IsSkippedAndReported()
    {
        // Arrange: a file that starts with a NUL byte
        using var temp = new TempDirectory();
        WriteBytes(temp, "data.bin", [0x00, 0x01]);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.bin"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file is reported as binary, skipped, and not an error
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("data.bin", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.Binary, diagnostic.Kind),
            () => Assert.False(diagnostic.IsError),
            () => Assert.Equal(1, result.FilesSkipped),
            () => Assert.Equal(0, result.FilesChecked));
    }

    /// <summary>
    ///     Test that a file larger than the maximum file size is skipped and reported.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported()
    {
        // Arrange: a file of eleven bytes and a limit of ten
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown + " " + Unknown);
        var settings = new TextLintSettings { Files = ["**/*.md"], MaxFileSize = SmallSizeLimit };

        // Act: check
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file is skipped as too large, not checked, and has no findings
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.TooLarge, diagnostic.Kind),
            () => Assert.Equal(1, result.FilesSkipped),
            () => Assert.Empty(result.Issues));
    }

    /// <summary>
    ///     Test that files skipped and files checked are counted separately.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_MixedFiles_CountsCheckedAndSkipped()
    {
        // Arrange: two small text files, one binary file, and one file over the size limit
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", Unknown);
        temp.CreateFile("b.txt", "hello");
        WriteBytes(temp, "c.txt", [0x00]);
        temp.CreateFile("d.txt", new string('a', OversizeFileLength));
        var settings = new TextLintSettings { Files = ["**/*.txt"], MaxFileSize = MediumSizeLimit };

        // Act: check
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: two files checked, two skipped
        Assert.Multiple(
            () => Assert.Equal(2, result.FilesChecked),
            () => Assert.Equal(2, result.FilesSkipped),
            () => Assert.Equal(2, result.FileDiagnostics.Count));
    }

    /// <summary>
    ///     Test that a file that is selected and then removed is reported as unreadable.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_FileRemovedAfterSelection_IsUnreadable()
    {
        // Arrange: a selected file that no longer exists
        using var temp = new TempDirectory();
        var path = temp.CreateFile("a.md", Unknown);
        var files = SelectAll(temp);
        File.Delete(path);

        // Act: check the stale selection
        var result = await SpellChecker.CheckFilesAsync(
            files,
            0,
            DefaultCheck(),
            new SpellCheckOptions(),
            TestContext.Current.CancellationToken);

        // Assert: the file is an unreadable error and nothing else happened
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.Unreadable, diagnostic.Kind),
            () => Assert.Empty(result.Issues),
            () => Assert.Equal(0, result.FilesChecked));
    }

    /// <summary>
    ///     Test that a time-out while checking a file is reported as data for that file.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_TimeoutWhileChecking_IsReportedTimedOut()
    {
        // Arrange: files of which the check of one throws the exception that a pattern time-out raises
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("b.md", "hostile");
        var inner = DefaultCheck();
        IReadOnlyList<SpellingIssue> Check(string text, string? name) =>
            name == "b.md" ? throw new RegexMatchTimeoutException() : inner(text, name);

        // Act: check
        var result = await SpellChecker.CheckFilesAsync(
            SelectAll(temp),
            0,
            Check,
            new SpellCheckOptions(),
            TestContext.Current.CancellationToken);

        // Assert: the slow file is a timed-out error, is not counted as checked, and the other file is checked
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("b.md", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.TimedOut, diagnostic.Kind),
            () => Assert.True(diagnostic.IsError),
            () => Assert.Equal(["a.md"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.Equal(1, result.FilesChecked),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that any other exception from checking a file is a defect that propagates.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_DefectWhileChecking_Propagates()
    {
        // Arrange: a check that fails with an exception that is not a time-out
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        static IReadOnlyList<SpellingIssue> Check(string text, string? name) => throw new InvalidOperationException("defect");

        // Act / Assert: the exception reaches the caller and is not swallowed as a diagnostic
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SpellChecker.CheckFilesAsync(
            SelectAll(temp),
            0,
            Check,
            new SpellCheckOptions(),
            TestContext.Current.CancellationToken));
        Assert.Equal("defect", exception.Message);
    }

    /// <summary>
    ///     Test that a run with a finding has problems.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_FindingPresent_HasProblems()
    {
        // Arrange: a file with a misspelling
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: there is a problem
        Assert.True(result.HasProblems);
    }

    /// <summary>
    ///     Test that skipped files alone are not a problem.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_OnlySkippedFiles_HasNoProblems()
    {
        // Arrange: a binary file and nothing else
        using var temp = new TempDirectory();
        WriteBytes(temp, "data.bin", [0x00]);

        // Act: check
        var result = await SpellChecker.CheckAsync(Select("**/*.bin"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file is reported but there is no problem
        Assert.Multiple(
            () => Assert.Single(result.FileDiagnostics),
            () => Assert.False(result.HasProblems));
    }

    /// <summary>
    ///     Test that an unreadable file is a problem even when nothing was found.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_ErrorDiagnosticOnly_HasProblems()
    {
        // Arrange: a selected file that was removed, so that the only outcome is an error diagnostic
        using var temp = new TempDirectory();
        var path = temp.CreateFile("a.md", "hello");
        var files = SelectAll(temp);
        File.Delete(path);

        // Act: check the stale selection
        var result = await SpellChecker.CheckFilesAsync(files, 0, DefaultCheck(), new SpellCheckOptions(), TestContext.Current.CancellationToken);

        // Assert: there is a problem although there is no finding
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that a time-out and an unreadable file are errors.
    /// </summary>
    [Fact]
    public void SpellChecker_FileDiagnostic_UnreadableAndTimedOut_AreErrors()
    {
        // Arrange: diagnostics of the two kinds that mean a file should have been checked and was not
        var unreadable = new FileDiagnostic("a.md", FileDiagnosticKind.Unreadable, "locked");
        var timedOut = new FileDiagnostic("b.md", FileDiagnosticKind.TimedOut, "slow");

        // Act / Assert: both are errors
        Assert.Multiple(
            () => Assert.True(unreadable.IsError),
            () => Assert.True(timedOut.IsError));
    }

    /// <summary>
    ///     Test that a binary file and a file that is too large are not errors.
    /// </summary>
    [Fact]
    public void SpellChecker_FileDiagnostic_BinaryAndTooLarge_AreNotErrors()
    {
        // Arrange: diagnostics of the two kinds that mean a file was skipped on purpose
        var binary = new FileDiagnostic("a.bin", FileDiagnosticKind.Binary, "binary");
        var tooLarge = new FileDiagnostic("b.md", FileDiagnosticKind.TooLarge, "large");

        // Act / Assert: neither is an error
        Assert.Multiple(
            () => Assert.False(binary.IsError),
            () => Assert.False(tooLarge.IsError));
    }

    /// <summary>
    ///     Test that an error diagnostic is formatted with the word error.
    /// </summary>
    [Fact]
    public void SpellChecker_FileDiagnostic_ErrorToString_UsesErrorSeverity()
    {
        // Arrange: an unreadable diagnostic
        var diagnostic = new FileDiagnostic("docs/a.md", FileDiagnosticKind.Unreadable, "The file cannot be read.");

        // Act: format it
        var formatted = diagnostic.ToString();

        // Assert: the file, the severity, and the message are in the compiler style
        Assert.Equal("docs/a.md: error: The file cannot be read.", formatted);
    }

    /// <summary>
    ///     Test that a skip diagnostic is formatted with the word info.
    /// </summary>
    [Fact]
    public void SpellChecker_FileDiagnostic_SkipToString_UsesInfoSeverity()
    {
        // Arrange: a binary diagnostic
        var diagnostic = new FileDiagnostic("a.bin", FileDiagnosticKind.Binary, "Looks binary.");

        // Act: format it
        var formatted = diagnostic.ToString();

        // Assert: the severity is info
        Assert.Equal("a.bin: info: Looks binary.", formatted);
    }

    /// <summary>
    ///     Test that a token that is already cancelled stops the run.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange: a directory with a file and a token that is already cancelled
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the run is cancelled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: source.Token));
    }

    /// <summary>
    ///     Test that a token that is already cancelled stops the file checking before any file is checked.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_CancelledToken_ChecksNoFile()
    {
        // Arrange: files, a check that counts its calls, and a token that is already cancelled
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var calls = 0;
        IReadOnlyList<SpellingIssue> Check(string text, string? name)
        {
            Interlocked.Increment(ref calls);
            return [];
        }

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the run is cancelled and no file was checked
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SpellChecker.CheckFilesAsync(SelectAll(temp), 0, Check, new SpellCheckOptions(), source.Token));
        Assert.Equal(0, calls);
    }

    /// <summary>
    ///     Test that cancelling during the run stops it with a cancellation and no partial result.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_CancelledDuringRun_ThrowsOperationCanceledException()
    {
        // Arrange: many files and a check that cancels the run when it first runs
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var files = SelectAll(temp);
        using var source = new CancellationTokenSource();
        var calls = 0;
        IReadOnlyList<SpellingIssue> Check(string text, string? name)
        {
            Interlocked.Increment(ref calls);
            source.Cancel();
            return [];
        }

        // Act / Assert: the run ends with a cancellation instead of a result, before every file was checked
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SpellChecker.CheckFilesAsync(files, 0, Check, new SpellCheckOptions { MaxDegreeOfParallelism = 1 }, source.Token));
        Assert.True(calls < files.Count);
    }

    /// <summary>
    ///     Test that the caller's token is handed to the file selection, so that cancelling stops a directory walk.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckWithSelectionAsync_Selection_ReceivesCallersToken()
    {
        // Arrange: a selection that records the token it is given and selects nothing
        using var source = new CancellationTokenSource();
        var received = CancellationToken.None;
        IReadOnlyList<SelectedFile> SelectNothing(CancellationToken token)
        {
            received = token;
            return [];
        }

        // Act: run with the caller's token
        var result = await SpellChecker.CheckWithSelectionAsync(Select("**/*.md"), SelectNothing, null, source.Token);

        // Assert: the selection got that very token, and the run completed with no files
        Assert.Equal(source.Token, received);
        Assert.Equal(0, result.FilesChecked);
    }

    /// <summary>
    ///     Test that a selection that is cancelled while it runs stops the call with a cancellation and no result.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckWithSelectionAsync_SelectionCancelled_ThrowsOperationCanceledException()
    {
        // Arrange: a selection that cancels the token while it runs, as a cancelled directory walk does
        using var source = new CancellationTokenSource();
        var checkedAfterSelection = false;
        IReadOnlyList<SelectedFile> SelectAndCancel(CancellationToken token)
        {
            source.Cancel();
            token.ThrowIfCancellationRequested();
            checkedAfterSelection = true;
            return [];
        }

        // Act / Assert: the call ends with a cancellation and does not go on to check anything
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SpellChecker.CheckWithSelectionAsync(Select("**/*.md"), SelectAndCancel, null, source.Token));
        Assert.False(checkedAfterSelection);
    }

    /// <summary>
    ///     Test that a null selection function is rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckWithSelectionAsync_NullSelection_ThrowsArgumentNullException()
    {
        // Arrange: no selection function

        // Act / Assert: the call throws and names the parameter
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckWithSelectionAsync(Select("**/*.md"), null!, null, TestContext.Current.CancellationToken));
        Assert.Equal("selectFiles", exception.ParamName);
    }

    /// <summary>
    ///     Test that null settings are rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange: no settings

        // Act / Assert: the call throws and names the parameter
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckAsync(null!, Path.GetTempPath(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("settings", exception.ParamName);
    }

    /// <summary>
    ///     Test that a null root directory is rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NullRoot_ThrowsArgumentNullException()
    {
        // Arrange: no root directory

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckAsync(new TextLintSettings(), null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an empty root directory is rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_EmptyRoot_ThrowsArgumentException()
    {
        // Arrange: an empty root directory

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentException>(
            () => SpellChecker.CheckAsync(new TextLintSettings(), string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a white-space root directory is rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_WhiteSpaceRoot_ThrowsArgumentException()
    {
        // Arrange: a root directory of spaces

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentException>(
            () => SpellChecker.CheckAsync(new TextLintSettings(), "   ", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a root directory that does not exist is rejected.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException()
    {
        // Arrange: a directory path that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => SpellChecker.CheckAsync(Select("**/*.md"), missing, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an invalid file pattern is rejected with an error that names it.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_InvalidPattern_ThrowsInvalidGlobPatternException()
    {
        // Arrange: settings with a negated pattern, which is not supported
        using var temp = new TempDirectory();
        var settings = new TextLintSettings { Files = ["!**/*.md"] };

        // Act / Assert: the call throws and the message names the pattern
        var exception = await Assert.ThrowsAsync<InvalidGlobPatternException>(
            () => SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("!**/*.md", exception.Message);
    }

    /// <summary>
    ///     Test that an unusable language is rejected before any file or directory is touched.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_UnusableLanguage_ThrowsBeforeTouchingFiles()
    {
        // Arrange: a bad language and a root directory that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");
        var settings = new TextLintSettings { Files = ["**/*.md"], Language = "klingon" };

        // Act / Assert: the settings problem is reported, not the missing directory
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpellChecker.CheckAsync(settings, missing, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("klingon", exception.Message);
    }

    /// <summary>
    ///     Test that an unusable word-list entry is rejected before any file or directory is touched.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_UnusableWordEntry_ThrowsBeforeTouchingFiles()
    {
        // Arrange: a blank word-list entry and a root directory that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");
        var settings = new TextLintSettings { Files = ["**/*.md"], IgnoreWords = [" "] };

        // Act / Assert: the settings problem is reported, not the missing directory
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpellChecker.CheckAsync(settings, missing, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("'ignoreWords'", exception.Message);
    }

    /// <summary>
    ///     Test that a null file list is rejected by the file-checking seam.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_NullFiles_ThrowsArgumentNullException()
    {
        // Arrange: no file list

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckFilesAsync(null!, 0, DefaultCheck(), new SpellCheckOptions(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a null check function is rejected by the file-checking seam.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_NullCheckFunction_ThrowsArgumentNullException()
    {
        // Arrange: no check function

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckFilesAsync([], 0, null!, new SpellCheckOptions(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that null options are rejected by the file-checking seam.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckFilesAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange: no options

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpellChecker.CheckFilesAsync([], 0, DefaultCheck(), null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that the findings of a result cannot be changed by casting them to a mutable interface.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_Issues_AreImmutable()
    {
        // Arrange: a result with a finding
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        var result = await SpellChecker.CheckAsync(Select("**/*.md"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Act / Assert: the list is not a mutable collection
        var list = Assert.IsAssignableFrom<IList<SpellingIssue>>(result.Issues);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    /// <summary>
    ///     Test that the file diagnostics of a result cannot be changed by casting them to a mutable interface.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_FileDiagnostics_AreImmutable()
    {
        // Arrange: a result with a diagnostic
        using var temp = new TempDirectory();
        WriteBytes(temp, "data.bin", [0x00]);
        var result = await SpellChecker.CheckAsync(Select("**/*.bin"), temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Act / Assert: the list is not a mutable collection
        var list = Assert.IsAssignableFrom<IList<FileDiagnostic>>(result.FileDiagnostics);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    /// <summary>
    ///     Test that a result copies the collections it is created from.
    /// </summary>
    [Fact]
    public void SpellChecker_Result_CallerCollections_AreCopied()
    {
        // Arrange: lists of one finding and one diagnostic, and a result created from them
        var issues = new List<SpellingIssue> { new(null, 1, 1, Unknown, SpellingIssueKind.UnknownWord, ImmutableArray<string>.Empty) };
        var diagnostics = new List<FileDiagnostic> { new("a.bin", FileDiagnosticKind.Binary, "binary") };
        var result = new SpellCheckResult(issues, diagnostics, 1, 1);

        // Act: change the caller's lists
        issues.Clear();
        diagnostics.Clear();

        // Assert: the result is unchanged
        Assert.Multiple(
            () => Assert.Single(result.Issues),
            () => Assert.Single(result.FileDiagnostics));
    }

    /// <summary>
    ///     Asserts that a result has no findings, no diagnostics, zero counts, and no problem.
    /// </summary>
    /// <param name="result">The result to examine.</param>
    private static void AssertEmpty(SpellCheckResult result) =>
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.Empty(result.FileDiagnostics),
            () => Assert.Equal(0, result.FilesChecked),
            () => Assert.Equal(0, result.FilesSkipped),
            () => Assert.False(result.HasProblems));
}
