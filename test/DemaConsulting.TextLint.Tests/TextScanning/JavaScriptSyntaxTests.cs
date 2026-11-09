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

// cspell:ignore FEFF

using System.Text.RegularExpressions;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="JavaScriptSyntax"/> class.
/// </summary>
/// <remarks>
///     The class is constants and one small function over strings, so these tests use the real class with no
///     test doubles. The expected white space set was read from the JavaScript engine that cspell runs its
///     patterns in: the characters for which <c>/\s/.test(c)</c> is true.
/// </remarks>
public partial class JavaScriptSyntaxTests
{
    /// <summary>
    ///     The number of UTF-16 code units, the range over which the white space set is compared.
    /// </summary>
    private const int CodeUnitCount = 0x10000;

    /// <summary>
    ///     The line terminators that JavaScript knows, plus U+0085, which it does not treat as one.
    /// </summary>
    private static readonly string[] LineTerminatorProbes = ["\n", "\r", "\u2028", "\u2029", "\u0085"];

    /// <summary>
    ///     The code units that JavaScript treats as white space, as recorded from <c>/\s/</c>.
    /// </summary>
    private static readonly HashSet<char> RecordedSpace = BuildRecordedSpace();

    /// <summary>
    ///     Gets a pattern that matches exactly one white space character, from <see cref="JavaScriptSyntax.Space"/>.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^" + JavaScriptSyntax.Space + "$", RegexOptions.CultureInvariant)]
    private static partial Regex SpacePattern();

    /// <summary>
    ///     Gets a pattern that matches exactly one non-white-space character, from <see cref="JavaScriptSyntax.NotSpace"/>.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^" + JavaScriptSyntax.NotSpace + "$", RegexOptions.CultureInvariant)]
    private static partial Regex NotSpacePattern();

    /// <summary>
    ///     Gets a pattern that matches an <c>x</c> at the start of a line, from <see cref="JavaScriptSyntax.LineStart"/>.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(JavaScriptSyntax.LineStart + "x", RegexOptions.CultureInvariant)]
    private static partial Regex LineStartPattern();

    /// <summary>
    ///     Gets a pattern that matches exactly one non-line-terminator character, from
    ///     <see cref="JavaScriptSyntax.NotLineTerminator"/>.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^" + JavaScriptSyntax.NotLineTerminator + "$", RegexOptions.CultureInvariant)]
    private static partial Regex NotLineTerminatorPattern();

    /// <summary>
    ///     Verifies that <see cref="JavaScriptSyntax.IsSpace"/> agrees with JavaScript on every code unit.
    /// </summary>
    [Fact]
    public void JavaScriptSyntax_IsSpace_EveryCodeUnit_AgreesWithJavaScript()
    {
        // Arrange: every code unit

        // Act: collect those that the library calls white space
        var actual = Enumerable.Range(0, CodeUnitCount).Select(unit => (char)unit).Where(JavaScriptSyntax.IsSpace);

        // Assert: exactly the recorded set
        Assert.True(RecordedSpace.SetEquals(actual));
    }

    /// <summary>
    ///     Verifies that the Space and NotSpace patterns agree with <see cref="JavaScriptSyntax.IsSpace"/>.
    /// </summary>
    [Fact]
    public void JavaScriptSyntax_SpaceAndNotSpace_EveryCodeUnit_ArePartitionedLikeIsSpace()
    {
        // Arrange: the two patterns
        var space = SpacePattern();
        var notSpace = NotSpacePattern();

        // Act: find the code units on which a pattern disagrees with the function
        var wrong = Enumerable.Range(0, CodeUnitCount)
            .Select(unit => ((char)unit).ToString())
            .Where(text =>
            {
                var isSpace = JavaScriptSyntax.IsSpace(text[0]);
                return space.IsMatch(text) != isSpace || notSpace.IsMatch(text) == isSpace;
            })
            .ToList();

        // Assert: there is none
        Assert.Empty(wrong);
    }

