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

// cspell:ignore dbff

using System.Globalization;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the translation of patterns with the <c>u</c> flag, in which a surrogate pair is one character.
/// </summary>
/// <remarks>
///     The class is pure logic over strings and depends on nothing but the .NET base class library, so these tests use
///     the real classes with no test doubles. The expected matches were recorded from the JavaScript engine that cspell
///     runs its patterns in (Node.js v22.18.0) by <c>record-unicode-mode-matches.mjs</c>: with the flag, <c>.</c>, a
///     negated class, and the negated shorthands match a character outside the basic plane whole, a surrogate that is not
///     half of a pair is a character of its own, and a match never starts inside a pair.
/// </remarks>
public class JavaScriptUnicodeModeTests
{
    /// <summary>
    ///     The number of tab-separated fields that a recorded case has: the flags, the pattern, the text, the matches, and
    ///     the readable form for people.
    /// </summary>
    private const int RecordedFields = 5;

    /// <summary>
    ///     The text that stands in the matches field for a pattern that the JavaScript engine rejects.
    /// </summary>
    private const string RejectedMarker = "!";

    /// <summary>
    ///     A number of repeated properties that is over the translation limit with the Unicode flag.
    /// </summary>
    private const int ManyProperties = 400;

    /// <summary>
    ///     Verifies that every recorded case gives the matches that the JavaScript engine found.
    /// </summary>
    [Fact]
    public void JavaScriptUnicodeMode_Translation_EveryRecordedCase_FindsWhatJavaScriptFinds()
    {
        // Arrange: the recorded cases, each a pattern with its flags, a text, and the matches that JavaScript found
        var cases = ReadRecordedCases();

        // Act: translate each pattern, search the text, and collect the cases on which the result differs
        var wrong = new List<string>();
        foreach (var (flags, pattern, input, expected, readable) in cases)
        {
            var outcome = Find(flags, pattern, input);
            if (!outcome.SequenceEqual(expected))
            {
                wrong.Add($"{readable}: expected [{string.Join(';', expected.Select(Show))}] but found [{string.Join(';', outcome.Select(Show))}]");
            }
        }

        // Assert: there is none, and the file is not empty
        Assert.Multiple(
            () => Assert.NotEmpty(cases),
            () => Assert.Empty(wrong));
    }

