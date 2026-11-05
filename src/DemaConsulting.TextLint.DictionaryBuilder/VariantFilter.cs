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

using System.Runtime.InteropServices;

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     Combines the technical word lists under the spelling-variant rule: a word that is a US-only
///     or GB-only spelling variant is known in every language only when a programming-language list
///     or the reviewed keep list has it.
/// </summary>
/// <remarks>
///     The language setting (en-US or en-GB) must decide ordinary spelling variants such as
///     <c>favorite</c> or <c>flavors</c>, but the general technical lists, imported from upstream,
///     contain many of them and would make them known everywhere. Programming keywords and API names
///     such as <c>color</c> are different: they are fixed by the language and must stay known
///     however the user spells English, which is why the language lists are exempt. A short,
///     human-reviewed keep list holds the remaining genuine programming terms in variant spellings.
///     The rule is a pure function over four sets of folded words, so it can be verified without a
///     file system, and it is applied at build time so that the source lists stay faithful to their
///     upstream origin. The class holds no state and every member is thread-safe.
/// </remarks>
internal static class VariantFilter
{
    /// <summary>
    ///     Applies the spelling-variant rule.
    /// </summary>
    /// <remarks>
    ///     Every language-list word and every keep-list word is kept. A general-list word is kept
    ///     unless it is a variant that neither the language lists nor the keep list have, in which
    ///     case it is stripped. The capitalization-form flags of a kept word are the union of its
    ///     flags in every list that has it. Allocates the result; the inputs are not modified.
    /// </remarks>
    /// <param name="general">The words of the general technical lists. Must not be null.</param>
    /// <param name="language">The words of the programming-language lists. Must not be null.</param>
    /// <param name="keep">The words of the reviewed keep list. Must not be null.</param>
    /// <param name="variants">The folded words of the US-only and GB-only dictionaries. Must not be null.</param>
    /// <returns>The remaining words, the counts, and the keep-list words that are not variants.</returns>
    internal static VariantFilterResult Apply(
        IReadOnlyDictionary<string, uint> general,
        IReadOnlyDictionary<string, uint> language,
        IReadOnlyDictionary<string, uint> keep,
        IReadOnlySet<string> variants)
    {
        // Every language word stays, variant or not, because programming languages fix their own spelling
        var words = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var (word, flags) in language)
        {
            Merge(words, word, flags);
        }

        var keptViaLanguage = language.Keys.Count(variants.Contains);

        // Every keep-list word stays; one that is not a variant is reported so the list stays tight
        var notVariants = new List<string>();
        var keptViaKeepList = 0;
        foreach (var (word, flags) in keep)
        {
            Merge(words, word, flags);
            if (!variants.Contains(word))
            {
                notVariants.Add(word);
            }
            else if (!language.ContainsKey(word))
            {
                keptViaKeepList++;
            }
        }

        // A general word stays unless it is a variant that nothing exempts
        var stripped = 0;
        foreach (var (word, flags) in general)
        {
            if (variants.Contains(word) && !language.ContainsKey(word) && !keep.ContainsKey(word))
            {
                stripped++;
                continue;
            }

            Merge(words, word, flags);
        }

        notVariants.Sort(StringComparer.Ordinal);
        return new VariantFilterResult(words, keptViaLanguage, keptViaKeepList, stripped, notVariants);
    }

    /// <summary>
    ///     Adds a word to a set, or ORs its flags into the flags it already has.
    /// </summary>
    /// <remarks>
    ///     Keeps the knowledge of every spelling a word had in every list. Modifies the set passed
    ///     in; thread-safe only if the caller owns the set.
    /// </remarks>
    /// <param name="words">The set to add to. Must not be null.</param>
    /// <param name="word">The folded word.</param>
    /// <param name="flags">The capitalization-form flags to merge.</param>
    private static void Merge(Dictionary<string, uint> words, string word, uint flags)
    {
        ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(words, word, out _);
        existing |= flags;
    }
}
