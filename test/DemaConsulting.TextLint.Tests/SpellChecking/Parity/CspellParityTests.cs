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

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;

namespace DemaConsulting.TextLint.Tests.SpellChecking.Parity;

/// <summary>
///     System-level tests that compare the spell checker with the recorded output of the real cspell.
/// </summary>
/// <remarks>
///     The goal of the product is to replace cspell without surprises, so the evidence is a set of
///     corpora (text files built from invented words, so the result does not depend on the dictionary of
///     either tool) that were checked by cspell with the settings file stored beside each corpus. The
///     recorded output was produced by <c>record-cspell.ps1</c> and is checked in. Each test loads the
///     same settings file through the library's own loader, checks the corpus with the public API, and
///     compares the findings with the recorded ones. Every deliberate difference is listed with its reason
///     in <c>known-divergences.txt</c>, and the comparison must match that list exactly: a difference that
///     is not listed, or a listed difference that no longer exists, fails the test. A finding is compared
///     as <c>line:column word</c>, followed by <c>flagged</c> for a flagged word or <c>directive</c> for an
///     unsupported directive. The data files are copied beside the test assembly.
/// </remarks>
public class CspellParityTests
{
    /// <summary>
    ///     The corpus of tokenization rules.
    /// </summary>
    private const string TokenizationCorpus = "tokenization";

    /// <summary>
    ///     The corpus of directives.
    /// </summary>
    private const string DirectivesCorpus = "directives";

    /// <summary>
    ///     The corpus of an unterminated disable directive.
    /// </summary>
    private const string UnterminatedCorpus = "unterminated";

    /// <summary>
    ///     The corpus of word lists.
    /// </summary>
    private const string WordListsCorpus = "word-lists";

    /// <summary>
    ///     The corpus with a raised minimum word length.
    /// </summary>
    private const string MinLengthRaisedCorpus = "min-length-raised";

    /// <summary>
    ///     The corpus with a lowered minimum word length.
    /// </summary>
    private const string MinLengthLoweredCorpus = "min-length-lowered";

    /// <summary>
    ///     The corpus of possessives and contractions.
    /// </summary>
    private const string PossessivesCorpus = "possessives";

    /// <summary>
    ///     The corpus of the deliberate differences in word splitting and dictionaries.
    /// </summary>
    private const string DivergencesCorpus = "divergences";

    /// <summary>
    ///     The corpus of directives that are reported instead of applied.
    /// </summary>
    private const string UnsupportedDirectivesCorpus = "unsupported-directives";

    /// <summary>
    ///     The suffix of the name of the recording that cspell made for the CRLF variant of a corpus.
    /// </summary>
    private const string CrLfRecordingSuffix = ".crlf";

    /// <summary>
    ///     The sign, in the divergences file, of a finding that only TextLint reports.
    /// </summary>
    private const string OnlyTextLint = "+";

    /// <summary>
    ///     The sign, in the divergences file, of a finding that only cspell reports.
    /// </summary>
    private const string OnlyCspell = "-";

    /// <summary>
    ///     The number of fields on a line of the divergences file: corpus, side, finding, and reason.
    /// </summary>
    private const int DivergenceFields = 4;

    /// <summary>
    ///     The suffix of a finding that is a flagged word.
    /// </summary>
    private const string FlaggedSuffix = " flagged";

    /// <summary>
    ///     The suffix of a finding that is an unsupported directive.
    /// </summary>
    private const string DirectiveSuffix = " directive";

    /// <summary>
    ///     Gets the folder that holds the copied corpus, recorded, and divergence files.
    /// </summary>
    private static string ParityFolder => Path.Combine(AppContext.BaseDirectory, "SpellChecking", "Parity");

