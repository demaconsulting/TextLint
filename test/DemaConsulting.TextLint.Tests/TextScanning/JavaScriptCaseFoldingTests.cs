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

// cspell:ignore Deseret nonunicode

using System.Globalization;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="JavaScriptCaseFolding"/> class.
/// </summary>
/// <remarks>
///     The class is pure logic over code units and depends on nothing but the .NET base class library, so these
///     tests use the real class with no test doubles. The reference is a file of the letter groups of the
///     JavaScript engine that cspell runs its patterns in, recorded by <c>record-case-groups.mjs</c>. The groups
///     that the .NET data cannot reproduce, because the Unicode version of the JavaScript engine is newer, are
///     the five pairs listed in <see cref="UnicodeVersionPairs"/>; every other group must match exactly.
/// </remarks>
public class JavaScriptCaseFoldingTests
{
    /// <summary>
    ///     The mode name of the recorded groups that stand for the <c>i</c> flag.
    /// </summary>
    private const string NonUnicodeMode = "nonunicode";

    /// <summary>
    ///     The mode name of the recorded groups that stand for the <c>iu</c> flags.
    /// </summary>
    private const string UnicodeMode = "unicode";

    /// <summary>
    ///     The number of UTF-16 code units, which is the size of every recorded table.
    /// </summary>
    private const int CodeUnitCount = 0x10000;

    /// <summary>
    ///     The pairs of code units that the JavaScript engine of the recording treats as one letter and the
    ///     .NET Unicode data does not, because the engine knows a newer version of Unicode.
    /// </summary>
    private static readonly (char First, char Second)[] UnicodeVersionPairs =
    [
        ('\u019B', '\uA7DC'),
        ('\u0264', '\uA7CB'),
        ('\u1C89', '\u1C8A'),
        ('\uA7CC', '\uA7CD'),
        ('\uA7DA', '\uA7DB')
    ];

    /// <summary>
    ///     Verifies that the code units that are the same letter in the library are the same letter in JavaScript.
    /// </summary>
    /// <param name="mode">The mode to compare: the recorded mode name.</param>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    [Theory]
    [InlineData(NonUnicodeMode, false)]
    [InlineData(UnicodeMode, true)]
    public void JavaScriptCaseFolding_Equivalents_EveryRecordedGroup_IsReproducedWithoutExtras(string mode, bool unicode)
    {
        // Arrange: the recorded groups, as the set of ordered pairs each one implies
        var recorded = ReadRecordedPairs(mode);

        // Act: the pairs of the library, over every code unit
        var actual = new HashSet<(char, char)>();
        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            foreach (var other in JavaScriptCaseFolding.Equivalents((char)unit, unicode))
            {
                if (other != unit)
                {
                    actual.Add(((char)unit, other));
                }
            }
        }

