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
///     Subsystem tests for the TextScanning subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="WordTokenizer"/>, <see cref="DefaultPatterns"/>, and
///     <see cref="DocumentDirectives"/> together the way a spell checker uses them: the excluded ranges of
///     the patterns and the directives are combined, and the words and tokens of the tokenizer are then
///     filtered and compared with the entries of a word list. The subsystem has no dependencies on other
///     subsystems, so no other part of the library is used.
/// </remarks>
public class TextScanningSubsystemTests
{
    /// <summary>
    ///     The number of UTF-16 code units in the emoji written as a surrogate pair in these tests.
    /// </summary>
    private const int EmojiUnits = 2;

    /// <summary>
    ///     The emoji (a character outside the basic multilingual plane) used to test surrogate pairs.
    /// </summary>
    private const string Emoji = "\U0001F600";

    /// <summary>
    ///     The size of the very large inputs, in characters (one megabyte of text, counted as 1,048,576 characters).
    /// </summary>
    private const int OneMegabyte = 1024 * 1024;

    /// <summary>
    ///     The prefix of a directive, with its colon, that the test text is built from, so that no directive is
    ///     written out in this file, where the spell checker that lints the repository would read it.
    /// </summary>
    private const string Cspell = "cspell:";

    /// <summary>
    ///     Finds the ranges of the words in a text that no excluded range covers.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The ranges of the words that are to be checked, in order.</returns>
    private static List<TextRange> CheckedWordRanges(string text)
    {
        // Combine the ranges excluded by the default patterns and by the directives
        var excluded = new List<TextRange>();
        DefaultPatterns.FindExcludedRanges(text, excluded);
        excluded.AddRange(DocumentDirectives.Scan(text).ExcludedRanges);

        // Keep the words that do not start inside an excluded range
        var words = new List<TextRange>();
        WordTokenizer.FindWords(text, new TextRange(0, text.Length), words);
        return [.. words.Where(word => !excluded.Exists(range => word.Start >= range.Start && word.Start < range.End))];
    }

    /// <summary>
    ///     Finds the words in a text that no excluded range covers.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The words that are to be checked, in order.</returns>
    private static string[] CheckedWords(string text) =>
        [.. CheckedWordRanges(text).Select(word => text.Substring(word.Start, word.Length))];

    /// <summary>
    ///     Finds the case-change pieces of the words in a text that no excluded range covers.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The pieces of the words that are to be checked, in order.</returns>
    private static string[] CheckedPieces(string text)
    {
        var pieces = new List<TextRange>();
        foreach (var word in CheckedWordRanges(text))
        {
            WordTokenizer.SplitCase(text, word, pieces);
        }

        return [.. pieces.Select(piece => text.Substring(piece.Start, piece.Length))];
    }

    /// <summary>
    ///     Test that identifiers and prose are split into the pieces that are checked.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_ExtractWords_IdentifiersAndProse_YieldsCheckedPieces()
    {
        // Arrange: camel case joined by an underscore and a hyphen, an apostrophe word, an acronym, an accented
        // word, a Han word, and a word with digits
        const string text = "fooBar_baz-qux don't parseXMLFile caf\u00E9 \u6F22\u5B57 alpha2beta";

        // Act: extract the pieces that are checked
        var pieces = CheckedPieces(text);

        // Assert: the identifiers are split, the Han word is left out, and the digit ends a word
        Assert.Equal(
            ["foo", "Bar", "baz", "qux", "don't", "parse", "XML", "File", "caf\u00E9", "alpha", "beta"],
            pieces);
    }

    /// <summary>
    ///     Test that the positions of the extracted words count UTF-16 code units, also after an emoji.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_ExtractWords_AfterEmoji_PositionsAreUtf16()
    {
        // Arrange: a word, an emoji, and a second word
        const string first = "alpha";
        const string second = "beta";
        var text = first + " " + Emoji + " " + second;

        // Act: extract the words
        var words = CheckedWordRanges(text);

        // Assert: the second word starts after the first, two spaces, and the two code units of the emoji
        Assert.Equal(
            [new TextRange(0, first.Length), new TextRange(first.Length + 1 + EmojiUnits + 1, second.Length)],
            words);
    }

    /// <summary>
    ///     Test that malformed text, with lone surrogates and a very long line, is processed without error.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_ExtractWords_MalformedText_CompletesWithoutError()
    {
        // Arrange: lone surrogates around words and a line of one megabyte
        var line = new string('a', OneMegabyte);
        var text = "\uD800alpha\uDC00 " + line + " beta\uD800";

        // Act: extract the pieces that are checked
        var pieces = CheckedPieces(text);

        // Assert: the words next to the surrogates and the long word are all found
        Assert.Equal(["alpha", line, "beta"], pieces);
    }

