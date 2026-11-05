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

// cspell:ignore wrold Wrold speling xqzv xqz zorb ZORB zorbs ZORBs nwrold wroldspeling wrolhttp wrol qqq
// cspell:ignore favourite textlint FEFF cspe

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;

namespace DemaConsulting.TextLint.Tests.SpellChecking;

/// <summary>
///     Unit tests for the <see cref="TextChecker"/> class and the <see cref="SpellingIssue"/> findings it returns.
/// </summary>
/// <remarks>
///     The checker is tested with its documented dependencies real: the settings value, the embedded
///     dictionaries behind the spelling dictionary, and the text scanning units (tokenizer, default
///     patterns, and directives). Words are chosen to be unambiguous: <c>wrold</c> and <c>speling</c> are
///     misspellings that no dictionary or technical list holds, <c>xqzv</c> is a four-letter and <c>xqz</c> a
///     three-letter non-word, and <c>hello</c> and <c>world</c> are ordinary English words.
/// </remarks>
public class TextCheckerTests
{
    /// <summary>
    ///     A misspelled word that is five characters long and unknown to every dictionary.
    /// </summary>
    private const string Unknown = "wrold";

    /// <summary>
    ///     A second misspelled word, seven characters long and unknown to every dictionary.
    /// </summary>
    private const string OtherUnknown = "speling";

    /// <summary>
    ///     A non-word of exactly the default minimum word length.
    /// </summary>
    private const string FourLetterNonWord = "xqzv";

    /// <summary>
    ///     A non-word one character shorter than the default minimum word length.
    /// </summary>
    private const string ThreeLetterNonWord = "xqz";

    /// <summary>
    ///     Text made only of known English words.
    /// </summary>
    private const string KnownSentence = "Hello world, this is a spelling test.";

    /// <summary>
    ///     The number of threads that use one checker at the same time in the concurrency test.
    /// </summary>
    private const int ConcurrentThreads = 8;

    /// <summary>
    ///     The number of calls each thread makes in the concurrency test.
    /// </summary>
    private const int CallsPerThread = 100;

    /// <summary>
    ///     The length of the very long word used to test hostile input.
    /// </summary>
    private const int VeryLongWordLength = 50_000;

    /// <summary>
    ///     The number of lines in the long text used to test line counting.
    /// </summary>
    private const int ManyLines = 5_000;

    /// <summary>
    ///     A minimum word length below the default, which lets three-letter words be checked.
    /// </summary>
    private const int LowMinimumLength = 3;

    /// <summary>
    ///     The default minimum word length, stated so that boundary tests can name it.
    /// </summary>
    private const int DefaultMinimumLength = 4;

    /// <summary>
    ///     A minimum word length one above the default.
    /// </summary>
    private const int HighMinimumLength = 5;

    /// <summary>
    ///     A minimum word length that lies between a five-letter and a seven-letter word.
    /// </summary>
    private const int HigherMinimumLength = 6;

    /// <summary>
    ///     Checks a text with a checker built from settings and returns the findings.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <param name="settings">The settings, or null for the defaults.</param>
    /// <param name="sourceName">The source name to put on the findings, or null.</param>
    /// <returns>The findings in order.</returns>
    private static IReadOnlyList<SpellingIssue> Check(string text, TextLintSettings? settings = null, string? sourceName = null) =>
        new TextChecker(settings ?? new TextLintSettings()).Check(text, sourceName);

    /// <summary>
    ///     Extracts the text of each finding.
    /// </summary>
    /// <param name="issues">The findings.</param>
    /// <returns>The text of each finding in order.</returns>
    private static string[] Words(IReadOnlyList<SpellingIssue> issues) => [.. issues.Select(issue => issue.Text)];

    /// <summary>
    ///     Formats each finding as <c>line:column text</c> so that positions and words are compared together.
    /// </summary>
    /// <param name="issues">The findings.</param>
    /// <returns>One string per finding in order.</returns>
    private static string[] Positions(IReadOnlyList<SpellingIssue> issues) =>
        [.. issues.Select(issue => $"{issue.Line}:{issue.Column} {issue.Text}")];

    /// <summary>
    ///     Test that an unknown word is reported with its line, column, and text.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnknownWord_ReportsWordAtPosition()
    {
        // Arrange: a sentence whose second word is misspelled
        const string text = "Hello wrold";
        const int expectedColumn = 7;

        // Act: check the text
        var issue = Assert.Single(Check(text));

        // Assert: the finding names the word and its position
        Assert.Multiple(
            () => Assert.Equal(Unknown, issue.Text),
            () => Assert.Equal(1, issue.Line),
            () => Assert.Equal(expectedColumn, issue.Column),
            () => Assert.Equal(SpellingIssueKind.UnknownWord, issue.Kind));
    }

