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

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     One usable entry of a word list, normalized so that it can be matched against the text.
/// </summary>
/// <remarks>
///     Produced by <see cref="WordTokenizer.TryParseListEntry"/>, which is the only place that decides
///     whether an entry can match anything. The type is an immutable value; the suggestions array is
///     copied from the entry text and never changed, so the value is thread-safe.
/// </remarks>
/// <param name="Word">The entry without edge punctuation and without its suggestions.</param>
/// <param name="IsSingleWord">
///     <see langword="true"/> when the entry is exactly one word, so it is matched against words;
///     <see langword="false"/> when it contains digits or punctuation, so it is matched against whole tokens.
/// </param>
/// <param name="Suggestions">The suggestions given with a flagged-word entry; empty for every other entry.</param>
internal readonly record struct ListEntry(string Word, bool IsSingleWord, ImmutableArray<string> Suggestions);