    /// <summary>
    ///     Test that a list entry with digits matches the trimmed token found in the text.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_MatchListEntry_TokenEntry_MatchesTrimmedToken()
    {
        // Arrange: an entry with digits, and a text that holds the same token inside punctuation
        const string text = "install (k8s). now";
        var parsed = WordTokenizer.TryParseListEntry("k8s", false, out var entry, out _);

        // Act: find the tokens of the text and trim each one the way entries are trimmed
        var tokens = new List<TextRange>();
        WordTokenizer.FindTokens(text, 0, text.Length, tokens);
        var trimmed = tokens.Select(token => WordTokenizer.TrimToken(text, token)).Select(range => text.Substring(range.Start, range.Length));

        // Assert: the entry is a token entry, and a trimmed token equals it
        Assert.True(parsed);
        Assert.False(entry.IsSingleWord);
        Assert.Contains(entry.Word, trimmed);
    }

    /// <summary>
    ///     Test that a single-word list entry matches a word of the text in any letter case.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_MatchListEntry_WordEntry_MatchesWordInAnyCase()
    {
        // Arrange: an entry written with a capital, and a text that holds the word in lower case
        const string text = "an alpha test";
        var parsed = WordTokenizer.TryParseListEntry("Alpha", false, out var entry, out _);

        // Act: find the words of the text
        var words = CheckedWords(text);

        // Assert: the entry is a single word and a word of the text equals it ignoring letter case
        Assert.True(parsed);
        Assert.True(entry.IsSingleWord);
        Assert.Contains(words, word => string.Equals(word, entry.Word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Test that an entry that could never match is rejected with a reason when it is interpreted.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_MatchListEntry_EntryThatCannotMatch_IsRejectedWithReason()
    {
        // Arrange: an entry with white space, which no word or token can ever equal
        const string entry = "alpha beta";

        // Act: interpret the entry, and find the words of a text that holds it
        var parsed = WordTokenizer.TryParseListEntry(entry, false, out _, out var problem);
        var words = CheckedWords(entry);

        // Assert: the entry is rejected with a reason, and the text does hold two separate words instead
        Assert.False(parsed);
        Assert.False(string.IsNullOrEmpty(problem));
        Assert.Equal(["alpha", "beta"], words);
    }

    /// <summary>
    ///     Test that a flagged-word entry gives the word and its suggestions, and the word matches the text.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_MatchListEntry_FlaggedEntryWithSuggestions_CarriesSuggestions()
    {
        // Arrange: a flagged-word entry in the arrow form and a text that holds the word
        const string text = "write teh word";
        var parsed = WordTokenizer.TryParseListEntry("teh->the, tea", true, out var entry, out _);

        // Act: find the words of the text
        var words = CheckedWords(text);

        // Assert: the entry matches a word of the text and carries both suggestions
        Assert.True(parsed);
        Assert.Contains(entry.Word, words);
        Assert.Equal(["the", "tea"], entry.Suggestions.ToArray());
    }

    /// <summary>
    ///     Test that URLs and email addresses are excluded and the words around them are kept.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_FindExcludedRanges_UrlAndEmail_AreExcludedFromWords()
    {
        // Arrange: prose that holds a URL and an email address
        const string text = "visit https://example.com/path or write to alice@example.com about this";

        // Act: extract the words that are checked
        var words = CheckedWords(text);

        // Assert: nothing of the URL or the address is checked
        Assert.Equal(["visit", "or", "write", "to", "about", "this"], words);
    }

    /// <summary>
    ///     Test that hexadecimal values, UUIDs, and encoded data are excluded and prose is kept.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_FindExcludedRanges_HexUuidAndEncodedData_AreExcludedFromWords()
    {
        // Arrange: prose that holds a hash, a hexadecimal literal, a color, a UUID, and encoded data
        const string text = "commit a1b2c3d with 0xdeadbeef and #ff00ff plus 123e4567-e89b-12d3-a456-426614174000 " +
                            "then QmFzZTY0IGlzIGEgc2FtcGxlIHN0cmluZyB3aXRoIG1peGVkIGNhc2U done";

        // Act: extract the words that are checked
        var words = CheckedWords(text);

        // Assert: only the prose is checked
        Assert.Equal(["commit", "with", "and", "plus", "then", "done"], words);
    }

    /// <summary>
    ///     Test that ordinary prose is not excluded by the patterns or the directives.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_FindExcludedRanges_OrdinaryProse_ExcludesNothing()
    {
        // Arrange: prose with no address, value, or directive
        const string text = "The quick brown fox jumps over the lazy dog.";

        // Act: extract the words that are checked
        var words = CheckedWords(text);

        // Assert: every word is checked
        Assert.Equal(["The", "quick", "brown", "fox", "jumps", "over", "the", "lazy", "dog"], words);
    }

    /// <summary>
    ///     Test that a disabled block hides its words and the words around it stay checked.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_Scan_DisabledBlock_ExcludesWordsInside()
    {
        // Arrange: a block between a disable and an enable directive, with text around it
        var text = string.Join(
            "\n",
            "visible one",
            "// " + Cspell + "disable",
            "hidden words",
            "// " + Cspell + "enable visible two");

        // Act: extract the words that are checked
        var words = CheckedWords(text);

        // Assert: the words of the block and of the directives are not checked
        Assert.Equal(["visible", "one", "visible", "two"], words);
    }

    /// <summary>
    ///     Test that an ignore directive adds its words and its own text is not checked.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_Scan_IgnoreDirective_AddsWordsAndExcludesDirectiveText()
    {
        // Arrange: a line with text before an ignore directive, and a line after it
        var text = string.Join("\n", "visible // " + Cspell + "ignore tarball, layout", "next line");

        // Act: scan the directives and extract the words that are checked
        var scan = DocumentDirectives.Scan(text);
        var words = CheckedWords(text);

        // Assert: the words are ignored words, and the words of the directive are not checked
        Assert.Equal(["tarball", "layout"], scan.IgnoredWords.ToArray());
        Assert.Equal(["visible", "next", "line"], words);
    }

    /// <summary>
    ///     Test that an unsupported directive is reported at its name and its text is still checked.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_Scan_UnsupportedDirective_IsReportedAtItsName()
    {
        // Arrange: a line with an unsupported directive after some text
        const string text = "visible // " + Cspell + "locale en-GB";

        // Act: scan the directives and extract the words that are checked
        var scan = DocumentDirectives.Scan(text);
        var words = CheckedWords(text);

        // Assert: the directive is reported at its name, and the text is checked as usual
        var range = Assert.Single(scan.UnsupportedDirectives);
        Assert.Equal("locale", text.Substring(range.Start, range.Length));
        Assert.Contains("visible", words);
    }

    /// <summary>
    ///     Test that every directive prefix switches off checking in the same way.
    /// </summary>
    /// <param name="prefix">The directive prefix as written.</param>
    [Theory]
    [InlineData("cspell:")]
    [InlineData("spell:")]
    [InlineData("spellchecker:")]
    [InlineData("spell-checker:")]
    [InlineData("textlint:")]
    [InlineData("TextLint: ")]
    public void TextScanningSubsystem_Scan_EveryPrefix_SwitchesOffCheckingAlike(string prefix)
    {
        // Arrange: a disable-line directive with the given prefix on the middle line
        var text = string.Join("\n", "first line", $"hidden words {prefix}disable-line here", "last line");

        // Act: extract the words that are checked
        var words = CheckedWords(text);

        // Assert: the words of the middle line are not checked
        Assert.Equal(["first", "line", "last", "line"], words);
    }

    /// <summary>
    ///     Test that a text with a disabled block, a URL, and an ignore directive leaves only the checked words.
    /// </summary>
    [Fact]
    public void TextScanningSubsystem_ScanDocument_DisabledBlockUrlAndIgnoreDirective_LeavesOnlyCheckedWords()
    {
        // Arrange: a document with a disabled block that holds a URL, a visible URL and email address, and
        // an ignore directive
        var text = string.Join(
            "\n",
            "visible one",
            "// " + Cspell + "disable",
            "hidden words https://example.com/hidden",
            "// " + Cspell + "enable",
            "see https://example.com/page and alice@example.com",
            "// " + Cspell + "ignore tarball",
            "visible two");

        // Act: scan the directives and extract the words that are checked
        var scan = DocumentDirectives.Scan(text);
        var words = CheckedWords(text);

        // Assert: the ignored word is reported and only the visible prose is checked
        Assert.Equal(["tarball"], scan.IgnoredWords.ToArray());
        Assert.Equal(["visible", "one", "see", "and", "visible", "two"], words);
    }
}
