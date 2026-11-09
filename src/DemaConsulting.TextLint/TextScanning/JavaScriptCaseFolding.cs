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

using System.Globalization;
using System.Text;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Knows which characters JavaScript treats as the same letter when a regular expression ignores letter case,
///     so that a translated pattern matches exactly those characters, on every platform.
/// </summary>
/// <remarks>
///     The <c>i</c> flag of a JavaScript expression compares characters through a canonical form. Without the
///     <c>u</c> flag it is the character's upper-case form, kept only when that form is a single code unit and does
///     not turn a non-ASCII character into an ASCII one (so the Kelvin sign U+212A and the long s U+017F are not
///     the letters <c>k</c> and <c>s</c>, while the micro sign U+00B5 is the Greek letter mu); with the flag it is
///     simple case folding (where the Kelvin sign and the long s are those letters). The .NET case-insensitive
///     option follows neither, and the casing functions of .NET follow the Unicode data of the runtime that runs
///     them, which differs between .NET versions, so neither is used. The groups come from <see cref="JavaScriptCaseData"/>,
///     a table that a script recorded from a pinned Node.js (the engine cspell runs in) and that is compiled into the
///     library, so the behavior of a pattern is the same on every operating system and every runtime, and changes only
///     when the table is regenerated. Nothing here calls a casing function, a culture, or the case option of the
///     regular expression engine. The table holds code units for the mode without the Unicode flag and code points for
///     the mode with it; a character outside the basic plane has a case only in the second mode. The tables are
///     built once, on first use, and never change, so the class is thread-safe.
/// </remarks>
internal static class JavaScriptCaseFolding
{
    /// <summary>
    ///     The number of UTF-16 code units, which is the size of a table indexed by code unit.
    /// </summary>
    private const int CodeUnitCount = 0x10000;

    /// <summary>
    ///     The equivalence classes of the mode without the Unicode flag, built on first use.
    /// </summary>
    private static readonly Lazy<Tables> NonUnicodeTables = new(() => Parse(JavaScriptCaseData.NonUnicodeGroups));

    /// <summary>
    ///     The equivalence classes of the mode with the Unicode flag, built on first use.
    /// </summary>
    private static readonly Lazy<Tables> UnicodeTables = new(() => Parse(JavaScriptCaseData.UnicodeGroups));

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
    ///     flag its two code units have none, so this function is only asked in that mode. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pair">The character as its two UTF-16 code units, a surrogate pair. Must not be null.</param>
    /// <returns>The character itself first, then its other forms, without duplicates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pair"/> is null.</exception>
    internal static IReadOnlyList<string> AstralForms(string pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var forms = new List<string> { pair };
        if (Rune.TryGetRuneAt(pair, 0, out var rune) &&
            UnicodeTables.Value.Astral.TryGetValue(rune.Value, out var group))
        {
            foreach (var text in group.Select(char.ConvertFromUtf32).Where(text => !forms.Contains(text)))
            {
                forms.Add(text);
            }
        }

        return forms;
    }

    /// <summary>
    ///     Adds to a set of code units every code unit that JavaScript treats as the same letter as one in the set.
    /// </summary>
    /// <remarks>
    ///     This is what a character class means when the expression ignores case: a character matches when
    ///     some member of the class is the same letter. The work is proportional to the number of ranges and to the
    ///     number of letters that have a case (a few thousand), not to the 65,536 code units, so a pattern with
    ///     many classes does not pay for the whole range in each. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges of the set. Must not be null.</param>
    /// <param name="unicode"><see langword="true"/> for the mode of the <c>u</c> flag.</param>
    /// <returns>The inclusive ranges of the closed set, in ascending order and not overlapping or touching.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ranges"/> is null.</exception>
    internal static List<(int Low, int High)> Close(IEnumerable<(int Low, int High)> ranges, bool unicode)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var tables = (unicode ? UnicodeTables : NonUnicodeTables).Value;

        // Put the members in order, then add every other form of each member that has a case
        var members = Merge([.. ranges]);
        var others = new List<(int Low, int High)>();
        foreach (var unit in tables.Cased)
        {
            if (!Contains(members, unit))
            {
                continue;
            }

            for (var member = tables.Next[unit]; member != unit; member = tables.Next[member])
            {
                if (!Contains(members, member))
                {
                    others.Add((member, member));
                }
            }
        }

        if (others.Count == 0)
        {
            return members;
        }

