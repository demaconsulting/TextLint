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

// cspell:ignore zorb quix flib afoo FEFF baab ababab xyyx xyxy metacharacters gimsu

using System.Text.RegularExpressions;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="PatternParser"/> class and the JavaScript-to-.NET translation it relies on.
/// </summary>
/// <remarks>
///     The parser is pure logic over strings and depends on nothing but the .NET base class library, so these
///     tests use the real class with no test doubles. The expected matches of the meaning tests were computed
///     with the JavaScript engine that cspell runs its patterns in, so each case is a recorded JavaScript
///     result: a translation that changes the meaning of a pattern fails here. Characters outside ASCII are
///     written as escape sequences, and the separator between several expected matches is U+0001.
/// </remarks>
public class PatternParserTests
{
    /// <summary>
    ///     The documented time limit of every pattern, in milliseconds.
    /// </summary>
    private const int DocumentedTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The documented longest pattern body, in characters.
    /// </summary>
    private const int DocumentedMaximumLength = 4096;

    /// <summary>
    ///     The documented deepest nesting of groups.
    /// </summary>
    private const int DocumentedMaximumNesting = 100;

    /// <summary>
    ///     The documented longest translation, in characters.
    /// </summary>
    private const int DocumentedMaximumTranslation = 262144;

    /// <summary>
    ///     The time, in seconds, in which a pattern over the translation limit must be refused; it takes milliseconds.
    /// </summary>
    private const int DocumentedTranslationSeconds = 2;

    /// <summary>
    ///     A number of case-insensitive properties that is within the translation limit.
    /// </summary>
    private const int FewProperties = 50;

    /// <summary>
    ///     The number of translations that run at once in the concurrency test.
    /// </summary>
    private const int ParallelTranslations = 200;

    /// <summary>
    ///     The separator between several expected matches in the theory data.
    /// </summary>
    private const char MatchSeparator = '\u0001';

