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
using System.Text;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="JavaScriptCaseFolding"/> class and its generated table.
/// </summary>
/// <remarks>
///     The class is pure logic over code units and depends on nothing but the .NET base class library, so these
///     tests use the real class with no test doubles. The reference is a file of the letter groups of the
///     JavaScript engine that cspell runs its patterns in, recorded by <c>generate-javascript-case-data.mjs</c>,
///     the script that also generates the table the library embeds. The library must reproduce the file exactly
///     on every platform and every .NET version, because the table is data and nothing is derived from the
///     casing functions of the runtime; a failure on one platform only would mean such a function was used.
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
    ///     The last Unicode code point.
    /// </summary>
    private const int LastCodePoint = 0x10FFFF;

    /// <summary>
    ///     The files of the library that read or write patterns, which must not use the casing of the runtime.
    /// </summary>
    private static readonly string[] PatternSources =
    [
        "JavaScriptRegexTranslator.cs",
        "JavaScriptCaseFolding.cs",
        "JavaScriptSyntax.cs",
        "PatternParser.cs",
        "PatternCatalog.cs",
        "DefaultPatterns.cs",
        "MarkdownPatterns.cs",
        "DocumentDirectives.cs",
        "RandomStringDetector.cs",
    ];

    /// <summary>
    ///     Verifies that the code units that are the same letter in the library are exactly those that are the same
    ///     letter in the recorded JavaScript groups, with nothing added and nothing missing.
    /// </summary>
    /// <param name="mode">The mode to compare: the recorded mode name.</param>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    [Theory]
    [InlineData(NonUnicodeMode, false)]
    [InlineData(UnicodeMode, true)]
    public void JavaScriptCaseFolding_Equivalents_EveryCodeUnit_ReproducesTheRecordedGroupsExactly(string mode, bool unicode)
    {
        // Arrange: the recorded groups of the basic plane, as the set of ordered pairs each one implies
        var recorded = ReadRecordedGroups(mode).Where(group => group.All(member => member < CodeUnitCount))
            .SelectMany(group => group.SelectMany(one => group.Where(two => two != one).Select(two => ((char)one, (char)two))))
            .ToHashSet();

        // Act: the pairs of the library, over every code unit
        var actual = new HashSet<(char, char)>();
        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            foreach (var other in JavaScriptCaseFolding.Equivalents((char)unit, unicode).Where(other => other != unit))
            {
                actual.Add(((char)unit, other));
            }
        }

        // Assert: the sets are equal, so the library invents no pair and misses none
        Assert.True(recorded.SetEquals(actual), $"Missing {recorded.Except(actual).Count()}, extra {actual.Except(recorded).Count()}.");
    }

    /// <summary>
    ///     Verifies that the characters outside the basic plane that are the same letter in the library are exactly
    ///     those of the recorded groups of the mode of the <c>u</c> flag.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_AstralForms_EveryCodePoint_ReproducesTheRecordedGroupsExactly()
    {
        // Arrange: the recorded groups of characters outside the basic plane, by member
        var recorded = ReadRecordedGroups(UnicodeMode)
            .Where(group => group.All(member => member >= CodeUnitCount))
            .SelectMany(group => group.Select(member => (member, group)))
            .ToDictionary(entry => entry.member, entry => entry.group);

        // Act: ask the library for every character outside the basic plane
        var wrong = new List<int>();
        for (var codePoint = CodeUnitCount; codePoint <= LastCodePoint; codePoint++)
        {
            var forms = JavaScriptCaseFolding.AstralForms(char.ConvertFromUtf32(codePoint));
            var expected = recorded.TryGetValue(codePoint, out var group)
                ? group.Select(char.ConvertFromUtf32).ToHashSet()
                : [char.ConvertFromUtf32(codePoint)];
            if (!expected.SetEquals(forms) || forms[0] != char.ConvertFromUtf32(codePoint) || forms.Distinct().Count() != forms.Count)
            {
                wrong.Add(codePoint);
            }
        }

        // Assert: there is no character on which the library and the recording disagree
        Assert.Empty(wrong);
    }

    /// <summary>
    ///     Verifies that the table compiled into the library is the one the recording produced, line for line.
    /// </summary>
    /// <param name="mode">The mode to compare: the recorded mode name.</param>
    /// <param name="embedded">The embedded table of that mode.</param>
    [Theory]
    [InlineData(NonUnicodeMode, "NonUnicodeGroups")]
    [InlineData(UnicodeMode, "UnicodeGroups")]
    public void JavaScriptCaseData_Groups_TableText_IsTheRecordedFileLineForLine(string mode, string embedded)
    {
        // Arrange: the lines of the recorded file of the mode, and the lines of the embedded table
        var recorded = File.ReadLines(ReferencePath())
            .Where(line => line.StartsWith(mode + ":", StringComparison.Ordinal))
            .Select(line => line[(mode.Length + 1)..].Trim())
            .ToArray();
        var table = embedded == "NonUnicodeGroups" ? JavaScriptCaseData.NonUnicodeGroups : JavaScriptCaseData.UnicodeGroups;

        // Act: split the table the way the library does
        var lines = table.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // Assert: the same groups in the same order, so the table and the file come from one recording
        Assert.Equal(recorded, lines);
    }

    /// <summary>
    ///     Verifies that the table states the versions of the engine it was recorded from.
    /// </summary>
    [Fact]
    public void JavaScriptCaseData_Versions_AreStated()
    {
        // Arrange: the recorded file, whose header states the versions too

        // Act: read the versions of the table and the header of the file
        var header = string.Join('\n', File.ReadLines(ReferencePath()).Where(line => line.StartsWith('#')));

        // Assert: the table names a Node.js version and a Unicode version, and the file header repeats them
        Assert.Multiple(
            () => Assert.StartsWith("v", JavaScriptCaseData.NodeVersion, StringComparison.Ordinal),
            () => Assert.NotEmpty(JavaScriptCaseData.UnicodeVersion),
            () => Assert.Contains(JavaScriptCaseData.NodeVersion, header, StringComparison.Ordinal),
            () => Assert.Contains("Unicode " + JavaScriptCaseData.UnicodeVersion, header, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Verifies the letter groups that differ between .NET and JavaScript, and those on which the casing of one
    ///     .NET runtime differs from another.
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
    [InlineData('i', '\u0130', true, false)]
    [InlineData('i', '\u0131', false, false)]
    [InlineData('I', '\u0131', true, false)]
    [InlineData('a', 'A', false, true)]
    [InlineData('a', 'b', true, false)]
    [InlineData('\uA7CE', '\uA7CF', false, false)]
    [InlineData('\uA7CE', '\uA7CF', true, false)]
    [InlineData('\uA7D2', '\uA7D3', true, false)]
    [InlineData('\uA7D4', '\uA7D5', true, false)]
    [InlineData('\u0390', '\u1FD3', false, false)]
    [InlineData('\u0390', '\u1FD3', true, true)]
    [InlineData('\uA7DA', '\uA7DB', false, true)]
    [InlineData('\uA7DA', '\uA7DB', true, true)]
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
    ///     Verifies that closing a set gives the same result as testing every code unit against the letters that are
    ///     the same, for sets given in any order, overlapping, and touching.
    /// </summary>
    /// <param name="unicode">Whether the mode is the one of the <c>u</c> flag.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JavaScriptCaseFolding_Close_AnySet_EqualsTheCodeUnitByCodeUnitDefinition(bool unicode)
    {
        // Arrange: sets that are unsorted, overlapping, touching, the whole range, a single astral-adjacent unit, and a
        // scattered one; the definition is that a code unit is in the closed set when it or a letter equal to it is a member
        (int Low, int High)[][] sets =
        [
            [('z', 'z'), ('a', 'f'), ('d', 'k'), ('l', 'l')],
            [(0x0000, 0xFFFF)],
            [(0x00B5, 0x00B5), (0x017F, 0x017F), (0x212A, 0x212A)],
            [(0x03A3, 0x03A3), (0x0391, 0x03A1)],
            [(0xFFFF, 0xFFFF), (0x0000, 0x0000), (0x1E9E, 0x1E9E)],
            [(0x2000, 0x20FF), (0x2100, 0x21FF)],
        ];

        foreach (var set in sets)
        {
            // Act: close the set, and compute the definition for every code unit
            var closed = JavaScriptCaseFolding.Close(set, unicode);
            var actual = new bool[CodeUnitCount];
            foreach (var (low, high) in closed)
            {
                for (var unit = low; unit <= high; unit++)
                {
                    actual[unit] = true;
                }
            }

            var wrong = Enumerable.Range(0, CodeUnitCount)
                .Where(unit => actual[unit] != JavaScriptCaseFolding.Equivalents((char)unit, unicode)
                    .Any(member => set.Any(range => member >= range.Low && member <= range.High)))
                .ToList();

            // Assert: no code unit differs, and the ranges neither overlap nor touch
            Assert.Multiple(
                () => Assert.Empty(wrong),
                () => Assert.All(closed.Zip(closed.Skip(1)), pair => Assert.True(pair.First.High + 1 < pair.Second.Low)));
        }
    }

    /// <summary>
    ///     Verifies that closing a set of code points adds the other cases of the characters outside the basic plane as
    ///     well as those inside it, and joins the ranges.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_CloseCodePoints_BasicAndAstralMembers_AddsTheOtherCaseOfEach()
    {
        // Arrange: the Deseret capital letters and the letter k, as unsorted ranges
        (int Low, int High)[] set = [(0x10400, 0x1044F), ('k', 'k'), (0x10400, 0x10410)];

        // Act: close it
        var closed = JavaScriptCaseFolding.CloseCodePoints(set);

        // Assert: the Deseret small letters and the capital K and the Kelvin sign are added, and the ranges are joined
        Assert.Multiple(
            () => Assert.Contains(('K', 'K'), closed),
            () => Assert.Contains((0x212A, 0x212A), closed),
            () => Assert.Contains((0x10400, 0x1044F), closed),
            () => Assert.Contains(closed, range => range.Low <= 0x10428 && 0x10428 <= range.High),
            () => Assert.All(closed.Zip(closed.Skip(1)), pair => Assert.True(pair.First.High + 1 < pair.Second.Low)));
    }

    /// <summary>
    ///     Verifies that closing a set of code points gives, for every character outside the basic plane, the same
    ///     result as the definition with the groups of the table.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_CloseCodePoints_EveryAstralLetter_EqualsItsGroup()
    {
        // Arrange: every character outside the basic plane that has a case, as a set of its own
        var wrong = new List<int>();
        foreach (var codePoint in Enumerable.Range(0x10000, 0x10FFFF - 0x10000 + 1))
        {
            var forms = JavaScriptCaseFolding.AstralForms(char.ConvertFromUtf32(codePoint)).Select(form => char.ConvertToUtf32(form, 0)).Order().ToArray();
            if (forms.Length == 1)
            {
                continue;
            }

            // Act: close the set of that character
            var closed = JavaScriptCaseFolding.CloseCodePoints([(codePoint, codePoint)]);
            var members = closed.SelectMany(range => Enumerable.Range(range.Low, range.High - range.Low + 1)).ToArray();

            // Assert (collected): the closed set is the group
            if (!members.SequenceEqual(forms))
            {
                wrong.Add(codePoint);
            }
        }

        Assert.Empty(wrong);
    }

    /// <summary>
    ///     Verifies that closing a set of code points rejects a null set.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_CloseCodePoints_NullSet_ThrowsArgumentNullException()
    {
        // Arrange: null for the set

        // Act and assert: the call refuses it
        Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.CloseCodePoints(null!));
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
    ///     Verifies that the entry points reject a null argument.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_EntryPoints_NullArgument_ThrowArgumentNullException()
    {
        // Arrange: null for every reference argument

        // Act and assert: each call refuses it
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.AstralForms(null!)),
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.Close(null!, unicode: true)),
            () => Assert.Throws<ArgumentNullException>(() => JavaScriptCaseFolding.Parse(null!)));
    }

    /// <summary>
    ///     Verifies that the table text is read the same with either line ending.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Parse_AnyLineEnding_GivesTheSameTables()
    {
        // Arrange: two groups, written with line feeds, with carriage returns and line feeds, and with a trailing break
        const string lf = "0041 0061\n0042 0062";
        const string crlf = "0041 0061\r\n0042 0062\r\n";

        // Act: parse them
        var fromLf = JavaScriptCaseFolding.Parse(lf);
        var fromCrlf = JavaScriptCaseFolding.Parse(crlf);

        // Assert: the tables are equal
        Assert.Multiple(
            () => Assert.Equal(fromLf.Next, fromCrlf.Next),
            () => Assert.Equal(fromLf.Cased, fromCrlf.Cased),
            () => Assert.Equal([(ushort)0x41, (ushort)0x42, (ushort)0x61, (ushort)0x62], fromLf.Cased));
    }

    /// <summary>
    ///     Verifies that data that is damaged is refused with a message that quotes the group.
    /// </summary>
    /// <param name="groups">The damaged table text.</param>
    [Theory]
    [InlineData("0041")]
    [InlineData("0041 0041")]
    [InlineData("0041 0061\n0061 0062")]
    [InlineData("0041 10400")]
    [InlineData("10400 10428\n10428 10429")]
    public void JavaScriptCaseFolding_Parse_DamagedGroup_ThrowsInvalidDataException(string groups)
    {
        // Arrange: a table with a group that has one member, repeats a character, shares a character with another group,
        // or mixes the basic plane with the astral planes

        // Act and assert: it is refused
        var exception = Assert.Throws<InvalidDataException>(() => JavaScriptCaseFolding.Parse(groups));
        Assert.Contains("case group", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies that a member that is not hexadecimal is refused.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Parse_MemberNotHexadecimal_ThrowsFormatException()
    {
        // Arrange: a group with a member that is not a number

        // Act and assert: it is refused
        Assert.Throws<FormatException>(() => JavaScriptCaseFolding.Parse("0041 zz"));
    }

    /// <summary>
    ///     Verifies that no source of the library that reads or writes patterns uses the case option of the regular
    ///     expression engine or a casing function of the runtime, which are the two ways in which the behavior of a
    ///     pattern could depend on the platform or the runtime version.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_PatternSources_DoNotUseTheCasingOfTheRuntime()
    {
        // Arrange: the sources, found from the test assembly up to the repository; the test cannot run without them
        var folder = FindSourceFolder();
        Assert.SkipWhen(folder is null, "The source files are not beside the test assembly.");
        string[] forbidden =
        [
            "RegexOptions.IgnoreCase", "(?i", "ToUpper", "ToLower", "TextInfo", "CompareInfo", "CurrentCulture",
            "InvariantCultureIgnoreCase", "Rune.To", "string.Normalize", ".Normalize(",
        ];

        // Act: find the code lines (comments are not code) that use any of them
        var offending = PatternSources
            .SelectMany(file => File.ReadLines(Path.Combine(folder, file)).Select(line => (file, line: line.Trim())))
            .Where(entry => !entry.line.StartsWith("//", StringComparison.Ordinal))
            .Where(entry => forbidden.Any(token => entry.line.Contains(token, StringComparison.Ordinal)))
            .Select(entry => $"{entry.file}: {entry.line}")
            .ToList();

        // Assert: none (the ordinal comparison that ignores case is only used on ASCII keywords and extensions, where it
        // is the same on every platform)
        Assert.Empty(offending);
    }

    /// <summary>
    ///     Verifies that nothing in the library builds a regular expression with the case option, whatever it is for.
    /// </summary>
    [Fact]
    public void JavaScriptCaseFolding_Library_NoRegularExpressionUsesTheCaseOption()
    {
        // Arrange: every source file of the library
        var folder = FindSourceFolder();
        Assert.SkipWhen(folder is null, "The source files are not beside the test assembly.");
        var library = Directory.GetParent(folder)!.FullName;

        // Act: find the code lines that name the case option or an inline case group
        var offending = Directory.EnumerateFiles(library, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select(line => (file: Path.GetFileName(file), line: line.Trim())))
            .Where(entry => !entry.line.StartsWith("//", StringComparison.Ordinal))
            .Where(entry => entry.line.Contains("RegexOptions.IgnoreCase", StringComparison.Ordinal) ||
                            entry.line.Contains("(?i", StringComparison.Ordinal))
            .Select(entry => $"{entry.file}: {entry.line}")
            .ToList();

        // Assert: there is none
        Assert.Empty(offending);
    }

    /// <summary>
    ///     Gets the path of the recorded groups.
    /// </summary>
    /// <returns>The path of the file beside the test assembly.</returns>
    private static string ReferencePath() =>
        Path.Combine(AppContext.BaseDirectory, "TextScanning", "Data", "javascript-case-groups.txt");

    /// <summary>
    ///     Finds the folder of the pattern sources of the library, by walking up from the test assembly.
    /// </summary>
    /// <returns>The folder, or null when the repository is not above the test assembly.</returns>
    private static string? FindSourceFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "DemaConsulting.TextLint", "TextScanning");
            if (File.Exists(Path.Combine(candidate, "JavaScriptCaseFolding.cs")))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the recorded groups of a mode.
    /// </summary>
    /// <param name="mode">The recorded mode name.</param>
    /// <returns>The groups, each the code points of its members.</returns>
    private static List<int[]> ReadRecordedGroups(string mode)
    {
        var prefix = mode + ":";
        return File.ReadLines(ReferencePath())
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(hex => int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray())
            .ToList();
    }
}
