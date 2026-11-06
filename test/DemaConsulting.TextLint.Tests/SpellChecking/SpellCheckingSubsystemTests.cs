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

// cspell:ignore wrold speling xqzv klingon favourite

using System.Text;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.SpellChecking;

/// <summary>
///     Subsystem tests for the SpellChecking subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="TextChecker"/>, the file reader behind it, and
///     <see cref="SpellChecker"/> together through the public boundary of the subsystem. The documented
///     dependencies are real: the settings value of the Configuration subsystem, the file selector of the
///     FileSelection subsystem, the spelling dictionary of the Dictionaries subsystem, and the text scanning
///     units. Files are created in a per-test temporary directory. Words are chosen to be unambiguous:
///     <c>wrold</c> and <c>speling</c> are misspellings that no dictionary holds.
/// </remarks>
public class SpellCheckingSubsystemTests
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
    ///     The number of files in the tree used to compare degrees of parallelism.
    /// </summary>
    private const int FileCount = 40;

    /// <summary>
    ///     The high degree of parallelism that is compared with a single worker.
    /// </summary>
    private const int HighParallelism = 8;

    /// <summary>
    ///     The number of runs that are started at the same time in the concurrent-runs test.
    /// </summary>
    private const int ConcurrentRuns = 6;

    /// <summary>
    ///     A size limit in bytes that a file of three misspellings exceeds.
    /// </summary>
    private const long SmallSizeLimit = 10;

    /// <summary>
    ///     Checks a text with the default settings or the given ones.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <param name="settings">The settings, or null for the defaults.</param>
    /// <returns>The findings in order.</returns>
    private static IReadOnlyList<SpellingIssue> Check(string text, TextLintSettings? settings = null) =>
        new TextChecker(settings ?? new TextLintSettings()).Check(text);

    /// <summary>
    ///     Formats each finding as <c>line:column text</c>.
    /// </summary>
    /// <param name="issues">The findings.</param>
    /// <returns>One string per finding in order.</returns>
    private static string[] Positions(IReadOnlyList<SpellingIssue> issues) =>
        [.. issues.Select(issue => $"{issue.Line}:{issue.Column} {issue.Text}")];

    /// <summary>
    ///     Writes bytes to a file in a temporary directory.
    /// </summary>
    /// <param name="temp">The directory to write in.</param>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The content.</param>
    private static void WriteBytes(TempDirectory temp, string name, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(temp.FullPath, name), bytes);

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
    ///     Creates a tree of many files, each with misspellings, spread over directories.
    /// </summary>
    /// <param name="temp">The directory to create the tree in.</param>
    private static void CreateManyFiles(TempDirectory temp)
    {
        string[] directories = ["docs", "docs/api", "src", "."];
        for (var index = 0; index < FileCount; index++)
        {
            var directory = directories[index % directories.Length];
            temp.CreateFile($"{directory}/file{index:D2}.md", $"Hello {Unknown}\nline {index}\n{OtherUnknown} {Unknown}\n");
        }
    }

    /// <summary>
    ///     Test that an unknown word in a text is reported with its line, column, and word.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_UnknownWord_ReportsLineColumnAndWord()
    {
        // Arrange: a two-line text whose second line holds a misspelling
        const string text = "Hello world\nsay wrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the finding names the word and its position
        Assert.Equal(["2:5 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a text of known words is reported clean.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_KnownWords_ReportsNothing()
    {
        // Arrange: ordinary words, with punctuation and a contraction
        const string text = "Hello world, this isn't a spelling test.";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that identifiers are split into words and only the unknown words are reported.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_CamelAndSnakeCase_ReportsEachUnknownWord()
    {
        // Arrange: a camel-case and a snake-case identifier, each with one misspelling
        const string text = "helloWroldWorld hello_speling_world";

        // Act: check the text
        var issues = Check(text);

        // Assert: each misspelled piece is reported at its own position
        Assert.Equal(["1:6 Wrold", "1:23 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a short word is not checked.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_ShortWord_IsNotChecked()
    {
        // Arrange: a non-word shorter than the default minimum length
        const string text = "xqz";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a possessive of a known word and a known word after a backslash escape are accepted.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_PossessiveAndBackslashEscape_AreAccepted()
    {
        // Arrange: a possessive and a string-literal escape that precedes a known word
        const string text = "the world's end\\nworld";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the word lists apply in the order ignored, flagged, accepted, dictionary.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_WordLists_ApplyPrecedence()
    {
        // Arrange: a word in all three lists, one accepted, one flagged known word, and one unknown word
        var settings = new TextLintSettings
        {
            IgnoreWords = ["alpha"],
            FlagWords = ["alpha", "hello"],
            Words = ["alpha", Unknown],
        };

        // Act: check a text that uses them
        var issues = Check($"alpha {Unknown} hello {OtherUnknown}", settings);

        // Assert: the ignored and accepted words are not reported, the flagged known word and the unknown word are
        Assert.Equal(
            [("hello", SpellingIssueKind.FlaggedWord), (OtherUnknown, SpellingIssueKind.UnknownWord)],
            issues.Select(issue => (issue.Text, issue.Kind)));
    }

    /// <summary>
    ///     Test that directives in comments switch checking off and add words.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_Directives_ControlChecking()
    {
        // Arrange: a disabled region, a words directive, and a line after them with a misspelling
        const string text =
            "// textlint:disable\nwrold\n// textlint:enable\n// textlint:words speling\nspeling wrold xqzv";

        // Act: check the text
        var issues = Check(text);

        // Assert: the disabled word and the listed word are not reported, the others are
        Assert.Equal(["5:9 wrold", "5:15 xqzv"], Positions(issues));
    }

    /// <summary>
    ///     Test that a directive that is not supported is reported at its position.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_UnsupportedDirective_IsReportedAtItsPosition()
    {
        // Arrange: a directive that the checker does not support, after a misspelling
        const string text = "wrold\ntextlint:locale en-GB";

        // Act: check the text
        var issues = Check(text);

        // Assert: both findings are reported in text order
        Assert.Equal(
            [("wrold", SpellingIssueKind.UnknownWord), ("locale", SpellingIssueKind.UnsupportedDirective)],
            issues.Select(issue => (issue.Text, issue.Kind)));
    }

    /// <summary>
    ///     Test that URLs and email addresses are not checked.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_UrlsAndEmails_AreNotChecked()
    {
        // Arrange: a URL and an email address that contain misspellings, and a misspelling outside them
        const string text = "https://example.com/wrold user@example.com speling";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the word outside the excluded text is reported
        Assert.Equal(["1:44 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that the language setting changes the result.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_BritishLanguage_ChangesResult()
    {
        // Arrange: a British spelling, checked with the default and with the British language
        var british = new TextLintSettings { Language = "en-GB" };

        // Act: check the same text both ways
        var american = Check("favourite");
        var accepted = Check("favourite", british);

        // Assert: the default reports it and the British language accepts it
        Assert.Multiple(
            () => Assert.Single(american),
            () => Assert.Empty(accepted));
    }

    /// <summary>
    ///     Test that a line break is counted by the line feed and a carriage return alone is not a break.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_CrLfAndLoneCr_CountLinesByLineFeed()
    {
        // Arrange: a CRLF line break followed by a carriage return alone
        const string text = "hello\r\nwrold\rspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: the carriage return does not start a line
        Assert.Equal(["2:1 wrold", "2:7 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that unusable settings are rejected when the checker is created.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Check_UnusableSettings_Throw()
    {
        // Arrange: settings with an unsupported language, and settings with a word entry that can never match
        var badLanguage = new TextLintSettings { Language = "klingon" };
        var badEntry = new TextLintSettings { Words = ["two words"] };

        // Act / Assert: creating a checker throws for each, naming the problem
        Assert.Multiple(
            () => Assert.Contains("klingon", Assert.Throws<ArgumentException>(() => new TextChecker(badLanguage)).Message),
            () => Assert.Contains("'two words'", Assert.Throws<ArgumentException>(() => new TextChecker(badEntry)).Message));
    }

    /// <summary>
    ///     Test that the files the settings select are checked and findings name file, line, column, and word.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_FilesWithUnknownWords_ReportsFileLineColumnAndWord()
    {
        // Arrange: two markdown files with misspellings in different places
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", $"Hello world\nsay {Unknown}");
        temp.CreateFile("docs/b.md", $"{OtherUnknown} here");
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: each finding names its file, line, column, and word
        Assert.Equal(
            ["a.md(2,5): error: Unknown word 'wrold'", "docs/b.md(1,1): error: Unknown word 'speling'"],
            result.Issues.Select(issue => issue.ToString()));
    }

    /// <summary>
    ///     Test that clean files give no findings and no problem.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_CleanFiles_ReportsNothing()
    {
        // Arrange: files made of known words
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello world");
        temp.CreateFile("b.md", "This is a spelling test.");
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing is reported and both files count as checked
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.Equal(2, result.FilesChecked),
            () => Assert.False(result.HasProblems));
    }

    /// <summary>
    ///     Test that files that are ignored or not selected are not checked.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_IgnoredAndUnselectedFiles_AreNotChecked()
    {
        // Arrange: a selected file, an ignored file, and an unselected file, all with a misspelling
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("generated/b.md", Unknown);
        temp.CreateFile("c.txt", Unknown);
        var settings = new TextLintSettings { Files = ["**/*.md"], IgnorePaths = ["generated"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: only the selected file is checked
        Assert.Equal(["a.md"], result.Issues.Select(issue => issue.FilePath));
    }

    /// <summary>
    ///     Test that UTF-8 and UTF-16 files, with and without a byte-order mark, report the same positions.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_Utf8Utf16AndBom_ReportSamePositions()
    {
        // Arrange: the same text, with a character outside the Basic Multilingual Plane, in four encodings
        const string text = "\U0001F600 Hello\nwrold";
        using var temp = new TempDirectory();
        WriteBytes(temp, "utf8.txt", Encoding.UTF8.GetBytes(text));
        WriteBytes(temp, "utf8bom.txt", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);
        WriteBytes(temp, "utf16le.txt", [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)]);
        WriteBytes(temp, "utf16be.txt", [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)]);
        var settings = new TextLintSettings { Files = ["**/*.txt"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: every file reports the same word at the same position
        Assert.Equal(
            ["utf16be.txt:2:1", "utf16le.txt:2:1", "utf8.txt:2:1", "utf8bom.txt:2:1"],
            result.Issues.Select(issue => $"{issue.FilePath}:{issue.Line}:{issue.Column}"));
    }

    /// <summary>
    ///     Test that a binary file is skipped and reported.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_BinaryFile_IsSkippedAndReported()
    {
        // Arrange: a binary file and a text file with a misspelling
        using var temp = new TempDirectory();
        WriteBytes(temp, "image.dat", [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01]);
        temp.CreateFile("a.dat", Unknown);
        var settings = new TextLintSettings { Files = ["**/*.dat"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the binary file is reported and skipped, and the text file is checked
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("image.dat", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.Binary, diagnostic.Kind),
            () => Assert.Equal(1, result.FilesSkipped),
            () => Assert.Equal(["a.dat"], result.Issues.Select(issue => issue.FilePath)));
    }

    /// <summary>
    ///     Test that a file larger than the maximum file size is skipped and reported.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported()
    {
        // Arrange: a file of more than ten bytes and a limit of ten
        using var temp = new TempDirectory();
        temp.CreateFile("big.md", $"{Unknown} {Unknown} {Unknown}");
        var settings = new TextLintSettings { Files = ["**/*.md"], MaxFileSize = SmallSizeLimit };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the file is reported as too large and has no findings
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.TooLarge, diagnostic.Kind),
            () => Assert.Empty(result.Issues),
            () => Assert.Equal(1, result.FilesSkipped));
    }

    /// <summary>
    ///     Test that a file that cannot be read is reported and the run continues with the other files.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_UnreadableFile_IsReportedAndRunContinues()
    {
        // Arrange: a file that this test holds open exclusively, and a readable file with a misspelling
        using var temp = new TempDirectory();
        var lockedPath = temp.CreateFile("a-locked.md", "hello");
        temp.CreateFile("b.md", Unknown);
        using var holder = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: check the directory
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the locked file is an error, the readable file was checked, and the run has problems
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.Unreadable, diagnostic.Kind),
            () => Assert.True(diagnostic.IsError),
            () => Assert.Equal(["b.md"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that a missing root directory is an error rather than an empty result.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException()
    {
        // Arrange: a root directory that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act / Assert: the call throws
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => SpellChecker.CheckAsync(settings, missing, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that one worker and many workers give identical results in identical order.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults()
    {
        // Arrange: many files in several directories
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

        // Assert: the results are equal, ordered by path, and there is something to compare
        var paths = sequential.Issues.Select(issue => issue.FilePath!).ToList();
        Assert.Multiple(
            () => Assert.NotEmpty(paths),
            () => Assert.Equal(paths.Order(StringComparer.Ordinal), paths),
            () => Assert.Equal(Format(sequential), Format(parallel)));
    }

    /// <summary>
    ///     Test that several runs at the same time over the same tree give identical results.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_ConcurrentRuns_ReturnIdenticalResults()
    {
        // Arrange: many files, and settings shared by every run
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: start several runs together
        var runs = Enumerable.Range(0, ConcurrentRuns)
            .Select(_ => SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken));
        var results = await Task.WhenAll(runs);

        // Assert: every run returned the same result
        Assert.All(results, result => Assert.Equal(Format(results[0]), Format(result)));
    }

    /// <summary>
    ///     Test that an invalid degree of parallelism is rejected.
    /// </summary>
    [Fact]
    public void SpellCheckingSubsystem_Options_InvalidParallelism_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: zero and negative values are rejected when the options are created
        Assert.Multiple(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new SpellCheckOptions { MaxDegreeOfParallelism = 0 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new SpellCheckOptions { MaxDegreeOfParallelism = -1 }));
    }

    /// <summary>
    ///     Test that cancelling stops the run with a cancellation and no result.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange: a directory with files and a token that is already cancelled
        using var temp = new TempDirectory();
        CreateManyFiles(temp);
        var settings = new TextLintSettings { Files = ["**/*.md"] };
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the run is cancelled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: source.Token));
    }

    /// <summary>
    ///     Test that findings and file diagnostics of a run can be formatted under names chosen by the caller.
    /// </summary>
    [Fact]
    public async Task SpellCheckingSubsystem_CheckAsync_CallerFileNames_NameIssueAndDiagnostic()
    {
        // Arrange: a text file with a misspelling and a binary file, checked in one run
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", $"Hello {Unknown}\n");
        WriteBytes(temp, "b.bin", [0, 1, 2]);
        var settings = new TextLintSettings { Files = ["*.md", "*.bin"] };

        // Act: run the check, then format each entry under its absolute path
        var result = await SpellChecker.CheckAsync(settings, temp.FullPath, cancellationToken: TestContext.Current.CancellationToken);
        var issue = Assert.Single(result.Issues);
        var diagnostic = Assert.Single(result.FileDiagnostics);
        var issueName = Path.Combine(temp.FullPath, issue.FilePath!);
        var diagnosticName = Path.Combine(temp.FullPath, diagnostic.FilePath);

        // Assert: the caller's names replace the relative names and nothing else changes
        Assert.Multiple(
            () => Assert.Equal($"{issueName}(1,7): error: Unknown word '{Unknown}'", issue.Format(issueName)),
            () => Assert.StartsWith($"{diagnosticName}: info: ", diagnostic.Format(diagnosticName)),
            () => Assert.Equal(issue.ToString(), issue.Format(issue.FilePath)),
            () => Assert.Equal(diagnostic.ToString(), diagnostic.Format(diagnostic.FilePath)));
    }
}
