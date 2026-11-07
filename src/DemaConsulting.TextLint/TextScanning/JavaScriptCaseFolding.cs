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

using System.Text;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Knows which characters JavaScript treats as the same letter when a regular expression ignores letter case,
///     so that a translated pattern matches exactly those characters.
/// </summary>
/// <remarks>
///     The <c>i</c> flag of a JavaScript expression compares characters through a canonical form. Without the
///     <c>u</c> flag it is the character's upper-case form, kept only when that form is a single code unit and does
///     not turn a non-ASCII character into an ASCII one (so the Kelvin sign U+212A and the long s U+017F are not
///     the letters <c>k</c> and <c>s</c>, while the micro sign U+00B5 is the Greek letter mu); with the flag it is
///     simple case folding (where the Kelvin sign and the long s are those letters). The .NET case-insensitive
///     option follows neither: it equates the Kelvin sign with <c>k</c> in both modes, and it misses pairs such
///     as the final and the ordinary sigma. The translator therefore does not use the option; it writes every
///     letter and every class as the explicit set of characters that this class returns. The equivalences are
///     derived from the character data of .NET (the simple upper-case and lower-case mappings), with the 102
///     characters whose full upper-case form has several characters taken out for the mode without the Unicode
///     flag, as the JavaScript <c>toUpperCase</c> does; the result was checked against the JavaScript engine for
///     every code unit and is identical except for five pairs of letters that Unicode added after the version
///     of the .NET character data (for example U+A7DA and U+A7DB), which is the one difference that remains and
///     depends on the versions of .NET and of the JavaScript engine. Only the basic multilingual plane is
///     tabulated; a character outside it is handled through <see cref="Rune"/> and its simple case mappings.
///     The tables are built once, on first use, and never change, so the class is thread-safe.
/// </remarks>
internal static class JavaScriptCaseFolding
{
    /// <summary>
    ///     The number of UTF-16 code units, which is the size of a table indexed by code unit.
    /// </summary>
    private const int CodeUnitCount = 0x10000;

    /// <summary>
    ///     The Kelvin sign, which the .NET case-insensitive option equates with <c>k</c> and JavaScript does not
    ///     without the Unicode flag.
    /// </summary>
    private const char KelvinSign = '\u212A';

    /// <summary>
    ///     A private-use character that stands in for the Kelvin sign: no pattern of the library reads it as a letter.
    /// </summary>
    private const char KelvinPlaceholder = '\uE000';

    /// <summary>
    ///     The code units whose full upper-case form has more than one character, as inclusive ranges: the
    ///     JavaScript <c>toUpperCase</c> keeps such a character unchanged when it canonicalizes a letter.
    /// </summary>
    private static readonly (int Low, int High)[] MultiCharacterUppercase =
    [
        (0x00DF, 0x00DF), (0x0149, 0x0149), (0x01F0, 0x01F0), (0x0390, 0x0390), (0x03B0, 0x03B0), (0x0587, 0x0587),
        (0x1E96, 0x1E9A), (0x1F50, 0x1F50), (0x1F52, 0x1F52), (0x1F54, 0x1F54), (0x1F56, 0x1F56), (0x1F80, 0x1FAF),
        (0x1FB2, 0x1FB4), (0x1FB6, 0x1FB7), (0x1FBC, 0x1FBC), (0x1FC2, 0x1FC4), (0x1FC6, 0x1FC7), (0x1FCC, 0x1FCC),
        (0x1FD2, 0x1FD3), (0x1FD6, 0x1FD7), (0x1FE2, 0x1FE4), (0x1FE6, 0x1FE7), (0x1FF2, 0x1FF4), (0x1FF6, 0x1FF7),
        (0x1FFC, 0x1FFC), (0xFB00, 0xFB06), (0xFB13, 0xFB17),
    ];

    /// <summary>
    ///     The equivalence classes of the mode without the Unicode flag, built on first use.
    /// </summary>
    private static readonly Lazy<Tables> NonUnicodeTables = new(() => Build(unicode: false));

