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
///     The outcome of reading one dictionary's word lists: the merged words, or the problems that
///     prevented a trustworthy result.
/// </summary>
/// <remarks>
///     Returning a result instead of throwing lets the builder report every problem in every list
///     at once. The diagnostics are copied into an immutable array and the words into an immutable
///     frozen dictionary when the result is created, so neither a later change to the collection the
///     caller passed in nor any caller of the result can change it afterwards. Words are keyed by
///     ordinal comparison, as the reader folds them. Instances are immutable after construction and thread-safe.
/// </remarks>
internal sealed class WordListReadResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WordListReadResult"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the reader creates results.
    /// </remarks>
    /// <param name="words">
    ///     The merged words: each distinct folded word with the capitalization-form flags OR-ed over
    ///     every source spelling. Must not be null.
    /// </param>
    /// <param name="diagnostics">The problems found, in source order, including any cap summary. Must not be null.</param>
    /// <param name="problemCount">The total number of problems found, which may exceed the number of diagnostics kept.</param>
    /// <param name="fileCount">The number of word-list files that were read.</param>
    internal WordListReadResult(
        IReadOnlyDictionary<string, uint> words,
        IReadOnlyList<WordListDiagnostic> diagnostics,
        int problemCount,
        int fileCount)
    {
        Words = words.ToFrozenDictionary(StringComparer.Ordinal);
        Diagnostics = diagnostics.ToImmutableArray();
        ProblemCount = problemCount;
        FileCount = fileCount;
    }

    /// <summary>
    ///     Gets the merged words.
    /// </summary>
    /// <value>
    ///     Each distinct folded word mapped to the capitalization-form flags OR-ed over every source
    ///     spelling. Not meaningful when <see cref="HasProblems"/> is <see langword="true"/>.
    /// </value>
    internal IReadOnlyDictionary<string, uint> Words { get; }

    /// <summary>
    ///     Gets the problems found, in source order.
    /// </summary>
    /// <value>
    ///     At most <see cref="WordListReader.MaxDiagnostics"/> detailed problems, followed by one
    ///     summary diagnostic when more were found. Empty when reading succeeded.
    /// </value>
    internal IReadOnlyList<WordListDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets the total number of problems found.
    /// </summary>
    /// <value>The count of all problems, including those not kept in <see cref="Diagnostics"/>.</value>
    internal int ProblemCount { get; }

    /// <summary>
    ///     Gets the number of word-list files that were read.
    /// </summary>
    /// <value>The file count; zero when the dictionary folder holds no word-list files.</value>
    internal int FileCount { get; }

    /// <summary>
    ///     Gets a value indicating whether any problem was found.
    /// </summary>
    /// <value><see langword="true"/> when at least one problem was found; otherwise <see langword="false"/>.</value>
    internal bool HasProblems => ProblemCount > 0;
}
