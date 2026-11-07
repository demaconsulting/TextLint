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

// cspell:ignore zorb markdn mdown mdtext mdtxt mdwn workbook

using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="MarkdownPatterns"/> class.
/// </summary>
/// <remarks>
///     The patterns are pure logic over strings and depend on nothing but the .NET base class library, so these
///     tests use the real class with no test doubles. The expected matches were computed with the JavaScript
///     engine and the pattern definitions of the cspell version that the patterns are ported from, so each case
///     is a recorded cspell result. The separator between several expected matches is U+0001.
/// </remarks>
public class MarkdownPatternsTests
{
    /// <summary>
    ///     The documented number of Markdown patterns.
    /// </summary>
    private const int DocumentedPatternCount = 5;

    /// <summary>
    ///     The documented time limit of every pattern, in milliseconds.
    /// </summary>
    private const int DocumentedTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The separator between several expected matches in the theory data.
    /// </summary>
    private const char MatchSeparator = '\u0001';

    /// <summary>
    ///     The names of the patterns, in the order the class lists them.
    /// </summary>
    private static readonly string[] PatternNames = ["LinkReference", "LinkFooter", "Link", "Anchor", "SymbolEntity"];

    /// <summary>
    ///     Finds the matches of one named pattern in a text.
    /// </summary>
    /// <param name="name">The name of the pattern.</param>
    /// <param name="input">The text to search.</param>
    /// <returns>The matches joined by <see cref="MatchSeparator"/>.</returns>
    private static string Matches(string name, string input)
    {
        var regex = MarkdownPatterns.All[Array.IndexOf(PatternNames, name)];
        var found = new List<string>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            if (match.Length > 0)
            {
                found.Add(input.Substring(match.Index, match.Length));
            }
        }

