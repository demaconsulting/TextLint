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

using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     An immutable set of accepted words and tokens, matched without regard to letter case.
/// </summary>
/// <remarks>
///     Backs the <c>words</c> and <c>ignoreWords</c> settings and the document directives. Entries that are
///     a single word are looked up against words; entries that contain digits or punctuation are looked up
///     against whole tokens. Both lookups take a span of the checked text, so asking whether a word is in
///     the set never allocates. The set is built once and never changed, so it is safe for concurrent use.
/// </remarks>
internal sealed class WordSet
{
    /// <summary>
    ///     The set that holds nothing, shared because it never changes.
    /// </summary>
    internal static readonly WordSet Empty = new([]);

    /// <summary>
    ///     The single-word entries, compared ignoring letter case.
    /// </summary>
    private readonly HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The entries that contain digits or punctuation, compared ignoring letter case.
    /// </summary>
    private readonly HashSet<string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     A view of <see cref="_words"/> that accepts a span, so lookups need no string.
    /// </summary>
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _wordLookup;

    /// <summary>
    ///     A view of <see cref="_tokens"/> that accepts a span, so lookups need no string.
    /// </summary>
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _tokenLookup;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordSet"/> class from interpreted entries.
    /// </summary>
    /// <remarks>
    ///     Duplicate entries, in any letter case, are stored once.
    /// </remarks>
    /// <param name="entries">The entries to hold. Must not be null.</param>
    internal WordSet(IEnumerable<ListEntry> entries)
    {
        // Sort each entry into the set of words or the set of tokens
        foreach (var entry in entries)
        {
            _ = entry.IsSingleWord ? _words.Add(entry.Word) : _tokens.Add(entry.Word);
        }

        // Prepare the allocation-free lookups once the sets are filled
        _wordLookup = _words.GetAlternateLookup<ReadOnlySpan<char>>();
        _tokenLookup = _tokens.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    ///     Gets a value indicating whether the set holds any entry that contains digits or punctuation.
    /// </summary>
    /// <remarks>
    ///     Lets the checker skip token matching altogether when no such entry exists, which is the common case.
    /// </remarks>
    /// <value><see langword="true"/> when at least one token entry is held.</value>
    internal bool HasTokens => _tokens.Count > 0;

    /// <summary>
    ///     Determines whether a word is one of the single-word entries.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to look up.</param>
    /// <returns><see langword="true"/> when the word is held, in any letter case.</returns>
    internal bool ContainsWord(ReadOnlySpan<char> word) => _wordLookup.Contains(word);

    /// <summary>
    ///     Determines whether a token is one of the entries that contain digits or punctuation.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="token">The token to look up, without edge punctuation.</param>
    /// <returns><see langword="true"/> when the token is held, in any letter case.</returns>
    internal bool ContainsToken(ReadOnlySpan<char> token) => _tokenLookup.Contains(token);
}