        members.AddRange(others);
        return Merge(members);
    }

    /// <summary>
    ///     Adds to a set of code points every code point that JavaScript treats as the same letter as one in the set,
    ///     in the mode of the <c>u</c> flag.
    /// </summary>
    /// <remarks>
    ///     The characters of the basic plane are closed as by <see cref="Close"/>; the characters outside it are closed
    ///     by the groups of the table (a group never mixes the two). The work is proportional to the number of ranges
    ///     and of the letters that have a case. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges of code points, from 0 to 0x10FFFF. Must not be null.</param>
    /// <returns>The inclusive ranges of the closed set, in ascending order and not overlapping or touching.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ranges"/> is null.</exception>
    internal static List<(int Low, int High)> CloseCodePoints(IEnumerable<(int Low, int High)> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        // Split the set at the end of the basic plane, close that part, and close the rest by the groups
        var basic = new List<(int Low, int High)>();
        var astral = new List<(int Low, int High)>();
        foreach (var (low, high) in ranges)
        {
            if (low < CodeUnitCount)
            {
                basic.Add((low, Math.Min(high, CodeUnitCount - 1)));
            }

            if (high >= CodeUnitCount)
            {
                astral.Add((Math.Max(low, CodeUnitCount), high));
            }
        }

        var result = Close(basic, unicode: true);
        var members = Merge(astral);
        var others = new List<(int Low, int High)>();
        foreach (var (member, group) in UnicodeTables.Value.Astral)
        {
            if (!Contains(members, member))
            {
                continue;
            }

            others.AddRange(group.Where(other => !Contains(members, other)).Select(other => (other, other)));
        }

        result.AddRange(members);
        result.AddRange(others);
        return Merge(result);
    }

    /// <summary>
    ///     Sorts ranges and joins those that overlap or touch.
    /// </summary>
    /// <remarks>
    ///     Side effect: sorts <paramref name="ranges"/> in place. Stateless.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges, in any order. Must not be null.</param>
    /// <returns>The ranges in ascending order, not overlapping and not touching.</returns>
    internal static List<(int Low, int High)> Merge(List<(int Low, int High)> ranges)
    {
        ranges.Sort(static (first, second) => first.Low.CompareTo(second.Low));
        var result = new List<(int Low, int High)>();
        foreach (var (low, high) in ranges)
        {
            if (result.Count > 0 && low <= result[^1].High + 1)
            {
                result[^1] = (result[^1].Low, Math.Max(result[^1].High, high));
            }
            else
            {
                result.Add((low, high));
            }
        }

        return result;
    }

    /// <summary>
    ///     Determines whether a code unit lies in one of the ranges.
    /// </summary>
    /// <remarks>
    ///     A binary search. Stateless.
    /// </remarks>
    /// <param name="ranges">Ranges in ascending order, not overlapping. Must not be null.</param>
    /// <param name="unit">The code unit.</param>
    /// <returns><see langword="true"/> when some range holds the code unit.</returns>
    private static bool Contains(List<(int Low, int High)> ranges, int unit)
    {
        var low = 0;
        var high = ranges.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (unit < ranges[middle].Low)
            {
                high = middle - 1;
            }
            else if (unit > ranges[middle].High)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Builds the equivalence classes of one mode from the text of its groups.
    /// </summary>
    /// <remarks>
    ///     Each line is one group of hexadecimal characters separated by spaces. A group of code units of the basic
    ///     plane is linked into a ring, so it can be walked from any member; a group of characters outside the basic
    ///     plane is stored by member. A group that mixes the two is a defect of the data. Stateless.
    /// </remarks>
    /// <param name="groups">The groups, one per line. Must not be null.</param>
    /// <returns>The tables of the mode.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="groups"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when a member is not a hexadecimal number.</exception>
    /// <exception cref="InvalidDataException">Thrown when a group mixes basic-plane and astral characters, has
    /// fewer than two different members, or repeats a character already in a group.</exception>
    internal static Tables Parse(string groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var next = new ushort[CodeUnitCount];
        for (var unit = 0; unit < CodeUnitCount; unit++)
        {
            next[unit] = (ushort)unit;
        }

        var cased = new List<ushort>();
        var astral = new Dictionary<int, int[]>();
        foreach (var line in groups.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var members = line
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(hex => int.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture))
                .ToArray();
            if (members.Length < 2 || members.Distinct().Count() != members.Length)
            {
                throw new InvalidDataException($"The case group '{line}' has fewer than two different members.");
            }

            if (members.All(member => member < CodeUnitCount))
            {
                LinkRing(next, cased, members, line);
            }
            else if (members.All(member => member >= CodeUnitCount))
            {
                if (members.Any(astral.ContainsKey))
                {
                    throw new InvalidDataException($"The case group '{line}' repeats a character of another group.");
                }

                foreach (var member in members)
                {
                    astral.Add(member, members);
                }
            }
            else
            {
                throw new InvalidDataException($"The case group '{line}' mixes basic-plane and astral characters.");
            }
        }

        cased.Sort();
        return new Tables(next, [.. cased], astral);
    }

    /// <summary>
    ///     Links the members of a group of code units into a ring.
    /// </summary>
    /// <remarks>
    ///     Side effect: changes <paramref name="next"/> and <paramref name="cased"/>.
    /// </remarks>
    /// <param name="next">For each code unit, the next member of its ring.</param>
    /// <param name="cased">The code units that have another form, which this call adds the members to.</param>
    /// <param name="members">The members of the group.</param>
    /// <param name="line">The text of the group, for the message.</param>
    private static void LinkRing(ushort[] next, List<ushort> cased, int[] members, string line)
    {
        if (members.Any(member => next[member] != member))
        {
            throw new InvalidDataException($"The case group '{line}' repeats a character of another group.");
        }

        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            next[member] = (ushort)members[(index + 1) % members.Length];
            cased.Add((ushort)member);
        }
    }

    /// <summary>
    ///     The equivalence classes of one mode.
    /// </summary>
    /// <param name="Next">For each code unit, the next member of its class in a ring; the code unit itself when alone.</param>
    /// <param name="Cased">The code units that have another form, in ascending order.</param>
    /// <param name="Astral">For each character outside the basic plane that has another form, all the members of its
    /// group.</param>
    internal sealed record Tables(ushort[] Next, ushort[] Cased, IReadOnlyDictionary<int, int[]> Astral);
}