    /// <summary>
    ///     Verifies that the translation of <c>\s</c> uses the shared white space set.
    /// </summary>
    [Fact]
    public void JavaScriptSyntax_Space_TranslatedShorthand_MatchesTheSameCodeUnits()
    {
        // Arrange: the translation of the shorthand
        var translated = PatternParser.Parse("/^\\s$/");

        // Act: find the code units on which it disagrees with the function
        var wrong = Enumerable.Range(0, CodeUnitCount)
            .Select(unit => (char)unit)
            .Where(c => translated.IsMatch(c.ToString()) != JavaScriptSyntax.IsSpace(c))
            .ToList();

        // Assert: there is none
        Assert.Empty(wrong);
    }

    /// <summary>
    ///     Verifies the characters on which .NET and JavaScript disagree about white space.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="expected">Whether JavaScript treats it as white space.</param>
    [Theory]
    [InlineData('\u0085', false)]
    [InlineData('\u180E', false)]
    [InlineData('\uFEFF', true)]
    [InlineData('\u00A0', true)]
    [InlineData('\u2028', true)]
    [InlineData('\u2029', true)]
    [InlineData('\u202F', true)]
    [InlineData('\u3000', true)]
    [InlineData('\u200B', false)]
    [InlineData('a', false)]
    public void JavaScriptSyntax_IsSpace_DivergentCharacters_FollowJavaScript(char character, bool expected)
    {
        // Arrange: a character on which the engines are known to differ

        // Act: classify it
        var actual = JavaScriptSyntax.IsSpace(character);

        // Assert: the JavaScript answer
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Verifies that the trim removes JavaScript white space and nothing else.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("  abc \t", "abc")]
    [InlineData("\uFEFFabc\uFEFF", "abc")] // textlint:disable-line
    [InlineData("\u0085abc\u0085", "\u0085abc\u0085")]
    [InlineData("\u00A0\u2028abc\u3000", "abc")]
    [InlineData("a b", "a b")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    public void JavaScriptSyntax_Trim_Text_RemovesOnlyJavaScriptWhiteSpace(string text, string expected)
    {
        // Arrange: the text

        // Act: trim it
        var trimmed = JavaScriptSyntax.Trim(text);

        // Assert: the recorded result
        Assert.Equal(expected, trimmed);
    }

    /// <summary>
    ///     Verifies that the trim rejects a null text.
    /// </summary>
    [Fact]
    public void JavaScriptSyntax_Trim_NullText_ThrowsArgumentNullException()
    {
        // Arrange: null

        // Act and assert: the call refuses it
        Assert.Throws<ArgumentNullException>(() => JavaScriptSyntax.Trim(null!));
    }

    /// <summary>
    ///     Verifies the line-oriented constants against the JavaScript meaning of <c>^</c> and <c>.</c>.
    /// </summary>
    [Fact]
    public void JavaScriptSyntax_LineConstants_LineTerminators_FollowJavaScript()
    {
        // Arrange: a line start and a dot that treat the four JavaScript line terminators alike
        var start = LineStartPattern();
        var dot = NotLineTerminatorPattern();

        // Act: test the start after each terminator and the dot on each terminator and on U+0085
        var starts = LineTerminatorProbes.Select(t => start.IsMatch("a" + t + "x")).ToList();
        var dots = LineTerminatorProbes.Select(t => dot.IsMatch(t)).ToList();

        // Assert: the four terminators start a line and stop the dot; U+0085 does neither
        Assert.Multiple(
            () => Assert.Equal([true, true, true, true, false], starts),
            () => Assert.Equal([false, false, false, false, true], dots));
    }

    /// <summary>
    ///     Builds the recorded white space set of the JavaScript engine.
    /// </summary>
    /// <returns>The code units for which the JavaScript <c>\s</c> matches.</returns>
    private static HashSet<char> BuildRecordedSpace()
    {
        var set = new HashSet<char>();
        for (var unit = '\u0009'; unit <= '\u000D'; unit++)
        {
            set.Add(unit);
        }

        for (var unit = '\u2000'; unit <= '\u200A'; unit++)
        {
            set.Add(unit);
        }

        foreach (var unit in new[]
        {
            '\u0020', '\u00A0', '\u1680', '\u2028', '\u2029', '\u202F', '\u205F', '\u3000', '\uFEFF'
        })
        {
            set.Add(unit);
        }

        return set;
    }
}
