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

// cspell:ignore zorb quix flib urlz MIIBIjANBg predefined hexvalues thref azzbz zorbone zorbdef

using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="PatternCatalog"/> class.
/// </summary>
/// <remarks>
///     The catalog depends on <see cref="PatternParser"/> and on the .NET base class library, so these tests
///     use the real classes with no test doubles. The expected matches of the predefined patterns were computed
///     with the JavaScript engine and the pattern definitions of the cspell version that the patterns are
///     ported from, so each case is a recorded cspell result. The separator between several expected matches
///     is U+0001.
/// </remarks>
public class PatternCatalogTests
{
    /// <summary>
    ///     The documented number of predefined pattern names.
    /// </summary>
    private const int DocumentedNameCount = 28;

    /// <summary>
    ///     The documented number of predefined patterns that exclude text only when listed.
    /// </summary>
    private const int DocumentedOptionalCount = 9;

    /// <summary>
    ///     The documented time limit of every pattern, in milliseconds.
    /// </summary>
    private const int DocumentedTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The separator between several expected matches in the theory data.
    /// </summary>
    private const char MatchSeparator = '\u0001';

    /// <summary>
    ///     The predefined names that are applied to every text, so that listing one changes nothing.
    /// </summary>
    private static readonly string[] AlwaysAppliedNames =
    [
        "Urls", "Email", "RsaCert", "SshRsa", "Base64MultiLine", "Base64SingleLine", "CommitHash", "CommitHashLink",
        "CStyleHexValue", "CSSHexValue", "SHA", "HashStrings", "UnicodeRef", "UUID",
        "SpellCheckerDisable", "SpellCheckerDisableBlock", "SpellCheckerDisableLine", "SpellCheckerDisableNext",
        "SpellCheckerIgnoreInDocSetting",
    ];

    /// <summary>
    ///     Finds the matches of the patterns that a list resolves to.
    /// </summary>
    /// <param name="list">The entries of <c>ignoreRegExpList</c>.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="definitions">The named patterns, or none.</param>
    /// <returns>The text of the matches, joined by <see cref="MatchSeparator"/>.</returns>
    private static string Matches(string[] list, string input, params NamedPattern[] definitions)
    {
        var regexes = PatternCatalog.Resolve(list, definitions);
        var ranges = new List<TextRange>();
        PatternCatalog.FindExcludedRanges(regexes, input, ranges);
        return string.Join(MatchSeparator, ranges.Select(range => input.Substring(range.Start, range.Length)));
    }

    /// <summary>
    ///     Resolves lists that must be rejected and returns the exception.
    /// </summary>
    /// <param name="list">The entries of <c>ignoreRegExpList</c>.</param>
    /// <param name="definitions">The named patterns, or none.</param>
    /// <returns>The exception thrown.</returns>
    private static InvalidPatternException Rejected(string[] list, params NamedPattern[] definitions) =>
        Assert.Throws<InvalidPatternException>(() => PatternCatalog.Resolve(list, definitions));

    /// <summary>
    ///     Test that the predefined names are exactly the twenty-eight names of cspell.
    /// </summary>
    [Fact]
    public void PatternCatalog_PredefinedNames_AreTheTwentyEightNamesOfCspell()
    {
        // Arrange: the names that cspell defines in advance
        string[] expected =
        [
            .. AlwaysAppliedNames,
            "HexValues", "PublicKey", "EscapeCharacters", "Base64", "href", "string", "CStyleComment", "PhpHereDoc", "Everything",
        ];

        // Act: read the names from the catalog
        var names = PatternCatalog.PredefinedNames;

        // Assert: the catalog lists exactly those, and counts match the documentation
        Assert.Multiple(
            () => Assert.Equal(DocumentedNameCount, names.Count),
            () => Assert.Equal(expected.Order(StringComparer.OrdinalIgnoreCase), names.Order(StringComparer.OrdinalIgnoreCase)),
            () => Assert.Equal(DocumentedOptionalCount, PatternCatalog.OptionalRegexes.Count));
    }

