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

using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     The outcome of applying the spelling-variant rule: the technical words that remain, how many
///     variants were kept or stripped, and the keep-list words that should not be there.
/// </summary>
/// <remarks>
///     The counts exist so that a build (and a person reviewing a refresh of the lists) can see
///     what the rule did without diffing dictionaries. The words are copied into an immutable frozen
///     dictionary (keyed by ordinal comparison, as the rule folds them) and the list into an immutable array when
///     the result is created, so neither a later change to the collections the caller passed in nor
///     any caller of the result can change it afterwards. Instances are immutable after construction
///     and thread-safe.
/// </remarks>
internal sealed class VariantFilterResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VariantFilterResult"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only <see cref="VariantFilter"/> creates results.
    /// </remarks>
    /// <param name="words">The technical words that remain, with their capitalization-form flags. Must not be null.</param>
    /// <param name="keptViaLanguage">The number of spelling variants kept because a language list has them.</param>
    /// <param name="keptViaKeepList">The number of spelling variants kept only because the keep list has them.</param>
    /// <param name="stripped">The number of spelling variants removed from the general lists.</param>
    /// <param name="keepWordsNotVariants">The keep-list words that are not spelling variants, in ordinal order. Must not be null.</param>
    internal VariantFilterResult(
        IReadOnlyDictionary<string, uint> words,
        int keptViaLanguage,
        int keptViaKeepList,
        int stripped,
        IReadOnlyList<string> keepWordsNotVariants)
    {
        Words = words.ToFrozenDictionary(StringComparer.Ordinal);
        KeptViaLanguage = keptViaLanguage;
        KeptViaKeepList = keptViaKeepList;
        Stripped = stripped;
        KeepWordsNotVariants = keepWordsNotVariants.ToImmutableArray();
    }

    /// <summary>
    ///     Gets the technical words that remain after the rule.
    /// </summary>
    /// <value>Each distinct folded word mapped to its capitalization-form flags.</value>
    internal IReadOnlyDictionary<string, uint> Words { get; }

    /// <summary>
    ///     Gets the number of spelling variants kept because a language list has them.
    /// </summary>
    /// <value>The count of distinct variant words found in the language lists.</value>
    internal int KeptViaLanguage { get; }

    /// <summary>
    ///     Gets the number of spelling variants kept only because the keep list has them.
    /// </summary>
    /// <value>The count of distinct variant words in the keep list that no language list has.</value>
    internal int KeptViaKeepList { get; }

    /// <summary>
    ///     Gets the number of spelling variants removed from the general lists.
    /// </summary>
    /// <value>The count of distinct variant words of the general lists that were not kept.</value>
    internal int Stripped { get; }

    /// <summary>
    ///     Gets the keep-list words that are not spelling variants.
    /// </summary>
    /// <value>The words in ordinal order; empty when the keep list is tight.</value>
    internal IReadOnlyList<string> KeepWordsNotVariants { get; }
}