    /// <summary>
    ///     Checks a corpus with its settings file and formats the findings.
    /// </summary>
    /// <param name="corpus">The name of the corpus.</param>
    /// <param name="useCrLf"><see langword="true"/> to check the corpus with Windows line endings.</param>
    /// <returns>One string per finding, in order.</returns>
    private static async Task<string[]> CheckCorpusAsync(string corpus, bool useCrLf = false)
    {
        // Load the settings the way an application does, and fail loudly if the file is not understood
        var loaded = await SettingsLoader.LoadAsync(
            Path.Combine(ParityFolder, "corpus", corpus + ".settings.yaml"),
            TestContext.Current.CancellationToken);
        Assert.True(loaded.Succeeded, string.Join(Environment.NewLine, loaded.Diagnostics));

        // Check the text of the corpus, optionally with CRLF line endings
        var text = await File.ReadAllTextAsync(Path.Combine(ParityFolder, "corpus", corpus + ".input"), TestContext.Current.CancellationToken);
        if (useCrLf)
        {
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        return [.. new TextChecker(loaded.Settings!).Check(text, corpus + ".input").Select(FormatFinding)];
    }

    /// <summary>
    ///     Formats a finding in the form of the recorded cspell output.
    /// </summary>
    /// <param name="issue">The finding.</param>
    /// <returns>The finding as <c>line:column word</c> with a suffix for its kind.</returns>
    private static string FormatFinding(SpellingIssue issue) =>
        $"{issue.Line}:{issue.Column} {issue.Text}" + issue.Kind switch
        {
            SpellingIssueKind.FlaggedWord => FlaggedSuffix,
            SpellingIssueKind.UnsupportedDirective => DirectiveSuffix,
            _ => string.Empty,
        };

    /// <summary>
    ///     Reads the recorded cspell findings of a corpus.
    /// </summary>
    /// <param name="corpus">The name of the corpus.</param>
    /// <returns>One string per finding; the comment lines of the header are skipped.</returns>
    private static string[] ReadRecorded(string corpus) =>
        [.. File.ReadAllLines(Path.Combine(ParityFolder, "expected", corpus + ".cspell"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))];

    /// <summary>
    ///     Reads the divergences that are listed for a corpus on one side.
    /// </summary>
    /// <param name="corpus">The name of the corpus.</param>
    /// <param name="side">The side sign: <c>+</c> for TextLint only, <c>-</c> for cspell only.</param>
    /// <returns>The listed findings.</returns>
    private static string[] ReadDivergences(string corpus, string side) =>
        [.. ReadDivergenceLines()
            .Where(fields => fields[0] == corpus && fields[1] == side)
            .Select(fields => fields[2])];

    /// <summary>
    ///     Reads every line of the divergences file, split into its fields.
    /// </summary>
    /// <returns>The fields of each line: corpus, side, finding, and reason.</returns>
    private static string[][] ReadDivergenceLines() =>
        [.. File.ReadAllLines(Path.Combine(ParityFolder, "known-divergences.txt"))
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('|', DivergenceFields, StringSplitOptions.TrimEntries))];

    /// <summary>
    ///     Compares TextLint's findings with cspell's recorded findings for a corpus, allowing exactly the listed differences.
    /// </summary>
    /// <param name="corpus">The name of the corpus.</param>
    /// <param name="ours">The findings of TextLint.</param>
    /// <param name="recordedCorpus">The name of the corpus whose recorded output and divergences apply.</param>
    private static void AssertMatchesRecorded(string corpus, string[] ours, string recordedCorpus)
    {
        // Work out what only one of the tools reports
        var recorded = ReadRecorded(recordedCorpus);
        var onlyOurs = ours.Except(recorded).Order(StringComparer.Ordinal).ToArray();
        var onlyCspell = recorded.Except(ours).Order(StringComparer.Ordinal).ToArray();

        // The differences must be exactly the listed ones, and the findings must be unique so that the comparison is exact
        Assert.Multiple(
            () => Assert.Equal(ours.Length, ours.Distinct().Count()),
            () => Assert.Equal(ReadDivergences(corpus, OnlyTextLint).Order(StringComparer.Ordinal), onlyOurs),
            () => Assert.Equal(ReadDivergences(corpus, OnlyCspell).Order(StringComparer.Ordinal), onlyCspell));
    }

    /// <summary>
    ///     Test that the tokenization corpus gives the same findings as the recorded cspell output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_TokenizationCorpus_MatchesRecordedCspellOutput()
    {
        // Arrange: the corpus of word splitting, ignored text, escapes, scripts, and positions
        // Act: check it with the settings stored beside it
        var ours = await CheckCorpusAsync(TokenizationCorpus);

        // Assert: the findings equal cspell's, with no listed differences
        AssertMatchesRecorded(TokenizationCorpus, ours, TokenizationCorpus);
    }

    /// <summary>
    ///     Test that the directive corpora give the same findings as the recorded cspell output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_DirectiveCorpora_MatchRecordedCspellOutput()
    {
        // Arrange: the corpus of every supported directive and the corpus of a disable that is never enabled
        // Act: check both with the settings stored beside them
        var directives = await CheckCorpusAsync(DirectivesCorpus);
        var unterminated = await CheckCorpusAsync(UnterminatedCorpus);

        // Assert: the findings equal cspell's, with no listed differences
        AssertMatchesRecorded(DirectivesCorpus, directives, DirectivesCorpus);
        AssertMatchesRecorded(UnterminatedCorpus, unterminated, UnterminatedCorpus);
    }

