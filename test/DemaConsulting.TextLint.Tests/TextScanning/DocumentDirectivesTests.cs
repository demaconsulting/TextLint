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
///     Unit tests for the <see cref="DocumentDirectives"/> class and the <see cref="DirectiveScan"/> value it
///     produces.
/// </summary>
/// <remarks>
///     The scanner is pure logic over strings and uses nothing but the .NET base class library, so these tests use
///     the real class with no test doubles. Tests that depend on the line terminator run with both LF and CRLF,
///     because the directive patterns must give the same answer for both. The text that holds a directive is built
///     from lines, and the expected excluded text is cut from the same text, so the expectations never repeat an
///     offset. Directive text is assembled from a prefix constant and the directive name, so that no directive is
///     written out in this file: the spell checker that lints the repository reads directives in every file, and
///     one written here would change how this file is checked.
/// </remarks>
public class DocumentDirectivesTests
{
    /// <summary>
    ///     The prefix of a directive, with its colon, that the test text is built from.
    /// </summary>
    private const string Cspell = "cspell:";

    /// <summary>
    ///     The same prefix in upper case, to test that the prefix is not case-sensitive.
    /// </summary>
    private const string UpperCspell = "CSPELL:";

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
    ///     Gets the text of the ranges in which checking is switched off.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The text of each excluded range in the order the scan reports them.</returns>
    private static string[] Excluded(string text) =>
        [.. DocumentDirectives.Scan(text).ExcludedRanges.Select(range => text.Substring(range.Start, range.Length))];

    /// <summary>
    ///     Gets the text of the ranges of the unsupported directives.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The text of each reported directive name in order.</returns>
    private static string[] Unsupported(string text) =>
        [.. DocumentDirectives.Scan(text).UnsupportedDirectives.Select(range => text.Substring(range.Start, range.Length))];

    /// <summary>
    ///     Cuts the text from the start of one marker to the end of another.
    /// </summary>
    /// <param name="text">The text to cut from.</param>
    /// <param name="first">The marker where the cut starts; the first occurrence is used.</param>
    /// <param name="last">The marker where the cut ends; the first occurrence after <paramref name="first"/> is used.</param>
    /// <returns>The text from the start of <paramref name="first"/> to the end of <paramref name="last"/>.</returns>
    private static string Between(string text, string first, string last)
    {
        var start = text.IndexOf(first, StringComparison.Ordinal);
        var end = text.IndexOf(last, start, StringComparison.Ordinal) + last.Length;
        return text.Substring(start, end - start);
    }

    /// <summary>
    ///     Joins lines with a line terminator.
    /// </summary>
    /// <param name="lineEnding">The line terminator to put between the lines.</param>
    /// <param name="lines">The lines.</param>
    /// <returns>The joined text, with no terminator after the last line.</returns>
    private static string Lines(string lineEnding, params string[] lines) => string.Join(lineEnding, lines);

    /// <summary>
    ///     Test that a disable and enable block hides the text between them and nothing outside them.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_DisableEnableBlock_ExcludesFromDisableToEnable(string lineEnding)
    {
        // Arrange: text before the directive, a hidden line, and text after the enable on its line
        var text = Lines(lineEnding, "visible one // " + Cspell + "disable", "hidden words", "// " + Cspell + "enable visible two");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the block runs from the disable keyword to the enable keyword
        Assert.Equal([Between(text, Cspell + "disable", Cspell + "enable")], excluded);
    }

    /// <summary>
    ///     Test that a block whose disable and enable are on one line hides only the text between them.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_DisableAndEnableOnOneLine_ExcludesOnlyBetweenThem()
    {
        // Arrange: a line with text before, between, and after the two directives
        const string block = Cspell + "disable beta " + Cspell + "enable";
        const string text = "alpha " + block + " gamma";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the words before the disable and after the enable stay checked
        Assert.Equal([block], excluded);
    }