    /// <summary>
    ///     Finds every non-empty match of a pattern in a text, as the checker does.
    /// </summary>
    /// <param name="entry">The pattern entry.</param>
    /// <param name="input">The text to search.</param>
    /// <returns>The matches joined by <see cref="MatchSeparator"/>.</returns>
    private static string Matches(string entry, string input)
    {
        var regex = PatternParser.Parse(entry);
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
    ///     Parses an entry that must be rejected and returns the exception.
    /// </summary>
    /// <param name="entry">The pattern entry.</param>
    /// <returns>The exception thrown.</returns>
    private static InvalidPatternException Rejected(string entry) =>
        Assert.Throws<InvalidPatternException>(() => PatternParser.Parse(entry));

    /// <summary>
    ///     Test that a slash literal uses only the flags that are written, so it is case-sensitive without <c>i</c>.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_SlashForm_UsesOnlyTheWrittenFlags()
    {
        // Arrange: a literal without flags and the same literal with the case flag
        const string text = "ZORB zorb";

        // Act: find the matches of each
        var sensitive = Matches("/zorb/", text);
        var insensitive = Matches("/zorb/i", text);

        // Assert: the first matches only the exact case
        Assert.Multiple(
            () => Assert.Equal("zorb", sensitive),
            () => Assert.Equal("ZORB" + MatchSeparator + "zorb", insensitive));
    }

    /// <summary>
    ///     Test that a bare expression is case-insensitive and treats the line anchors as line starts and ends.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_BareForm_IsCaseInsensitiveAndMultiline()
    {
        // Arrange: a bare expression with a line start, and one with a line end
        // Act: find the matches in texts with several lines
        var start = Matches("^zorb", "a\nZORB");
        var end = Matches("zorb$", "zorb\nx");

        // Assert: both anchors work at every line, and the case is ignored
        Assert.Multiple(
            () => Assert.Equal("ZORB", start),
            () => Assert.Equal("zorb", end));
    }

    /// <summary>
    ///     Test that white space around an entry is ignored and that the body may hold unescaped slashes.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_WhiteSpaceAndSlashesInBody_AreHandled()
    {
        // Arrange: a literal with surrounding white space, and a body with a slash inside
        // Act: find the matches
        var spaced = Matches("  /zorb/g  ", "zorb");
        var inner = Matches("/a/b/g", "a/b");

        // Assert: both literals work
        Assert.Multiple(
            () => Assert.Equal("zorb", spaced),
            () => Assert.Equal("a/b", inner));
    }

    /// <summary>
    ///     Test that the white space trimmed from an entry is the JavaScript set, not the .NET one.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_EntryEdges_TrimsOnlyJavaScriptWhiteSpace()
    {
        // Arrange: a literal with the byte order mark and a no-break space around it, which JavaScript trims, and a bare
        // expression with the next-line character around it, which JavaScript does not trim
        // Act: find the matches
        var trimmed = Matches("\uFEFF\u00A0/zorb/\u00A0\uFEFF", "zorb");
        var kept = Matches("\u0085zorb\u0085", "zorb \u0085zorb\u0085");

        // Assert: the literal is read as a literal, and the next-line characters stay part of the expression
        Assert.Multiple(
            () => Assert.Equal("zorb", trimmed),
            () => Assert.Equal("\u0085zorb\u0085", kept));
    }

    /// <summary>
    ///     Test that text that starts with a slash but does not end in flags is a bare expression, such as a path.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_LeadingSlashWithoutFlagsEnding_IsBareExpression()
    {
        // Arrange: a path-like entry and a lone slash
        // Act: find the matches
        var path = Matches("/usr/local", "/usr/local");
        var lone = Matches("/", "a/b");

        // Assert: both are read as literal text
        Assert.Multiple(
            () => Assert.Equal("/usr/local", path),
            () => Assert.Equal("/", lone));
    }

    /// <summary>
    ///     Test that the supported flags are accepted in any combination, including repeats.
    /// </summary>
    /// <param name="entry">An entry that uses supported flags.</param>
    [Theory]
    [InlineData("/zorb/g")]
    [InlineData("/zorb/i")]
    [InlineData("/zorb/m")]
    [InlineData("/zorb/u")]
    [InlineData("/zorb/s")]
    [InlineData("/zorb/gimsu")]
    [InlineData("/zorb/gg")]
    public void PatternParser_Parse_SupportedFlags_AreAccepted(string entry)
    {
        // Arrange: the entry is passed in by the theory data
        // Act: parse it
        var regex = PatternParser.Parse(entry);

        // Assert: a regular expression is returned
        Assert.NotNull(regex);
    }

    /// <summary>
    ///     Test that a flag without a safe equivalent is rejected and named.
    /// </summary>
    /// <param name="flag">The flag letter.</param>
    [Theory]
    [InlineData('x')]
    [InlineData('y')]
    [InlineData('d')]
    [InlineData('v')]
    public void PatternParser_Parse_UnsupportedFlag_ThrowsNamingTheFlag(char flag)
    {
        // Arrange: an entry with an unsupported flag
        var entry = $"/zorb/g{flag}";

        // Act: parse it
        var exception = Rejected(entry);

        // Assert: the exception names the entry and the flag
        Assert.Multiple(
            () => Assert.Equal(entry, exception.Pattern),
            () => Assert.Contains($"'{flag}'", exception.Reason, StringComparison.Ordinal),
            () => Assert.Contains(entry, exception.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the regular expression gets the invariant culture, the case option, and the documented time limit.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_Result_HasInvariantCultureCaseOptionAndTimeLimit()
    {
        // Arrange: a case-sensitive and a case-insensitive entry
        // Act: parse them
        var sensitive = PatternParser.Parse("/zorb/m");
        var insensitive = PatternParser.Parse("/zorb/i");

        // Assert: the options and the limit are as documented; the multiline flag is written into the translation
        Assert.Multiple(
            () => Assert.Equal(RegexOptions.CultureInvariant, sensitive.Options),
            () => Assert.Equal(RegexOptions.CultureInvariant, insensitive.Options),
            () => Assert.Equal("ZORB", Matches("/zorb/i", "ZORB")),
            () => Assert.Equal(string.Empty, Matches("/zorb/", "ZORB")),
            () => Assert.Equal(TimeSpan.FromMilliseconds(DocumentedTimeoutMilliseconds), sensitive.MatchTimeout),
            () => Assert.Equal(DocumentedTimeoutMilliseconds, PatternParser.MatchTimeoutMilliseconds));
    }

    /// <summary>
    ///     Test that a construct keeps its JavaScript meaning, as recorded from the JavaScript engine.
    /// </summary>
    /// <param name="entry">The pattern entry as written in the settings.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The expected non-empty matches, separated by U+0001.</param>
    [Theory]
    [InlineData("/\\w+/", "\u00E91_a \u00E9", "1_a")]
    [InlineData("/\\W/", "\u00E9", "\u00E9")]
    [InlineData("/[\\W]/", "\u00E9a", "\u00E9")]
    [InlineData("/[^\\W]/", "\u00E9a", "a")]
    [InlineData("/\\d+/", "\u066312", "12")]
    [InlineData("/\\D+/", "\u0663a12", "\u0663a")]
    [InlineData("/\\bfoo/", "\u00E9foo afoo", "foo")]
    [InlineData("/\\Bfoo/", "\u00E9foo afoo", "foo")]
    [InlineData("/\\s/", "\uFEFF", "\uFEFF")]
    [InlineData("/\\s/", "\u00A0", "\u00A0")]
    [InlineData("/\\s/", "\u0085", "")]
    [InlineData("/\\S/", "\u0085", "\u0085")]
    [InlineData("/[\\S]/", "\u0085 \uFEFF", "\u0085")]
    [InlineData("/a.b/", "a\nb a\rb a\u2028b a\u2029b a\u0085b axb", "a\u0085b\u0001axb")]
    [InlineData("/a.b/s", "a\nb a\rb a\u2028b a\u2029b a\u0085b axb", "a\nb\u0001a\rb\u0001a\u2028b\u0001a\u2029b\u0001a\u0085b\u0001axb")]
    [InlineData("/a.*b/s", "a\n\nb", "a\n\nb")]
    [InlineData("/a.*b/", "a\n\nb", "")]
    [InlineData("/a.*b/gs", "a\r\nb x\u2028b", "a\r\nb x\u2028b")]
    [InlineData("/A.B/is", "a\nb", "a\nb")]
    [InlineData("/^a.b$/ms", "x\na\nb\ny", "a\nb")]
    [InlineData("/^a.b$/s", "a\nb", "a\nb")]
    [InlineData("/^a.b$/", "a\nb", "")]
    [InlineData("/a[^x]b/s", "a\nb", "a\nb")]
    [InlineData("/a\\.b/s", "a\nb a.b", "a.b")]
    [InlineData("/a(.)\\1b/s", "a\n\nb aa\n", "a\n\nb")]
    [InlineData("/\\p{L}.\\p{L}/su", "a\nb", "a\nb")]
    [InlineData("/^a$/", "a\n", "")]
    [InlineData("/^a$/", "a", "a")]
    [InlineData("/^a$/m", "b\na\nc", "a")]
    [InlineData("/^a$/m", "a\r\nb", "a")]
    [InlineData("/^b/m", "a\u2028b", "b")]
    [InlineData("/a$/m", "a\u2029b", "a")]
    [InlineData("/^b/", "a\nb", "")]
    [InlineData("/^/", "abc", "")]
    [InlineData("/[\\d\\s]+/", "x 1 2y", " 1 2")]
    [InlineData("/[a-c-]+/", "a-b-d", "a-b-")]
    [InlineData("/[\\d-z]+/", "1-z9y", "1-z9")]
    [InlineData("/[]/", "a", "")]
    [InlineData("/[^]/", "\n", "\n")]
    [InlineData("/[\\b]/", "a\bb", "\b")]
    [InlineData("/[\\s\\S]+/", "a\nb", "a\nb")]
    [InlineData("/[.*+?^${}()|[\\]\\\\]/", "a.b", ".")]
    [InlineData("/[a-z]+/i", "ABC", "ABC")]
    [InlineData("/(a)\\1/", "aa ab", "aa")]
    [InlineData("/(?<n>a)\\k<n>/", "aa ab", "aa")]
    [InlineData("/(?<a>x)(y)\\2/", "xyy xyx", "xyy")]
    [InlineData("/(?<=a)b/", "ab cb", "b")]
    [InlineData("/(?<!a)b/", "ab cb", "b")]
    [InlineData("/a(?=b)/", "ab ac", "a")]
    [InlineData("/a(?!b)/", "ab ac", "a")]
    [InlineData("/a{2}/", "aaa", "aa")]
    [InlineData("/a{2,}/", "aaaa", "aaaa")]
    [InlineData("/a{2,3}?/", "aaaa", "aa\u0001aa")]
    [InlineData("/a{,3}/", "a{,3} aaa", "a{,3}")]
    [InlineData("/a{/", "a{ a", "a{")]
    [InlineData("/{abc}/", "{abc}", "{abc}")]
    [InlineData("/a{1/", "a{1", "a{1")]
    [InlineData("/a}/", "a}", "a}")]
    [InlineData("/]/", "a]", "]")]
    [InlineData("/a+?/", "aaa", "a\u0001a\u0001a")]
    [InlineData("/a*/", "baab", "aa")]
    [InlineData("/(?:ab)+/", "ababab", "ababab")]
    [InlineData("/a|b|/", "abc", "a\u0001b")]
    [InlineData("/(?:a|b)c/", "bc", "bc")]
    [InlineData("/\\x41/", "A", "A")]
    [InlineData("/\\u0041/", "A", "A")]
    [InlineData("/\\u00e9/", "\u00E9", "\u00E9")]
    [InlineData("/\\u{1F600}/u", "\uD83D\uDE00", "\uD83D\uDE00")]
    [InlineData("/\\cJ/", "a\nb", "\n")]
    [InlineData("/\\0/", "a\u0000b", "\u0000")]
    [InlineData("/\\//", "a/b", "/")]
    [InlineData("/\\./", "a.b", ".")]
    [InlineData("/a\\-b/", "a-b", "a-b")]
    [InlineData("/\\t\\n\\r\\f\\v/", "\t\n\r\f\v", "\t\n\r\f\v")]
    [InlineData("/\\p{L}+/u", "\u00DCn\u00EF1", "\u00DCn\u00EF")]
    [InlineData("/\\P{L}+/u", "ab12cd", "12")]
    [InlineData("/[\\p{L}\\d]+/u", "ab12-", "ab12")]
    [InlineData("/\\p{Lu}/u", "aB\u00C9", "B\u0001\u00C9")]
    [InlineData("/\u00E9/i", "\u00C9", "\u00C9")]
    [InlineData("/\uD83D\uDE00+/u", "\uD83D\uDE00\uD83D\uDE00", "\uD83D\uDE00\uD83D\uDE00")]
    [InlineData("/\uD83D\uDE00+/", "\uD83D\uDE00\uD83D\uDE00", "\uD83D\uDE00\u0001\uD83D\uDE00")]
    public void PatternParser_Parse_Construct_KeepsItsJavaScriptMeaning(string entry, string input, string expected)
    {
        // Arrange: the entry, the text, and the matches recorded from the JavaScript engine are passed in by the theory data
        // Act: parse the entry and find its matches
        var matches = Matches(entry, input);

        // Assert: the matches are the ones JavaScript finds
        Assert.Equal(expected, matches);
    }

    /// <summary>
    ///     Test that the letters that are the same when the expression ignores case are the ones JavaScript equates, with and
    ///     without the Unicode flag, as recorded from the JavaScript engine.
    /// </summary>
    /// <param name="entry">The pattern entry as written in the settings.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The expected non-empty matches, separated by U+0001.</param>
    [Theory]
    [InlineData("/k/i", "k K \u212A", "k\u0001K")]
    [InlineData("/\u212A/i", "k K \u212A", "\u212A")]
    [InlineData("/k/iu", "k K \u212A", "k\u0001K\u0001\u212A")]
    [InlineData("/s/i", "s S \u017F", "s\u0001S")]
    [InlineData("/\u017F/i", "s S \u017F", "\u017F")]
    [InlineData("/s/iu", "s S \u017F", "s\u0001S\u0001\u017F")]
    [InlineData("/\u00B5/i", "\u00B5 \u039C \u03BC", "\u00B5\u0001\u039C\u0001\u03BC")]
    [InlineData("/\u03C3/i", "\u03C3 \u03C2 \u03A3", "\u03C3\u0001\u03C2\u0001\u03A3")]
    [InlineData("/\u03C2/iu", "\u03C3 \u03C2 \u03A3", "\u03C3\u0001\u03C2\u0001\u03A3")]
    [InlineData("/\u00DF/i", "\u00DF \u1E9E", "\u00DF")]
    [InlineData("/\u00DF/iu", "\u00DF \u1E9E", "\u00DF\u0001\u1E9E")]
    [InlineData("/\u0130/i", "\u0130 i I \u0131", "\u0130")]
    [InlineData("/\u0131/i", "\u0130 i I \u0131", "\u0131")]
    [InlineData("/i/i", "\u0130 i I \u0131", "i\u0001I")]
    [InlineData("/[a-z]/i", "\u212A \u017F k", "k")]
    [InlineData("/[a-z]/iu", "\u212A \u017F k", "\u212A\u0001\u017F\u0001k")]
    [InlineData("/[^k]/i", "k K \u212A", " \u0001 \u0001\u212A")]
    [InlineData("/[^k]/iu", "k K \u212A", " \u0001 ")]
    [InlineData("/\\w/i", "\u017F \u212A s", "s")]
    [InlineData("/\\w/iu", "\u017F \u212A s", "\u017F\u0001\u212A\u0001s")]
    [InlineData("/\\W/iu", "\u017F \u212A s -", " \u0001 \u0001 \u0001-")]
    [InlineData("/\\W/i", "\u017F \u212A s -", "\u017F\u0001 \u0001\u212A\u0001 \u0001 \u0001-")]
    [InlineData("/[\\W]/iu", "\u017F \u212A s -", " \u0001 \u0001 \u0001-")]
    [InlineData("/\\bs\\b/iu", "\u017F s", "\u017F\u0001s")]
    [InlineData("/\\p{Lu}/u", "a B", "B")]
    [InlineData("/\\p{Lu}/iu", "a B 1", "a\u0001B")]
    [InlineData("/\\P{Lu}/iu", "a B 1", "a\u0001 \u0001B\u0001 \u00011")]
    [InlineData("/[\\p{Ll}\\d]/iu", "a B 1 -", "a\u0001B\u00011")]
    [InlineData("/[\u03B1-\u03C9]/i", "\u03A3 \u03C2 \u03C3 \u0391 a", "\u03A3\u0001\u03C2\u0001\u03C3\u0001\u0391")]
    [InlineData("/\uD801\uDC00/iu", "\uD801\uDC28 \uD801\uDC00", "\uD801\uDC28\u0001\uD801\uDC00")]
    [InlineData("/\uD801\uDC00/i", "\uD801\uDC28 \uD801\uDC00", "\uD801\uDC00")]
    [InlineData("/[\u00E0-\u00FE]/i", "\u00C0 \u00D7 \u00FF \u0178 \u00DE", "\u00C0\u0001\u00DE")]
    [InlineData("/\u00FF/i", "\u0178 \u00FF", "\u0178\u0001\u00FF")]
    [InlineData("/\u01C5/i", "\u01C4 \u01C5 \u01C6", "\u01C4\u0001\u01C5\u0001\u01C6")]
    [InlineData("/\u1E9B/i", "\u1E9B \u1E60 \u1E61", "\u1E9B\u0001\u1E60\u0001\u1E61")]
    [InlineData("/\u1E9B/iu", "\u1E9B \u1E60 \u1E61", "\u1E9B\u0001\u1E60\u0001\u1E61")]
    [InlineData("/\u1F80/i", "\u1F80 \u1F88", "\u1F80")]
    [InlineData("/\u1F80/iu", "\u1F80 \u1F88", "\u1F80\u0001\u1F88")]
    [InlineData("/\u212B/i", "\u00C5 \u00E5 \u212B", "\u212B")]
    [InlineData("/\u2126/iu", "\u03A9 \u03C9 \u2126", "\u03A9\u0001\u03C9\u0001\u2126")]
    [InlineData("/\uA7CE/i", "\uA7CE \uA7CF", "\uA7CE")]
    [InlineData("/\uA7CE/iu", "\uA7CE \uA7CF", "\uA7CE")]
    [InlineData("/\uA7D2/iu", "\uA7D2 \uA7D3", "\uA7D2")]
    [InlineData("/\uA7D4/iu", "\uA7D4 \uA7D5", "\uA7D4")]
    [InlineData("/\u0390/i", "\u0390 \u1FD3", "\u0390")]
    [InlineData("/\u0390/iu", "\u0390 \u1FD3", "\u0390\u0001\u1FD3")]
    [InlineData("/\u03B0/iu", "\u03B0 \u1FE3", "\u03B0\u0001\u1FE3")]
    [InlineData("/\uFB05/iu", "\uFB05 \uFB06", "\uFB05\u0001\uFB06")]
    [InlineData("/\uA7DA/i", "\uA7DA \uA7DB", "\uA7DA\u0001\uA7DB")]
    [InlineData("/\uA7DA/iu", "\uA7DA \uA7DB", "\uA7DA\u0001\uA7DB")]
    [InlineData("/\u1C89/iu", "\u1C89 \u1C8A", "\u1C89\u0001\u1C8A")]
    [InlineData("/\u019B/iu", "\u019B \uA7DC", "\u019B\u0001\uA7DC")]
    public void PatternParser_Parse_CaseFlag_KeepsTheJavaScriptLetterEquivalence(string entry, string input, string expected)
    {
        // Arrange: the entry, the text, and the matches recorded from the JavaScript engine are passed in by the theory data
        // Act: parse the entry and find its matches
        var matches = Matches(entry, input);

        // Assert: the matches are the ones JavaScript finds, where .NET's own case option would give others (the Kelvin sign,
        // the long s, the micro sign, the Greek sigmas, and the letters with several-character upper-case forms), and
        // that option is not used
        Assert.Multiple(
            () => Assert.Equal(expected, matches),
            () => Assert.Equal(RegexOptions.CultureInvariant, PatternParser.Parse(entry).Options));
    }

    /// <summary>
    ///     Test that a bare expression is case-insensitive with the JavaScript rules, and that no letter case is applied
    ///     to an expression written without the flag.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_BareExpression_UsesTheJavaScriptCaseRules()
    {
        // Arrange: a bare expression with the letter k, and the same as a literal without the flag
        const string text = "k K \u212A";

        // Act: find the matches
        var bare = Matches("k", text);
        var sensitive = Matches("/k/", text);

        // Assert: the bare expression matches both ASCII letters but not the Kelvin sign; the literal only the lower case
        Assert.Multiple(
            () => Assert.Equal("k\u0001K", bare),
            () => Assert.Equal("k", sensitive));
    }

    /// <summary>
    ///     Test that a back-reference to a group that did not take part in the match fails, unlike in JavaScript.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_BackReferenceToUnmatchedGroup_DoesNotMatch()
    {
        // Arrange: a pattern whose second alternative refers to a group of the first, which JavaScript matches as empty
        const string entry = "/(a)|b\\1/";

        // Act: find its matches in "b"
        var matches = Matches(entry, "b");

        // Assert: the documented difference: .NET does not match, where JavaScript matches the letter
        Assert.Equal(string.Empty, matches);
    }

    /// <summary>
    ///     Test that every capture group, named or not, is numbered in order of its opening parenthesis.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_NamedAndPlainGroups_AreNumberedInOrder()
    {
        // Arrange: a named group followed by a plain group that a numbered reference refers to
        const string entry = "/(?<first>x)(y)\\2\\1/";

        // Act: find its matches in a text where the references repeat the plain group and then the named group
        var matches = Matches(entry, "xyyx xyxy");

        // Assert: only the first text matches
        Assert.Equal("xyyx", matches);
    }

    /// <summary>
    ///     Test that an astral literal is one character with the Unicode flag, and its last code unit without it.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_AstralLiteralWithQuantifier_FollowsTheUnicodeFlag()
    {
        // Arrange: a character outside the basic plane, doubled
        const string text = "\uD83D\uDE00\uD83D\uDE00";

        // Act: find the longest match with and without the Unicode flag
        var withFlag = Matches("/\uD83D\uDE00{2}/u", text);
        var withoutFlag = Matches("/\uD83D\uDE00{2}/", text);

        // Assert: with the flag both characters are one match; without it the quantifier repeats only the second code unit
        Assert.Multiple(
            () => Assert.Equal(text, withFlag),
            () => Assert.Equal(string.Empty, withoutFlag));
    }

    /// <summary>
    ///     Test that a construct that has no safe equivalent, or means something else in .NET, is rejected with its reason.
    /// </summary>
    /// <param name="entry">The entry with the construct.</param>
    /// <param name="reason">Text that the reason must contain.</param>
    [Theory]
    [InlineData("/(?i)zorb/", "group syntax")]
    [InlineData("/(?>zorb)/", "group syntax")]
    [InlineData("/(?(1)a|b)/", "group syntax")]
    [InlineData("/(?'n'a)/", "group syntax")]
    [InlineData("/(?#note)a/", "group syntax")]
    [InlineData("/(?<n>a)(?<n>b)/", "used twice")]
    [InlineData("/(?<1a>x)/", "group name")]
    [InlineData("/(?<n/", "not closed")]
    [InlineData("/\\z/", "not supported")]
    [InlineData("/\\A/", "not supported")]
    [InlineData("/\\Z/", "not supported")]
    [InlineData("/\\e/", "not supported")]
    [InlineData("/\\G/", "not supported")]
    [InlineData("/\\a/", "not supported")]
    [InlineData("/\\h/", "not supported")]
    [InlineData("/\\1/", "does not exist")]
    [InlineData("/(a)\\2/", "does not exist")]
    [InlineData("/\\1(a)/", "not complete")]
    [InlineData("/(a\\1)/", "not complete")]
    [InlineData("/\\k<a>/", "named group")]
    [InlineData("/(?<a>x)\\k<b>/", "not defined")]
    [InlineData("/(?<a>x)\\k/", "angle brackets")]
    [InlineData("/(a)\\1/i", "cannot be used with the flag 'i'")]
    [InlineData("/(?<n>a)\\k<n>/iu", "cannot be used with the flag 'i'")]
    [InlineData("(a)\\1", "cannot be used with the flag 'i'")]
    [InlineData("/\\01/", "octal")]
    [InlineData("/a**/", "quantifier")]
    [InlineData("/a++/", "quantifier")]
    [InlineData("/a???/", "quantifier")]
    [InlineData("/*a/", "quantifier")]
    [InlineData("/+a/", "quantifier")]
    [InlineData("/?a/", "quantifier")]
    [InlineData("/{2}a/", "quantifier")]
    [InlineData("/^*/", "cannot be repeated")]
    [InlineData("/$+/", "cannot be repeated")]
    [InlineData("/\\b+/", "cannot be repeated")]
    [InlineData("/(?<=a)+/", "cannot be repeated")]
    [InlineData("/(?=a)*/u", "cannot be repeated")]
    [InlineData("/a{3,2}/", "out of order")]
    [InlineData("/a{99999999999}/", "too large")]
    [InlineData("/(a/", "not closed")]
    [InlineData("/a)/", "no matching")]
    [InlineData("/[a/", "not closed")]
    [InlineData("/[a-/", "not closed")]
    [InlineData("/[b-a]/", "out of order")]
    [InlineData("/zorb\\/", "ends with a backslash")]
    [InlineData("/\\u12/", "four hexadecimal")]
    [InlineData("/\\x1/", "two hexadecimal")]
    [InlineData("/\\c1/", "followed by a letter")]
    [InlineData("/\\u{110000}/u", "code point")]
    [InlineData("/\\u{}/u", "code point")]
    [InlineData("/[\uD83D\uDE00]/", "Basic Multilingual")]
    [InlineData("/\\p{L}/", "u flag")]
    [InlineData("/\\P{L}/", "u flag")]
    [InlineData("/\\p{Script=Greek}/u", "not supported")]
    [InlineData("/\\p{Letter}/u", "not supported")]
    [InlineData("/\\p/u", "\\p{L}")]
    [InlineData("/a{/u", "must be escaped")]
    [InlineData("/a}/u", "must be escaped")]
    [InlineData("/]/u", "must be escaped")]
    [InlineData("/[\\d-z]/u", "range")]
    [InlineData("/[\\B]/", "inside")]
    [InlineData("/[\\k]/", "inside")]
    [InlineData("/[\\1]/", "inside")]
    public void PatternParser_Parse_UnsupportedConstruct_ThrowsWithReason(string entry, string reason)
    {
        // Arrange: the entry and the expected wording are passed in by the theory data
        // Act: parse it
        var exception = Rejected(entry);

        // Assert: the exception names the entry and gives a reason that explains the problem
        Assert.Multiple(
            () => Assert.Equal(entry, exception.Pattern),
            () => Assert.Contains(reason, exception.Reason, StringComparison.Ordinal),
            () => Assert.Contains(entry, exception.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a blank entry or an empty body is rejected.
    /// </summary>
    /// <param name="entry">An entry with no pattern in it.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("//")]
    [InlineData("//gi")]
    public void PatternParser_Parse_BlankOrEmptyBody_Throws(string entry)
    {
        // Arrange: the entry is passed in by the theory data
        // Act: parse it
        var exception = Rejected(entry);

        // Assert: the reason says that there is no pattern
        Assert.True(
            exception.Reason.Contains("blank", StringComparison.Ordinal) || exception.Reason.Contains("empty", StringComparison.Ordinal),
            exception.Reason);
    }

    /// <summary>
    ///     Test that a null entry is a programming error.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_NullEntry_ThrowsArgumentNullException()
    {
        // Arrange: no entry
        // Act / Assert: parsing null is rejected
        Assert.Throws<ArgumentNullException>(() => PatternParser.Parse(null!));
    }

    /// <summary>
    ///     Test that a pattern at the length limit is accepted and one character more is rejected.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_LengthLimit_IsEnforced()
    {
        // Arrange: a body of exactly the documented limit, and one that is a character longer
        var atLimit = "/" + new string('a', DocumentedMaximumLength) + "/";
        var overLimit = "/" + new string('a', DocumentedMaximumLength + 1) + "/";

        // Act: parse both
        var accepted = PatternParser.Parse(atLimit);
        var exception = Rejected(overLimit);

        // Assert: the first is accepted, the second rejected with the limit named
        Assert.Multiple(
            () => Assert.NotNull(accepted),
            () => Assert.Equal(DocumentedMaximumLength, JavaScriptRegexTranslator.MaxPatternLength),
            () => Assert.Contains(DocumentedMaximumLength.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that groups nested to the limit are accepted and one level more is rejected.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_NestingLimit_IsEnforced()
    {
        // Arrange: groups nested exactly to the documented depth, and one level deeper
        var atLimit = "/" + new string('(', DocumentedMaximumNesting) + "a" + new string(')', DocumentedMaximumNesting) + "/";
        var overLimit = "/" + new string('(', DocumentedMaximumNesting + 1) + "a" + new string(')', DocumentedMaximumNesting + 1) + "/";

        // Act: parse both
        var accepted = PatternParser.Parse(atLimit);
        var exception = Rejected(overLimit);

        // Assert: the first is accepted, the second rejected
        Assert.Multiple(
            () => Assert.NotNull(accepted),
            () => Assert.Contains("nested", exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a pattern whose translation would be too long is rejected quickly, with the reason, however it
    ///     builds up the translation: with many case-insensitive Unicode properties, with a class that collects them,
    ///     or with a negated class of them.
    /// </summary>
    /// <param name="unit">The piece that is repeated to fill the pattern up to its length limit.</param>
    [Theory]
    [InlineData("\\p{L}")]
    [InlineData("\\P{L}")]
    [InlineData("\\p{Lo}")]
    [InlineData("[\\p{L}]")]
    [InlineData("[^\\p{L}]")]
    public void PatternParser_Parse_CaseInsensitiveExpansionOverTheLimit_IsRejectedQuicklyWithReason(string unit)
    {
        // Arrange: a pattern of the longest allowed length made of the piece, with the flags that expand it
        var body = string.Concat(Enumerable.Repeat(unit, DocumentedMaximumLength / unit.Length));
        var started = System.Diagnostics.Stopwatch.StartNew();

        // Act: parse it
        var exception = Rejected("/" + body + "/iu");
        started.Stop();

        // Assert: it is refused with the limit named, and in far less time than building the regular expression would take
        Assert.Multiple(
            () => Assert.Equal(DocumentedMaximumTranslation, JavaScriptRegexTranslator.MaxTranslatedLength),
            () => Assert.Contains(
                $"longer than {DocumentedMaximumTranslation} characters",
                exception.Reason,
                StringComparison.Ordinal),
            () => Assert.True(started.Elapsed < TimeSpan.FromSeconds(DocumentedTranslationSeconds), $"Took {started.Elapsed}."));
    }

    /// <summary>
    ///     Test that patterns that need a long translation but stay within the limit are accepted, and that the limit
    ///     does not apply to an expansion that the flags do not ask for.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_TranslationWithinTheLimit_IsAccepted()
    {
        // Arrange: the longest ordinary pattern (letters with the case flag), a few properties with the case flag,
        // as many properties as fit in the source without the case flag, and many shorthands with it
        var letters = "/" + new string('a', DocumentedMaximumLength) + "/i";
        var properties = "/" + string.Concat(Enumerable.Repeat("\\p{L}", FewProperties)) + "/iu";
        var plain = "/" + string.Concat(Enumerable.Repeat("\\p{L}", DocumentedMaximumLength / 5)) + "/u";
        var shorthands = "/" + string.Concat(Enumerable.Repeat("\\W", DocumentedMaximumLength / 2)) + "/i";

        // A class that collects properties whose union is nearly every character has a short translation, however
        // many are written, so it is accepted, as is the work of building each one
        var unions = "/" + string.Concat(Enumerable.Repeat("[\\p{L}\\p{Ll}\\P{Lu}]", FewProperties)) + "/iu";

        // Act: parse them
        var parsed = new[] { letters, properties, plain, shorthands, unions }.Select(PatternParser.Parse).ToArray();

        // Assert: all are accepted
        Assert.All(parsed, regex => Assert.NotNull(regex));
    }

    /// <summary>
    ///     Test that the translation of a pattern is the same when many threads translate it at once, so that the
    ///     shared caches of the sets do not change a result.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_ConcurrentTranslations_GiveTheSameResult()
    {
        // Arrange: a pattern that uses the cached sets of a property, a negated property, and shorthands, in both modes
        string[] entries = ["/\\p{Lu}\\P{Nd}\\w\\W[\\s\\S]/iu", "/\\w\\D[\\W\\d]/i"];
        var expected = entries.Select(entry => PatternParser.Parse(entry).ToString()).ToArray();

        // Act: translate them again on many threads
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(
            0,
            ParallelTranslations,
            index => results.Add(PatternParser.Parse(entries[index % entries.Length]).ToString()));

        // Assert: every result equals the sequential one
        Assert.All(results, result => Assert.Contains(result, expected));
    }

    /// <summary>
    ///     Test that a braced code point is read as JavaScript reads it: any number of leading zeros is accepted, as
    ///     long as the value is not above 10FFFF. The results were recorded from Node.js v22.18.0.
    /// </summary>
    /// <param name="entry">The pattern entry.</param>
    /// <param name="canonical">The same pattern written without the extra leading zeros.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The expected match.</param>
    [Theory]
    [InlineData("/\\u{41}/u", "/\\u{41}/u", "A", "A")]
    [InlineData("/\\u{00000041}/u", "/\\u{41}/u", "A", "A")]
    [InlineData("/\\u{0000000000041}/u", "/\\u{41}/u", "A", "A")]
    [InlineData("/\\u{10FFFF}/u", "/\\u{10FFFF}/u", "\U0010FFFF", "\U0010FFFF")]
    [InlineData("/\\u{0000000010FFFF}/u", "/\\u{10FFFF}/u", "\U0010FFFF", "\U0010FFFF")]
    [InlineData("/\\u{00000000000000000000FFFF}/u", "/\\u{FFFF}/u", "\uFFFF", "\uFFFF")]
    public void PatternParser_Parse_BracedCodePointWithLeadingZeros_IsAccepted(
        string entry,
        string canonical,
        string input,
        string expected)
    {
        // Arrange: the entry, its form without leading zeros, and the text are passed in by the theory data
        // Act: find the matches, and translate both forms
        var matches = Matches(entry, input);
        var translated = PatternParser.Parse(entry).ToString();
        var canonicalTranslated = PatternParser.Parse(canonical).ToString();

        // Assert: the escape is the code point that its value names, and the translation does not depend on the zeros
        Assert.Multiple(
            () => Assert.Equal(expected, matches),
            () => Assert.Equal(canonicalTranslated, translated));
    }

    /// <summary>
    ///     Test that a braced code point above 10FFFF, an empty one, and one that is not made of hexadecimal digits are
    ///     rejected, however many digits it has, as in Node.js v22.18.0.
    /// </summary>
    /// <param name="entry">The pattern entry.</param>
    [Theory]
    [InlineData("/\\u{110000}/u")]
    [InlineData("/\\u{0110000}/u")]
    [InlineData("/\\u{FFFFFFFFFFFF}/u")]
    [InlineData("/\\u{00000000000000110000}/u")]
    [InlineData("/\\u{}/u")]
    [InlineData("/\\u{ 41}/u")]
    [InlineData("/\\u{-41}/u")]
    [InlineData("/\\u{+41}/u")]
    public void PatternParser_Parse_BracedCodePointOutOfRange_IsRejected(string entry)
    {
        // Arrange: the entry is passed in by the theory data
        // Act: parse it
        var exception = Rejected(entry);

        // Assert: the reason names the range of a code point
        Assert.Contains("code point from 0 to 10FFFF", exception.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a hostile pattern of many characters is rejected or accepted promptly, never by crashing.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_HostilePatterns_AreHandledWithoutErrorOtherThanInvalidPattern()
    {
        // Arrange: patterns built from repeated metacharacters, at the length limit
        var repeated = new[]
        {
            new string('(', DocumentedMaximumLength),
            new string(')', DocumentedMaximumLength),
            new string('[', DocumentedMaximumLength),
            new string('\\', DocumentedMaximumLength),
            new string('*', DocumentedMaximumLength),
            new string('{', DocumentedMaximumLength),
            string.Concat(Enumerable.Repeat("(?<", DocumentedMaximumLength / 3)),
        };

        // Act / Assert: each is parsed or rejected with the documented exception, and nothing else escapes
        Assert.All(
            repeated,
            body =>
            {
                try
                {
                    _ = PatternParser.Parse("/" + body + "/");
                }
                catch (InvalidPatternException)
                {
                    // The expected failure for a pattern that is not valid
                }
            });
    }

    /// <summary>
    ///     Test that a catastrophic pattern ends with the time-out exception of the regular expression engine.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_CatastrophicPattern_TimesOutWhenMatching()
    {
        // Arrange: a nested quantifier that backtracks without end on a text that almost matches, with a short limit
        // that the internal overload allows, so that the test is fast and the limit is the one under test
        var regex = PatternParser.Parse("/^(a|aa)+$/", TimeSpan.FromMilliseconds(50));
        var text = new string('a', 60) + "b";

        // Act / Assert: matching the text exceeds the limit and throws, instead of hanging
        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromMilliseconds(50), regex.MatchTimeout),
            () => Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(text)));
    }

    /// <summary>
    ///     Test that the production limit is five seconds and that a limit that is not positive is rejected.
    /// </summary>
    [Fact]
    public void PatternParser_Parse_MatchTimeout_DefaultsToFiveSecondsAndMustBePositive()
    {
        // Arrange / Act: parse with the default limit
        var regex = PatternParser.Parse("/zorb/");

        // Assert: the limit is five seconds, and zero, negative, and infinite limits are rejected
        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromSeconds(5), regex.MatchTimeout),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => PatternParser.Parse("/zorb/", TimeSpan.Zero)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => PatternParser.Parse("/zorb/", TimeSpan.FromMilliseconds(-5))),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => PatternParser.Parse("/zorb/", Timeout.InfiniteTimeSpan)));
    }
}
