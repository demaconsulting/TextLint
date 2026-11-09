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

// cspell:ignore wroldprint prnt wrold speling qwzx favourite cspe wrld llchecker xqzv Urlz SPELINGS qwzxabc xqzvw zorbbegin zorbend nxqzvw nzorbend

using System.Text;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tests.TestSupport;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests;

/// <summary>
///     System-level tests for the spell-checking capabilities of the TextLint library.
/// </summary>
/// <remarks>
///     These tests drive the public API end to end, exactly as a consuming application would: settings
///     from a settings file or built in code, text checked through <see cref="TextChecker"/>, and the files
///     of a temporary directory checked through <see cref="SpellChecker"/>. Words are chosen to be
///     unambiguous: <c>wrold</c>, <c>speling</c>, and <c>qwzx</c> are not in any dictionary. Directive
///     prefixes are assembled from pieces so that the spell checker that checks this repository does not
///     read them as directives of this file. The comparison with cspell is in <c>CspellParityTests</c>.
/// </remarks>
public class TextLintSpellCheckTests
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
    ///     A third misspelled word that is unknown to every dictionary.
    /// </summary>
    private const string ThirdUnknown = "qwzx";

    /// <summary>
    ///     The directive prefix of cspell, assembled so that it is not read as a directive in this file.
    /// </summary>
    private const string CspellPrefix = "cspe" + "ll:";

    /// <summary>
    ///     The directive prefix of TextLint.
    /// </summary>
    private const string TextLintPrefix = "textlint:";

    /// <summary>
    ///     The number of files in the tree that is checked at different degrees of parallelism.
    /// </summary>
    private const int FileCount = 40;

    /// <summary>
    ///     The high degree of parallelism that is compared with a single worker.
    /// </summary>
    private const int HighParallelism = 8;

    /// <summary>
    ///     The size limit in bytes used to show that a larger file is skipped.
    /// </summary>
    private const long SmallSizeLimit = 20;

    /// <summary>
    ///     The raised minimum word length: a word of five letters is shorter than this.
    /// </summary>
    private const int RaisedMinimum = 6;

    /// <summary>
    ///     The lowered minimum word length.
    /// </summary>
    private const int LoweredMinimum = 2;

    /// <summary>
    ///     The line on which the settings tests expect an error.
    /// </summary>
    private const int SecondLine = 2;

    /// <summary>
    ///     The number of files in the small trees that the file tests create and expect to be checked.
    /// </summary>
    private const int TwoFiles = 2;

    /// <summary>
    ///     The number of encodings of the same text that the encoding test writes.
    /// </summary>
    private const int EncodingCount = 4;

    /// <summary>
    ///     The minimum word length that the settings file test configures.
    /// </summary>
    private const int ConfiguredMinimum = 5;

    /// <summary>
    ///     The number of bytes in two kilobytes, which the settings file test configures as the size limit.
    /// </summary>
    private const long ConfiguredSizeBytes = 2048;

    /// <summary>
    ///     The column of the value in the settings line <c>language: fr</c>.
    /// </summary>
    private const int ValueColumnAfterLanguageKey = 11;

    /// <summary>
    ///     The column of a value after the key <c>maxFileSize</c>, a colon, and a space.
    /// </summary>
    private const int ValueColumnAfterMaxFileSizeKey = 14;

    /// <summary>
    ///     The column of a list item that is indented by two spaces and a dash and a space.
    /// </summary>
    private const int ListItemColumn = 5;

    /// <summary>
    ///     The column of the word <c>wrold</c> in <c>the wrold is big</c>, which is also the column of a list item
    ///     in the settings file tests.
    /// </summary>
    private const int FifthColumn = 5;

    /// <summary>
    ///     The minimum random length that the settings file test configures.
    /// </summary>
    private const int RandomLengthConfigured = 25;

    /// <summary>
    ///     The number of letters in the text that a catastrophic pattern backtracks over.
    /// </summary>
    private const int CatastrophicLength = 60;

    /// <summary>
    ///     Checks a text with the given settings and returns the findings.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <param name="settings">The settings, or null for the defaults.</param>
    /// <returns>The findings in text order.</returns>
    private static IReadOnlyList<SpellingIssue> Check(string text, TextLintSettings? settings = null) =>
        new TextChecker(settings ?? new TextLintSettings()).Check(text);

    /// <summary>
    ///     Formats each finding as <c>line:column text</c>.
    /// </summary>
    /// <param name="issues">The findings.</param>
    /// <returns>One string per finding.</returns>
    private static string[] Positions(IEnumerable<SpellingIssue> issues) =>
        [.. issues.Select(issue => $"{issue.Line}:{issue.Column} {issue.Text}")];

    /// <summary>
    ///     Checks the text files of a temporary directory.
    /// </summary>
    /// <param name="temp">The directory.</param>
    /// <param name="settings">The settings, or null to check every text file with the defaults.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The result.</returns>
    private static Task<SpellCheckResult> CheckFilesAsync(
        TempDirectory temp,
        TextLintSettings? settings = null,
        SpellCheckOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SpellChecker.CheckAsync(settings ?? new TextLintSettings { Files = ["**/*.txt"] }, temp.FullPath, options, cancellationToken);

    /// <summary>
    ///     Loads settings from text written to a settings file.
    /// </summary>
    /// <param name="temp">The directory to write in.</param>
    /// <param name="yaml">The text of the settings file.</param>
    /// <returns>The load result.</returns>
    private static Task<SettingsLoadResult> LoadAsync(TempDirectory temp, string yaml) =>
        SettingsLoader.LoadAsync(temp.CreateFile(".textlint.yaml", yaml), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Test that a settings file with the spelling keys loads their values.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_SpellingKeysConfigFile_LoadsConfiguredValues()
    {
        // Arrange: a settings file that uses every spelling key
        using var temp = new TempDirectory();
        const string yaml = "language: en-GB\nwords: [alpha, beta]\nignoreWords: [gamma]\nflagWords: ['delta->epsilon']\n" +
                            "minWordLength: 5\nmaxFileSize: 2KB\n";

        // Act: load it
        var result = await LoadAsync(temp, yaml);

        // Assert: every key is mapped
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal("en-GB", settings.Language),
            () => Assert.Equal(["alpha", "beta"], settings.Words),
            () => Assert.Equal(["gamma"], settings.IgnoreWords),
            () => Assert.Equal(["delta->epsilon"], settings.FlagWords),
            () => Assert.Equal(ConfiguredMinimum, settings.MinWordLength),
            () => Assert.Equal(ConfiguredSizeBytes, settings.MaxFileSize));
    }

    /// <summary>
    ///     Test that an unsupported language is reported with the file, line, and column and names the language.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_UnsupportedLanguage_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file with a language that has no dictionary
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "language: fr\n");

        // Assert: loading fails with an error at the value that names the language
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("'fr'", diagnostic.Message),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(ValueColumnAfterLanguageKey, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a word-list entry that could never match is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_UnusableWordEntry_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file whose word list has an entry with white space
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "words:\n  - \"two words\"\n");

        // Assert: loading fails with an error at the entry that names the problem
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("white space", diagnostic.Message),
            () => Assert.Equal(SecondLine, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an invalid maximum file size is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_InvalidMaxFileSize_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file whose size is not a size
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "maxFileSize: lots\n");

        // Assert: loading fails with an error at the value
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("maxFileSize", diagnostic.Message),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(ValueColumnAfterMaxFileSizeKey, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an unknown word in a text is reported with its line, column, and the word.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_UnknownWord_ReportsLineColumnAndWord()
    {
        // Arrange: a text with one misspelling on its second line
        var text = $"good morning\nthe {Unknown} is big";

        // Act: check it
        var issues = Check(text);

        // Assert: one finding that names the word and its position
        var issue = Assert.Single(issues);
        Assert.Multiple(
            () => Assert.Equal(SecondLine, issue.Line),
            () => Assert.Equal(FifthColumn, issue.Column),
            () => Assert.Equal(Unknown, issue.Text),
            () => Assert.Equal(SpellingIssueKind.UnknownWord, issue.Kind));
    }

    /// <summary>
    ///     Test that a text of known words has no findings.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_KnownWords_ReportsNothing()
    {
        // Arrange: a sentence of ordinary English words
        const string text = "The quick brown fox jumps over the lazy dog.";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that identifiers are split at case changes and underscores and each word is checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_CamelAndSnakeCase_ReportsEachWordSeparately()
    {
        // Arrange: an identifier made of a short word, an unknown word, and a second unknown word
        var text = $"my{char.ToUpperInvariant(Unknown[0])}{Unknown[1..]}_{OtherUnknown}";

        // Act: check it
        var issues = Check(text);

        // Assert: each unknown word is reported separately at its own position
        Assert.Equal(["1:3 Wrold", "1:9 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that an identifier whose case-separated parts form a listed word is accepted.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_CaseSeparatedListedWord_IsAccepted()
    {
        // Arrange: a listed word whose first part is unknown alone, and identifiers that contain it
        var settings = new TextLintSettings { Words = ["WroldPrint"] };
        const string text = "WroldPrintTool HelloWroldPrintTool WroldPrntTool";

        // Act: check it
        var issues = Check(text, settings);

        // Assert: the identifiers with the listed word pass, and the one that misspells it is reported
        Assert.Equal(["1:36 Wrold", "1:41 Prnt"], Positions(issues));
    }

    /// <summary>
    ///     Test that words shorter than the minimum length are not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_ShortWords_AreNotChecked()
    {
        // Arrange: a short unknown word, and an unknown word of exactly the minimum length
        const string text = "xq qwz wrld";

        // Act: check it
        var issues = Check(text);

        // Assert: only the word of four letters is reported
        Assert.Equal(["1:8 wrld"], Positions(issues));
    }

    /// <summary>
    ///     Test that an apostrophe inside a word does not split it.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_ApostropheWords_StayWhole()
    {
        // Arrange: a contraction of known words, and a possessive of an unknown word
        var text = $"don't {Unknown}'s";

        // Act: check it
        var issues = Check(text);

        // Assert: the contraction passes and the possessive is reported whole
        Assert.Equal([$"1:7 {Unknown}'s"], Positions(issues));
    }

    /// <summary>
    ///     Test that the possessive of a known word is accepted.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_PossessiveOfKnownWord_IsAccepted()
    {
        // Arrange: possessives of known words, with a straight and a typographic apostrophe
        const string text = "the parser's output and the dog\u2019s bone";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a known word after a backslash escape is accepted.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_WordAfterBackslashEscape_IsAccepted()
    {
        // Arrange: a string literal with a line-feed escape in front of a known word
        const string text = "print(\"hello\\nworld\")";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported, because world is known without the n
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that URLs and email addresses are not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_UrlsAndEmails_AreNotChecked()
    {
        // Arrange: a URL and an email address made of unknown words
        var text = $"see https://{Unknown}.example.com/{OtherUnknown}?x=1 or write to {Unknown}@{OtherUnknown}.org";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that hexadecimal values, UUIDs, and hashes are not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_HexUuidAndHashValues_AreNotChecked()
    {
        // Arrange: a C literal, a CSS color, a UUID, a commit hash, and a digest
        const string text = "0xDEADBEEF #ff00ff 550e8400-e29b-41d4-a716-446655440000 3fa4c5d9e8 sha256-AbCdEfGhIjKlMnOpQrStUvWxYz012345678901234567";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that encoded data is not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_EncodedData_IsNotChecked()
    {
        // Arrange: a line of Base64 data
        const string text = "data: QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVowMTIzNDU2Nzg5YWJjZGVmZ2hpams=";

        // Act: check it
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a disable directive hides text up to the matching enable directive.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_DisableEnableBlock_SkipsRegion()
    {
        // Arrange: a misspelling before, inside, and after a disabled block
        var text = $"{Unknown}\n// {TextLintPrefix}disable\n{OtherUnknown}\n// {TextLintPrefix}enable\n{ThirdUnknown}\n";

        // Act: check it
        var issues = Check(text);

        // Assert: only the words outside the block are reported
        Assert.Equal([$"1:1 {Unknown}", $"5:1 {ThirdUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable-line directive hides the whole line.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_DisableLine_SkipsLine()
    {
        // Arrange: a misspelling before the directive on its line, and one on the next line
        var text = $"{Unknown} // {TextLintPrefix}disable-line\n{OtherUnknown}\n";

        // Act: check it
        var issues = Check(text);

        // Assert: only the next line is reported
        Assert.Equal([$"2:1 {OtherUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable-next-line directive hides the line after it.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_DisableNextLine_SkipsNextLine()
    {
        // Arrange: a directive followed by two misspelled lines
        var text = $"// {TextLintPrefix}disable-next-line\n{Unknown}\n{OtherUnknown}\n";

        // Act: check it
        var issues = Check(text);

        // Assert: only the second line is reported
        Assert.Equal([$"3:1 {OtherUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that ignore and words directives add words for the document.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_IgnoreAndWordsDirectives_AddDocumentWords()
    {
        // Arrange: one word added by each directive, and a third word that is not added
        var text = $"// {TextLintPrefix}ignore {Unknown}\n// {CspellPrefix}words {OtherUnknown}\n{Unknown} {OtherUnknown} {ThirdUnknown}\n";

        // Act: check it
        var issues = Check(text);

        // Assert: only the word that was not added is reported
        Assert.Equal([$"3:{Unknown.Length + OtherUnknown.Length + 3} {ThirdUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable directive without an enable directive hides the rest of the text.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_UnterminatedDisable_RunsToEndOfText()
    {
        // Arrange: a visible misspelling, then a disable directive that is never closed
        var text = $"{Unknown}\n// {TextLintPrefix}disable\n{OtherUnknown}\n{ThirdUnknown}\n";

        // Act: check it
        var issues = Check(text);

        // Assert: only the first line is reported
        Assert.Equal([$"1:1 {Unknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that every directive prefix is accepted, in any letter case.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_AllDirectivePrefixes_AreAccepted()
    {
        // Arrange: the five prefixes, with the first and last also in upper case
        string[] prefixes = ["cspe" + "ll", "spe" + "ll", "spe" + "llchecker", "spe" + "ll-checker", "textlint", "CSPE" + "LL", "TEXTLINT"];

        // Act: check, for each prefix, a text whose first line is hidden by the prefix and whose second line is not
        var results = prefixes
            .Select(prefix => Positions(Check($"{Unknown} {prefix}:disable-line\n{OtherUnknown}\n")))
            .ToList();

        // Assert: for every prefix, only the second line is reported
        Assert.All(results, positions => Assert.Equal([$"2:1 {OtherUnknown}"], positions));
    }

    /// <summary>
    ///     Test that a directive that is recognized but not supported is reported at its position.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_UnsupportedDirective_ReportsPosition()
    {
        // Arrange: a locale directive, which cspell supports and TextLint does not
        var text = $"// {TextLintPrefix}locale en-GB\n";

        // Act: check it
        var issue = Assert.Single(Check(text));

        // Assert: the finding is an unsupported directive at the position of its name
        Assert.Multiple(
            () => Assert.Equal(SpellingIssueKind.UnsupportedDirective, issue.Kind),
            () => Assert.Equal("locale", issue.Text),
            () => Assert.Equal(1, issue.Line),
            () => Assert.Equal(TextLintPrefix.Length + 4, issue.Column));
    }

    /// <summary>
    ///     Test that the language setting chooses between British and American spelling in checked files.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_BritishLanguage_AcceptsBritishNotAmericanSpelling()
    {
        // Arrange: one file that uses both spellings
        using var temp = new TempDirectory();
        temp.CreateFile("words.txt", "favorite favourite\n");
        var american = new TextLintSettings { Files = ["**/*.txt"] };
        var british = american with { Language = "en-GB" };

        // Act: check the file with each language
        var americanResult = await CheckFilesAsync(temp, american, cancellationToken: TestContext.Current.CancellationToken);
        var britishResult = await CheckFilesAsync(temp, british, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: each language reports the other's spelling only
        Assert.Multiple(
            () => Assert.Equal(["1:10 favourite"], Positions(americanResult.Issues)),
            () => Assert.Equal(["1:1 favorite"], Positions(britishResult.Issues)));
    }

    /// <summary>
    ///     Test that an accepted word matches in any letter case.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_WordsSetting_AcceptsAnyCase()
    {
        // Arrange: an unknown word written three ways, and settings that accept it
        var text = $"{Unknown} {Unknown.ToUpperInvariant()} W{Unknown[1..]}";
        var settings = new TextLintSettings { Words = [Unknown] };

        // Act: check it with and without the setting
        var accepted = Check(text, settings);
        var unaccepted = Check(text);

        // Assert: the setting accepts all three, and without it all three are reported
        Assert.Multiple(
            () => Assert.Empty(accepted),
            () => Assert.Equal(3, unaccepted.Count));
    }

    /// <summary>
    ///     Test that a flagged word is reported as flagged even though it is a known word, with its suggestions.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_FlagWords_ReportsFlaggedKind()
    {
        // Arrange: a known word that the settings flag, with two suggestions
        var settings = new TextLintSettings { FlagWords = ["dog->canine, hound"] };

        // Act: check a text that uses it
        var issue = Assert.Single(Check("the dog barks", settings));

        // Assert: the finding is flagged and carries the suggestions
        Assert.Multiple(
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind),
            () => Assert.Equal("dog", issue.Text),
            () => Assert.Equal(["canine", "hound"], issue.Suggestions));
    }

    /// <summary>
    ///     Test that a flagged entry with a capital letter matches only that exact spelling.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_FlagWordCapitalEntry_MatchesExactCase()
    {
        // Arrange: a flagged entry written with a capital, and the word in three letter cases
        var settings = new TextLintSettings { FlagWords = ["Dog"] };

        // Act: check them
        var issues = Check("Dog dog DOG", settings);

        // Assert: only the exact spelling is flagged
        Assert.Equal(["1:1 Dog"], Positions(issues));
    }

    /// <summary>
    ///     Test that an ignored word is never reported, even when it is also flagged.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_IgnoreWordsOverrideFlagWords_ReportsNothing()
    {
        // Arrange: a word that is both ignored and flagged
        var settings = new TextLintSettings { IgnoreWords = ["dog"], FlagWords = ["dog"] };

        // Act: check a text that uses it
        var issues = Check("the dog barks", settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a word-list entry with digits and punctuation matches a whole token.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_WordWithDigitsOrPunctuation_MatchesWholeToken()
    {
        // Arrange: an entry with a digit and a hyphen, used as a whole and in part
        var settings = new TextLintSettings { Words = [$"ab1cde-{Unknown}"] };
        var text = $"ab1cde-{Unknown} AB1CDE-{Unknown.ToUpperInvariant()} {Unknown}";

        // Act: check it
        var issues = Check(text, settings);

        // Assert: the whole tokens are accepted in any letter case, and the part alone is reported
        Assert.Equal([$"1:{text.LastIndexOf(Unknown, StringComparison.Ordinal) + 1} {Unknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a raised minimum word length skips words shorter than it.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_MinWordLengthRaised_SkipsShorterWords()
    {
        // Arrange: an unknown word of five letters and one of seven, with a minimum of six
        var settings = new TextLintSettings { MinWordLength = RaisedMinimum };

        // Act: check them
        var issues = Check($"{Unknown} {OtherUnknown}", settings);

        // Assert: only the longer word is reported
        Assert.Equal([$"1:7 {OtherUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a lowered minimum word length checks shorter words.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_MinWordLengthLowered_ChecksShorterWords()
    {
        // Arrange: an unknown word of two letters, with a minimum of two
        var settings = new TextLintSettings { MinWordLength = LoweredMinimum };

        // Act: check it with and without the setting
        var lowered = Check("xq", settings);
        var standard = Check("xq");

        // Assert: it is reported only with the lowered minimum
        Assert.Multiple(
            () => Assert.Equal(["1:1 xq"], Positions(lowered)),
            () => Assert.Empty(standard));
    }

    /// <summary>
    ///     Test that checking files reports each finding with its file, line, column, and word.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_FilesWithUnknownWords_ReportsFileLineColumnAndWord()
    {
        // Arrange: misspellings in two files, one in a subdirectory, and a file the patterns do not select
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", $"fine text\nthe {Unknown} here\n");
        temp.CreateFile("sub/b.txt", $"{OtherUnknown}\n");
        temp.CreateFile("c.md", $"{ThirdUnknown}\n");

        // Act: check the text files
        var result = await CheckFilesAsync(temp, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the findings name their files in path order, with line, column, and word
        Assert.Multiple(
            () => Assert.Equal(["a.txt", "sub/b.txt"], result.Issues.Select(issue => issue.FilePath).ToArray()),
            () => Assert.Equal([$"2:5 {Unknown}", $"1:1 {OtherUnknown}"], Positions(result.Issues)),
            () => Assert.Equal(TwoFiles, result.FilesChecked),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that files with only known words have no findings.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_CleanFiles_ReportsNothing()
    {
        // Arrange: files that contain only ordinary words
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", "The quick brown fox.\n");
        temp.CreateFile("sub/b.txt", "Jumps over the lazy dog.\n");

        // Act: check the files
        var result = await CheckFilesAsync(temp, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: nothing is reported and nothing is skipped
        Assert.Multiple(
            () => Assert.Empty(result.Issues),
            () => Assert.Empty(result.FileDiagnostics),
            () => Assert.Equal(TwoFiles, result.FilesChecked),
            () => Assert.False(result.HasProblems));
    }

    /// <summary>
    ///     Test that UTF-8, UTF-8 with a mark, and both UTF-16 encodings with a mark give the same positions.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_Utf8Utf16AndBom_ReportSamePositions()
    {
        // Arrange: the same text in four encodings, each written as bytes
        using var temp = new TempDirectory();
        var text = $"ok\nthe {Unknown} is here\n";
        await File.WriteAllBytesAsync(Path.Combine(temp.FullPath, "utf8.txt"), Encoding.UTF8.GetBytes(text), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(temp.FullPath, "utf8bom.txt"),
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(temp.FullPath, "utf16le.txt"),
            [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(temp.FullPath, "utf16be.txt"),
            [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)],
            TestContext.Current.CancellationToken);

        // Act: check the files
        var result = await CheckFilesAsync(temp, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: every file reports the same word at the same position, and none is skipped
        Assert.Multiple(
            () => Assert.Equal(EncodingCount, result.Issues.Count),
            () => Assert.All(result.Issues, issue => Assert.Equal($"2:5 {Unknown}", $"{issue.Line}:{issue.Column} {issue.Text}")),
            () => Assert.Empty(result.FileDiagnostics));
    }

    /// <summary>
    ///     Test that a file that looks binary is skipped and reported, and the other files are still checked.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_BinaryFile_IsSkippedAndReported()
    {
        // Arrange: a file with a NUL character and a text file with a misspelling
        using var temp = new TempDirectory();
        await File.WriteAllBytesAsync(
            Path.Combine(temp.FullPath, "data.txt"),
            [.. Encoding.ASCII.GetBytes($"ab{Unknown}"), 0, 1, 2],
            TestContext.Current.CancellationToken);
        temp.CreateFile("text.txt", $"{OtherUnknown}\n");

        // Act: check the files
        var result = await CheckFilesAsync(temp, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the binary file is reported as skipped and the text file is checked
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("data.txt", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.Binary, diagnostic.Kind),
            () => Assert.False(diagnostic.IsError),
            () => Assert.Equal(1, result.FilesSkipped),
            () => Assert.Equal(1, result.FilesChecked),
            () => Assert.Equal([$"1:1 {OtherUnknown}"], Positions(result.Issues)));
    }

    /// <summary>
    ///     Test that a file that cannot be read is reported as an error and the run continues.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_UnreadableFile_IsReportedAndRunContinues()
    {
        // Arrange: a file that another stream holds with no sharing, and a readable file with a misspelling
        using var temp = new TempDirectory();
        var lockedPath = temp.CreateFile("locked.txt", $"{Unknown}\n");
        temp.CreateFile("readable.txt", $"{OtherUnknown}\n");
        using var held = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act: check the files while the lock is held
        var result = await CheckFilesAsync(temp, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the locked file is an error diagnostic, the other file is checked, and the run has problems
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("locked.txt", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.Unreadable, diagnostic.Kind),
            () => Assert.True(diagnostic.IsError),
            () => Assert.Equal([$"1:1 {OtherUnknown}"], Positions(result.Issues)),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that a file larger than the maximum file size is skipped and reported.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_FileOverMaxSize_IsSkippedAndReported()
    {
        // Arrange: a large file and a small file with misspellings, and a size limit between them
        using var temp = new TempDirectory();
        temp.CreateFile("large.txt", string.Join(' ', Enumerable.Repeat(Unknown, (int)SmallSizeLimit)));
        temp.CreateFile("small.txt", $"{OtherUnknown}\n");
        var settings = new TextLintSettings { Files = ["**/*.txt"], MaxFileSize = SmallSizeLimit };

        // Act: check the files
        var result = await CheckFilesAsync(temp, settings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the large file is skipped as too large, and the small file is checked
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("large.txt", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.TooLarge, diagnostic.Kind),
            () => Assert.Equal(1, result.FilesSkipped),
            () => Assert.Equal([$"1:1 {OtherUnknown}"], Positions(result.Issues)));
    }

    /// <summary>
    ///     Test that the results are identical, and in the same order, at every degree of parallelism.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_DifferentConcurrency_ReturnsIdenticalOrderedResults()
    {
        // Arrange: many files, spread over directories, each with misspellings on several lines
        using var temp = new TempDirectory();
        string[] directories = ["docs/", "src/", string.Empty];
        for (var index = 0; index < FileCount; index++)
        {
            var directory = directories[index % directories.Length];
            temp.CreateFile($"{directory}file{index:D2}.txt", $"first {Unknown}{index}\nsecond line\n{OtherUnknown} {ThirdUnknown}\n");
        }

        // Act: check them with one worker and with many
        var single = await CheckFilesAsync(
            temp,
            options: new SpellCheckOptions { MaxDegreeOfParallelism = 1 },
            cancellationToken: TestContext.Current.CancellationToken);
        var many = await CheckFilesAsync(
            temp,
            options: new SpellCheckOptions { MaxDegreeOfParallelism = HighParallelism },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the findings are the same, and they are ordered by file path and then position
        var singleText = single.Issues.Select(issue => issue.ToString()).ToArray();
        var paths = single.Issues.Select(issue => issue.FilePath!).ToArray();
        Assert.Multiple(
            () => Assert.NotEmpty(singleText),
            () => Assert.Equal(singleText, many.Issues.Select(issue => issue.ToString())),
            () => Assert.Equal(paths.Order(StringComparer.Ordinal), paths),
            () => Assert.Equal(single.FilesChecked, many.FilesChecked));
    }

    /// <summary>
    ///     Test that a degree of parallelism below one is rejected.
    /// </summary>
    [Fact]
    public void TextLint_SpellCheck_InvalidConcurrency_Throws()
    {
        // Arrange: no setup required

        // Act / Assert: a degree of parallelism of zero is not accepted
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpellCheckOptions { MaxDegreeOfParallelism = 0 });
    }

    /// <summary>
    ///     Test that text matched by the <c>ignoreRegExpList</c> setting is not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_IgnoreRegExpList_SkipsMatches()
    {
        // Arrange: settings with an expression written between slashes, a bare expression, and a named pattern
        var settings = new TextLintSettings
        {
            IgnoreRegExpList = ["/wrold\\d+/", "speling\\w*", "ThirdWord"],
            Patterns = [new NamedPattern("ThirdWord", "/qwzx[a-z]+/")],
        };

        // Act: check a text with a match of each pattern and an unmatched word
        var issues = Check("wrold12 SPELINGS qwzxabc wrold", settings);

        // Assert: only the word that no pattern matches is reported
        Assert.Equal(["1:26 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that an ignoreRegExp directive in the text hides its matches in the whole text, as it does in cspell.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_IgnoreRegExpDirective_SkipsMatchesInTheWholeText()
    {
        // Arrange: a word before the directive that the pattern matches, the directive with the textlint prefix, and an
        // unmatched word after it
        const string text = "wrold12 speling\n// textlint:ignoreRegExp /wrold\\d+/\nxqzvw";

        // Act: check the text
        var issues = Check(text);

        // Assert: the matched word is hidden although it comes before the directive, and the others are reported
        Assert.Equal(["1:9 speling", "3:1 xqzvw"], Positions(issues));
    }

    /// <summary>
    ///     Test that a pattern of an ignoreRegExp directive that cannot be used is reported with its position and the reason.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_IgnoreRegExpDirectiveInvalid_IsReportedAtItsPosition()
    {
        // Arrange: a directive that names a pattern that nothing defines
        const string text = "// cspell:ignoreRegExp Urlz\nwrold";

        // Act: check the text under a source name
        var issues = new TextChecker(new TextLintSettings()).Check(text, "a.txt");

        // Assert: the directive is reported as an error at its pattern, and the text is checked as usual
        Assert.Multiple(
            () => Assert.StartsWith("a.txt(1,24): error: Cannot use the pattern 'Urlz' of the directive 'ignoreRegExp': ", issues[0].ToString(), StringComparison.Ordinal),
            () => Assert.Equal("2:1 wrold", $"{issues[1].Line}:{issues[1].Column} {issues[1].Text}"));
    }

    /// <summary>
    ///     Test that the dot-all flag lets a pattern of the settings match across lines.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_DotAllFlag_MatchesAcrossLines()
    {
        // Arrange: a pattern that spans from a start marker to an end marker with the dot, with and without the flag
        const string text = "wrold zorbbegin\nspeling\nzorbend xqzvw";
        var withFlag = new TextLintSettings { IgnoreRegExpList = ["/zorbbegin.*zorbend/s"] };
        var withoutFlag = new TextLintSettings { IgnoreRegExpList = ["/zorbbegin.*zorbend/"] };

        // Act: check the text with each
        var skipped = Check(text, withFlag);
        var checkedAgain = Check(text, withoutFlag);

        // Assert: with the flag the dot crosses the line breaks, so the whole span is skipped; without it, nothing is
        Assert.Multiple(
            () => Assert.Equal(["1:1 wrold", "3:9 xqzvw"], Positions(skipped)),
            () => Assert.Contains("speling", checkedAgain.Select(issue => issue.Text)));
    }

    /// <summary>
    ///     Test that the predefined pattern names that cspell knows exclude their text when they are listed.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_PredefinedPatternNames_ExcludeTheirText()
    {
        // Arrange: settings that list three predefined names that the default patterns do not apply, and one that they do
        var settings = new TextLintSettings { IgnoreRegExpList = ["string", "CStyleComment", "href", "Urls"] };

        // Act: check a text with a string, a comment, an attribute, and an unmatched word
        var issues = Check("\"wrold\" // speling\n<a href=\"qwzx\">x</a> xqzv", settings);

        // Assert: only the unmatched word is reported
        Assert.Equal(["2:22 xqzv"], Positions(issues));
    }

    /// <summary>
    ///     Test that a settings file with the pattern and random-string keys loads their values.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_PatternKeysConfigFile_LoadsConfiguredValues()
    {
        // Arrange: a settings file that uses every key of this feature
        using var temp = new TempDirectory();
        const string yaml =
            "ignoreRegExpList:\n  - \"/wrold\\\\d+/\"\n  - Marker\n  - Urls\npatterns:\n  - name: Marker\n    pattern: \"^speling.*$\"\n" +
            "ignoreRandomStrings: false\nminRandomLength: 25\n";

        // Act: load it
        var result = await LoadAsync(temp, yaml);

        // Assert: every key is mapped
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["/wrold\\d+/", "Marker", "Urls"], settings.IgnoreRegExpList),
            () => Assert.Equal("Marker", Assert.Single(settings.Patterns).Name),
            () => Assert.False(settings.IgnoreRandomStrings),
            () => Assert.Equal(RandomLengthConfigured, settings.MinRandomLength));
    }

    /// <summary>
    ///     Test that an invalid regular expression is reported with the file, line, and column of its entry.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_InvalidRegularExpression_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file with an expression that has an unsupported flag
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "ignoreRegExpList:\n  - \"/wrold/x\"\n");

        // Assert: loading fails with an error at the entry that names the flag
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("the flag 'x' is not supported", diagnostic.Message),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(SecondLine, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a pattern name that nothing defines is reported with the file, line, and column of its entry.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_UnknownPatternName_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file with a misspelled predefined name
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "ignoreRegExpList:\n  - Urlz\n");

        // Assert: loading fails with an error at the entry that says the name is not defined
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("not a defined or predefined pattern name", diagnostic.Message),
            () => Assert.Equal(SecondLine, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that patterns from a settings file exclude text in the files of a run, and that a Markdown file also
    ///     skips its link targets.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_PatternsFromConfigFile_SkipMatchesInFiles()
    {
        // Arrange: a settings file with a pattern, and a text file and a Markdown file with the same matched text
        using var temp = new TempDirectory();
        var loaded = await LoadAsync(temp, "files: ['**/*.txt', '**/*.md']\nignoreRegExpList:\n  - \"/wrold\\\\d+/\"\n");
        temp.CreateFile("a.txt", $"wrold1 [x]({OtherUnknown}) {ThirdUnknown}\n");
        temp.CreateFile("b.md", $"wrold1 [x]({OtherUnknown}) {ThirdUnknown}\n");

        // Act: check the files with the loaded settings
        var result = await SpellChecker.CheckAsync(
            loaded.Settings!,
            temp.FullPath,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the pattern hides its match in both files, and only the Markdown file also hides the link target
        Assert.Equal(
            [$"a.txt:{OtherUnknown}", $"a.txt:{ThirdUnknown}", $"b.md:{ThirdUnknown}"],
            result.Issues.Select(issue => $"{issue.FilePath}:{issue.Text}"));
    }

    /// <summary>
    ///     Test that a pattern that backtracks without end makes the file a timed-out error instead of hanging the run.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_CatastrophicPattern_ReportsTimedOutDiagnostic()
    {
        // Arrange: a pattern that backtracks exponentially, a file that almost matches it, and a file that does not start to match
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", new string('a', CatastrophicLength) + "b\n");
        temp.CreateFile("b.txt", $"{Unknown}\n");
        var settings = new TextLintSettings { Files = ["**/*.txt"], IgnoreRegExpList = ["/^(a|aa)+$/m"] };

        // Act: check the files through the internal seam that shortens the limit, so that the test is fast
        var result = await SpellChecker.CheckWithSelectionAsync(
            settings,
            token => FileSelector.Select(temp.FullPath, settings.Files, settings.IgnorePaths, settings.EnableGlobDot, token),
            null,
            TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(100));
        var diagnostic = Assert.Single(result.FileDiagnostics);
        Assert.Multiple(
            () => Assert.Equal("a.txt", diagnostic.FilePath),
            () => Assert.Equal(FileDiagnosticKind.TimedOut, diagnostic.Kind),
            () => Assert.Equal(["b.txt"], result.Issues.Select(issue => issue.FilePath)),
            () => Assert.True(result.HasProblems));
    }

    /// <summary>
    ///     Test that the link targets, reference labels, anchors, and entities of a Markdown file are not checked.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_MarkdownLinkTargets_AreNotChecked()
    {
        // Arrange: a line of Markdown with a link target, a reference label, an anchor id, and an entity
        const string markdown = "[text](wrold) [text][speling] <a id=\"qwzx\"></a> &wrold; hello";

        // Act: check it as a Markdown file and as a text file
        var checker = new TextChecker(new TextLintSettings());
        var asMarkdown = checker.Check(markdown, "docs/guide.md");
        var asText = checker.Check(markdown, "docs/guide.txt");

        // Assert: nothing is reported in the Markdown file, and the words are reported in the text file
        Assert.Multiple(
            () => Assert.Empty(asMarkdown),
            () => Assert.NotEmpty(asText));
    }

    /// <summary>
    ///     Test that a long token that looks like random data is not checked, and is checked when the setting is off.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_RandomLookingToken_IsNotChecked()
    {
        // Arrange: a 46-character token of non-words, digits, and hyphens that looks like random data
        const string token = "qwzx-1-xqzv-2-qwzx-3-xqzv-4-qwzx-5-xqzv-6-qwzx";

        // Act: check it with the defaults, with the skipping off, and with a minimum length above its length
        var skipped = Check(token);
        var checkedWhenOff = Check(token, new TextLintSettings { IgnoreRandomStrings = false });
        var checkedWhenShort = Check(token, new TextLintSettings { MinRandomLength = token.Length + 1 });

        // Assert: it is skipped by default and checked in the other two cases
        Assert.Multiple(
            () => Assert.Empty(skipped),
            () => Assert.NotEmpty(checkedWhenOff),
            () => Assert.Equal(Positions(checkedWhenOff), Positions(checkedWhenShort)));
    }

    /// <summary>
    ///     Test that a contraction whose parts are known or too short to check is accepted, and one with an unknown part is not.
    /// </summary>
    [Fact]
    public void TextLint_CheckText_ContractionOfKnownWords_IsAccepted()
    {
        // Arrange: contractions of known words, and one with an unknown part
        const string text = "exec'd OR'd rock'n'roll she'd've wrold'd";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the contraction with an unknown part is reported, whole
        Assert.Equal(["1:34 wrold'd"], Positions(issues));
    }

    /// <summary>
    ///     Test that a canceled token stops the run with an exception.
    /// </summary>
    [Fact]
    public async Task TextLint_SpellCheck_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange: a directory with a file, and a token that is already canceled
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", $"{Unknown}\n");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the run is canceled and throws
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CheckFilesAsync(temp, cancellationToken: source.Token));
    }
}