        // Assert: the library invents no pair, and misses only pairs of a newer Unicode version
        var extra = actual.Where(pair => !recorded.Contains(pair)).ToList();
        var missing = recorded.Where(pair => !actual.Contains(pair)).ToList();
        Assert.Multiple(
            () => Assert.Empty(extra),
            () => Assert.All(missing, pair => Assert.Contains(pair, AllowedMissingPairs(unicode))));
    }

    /// <summary>
    ///     Verifies the letter groups that differ between .NET and JavaScript.
    /// </summary>
    /// <param name="first">The first code unit.</param>
    /// <param name="second">The second code unit.</param>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    /// <param name="same">Whether JavaScript treats the two as one letter.</param>
    [Theory]
    [InlineData('K', '\u212A', false, false)]
    [InlineData('k', '\u212A', false, false)]
    [InlineData('K', '\u212A', true, true)]
    [InlineData('k', '\u212A', true, true)]
    [InlineData('s', '\u017F', false, false)]
    [InlineData('S', '\u017F', true, true)]
    [InlineData('\u00B5', '\u039C', false, true)]
    [InlineData('\u00B5', '\u03BC', false, true)]
    [InlineData('\u03C3', '\u03C2', false, true)]
    [InlineData('\u03C3', '\u03A3', false, true)]
    [InlineData('\u00DF', '\u1E9E', false, false)]
    [InlineData('\u00DF', '\u1E9E', true, true)]
    [InlineData('i', '\u0130', false, false)]
    [InlineData('i', '\u0131', false, false)]
    [InlineData('I', '\u0131', true, false)]
    [InlineData('a', 'A', false, true)]
    [InlineData('a', 'b', true, false)]
    public void JavaScriptCaseFolding_Equivalents_KnownDivergentLetters_FollowJavaScript(
        char first,
        char second,
        bool unicode,
        bool same)
    {
        // Arrange: the two code units and the mode, as recorded from JavaScript

        // Act: ask for the letters equal to the first
        var equivalents = JavaScriptCaseFolding.Equivalents(first, unicode);

        // Assert: the second is among them exactly when JavaScript says so
        Assert.Equal(same, equivalents.Contains(second));
    }

    /// <summary>
    ///     Verifies that a code unit without a case is only equal to itself.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Equivalents_CharacterWithoutCase_ReturnsOnlyItself()
    {
        // Arrange: a digit and a letter of a script without case
        char[] characters = ['7', '\u4E2D'];

        // Act: ask in both modes
        var results = characters.SelectMany(c => new[]
        {
            JavaScriptCaseFolding.Equivalents(c, unicode: false),
            JavaScriptCaseFolding.Equivalents(c, unicode: true)
        }).ToList();

        // Assert: each answer is the character itself and nothing else
        Assert.All(results, result => Assert.Single(result));
    }

    /// <summary>
    ///     Verifies that closing a set adds the other forms of its letters.
    /// </summary>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    /// <param name="expectKelvin">Whether the Kelvin sign joins the closure of <c>[k]</c>.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void JavaScriptCaseFolding_Close_LowerCaseLetter_AddsTheJavaScriptForms(bool unicode, bool expectKelvin)
    {
        // Arrange: the set of the single letter k
        (int Low, int High)[] set = [('k', 'k')];

        // Act: close it
        var closed = JavaScriptCaseFolding.Close(set, unicode);

        // Assert: K is added, and the Kelvin sign only when the u flag is on
        Assert.Multiple(
            () => Assert.Contains(('K', 'K'), closed),
            () => Assert.Contains(('k', 'k'), closed),
            () => Assert.Equal(expectKelvin, closed.Contains((0x212A, 0x212A))));
    }

    /// <summary>
    ///     Verifies that closing a range merges adjacent code units into ascending, non-overlapping ranges.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Close_ContiguousRange_ReturnsAscendingNonOverlappingRanges()
    {
        // Arrange: the whole of a to z
        (int Low, int High)[] set = [('a', 'z')];

        // Act: close it
        var closed = JavaScriptCaseFolding.Close(set, unicode: false);

        // Assert: the ranges rise and never touch, and a to z is joined with A to Z
        Assert.Multiple(
            () => Assert.Contains(('A', 'Z'), closed),
            () => Assert.Contains(('a', 'z'), closed),
            () => Assert.All(
                closed.Zip(closed.Skip(1)),
                pair => Assert.True(pair.First.High + 1 < pair.Second.Low)));
    }

    /// <summary>
    ///     Verifies that an empty set stays empty.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Close_EmptySet_ReturnsEmpty()
    {
        // Arrange: no ranges
        var set = Array.Empty<(int Low, int High)>();

        // Act: close it
        var closed = JavaScriptCaseFolding.Close(set, unicode: true);

        // Assert: there is nothing to add
        Assert.Empty(closed);
    }

    /// <summary>
    ///     Verifies the forms of a character outside the basic plane.
    /// </summary>
    /// <param name="pair">The character as a surrogate pair.</param>
    /// <param name="expected">The other form that must be listed.</param>
    [Theory]
    [InlineData("\U00010400", "\U00010428")]
    [InlineData("\U00010428", "\U00010400")]
    public void JavaScriptCaseFolding_AstralForms_LetterOutsideBasicPlane_ListsTheOtherCase(string pair, string expected)
    {
        // Arrange: a Deseret letter

        // Act: list its forms
        var forms = JavaScriptCaseFolding.AstralForms(pair);

        // Assert: the letter itself first, then the other case, without duplicates
        Assert.Multiple(
            () => Assert.Equal(pair, forms[0]),
            () => Assert.Contains(expected, forms),
            () => Assert.Equal(forms.Count, forms.Distinct().Count()));
    }

    /// <summary>
    ///     Verifies that a character outside the basic plane without a case lists only itself.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_AstralForms_CharacterWithoutCase_ListsOnlyItself()
    {
        // Arrange: an emoji
        const string emoji = "\U0001F600";

        // Act: list its forms
        var forms = JavaScriptCaseFolding.AstralForms(emoji);

        // Assert: nothing else
        Assert.Equal([emoji], forms);
    }

    /// <summary>
    ///     Verifies that masking the Kelvin sign keeps the length and changes nothing else.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_MaskKelvinSign_TextWithSign_KeepsLengthAndReplacesOnlyTheSign()
    {
        // Arrange: a text with the sign twice
        const string text = "a\u212Ab\u212A";

        // Act: mask it
        var masked = JavaScriptCaseFolding.MaskKelvinSign(text);

        // Assert: the length is the same, the other letters stay, and the sign is gone
        Assert.Multiple(
            () => Assert.Equal(text.Length, masked.Length),
            () => Assert.DoesNotContain('\u212A', masked),
            () => Assert.Equal('a', masked[0]),
            () => Assert.Equal('b', masked[2]));
    }

    /// <summary>
    ///     Verifies that a text without the sign is returned as it is.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_MaskKelvinSign_TextWithoutSign_ReturnsTheSameInstance()
    {
        // Arrange: a text with an ordinary K
        var text = new string(['K', 'e', 'l', 'v', 'i', 'n']);

        // Act: mask it
        var masked = JavaScriptCaseFolding.MaskKelvinSign(text);

        // Assert: no copy was made
        Assert.Same(text, masked);
    }

    /// <summary>
    ///     Verifies that the entry points reject a null argument.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_EntryPoints_NullArgument_ThrowArgumentNullException()
    {
        // Arrange: null for every reference argument

        // Act and assert: each call refuses it
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.MaskKelvinSign(null!)),
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.AstralForms(null!)),
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.Close(null!, unicode: true)));
    }

    /// <summary>
    ///     Gets the pairs a mode may miss compared with the recorded JavaScript groups.
    /// </summary>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    /// <returns>Both orders of each pair that depends on a newer Unicode version.</returns>
    private static HashSet<(char, char)> AllowedMissingPairs(bool unicode)
    {
        // The two modes share the pairs; the argument keeps the call site explicit about the mode
        _ = unicode;
        var allowed = new HashSet<(char, char)>();
        foreach (var (first, second) in UnicodeVersionPairs)
        {
            allowed.Add((first, second));
            allowed.Add((second, first));
        }

        return allowed;
    }

    /// <summary>
    ///     Reads the recorded groups of a mode and expands each into all its ordered pairs.
    /// </summary>
    /// <param name="mode">The recorded mode name.</param>
    /// <returns>The ordered pairs of distinct code units that are one letter.</returns>
    private static HashSet<(char, char)> ReadRecordedPairs(string mode)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TextScanning", "Data", "javascript-case-groups.txt");
        var pairs = new HashSet<(char, char)>();
        foreach (var line in File.ReadLines(path))
        {
            var prefix = mode + ":";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var members = line[prefix.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(hex => (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToList();
            foreach (var one in members)
            {
                foreach (var two in members.Where(member => member != one))
                {
                    pairs.Add((one, two));
                }
            }
        }

        return pairs;
    }
}