    /// <summary>
    ///     Test that each block of a text is hidden separately.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_SeveralBlocks_AreEachExcluded()
    {
        // Arrange: two blocks with visible text between them
        const string first = Cspell + "disable one " + Cspell + "enable";
        const string second = Cspell + "disable two " + Cspell + "enable";
        const string text = first + " visible " + second;

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: both blocks are hidden and the text between them is not
        Assert.Equal([first, second], excluded);
    }

    /// <summary>
    ///     Test that an enable directive with no disable before it hides nothing.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_EnableWithoutDisable_ExcludesNothing()
    {
        // Arrange: a text with an enable directive only
        const string text = "alpha " + Cspell + "enable beta";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the scan is the shared empty scan
        Assert.Same(DirectiveScan.Empty, scan);
    }

    /// <summary>
    ///     Test that the enable directive closes a block in any letter case.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_EnableInDifferentLetterCase_ClosesBlock()
    {
        // Arrange: an upper-case disable and a lower-case enable
        const string block = UpperCspell + "DISABLE hidden " + Cspell + "enable";
        const string text = "alpha " + block + " visible";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the block ends at the enable
        Assert.Equal([block], excluded);
    }

    /// <summary>
    ///     Test that an enable directive with another prefix does not close a block.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_EnableWithOtherPrefix_DoesNotCloseBlock()
    {
        // Arrange: a block opened with spell-checker, an enable with spell, and a matching enable
        const string opening = "spell-checker:" + "disable";
        const string closing = "spell-checker:" + "enable";
        var text = Lines("\n", "x " + opening, "one", "spell:" + "enable", "two", closing, "three");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the block ends at the enable with the same prefix, so "two" is hidden and "three" is not
        Assert.Equal([Between(text, opening, closing)], excluded);
    }

    /// <summary>
    ///     Test that an enable directive whose prefix is the tail of the disable prefix does not close the block.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_EnableWithShorterPrefix_DoesNotCloseBlock()
    {
        // Arrange: a block opened with cspell and an enable written with spell
        const string block = Cspell + "disable hidden spell:" + "enable still hidden";
        const string text = "alpha " + block;

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the block is not closed and runs to the end of the text
        Assert.Equal([block], excluded);
    }

    /// <summary>
    ///     Test that a block with no enable hides the text to the end.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_DisableWithoutEnable_ExcludesToEndOfText(string lineEnding)
    {
        // Arrange: a visible line and a disable directive followed by more lines
        var text = Lines(lineEnding, "visible", "// " + Cspell + "disable", "hidden one", "hidden two");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: everything from the directive to the end is hidden
        Assert.Equal([text[text.IndexOf(Cspell + "disable", StringComparison.Ordinal)..]], excluded);
    }

    /// <summary>
    ///     Test that a disable-line directive hides the whole line, including the text before it.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_DisableLine_ExcludesWholeLine(string lineEnding)
    {
        // Arrange: three lines, the second with text before and after the directive
        const string hiddenLine = "text before // " + Cspell + "disable-line text after";
        var text = Lines(lineEnding, "first line", hiddenLine, "third line");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: only the second line is hidden, without its line terminator
        Assert.Equal([hiddenLine], excluded);
    }

    /// <summary>
    ///     Test that a disable-line directive on the only line, with no terminator, hides that line.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_DisableLineOnLastLineWithoutTerminator_ExcludesLine()
    {
        // Arrange: a single line with the directive at its end
        const string text = "alpha // " + Cspell + "disable-line";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the whole text is hidden
        Assert.Equal([text], excluded);
    }

    /// <summary>
    ///     Test that a line separator character ends the line that a disable-line directive hides.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_DisableLineBeforeLineSeparator_EndsAtSeparator()
    {
        // Arrange: a directive line followed by a Unicode line separator and another line
        const string hiddenLine = "alpha " + Cspell + "disable-line beta";
        const string text = hiddenLine + "\u2028next";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the next line is not hidden
        Assert.Equal([hiddenLine], excluded);
    }

    /// <summary>
    ///     Test that a disable-next-line directive hides the rest of its line and the whole next line.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_DisableNextLine_ExcludesRestOfLineAndNextLine(string lineEnding)
    {
        // Arrange: a directive after code on its line, a next line, and a line after that
        var text = Lines(lineEnding, "code before // " + Cspell + "disable-next-line trailing", "hidden line", "visible again");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the range starts at the directive and ends with the next line
        Assert.Equal([Between(text, Cspell + "disable-next-line", "hidden line")], excluded);
    }