    /// <summary>
    ///     Verifies that the recorded cases cover the constructs of the mode of the <c>u</c> flag that the translation
    ///     writes specially, so that deleting a case family from the recording is noticed.
    /// </summary>
    [Fact]
    public void JavaScriptUnicodeMode_RecordedCases_CoverEveryConstructOfTheMode()
    {
        // Arrange: the readable forms of the recorded cases
        var readable = string.Join('\n', ReadRecordedCases().Select(recorded => recorded.Readable));

        // Act: look for a case of each family
        string[] families =
        [
            "\"^.$\" /u", "\"^.$\" /su", "\"[^a]\" /u", "\"[^]\" /u", "\"\\\\W\" /u", "\"\\\\S\" /u", "\"\\\\D\" /u", "\"[^\\\\W]\" /u",
            "\"\\\\P{L}\" /u", "\"\\\\p{Cs}\" /u", "\"[^\\\\p{So}]\" /u", "\"\\\\p{Lu}\" /iu", "\"(?<=.)a\" /u", "\"\\\\bx\" /u",
            "\"(.)\\\\1\" /u", "\"\\\\uD83D\" /u", "\"\\\\uDE00\" /u", "\"[\\\\uD800-\\\\uDBFF]\" /u", "\"[a-\\\\u{1F600}]\" /u",
            "\"\\\\u{D83D}\\\\u{DE00}\" /u", "\"\\\\uD83D\\\\uDE00{2}\" /u",
        ];

        // Assert: each family has a case
        Assert.All(families, family => Assert.Contains(family, readable, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies that the translation of the dot with the <c>u</c> flag does not depend on the text it is used on, and
    ///     that without the flag it is still one code unit.
    /// </summary>
    [Fact]
    public void JavaScriptUnicodeMode_Dot_OnlyTheUnicodeFlagReadsAPairAsOneCharacter()
    {
        // Arrange: a grinning face, which is two code units
        const string face = "\uD83D\uDE00";

        // Act: test the dot with and without the flag, with the dot-all flag as well
        var unicode = PatternParser.Parse("/^.$/u").IsMatch(face);
        var unicodeDotAll = PatternParser.Parse("/^.$/su").IsMatch(face);
        var plain = PatternParser.Parse("/^.$/").IsMatch(face);
        var plainTwo = PatternParser.Parse("/^..$/").IsMatch(face);

        // Assert: only with the flag is the pair one character
        Assert.Multiple(
            () => Assert.True(unicode),
            () => Assert.True(unicodeDotAll),
            () => Assert.False(plain),
            () => Assert.True(plainTwo));
    }

    /// <summary>
    ///     Verifies that the translation of the Unicode properties, the negated shorthands, and the negated classes stays
    ///     inside the translation limit for ordinary use, and that a pattern with many of them is refused, with the flag.
    /// </summary>
    [Fact]
    public void JavaScriptUnicodeMode_Sets_OrdinaryUseIsWithinTheTranslationLimitAndManyAreRefused()
    {
        // Arrange: ordinary patterns that use a property, a negated property, a negated class, and a negated shorthand, and a
        // pattern that repeats a property until it is over the limit
        string[] ordinary = ["/\\p{L}+\\P{Lu}[^\\p{So}a-z]\\W\\S\\D./u", "/^(?:\\p{L}|\\p{N}|[^\\W\\d])+$/iu", "/[^\\u{1F600}-\\u{1F64F}]+/u"];
        var many = "/" + string.Concat(Enumerable.Repeat("\\p{L}", ManyProperties)) + "/u";

        // Act: translate them
        var translated = ordinary.Select(entry => PatternParser.Parse(entry).ToString().Length).ToArray();
        var exception = Assert.Throws<InvalidPatternException>(() => PatternParser.Parse(many));

        // Assert: the ordinary ones are far below the limit, and the many are refused with the limit named
        Assert.Multiple(
            () => Assert.All(translated, length => Assert.True(length < JavaScriptRegexTranslator.MaxTranslatedLength / 2, $"Length {length}.")),
            () => Assert.Contains("longer than 262144 characters", exception.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Finds the non-empty matches of a pattern in a text.
    /// </summary>
    /// <param name="flags">The flags of the pattern.</param>
    /// <param name="pattern">The body of the pattern.</param>
    /// <param name="input">The text to search.</param>
    /// <returns>The matches, or the single marker when the pattern is rejected.</returns>
    private static List<string> Find(string flags, string pattern, string input)
    {
        System.Text.RegularExpressions.Regex regex;
        try
        {
            regex = PatternParser.Parse("/" + pattern + "/" + flags);
        }
        catch (InvalidPatternException)
        {
            return [RejectedMarker];
        }

        var found = new List<string>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            if (match.Length > 0)
            {
                found.Add(input.Substring(match.Index, match.Length));
            }
        }

        return found;
    }

    /// <summary>
    ///     Reads the recorded cases.
    /// </summary>
    /// <returns>The flags, the pattern, the text, the expected matches, and the readable form of each case.</returns>
    private static List<(string Flags, string Pattern, string Input, List<string> Matches, string Readable)> ReadRecordedCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TextScanning", "Data", "javascript-unicode-matches.txt");
        var result = new List<(string, string, string, List<string>, string)>();
        foreach (var fields in File.ReadLines(path).Where(line => line.Length > 0 && line[0] != '#').Select(line => line.Split('\t')))
        {
            Assert.Equal(RecordedFields, fields.Length);
            var matches = fields[3] == RejectedMarker
                ? [RejectedMarker]
                : fields[3].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Decode).ToList();
            result.Add((fields[0], Decode(fields[1]), Decode(fields[2]), matches, fields[4]));
        }

        return result;
    }

    /// <summary>
    ///     Turns hexadecimal UTF-16 code units, separated by spaces, into a string.
    /// </summary>
    /// <param name="hex">The code units.</param>
    /// <returns>The string, which may hold surrogates that are not halves of a pair.</returns>
    private static string Decode(string hex) =>
        new([.. hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(unit => (char)int.Parse(unit, NumberStyles.HexNumber, CultureInfo.InvariantCulture))]);

    /// <summary>
    ///     Writes a string with its code units in hexadecimal, so that a failure shows lone surrogates.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The code units.</returns>
    private static string Show(string text) => string.Join(' ', text.Select(unit => ((int)unit).ToString("X4", CultureInfo.InvariantCulture)));
}
