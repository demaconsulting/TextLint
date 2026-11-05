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

using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="WordTokenizer"/> class and the <see cref="TextRange"/> and
///     <see cref="ListEntry"/> values it produces.
/// </summary>
/// <remarks>
///     The tokenizer is pure logic over strings and depends on nothing but the .NET base class library,
///     so these tests use the real class with no test doubles. Characters outside ASCII are written as
///     escape sequences so that the expected values are unambiguous in the source file.
/// </remarks>
public class WordTokenizerTests
{
    /// <summary>
    ///     The separator used to join expected and actual items into one comparable string.
    /// </summary>
    private const string Separator = "|";

    /// <summary>
    ///     The number of UTF-16 code units in the emoji written as a surrogate pair in these tests.
    /// </summary>
    private const int EmojiUnits = 2;

    /// <summary>
    ///     The emoji (a character outside the basic multilingual plane) used to test surrogate pairs.
    /// </summary>
    private const string Emoji = "\U0001F600";

    /// <summary>
    ///     A mathematical bold capital letter A, a letter outside the basic multilingual plane.
    /// </summary>
    private const string AstralCapital = "\U0001D400";

    /// <summary>
    ///     The size of the very large inputs, in characters (one megabyte of text, counted as 1,048,576 characters).
    /// </summary>
    private const int OneMegabyte = 1024 * 1024;

    /// <summary>
    ///     The reason reported for a blank list entry.
    /// </summary>
    private const string BlankProblem = "it is blank.";

    /// <summary>
    ///     The reason reported for a list entry that contains white space.
    /// </summary>
    private const string WhiteSpaceProblem = "it contains white space, so it can never match a word; list each word separately.";

    /// <summary>
    ///     The reason reported for a list entry that starts with the forbidden-word marker.
    /// </summary>
    private const string ForbiddenProblem = "the forbidden-word marker '!' is not supported; list the word in flagWords instead.";

    /// <summary>
    ///     The reason reported for a list entry that has no letter or digit.
    /// </summary>
    private const string NoLetterOrDigitProblem = "it has no letter or digit, so it can never match.";

    /// <summary>
    ///     The reason reported for a suggestion entry that has no word before the suggestions.
    /// </summary>
    private const string NoWordProblem = "there is no word before its suggestions.";