    /// <summary>
    ///     Test that the short form disable-next hides the next line like disable-next-line.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_DisableNextShortForm_ExcludesNextLine()
    {
        // Arrange: the short form followed by a next line and a line after that
        var text = Lines("\n", "// " + Cspell + "disable-next", "hidden line", "visible again");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the next line is hidden and the one after it is not
        Assert.Equal([Between(text, Cspell + "disable-next", "hidden line")], excluded);
    }

    /// <summary>
    ///     Test that a disable-next-line directive on the last line, with no terminator, hides nothing.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_DisableNextLineOnLastLineWithoutTerminator_ExcludesNothing()
    {
        // Arrange: a directive on the last line, so there is no next line
        const string text = "alpha // " + Cspell + "disable-next-line";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the scan is the shared empty scan
        Assert.Same(DirectiveScan.Empty, scan);
    }

    /// <summary>
    ///     Test that with LF line breaks a blank next line is hidden together with the line after it.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_BlankNextLineWithLf_ExcludesBlankAndFollowingLine()
    {
        // Arrange: a directive, a blank line, a line after it, and a last line, all separated by LF
        var text = Lines("\n", "// " + Cspell + "disable-next-line", string.Empty, "following line", "visible");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the line break after the blank line is covered, so the following line is hidden too
        Assert.Equal([Between(text, Cspell + "disable-next-line", "following line")], excluded);
    }

    /// <summary>
    ///     Test that with CRLF line breaks a blank next line is the only line hidden.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_BlankNextLineWithCrLf_ExcludesOnlyBlankLine()
    {
        // Arrange: a directive, a blank line, a line after it, and a last line, all separated by CRLF
        var text = Lines("\r\n", "// " + Cspell + "disable-next-line", string.Empty, "following line", "visible");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the range ends with the line break of the directive line, so the following line is visible
        Assert.Equal([Between(text, Cspell + "disable-next-line", "\r\n")], excluded);
    }

    /// <summary>
    ///     Test that every ignore form adds its words to the ignored words of the document.
    /// </summary>
    /// <param name="keyword">The directive keyword after the prefix.</param>
    [Theory]
    [InlineData("ignore")]
    [InlineData("ignoreWord")]
    [InlineData("ignoreWords")]
    [InlineData("ignore-word")]
    [InlineData("ignore-words")]
    public void DocumentDirectives_Scan_IgnoreDirectiveForms_AddIgnoredWords(string keyword)
    {
        // Arrange: an ignore directive in the given form that lists two words
        var text = $"// {Cspell}{keyword} alpha beta";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the words are ignored words and not document words
        Assert.Equal(["alpha", "beta"], scan.IgnoredWords.ToArray());
        Assert.Empty(scan.Words);
    }

    /// <summary>
    ///     Test that both words forms add their words to the words of the document.
    /// </summary>
    /// <param name="keyword">The directive keyword after the prefix.</param>
    [Theory]
    [InlineData("words")]
    [InlineData("word")]
    public void DocumentDirectives_Scan_WordsDirectiveForms_AddWords(string keyword)
    {
        // Arrange: a words directive in the given form that lists two words
        var text = $"// {Cspell}{keyword} alpha beta";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the words are document words and not ignored words
        Assert.Equal(["alpha", "beta"], scan.Words.ToArray());
        Assert.Empty(scan.IgnoredWords);
    }

    /// <summary>
    ///     Test that commas, semicolons, and white space separate the words of a directive.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_WordSeparators_AreCommaSemicolonAndWhiteSpace()
    {
        // Arrange: words separated by each kind of separator, with doubled separators
        const string text = Cspell + "words alpha, beta;gamma\tdelta  epsilon,,;zeta";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: six words and no empty items
        Assert.Equal(["alpha", "beta", "gamma", "delta", "epsilon", "zeta"], scan.Words.ToArray());
    }