    /// <summary>
    ///     Test that the directive corpora with Windows line endings give the same findings as the cspell output recorded for them.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_CrlfDirectiveCorpus_MatchesRecordedCspellOutput()
    {
        // Arrange: the directive corpora with every line ending turned into CRLF; cspell's output for them is recorded
        // separately because the scope of a next-line directive depends on the line endings
        // Act: check them
        var directives = await CheckCorpusAsync(DirectivesCorpus, useCrLf: true);
        var unterminated = await CheckCorpusAsync(UnterminatedCorpus, useCrLf: true);

        // Assert: the findings equal the output recorded for the CRLF variant, with no listed differences
        AssertMatchesRecorded(DirectivesCorpus, directives, DirectivesCorpus + CrLfRecordingSuffix);
        AssertMatchesRecorded(UnterminatedCorpus, unterminated, UnterminatedCorpus + CrLfRecordingSuffix);
    }

    /// <summary>
    ///     Test that the word-list corpus gives the same findings as the recorded cspell output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_WordListCorpus_MatchesRecordedCspellOutput()
    {
        // Arrange: the corpus of accepted, ignored, and flagged words, letter cases, and entries with punctuation
        // Act: check it with the word lists stored beside it
        var ours = await CheckCorpusAsync(WordListsCorpus);

        // Assert: the findings equal cspell's, with no listed differences
        AssertMatchesRecorded(WordListsCorpus, ours, WordListsCorpus);
    }

    /// <summary>
    ///     Test that the minimum-length corpora give the same findings as the recorded cspell output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_MinimumLengthCorpora_MatchRecordedCspellOutput()
    {
        // Arrange: one corpus with a minimum word length of 6 and one with a minimum of 2
        // Act: check both with the settings stored beside them
        var raised = await CheckCorpusAsync(MinLengthRaisedCorpus);
        var lowered = await CheckCorpusAsync(MinLengthLoweredCorpus);

        // Assert: the findings equal cspell's, with no listed differences
        AssertMatchesRecorded(MinLengthRaisedCorpus, raised, MinLengthRaisedCorpus);
        AssertMatchesRecorded(MinLengthLoweredCorpus, lowered, MinLengthLoweredCorpus);
    }

    /// <summary>
    ///     Test that the possessive and contraction corpus gives the same findings as the recorded cspell output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_PossessiveCorpus_MatchesRecordedCspellOutput()
    {
        // Arrange: the corpus of possessives, contractions, and apostrophes at the edges of words
        // Act: check it with the settings stored beside it
        var ours = await CheckCorpusAsync(PossessivesCorpus);

        // Assert: the findings equal cspell's, with no listed differences
        AssertMatchesRecorded(PossessivesCorpus, ours, PossessivesCorpus);
    }

    /// <summary>
    ///     Test that the differences from cspell are exactly the documented ones, and that every corpus has recorded output.
    /// </summary>
    [Fact]
    public async Task TextLint_CspellParity_KnownDivergences_AreExactlyTheDocumentedOnes()
    {
        // Arrange: the corpora that exist only to show the deliberate differences
        // Act: check them
        var divergences = await CheckCorpusAsync(DivergencesCorpus);
        var unsupported = await CheckCorpusAsync(UnsupportedDirectivesCorpus);

        // Assert: the differences are exactly the listed ones, every listed difference belongs to a corpus, and
        // every corpus has a settings file and recorded output
        var corpora = Directory.GetFiles(Path.Combine(ParityFolder, "corpus"), "*.input")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();
        var listedCorpora = ReadDivergenceLines().Select(fields => fields[0]).Distinct().ToArray();
        Assert.Multiple(
            () => AssertMatchesRecorded(DivergencesCorpus, divergences, DivergencesCorpus),
            () => AssertMatchesRecorded(UnsupportedDirectivesCorpus, unsupported, UnsupportedDirectivesCorpus),
            () => Assert.All(listedCorpora, corpus => Assert.Contains(corpus, corpora)),
            () => Assert.All(
                corpora,
                corpus => Assert.True(
                    File.Exists(Path.Combine(ParityFolder, "corpus", corpus + ".settings.yaml")) &&
                    File.Exists(Path.Combine(ParityFolder, "expected", corpus + ".cspell")),
                    $"The corpus '{corpus}' has no settings file or no recorded output.")));
    }
}