    /// <summary>
    ///     Finds the ranges of the words in a whole text.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The ranges of the words in order.</returns>
    private static List<TextRange> FindWordRanges(string text)
    {
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, new TextRange(0, text.Length), words);
        return words;
    }

    /// <summary>
    ///     Finds the words in a whole text and returns their text.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The text of each word in order.</returns>
    private static string[] Words(string text) =>
        [.. FindWordRanges(text).Select(range => text.Substring(range.Start, range.Length))];

    /// <summary>
    ///     Finds the tokens in a whole text and returns their text.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The text of each token in order.</returns>
    private static string[] Tokens(string text)
    {
        var tokens = new List<TextRange>();
        WordTokenizer.FindTokens(text, 0, text.Length, tokens);
        return [.. tokens.Select(range => text.Substring(range.Start, range.Length))];
    }

    /// <summary>
    ///     Splits a whole word at case changes and returns the text of the pieces.
    /// </summary>
    /// <param name="word">The word to split.</param>
    /// <returns>The text of each piece in order.</returns>
    private static string[] Pieces(string word)
    {
        var pieces = new List<TextRange>();
        WordTokenizer.SplitCase(word, new TextRange(0, word.Length), pieces);
        return [.. pieces.Select(range => word.Substring(range.Start, range.Length))];
    }

    /// <summary>
    ///     Trims the edge punctuation off a whole token and returns the remaining text.
    /// </summary>
    /// <param name="token">The token to trim.</param>
    /// <returns>The text of the trimmed token.</returns>
    private static string Trimmed(string token)
    {
        var trimmed = WordTokenizer.TrimToken(token, new TextRange(0, token.Length));
        return token.Substring(trimmed.Start, trimmed.Length);
    }

    /// <summary>
    ///     Builds the reason reported for a list entry that contains a character that cannot be part of a word.
    /// </summary>
    /// <param name="character">The offending character as it appears in the entry.</param>
    /// <returns>The expected reason.</returns>
    private static string CharacterProblem(string character) =>
        $"it contains the character '{character}', which cannot be part of a word, so it can never match.";

    /// <summary>
    ///     Test that words separated by spaces are each reported.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_SpaceSeparatedLetters_ReturnsEachWord()
    {
        // Arrange: three words separated by spaces
        const string text = "alpha beta gamma";

        // Act: find the words
        var words = Words(text);

        // Assert: every word is reported in order
        Assert.Equal(["alpha", "beta", "gamma"], words);
    }

    /// <summary>
    ///     Test that digits, underscores, hyphens, dots, and other punctuation end a word.
    /// </summary>
    /// <param name="text">The text holding two words joined by a non-letter.</param>
    /// <param name="expected">The expected words joined with the separator.</param>
    [Theory]
    [InlineData("alpha1beta", "alpha|beta")]
    [InlineData("alpha_beta", "alpha|beta")]
    [InlineData("alpha-beta", "alpha|beta")]
    [InlineData("alpha.beta", "alpha|beta")]
    [InlineData("alpha,beta", "alpha|beta")]
    [InlineData("alpha/beta", "alpha|beta")]
    [InlineData("alpha+beta", "alpha|beta")]
    [InlineData("alpha@beta", "alpha|beta")]
    [InlineData("alpha`beta", "alpha|beta")]
    [InlineData("alpha\\beta", "alpha|beta")]
    [InlineData("alpha123", "alpha")]
    [InlineData("123alpha", "alpha")]
    public void WordTokenizer_FindWords_NonLetterCharacters_EndWords(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the words
        var words = Words(text);

        // Assert: the non-letter character separated the words
        Assert.Equal(expected, string.Join(Separator, words));
    }

    /// <summary>
    ///     Test that text without any letter has no words.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_OnlyDigitsAndPunctuation_ReturnsNoWords()
    {
        // Arrange: digits and punctuation only
        const string text = "123 -- ... __ 4.5 (6) !?";

        // Act: find the words
        var words = Words(text);

        // Assert: there are none
        Assert.Empty(words);
    }

    /// <summary>
    ///     Test that a reported range holds the start and the length of the word in the original text.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_ReportedRange_IsOriginalText()
    {
        // Arrange: one word after leading white space
        const string word = "alpha";
        var text = "  " + word + "  ";

        // Act: find the word
        var ranges = FindWordRanges(text);

        // Assert: the range starts at the word and is as long as the word
        var range = Assert.Single(ranges);
        Assert.Equal(text.IndexOf(word, StringComparison.Ordinal), range.Start);
        Assert.Equal(word.Length, range.Length);
    }

    /// <summary>
    ///     Test that only the words inside the requested range are reported.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_RangeInsideText_ReportsOnlyWordsInRange()
    {
        // Arrange: a text and a range that covers only the middle word
        const string text = "alpha beta gamma";
        const string middle = "beta";
        var within = new TextRange(text.IndexOf(middle, StringComparison.Ordinal), middle.Length);

        // Act: find the words inside the range
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, within, words);

        // Assert: only the middle word is reported
        Assert.Equal([within], words);
    }

    /// <summary>
    ///     Test that a word that continues past the end of the requested range is cut at the end of the range.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_RangeEndsInsideWord_CutsWordAtRangeEnd()
    {
        // Arrange: a range that covers only the first three letters of a word
        const string text = "alphabet";
        const int rangeLength = 3;

        // Act: find the words inside the range
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, new TextRange(0, rangeLength), words);

        // Assert: the word stops at the end of the range
        Assert.Equal([new TextRange(0, rangeLength)], words);
    }

    /// <summary>
    ///     Test that an apostrophe between two letters stays inside the word, in each accepted form, and that the
    ///     reported text keeps the original apostrophe.
    /// </summary>
    /// <param name="text">The word written with an apostrophe.</param>
    [Theory]
    [InlineData("don't")]
    [InlineData("don\u2019t")]
    [InlineData("don\\'t")]
    [InlineData("rock'n'roll")]
    [InlineData("O'Brien")]
    public void WordTokenizer_FindWords_ApostropheBetweenLetters_StaysInsideWord(string text)
    {
        // Arrange: the word is passed in by the theory data

        // Act: find the words
        var words = Words(text);

        // Assert: one word is reported and it is the original text, apostrophe included
        Assert.Equal([text], words);
    }

    /// <summary>
    ///     Test that apostrophes at the start and the end of a word are not part of it.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_LeadingAndTrailingApostrophe_AreNotPartOfWord()
    {
        // Arrange: words quoted with apostrophes and a possessive plural
        const string text = "'alpha' \u2018beta\u2019 dogs'";

        // Act: find the words
        var words = Words(text);

        // Assert: the apostrophes and quotes are left out
        Assert.Equal(["alpha", "beta", "dogs"], words);
    }

    /// <summary>
    ///     Test that two apostrophes in a row end a word.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_DoubleApostrophe_EndsWord()
    {
        // Arrange: two apostrophes between letters
        const string text = "don''t";

        // Act: find the words
        var words = Words(text);

        // Assert: only a single apostrophe may join letters, so the text is two words
        Assert.Equal(["don", "t"], words);
    }

    /// <summary>
    ///     Test that Han, Hiragana, Katakana, and Hangul characters are never words.
    /// </summary>
    /// <param name="text">A text written in one of those scripts.</param>
    [Theory]
    [InlineData("\u6F22\u5B57")]
    [InlineData("\u3072\u3089\u304C\u306A")]
    [InlineData("\u30AB\u30BF\u30AB\u30CA")]
    [InlineData("\uD55C\uAE00")]
    [InlineData("\uFF76\uFF80\uFF76\uFF85")]
    [InlineData("\u3005")]
    [InlineData("\u1100\u1161")]
    [InlineData("\U00020000")]
    public void WordTokenizer_FindWords_HanKanaAndHangul_AreNotWords(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the words
        var words = Words(text);

        // Assert: none of the characters is part of a word
        Assert.Empty(words);
    }

    /// <summary>
    ///     Test that characters of an ignored script end a word written in a script that is checked.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_HanBetweenLatinWords_SeparatesWords()
    {
        // Arrange: two Latin words with Han characters between them
        const string text = "alpha\u6F22\u5B57beta";

        // Act: find the words
        var words = Words(text);

        // Assert: the Han characters are skipped and both words are reported
        Assert.Equal(["alpha", "beta"], words);
    }

    /// <summary>
    ///     Test that accented letters and letters of other scripts are words.
    /// </summary>
    /// <param name="text">A word that is not written in plain ASCII.</param>
    [Theory]
    [InlineData("caf\u00E9")]
    [InlineData("na\u00EFve")]
    [InlineData("\u00C6r\u00F8")]
    [InlineData("\u0416\u0443\u043A")]
    [InlineData("\u03B1\u03B2\u03B3")]
    [InlineData("\u05D0\u05D1\u05D2")]
    [InlineData("\u0627\u0644\u0639")]
    public void WordTokenizer_FindWords_AccentedAndOtherScripts_AreWords(string text)
    {
        // Arrange: the word is passed in by the theory data

        // Act / Assert: the whole text is the only word
        Assert.Single(Words(text), text);
    }

    /// <summary>
    ///     Test that a combining mark stays with the letter before it.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_CombiningMarkAfterLetter_StaysInsideWord()
    {
        // Arrange: a word whose accent is a separate combining character
        const string text = "cafe\u0301 bar";

        // Act: find the words
        var words = Words(text);

        // Assert: the mark is part of the first word
        Assert.Equal(["cafe\u0301", "bar"], words);
    }

    /// <summary>
    ///     Test that a combining mark with no letter before it is not a word.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_CombiningMarkAlone_IsNotWord()
    {
        // Arrange: a combining mark standing alone
        const string text = " \u0301 ";

        // Act: find the words
        var words = Words(text);

        // Assert: nothing is reported
        Assert.Empty(words);
    }

    /// <summary>
    ///     Test that letters are classified as word letters and everything else is not.
    /// </summary>
    /// <param name="codePoint">The code point to classify.</param>
    /// <param name="expected">Whether the code point is a letter that belongs to a word.</param>
    [Theory]
    [InlineData(0x61, true)]
    [InlineData(0x5A, true)]
    [InlineData(0xE9, true)]
    [InlineData(0x3B1, true)]
    [InlineData(0x1D400, true)]
    [InlineData(0x30, false)]
    [InlineData(0x5F, false)]
    [InlineData(0x27, false)]
    [InlineData(0x7F, false)]
    [InlineData(0x301, false)]
    [InlineData(0x2019, false)]
    [InlineData(0xFFFD, false)]
    [InlineData(0x4E2D, false)]
    [InlineData(0x3042, false)]
    [InlineData(0x30AB, false)]
    [InlineData(0xAC00, false)]
    [InlineData(0x20000, false)]
    public void WordTokenizer_IsWordLetter_LettersAndOtherCodePoints_AreClassified(int codePoint, bool expected)
    {
        // Arrange: the code point is passed in by the theory data

        // Act: classify the code point
        var isLetter = WordTokenizer.IsWordLetter(codePoint);

        // Assert: only letters of the checked scripts are word letters
        Assert.Equal(expected, isLetter);
    }

    /// <summary>
    ///     Test that a word is split at case changes and at the end of an acronym.
    /// </summary>
    /// <param name="word">The word to split.</param>
    /// <param name="expected">The expected pieces joined with the separator.</param>
    [Theory]
    [InlineData("camelCase", "camel|Case")]
    [InlineData("PascalCase", "Pascal|Case")]
    [InlineData("getHttpResponse", "get|Http|Response")]
    [InlineData("XMLParser", "XML|Parser")]
    [InlineData("parseXMLFile", "parse|XML|File")]
    [InlineData("HTTPServer", "HTTP|Server")]
    [InlineData("XMLEditor", "XML|Editor")]
    [InlineData("fooBAR", "foo|BAR")]
    [InlineData("aB", "a|B")]
    public void WordTokenizer_SplitCase_CaseChanges_SplitIntoPieces(string word, string expected)
    {
        // Arrange: the word is passed in by the theory data

        // Act: split the word
        var pieces = Pieces(word);

        // Assert: the word is split where the case changes
        Assert.Equal(expected, string.Join(Separator, pieces));
    }

    /// <summary>
    ///     Test that a word with no case change is one piece.
    /// </summary>
    /// <param name="word">A word with no case change.</param>
    [Theory]
    [InlineData("lowercase")]
    [InlineData("UPPERCASE")]
    [InlineData("Capitalized")]
    [InlineData("A")]
    public void WordTokenizer_SplitCase_NoCaseChange_ReturnsWholeWord(string word)
    {
        // Arrange: the word is passed in by the theory data

        // Act: split the word
        var pieces = Pieces(word);

        // Assert: the word is its own single piece
        Assert.Equal([word], pieces);
    }

    /// <summary>
    ///     Test that an acronym followed by an English suffix stays whole.
    /// </summary>
    /// <param name="word">An acronym with a suffix such as the plural s.</param>
    [Theory]
    [InlineData("APIs")]
    [InlineData("URLs")]
    [InlineData("HTTPs")]
    [InlineData("IDs")]
    [InlineData("XMLed")]
    [InlineData("RAMing")]
    public void WordTokenizer_SplitCase_AcronymWithEnglishSuffix_StaysWhole(string word)
    {
        // Arrange: the word is passed in by the theory data

        // Act / Assert: the suffix is not split off the acronym, so the word is its own only piece
        Assert.Single(Pieces(word), word);
    }

    /// <summary>
    ///     Test that an acronym with a suffix is still split from the word that follows it, and that a lower-case
    ///     letter after a capital that is not a suffix still splits the acronym.
    /// </summary>
    /// <param name="word">An acronym with a suffix followed by another word, or an acronym before a word.</param>
    /// <param name="expected">The expected pieces joined with the separator.</param>
    [Theory]
    [InlineData("APIsFoo", "APIs|Foo")]
    [InlineData("IDsAndTags", "IDs|And|Tags")]
    [InlineData("HTTPSServer", "HTTPS|Server")]
    public void WordTokenizer_SplitCase_AcronymSuffixThenWord_SplitsAtNextWord(string word, string expected)
    {
        // Arrange: the word is passed in by the theory data

        // Act: split the word and join the pieces
        var joined = string.Join(Separator, Pieces(word));

        // Assert: the acronym keeps its suffix and the next word is split off
        Assert.Equal(expected, joined);
    }

    /// <summary>
    ///     Test that accented capitals, including those written with a combining mark, split like plain capitals.
    /// </summary>
    /// <param name="word">A word with accented letters.</param>
    /// <param name="expected">The expected pieces joined with the separator.</param>
    [Theory]
    [InlineData("caf\u00E9Bar", "caf\u00E9|Bar")]
    [InlineData("na\u00EFveBayes", "na\u00EFve|Bayes")]
    [InlineData("\u00C9cole", "\u00C9cole")]
    [InlineData("cafe\u0301Bar", "cafe\u0301|Bar")]
    [InlineData("fooE\u0301cole", "foo|E\u0301cole")]
    [InlineData("XE\u0301clair", "X|E\u0301clair")]
    [InlineData("E\u0301clair", "E\u0301clair")]
    public void WordTokenizer_SplitCase_AccentedCapitals_SplitLikePlainCapitals(string word, string expected)
    {
        // Arrange: the word is passed in by the theory data

        // Act / Assert: the combining mark neither splits nor hides a case change
        Assert.Equal(expected.Split(Separator[0]), Pieces(word));
    }

    /// <summary>
    ///     Test that the pieces cover the word completely and in order, at an offset inside a larger text.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_WordInsideText_PiecesCoverWordInOrder()
    {
        // Arrange: a word with several case changes in the middle of a text
        const string word = "parseXMLFileAndMoreIDs";
        const string padding = "  ";
        var text = padding + word + padding;
        var wordRange = new TextRange(padding.Length, word.Length);

        // Act: split the word
        var pieces = new List<TextRange>();
        WordTokenizer.SplitCase(text, wordRange, pieces);

        // Assert: the first piece starts the word, each piece starts where the previous one ended, and the last
        // piece ends the word
        Assert.True(pieces.Count > 1);
        Assert.Equal(wordRange.Start, pieces[0].Start);
        for (var i = 1; i < pieces.Count; i++)
        {
            Assert.Equal(pieces[i - 1].End, pieces[i].Start);
        }

        Assert.Equal(wordRange.End, pieces[^1].End);
    }

    /// <summary>
    ///     Test that a capital letter outside the basic multilingual plane splits a word and is kept whole.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_AstralCapital_SplitsBeforeBothCodeUnits()
    {
        // Arrange: a lower-case word followed by an astral capital and more lower-case letters
        var word = "foo" + AstralCapital + "bar";

        // Act: split the word
        var pieces = Pieces(word);

        // Assert: the split falls before the whole surrogate pair, not between its halves
        Assert.Equal(["foo", AstralCapital + "bar"], pieces);
    }

    /// <summary>
    ///     Test that offsets after a character written as a surrogate pair count UTF-16 code units.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_AfterSurrogatePair_OffsetsCountUtf16Units()
    {
        // Arrange: an emoji, a space, and a word
        const string word = "alpha";
        var text = Emoji + " " + word;

        // Act: find the words
        var range = Assert.Single(FindWordRanges(text));

        // Assert: the word starts after two code units for the emoji and one for the space
        Assert.Equal(EmojiUnits + 1, range.Start);
        Assert.Equal(word.Length, range.Length);
    }

    /// <summary>
    ///     Test that a token found after a surrogate pair is positioned in UTF-16 code units.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_AfterSurrogatePair_OffsetsCountUtf16Units()
    {
        // Arrange: an emoji, a space, and a token
        const string token = "k8s";
        var text = Emoji + " " + token;
        var tokens = new List<TextRange>();

        // Act: find the tokens
        WordTokenizer.FindTokens(text, 0, text.Length, tokens);

        // Assert: the token starts after two code units for the emoji and one for the space
        Assert.Equal([new TextRange(EmojiUnits + 1, token.Length)], tokens);
    }

    /// <summary>
    ///     Test that letters outside the basic multilingual plane form one word of two code units each.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_AstralLetters_FormOneWord()
    {
        // Arrange: two astral letters in a row
        var text = AstralCapital + AstralCapital;

        // Act: find the words
        var range = Assert.Single(FindWordRanges(text));

        // Assert: the word covers all four code units
        Assert.Equal(new TextRange(0, EmojiUnits * 2), range);
    }

    /// <summary>
    ///     Test that words found inside a range that does not start at the beginning have offsets into the whole text.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_RangeNotAtStart_ReportsOffsetsIntoWholeText()
    {
        // Arrange: a text and a range that starts at its second word
        const string text = "alpha beta gamma";
        var start = text.IndexOf("beta", StringComparison.Ordinal);
        var within = new TextRange(start, text.Length - start);

        // Act: find the words inside the range
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, within, words);

        // Assert: the offsets are counted from the start of the whole text
        Assert.Equal(start, words[0].Start);
        Assert.Equal("beta", text.Substring(words[0].Start, words[0].Length));
    }

    /// <summary>
    ///     Test that a range reports its end and whether it is empty from its start and length.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_ReportedRange_EndAndIsEmptyFollowFromStartAndLength()
    {
        // Arrange: a word range found in a text and an empty range
        const string text = "  alpha";
        var range = Assert.Single(FindWordRanges(text));
        var empty = new TextRange(range.Start, 0);

        // Act / Assert: the end is the start plus the length, and only the zero-length range is empty
        Assert.Equal(text.Length, range.End);
        Assert.False(range.IsEmpty);
        Assert.True(empty.IsEmpty);
    }

    /// <summary>
    ///     Test that letters, digits, and the allowed punctuation form one token.
    /// </summary>
    /// <param name="text">A token that contains digits or punctuation.</param>
    [Theory]
    [InlineData("k8s")]
    [InlineData("foo-bar")]
    [InlineData("v1.2.3")]
    [InlineData("snake_case")]
    [InlineData("it's")]
    [InlineData("it\u2019s")]
    [InlineData("c++")]
    [InlineData("a.b-c")]
    [InlineData("`code`")]
    [InlineData("--flag")]
    [InlineData("a\U0001D400b")]
    public void WordTokenizer_FindTokens_DigitsAndPunctuation_StayInsideToken(string text)
    {
        // Arrange: the token is passed in by the theory data

        // Act: find the tokens
        var tokens = Tokens(text);

        // Assert: the whole text is one token
        Assert.Equal([text], tokens);
    }

    /// <summary>
    ///     Test that a backslash is part of a token only when it escapes an apostrophe.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_Backslash_IsPartOfTokenOnlyBeforeApostrophe()
    {
        // Arrange: an escaped apostrophe and a backslash between letters
        const string escaped = "don\\'t";
        const string separated = "alpha\\beta";

        // Act: find the tokens of each text
        var escapedTokens = Tokens(escaped);
        var separatedTokens = Tokens(separated);

        // Assert: the escape stays in the token and the other backslash ends it
        Assert.Equal([escaped], escapedTokens);
        Assert.Equal(["alpha", "beta"], separatedTokens);
    }

    /// <summary>
    ///     Test that white space and punctuation outside the token alphabet separate tokens.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_SpacesAndOtherPunctuation_SeparateTokens()
    {
        // Arrange: tokens separated by spaces, commas, brackets, a slash, and a semicolon
        const string text = "alpha, (beta) [gamma]/delta;epsilon";

        // Act: find the tokens
        var tokens = Tokens(text);

        // Assert: five tokens without the separators
        Assert.Equal(["alpha", "beta", "gamma", "delta", "epsilon"], tokens);
    }

    /// <summary>
    ///     Test that a period after a token is part of it, because tokens are trimmed separately.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_TrailingPeriod_IsPartOfToken()
    {
        // Arrange: a token at the end of a sentence
        const string text = "install k8s.";

        // Act: find the tokens
        var tokens = Tokens(text);

        // Assert: the period is kept in the token
        Assert.Equal(["install", "k8s."], tokens);
    }

    /// <summary>
    ///     Test that a combining mark continues a token but cannot start one.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_CombiningMark_ContinuesButDoesNotStartToken()
    {
        // Arrange: a token with a combining accent and a mark before another token
        const string text = "cafe\u0301 \u0301abc";

        // Act: find the tokens
        var tokens = Tokens(text);

        // Assert: the accent stays in the first token and the lone mark is skipped
        Assert.Equal(["cafe\u0301", "abc"], tokens);
    }

    /// <summary>
    ///     Test that tokens are found only inside the requested part of the text.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_PartOfText_ReportsOnlyTokensInPart()
    {
        // Arrange: a text and a part that holds only the middle token
        const string text = "alpha k8s gamma";
        var start = text.IndexOf("k8s", StringComparison.Ordinal);
        var tokens = new List<TextRange>();

        // Act: find the tokens in the part
        WordTokenizer.FindTokens(text, start, start + "k8s".Length, tokens);

        // Assert: only the middle token is reported, at its offset in the whole text
        Assert.Equal([new TextRange(start, "k8s".Length)], tokens);
    }

    /// <summary>
    ///     Test that edge punctuation is removed from a token.
    /// </summary>
    /// <param name="token">A token with punctuation at its edges.</param>
    /// <param name="expected">The token without edge punctuation.</param>
    [Theory]
    [InlineData("..k8s..", "k8s")]
    [InlineData("-foo-", "foo")]
    [InlineData("'it's'", "it's")]
    [InlineData("_x_", "x")]
    [InlineData("`code`", "code")]
    [InlineData("+1+", "1")]
    [InlineData("v1.2.", "v1.2")]
    [InlineData("caf\u00E9.", "caf\u00E9")]
    [InlineData("\u00E9a", "\u00E9a")]
    [InlineData("clean", "clean")]
    public void WordTokenizer_TrimToken_EdgePunctuation_IsRemoved(string token, string expected)
    {
        // Arrange: the token is passed in by the theory data

        // Act: trim the token
        var trimmed = Trimmed(token);

        // Assert: letters and digits remain and the edge punctuation is gone
        Assert.Equal(expected, trimmed);
    }

    /// <summary>
    ///     Test that a token with no letter or digit trims to nothing.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_NoLetterOrDigit_ReturnsEmptyRange()
    {
        // Arrange: a token made only of punctuation
        const string token = "..-_'";

        // Act: trim the token
        var trimmed = WordTokenizer.TrimToken(token, new TextRange(0, token.Length));

        // Assert: nothing remains
        Assert.True(trimmed.IsEmpty);
    }

    /// <summary>
    ///     Test that trimming a token inside a larger text reports the range in the whole text.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_TokenInsideText_ReturnsRangeInWholeText()
    {
        // Arrange: a text whose second token ends in a period
        const string text = "see k8s. now";
        var tokens = new List<TextRange>();
        WordTokenizer.FindTokens(text, 0, text.Length, tokens);
        var token = tokens[1];

        // Act: trim that token
        var trimmed = WordTokenizer.TrimToken(text, token);

        // Assert: the range is the token without its period, at its offset in the whole text
        Assert.Equal(new TextRange(text.IndexOf("k8s", StringComparison.Ordinal), "k8s".Length), trimmed);
    }

    /// <summary>
    ///     Test that characters written as surrogate pairs at the edges of a range are trimmed whole.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_SurrogatePairAtEdges_AreTrimmed()
    {
        // Arrange: a token with an emoji on each side
        const string core = "k8s";
        var text = Emoji + core + Emoji;

        // Act: trim the whole text as a token
        var trimmed = WordTokenizer.TrimToken(text, new TextRange(0, text.Length));

        // Assert: both emoji are removed and the range is positioned in code units
        Assert.Equal(new TextRange(EmojiUnits, core.Length), trimmed);
    }

    /// <summary>
    ///     Test that an empty token trims to an empty range.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_EmptyToken_ReturnsEmptyRange()
    {
        // Arrange: a text and an empty token range inside it
        const string text = "alpha";

        // Act: trim the empty range
        var trimmed = WordTokenizer.TrimToken(text, new TextRange(1, 0));

        // Assert: the result is empty
        Assert.True(trimmed.IsEmpty);
    }

    /// <summary>
    ///     Test that a single word entry is classified as a word and keeps its letter case.
    /// </summary>
    /// <param name="entry">An entry that is exactly one word.</param>
    [Theory]
    [InlineData("alpha")]
    [InlineData("Alpha")]
    [InlineData("don't")]
    [InlineData("caf\u00E9")]
    public void WordTokenizer_TryParseListEntry_SingleWord_IsClassifiedAsWord(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out var problem);

        // Assert: the entry is usable, matched as a word, and unchanged
        Assert.True(parsed);
        Assert.Null(problem);
        Assert.Equal(entry, result.Word);
        Assert.True(result.IsSingleWord);
        Assert.Empty(result.Suggestions);
    }

    /// <summary>
    ///     Test that an entry with digits or punctuation is classified as a token entry.
    /// </summary>
    /// <param name="entry">An entry that contains something that ends a word.</param>
    [Theory]
    [InlineData("k8s")]
    [InlineData("well-known")]
    [InlineData("snake_case")]
    [InlineData("v1.2")]
    [InlineData("123")]
    public void WordTokenizer_TryParseListEntry_DigitsOrPunctuation_IsClassifiedAsToken(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out _);

        // Assert: the entry is usable and is matched against whole tokens
        Assert.True(parsed);
        Assert.Equal(entry, result.Word);
        Assert.False(result.IsSingleWord);
    }

    /// <summary>
    ///     Test that punctuation at the edges of an entry and white space around it are removed.
    /// </summary>
    /// <param name="entry">An entry with edge punctuation or surrounding white space.</param>
    /// <param name="expected">The expected normalized word.</param>
    [Theory]
    [InlineData("-alpha-", "alpha")]
    [InlineData(".alpha.", "alpha")]
    [InlineData("'alpha'", "alpha")]
    [InlineData("_alpha_", "alpha")]
    [InlineData("  alpha\t", "alpha")]
    [InlineData("-v1.2-", "v1.2")]
    public void WordTokenizer_TryParseListEntry_EdgePunctuationAndWhiteSpace_AreRemoved(string entry, string expected)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out _);

        // Assert: the normalized word has neither the punctuation nor the white space
        Assert.True(parsed);
        Assert.Equal(expected, result.Word);
    }

    /// <summary>
    ///     Test that a blank or missing entry is rejected as blank.
    /// </summary>
    /// <param name="entry">A null, empty, or white space entry.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void WordTokenizer_TryParseListEntry_BlankOrNull_IsRejectedAsBlank(string? entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out var problem);

        // Assert: the entry is rejected with the blank reason and no entry
        Assert.False(parsed);
        Assert.Equal(BlankProblem, problem);
        Assert.Equal(default, result);
    }

    /// <summary>
    ///     Test that an entry that contains white space is rejected with the white space reason.
    /// </summary>
    /// <param name="entry">An entry with white space between its characters.</param>
    [Theory]
    [InlineData("alpha beta")]
    [InlineData("alpha\tbeta")]
    [InlineData("alpha\u00A0beta")]
    public void WordTokenizer_TryParseListEntry_InnerWhiteSpace_IsRejectedWithReason(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out _, out var problem);

        // Assert: the entry is rejected and the reason says to list each word separately
        Assert.False(parsed);
        Assert.Equal(WhiteSpaceProblem, problem);
    }

    /// <summary>
    ///     Test that an entry that starts with the forbidden-word marker is rejected with the marker reason.
    /// </summary>
    /// <param name="entry">An entry that starts with an exclamation mark.</param>
    [Theory]
    [InlineData("!alpha")]
    [InlineData("!")]
    [InlineData("!!")]
    public void WordTokenizer_TryParseListEntry_ForbiddenMarker_IsRejectedWithReason(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out _, out var problem);

        // Assert: the entry is rejected and the reason names the marker and the flagWords setting
        Assert.False(parsed);
        Assert.Equal(ForbiddenProblem, problem);
    }

    /// <summary>
    ///     Test that an entry with a character that cannot be part of a word is rejected and the character is named.
    /// </summary>
    /// <param name="entry">An entry that contains a character outside the token alphabet.</param>
    /// <param name="character">The character the reason must name.</param>
    [Theory]
    [InlineData("alpha!", "!")]
    [InlineData("(alpha)", "(")]
    [InlineData("alpha/beta", "/")]
    [InlineData("alpha,beta", ",")]
    [InlineData("al$pha", "$")]
    [InlineData("al\U0001F600pha", "\U0001F600")]
    public void WordTokenizer_TryParseListEntry_DisallowedCharacter_IsRejectedNamingCharacter(string entry, string character)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out _, out var problem);

        // Assert: the entry is rejected and the reason names the first offending character
        Assert.False(parsed);
        Assert.Equal(CharacterProblem(character), problem);
    }

    /// <summary>
    ///     Test that an entry made only of punctuation is rejected because it has no letter or digit.
    /// </summary>
    /// <param name="entry">An entry that has no letter or digit.</param>
    [Theory]
    [InlineData("--")]
    [InlineData("_")]
    [InlineData("'")]
    [InlineData("...")]
    public void WordTokenizer_TryParseListEntry_NoLetterOrDigit_IsRejectedWithReason(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out _, out var problem);

        // Assert: the entry is rejected because it could never match
        Assert.False(parsed);
        Assert.Equal(NoLetterOrDigitProblem, problem);
    }

    /// <summary>
    ///     Test that a failed interpretation returns no entry and a successful one returns no problem.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_Outcome_FillsExactlyOneOfResultAndProblem()
    {
        // Arrange: one usable entry and one unusable entry
        const string usable = "alpha";
        const string unusable = "alpha beta";

        // Act: interpret both
        _ = WordTokenizer.TryParseListEntry(usable, false, out var goodResult, out var goodProblem);
        _ = WordTokenizer.TryParseListEntry(unusable, false, out var badResult, out var badProblem);

        // Assert: the usable entry has a result and no problem; the unusable one the reverse
        Assert.NotNull(goodResult.Word);
        Assert.Null(goodProblem);
        Assert.Equal(default, badResult);
        Assert.NotNull(badProblem);
    }

    /// <summary>
    ///     Test that the suggestion characters are not accepted when suggestions are not allowed.
    /// </summary>
    /// <param name="entry">An entry written in one of the suggestion forms.</param>
    /// <param name="character">The character the reason must name.</param>
    [Theory]
    [InlineData("teh->the", ">")]
    [InlineData("color:hue", ":")]
    public void WordTokenizer_TryParseListEntry_SuggestionFormNotAllowed_IsRejected(string entry, string character)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry with suggestions switched off
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out var problem);

        // Assert: the whole entry is the word, so the separator is an invalid character and no entry results
        Assert.False(parsed);
        Assert.Equal(default, result);
        Assert.Equal(CharacterProblem(character), problem);
    }

    /// <summary>
    ///     Test that the arrow form separates a flagged word from its suggestions.
    /// </summary>
    /// <param name="entry">An entry in the arrow form.</param>
    /// <param name="word">The expected flagged word.</param>
    /// <param name="expectedSuggestions">The expected suggestions joined with the separator.</param>
    [Theory]
    [InlineData("teh->the", "teh", "the")]
    [InlineData("teh->the, tea", "teh", "the|tea")]
    [InlineData("teh -> the", "teh", "the")]
    [InlineData("teh->the,, ,tea", "teh", "the|tea")]
    [InlineData("teh->the: tea", "teh", "the: tea")]
    public void WordTokenizer_TryParseListEntry_ArrowForm_SeparatesWordFromSuggestions(
        string entry,
        string word,
        string expectedSuggestions)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(entry, true, out var result, out var problem);

        // Assert: the word and the trimmed suggestions are separated
        Assert.True(parsed);
        Assert.Null(problem);
        Assert.Equal(word, result.Word);
        Assert.Equal(expectedSuggestions, string.Join(Separator, result.Suggestions));
    }

    /// <summary>
    ///     Test that the colon form separates a flagged word from its suggestion.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_ColonForm_SeparatesWordFromSuggestion()
    {
        // Arrange: an entry in the colon form
        const string entry = "color: hue";

        // Act: interpret the entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(entry, true, out var result, out _);

        // Assert: the word is before the colon and the suggestion after it
        Assert.True(parsed);
        Assert.Equal("color", result.Word);
        Assert.Equal(["hue"], result.Suggestions.ToArray());
    }

    /// <summary>
    ///     Test that a suggestion separator with nothing after it gives a word with no suggestions.
    /// </summary>
    /// <param name="entry">An entry whose separator is followed by nothing.</param>
    [Theory]
    [InlineData("teh->")]
    [InlineData("teh:")]
    [InlineData("teh->  ,")]
    public void WordTokenizer_TryParseListEntry_SeparatorWithoutSuggestions_ReturnsWordOnly(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(entry, true, out var result, out _);

        // Assert: the word is usable and has no suggestions
        Assert.True(parsed);
        Assert.Equal("teh", result.Word);
        Assert.Empty(result.Suggestions);
    }

    /// <summary>
    ///     Test that an entry with no separator is a plain word when suggestions are allowed.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_NoSeparatorWithSuggestionsAllowed_ReturnsPlainWord()
    {
        // Arrange: an entry with no suggestion separator
        const string entry = "teh";

        // Act: interpret the entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(entry, true, out var result, out _);

        // Assert: the word has no suggestions
        Assert.True(parsed);
        Assert.Equal(entry, result.Word);
        Assert.Empty(result.Suggestions);
    }

    /// <summary>
    ///     Test that a suggestion entry with nothing before the separator is rejected.
    /// </summary>
    /// <param name="entry">An entry whose word is missing.</param>
    [Theory]
    [InlineData("->the")]
    [InlineData(": the")]
    [InlineData("  ->the")]
    public void WordTokenizer_TryParseListEntry_SuggestionsWithoutWord_IsRejectedWithReason(string entry)
    {
        // Arrange: the entry is passed in by the theory data

        // Act: interpret the entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(entry, true, out _, out var problem);

        // Assert: the entry is rejected because there is no word to flag
        Assert.False(parsed);
        Assert.Equal(NoWordProblem, problem);
    }

    /// <summary>
    ///     Test that the word of a suggestion entry is checked like the word of any other entry.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_InvalidWordWithSuggestions_IsRejectedWithReason()
    {
        // Arrange: a suggestion entry whose word starts with the forbidden-word marker and one with white space
        const string marked = "!teh->the";
        const string spaced = "te h->the";

        // Act: interpret both entries with suggestions allowed
        _ = WordTokenizer.TryParseListEntry(marked, true, out _, out var markedProblem);
        _ = WordTokenizer.TryParseListEntry(spaced, true, out _, out var spacedProblem);

        // Assert: each is rejected for the same reason as without suggestions
        Assert.Equal(ForbiddenProblem, markedProblem);
        Assert.Equal(WhiteSpaceProblem, spacedProblem);
    }

    /// <summary>
    ///     Test that a null entry is rejected as blank when suggestions are allowed.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_NullWithSuggestionsAllowed_IsRejectedAsBlank()
    {
        // Arrange: no entry text

        // Act: interpret a null entry with suggestions allowed
        var parsed = WordTokenizer.TryParseListEntry(null, true, out _, out var problem);

        // Assert: the entry is rejected as blank
        Assert.False(parsed);
        Assert.Equal(BlankProblem, problem);
    }

    /// <summary>
    ///     Test that characters are counted as Unicode scalar values, with a surrogate pair counted once.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="expected">The expected number of characters.</param>
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("a\U0001F600b", 3)]
    [InlineData("\U0001F600", 1)]
    [InlineData("e\u0301", 2)]
    public void WordTokenizer_CountCharacters_Text_CountsSurrogatePairsOnce(string text, int expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: count the characters
        var count = WordTokenizer.CountCharacters(text);

        // Assert: pairs count once and a combining mark counts as a character of its own
        Assert.Equal(expected, count);
    }

    /// <summary>
    ///     Test that a lone surrogate counts as one character, wherever it is.
    /// </summary>
    [Fact]
    public void WordTokenizer_CountCharacters_LoneSurrogates_CountAsOneEach()
    {
        // Arrange: a lone high surrogate, a lone low surrogate, a lone high surrogate before a letter, and a
        // low surrogate before a high one (the theory data cannot carry lone surrogates, so they are built here)
        const string high = "\uD800";
        const string low = "\uDC00";

        // Act: count the characters of each
        var counts = new[]
        {
            WordTokenizer.CountCharacters(high),
            WordTokenizer.CountCharacters(low),
            WordTokenizer.CountCharacters(high + "a"),
            WordTokenizer.CountCharacters(low + high),
        };

        // Assert: every surrogate that is not part of a pair counts on its own
        Assert.Equal([1, 1, 2, 2], counts);
    }

    /// <summary>
    ///     Test that the words found are appended to a list that already holds items.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_ExistingListContent_IsKeptAndAppendedTo()
    {
        // Arrange: a list that already holds a range
        var existing = new TextRange(0, 0);
        var words = new List<TextRange> { existing };

        // Act: find the words of a text
        WordTokenizer.FindWords("alpha", new TextRange(0, "alpha".Length), words);

        // Assert: the earlier range is still first and the word follows it
        Assert.Equal([existing, new TextRange(0, "alpha".Length)], words);
    }

    /// <summary>
    ///     Test that the tokens found are appended to a list that already holds items.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_ExistingListContent_IsKeptAndAppendedTo()
    {
        // Arrange: a list that already holds a range
        var existing = new TextRange(0, 0);
        var tokens = new List<TextRange> { existing };

        // Act: find the tokens of a text
        WordTokenizer.FindTokens("k8s", 0, "k8s".Length, tokens);

        // Assert: the earlier range is still first and the token follows it
        Assert.Equal([existing, new TextRange(0, "k8s".Length)], tokens);
    }

    /// <summary>
    ///     Test that the pieces found are appended to a list that already holds items.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_ExistingListContent_IsKeptAndAppendedTo()
    {
        // Arrange: a list that already holds a range
        const string word = "fooBar";
        var existing = new TextRange(0, 0);
        var pieces = new List<TextRange> { existing };

        // Act: split a word
        WordTokenizer.SplitCase(word, new TextRange(0, word.Length), pieces);

        // Assert: the earlier range is still first and the two pieces follow it
        Assert.Equal([existing, new TextRange(0, "foo".Length), new TextRange("foo".Length, "Bar".Length)], pieces);
    }

    /// <summary>
    ///     Test that empty text has no words, tokens, or trimmed content.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_EmptyText_ReturnsNothing()
    {
        // Arrange: an empty text

        // Act: find words and tokens and trim an empty token
        var words = Words(string.Empty);
        var tokens = Tokens(string.Empty);
        var trimmed = WordTokenizer.TrimToken(string.Empty, new TextRange(0, 0));

        // Assert: nothing is found
        Assert.Empty(words);
        Assert.Empty(tokens);
        Assert.True(trimmed.IsEmpty);
    }

    /// <summary>
    ///     Test that lone surrogates are skipped when words are found.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_LoneSurrogates_AreSkipped()
    {
        // Arrange: words separated and surrounded by lone high and low surrogates
        const string text = "\uD800alpha\uDC00beta\uD800";

        // Act: find the words
        var words = Words(text);

        // Assert: the surrogates are neither letters nor errors
        Assert.Equal(["alpha", "beta"], words);
    }

    /// <summary>
    ///     Test that lone surrogates are skipped when tokens are found.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_LoneSurrogates_AreSkipped()
    {
        // Arrange: tokens separated and surrounded by lone high and low surrogates
        const string text = "\uD800alpha\uDC00beta\uD800";

        // Act: find the tokens
        var tokens = Tokens(text);

        // Assert: the surrogates end tokens and are not reported
        Assert.Equal(["alpha", "beta"], tokens);
    }

    /// <summary>
    ///     Test that a surrogate pair cut by the end of the requested range is not read as a letter.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_PairCutByRangeEnd_IsNotPartOfWord()
    {
        // Arrange: a word followed by an astral letter, with a range that ends between its two halves
        var text = "alpha" + AstralCapital;
        var within = new TextRange(0, "alpha".Length + 1);

        // Act: find the words in the range
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, within, words);

        // Assert: only the word before the cut half is reported
        Assert.Equal([new TextRange(0, "alpha".Length)], words);
    }

    /// <summary>
    ///     Test that a word containing a lone surrogate can be split without error.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_LoneSurrogateInWord_CoversWholeWord()
    {
        // Arrange: a word with a lone surrogate between case changes
        const string word = "foo\uD800Bar\uDC00Baz";
        var pieces = new List<TextRange>();

        // Act: split the word
        WordTokenizer.SplitCase(word, new TextRange(0, word.Length), pieces);

        // Assert: the pieces still cover the word completely and in order
        Assert.Equal(0, pieces[0].Start);
        Assert.Equal(word.Length, pieces[^1].End);
        for (var i = 1; i < pieces.Count; i++)
        {
            Assert.Equal(pieces[i - 1].End, pieces[i].Start);
        }
    }

    /// <summary>
    ///     Test that lone surrogates at the edges of a token are trimmed.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_LoneSurrogatesAtEdges_AreTrimmed()
    {
        // Arrange: a token with a lone high surrogate before it and a lone low surrogate after it
        const string text = "\uD800k8s\uDC00";

        // Act: trim the whole text as a token
        var trimmed = Trimmed(text);

        // Assert: only the token remains
        Assert.Equal("k8s", trimmed);
    }

    /// <summary>
    ///     Test that a text of one megabyte is scanned completely.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_OneMegabyteOfText_ReturnsEveryWord()
    {
        // Arrange: a single line of one megabyte made of two words repeated
        const string unit = "alpha beta ";
        var repeats = OneMegabyte / unit.Length;
        var text = string.Concat(Enumerable.Repeat(unit, repeats));

        // Act: find the words
        var words = FindWordRanges(text);

        // Assert: every word is found
        Assert.Equal(repeats * 2, words.Count);
    }

    /// <summary>
    ///     Test that a single word of one megabyte is one word.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_OneMegabyteWord_ReturnsOneWord()
    {
        // Arrange: one word of one megabyte
        var text = new string('a', OneMegabyte);

        // Act: find the words
        var word = Assert.Single(FindWordRanges(text));

        // Assert: the word covers the whole text
        Assert.Equal(new TextRange(0, OneMegabyte), word);
    }

    /// <summary>
    ///     Test that a token of one megabyte is one token and can be trimmed.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_OneMegabyteToken_ReturnsOneToken()
    {
        // Arrange: one token of one megabyte with punctuation at both edges
        var text = "." + new string('a', OneMegabyte) + ".";
        var tokens = new List<TextRange>();

        // Act: find the token and trim it
        WordTokenizer.FindTokens(text, 0, text.Length, tokens);
        var trimmed = WordTokenizer.TrimToken(text, Assert.Single(tokens));

        // Assert: the token is whole and the trimmed range excludes the edge periods
        Assert.Equal(new TextRange(1, OneMegabyte), trimmed);
    }

    /// <summary>
    ///     Test that a word of one megabyte with a case change at every second letter is split completely.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_OneMegabyteWord_SplitsEveryCaseChange()
    {
        // Arrange: a word of one megabyte made of the pair "aB" repeated
        var repeats = OneMegabyte / 2;
        var word = string.Concat(Enumerable.Repeat("aB", repeats));
        var pieces = new List<TextRange>();

        // Act: split the word
        WordTokenizer.SplitCase(word, new TextRange(0, word.Length), pieces);

        // Assert: a split falls before each capital, so there is one piece more than there are capitals
        Assert.Equal(repeats + 1, pieces.Count);
    }

    /// <summary>
    ///     Test that a list entry of one megabyte is interpreted.
    /// </summary>
    [Fact]
    public void WordTokenizer_TryParseListEntry_OneMegabyteEntry_IsOneWord()
    {
        // Arrange: an entry of one megabyte of letters
        var entry = new string('a', OneMegabyte);

        // Act: interpret the entry
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out var result, out _);

        // Assert: the entry is usable and is one word
        Assert.True(parsed);
        Assert.True(result.IsSingleWord);
        Assert.Equal(OneMegabyte, result.Word.Length);
    }

    /// <summary>
    ///     Test that finding words rejects a missing text or a missing list.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindWords_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a valid text and list to pair with each null argument
        var words = new List<TextRange>();

        // Act / Assert: a null text and a null list each throw
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.FindWords(null!, new TextRange(0, 0), words));
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.FindWords("alpha", new TextRange(0, 0), null!));
    }

    /// <summary>
    ///     Test that finding words rejects a range that is not inside the text.
    /// </summary>
    /// <param name="start">The start of the range.</param>
    /// <param name="length">The length of the range.</param>
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 100)]
    [InlineData(3, 5)]
    [InlineData(2, -1)]
    public void WordTokenizer_FindWords_RangeOutsideText_ThrowsArgumentOutOfRangeException(int start, int length)
    {
        // Arrange: a short text and a range that does not fit in it
        const string text = "alpha";

        // Act / Assert: the range is rejected
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WordTokenizer.FindWords(text, new TextRange(start, length), []));
    }

    /// <summary>
    ///     Test that finding tokens rejects a missing text or a missing list.
    /// </summary>
    [Fact]
    public void WordTokenizer_FindTokens_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a valid list to pair with a null text

        // Act / Assert: a null text and a null list each throw
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.FindTokens(null!, 0, 0, []));
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.FindTokens("alpha", 0, 0, null!));
    }

    /// <summary>
    ///     Test that finding tokens rejects a part that is not inside the text.
    /// </summary>
    /// <param name="start">The offset of the first code unit of the part.</param>
    /// <param name="end">The offset just past the part.</param>
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 100)]
    [InlineData(3, 2)]
    public void WordTokenizer_FindTokens_PartOutsideText_ThrowsArgumentOutOfRangeException(int start, int end)
    {
        // Arrange: a short text and a part that does not fit in it
        const string text = "alpha";

        // Act / Assert: the part is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => WordTokenizer.FindTokens(text, start, end, []));
    }

    /// <summary>
    ///     Test that splitting rejects a missing text or a missing list.
    /// </summary>
    [Fact]
    public void WordTokenizer_SplitCase_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a valid range to pair with each null argument

        // Act / Assert: a null text and a null list each throw
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.SplitCase(null!, new TextRange(0, 0), []));
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.SplitCase("alpha", new TextRange(0, 0), null!));
    }

    /// <summary>
    ///     Test that splitting rejects a word range that is not inside the text.
    /// </summary>
    /// <param name="start">The start of the range.</param>
    /// <param name="length">The length of the range.</param>
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 100)]
    [InlineData(2, -1)]
    public void WordTokenizer_SplitCase_RangeOutsideText_ThrowsArgumentOutOfRangeException(int start, int length)
    {
        // Arrange: a short text and a range that does not fit in it
        const string text = "alpha";

        // Act / Assert: the range is rejected
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WordTokenizer.SplitCase(text, new TextRange(start, length), []));
    }

    /// <summary>
    ///     Test that trimming rejects a missing text.
    /// </summary>
    [Fact]
    public void WordTokenizer_TrimToken_NullText_ThrowsArgumentNullException()
    {
        // Arrange: no text

        // Act / Assert: a null text throws
        Assert.Throws<ArgumentNullException>(() => WordTokenizer.TrimToken(null!, new TextRange(0, 0)));
    }

    /// <summary>
    ///     Test that trimming rejects a token range that is not inside the text.
    /// </summary>
    /// <param name="start">The start of the range.</param>
    /// <param name="length">The length of the range.</param>
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 100)]
    [InlineData(2, -1)]
    public void WordTokenizer_TrimToken_RangeOutsideText_ThrowsArgumentOutOfRangeException(int start, int length)
    {
        // Arrange: a short text and a range that does not fit in it
        const string text = "alpha";

        // Act / Assert: the range is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => WordTokenizer.TrimToken(text, new TextRange(start, length)));
    }
}