    /// <summary>
    ///     Test that the prefix and keyword are case-insensitive while the words keep their letter case.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_WordDirectiveLetterCase_IsInsensitiveForKeywordsOnly()
    {
        // Arrange: an upper-case ignore directive and a mixed-case words directive
        var text = Lines("\n", UpperCspell + "IGNORE Alpha", "CSpell:" + "Words Beta");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: both directives are recognized and the words are unchanged
        Assert.Equal(["Alpha"], scan.IgnoredWords.ToArray());
        Assert.Equal(["Beta"], scan.Words.ToArray());
    }

    /// <summary>
    ///     Test that the words of several directives are collected in the order they appear.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_SeveralWordDirectives_AppendWordsInOrder()
    {
        // Arrange: two words directives and two ignore directives interleaved
        var text = Lines(
            "\n",
            Cspell + "words one",
            Cspell + "ignore two",
            Cspell + "words three four",
            Cspell + "ignore five");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: each list holds its words in text order
        Assert.Equal(["one", "three", "four"], scan.Words.ToArray());
        Assert.Equal(["two", "five"], scan.IgnoredWords.ToArray());
    }

    /// <summary>
    ///     Test that a words directive ends at the end of its line, whatever the line terminator.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_WordsDirectiveAtLineEnd_EndsAtLineTerminator(string lineEnding)
    {
        // Arrange: a words directive followed by a line of ordinary text
        var text = Lines(lineEnding, Cspell + "words alpha beta", "gamma");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the words of the next line are not taken and no terminator stays in a word
        Assert.Equal(["alpha", "beta"], scan.Words.ToArray());
    }

    /// <summary>
    ///     Test that a word directive without any word adds nothing.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_WordDirectiveWithoutWords_AddsNoWords()
    {
        // Arrange: directives with a keyword and no words
        var text = Lines("\n", Cspell + "ignore", Cspell + "words ,; ");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: no word is added
        Assert.Empty(scan.IgnoredWords);
        Assert.Empty(scan.Words);
    }

    /// <summary>
    ///     Test that the text of a word directive is excluded from checking, but the text before it is not.
    /// </summary>
    /// <param name="lineEnding">The line terminator of the text.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DocumentDirectives_Scan_WordDirectiveText_IsExcluded(string lineEnding)
    {
        // Arrange: a words directive after text, followed by another line
        const string directive = Cspell + "ignore beta gamma";
        var text = Lines(lineEnding, "alpha // " + directive, "next");

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the directive, from its prefix to the end of its line, is excluded
        Assert.Equal([directive], excluded);
    }

    /// <summary>
    ///     Test that a words directive inside a disabled block still adds its words.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_WordsDirectiveInsideDisabledBlock_StillAddsWords()
    {
        // Arrange: a words directive between a disable and an enable directive
        var text = Lines("\n", Cspell + "disable", Cspell + "words alpha", Cspell + "enable");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the word is added for the whole document
        Assert.Equal(["alpha"], scan.Words.ToArray());
    }

    /// <summary>
    ///     Test that a keyword continued by more letters is not a words directive.
    /// </summary>
    /// <param name="text">A text with a directive-like keyword that is longer than a real one.</param>
    [Theory]
    [InlineData(Cspell + "wordsmith alpha")]
    [InlineData(Cspell + "ignored alpha")]
    [InlineData(Cspell + "words-x alpha")]
    public void DocumentDirectives_Scan_KeywordContinuedByLetters_AddsNoWords(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: no word is added
        Assert.Empty(scan.IgnoredWords);
        Assert.Empty(scan.Words);
    }

    /// <summary>
    ///     Test that every prefix, in any letter case, with or without space after the colon, starts a disable line.
    /// </summary>
    /// <param name="prefix">The prefix as written, including its colon and any following space.</param>
    [Theory]
    [InlineData("cspell:")]
    [InlineData("spell:")]
    [InlineData("spellchecker:")]
    [InlineData("spell-checker:")]
    [InlineData("textlint:")]
    [InlineData("CSpell:")]
    [InlineData("SPELL-CHECKER:")]
    [InlineData("SpellChecker:")]
    [InlineData("TextLint:")]
    [InlineData("cspell: ")]
    [InlineData("cspell::")]
    [InlineData("spell:: ")]
    public void DocumentDirectives_Scan_AnyPrefixForm_StartsDisableLine(string prefix)
    {
        // Arrange: a line that holds a disable-line directive with the given prefix
        var text = $"alpha {prefix}disable-line beta";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the whole line is hidden
        Assert.Equal([text], excluded);
    }

