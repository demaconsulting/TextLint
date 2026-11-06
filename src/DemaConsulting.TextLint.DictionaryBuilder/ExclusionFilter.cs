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

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     Removes the words of the reviewed exclusions list from every dictionary, so that an excluded
///     word is never known, whichever list or dictionary an imported source put it in.
/// </summary>
/// <remarks>
///     The imported English lists are large and faithful to their upstream sources, and some of their
///     entries hide real typos (a misspelling the source contains, or a rare word that is almost
///     always a mistyped common one). Editing the imported lists would be undone by the next refresh,
///     so the removal is a rule applied when the dictionaries are combined, driven by one reviewed
///     list. The rule is a pure function over folded words, so it can be verified without a file
///     system. It also reports each exclusion that no dictionary has: such a word is a stale entry,
///     and reporting it keeps the reviewed list honest and short. The class holds no state and every
///     member is thread-safe.
/// </remarks>
internal static class ExclusionFilter
{
    /// <summary>
    ///     Applies the exclusions to a set of dictionaries.
    /// </summary>
    /// <remarks>
    ///     A word is excluded from every dictionary that has it. Matching is on folded words, so every
    ///     letter case of an excluded word is removed together. Allocates the result; the inputs are
    ///     not modified.
    /// </remarks>
    /// <param name="dictionaries">The words of each dictionary, in the order the result keeps. Must not be null.</param>
    /// <param name="exclusions">The folded words to exclude. Must not be null.</param>
    /// <returns>The remaining words of each dictionary, how many words were removed from each, and the exclusions no dictionary had.</returns>
    internal static ExclusionFilterResult Apply(
        IReadOnlyList<IReadOnlyDictionary<string, uint>> dictionaries,
        IReadOnlyDictionary<string, uint> exclusions)
    {
        var remaining = new List<IReadOnlyDictionary<string, uint>>(dictionaries.Count);
        var removed = new List<int>(dictionaries.Count);
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Copy each dictionary without the excluded words, remembering which exclusions found a word to remove
        foreach (var dictionary in dictionaries)
        {
            var kept = new Dictionary<string, uint>(dictionary.Count, StringComparer.Ordinal);
            var removedCount = 0;
            foreach (var (word, flags) in dictionary)
            {
                if (exclusions.ContainsKey(word))
                {
                    removedCount++;
                    used.Add(word);
                    continue;
                }

                kept.Add(word, flags);
            }

            remaining.Add(kept);
            removed.Add(removedCount);
        }

        // An exclusion that no dictionary has does nothing, so it is reported rather than silently kept
        var unused = exclusions.Keys.Where(word => !used.Contains(word)).ToList();
        unused.Sort(StringComparer.Ordinal);
        return new ExclusionFilterResult(remaining, removed, unused);
    }
}