    /// <summary>
    ///     Test that every regular expression of the optional predefined patterns has the documented time limit.
    /// </summary>
    [Fact]
    public void PatternCatalog_OptionalRegexes_EveryPattern_HasDocumentedMatchTimeout()
    {
        // Arrange: the time limit the design documents
        var documented = TimeSpan.FromMilliseconds(DocumentedTimeoutMilliseconds);

        // Act: read the limit of every optional pattern
        var timeouts = PatternCatalog.OptionalRegexes.Select(regex => regex.MatchTimeout).ToArray();

        // Assert: all equal the documented limit
        Assert.All(timeouts, timeout => Assert.Equal(documented, timeout));
    }

    /// <summary>
    ///     Test that each optional predefined pattern finds what cspell's pattern finds.
    /// </summary>
    /// <param name="name">The name of the predefined pattern.</param>
    /// <param name="input">The text to search.</param>
    /// <param name="expected">The matches recorded from cspell, separated by U+0001.</param>
    [Theory]
    [InlineData("HexValues", "#abc123 and #ab 0xDEAD0 and \\u00e9 and \\x{00e9} and \\u00e and 0x", "#abc123\u00010xDEAD0\u0001\\u00e9\u0001\\x{00e9}")]
    [InlineData("HexValues", "plain text 12", "")]
    [InlineData("PublicKey", "-----BEGIN PUBLIC KEY-----\nMIIBIjANBg+/=\n-----END PUBLIC KEY-----", "-----BEGIN PUBLIC KEY-----\nMIIBIjANBg+/=\n-----END PUBLIC KEY-----")]
    [InlineData("PublicKey", "-----BEGIN RSA PUBLIC KEY-----\nabc\n-----END RSA PUBLIC KEY----- tail", "-----BEGIN RSA PUBLIC KEY-----\nabc\n-----END RSA PUBLIC KEY-----")]
    [InlineData("PublicKey", "-----BEGIN PRIVATE KEY-----\nabc\n-----END PRIVATE KEY-----", "")]
    [InlineData("PublicKey", "-----BEGIN PUBLIC KEY-----\nabc\n-----END RSA PUBLIC KEY-----", "")]
    [InlineData("EscapeCharacters", "\\n \\t \\x41 \\u0041 \\q \\N \\xZ done", "\\n\u0001\\t\u0001\\x41\u0001\\u0041\u0001\\N")]
    [InlineData("EscapeCharacters", "a\\\\nb", "\\n")]
    [InlineData("Base64", "x aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa y", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Base64", "x aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa y", "")]
    [InlineData("Base64", "q AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\nCCCCC= z", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\nBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\nCCCCC=")]
    [InlineData("Base64", "+aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "+aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Base64", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=", "")]
    [InlineData("href", "<a href=\"a b\">x</a> <A HREF = \"c\">y</A> thref=\"z\" href=q href=\"nope", "href=\"a b\"\u0001HREF = \"c\"")]
    [InlineData("href", "href=\"a\nb\"", "")]
    [InlineData("string", "a \"one\" b 'two' c `three\nfour` d \"esc\\\"aped\" e 'it\\'s' f \"unclosed", "\"one\"\u0001'two'\u0001`three\nfour`\u0001\"esc\\\"aped\"\u0001'it\\'s'")]
    [InlineData("string", "x = \"a\\\\\" y \"b\"", "\"a\\\\\"\u0001\"b\"")]
    [InlineData("string", "\"\"  ''  ``", "\"\"\u0001''\u0001``")]
    [InlineData("CStyleComment", "a // line\nb /* block\nmore */ c", "// line\u0001/* block\nmore */")]
    [InlineData("CStyleComment", "http://x.y // z", "// z")]
    [InlineData("CStyleComment", "a:// not\nb // yes", "// yes")]
    [InlineData("CStyleComment", "/* open", "")]
    [InlineData("CStyleComment", "x//y", "//y")]
    [InlineData("PhpHereDoc", "<<<EOT\nline one\nEOT;\nafter", "<<<EOT\nline one\nEOT;")]
    [InlineData("PhpHereDoc", "<<<'TAG'\nx\nTAG;", "<<<'TAG'\nx\nTAG;")]
    [InlineData("PhpHereDoc", "<<<EOT\nnever closed\n", "")]
    [InlineData("PhpHereDoc", "<<<A\nA;", "<<<A\nA;")]
    [InlineData("PhpHereDoc", "<<<EOT\nx\n EOT;\nEOT;", "<<<EOT\nx\n EOT;\nEOT;")]
    [InlineData("Everything", "ab\ncd\r\nef", "ab\u0001cd\u0001ef")]
    public void PatternCatalog_Resolve_OptionalPredefinedName_FindsWhatCspellFinds(string name, string input, string expected)
    {
        // Arrange: the name, the text, and the matches recorded from cspell are passed in by the theory data
        // Act: resolve the name and find its matches
        var matches = Matches([name], input);

        // Assert: the matches are the ones cspell finds
        Assert.Equal(expected, matches);
    }

    /// <summary>
    ///     Test that a predefined name is matched ignoring letter case.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_PredefinedNameInOtherCase_IsFound()
    {
        // Arrange: the name of a predefined pattern in lower case and in upper case
        // Act: find the matches
        var lower = Matches(["hexvalues"], "#abc123");
        var upper = Matches(["HEXVALUES"], "#abc123");

        // Assert: both resolve to the pattern
        Assert.Multiple(
            () => Assert.Equal("#abc123", lower),
            () => Assert.Equal("#abc123", upper));
    }

    /// <summary>
    ///     Test that predefined names that are applied anyway are accepted and add nothing.
    /// </summary>
    /// <param name="name">A predefined name that is always applied.</param>
    [Theory]
    [InlineData("Urls")]
    [InlineData("Email")]
    [InlineData("RsaCert")]
    [InlineData("SshRsa")]
    [InlineData("Base64MultiLine")]
    [InlineData("Base64SingleLine")]
    [InlineData("CommitHash")]
    [InlineData("CommitHashLink")]
    [InlineData("CStyleHexValue")]
    [InlineData("CSSHexValue")]
    [InlineData("SHA")]
    [InlineData("HashStrings")]
    [InlineData("UnicodeRef")]
    [InlineData("UUID")]
    [InlineData("SpellCheckerDisable")]
    [InlineData("SpellCheckerDisableBlock")]
    [InlineData("SpellCheckerDisableLine")]
    [InlineData("SpellCheckerDisableNext")]
    [InlineData("SpellCheckerIgnoreInDocSetting")]
    public void PatternCatalog_Resolve_AlwaysAppliedName_IsAcceptedAndAddsNothing(string name)
    {
        // Arrange: the name is passed in by the theory data
        // Act: resolve a list with the name
        var resolved = PatternCatalog.Resolve([name], []);

        // Assert: no pattern is added, because the default patterns and the directives apply to every text anyway
        Assert.Empty(resolved);
    }

    /// <summary>
    ///     Test that the always-applied names of the documentation are exactly the nineteen the tests cover.
    /// </summary>
    [Fact]
    public void PatternCatalog_PredefinedNames_AlwaysAppliedNames_AreNineteen()
    {
        // Arrange: the predefined names that are not optional
        var optional = new[] { "HexValues", "PublicKey", "EscapeCharacters", "Base64", "href", "string", "CStyleComment", "PhpHereDoc", "Everything" };

        // Act: separate them from the optional ones
        var applied = PatternCatalog.PredefinedNames.Where(name => !optional.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();

        // Assert: the nineteen that are always applied
        Assert.Equal(AlwaysAppliedNames.Order(StringComparer.OrdinalIgnoreCase), applied.Order(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Test that a user pattern is applied only when the list names it, and under any letter case.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_NamedPattern_IsAppliedOnlyWhenListed()
    {
        // Arrange: two named patterns, one of which the list names in other letter case
        var definitions = new[] { new NamedPattern("ZorbOne", "/zorb\\d+/"), new NamedPattern("QuixTwo", "/quix\\d+/") };

        // Act: find the matches of a list that names only the first
        var matches = Matches(["zorbone"], "zorb1 quix2", definitions);

        // Assert: only the listed pattern applies
        Assert.Equal("zorb1", matches);
    }

    /// <summary>
    ///     Test that expressions and names can be mixed, and that the order of the list is kept.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_ExpressionsAndNames_AreAppliedInListOrder()
    {
        // Arrange: an expression, a name, and a predefined name, in that order
        var definitions = new[] { new NamedPattern("Digits", "/\\d+/") };

        // Act: find the matches
        var matches = Matches(["/zorb/", "Digits", "HexValues"], "zorb 12 #abc", definitions);

        // Assert: the matches come pattern by pattern in the order of the list
        Assert.Equal("zorb" + MatchSeparator + "12" + MatchSeparator + "#abc", matches);
    }

    /// <summary>
    ///     Test that a name listed twice is applied once.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_NameListedTwice_IsAppliedOnce()
    {
        // Arrange: a user pattern and a predefined pattern, each listed twice with different letter case
        var definitions = new[] { new NamedPattern("Digits", "/\\d+/") };

        // Act: resolve and find the matches
        var resolved = PatternCatalog.Resolve(["Digits", "DIGITS", "HexValues", "hexvalues"], definitions);
        var matches = Matches(["Digits", "DIGITS"], "12", definitions);

        // Assert: two patterns result, and the text is matched once
        Assert.Multiple(
            () => Assert.Equal(2, resolved.Length),
            () => Assert.Equal("12", matches));
    }

    /// <summary>
    ///     Test that a definition that no entry uses is still validated.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_UnusedInvalidDefinition_Throws()
    {
        // Arrange: a definition with an invalid pattern that the list does not name
        var definition = new NamedPattern("Broken", "/(/");

        // Act: resolve an empty list with it
        var exception = Rejected([], definition);

        // Assert: the mistake is found where it is made
        Assert.Equal("/(/", exception.Pattern);
    }

    /// <summary>
    ///     Test that an entry with the shape of a name that nothing defines is rejected, not read as literal text.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_UnknownName_ThrowsAndAdvisesSlashes()
    {
        // Arrange: a misspelled predefined name
        // Act: resolve a list with it
        var exception = Rejected(["Urlz"]);

        // Assert: the exception names the entry and says how to write literal text
        Assert.Multiple(
            () => Assert.Equal("Urlz", exception.Pattern),
            () => Assert.Contains("not a defined or predefined pattern name", exception.Reason, StringComparison.Ordinal),
            () => Assert.Contains("/text/", exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that literal text written between slashes is accepted, where the same word alone is a name.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_LiteralWordBetweenSlashes_IsAnExpression()
    {
        // Arrange: a word that is not a name, written as an expression
        // Act: find its matches
        var matches = Matches(["/urlz/"], "urlz");

        // Assert: it matches as literal text
        Assert.Equal("urlz", matches);
    }

    /// <summary>
    ///     Test that a definition may not reuse a predefined name, and that names must be unique and well formed.
    /// </summary>
    /// <param name="name">The name of the definition.</param>
    /// <param name="reason">Text that the reason must contain.</param>
    [Theory]
    [InlineData("Urls", "predefined")]
    [InlineData("urls", "predefined")]
    [InlineData("Everything", "predefined")]
    [InlineData("1abc", "must start with a letter")]
    [InlineData("has space", "must start with a letter")]
    [InlineData("under.score", "must start with a letter")]
    [InlineData(" Padded", "must start with a letter")]
    [InlineData("", "must start with a letter")]
    public void PatternCatalog_Resolve_InvalidDefinitionName_ThrowsWithReason(string name, string reason)
    {
        // Arrange: a definition with the name from the theory data
        var definition = new NamedPattern(name, "/zorb/");

        // Act: resolve an empty list with it
        var exception = Rejected([], definition);

        // Assert: the exception names the name and the reason
        Assert.Multiple(
            () => Assert.Equal(name, exception.Pattern),
            () => Assert.Contains(reason, exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that two definitions with the same name, ignoring letter case, are rejected.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_DuplicateDefinitionName_Throws()
    {
        // Arrange: two definitions whose names differ only in letter case
        var definitions = new[] { new NamedPattern("Zorb", "/a/"), new NamedPattern("ZORB", "/b/") };

        // Act: resolve an empty list with them
        var exception = Rejected([], definitions);

        // Assert: the second is rejected as already defined
        Assert.Multiple(
            () => Assert.Equal("ZORB", exception.Pattern),
            () => Assert.Contains("already defined", exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that an expression with an unsupported flag or an invalid body is rejected with its entry.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_InvalidExpressions_ThrowForListAndDefinitions()
    {
        // Arrange: an invalid expression in the list, and one in a definition
        // Act: resolve them
        var inList = Rejected(["/zorb/x"]);
        var inDefinition = Rejected([], new NamedPattern("Zorb", "/(/"));

        // Assert: each exception names its entry
        Assert.Multiple(
            () => Assert.Equal("/zorb/x", inList.Pattern),
            () => Assert.Equal("/(/", inDefinition.Pattern));
    }

    /// <summary>
    ///     Test that empty lists resolve to nothing and that null arguments are rejected.
    /// </summary>
    [Fact]
    public void PatternCatalog_Resolve_EmptyListsAndNulls_AreHandled()
    {
        // Arrange: empty lists
        // Act: resolve them
        var empty = PatternCatalog.Resolve([], []);

        // Assert: nothing results, and nulls are rejected
        Assert.Multiple(
            () => Assert.Empty(empty),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.Resolve(null!, [])),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.Resolve([], null!)));
    }

    /// <summary>
    ///     Test that the name shape is recognized and that other entries are expressions.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="expected">Whether the entry has the shape of a name.</param>
    [Theory]
    [InlineData("Urls", true)]
    [InlineData("my-pattern_2", true)]
    [InlineData("  Padded  ", true)]
    [InlineData("zorb", true)]
    [InlineData("/zorb/", false)]
    [InlineData("zorb.*", false)]
    [InlineData("2zorb", false)]
    [InlineData("zorb quix", false)]
    [InlineData("", false)]
    [InlineData("-zorb", false)]
    public void PatternCatalog_IsNameShaped_Entries_AreClassified(string entry, bool expected) =>
        Assert.Equal(expected, PatternCatalog.IsNameShaped(entry));

    /// <summary>
    ///     Test that the checks of a definition name and of a reference give the documented reasons.
    /// </summary>
    [Fact]
    public void PatternCatalog_CheckDefinitionNameAndReference_GiveReasonOrNull()
    {
        // Arrange: a set of defined names
        var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mine" };

        // Act: check names
        var goodName = PatternCatalog.CheckDefinitionName("Mine");
        var predefinedName = PatternCatalog.CheckDefinitionName("Urls");
        var knownReference = PatternCatalog.CheckReference("mine", defined);
        var predefinedReference = PatternCatalog.CheckReference("urls", defined);
        var unknownReference = PatternCatalog.CheckReference("Other", defined);

        // Assert: valid names give no reason, others do
        Assert.Multiple(
            () => Assert.Null(goodName),
            () => Assert.NotNull(predefinedName),
            () => Assert.Null(knownReference),
            () => Assert.Null(predefinedReference),
            () => Assert.NotNull(unknownReference),
            () => Assert.True(PatternCatalog.IsPredefined(" Urls ")),
            () => Assert.False(PatternCatalog.IsPredefined("Mine")));
    }

    /// <summary>
    ///     Test that ranges are appended without clearing the list, and that empty matches are skipped.
    /// </summary>
    [Fact]
    public void PatternCatalog_FindExcludedRanges_ExistingListAndEmptyMatches_AreKeptAndSkipped()
    {
        // Arrange: a pattern that also matches the empty string, and a list that already holds a range
        var regexes = PatternCatalog.Resolve(["/z*/"], []);
        var ranges = new List<TextRange> { new(0, 1) };

        // Act: find the ranges in a text with two runs of z
        PatternCatalog.FindExcludedRanges(regexes, "azzbz", ranges);

        // Assert: the earlier range is kept, the empty matches are not added, and the runs are
        Assert.Equal([new TextRange(0, 1), new TextRange(1, 2), new TextRange(4, 1)], ranges);
    }

    /// <summary>
    ///     Test that a text of one megabyte is searched completely by a pattern that fails at every position.
    /// </summary>
    [Fact]
    public void PatternCatalog_FindExcludedRanges_OneMegabyteOfText_IsSearchedCompletely()
    {
        // Arrange: a text of one million characters and a pattern that starts to match at every position
        var regexes = PatternCatalog.Resolve(["/z{3}x/"], []);
        var text = new string('z', 1_000_000);

        // Act: search the text
        var ranges = new List<TextRange>();
        PatternCatalog.FindExcludedRanges(regexes, text, ranges);

        // Assert: the search completes and finds nothing
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that null arguments of the range search are rejected.
    /// </summary>
    [Fact]
    public void PatternCatalog_FindExcludedRanges_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: valid arguments to replace one at a time
        var regexes = PatternCatalog.Resolve([], []);
        var ranges = new List<TextRange>();

        // Act / Assert: each null argument is rejected
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.FindExcludedRanges(null!, "text", ranges)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.FindExcludedRanges(regexes, null!, ranges)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.FindExcludedRanges(regexes, "text", null!)));
    }

    /// <summary>
    ///     Test that an entry is resolved to a regular expression with the given limit, to nothing for a name that is always
    ///     applied, and to a reason when it cannot be used.
    /// </summary>
    [Fact]
    public void PatternCatalog_TryResolveEntry_Forms_AreResolvedOrExplained()
    {
        // Arrange: the parsed definitions of one named pattern, and a short limit
        var defined = PatternCatalog.ParseDefinitions([new NamedPattern("ZorbDef", "/zorbdef/")], TimeSpan.FromSeconds(1));
        var limit = TimeSpan.FromMilliseconds(250);

        // Act: resolve an expression, a defined name in another letter case, an optional predefined name, a name that is always
        // applied, an expression with an unsupported flag, and a name that nothing defines
        var expression = PatternCatalog.TryResolveEntry("/zorb/i", defined, limit, out var expressionRegex, out var expressionProblem);
        var user = PatternCatalog.TryResolveEntry("zorbdef", defined, limit, out var userRegex, out var userProblem);
        var optional = PatternCatalog.TryResolveEntry("HEXVALUES", defined, limit, out var optionalRegex, out var optionalProblem);
        var always = PatternCatalog.TryResolveEntry("Urls", defined, limit, out var alwaysRegex, out var alwaysProblem);
        var flag = PatternCatalog.TryResolveEntry("/zorb/x", defined, limit, out var flagRegex, out var flagProblem);
        var unknown = PatternCatalog.TryResolveEntry("Urlz", defined, limit, out var unknownRegex, out var unknownProblem);

        // Assert: the expression has the limit; the names resolve to their patterns or to nothing; the failures give a reason
        Assert.Multiple(
            () => Assert.True(expression),
            () => Assert.Equal(limit, expressionRegex!.MatchTimeout),
            () => Assert.Null(expressionProblem),
            () => Assert.True(user),
            () => Assert.Same(defined["ZorbDef"], userRegex),
            () => Assert.Null(userProblem),
            () => Assert.True(optional),
            () => Assert.NotNull(optionalRegex),
            () => Assert.Null(optionalProblem),
            () => Assert.True(always),
            () => Assert.Null(alwaysRegex),
            () => Assert.Null(alwaysProblem),
            () => Assert.False(flag),
            () => Assert.Null(flagRegex),
            () => Assert.Contains("flag 'x'", flagProblem, StringComparison.Ordinal),
            () => Assert.False(unknown),
            () => Assert.Null(unknownRegex),
            () => Assert.Contains("'Urlz' is not a defined or predefined pattern name", unknownProblem, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the parsed definitions are by name without regard to letter case, reject a bad or repeated name, and
    ///     carry the limit they were given.
    /// </summary>
    [Fact]
    public void PatternCatalog_ParseDefinitions_Names_AreCaseInsensitiveAndChecked()
    {
        // Arrange: one definition, a repeat that differs by letter case, and a predefined name
        var limit = TimeSpan.FromMilliseconds(300);

        // Act: parse the good definition and the two bad lists
        var parsed = PatternCatalog.ParseDefinitions([new NamedPattern("Zorb", "/zorb/")], limit);
        var repeated = Assert.Throws<InvalidPatternException>(
            () => PatternCatalog.ParseDefinitions([new NamedPattern("Zorb", "/a/"), new NamedPattern("ZORB", "/b/")], limit));
        var predefined = Assert.Throws<InvalidPatternException>(
            () => PatternCatalog.ParseDefinitions([new NamedPattern("Urls", "/a/")], limit));

        // Assert: the name is found in any case with the limit, and the bad lists are rejected with the name
        Assert.Multiple(
            () => Assert.Equal(limit, parsed["zorb"].MatchTimeout),
            () => Assert.Equal("ZORB", repeated.Pattern),
            () => Assert.Equal("Urls", predefined.Pattern),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.ParseDefinitions(null!, limit)));
    }

    /// <summary>
    ///     Test that the entries of the list are resolved with parsed definitions, each name once, and with the given limit.
    /// </summary>
    [Fact]
    public void PatternCatalog_ResolveEntries_Entries_AreResolvedOnceWithTheLimit()
    {
        // Arrange: parsed definitions, and a list that names one twice and has an expression and a name that is always applied
        var limit = TimeSpan.FromMilliseconds(400);
        var defined = PatternCatalog.ParseDefinitions([new NamedPattern("Zorb", "/zorb/")], limit);

        // Act: resolve the list
        var resolved = PatternCatalog.ResolveEntries(["zorb", "ZORB", "/quix/", "Urls"], defined, limit);

        // Assert: the defined pattern once, then the expression; the name that is always applied adds nothing
        Assert.Multiple(
            () => Assert.Equal(2, resolved.Length),
            () => Assert.Same(defined["Zorb"], resolved[0]),
            () => Assert.Equal(limit, resolved[1].MatchTimeout),
            () => Assert.Throws<InvalidPatternException>(() => PatternCatalog.ResolveEntries(["Urlz"], defined, limit)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.ResolveEntries(null!, defined, limit)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.ResolveEntries([], null!, limit)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.TryResolveEntry(null!, defined, limit, out _, out _)),
            () => Assert.Throws<ArgumentNullException>(() => PatternCatalog.TryResolveEntry("/a/", null!, limit, out _, out _)));
    }

    /// <summary>
    ///     Test that a named pattern keeps its name and pattern, and rejects null.
    /// </summary>
    [Fact]
    public void NamedPattern_Constructor_KeepsValuesAndRejectsNull()
    {
        // Arrange: a name and a pattern
        // Act: build the value
        var pattern = new NamedPattern("Zorb", "/zorb/");

        // Assert: both are kept, equal values are equal, and null is rejected
        Assert.Multiple(
            () => Assert.Equal("Zorb", pattern.Name),
            () => Assert.Equal("/zorb/", pattern.Pattern),
            () => Assert.Equal(new NamedPattern("Zorb", "/zorb/"), pattern),
            () => Assert.Throws<ArgumentNullException>(() => new NamedPattern(null!, "/zorb/")),
            () => Assert.Throws<ArgumentNullException>(() => new NamedPattern("Zorb", null!)));
    }
}
