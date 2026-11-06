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
///     The outcome of applying the exclusions: the words that remain in each dictionary, how many
///     words were removed from each, and the exclusions that no dictionary had.
/// </summary>
/// <remarks>
///     The counts and the unused words exist so that a build (and a person reviewing a refresh of the
///     lists) can see what the exclusions did without comparing dictionaries, and so that a stale
///     exclusion is noticed. The words are copied into immutable frozen dictionaries (keyed by ordinal
///     comparison, as the filter folds them) and the lists into immutable arrays when the result is
///     created, so neither a later change to the collections the caller passed in nor any caller of
///     the result can change it afterwards. Instances are immutable after construction and thread-safe.
/// </remarks>
internal sealed class ExclusionFilterResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExclusionFilterResult"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only <see cref="ExclusionFilter"/> creates results.
    /// </remarks>
    /// <param name="dictionaries">The remaining words of each dictionary, with their capitalization-form flags. Must not be null.</param>
    /// <param name="removed">The number of words removed from each dictionary, in the same order. Must not be null.</param>
    /// <param name="unusedWords">The exclusions that no dictionary had, in ordinal order. Must not be null.</param>
    internal ExclusionFilterResult(
        IReadOnlyList<IReadOnlyDictionary<string, uint>> dictionaries,
        IReadOnlyList<int> removed,
        IReadOnlyList<string> unusedWords)
    {
        Dictionaries = [.. dictionaries.Select(words => (IReadOnlyDictionary<string, uint>)words.ToFrozenDictionary(StringComparer.Ordinal))];
        Removed = removed.ToImmutableArray();
        UnusedWords = unusedWords.ToImmutableArray();
    }

    /// <summary>
    ///     Gets the words that remain in each dictionary.
    /// </summary>
    /// <value>One map of folded words to capitalization-form flags per input dictionary, in input order.</value>
    internal IReadOnlyList<IReadOnlyDictionary<string, uint>> Dictionaries { get; }

    /// <summary>
    ///     Gets the number of words removed from each dictionary.
    /// </summary>
    /// <value>One count per input dictionary, in input order.</value>
    internal IReadOnlyList<int> Removed { get; }

    /// <summary>
    ///     Gets the exclusions that no dictionary had.
    /// </summary>
    /// <value>The words in ordinal order; empty when every exclusion removed at least one word.</value>
    internal IReadOnlyList<string> UnusedWords { get; }
}