        return string.Join(MatchSeparator, found);
    }

    /// <summary>
    ///     Test that each Markdown pattern finds what cspell's pattern finds.
    /// </summary>
    /// <param name="name">The name of the pattern.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The matches recorded from cspell, separated by U+0001.</param>
    [Theory]
    [InlineData("LinkReference", "[text][zorb-ref.one_two] and [x][a b] [y][c/d] [z][]", "[zorb-ref.one_two]\u0001[a b]")]
    [InlineData("LinkReference", "[text] [ref]", "")]
    [InlineData("LinkReference", "[a]\n[b]", "")]
    [InlineData("LinkFooter", "[ref]: https://x.example/y title", "[ref]: https://x.example/y")]
    [InlineData("LinkFooter", "[ref]:nospace", "[ref]:")]
    [InlineData("LinkFooter", "[ref]: ", "[ref]: ")]
    [InlineData("LinkFooter", "[a b]: c d", "[a b]: c")]
    [InlineData("LinkFooter", "x [ref]: y", "[ref]: y")]
    [InlineData("Link", "[t](zorb-target) [u](a b) [v]( x) [w](<angle>) ![i](img.png \"title\")", "zorb-target\u0001a\u0001<angle>\u0001img.png")]
    [InlineData("Link", "(not) [x]", "")]
    [InlineData("Anchor", "<a id=\"zorb_anchor\"></a> <a  id=\"two\"> <A ID=\"up\"> <a name=\"x\"> <a id=\"a b\">", "zorb_anchor\u0001two\u0001a")]
    [InlineData("SymbolEntity", "&clubs; &Clubs; &zorb; &a1; &x &; &amp;amp;", "&clubs;\u0001&zorb;\u0001&amp;")]
    public void MarkdownPatterns_All_Pattern_FindsWhatCspellFinds(string name, string input, string expected)
    {
        // Arrange: the pattern name, the text, and the matches recorded from cspell are passed in by the theory data
        // Act: find the matches of the pattern
        var matches = Matches(name, input);

        // Assert: the matches are the ones cspell finds
        Assert.Equal(expected, matches);
    }

    /// <summary>
    ///     Test that the patterns end and join at the characters that JavaScript counts as white space, which are not the ones
    ///     .NET counts: U+0085 is not white space and U+FEFF is, as recorded from the JavaScript engine with cspell's definitions.
    /// </summary>
    /// <param name="name">The name of the pattern.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The matches the JavaScript engine finds, separated by U+0001.</param>
    [Theory]
    [InlineData("Link", "[a](b\u0085c)", "b\u0085c")]
    [InlineData("Link", "[a](b\uFEFFc)", "b")]
    [InlineData("Link", "[a](b\u00A0c)", "b")]
    [InlineData("Link", "[a](b\u2028c)", "b")]
    [InlineData("LinkFooter", "[ref]: http://x\u0085more", "[ref]: http://x\u0085more")]
    [InlineData("LinkFooter", "[ref]: http://x\uFEFFmore", "[ref]: http://x")]
    [InlineData("LinkFooter", "[ref]: http://x\u00A0more", "[ref]: http://x")]
    [InlineData("LinkFooter", "[ref]: http://x\u2028more", "[ref]: http://x")]
    [InlineData("Anchor", "<a\u0085id=\"x\u0085y\">", "")]
    [InlineData("Anchor", "<a\uFEFFid=\"x\uFEFFy\">", "x")]
    [InlineData("Anchor", "<a\u00A0id=\"x\u00A0y\">", "x")]
    [InlineData("Anchor", "<a\u2028id=\"x\u2028y\">", "x")]
    public void MarkdownPatterns_All_JavaScriptWhiteSpace_EndsAndJoinsLikeJavaScript(string name, string input, string expected)
    {
        // Arrange: the pattern name, the text, and the matches recorded from the JavaScript engine are passed in by the theory data
        // Act: find the matches of the pattern
        var matches = Matches(name, input);

        // Assert: the matches are the ones JavaScript finds, and every pattern is built without the case option
        Assert.Multiple(
            () => Assert.Equal(expected, matches),
            () => Assert.All(MarkdownPatterns.All, regex => Assert.Equal(System.Text.RegularExpressions.RegexOptions.CultureInvariant, regex.Options)));
    }

    /// <summary>
    ///     Test that there are five patterns and every one has the documented time limit.
    /// </summary>
    [Fact]
    public void MarkdownPatterns_All_EveryPattern_HasDocumentedMatchTimeout()
    {
        // Arrange: the time limit the design documents
        var documented = TimeSpan.FromMilliseconds(DocumentedTimeoutMilliseconds);

        // Act: read the patterns
        var patterns = MarkdownPatterns.All;

        // Assert: there are five, and all have the documented limit
        Assert.Multiple(
            () => Assert.Equal(DocumentedPatternCount, patterns.Count),
            () => Assert.Equal(DocumentedPatternCount, PatternNames.Length),
            () => Assert.All(patterns, pattern => Assert.Equal(documented, pattern.MatchTimeout)));
    }

    /// <summary>
    ///     Test that the patterns apply to the file name extensions that cspell reads as Markdown, in any letter case.
    /// </summary>
    /// <param name="sourceName">A source name with a Markdown extension.</param>
    [Theory]
    [InlineData("README.md")]
    [InlineData("README.MD")]
    [InlineData("docs/guide.markdown")]
    [InlineData("docs/guide.Markdown")]
    [InlineData("a.markdn")]
    [InlineData("a.mdown")]
    [InlineData("a.mdtext")]
    [InlineData("a.mdtxt")]
    [InlineData("a.mdwn")]
    [InlineData("a.mkd")]
    [InlineData("a.workbook")]
    [InlineData("a.mdx")]
    [InlineData("C:\\work\\guide.md")]
    [InlineData("dir.md/file.md")]
    [InlineData("a.b.md")]
    public void MarkdownPatterns_AppliesTo_MarkdownExtension_IsTrue(string sourceName) =>
        Assert.True(MarkdownPatterns.AppliesTo(sourceName));

    /// <summary>
    ///     Test that the patterns do not apply to other names, to a directory that looks like a Markdown file, or to no name.
    /// </summary>
    /// <param name="sourceName">A source name without a Markdown extension, or none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notes.txt")]
    [InlineData("notes.markdown.txt")]
    [InlineData("md")]
    [InlineData(".mdx.bak")]
    [InlineData("README")]
    [InlineData("dir.md/file")]
    [InlineData("dir.md\\file")]
    [InlineData("a.mdd")]
    public void MarkdownPatterns_AppliesTo_OtherNames_IsFalse(string? sourceName) =>
        Assert.False(MarkdownPatterns.AppliesTo(sourceName));
}
