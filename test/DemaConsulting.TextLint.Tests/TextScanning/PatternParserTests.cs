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

// cspell:ignore zorb quix flib afoo FEFF baab ababab xyyx xyxy metacharacters

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
    [InlineData("/zorb/gimu")]
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
    [InlineData('s')]
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
            () => Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, insensitive.Options),
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
        // to keep the test fast: the limit is a property of the pattern, so a pattern is rebuilt with a shorter one
        var regex = PatternParser.Parse("/^(a|aa)+$/");
        var shortLimit = new Regex(regex.ToString(), regex.Options, TimeSpan.FromMilliseconds(50));
        var text = new string('a', 60) + "b";

        // Act / Assert: matching the text exceeds the limit and throws, instead of hanging
        Assert.Throws<RegexMatchTimeoutException>(() => shortLimit.IsMatch(text));
    }
}