    /// <summary>
    ///     Test that every prefix form starts a disable block that its own enable directive closes.
    /// </summary>
    /// <param name="prefix">The prefix as written, including its colon and any following space.</param>
    [Theory]
    [InlineData("cspell:")]
    [InlineData("spell:")]
    [InlineData("spellchecker:")]
    [InlineData("spell-checker:")]
    [InlineData("textlint:")]
    [InlineData("CSpell:")]
    [InlineData("TextLint:")]
    [InlineData("cspell: ")]
    [InlineData("cspell::")]
    public void DocumentDirectives_Scan_AnyPrefixForm_StartsAndEndsBlock(string prefix)
    {
        // Arrange: a block opened and closed with the given prefix, with text around it
        var text = $"alpha {prefix}disable hidden {prefix}enable gamma";

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: the block runs from the first prefix to the end of the enable keyword
        Assert.Equal([Between(text, prefix, "enable")], excluded);
    }

    /// <summary>
    ///     Test that every prefix form starts a words directive.
    /// </summary>
    /// <param name="prefix">The prefix as written, including its colon and any following space.</param>
    [Theory]
    [InlineData("cspell:")]
    [InlineData("spell:")]
    [InlineData("spellchecker:")]
    [InlineData("spell-checker:")]
    [InlineData("textlint:")]
    [InlineData("SPELL-CHECKER:")]
    [InlineData("TextLint:")]
    [InlineData("cspell: ")]
    [InlineData("cspell::")]
    [InlineData("spell:: ")]
    public void DocumentDirectives_Scan_AnyPrefixForm_StartsWordsDirective(string prefix)
    {
        // Arrange: a words directive with the given prefix
        var text = $"// {prefix}words alpha";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the word is added
        Assert.Equal(["alpha"], scan.Words.ToArray());
    }

    /// <summary>
    ///     Test that text that resembles a directive, and names that are not directives, are ignored.
    /// </summary>
    /// <param name="text">A text that is not a directive.</param>
    [Theory]
    [InlineData(Cspell + "disabled")]
    [InlineData(Cspell + "disable-lines")]
    [InlineData(Cspell + "disable-next" + "line")]
    [InlineData(Cspell + "disable-foo")]
    [InlineData(Cspell + "disable_x")]
    [InlineData("x" + Cspell + "disable")]
    [InlineData("x" + Cspell + "disable-line")]
    [InlineData("my_" + Cspell + "disable")]
    [InlineData("spelling:" + "disable")]
    [InlineData("textlint-x:" + "disable")]
    [InlineData("cspell " + "disable")]
    [InlineData(Cspell)]
    [InlineData(Cspell + "enable")]
    [InlineData(Cspell + "mystery")]
    [InlineData(Cspell + "locals en")]
    [InlineData(Cspell + "locales en")]
    [InlineData(Cspell + "locale-x en")]
    [InlineData(Cspell + "languages en")]
    public void DocumentDirectives_Scan_TextResemblingDirective_IsNotDirective(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: the scan is the shared empty scan
        Assert.Same(DirectiveScan.Empty, scan);
    }

