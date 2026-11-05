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

using System.Collections.Immutable;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     An immutable set of flagged words and tokens, each with its suggestions.
/// </summary>
/// <remarks>
///     Backs the <c>flagWords</c> setting. An entry written entirely in lower case matches the word in any
///     letter case, and an entry with a capital letter matches only that exact spelling, as in cspell. The
///     lookups take a span of the checked text and do not allocate. The set is built once and never
///     changed, so it is safe for concurrent use.
/// </remarks>
internal sealed class FlagSet
{
    /// <summary>
    ///     The entries that match single words.
    /// </summary>
    private readonly FlagTable _words = new();

    /// <summary>
    ///     The entries that match whole tokens with digits or punctuation.
    /// </summary>
    private readonly FlagTable _tokens = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="FlagSet"/> class from interpreted entries.
    /// </summary>
    /// <remarks>
    ///     When an entry is repeated, the first one and its suggestions are kept.
    /// </remarks>
    /// <param name="entries">The entries to hold. Must not be null.</param>
    internal FlagSet(IEnumerable<ListEntry> entries)
    {
        foreach (var entry in entries)
        {
            (entry.IsSingleWord ? _words : _tokens).Add(entry);
        }

        _words.Freeze();
        _tokens.Freeze();
    }

    /// <summary>
    ///     Gets a value indicating whether the set holds any entry.
    /// </summary>
    /// <value><see langword="true"/> when no entry is held.</value>
    internal bool IsEmpty => _words.Count == 0 && _tokens.Count == 0;

    /// <summary>
    ///     Gets a value indicating whether the set holds any entry that contains digits or punctuation.
    /// </summary>
    /// <value><see langword="true"/> when at least one token entry is held.</value>
    internal bool HasTokens => _tokens.Count > 0;

    /// <summary>
    ///     Determines whether a word is flagged.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to look up.</param>
    /// <param name="suggestions">Receives the suggestions of the matching entry; the default array when no entry matches.</param>
    /// <returns><see langword="true"/> when an entry matches.</returns>
    internal bool TryMatchWord(ReadOnlySpan<char> word, out ImmutableArray<string> suggestions) =>
        _words.TryMatch(word, out suggestions);

    /// <summary>
    ///     Determines whether a token with digits or punctuation is flagged.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="token">The token to look up, without edge punctuation.</param>
    /// <param name="suggestions">Receives the suggestions of the matching entry; the default array when no entry matches.</param>
    /// <returns><see langword="true"/> when an entry matches.</returns>
    internal bool TryMatchToken(ReadOnlySpan<char> token, out ImmutableArray<string> suggestions) =>
        _tokens.TryMatch(token, out suggestions);

    /// <summary>
    ///     The entries of one kind (words or tokens), split by whether they match in any letter case.
    /// </summary>
    /// <remarks>
    ///     Not thread-safe while entries are being added; after <see cref="Freeze"/> it is read-only and
    ///     safe for concurrent use.
    /// </remarks>
    private sealed class FlagTable
    {
        /// <summary>
        ///     The entries that contain a capital letter, matched exactly.
        /// </summary>
        private readonly Dictionary<string, ImmutableArray<string>> _exact = new(StringComparer.Ordinal);

        /// <summary>
        ///     The entries written in lower case, matched in any letter case.
        /// </summary>
        private readonly Dictionary<string, ImmutableArray<string>> _anyCase = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     A span-accepting view of <see cref="_exact"/>; assigned by <see cref="Freeze"/>.
        /// </summary>
        private Dictionary<string, ImmutableArray<string>>.AlternateLookup<ReadOnlySpan<char>> _exactLookup;

        /// <summary>
        ///     A span-accepting view of <see cref="_anyCase"/>; assigned by <see cref="Freeze"/>.
        /// </summary>
        private Dictionary<string, ImmutableArray<string>>.AlternateLookup<ReadOnlySpan<char>> _anyCaseLookup;

        /// <summary>
        ///     Gets the number of entries held.
        /// </summary>
        /// <value>The number of distinct entries.</value>
        internal int Count => _exact.Count + _anyCase.Count;

        /// <summary>
        ///     Adds an entry, keeping the first of any repeated entry.
        /// </summary>
        /// <remarks>
        ///     An entry with no capital letter matches in any letter case; any other entry matches only
        ///     its exact spelling.
        /// </remarks>
        /// <param name="entry">The entry to add.</param>
        internal void Add(ListEntry entry)
        {
            var anyCase = string.Equals(entry.Word, entry.Word.ToLowerInvariant(), StringComparison.Ordinal);
            _ = (anyCase ? _anyCase : _exact).TryAdd(entry.Word, entry.Suggestions);
        }

        /// <summary>
        ///     Prepares the span-accepting lookups once every entry has been added.
        /// </summary>
        /// <remarks>
        ///     Must be called exactly once, after the last <see cref="Add"/>.
        /// </remarks>
        internal void Freeze()
        {
            _exactLookup = _exact.GetAlternateLookup<ReadOnlySpan<char>>();
            _anyCaseLookup = _anyCase.GetAlternateLookup<ReadOnlySpan<char>>();
        }

        /// <summary>
        ///     Finds the entry that matches a word or token.
        /// </summary>
        /// <remarks>
        ///     Does not allocate. Safe for concurrent use after <see cref="Freeze"/>.
        /// </remarks>
        /// <param name="text">The text to look up.</param>
        /// <param name="suggestions">Receives the suggestions of the matching entry; the default array when none matches.</param>
        /// <returns><see langword="true"/> when an entry matches.</returns>
        internal bool TryMatch(ReadOnlySpan<char> text, out ImmutableArray<string> suggestions) =>
            _exactLookup.TryGetValue(text, out suggestions) || _anyCaseLookup.TryGetValue(text, out suggestions);
    }
}
