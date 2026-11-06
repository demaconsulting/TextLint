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
// cspell:ignore quix flib cabdab fedacb xqzvw zorbquix zorbflag zorbstart zorbend neverpresent zorbinside quixone zorbq
// cspell:ignore Dowd Artagnan quixwords nflibquix Urlz zorbcomment zorbinblockxx zorbinline zorbnamed zorbnamedxx
// cspell:ignore zorbonexx zorbthreexx zorbtwoxx zorbxx zorbyy

using System.Text.RegularExpressions;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.TextScanning;

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
    ///     Test that a finding can be formatted under a file name chosen by the caller.
    /// </summary>
    [Fact]
    public void TextChecker_Format_CallerFileName_ReplacesRecordedFileName()
    {
        // Arrange: a finding recorded under a relative name, and an absolute name to show instead
        var issue = Assert.Single(Check("hello wrold", sourceName: "docs/a.md"));
        var shown = Path.GetFullPath("docs/a.md");

        // Act: format the finding under the caller's name, and with no name at all
        var formatted = issue.Format(shown);
        var withoutName = issue.Format(null);

        // Assert: only the file name changes; the position and message stay in the compiler style
        Assert.Multiple(
            () => Assert.Equal($"{shown}(1,7): error: Unknown word 'wrold'", formatted),
            () => Assert.Equal("(1,7): error: Unknown word 'wrold'", withoutName));
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
    ///     Test that text matched by an expression of <c>ignoreRegExpList</c> is not checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpListExpression_SkipsMatches()
    {
        // Arrange: settings that ignore zorb followed by digits, and a text with a match and a near miss
        var settings = new TextLintSettings { IgnoreRegExpList = ["/zorb\\d+/"] };

        // Act: check the text
        var issues = Check("zorb12 zorbq", settings);

        // Assert: only the near miss is reported, at its position
        Assert.Equal(["1:8 zorbq"], Positions(issues));
    }

    /// <summary>
    ///     Test that a named pattern and a predefined pattern are applied when the list names them.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpListNames_ApplyNamedAndPredefinedPatterns()
    {
        // Arrange: settings that name a user pattern and the predefined string pattern
        var settings = new TextLintSettings
        {
            IgnoreRegExpList = ["ZorbWord", "string"],
            Patterns = [new NamedPattern("ZorbWord", "/quix\\w+/")],
        };

        // Act: check a text with a match of each and an unmatched word
        var issues = Check("quixone \"zorbinside\" wrold", settings);

        // Assert: only the unmatched word is reported
        Assert.Equal(["1:22 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a pattern is applied to the whole text, so it can span lines and cut a word in two.
    /// </summary>
    [Fact]
    public void TextChecker_Check_PatternSpanningLinesAndCuttingAWord_ExcludesOnlyTheMatch()
    {
        // Arrange: a pattern that matches from one marker to the next across lines, and one that cuts a word
        var settings = new TextLintSettings { IgnoreRegExpList = ["/zorbstart[\\s\\S]*?zorbend/", "/flib/"] };

        // Act: check a text with a block across lines and a word that the second pattern cuts in two
        var issues = Check("zorbstart one\ntwo zorbend wrold\nflibquix", settings);

        // Assert: the block is skipped, and the rest of the cut word is checked as its own word
        Assert.Equal(["2:13 wrold", "3:5 quix"], Positions(issues));
    }

    /// <summary>
    ///     Test that a pattern that can match the empty string excludes nothing by doing so.
    /// </summary>
    [Fact]
    public void TextChecker_Check_PatternMatchingEmptyText_ExcludesOnlyRealMatches()
    {
        // Arrange: a pattern that matches any run of z, including none
        var settings = new TextLintSettings { IgnoreRegExpList = ["/z*/"] };

        // Act: check a text in which the run of z is part of a word
        var issues = Check("wrold zzzz wrold", settings);

        // Assert: both words are still reported, and the run of z is skipped
        Assert.Equal(["1:1 wrold", "1:12 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that the default patterns still apply when custom patterns are set.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CustomPatterns_AreAddedToTheDefaultPatterns()
    {
        // Arrange: settings with a custom pattern, and a text with a URL, a custom match, and an unknown word
        var settings = new TextLintSettings { IgnoreRegExpList = ["/zorb/"] };

        // Act: check the text
        var issues = Check("https://wrold.example/speling zorb wrold", settings);

        // Assert: the URL and the custom match are skipped
        Assert.Equal(["1:36 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a pattern that does not match anything changes nothing.
    /// </summary>
    [Fact]
    public void TextChecker_Check_PatternWithoutMatch_ChangesNothing()
    {
        // Arrange: a pattern that matches nothing in the text
        var settings = new TextLintSettings { IgnoreRegExpList = ["/neverpresent/"] };

        // Act: check the text with and without the pattern
        var with = Check("wrold speling", settings);
        var without = Check("wrold speling");

        // Assert: the findings are identical
        Assert.Equal(Positions(without), Positions(with));
    }

    /// <summary>
    ///     Test that an unusable pattern in settings built in code is rejected by the constructor.
    /// </summary>
    /// <param name="entry">An entry of <c>ignoreRegExpList</c> that cannot be used.</param>
    [Theory]
    [InlineData("/(/")]
    [InlineData("/zorb/x")]
    [InlineData("/\\z/")]
    [InlineData("Urlz")]
    [InlineData("")]
    public void TextChecker_Constructor_InvalidIgnoreRegExpListEntry_ThrowsInvalidPatternException(string entry)
    {
        // Arrange: settings with the entry from the theory data
        var settings = new TextLintSettings { IgnoreRegExpList = [entry] };

        // Act: create the checker
        var exception = Assert.Throws<InvalidPatternException>(() => new TextChecker(settings));

        // Assert: the exception names the entry, and is an argument exception
        Assert.Multiple(
            () => Assert.Equal(entry, exception.Pattern),
            () => Assert.IsAssignableFrom<ArgumentException>(exception));
    }

    /// <summary>
    ///     Test that unusable named patterns in settings built in code are rejected by the constructor.
    /// </summary>
    [Fact]
    public void TextChecker_Constructor_InvalidNamedPatterns_ThrowInvalidPatternException()
    {
        // Arrange: a predefined name, a repeated name, and an invalid expression
        var predefined = new TextLintSettings { Patterns = [new NamedPattern("Urls", "/zorb/")] };
        var repeated = new TextLintSettings { Patterns = [new NamedPattern("Zorb", "/a/"), new NamedPattern("zorb", "/b/")] };
        var invalid = new TextLintSettings { Patterns = [new NamedPattern("Zorb", "/(/")] };

        // Act / Assert: each is rejected with the documented exception
        Assert.Multiple(
            () => Assert.Throws<InvalidPatternException>(() => new TextChecker(predefined)),
            () => Assert.Throws<InvalidPatternException>(() => new TextChecker(repeated)),
            () => Assert.Throws<InvalidPatternException>(() => new TextChecker(invalid)));
    }

    /// <summary>
    ///     Test that a pattern that backtracks without end ends with a time-out error instead of hanging.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CatastrophicCustomPattern_ThrowsRegexMatchTimeoutException()
    {
        // Arrange: a nested quantifier that backtracks exponentially, and a text that almost matches it; the checker
        // is made with the internal short limit so that the test is fast, and the production limit is tested elsewhere
        var settings = new TextLintSettings { IgnoreRegExpList = ["/^(a|aa)+$/"] };
        var checker = new TextChecker(settings, TimeSpan.FromMilliseconds(100));
        var text = new string('a', 60) + "b";

        // Act / Assert: the check gives up at the time limit with the documented exception
        Assert.Throws<RegexMatchTimeoutException>(() => checker.Check(text));
    }

    /// <summary>
    ///     Test that the checker made for production has the five second limit, and that a limit that is not positive is rejected.
    /// </summary>
    [Fact]
    public void TextChecker_New_MatchTimeout_IsFiveSecondsAndMustBePositive()
    {
        // Arrange: settings with a custom pattern
        var settings = new TextLintSettings { IgnoreRegExpList = ["/zorb/"], Patterns = [new NamedPattern("Zorb", "/zorb/")] };

        // Act: make the checker the public way, and with limits that are not allowed
        var production = new TextChecker(settings);

        // Assert: the default is five seconds, and a limit of zero or less is rejected
        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromSeconds(5), production.MatchTimeout),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new TextChecker(settings, TimeSpan.Zero)),
            () => Assert.Throws<ArgumentNullException>(() => new TextChecker(null!, TimeSpan.FromSeconds(1))));
    }

    /// <summary>
    ///     Test that a pattern written in an ignoreRegExp directive hides its matches in the whole text, before and after the directive.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirective_HidesMatchesInTheWholeText()
    {
        // Arrange: a text with the invented word zorbxx before and zorbyy after a directive that hides such words
        var text = $"{Unknown} zorbxx\n// cspell:ignoreRegExp /zorb\\w+/\n{OtherUnknown} zorbyy";

        // Act: check it
        var issues = Check(text);

        // Assert: both invented words are hidden, wherever they are, and the other words are reported
        Assert.Equal(["1:1 wrold", "3:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a directive pattern is read with the flags of the way it is written: literals use only their own flags
    ///     and bare expressions are case-insensitive.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveForms_UseTheFlagsAsWritten()
    {
        // Arrange: a case-sensitive literal, a literal with the case flag, a bare word, and the words they should hide
        var text = string.Join(
            '\n',
            "// cspell:ignoreRegExp /zorbone\\w+/",
            "// cspell:ignoreRegExp /zorbtwo\\w+/i",
            "// cspell:ignoreRegExp zorbthree\\w+",
            "zorbonexx ZORBONEXX zorbtwoxx ZORBTWOXX zorbthreexx ZORBTHREEXX");

        // Act: check it
        var issues = Check(text);

        // Assert: only the capitals that the literal without the flag cannot match are reported
        Assert.Equal(["4:11 ZORBONEXX"], Positions(issues));
    }

    /// <summary>
    ///     Test that a directive can name a pattern of the settings or a predefined pattern, with the same names as the settings use.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveNames_RefersToSettingsAndPredefinedPatterns()
    {
        // Arrange: a settings pattern and a predefined one, each named in a directive, in a different letter case
        var settings = new TextLintSettings { Patterns = [new NamedPattern("ZorbNamed", "/zorbnamed\\w+/")] };
        var text = string.Join(
            '\n',
            "// cspell:ignoreRegExp zorbnamed",
            "// cspell:ignoreRegExp CStyleComment",
            "zorbnamedxx " + Unknown + " // zorbcomment");

        // Act: check it
        var issues = Check(text, settings);

        // Assert: the name of the settings pattern is the same word as the text only by letter case, so it is compared
        // without regard to case and hides the word; the comment pattern hides the comment; the rest is reported
        Assert.Equal(["3:13 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a directive pattern that cannot be used is reported at its position and has no other effect.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveInvalid_IsReportedAtItsPosition()
    {
        // Arrange: a directive with a pattern that .NET cannot read the JavaScript way, then a word it would have hidden
        const string text = "// cspell:ignoreRegExp /(?i)zorbinline/\nzorbinline";

        // Act: check it
        var issues = Check(text, sourceName: "notes.txt");

        // Assert: the pattern is reported where it is written, with the reason, and the word is still unknown
        var invalid = issues[0];
        Assert.Multiple(
            () => Assert.Equal(["1:24 /(?i)zorbinline/", "2:1 zorbinline"], Positions(issues)),
            () => Assert.Equal(SpellingIssueKind.InvalidDirective, invalid.Kind),
            () => Assert.Equal("the group syntax '(?i)' is not supported.", invalid.Detail),
            () => Assert.Equal(
                "notes.txt(1,24): error: Cannot use the pattern '/(?i)zorbinline/' of the directive 'ignoreRegExp': " +
                "the group syntax '(?i)' is not supported.",
                invalid.ToString()),
            () => Assert.Null(issues[1].Detail));
    }

    /// <summary>
    ///     Test that a directive that names a pattern that nothing defines is reported, where cspell would read the name as text.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveUnknownName_IsReportedAndHidesNothing()
    {
        // Arrange: a directive with a misspelled predefined name, and the word it spells
        const string text = "// cspell:ignoreRegExp Urlz\nurlz";

        // Act: check it
        var issues = Check(text);

        // Assert: the name is reported with the advice to use slashes, and the word is not hidden
        Assert.Multiple(
            () => Assert.Equal(["1:24 Urlz", "2:1 urlz"], Positions(issues)),
            () => Assert.Contains("is not a defined or predefined pattern name", issues[0].Detail, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a directive inside a disabled block still adds its pattern, and that an unusable one there is not reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveInDisabledBlock_AppliesButIsNotReported()
    {
        // Arrange: a valid and an invalid directive inside a disabled block, and words after the block
        var text = string.Join(
            '\n',
            "// cspell:disable",
            "// cspell:ignoreRegExp /zorbinblock\\w+/",
            "// cspell:ignoreRegExp /(?i)bad/",
            "// cspell:enable",
            "zorbinblockxx " + Unknown);

        // Act: check it
        var issues = Check(text);

        // Assert: the pattern hides its word after the block, and nothing is reported for the invalid one
        Assert.Equal(["5:15 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that the same unusable pattern written twice is reported at each place, in text order with the other findings.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveRepeated_IsReportedAtEachPlaceInOrder()
    {
        // Arrange: an unusable pattern twice, with a misspelling between and after
        var text = string.Join('\n', "// cspell:ignoreRegExp /(/", Unknown, "// cspell:ignoreRegExp /(/", OtherUnknown);

        // Act: check it
        var issues = Check(text);

        // Assert: the four findings are in text order, whatever order they were found in
        Assert.Equal(["1:24 /(/", "2:1 wrold", "3:24 /(/", "4:1 speling"], Positions(issues));
    }

    /// <summary>
    ///     Test that a directive with no pattern on its line is reported as an unsupported directive.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectiveWithoutPattern_IsReportedAsUnsupported()
    {
        // Arrange: a directive whose pattern is on the next line, which is not read
        const string text = "// cspell:ignoreRegExp\n/hello/";

        // Act: check it
        var issues = Check(text);

        // Assert: the directive name is reported
        var issue = Assert.Single(issues);
        Assert.Multiple(
            () => Assert.Equal(SpellingIssueKind.UnsupportedDirective, issue.Kind),
            () => Assert.Equal("ignoreRegExp", issue.Text));
    }

    /// <summary>
    ///     Test that a text may use only a limited number of different directive patterns, and that repeats do not count.
    /// </summary>
    [Fact]
    public void TextChecker_Check_IgnoreRegExpDirectivePatternLimit_RejectsTheExcessWithoutReadingIt()
    {
        // Arrange: one more different pattern than the limit, each repeated, and a word that the last pattern would hide;
        // the words are made of letters only, because digits would end a word
        var limit = TextChecker.MaxDirectivePatterns;
        static string Word(int index) => $"zorb{(char)('a' + (index / 26))}{(char)('a' + (index % 26))}qq";
        var lines = Enumerable.Range(0, limit + 1)
            .SelectMany(index => new[] { $"// cspell:ignoreRegExp /{Word(index)}/", $"// cspell:ignoreRegExp /{Word(index)}/" })
            .Append(Word(limit))
            .ToArray();

        // Act: check the text
        var issues = Check(string.Join('\n', lines));

        // Assert: the pattern over the limit is reported at both of its places, and the word it would have hidden is unknown
        var reasons = issues.Where(issue => issue.Kind == SpellingIssueKind.InvalidDirective).ToArray();
        Assert.Multiple(
            () => Assert.Equal(2, reasons.Length),
            () => Assert.All(reasons, issue => Assert.Contains($"at most {limit} different", issue.Detail, StringComparison.Ordinal)),
            () => Assert.Equal(Word(limit), issues[^1].Text));
    }

    /// <summary>
    ///     Test that a directive pattern that backtracks without end ends with a time-out error instead of hanging.
    /// </summary>
    [Fact]
    public void TextChecker_Check_CatastrophicDirectivePattern_ThrowsRegexMatchTimeoutException()
    {
        // Arrange: a directive with a nested quantifier that backtracks exponentially, and a line that almost matches it;
        // the checker has the internal short limit so that the test is fast
        var checker = new TextChecker(new TextLintSettings(), TimeSpan.FromMilliseconds(100));
        var text = "// cspell:ignoreRegExp ^(a|aa)+$\n" + new string('a', 60) + "b";

        // Act / Assert: the check gives up at the time limit with the documented exception
        Assert.Throws<RegexMatchTimeoutException>(() => checker.Check(text));
    }

    /// <summary>
    ///     Test that the Markdown link, label, anchor, and entity patterns apply to Markdown files only.
    /// </summary>
    [Fact]
    public void TextChecker_Check_MarkdownFile_SkipsLinkTargetsLabelsAnchorsAndEntities()
    {
        // Arrange: a Markdown line with a link, a reference, an anchor, an entity, and an unknown word
        const string text = "[a](wrold) [b][speling] <a id=\"xqzvw\"></a> &wrold; hello wrold";

        // Act: check it as a Markdown file and as a text file
        var markdown = Check(text, sourceName: "docs/guide.MD");
        var plain = Check(text, sourceName: "docs/guide.txt");

        // Assert: in Markdown only the last word is reported; in a text file everything is
        Assert.Multiple(
            () => Assert.Equal(["1:58 wrold"], Positions(markdown)),
            () => Assert.True(plain.Count > markdown.Count));
    }

    /// <summary>
    ///     Test that a source without a name is not treated as Markdown.
    /// </summary>
    [Fact]
    public void TextChecker_Check_NoSourceName_DoesNotApplyMarkdownPatterns()
    {
        // Arrange: a Markdown link with an unknown target
        const string text = "[a](wrold)";

        // Act: check it without a source name
        var issues = Check(text);

        // Assert: the target is reported
        Assert.Equal(["1:5 wrold"], Positions(issues));
    }

    /// <summary>
    ///     Test that a long token that looks random is skipped by default.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LongRandomLookingToken_IsSkipped()
    {
        // Arrange: a 46-character token made of invented words, digits, and hyphens, which scores above one half
        const string token = "zorb-1-quix-2-flib-3-zorb-4-quix-5-flib-6-zorb";

        // Act: check it with the default settings
        var issues = Check(token);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that the token is checked when random strings are not ignored, or it is shorter than the minimum length.
    /// </summary>
    [Fact]
    public void TextChecker_Check_RandomLookingTokenWhenNotSkipped_IsCheckedAsWords()
    {
        // Arrange: the same token, with the skipping switched off, with a minimum above its length, and at its length
        const string token = "zorb-1-quix-2-flib-3-zorb-4-quix-5-flib-6-zorb";
        var off = new TextLintSettings { IgnoreRandomStrings = false };
        var above = new TextLintSettings { MinRandomLength = token.Length + 1 };
        var exact = new TextLintSettings { MinRandomLength = token.Length };

        // Act: check it each way
        var checkedOff = Check(token, off);
        var checkedAbove = Check(token, above);
        var checkedExact = Check(token, exact);

        // Assert: it is checked when off and when too short for the minimum, and skipped at exactly the minimum
        Assert.Multiple(
            () => Assert.Equal(["1:1 zorb", "1:8 quix", "1:15 flib"], Positions(checkedOff).Take(3)),
            () => Assert.Equal(Positions(checkedOff), Positions(checkedAbove)),
            () => Assert.Empty(checkedExact));
    }

    /// <summary>
    ///     Test that a long identifier of words is still checked.
    /// </summary>
    [Fact]
    public void TextChecker_Check_LongIdentifierOfWords_IsStillChecked()
    {
        // Arrange: a 48-character identifier made of three invented words, which does not look random
        const string identifier = "ZorbQuixFlibZorbQuixFlibZorbQuixFlibZorbQuixFlib";

        // Act: check it
        var issues = Check(identifier);

        // Assert: its pieces are reported
        Assert.Equal(12, issues.Count);
    }

    /// <summary>
    ///     Test that a token that looks random hides a flagged word inside it, as in cspell.
    /// </summary>
    [Fact]
    public void TextChecker_Check_FlaggedWordInsideRandomLookingToken_IsNotReported()
    {
        // Arrange: a random-looking token that holds a flagged word
        var settings = new TextLintSettings { FlagWords = ["zorbflag"] };

        // Act: check the token and the flagged word alone
        var inside = Check("zorbflag-1-quix-2-flib-3-zorb-4-quix-5-flib-6-zorb", settings);
        var alone = Check("zorbflag", settings);

        // Assert: the flagged word is reported alone, and not inside the random-looking token
        Assert.Multiple(
            () => Assert.Empty(inside),
            () => Assert.Single(alone));
    }

    /// <summary>
    ///     Test that unknown words inside a hexadecimal sequence of a token are not reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_WordsInsideHexSequence_AreNotReported()
    {
        // Arrange: a word before a hexadecimal sequence made of two unknown hexadecimal words and a number
        const string token = "zorbquix-cabdab-0123-fedacb";

        // Act: check the token with the default settings and with random strings not ignored
        var skipped = Check(token);
        var reported = Check(token, new TextLintSettings { IgnoreRandomStrings = false });

        // Assert: only the word before the sequence is reported by default, and all three when not ignoring
        Assert.Multiple(
            () => Assert.Equal(["1:1 zorbquix"], Positions(skipped)),
            () => Assert.Equal(["1:1 zorbquix", "1:10 cabdab", "1:22 fedacb"], Positions(reported)));
    }

    /// <summary>
    ///     Test that a hexadecimal sequence in mixed letter case, or a flagged word inside one, does not hide words.
    /// </summary>
    [Fact]
    public void TextChecker_Check_MixedCaseHexSequenceOrFlaggedWord_StaysReported()
    {
        // Arrange: a sequence in mixed case, and a flagged word inside a sequence
        var flagged = new TextLintSettings { FlagWords = ["cabdab"] };

        // Act: check both
        var mixed = Check("Cabdab-0123-fedacb");
        var flaggedInside = Check("cabdab-0123-fedacb", flagged);

        // Assert: the mixed-case sequence hides nothing, and the flagged word is still reported
        Assert.Multiple(
            () => Assert.Equal(["1:1 Cabdab", "1:13 fedacb"], Positions(mixed)),
            () => Assert.Equal([SpellingIssueKind.FlaggedWord], flaggedInside.Select(issue => issue.Kind)));
    }

    /// <summary>
    ///     Test that a contraction made of acceptable parts is accepted, as in cspell.
    /// </summary>
    /// <param name="text">A contraction whose parts are all known or too short to check.</param>
    /// <param name="words">The invented words that the settings accept so that the contraction has known parts.</param>
    [Theory]
    [InlineData("exec'd")]
    [InlineData("OR'd")]
    [InlineData("typo'd")]
    [InlineData("rock'n'roll")]
    [InlineData("she'd've")]
    [InlineData("dd'T'HH")]
    [InlineData("hello'z")]
    [InlineData("ZORB'd", "zorb")]
    [InlineData("zorb\u2019d", "zorb")]
    [InlineData("zorb\\'d", "zorb")]
    [InlineData("d'zorb", "zorb")]
    [InlineData("zorb'quix", "zorb", "quix")]
    [InlineData("zorbQuix'd", "zorb", "quix")]
    public void TextChecker_Check_ContractionOfAcceptableParts_IsAccepted(string text, params string[] words)
    {
        // Arrange: settings that accept any invented words the contraction needs
        var settings = new TextLintSettings { Words = words };

        // Act: check the contraction
        var issues = Check(text, settings);

        // Assert: nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Test that a contraction with an unknown part of the minimum length or more is reported whole.
    /// </summary>
    /// <param name="text">A contraction with an unknown part.</param>
    /// <param name="expected">The finding that must result.</param>
    [Theory]
    [InlineData("D'Artagnan", "1:1 D'Artagnan")]
    [InlineData("O'Dowd", "1:1 O'Dowd")]
    [InlineData("wrold'd", "1:1 wrold'd")]
    [InlineData("hello'wrold", "1:1 hello'wrold")]
    [InlineData("wrold's", "1:1 wrold's")]
    [InlineData("hello'xqzvw", "1:1 hello'xqzvw")]
    [InlineData("ZORB'd", "1:1 ZORB'd")]
    public void TextChecker_Check_ContractionWithUnknownPart_IsReportedWhole(string text, string expected)
    {
        // Arrange: the contraction is passed in by the theory data
        // Act: check it with the default settings
        var issues = Check(text);

        // Assert: it is reported as one word, with its apostrophe
        Assert.Equal([expected], Positions(issues));
    }

    /// <summary>
    ///     Test that a flagged part, and a part that is long enough once the minimum length is lowered, keep the word reported.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ContractionWithFlaggedOrCheckedPart_IsReported()
    {
        // Arrange: a contraction whose second part is flagged, and one whose short part is checked at a low minimum
        var flagged = new TextLintSettings { FlagWords = ["wrold"], Words = ["zorb"] };
        var low = new TextLintSettings { MinWordLength = 2, Words = ["zorb"] };

        // Act: check both
        var flaggedIssues = Check("zorb'wrold zorb'", flagged);
        var lowIssues = Check("zorb'xq", low);

        // Assert: the flagged part keeps the word reported, and with a minimum of 2 the part xq is checked
        Assert.Multiple(
            () => Assert.Equal(["1:1 zorb'wrold"], Positions(flaggedIssues)),
            () => Assert.Equal(["1:1 zorb'xq"], Positions(lowIssues)));
    }

    /// <summary>
    ///     Test that the accepted parts of a contraction that follows a backslash use the escape rule for the first part.
    /// </summary>
    [Fact]
    public void TextChecker_Check_ContractionAfterBackslash_RetriesWithoutEscapeLetter()
    {
        // Arrange: a contraction whose first part is a known word with an escape letter in front of it
        const string text = "\\nhello'd";

        // Act: check it
        var issues = Check(text);

        // Assert: the first part is accepted without the n of the escape, so nothing is reported
        Assert.Empty(issues);
    }

    /// <summary>
    ///     Formats findings as one string so that two results can be compared exactly.
    /// </summary>
    /// <param name="issues">The findings to format.</param>
    /// <returns>The formatted findings, one per line.</returns>
    private static string Format(IReadOnlyList<SpellingIssue> issues) =>
        string.Join('\n', issues.Select(issue => $"{issue.ToString()}|{string.Join(',', issue.Suggestions)}"));
}