    /// <summary>
    ///     The equivalence classes of the mode with the Unicode flag, built on first use.
    /// </summary>
    private static readonly Lazy<Tables> UnicodeTables = new(() => Build(unicode: true));

    /// <summary>
    ///     Replaces the Kelvin sign in a text by a character that no library pattern reads as a letter.
    /// </summary>
    /// <remarks>
    ///     Used before a pattern that the .NET case-insensitive option makes case-insensitive reads a text that
    ///     must be judged the JavaScript way. The replacement has the same length, so every offset is unchanged.
    ///     A text without the sign is returned as it is, without copying. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <returns>The text, with the Kelvin sign replaced.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    internal static string MaskKelvinSign(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains(KelvinSign, StringComparison.Ordinal) ? text.Replace(KelvinSign, KelvinPlaceholder) : text;
    }

    /// <summary>
    ///     Gets the code units that JavaScript treats as the same letter as a code unit, itself included.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The code unit.</param>
    /// <param name="unicode"><see langword="true"/> for the mode of the <c>u</c> flag.</param>
    /// <returns>The code units, in no particular order; only the character itself when it has no case.</returns>
    internal static IReadOnlyList<char> Equivalents(char character, bool unicode)
    {
        var next = (unicode ? UnicodeTables : NonUnicodeTables).Value.Next;
        var members = new List<char> { character };
        for (var member = next[character]; member != character; member = next[member])
        {
            members.Add((char)member);
        }

        return members;
    }

    /// <summary>
    ///     Gets the other forms of a character outside the basic plane that JavaScript treats as the same letter.
    /// </summary>
    /// <remarks>
    ///     Only the mode with the Unicode flag treats such a character as one character with a case; without the
    ///     flag its two code units have none. The forms are the simple upper-case and lower-case mappings of the
    ///     character. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pair">The character as its two UTF-16 code units, a surrogate pair. Must not be null.</param>
    /// <returns>The character itself and its other forms, without duplicates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pair"/> is null.</exception>
    internal static IReadOnlyList<string> AstralForms(string pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var forms = new List<string> { pair };
        if (Rune.TryGetRuneAt(pair, 0, out var rune))
        {
            foreach (var other in new[] { Rune.ToUpperInvariant(rune), Rune.ToLowerInvariant(rune) })
            {
                var text = other.ToString();
                if (!forms.Contains(text))
                {
                    forms.Add(text);
                }
            }
        }

        return forms;
    }

    /// <summary>
    ///     Adds to a set of code units every code unit that JavaScript treats as the same letter as one in the set.
    /// </summary>
    /// <remarks>
    ///     This is what a character class means when the expression ignores case: a character matches when
    ///     some member of the class is the same letter. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges of the set. Must not be null.</param>
    /// <param name="unicode"><see langword="true"/> for the mode of the <c>u</c> flag.</param>
    /// <returns>The inclusive ranges of the closed set, in ascending order and not overlapping.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ranges"/> is null.</exception>
    internal static List<(int Low, int High)> Close(IEnumerable<(int Low, int High)> ranges, bool unicode)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var tables = (unicode ? UnicodeTables : NonUnicodeTables).Value;

        // Mark the members, then every other form of each member that has a case
        var marked = new bool[CodeUnitCount];
        foreach (var (low, high) in ranges)
        {
            for (var unit = low; unit <= high; unit++)
            {
                marked[unit] = true;
            }
        }

        foreach (var unit in tables.Cased)
        {
            if (!marked[unit])
            {
                continue;
            }

            for (var member = tables.Next[unit]; member != unit; member = tables.Next[member])
            {
                marked[member] = true;
            }
        }

        // Read the marks back as ranges
        var result = new List<(int Low, int High)>();
        var start = -1;
        for (var unit = 0; unit <= CodeUnitCount; unit++)
        {
            var isMarked = unit < CodeUnitCount && marked[unit];
            if (isMarked && start < 0)
            {
                start = unit;
            }
            else if (!isMarked && start >= 0)
            {
                result.Add((start, unit - 1));
                start = -1;
            }
        }

        return result;
    }

    /// <summary>
    ///     Builds the equivalence classes of one mode.
    /// </summary>
    /// <remarks>
    ///     Without the Unicode flag two code units are the same letter when their canonical forms are equal: the
    ///     simple upper-case form, kept only when the full upper-case form is a single character and when it
    ///     does not map a non-ASCII character to ASCII. With the flag they are the same letter when a chain of
    ///     simple upper-case and lower-case mappings joins them, which is what simple case folding does. Stateless.
    /// </remarks>
    /// <param name="unicode"><see langword="true"/> for the mode of the <c>u</c> flag.</param>
    /// <returns>The tables of the mode.</returns>
    private static Tables Build(bool unicode)
    {
        // Group the code units: by canonical form without the flag, by joined mappings with it
        var parent = new int[CodeUnitCount];
        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            parent[unit] = unit;
        }

        var multiple = new bool[CodeUnitCount];
        foreach (var (low, high) in MultiCharacterUppercase)
        {
            for (var unit = low; unit <= high; unit++)
            {
                multiple[unit] = true;
            }
        }

        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            var character = (char)unit;
            if (unicode)
            {
                Join(parent, unit, char.ToUpperInvariant(character));
                Join(parent, unit, char.ToLowerInvariant(character));
            }
            else
            {
                Join(parent, unit, CanonicalForm(character, multiple));
            }
        }

        // Link the members of each group into a cycle, so a group can be walked from any member
        var next = new ushort[CodeUnitCount];
        var last = new Dictionary<int, int>();
        var first = new Dictionary<int, int>();
        var cased = new List<ushort>();
        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            next[unit] = (ushort)unit;
            var root = Find(parent, unit);
            if (last.TryGetValue(root, out var previous))
            {
                next[previous] = (ushort)unit;
                last[root] = unit;
            }
            else
            {
                first[root] = unit;
                last[root] = unit;
            }
        }

        foreach (var (root, tail) in last)
        {
            var head = first[root];
            if (head != tail)
            {
                next[tail] = (ushort)head;
            }
        }

        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            if (next[unit] != unit)
            {
                cased.Add((ushort)unit);
            }
        }

        return new Tables(next, [.. cased]);
    }

    /// <summary>
    ///     Gets the canonical form of a code unit in the mode without the Unicode flag.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="character">The code unit.</param>
    /// <param name="multiple">Whether the full upper-case form of each code unit has several characters.</param>
    /// <returns>The upper-case form, or the code unit itself when JavaScript keeps it.</returns>
    private static char CanonicalForm(char character, bool[] multiple)
    {
        if (multiple[character])
        {
            return character;
        }

        var upper = char.ToUpperInvariant(character);
        return character >= 128 && upper < 128 ? character : upper;
    }

    /// <summary>
    ///     Joins the groups of two code units.
    /// </summary>
    /// <remarks>
    ///     Side effect: changes <paramref name="parent"/>.
    /// </remarks>
    /// <param name="parent">The parent table of the groups.</param>
    /// <param name="first">The first code unit.</param>
    /// <param name="second">The second code unit.</param>
    private static void Join(int[] parent, int first, int second)
    {
        var firstRoot = Find(parent, first);
        var secondRoot = Find(parent, second);
        if (firstRoot != secondRoot)
        {
            parent[firstRoot] = secondRoot;
        }
    }

    /// <summary>
    ///     Finds the root of the group of a code unit.
    /// </summary>
    /// <remarks>
    ///     Side effect: shortens the paths in <paramref name="parent"/>.
    /// </remarks>
    /// <param name="parent">The parent table of the groups.</param>
    /// <param name="unit">The code unit.</param>
    /// <returns>The root of its group.</returns>
    private static int Find(int[] parent, int unit)
    {
        while (parent[unit] != unit)
        {
            parent[unit] = parent[parent[unit]];
            unit = parent[unit];
        }

        return unit;
    }

    /// <summary>
    ///     The equivalence classes of one mode.
    /// </summary>
    /// <param name="Next">For each code unit, the next member of its class in a cycle; the code unit itself when alone.</param>
    /// <param name="Cased">The code units that have another form, in ascending order.</param>
    private sealed record Tables(ushort[] Next, ushort[] Cased);
}