    /// <summary>
    ///     Test that each unsupported directive is reported at the position of its name.
    /// </summary>
    /// <param name="text">A text that holds one unsupported directive.</param>
    /// <param name="name">The name of the directive as written.</param>
    [Theory]
    [InlineData("// " + Cspell + "flag alpha", "flag")]
    [InlineData("// " + Cspell + "flagWords alpha", "flagWords")]
    [InlineData("// " + Cspell + "forbid alpha", "forbid")]
    [InlineData("// " + Cspell + "forbid-word alpha", "forbid-word")]
    [InlineData("// " + Cspell + "locale en-GB", "locale")]
    [InlineData("// " + Cspell + "local en-GB", "local")]
    [InlineData("// " + Cspell + "language en-GB", "language")]
    [InlineData("// " + Cspell + "dictionary custom", "dictionary")]
    [InlineData("// " + Cspell + "dictionaries custom", "dictionaries")]
    [InlineData("// " + Cspell + "ignoreRegExp /alpha/", "ignoreRegExp")]
    [InlineData("// " + Cspell + "includeRegExp /alpha/", "includeRegExp")]
    [InlineData("// " + Cspell + "enableCompoundWords", "enableCompoundWords")]
    [InlineData("// " + Cspell + "disableCompoundWords", "disableCompoundWords")]
    [InlineData("// " + Cspell + "enableAllowCompoundWords", "enableAllowCompoundWords")]
    [InlineData("// " + Cspell + "enableCaseSensitive", "enableCaseSensitive")]
    [InlineData("// " + Cspell + "disableCaseSensitive", "disableCaseSensitive")]
    [InlineData("// " + UpperCspell + "LOCALE en-GB", "LOCALE")]
    [InlineData("// " + Cspell + " locale en-GB", "locale")]
    [InlineData("// spell::" + " locale en-GB", "locale")]
    [InlineData("// textlint:" + "locale en-GB", "locale")]
    public void DocumentDirectives_Scan_UnsupportedDirective_IsReportedAtItsName(string text, string name)
    {
        // Arrange: the text and the expected name are passed in by the theory data

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: one directive is reported, and its range is exactly the name
        var range = Assert.Single(scan.UnsupportedDirectives);
        Assert.Equal(text.IndexOf(name, StringComparison.OrdinalIgnoreCase), range.Start);
        Assert.Equal(name, text.Substring(range.Start, range.Length));
    }

    /// <summary>
    ///     Test that an unsupported directive neither excludes text nor adds words.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_UnsupportedDirective_HasNoOtherEffect()
    {
        // Arrange: a text with one unsupported directive
        const string text = "alpha // " + Cspell + "locale en-GB beta";

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: it is only reported
        Assert.Single(scan.UnsupportedDirectives);
        Assert.Empty(scan.ExcludedRanges);
        Assert.Empty(scan.IgnoredWords);
        Assert.Empty(scan.Words);
    }

    /// <summary>
    ///     Test that the position of an unsupported directive counts UTF-16 code units after a surrogate pair.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_UnsupportedDirectiveAfterSurrogatePair_PositionCountsUtf16Units()
    {
        // Arrange: an emoji and a space before the directive
        const string text = Emoji + " " + Cspell + "locale en-GB";

        // Act: scan the text
        var range = Assert.Single(DocumentDirectives.Scan(text).UnsupportedDirectives);

        // Assert: the name starts after the emoji (two code units), the space, and the prefix
        Assert.Equal(EmojiUnits + 1 + Cspell.Length, range.Start);
    }

    /// <summary>
    ///     Test that several unsupported directives are reported in text order.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_SeveralUnsupportedDirectives_AreReportedInTextOrder()
    {
        // Arrange: three unsupported directives on separate lines
        var text = Lines("\n", Cspell + "locale en", Cspell + "flag alpha", Cspell + "dictionary custom");

        // Act: scan the text
        var names = Unsupported(text);

        // Assert: each is reported in the order written
        Assert.Equal(["locale", "flag", "dictionary"], names);
    }

    /// <summary>
    ///     Test that an unsupported directive inside a disabled block is not reported.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_UnsupportedDirectiveInsideDisabledBlock_IsNotReported()
    {
        // Arrange: an unsupported directive between a disable and an enable directive
        var text = Lines("\n", Cspell + "disable", Cspell + "locale en", Cspell + "enable");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: it is not reported
        Assert.Empty(scan.UnsupportedDirectives);
    }