    /// <summary>
    ///     Test that a text made of known words has no findings.
    /// </summary>
    [Fact]
    public void TextChecker_Check_KnownWords_ReportsNothing()
    {
        // Arrange: text made only of ordinary words

        // Act: check the text
        var issues = Check(KnownSentence);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an empty text has no findings.
    /// </summary>
    [Fact]
    public void TextChecker_Check_EmptyText_ReturnsEmpty()
    {
        // Arrange: an empty text

        // Act: check the text
        var issues = Check(string.Empty);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a text of white space and punctuation has no findings.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WhiteSpaceAndPunctuationOnly_ReturnsEmpty()
    {
        // Arrange: a text with no word in it
        const string text = " \t\r\n ... --- !!! ??? \n";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the source name is copied to every finding.
    /// </summary>
    [Fact]
    public void TextChecker_Check_SourceName_IsCopiedToFindings()
    {
        // Arrange: a text with two findings and a source name
        const string sourceName = "docs/guide.md";
        var text = $"{Unknown} {OtherUnknown}";

        // Act: check the text with the name
        var issues = Check(text, sourceName: sourceName);

        // Assert: both findings carry the name
        Assert.Equal([sourceName, sourceName], issues.Select(issue => issue.FilePath));
    }

    /// <summary>
    ///     Test that findings have no file path when no source name is given.
    /// </summary>
    [Fact]
    public void TextChecker_Check_NoSourceName_LeavesFilePathNull()
    {
        // Arrange: a text with one finding

        // Act: check the text without a name
        var issue = Assert.Single(Check(Unknown));

        // Assert: the finding has no file path
        Assert.Null(issue.FilePath);
    }

    /// <summary>
    ///     Test that a null text is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Check_NullText_ThrowsArgumentNullException()
    {
        // Arrange: a checker with the default settings
        var checker = new TextChecker(new TextLintSettings());

        // Act / Assert: a null text throws and names the parameter
        var exception = Assert.Throws<ArgumentNullException>(() => checker.Check(null!));
        Assert.Equal("text", exception.ParamName);
    }

    /// <summary>
    ///     Test that the returned list cannot be changed by casting it to a mutable interface.
    /// </summary>
    [Fact]
    public void TextChecker_Check_Result_IsImmutable()
    {
        // Arrange: the findings of a text with one unknown word
        var issues = Check(Unknown);

        // Act / Assert: the list is not a mutable collection
        var list = Assert.IsAssignableFrom<IList<SpellingIssue>>(issues);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    /// <summary>
    ///     Test that a very long unknown word is reported once, at its start.
    /// </summary>
    [Fact]
    public void TextChecker_Check_VeryLongWord_IsReportedOnce()
    {
        // Arrange: a word of fifty thousand characters that is not a repeated letter
        var word = string.Concat(Enumerable.Repeat("qz", VeryLongWordLength / 2));

        // Act: check the text
        var issue = Assert.Single(Check(word));

        // Assert: the whole word is reported at the first column
        Assert.Multiple(
            () => Assert.Equal(word, issue.Text),
            () => Assert.Equal(1, issue.Column));
    }

    /// <summary>
    ///     Test that a lone surrogate in the text does not stop the check.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LoneSurrogate_ReportsFollowingWord()
    {
        // Arrange: a lone high surrogate directly before an unknown word
        var text = "\uD800 " + Unknown;

        // Act: check the text
        var issue = Assert.Single(Check(text));

        // Assert: the word after it is reported in the right place
        Assert.Multiple(
            () => Assert.Equal(Unknown, issue.Text),
            () => Assert.Equal(3, issue.Column));
    }

    /// <summary>
    ///     Test that a known compound identifier that the settings list is checked as a whole and not split.
    /// </summary>
    [Fact]
    public void TextChecker_Check_KnownCompoundIdentifier_IsNotSplit()
    {
        // Arrange: a camel-case identifier whose pieces are unknown but which is listed as a word
        var settings = new TextLintSettings { Words = ["wroldSpeling"] };

        // Act: check the identifier
        var issues = Check("wroldSpeling", settings);

        // Assert: it is accepted as a whole
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an unknown camel-case identifier reports only its unknown pieces.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnknownCamelCase_ReportsOnlyUnknownPieces()
    {
        // Arrange: an identifier of two known words around a misspelling
        const string text = "helloWroldWorld";

        // Act: check the identifier
        var issues = Check(text);

        // Assert: only the misspelled piece is reported, at its own position
        Assert.Equal(["1:6 Wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a snake-case identifier reports each unknown word separately.
    /// </summary>
    [Fact]
    public void TextChecker_Check_SnakeCase_ReportsEachUnknownWord()
    {
        // Arrange: an identifier with underscores and two misspellings
        const string text = "hello_wrold_world_speling";

        // Act: check the identifier
        var issues = Check(text);

        // Assert: each misspelling is reported at its own position
        Assert.Equal(["1:7 wrold", "1:19 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that digits separate words.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DigitsBetweenWords_SeparateWords()
    {
        // Arrange: a misspelling between a word and a digit run
        const string text = "hello2wrold3";

        // Act: check the text
        var issues = Check(text);

        // Assert: the misspelling is reported by itself
        Assert.Equal(["1:7 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that hyphens separate words.
    /// </summary>
    [Fact]
    public void TextChecker_Check_HyphenatedWords_AreCheckedSeparately()
    {
        // Arrange: a hyphenated phrase with a misspelling
        const string text = "hello-wrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the misspelling is reported by itself
        Assert.Equal(["1:7 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a run of capitals before a capitalized word is split from it.
    /// </summary>
    [Fact]
    public void TextChecker_Check_AcronymBeforeWord_SplitsAtTheLastCapital()
    {
        // Arrange: an identifier made of an acronym and a misspelled capitalized word
        const string text = "HTTPWrold";

        // Act: check the identifier
        var issues = Check(text);

        // Assert: only the misspelled word is reported
        Assert.Equal(["1:5 Wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that an unknown word in one letter case is not split without a case change.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LowerCaseConcatenation_IsReportedWhole()
    {
        // Arrange: two misspellings written together in lower case, which no case change separates
        const string text = "wroldspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: one finding for the whole word
        Assert.Equal(["1:1 wroldspeling"], Positions(issues));
    }

    /// <summary>
    ///     Test that an apostrophe stays inside a word.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ApostropheWord_StaysWhole()
    {
        // Arrange: contractions that are known words
        const string text = "don't can't won't";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the possessive of a known word is accepted, with a straight or a typographic apostrophe.
    /// </summary>
    [Fact]
    public void TextChecker_Check_PossessiveOfKnownWord_IsAccepted()
    {
        // Arrange: possessives of a known word with both apostrophe characters
        const string text = "world's world\u2019s";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the possessive of an unknown word is still reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_PossessiveOfUnknownWord_IsReported()
    {
        // Arrange: a possessive made from a misspelling
        const string text = "wrold's";

        // Act: check the text
        var issues = Check(text);

        // Assert: the word is reported
        Assert.Equal(["1:1 wrold's"], Positions(issues));
    }

    /// <summary>
    ///     Test that a known word that follows a backslash escape is accepted.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordAfterBackslashEscape_IsAccepted()
    {
        // Arrange: a string literal in which the escape letter n is written directly before a known word
        const string text = "\\nworld";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an unknown word after a backslash escape is still reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnknownWordAfterBackslashEscape_IsReported()
    {
        // Arrange: an escape letter before a misspelling
        const string text = "\\nwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the word with its escape letter is reported
        Assert.Equal(["1:2 nwrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that the escape allowance applies only after a backslash.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LetterPrefixWithoutBackslash_IsReported()
    {
        // Arrange: a known word with an extra first letter and no backslash before it
        const string text = "nworld";

        // Act: check the text
        var issues = Check(text);

        // Assert: the word is reported
        Assert.Equal(["1:1 nworld"], Positions(issues));
    }

    /// <summary>
    ///     Test that a run of capitals with a lower-case ending is accepted when the capitals are known.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CapitalRunWithEnding_IsAccepted()
    {
        // Arrange: plural acronyms
        const string text = "URLs APIs";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a run of capitals that is too short to check keeps its ending acceptable.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ShortCapitalRunWithEnding_IsAccepted()
    {
        // Arrange: an unknown four-letter acronym with an ending, and a minimum length above the acronym
        const string text = "ZORBs";
        var settings = new TextLintSettings { MinWordLength = HighMinimumLength };

        // Act: check the text
        var issues = Check(text, settings);

        // Assert: nothing is reported, because the capitals are shorter than the minimum
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a run of capitals long enough to check is reported when unknown, ending or not.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnknownCapitalRunWithEnding_IsReported()
    {
        // Arrange: the same acronym with the default minimum length, which it reaches
        const string text = "ZORBs";

        // Act: check the text
        var issues = Check(text);

        // Assert: something is reported at the start of the word
        Assert.Equal(1, Assert.Single(issues).Column);
    }

    /// <summary>
    ///     Test that a word of one repeated letter is never reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_RepeatedLetters_AreNeverReported()
    {
        // Arrange: placeholders made of one letter, in both letter cases
        const string text = "xxxx XXXX zzzzzzzz";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a word of three repeated letters is below the repeated-letter rule and is reported when checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ThreeRepeatedLettersAtLowMinimum_AreReported()
    {
        // Arrange: three repeated letters with a minimum length that lets them be checked
        var settings = new TextLintSettings { MinWordLength = LowMinimumLength };

        // Act: check the text
        var issues = Check("qqq", settings);

        // Assert: the word is reported
        Assert.Equal(["1:1 qqq"], Positions(issues));
    }

    /// <summary>
    ///     Test that a word shorter than the minimum length is not checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordShorterThanMinimum_IsNotChecked()
    {
        // Arrange: a non-word one character short of the default minimum

        // Act: check the text
        var issues = Check(ThreeLetterNonWord);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a word of exactly the minimum length is checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordAtMinimumLength_IsChecked()
    {
        // Arrange: a non-word of exactly the default minimum

        // Act: check the text
        var issues = Check(FourLetterNonWord);

        // Assert: the word is reported
        Assert.Equal([$"1:1 {FourLetterNonWord}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a raised minimum length skips longer words.
    /// </summary>
    [Fact]
    public void TextChecker_Check_MinimumLengthRaised_SkipsShorterWords()
    {
        // Arrange: a five-letter and a seven-letter misspelling with the minimum at six
        var settings = new TextLintSettings { MinWordLength = HigherMinimumLength };

        // Act: check the text
        var issues = Check($"{Unknown} {OtherUnknown}", settings);

        // Assert: only the longer word is reported
        Assert.Equal([$"1:7 {OtherUnknown}"], Positions(issues));
    }

    /// <summary>
    ///     Test that a lowered minimum length checks shorter words.
    /// </summary>
    [Fact]
    public void TextChecker_Check_MinimumLengthLowered_ChecksShorterWords()
    {
        // Arrange: a three-letter non-word with the minimum at three
        var settings = new TextLintSettings { MinWordLength = LowMinimumLength };

        // Act: check the text
        var issues = Check(ThreeLetterNonWord, settings);

        // Assert: the word is reported
        Assert.Equal([$"1:1 {ThreeLetterNonWord}"], Positions(issues));
    }

    /// <summary>
    ///     Test that the minimum length counts UTF-16 code units, so a character outside the Basic Multilingual Plane counts as two.
    /// </summary>
    [Fact]
    public void TextChecker_Check_MinimumLength_CountsUtf16Units()
    {
        // Arrange: a word of two letters outside the Basic Multilingual Plane, four UTF-16 units long
        const string text = "\U0001D400\U0001D401";

        // Act: check with the minimum at four and at five
        var atFour = Check(text, new TextLintSettings { MinWordLength = DefaultMinimumLength });
        var atFive = Check(text, new TextLintSettings { MinWordLength = HighMinimumLength });

        // Assert: the word is long enough for four and too short for five
        Assert.Multiple(
            () => Assert.Single(atFour),
            () => Assert.Empty(atFive));
    }

    /// <summary>
    ///     Test that a piece of an unknown word that is shorter than the minimum is not reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ShortPieceOfUnknownWord_IsNotReported()
    {
        // Arrange: an identifier whose unknown piece is three characters long
        const string text = "helloXqzWorld";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an entry of the words setting is accepted in any letter case.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordsEntry_AcceptsAnyCase()
    {
        // Arrange: an entry in lower case and the word written in three cases
        var settings = new TextLintSettings { Words = [Unknown] };

        // Act: check the text
        var issues = Check("wrold Wrold WROLD", settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an entry of the words setting with a capital letter also accepts other letter cases.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordsEntryWithCapital_AcceptsAnyCase()
    {
        // Arrange: an entry with a capital letter and the word written in lower case
        var settings = new TextLintSettings { Words = ["Wrold"] };

        // Act: check the text
        var issues = Check(Unknown, settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an entry of the ignoreWords setting is never reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreWordsEntry_IsNotReported()
    {
        // Arrange: a misspelling that the settings ignore
        var settings = new TextLintSettings { IgnoreWords = [Unknown] };

        // Act: check the text
        var issues = Check($"{Unknown} {OtherUnknown}", settings);

        // Assert: only the other misspelling is reported
        Assert.Equal([OtherUnknown], Words(issues));
    }

    /// <summary>
    ///     Test that an ignored word wins over a flagged word.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordBothIgnoredAndFlagged_IsNotReported()
    {
        // Arrange: the same word in ignoreWords and flagWords
        var settings = new TextLintSettings { IgnoreWords = ["hello"], FlagWords = ["hello"] };

        // Act: check the text
        var issues = Check("hello", settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a flagged word wins over an accepted word.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordBothAcceptedAndFlagged_IsReported()
    {
        // Arrange: the same word in words and flagWords
        var settings = new TextLintSettings { Words = [Unknown], FlagWords = [Unknown] };

        // Act: check the text
        var issues = Check(Unknown, settings);

        // Assert: the word is reported as flagged
        Assert.Equal(SpellingIssueKind.FlaggedWord, Assert.Single(issues).Kind);
    }

    /// <summary>
    ///     Test that a flagged word wins over the dictionary.
    /// </summary>
    [Fact]
    public void TextChecker_Check_KnownWordThatIsFlagged_IsReported()
    {
        // Arrange: an ordinary word in flagWords
        var settings = new TextLintSettings { FlagWords = ["hello"] };

        // Act: check the text
        var issues = Check("Say hello", settings);

        // Assert: the known word is reported as flagged
        var issue = Assert.Single(issues);
        Assert.Multiple(
            () => Assert.Equal("hello", issue.Text),
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind));
    }

    /// <summary>
    ///     Test that a words entry that contains digits matches the whole token and not its pieces.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordsEntryWithDigits_MatchesWholeToken()
    {
        // Arrange: an entry with a digit, and a text with the token and with its letters alone
        var settings = new TextLintSettings { Words = ["wrold2"] };

        // Act: check the token, and the same letters without the digit
        var token = Check("wrold2", settings);
        var letters = Check(Unknown, settings);

        // Assert: the token is accepted and the bare letters are not
        Assert.Multiple(
            () => Assert.Empty(token),
            () => Assert.Single(letters));
    }

    /// <summary>
    ///     Test that an ignoreWords entry that contains punctuation matches the whole token and not its pieces.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreEntryWithPunctuation_MatchesWholeToken()
    {
        // Arrange: a hyphenated entry, and a text with the token and with its words apart
        var settings = new TextLintSettings { IgnoreWords = ["wrold-speling"] };

        // Act: check the hyphenated token, and the same words separated by a space
        var token = Check("wrold-speling", settings);
        var apart = Check("wrold speling", settings);

        // Assert: the token is ignored and the separate words are reported
        Assert.Multiple(
            () => Assert.Empty(token),
            () => Assert.Equal(2, apart.Count));
    }

    /// <summary>
    ///     Test that a flagged word is reported with the flagged kind.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagWords_ReportsFlaggedKind()
    {
        // Arrange: a word in flagWords
        var settings = new TextLintSettings { FlagWords = ["obsolete"] };

        // Act: check the text
        var issue = Assert.Single(Check("an obsolete call", settings));

        // Assert: the finding is flagged and at the word
        Assert.Multiple(
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind),
            () => Assert.Equal(4, issue.Column),
            () => Assert.Empty(issue.Suggestions));
    }

    /// <summary>
    ///     Test that the arrow form of a flag entry attaches its suggestions.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagEntryWithArrow_ReportsSuggestions()
    {
        // Arrange: a flag entry with two suggestions after an arrow
        var settings = new TextLintSettings { FlagWords = ["master->main, primary"] };

        // Act: check the text
        var issue = Assert.Single(Check("the master branch", settings));

        // Assert: the finding carries both suggestions in order
        Assert.Equal(["main", "primary"], issue.Suggestions);
    }

    /// <summary>
    ///     Test that the colon form of a flag entry attaches its suggestion.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagEntryWithColon_ReportsSuggestion()
    {
        // Arrange: a flag entry with a suggestion after a colon
        var settings = new TextLintSettings { FlagWords = ["dummy: placeholder"] };

        // Act: check the text
        var issue = Assert.Single(Check("a dummy value", settings));

        // Assert: the finding carries the suggestion and the word, not the entry
        Assert.Multiple(
            () => Assert.Equal("dummy", issue.Text),
            () => Assert.Equal(["placeholder"], issue.Suggestions));
    }

    /// <summary>
    ///     Test that a flagged word is reported even when it is shorter than the minimum length.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlaggedWordShorterThanMinimum_IsStillReported()
    {
        // Arrange: a three-letter flagged word with the default minimum of four
        var settings = new TextLintSettings { FlagWords = [ThreeLetterNonWord] };

        // Act: check the text
        var issues = Check(ThreeLetterNonWord, settings);

        // Assert: the word is reported as flagged
        Assert.Equal(SpellingIssueKind.FlaggedWord, Assert.Single(issues).Kind);
    }

    /// <summary>
    ///     Test that a flag entry written in lower case matches the word in any letter case.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagEntryInLowerCase_MatchesAnyCase()
    {
        // Arrange: a lower-case flag entry and the word in three cases
        var settings = new TextLintSettings { FlagWords = ["obsolete"] };

        // Act: check the text
        var issues = Check("obsolete Obsolete OBSOLETE", settings);

        // Assert: all three are reported
        Assert.Equal(3, issues.Count);
    }

    /// <summary>
    ///     Test that a flag entry with a capital letter matches only that exact spelling.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagEntryWithCapital_MatchesOnlyThatSpelling()
    {
        // Arrange: a flag entry with a capital letter and the word in lower and capitalized form
        var settings = new TextLintSettings { FlagWords = ["Obsolete"] };

        // Act: check the text
        var issues = Check("obsolete Obsolete", settings);

        // Assert: only the capitalized form is reported
        Assert.Equal(["1:10 Obsolete"], Positions(issues));
    }

    /// <summary>
    ///     Test that a flagged piece of an unknown identifier is reported as flagged.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlaggedPieceOfIdentifier_IsReportedAsFlagged()
    {
        // Arrange: an identifier that contains a flagged word between two short pieces
        var settings = new TextLintSettings { FlagWords = ["dummy"] };

        // Act: check the identifier
        var issue = Assert.Single(Check("fooDummyBar", settings));

        // Assert: the flagged piece is reported at its own position
        Assert.Multiple(
            () => Assert.Equal("Dummy", issue.Text),
            () => Assert.Equal(4, issue.Column),
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind));
    }

    /// <summary>
    ///     Test that a flag entry that contains digits matches the whole token and reports it as one finding.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlagEntryWithDigits_ReportsWholeToken()
    {
        // Arrange: a flag entry with a digit
        var settings = new TextLintSettings { FlagWords = ["hello2"] };

        // Act: check the token
        var issue = Assert.Single(Check("say hello2", settings));

        // Assert: the whole token is reported as flagged
        Assert.Multiple(
            () => Assert.Equal("hello2", issue.Text),
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind));
    }

    /// <summary>
    ///     Test that the American dictionary is the default and rejects a British spelling.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DefaultLanguage_RejectsBritishSpelling()
    {
        // Arrange: a British spelling and its American form

        // Act: check both with the default settings
        var issues = Check("favourite favorite");

        // Assert: only the British spelling is reported
        Assert.Equal(["1:1 favourite"], Positions(issues));
    }

    /// <summary>
    ///     Test that the British language accepts a British spelling and rejects the American one.
    /// </summary>
    [Fact]
    public void TextChecker_Check_BritishLanguage_AcceptsBritishNotAmericanSpelling()
    {
        // Arrange: settings that select British English
        var settings = new TextLintSettings { Language = "en-GB" };

        // Act: check a British spelling and its American form
        var issues = Check("favourite favorite", settings);

        // Assert: only the American spelling is reported
        Assert.Equal(["1:11 favorite"], Positions(issues));
    }

    /// <summary>
    ///     Test that a list of languages accepts the spellings of each.
    /// </summary>
    [Fact]
    public void TextChecker_Check_BothLanguages_AcceptEitherSpelling()
    {
        // Arrange: settings that select both American and British English
        var settings = new TextLintSettings { Language = "en-US,en-GB" };

        // Act: check a British spelling and its American form
        var issues = Check("favourite favorite", settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a line feed starts a new line and resets the column.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LineFeed_StartsNewLine()
    {
        // Arrange: a misspelling on the second line
        const string text = "hello\nwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the finding is at the start of line two
        Assert.Equal(["2:1 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a carriage return and line feed together count as one line break.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CarriageReturnLineFeed_CountsOneLineBreak()
    {
        // Arrange: lines separated by CRLF with a misspelling on each of the last two
        const string text = "hello\r\nwrold\r\nspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: the lines are counted once per CRLF and the columns start at one
        Assert.Equal(["2:1 wrold", "3:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a lone carriage return is not a line break.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LoneCarriageReturn_IsNotLineBreak()
    {
        // Arrange: a carriage return between a word and a misspelling
        const string text = "hello\rwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the finding stays on line one, after the carriage return
        Assert.Equal(["1:7 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that the Unicode line separator is not a line break.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LineSeparatorCharacter_IsNotLineBreak()
    {
        // Arrange: U+2028 between a word and a misspelling
        const string text = "hello\u2028wrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the finding stays on line one
        Assert.Equal(["1:7 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a character outside the Basic Multilingual Plane counts as two columns.
    /// </summary>
    [Fact]
    public void TextChecker_Check_AstralCharacter_CountsTwoColumns()
    {
        // Arrange: an emoji, a space, and a misspelling
        const string text = "\U0001F600 wrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the column counts the emoji as two UTF-16 units
        Assert.Equal(["1:4 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a byte-order mark character in the text is just a character and counts as one column.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ByteOrderMarkCharacter_CountsOneColumn()
    {
        // Arrange: U+FEFF directly before a misspelling
        const string text = "\uFEFF wrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the character takes one column and is not removed
        Assert.Equal(["1:3 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a tab counts as one column.
    /// </summary>
    [Fact]
    public void TextChecker_Check_Tab_CountsOneColumn()
    {
        // Arrange: a tab before a misspelling
        const string text = "\twrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the tab takes one column
        Assert.Equal(["1:2 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that the line number is right after thousands of lines.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ManyLines_CountsLinesExactly()
    {
        // Arrange: thousands of clean lines before a misspelling
        var text = string.Concat(Enumerable.Repeat("hello\n", ManyLines)) + Unknown;

        // Act: check the text
        var issue = Assert.Single(Check(text));

        // Assert: the finding is on the line after the last clean one
        Assert.Multiple(
            () => Assert.Equal(ManyLines + 1, issue.Line),
            () => Assert.Equal(1, issue.Column));
    }

    /// <summary>
    ///     Test that findings are returned in text order with their lines and columns.
    /// </summary>
    [Fact]
    public void TextChecker_Check_SeveralFindings_AreInTextOrder()
    {
        // Arrange: misspellings on three lines, two of them on the same line
        const string text = "wrold hello speling\n\nxqzv";

        // Act: check the text
        var issues = Check(text);

        // Assert: the findings follow the text
        Assert.Equal(["1:1 wrold", "1:13 speling", "3:1 xqzv"], Positions(issues));
    }

    /// <summary>
    ///     Test that an unsupported directive is merged into the findings in text order.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnsupportedDirectiveBetweenWords_IsInTextOrder()
    {
        // Arrange: a misspelling, an unsupported directive, and another misspelling
        const string text = "wrold\n// textlint:locale en-GB\nspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: the directive is reported between the two words
        Assert.Equal(
            [SpellingIssueKind.UnknownWord, SpellingIssueKind.UnsupportedDirective, SpellingIssueKind.UnknownWord],
            issues.Select(issue => issue.Kind));
    }

    /// <summary>
    ///     Test that an unsupported directive that precedes the words is reported before them.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnsupportedDirectiveBeforeWords_IsReportedFirst()
    {
        // Arrange: an unsupported directive on the first line and a misspelling after it
        const string text = "// textlint:locale en-GB\nwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the directive comes first and the word second
        Assert.Equal(["1:13 locale", "2:1 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable and enable pair hides the text between them.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DisableEnableBlock_SkipsRegion()
    {
        // Arrange: misspellings before, inside, and after a disabled region
        const string text = "wrold\n// textlint:disable\nspeling\n// textlint:enable\nxqzv";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the words outside the region are reported
        Assert.Equal(["1:1 wrold", "5:1 xqzv"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable directive without an enable hides the rest of the text.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnterminatedDisable_RunsToEndOfText()
    {
        // Arrange: a misspelling before a disable directive and two after it
        const string text = "wrold\n// textlint:disable\nspeling\nxqzv";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the word before the directive is reported
        Assert.Equal(["1:1 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable-line directive hides its whole line.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DisableLine_SkipsLine()
    {
        // Arrange: a misspelling before the directive on its line, and one on the next line
        const string text = "wrold // textlint:disable-line\nspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the next line is reported
        Assert.Equal(["2:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a disable-next-line directive hides the following line.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DisableNextLine_SkipsNextLine()
    {
        // Arrange: a directive, a hidden line, and a visible line
        const string text = "// textlint:disable-next-line\nwrold\nspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the line after the hidden one is reported
        Assert.Equal(["3:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a words directive accepts the listed words for the whole document, even before it.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordsDirective_AcceptsWordsInWholeDocument()
    {
        // Arrange: a misspelling before the directive that accepts it, and another misspelling after
        const string text = "wrold\n// textlint:words wrold\nspeling";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the word that is not listed is reported
        Assert.Equal(["3:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that an ignore directive accepts the listed words for the document.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreDirective_AcceptsListedWords()
    {
        // Arrange: a directive that lists two words separated by a comma, and a text that uses them
        const string text = "// textlint:ignore wrold, speling\nwrold speling xqzv";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the word that is not listed is reported
        Assert.Equal(["2:15 xqzv"], Positions(issues));
    }

    /// <summary>
    ///     Test that the words a directive adds do not leak into the next call.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DocumentWords_DoNotLeakToNextCall()
    {
        // Arrange: one checker, and a text whose directive accepts a misspelling
        var checker = new TextChecker(new TextLintSettings());
        var first = checker.Check("// textlint:words wrold\nwrold");

        // Act: check a text without the directive using the same checker
        var second = checker.Check(Unknown);

        // Assert: the first text is clean and the second still reports the word
        Assert.Multiple(
            () => Assert.Empty(first),
            () => Assert.Single(second));
    }

    /// <summary>
    ///     Test that directive prefixes are accepted in any letter case.
    /// </summary>
    [Fact]
    public void TextChecker_Check_DirectivePrefixInUpperCase_IsAccepted()
    {
        // Arrange: a disable-next-line directive written in capitals
        const string text = "// TEXTLINT:DISABLE-NEXT-LINE\nwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the next line is hidden
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the cspell directive prefix is accepted.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CspellPrefix_IsAccepted()
    {
        // Arrange: a directive with the cspell prefix, assembled so that this file's own spell check is not affected
        var text = "// " + "cspe" + "ll:disable-next-line\nwrold";

        // Act: check the text
        var issues = Check(text);

        // Assert: the next line is hidden
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that an unsupported directive is reported with its kind, name, and position.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnsupportedDirective_ReportsKindAndPosition()
    {
        // Arrange: a directive that cspell supports and the checker does not
        const string text = "textlint:locale en-GB";
        const int expectedColumn = 10;

        // Act: check the text
        var issue = Assert.Single(Check(text));

        // Assert: the finding names the directive where it is written
        Assert.Multiple(
            () => Assert.Equal(SpellingIssueKind.UnsupportedDirective, issue.Kind),
            () => Assert.Equal("locale", issue.Text),
            () => Assert.Equal(1, issue.Line),
            () => Assert.Equal(expectedColumn, issue.Column));
    }

    /// <summary>
    ///     Test that an unsupported directive inside a disabled region is not reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnsupportedDirectiveInDisabledRegion_IsNotReported()
    {
        // Arrange: an unsupported directive between disable and enable
        const string text = "// textlint:disable\ntextlint:locale en-GB\n// textlint:enable";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that URLs, email addresses, and hash values are not checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UrlEmailAndHash_AreNotChecked()
    {
        // Arrange: a text with a URL, an email address, a SHA-256 value, and one misspelling
        const string text =
            "see https://example.com/wrold and user@example.com 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08 then speling";

        // Act: check the text
        var issues = Check(text);

        // Assert: only the misspelling is reported
        Assert.Equal([OtherUnknown], Words(issues));
    }

    /// <summary>
    ///     Test that a UUID is not checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_Uuid_IsNotChecked()
    {
        // Arrange: a UUID between two ordinary words
        const string text = "hello 550e8400-e29b-41d4-a716-446655440000 world";

        // Act: check the text
        var issues = Check(text);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that text on both sides of an excluded range is still checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_TextAroundExcludedRange_IsStillChecked()
    {
        // Arrange: a URL between two misspellings
        const string text = "wrold https://example.com speling";

        // Act: check the text
        var issues = Check(text);

        // Assert: both misspellings are reported
        Assert.Equal(["1:1 wrold", "1:27 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a word that an excluded range cuts in two is checked as two separate segments.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordSplitByExcludedRange_IsCheckedAsSeparateSegments()
    {
        // Arrange: a URL that starts directly after the letters wrol, with no space between them
        const string text = "wrolhttp://example.com/x";

        // Act: check the text
        var issues = Check(text);

        // Assert: the letters before the URL are reported alone, not joined with the URL's letters
        Assert.Equal(["1:1 wrol"], Positions(issues));
    }

    /// <summary>
    ///     Test that a null settings value is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange: no settings

        // Act / Assert: creating a checker throws and names the parameter
        var exception = Assert.Throws<ArgumentNullException>(() => new TextChecker(null!));
        Assert.Equal("settings", exception.ParamName);
    }

    /// <summary>
    ///     Test that an unsupported language is rejected with an error that names it.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_UnsupportedLanguage_ThrowsNamingLanguage()
    {
        // Arrange: settings with a language that has no dictionary
        var settings = new TextLintSettings { Language = "klingon" };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the language
        Assert.Contains("klingon", exception.Message);
    }

    /// <summary>
    ///     Test that the language is reported before any word-list entry.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_UnsupportedLanguageAndBadEntry_ReportsLanguage()
    {
        // Arrange: settings with both a bad language and a bad word-list entry
        var settings = new TextLintSettings { Language = "klingon", Words = [string.Empty] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the language is the problem that is reported
        Assert.Contains("klingon", exception.Message);
    }

    /// <summary>
    ///     Test that a blank word-list entry is rejected with an error that names the setting.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_BlankEntry_ThrowsNamingSetting()
    {
        // Arrange: a words list with an empty entry
        var settings = new TextLintSettings { Words = [string.Empty] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting
        Assert.Contains("'words'", exception.Message);
    }

    /// <summary>
    ///     Test that a white-space entry is rejected with an error that names the setting and the entry.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_WhiteSpaceEntry_ThrowsNamingSettingAndEntry()
    {
        // Arrange: an ignoreWords entry that holds two words
        var settings = new TextLintSettings { IgnoreWords = ["two words"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting and the entry
        Assert.Multiple(
            () => Assert.Contains("'ignoreWords'", exception.Message),
            () => Assert.Contains("'two words'", exception.Message));
    }

    /// <summary>
    ///     Test that an entry that begins with the forbidden-word marker is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_ForbiddenMarkerEntry_ThrowsNamingSettingAndEntry()
    {
        // Arrange: a words entry in the cspell forbidden-word form
        var settings = new TextLintSettings { Words = ["!wrold"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting and the entry
        Assert.Multiple(
            () => Assert.Contains("'words'", exception.Message),
            () => Assert.Contains("'!wrold'", exception.Message));
    }

    /// <summary>
    ///     Test that an entry with a character that cannot be part of a word is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_DisallowedCharacterEntry_ThrowsNamingSettingAndEntry()
    {
        // Arrange: a flagWords entry with an at sign inside it
        var settings = new TextLintSettings { FlagWords = ["wro@ld"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting and the entry
        Assert.Multiple(
            () => Assert.Contains("'flagWords'", exception.Message),
            () => Assert.Contains("'wro@ld'", exception.Message));
    }

    /// <summary>
    ///     Test that an entry with no letter or digit is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_EntryWithoutLetterOrDigit_ThrowsNamingSettingAndEntry()
    {
        // Arrange: a words entry made only of hyphens
        var settings = new TextLintSettings { Words = ["---"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting and the entry
        Assert.Multiple(
            () => Assert.Contains("'words'", exception.Message),
            () => Assert.Contains("'---'", exception.Message));
    }

    /// <summary>
    ///     Test that a flag entry with suggestions but no word is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_FlagEntryWithoutWord_ThrowsNamingSetting()
    {
        // Arrange: a flag entry that begins with the arrow
        var settings = new TextLintSettings { FlagWords = ["->main"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting
        Assert.Contains("'flagWords'", exception.Message);
    }

    /// <summary>
    ///     Test that the suggestion form is not accepted in the words setting.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_SuggestionFormInWords_ThrowsNamingSetting()
    {
        // Arrange: a words entry written like a flag entry with a suggestion
        var settings = new TextLintSettings { Words = ["old->new"] };

        // Act: create a checker
        var exception = Assert.Throws<ArgumentException>(() => new TextChecker(settings));

        // Assert: the message names the setting
        Assert.Contains("'words'", exception.Message);
    }

    /// <summary>
    ///     Test that one checker gives identical results to many threads that use it at the same time.
    /// </summary>
    [Fact]
    public async Task TextChecker_Check_ConcurrentCalls_GiveIdenticalResults()
    {
        // Arrange: one checker, a text that exercises flags, directive words, and an unsupported directive
        const string text = "wrold speling\nHello world\n// textlint:words zorb\nzorb zorbs xqzv\n// textlint:locale en\nobsolete";
        var checker = new TextChecker(new TextLintSettings { FlagWords = ["obsolete"] });
        var expected = Format(checker.Check(text, "a.md"));
        using var start = new ManualResetEventSlim(false);

        // Act: start the threads together so that their calls overlap, and collect every result
        var tasks = Enumerable.Range(0, ConcurrentThreads)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.Wait();
                    return Enumerable.Range(0, CallsPerThread).Select(_ => Format(checker.Check(text, "a.md"))).ToList();
                },
                TaskCreationOptions.LongRunning))
            .ToList();
        start.Set();
        var results = await Task.WhenAll(tasks);

        // Assert: every call of every thread returned exactly what the sequential call returned
        Assert.All(results.SelectMany(result => result), result => Assert.Equal(expected, result));
    }

    /// <summary>
    ///     Test that a finding exposes its file name, position, text, and kind.
    /// </summary>
    [Fact]
    public void TextChecker_Check_Issue_ExposesAllProperties()
    {
        // Arrange: a flagged word with a suggestion, checked with a source name
        var settings = new TextLintSettings { FlagWords = ["master->main"] };

        // Act: check the text
        var issue = Assert.Single(Check("the master", settings, "docs/a.md"));

        // Assert: every property of the finding is set
        Assert.Multiple(
            () => Assert.Equal("docs/a.md", issue.FilePath),
            () => Assert.Equal(1, issue.Line),
            () => Assert.Equal(5, issue.Column),
            () => Assert.Equal("master", issue.Text),
            () => Assert.Equal(SpellingIssueKind.FlaggedWord, issue.Kind),
            () => Assert.Equal(["main"], issue.Suggestions));
    }

    /// <summary>
    ///     Test that a finding with a file name is formatted in the compiler style.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IssueWithFileName_FormatsCompilerStyle()
    {
        // Arrange: an unknown word checked with a source name
        var issue = Assert.Single(Check("hello wrold", sourceName: "docs/a.md"));

        // Act: format the finding
        var formatted = issue.ToString();

        // Assert: the format is file(line,column): error: message
        Assert.Equal("docs/a.md(1,7): error: Unknown word 'wrold'", formatted);
    }

    /// <summary>
    ///     Test that a finding without a file name is formatted with the position only.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IssueWithoutFileName_FormatsPositionOnly()
    {
        // Arrange: an unknown word checked without a source name
        var issue = Assert.Single(Check("hello wrold"));

        // Act: format the finding
        var formatted = issue.ToString();

        // Assert: the location is just the position
        Assert.Equal("(1,7): error: Unknown word 'wrold'", formatted);
    }

    /// <summary>
    ///     Test that a flagged finding includes its suggestions in the message.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlaggedIssueWithSuggestions_FormatsSuggestions()
    {
        // Arrange: a flagged word with two suggestions
        var settings = new TextLintSettings { FlagWords = ["master->main, primary"] };
        var issue = Assert.Single(Check("the master", settings, "a.md"));

        // Act: format the finding
        var formatted = issue.ToString();

        // Assert: the message names the kind and lists the suggestions
        Assert.Equal("a.md(1,5): error: Flagged word 'master'; suggestions: main, primary", formatted);
    }

    /// <summary>
    ///     Test that an unsupported-directive finding is formatted with its own message.
    /// </summary>
    [Fact]
    public void TextChecker_Check_UnsupportedDirectiveIssue_FormatsDirectiveMessage()
    {
        // Arrange: an unsupported directive
        var issue = Assert.Single(Check("textlint:locale en-GB", sourceName: "a.md"));

        // Act: format the finding
        var formatted = issue.ToString();

        // Assert: the message names the directive
        Assert.Equal("a.md(1,10): error: The directive 'locale' is not supported", formatted);
    }

    /// <summary>
    ///     Test that the suggestions of a finding cannot be changed by casting them to a mutable interface.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IssueSuggestions_AreImmutable()
    {
        // Arrange: a flagged finding with a suggestion
        var settings = new TextLintSettings { FlagWords = ["master->main"] };
        var issue = Assert.Single(Check("the master", settings));

        // Act / Assert: the suggestions are not a mutable collection
        var suggestions = Assert.IsAssignableFrom<IList<string>>(issue.Suggestions);
        Assert.Throws<NotSupportedException>(() => suggestions.Add("other"));
    }

    /// <summary>
    ///     Formats findings as one string so that two results can be compared exactly.
    /// </summary>
    /// <param name="issues">The findings to format.</param>
    /// <returns>The formatted findings, one per line.</returns>
    private static string Format(IReadOnlyList<SpellingIssue> issues) =>
        string.Join('\n', issues.Select(issue => $"{issue.ToString()}|{string.Join(',', issue.Suggestions)}"));
}