    /// <summary>
    ///     Test that an unsupported directive on the line hidden by a disable-next-line directive is not reported.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_UnsupportedDirectiveOnHiddenNextLine_IsNotReported()
    {
        // Arrange: a disable-next-line directive and an unsupported directive on the next line
        var text = Lines("\n", "// " + Cspell + "disable-next-line", "// " + Cspell + "locale en");

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: it is not reported
        Assert.Empty(scan.UnsupportedDirectives);
    }

    /// <summary>
    ///     Test that unsupported directives before and after a disabled block are reported.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_UnsupportedDirectivesAroundDisabledBlock_AreReported()
    {
        // Arrange: an unsupported directive before and another after a block, and one inside it
        var text = Lines(
            "\n",
            Cspell + "locale en",
            Cspell + "disable",
            Cspell + "dictionary hidden",
            Cspell + "enable",
            Cspell + "flag alpha");

        // Act: scan the text
        var names = Unsupported(text);

        // Assert: only the two outside the block are reported
        Assert.Equal(["locale", "flag"], names);
    }

    /// <summary>
    ///     Test that a text without a directive returns the shared empty scan.
    /// </summary>
    /// <param name="text">A text that holds no directive, with and without the words the directive search looks for.</param>
    [Theory]
    [InlineData("")]
    [InlineData("plain text with nothing special")]
    [InlineData("The spelling of this word is fine")]
    [InlineData("A text that mentions the textlint tool")]
    public void DocumentDirectives_Scan_TextWithoutDirectives_ReturnsEmptyScan(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: the result is the shared empty scan
        Assert.Same(DirectiveScan.Empty, DocumentDirectives.Scan(text));
    }

    /// <summary>
    ///     Test that the empty scan holds no ranges and no words.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_EmptyScan_HoldsNothing()
    {
        // Arrange: the shared empty scan
        var scan = DirectiveScan.Empty;

        // Act / Assert: every list is empty
        Assert.Empty(scan.ExcludedRanges);
        Assert.Empty(scan.IgnoredWords);
        Assert.Empty(scan.Words);
        Assert.Empty(scan.UnsupportedDirectives);
    }

    /// <summary>
    ///     Test that offsets of excluded ranges after a surrogate pair count UTF-16 code units.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_BlockAfterSurrogatePair_RangeCountsUtf16Units()
    {
        // Arrange: an emoji and a space before a disable directive that runs to the end
        const string directive = Cspell + "disable hidden";
        const string text = Emoji + " " + directive;

        // Act: scan the text
        var range = Assert.Single(DocumentDirectives.Scan(text).ExcludedRanges);

        // Assert: the range starts after the emoji (two code units) and the space and covers the directive
        Assert.Equal(new TextRange(EmojiUnits + 1, directive.Length), range);
    }

    /// <summary>
    ///     Test that a text of one megabyte that holds the search words but no directive is scanned completely.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_OneMegabyteWithoutDirective_ReturnsEmptyScan()
    {
        // Arrange: a single line of one megabyte made of prefix-like words that are not directives
        const string unit = "spell: ";
        var text = string.Concat(Enumerable.Repeat(unit, OneMegabyte / unit.Length));

        // Act: scan the text
        var scan = DocumentDirectives.Scan(text);

        // Assert: nothing is found
        Assert.Same(DirectiveScan.Empty, scan);
    }

    /// <summary>
    ///     Test that a directive at the end of a text of one megabyte is found.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_OneMegabyteWithDirectiveAtEnd_FindsDirective()
    {
        // Arrange: a megabyte of ordinary lines followed by a disable-line directive
        const string unit = "alpha beta\n";
        const string lastLine = Cspell + "disable-line";
        var text = string.Concat(Enumerable.Repeat(unit, OneMegabyte / unit.Length)) + lastLine;

        // Act: scan the text
        var excluded = Excluded(text);

        // Assert: only the last line is hidden
        Assert.Equal([lastLine], excluded);
    }

    /// <summary>
    ///     Test that a missing text is rejected.
    /// </summary>
    [Fact]
    public void DocumentDirectives_Scan_NullText_ThrowsArgumentNullException()
    {
        // Arrange: no text

        // Act / Assert: a null text throws
        Assert.Throws<ArgumentNullException>(() => DocumentDirectives.Scan(null!));
    }
}
