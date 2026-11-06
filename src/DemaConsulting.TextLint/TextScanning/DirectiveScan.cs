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
///     What the in-document directives of one text say: where checking is switched off, which words the
///     document adds, which patterns it adds, and which directives are recognized but not supported.
/// </summary>
/// <remarks>
///     Produced by <see cref="DocumentDirectives.Scan"/>. The lists are immutable arrays, so the value can
///     be shared freely. Offsets are UTF-16 offsets into the scanned text. Instances are immutable and
///     therefore thread-safe.
/// </remarks>
internal sealed class DirectiveScan
{
    /// <summary>
    ///     The scan of a text that contains no directives, shared because it never changes.
    /// </summary>
    internal static readonly DirectiveScan Empty = new([], [], [], [], []);

    /// <summary>
    ///     Initializes a new instance of the <see cref="DirectiveScan"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only <see cref="DocumentDirectives"/> produces scans. The arrays are taken as
    ///     they are; the caller must not keep a mutable reference to them.
    /// </remarks>
    /// <param name="excludedRanges">The ranges in which checking is switched off. Not sorted or merged.</param>
    /// <param name="ignoredWords">The words added by <c>ignore</c> directives, in order.</param>
    /// <param name="words">The words added by <c>words</c> directives, in order.</param>
    /// <param name="unsupportedDirectives">The ranges of the names of recognized but unsupported directives, in order.</param>
    /// <param name="patternDirectives">The patterns that <c>ignoreRegExp</c> directives add, in text order.</param>
    internal DirectiveScan(
        ImmutableArray<TextRange> excludedRanges,
        ImmutableArray<string> ignoredWords,
        ImmutableArray<string> words,
        ImmutableArray<TextRange> unsupportedDirectives,
        ImmutableArray<PatternDirective> patternDirectives)
    {
        ExcludedRanges = excludedRanges;
        IgnoredWords = ignoredWords;
        Words = words;
        UnsupportedDirectives = unsupportedDirectives;
        PatternDirectives = patternDirectives;
    }

    /// <summary>
    ///     Gets the ranges in which checking is switched off.
    /// </summary>
    /// <remarks>
    ///     Includes disabled blocks, disabled lines, the line after a next-line directive, and the text of the
    ///     directives that add words (so the words they list are not themselves checked).
    /// </remarks>
    /// <value>The ranges in the order the directive patterns found them; not sorted, and possibly overlapping.</value>
    internal ImmutableArray<TextRange> ExcludedRanges { get; }

    /// <summary>
    ///     Gets the words that <c>ignore</c> directives add to the document.
    /// </summary>
    /// <value>The words in order, without duplicates removed; empty when there are none.</value>
    internal ImmutableArray<string> IgnoredWords { get; }

    /// <summary>
    ///     Gets the words that <c>words</c> directives add to the document.
    /// </summary>
    /// <value>The words in order, without duplicates removed; empty when there are none.</value>
    internal ImmutableArray<string> Words { get; }

    /// <summary>
    ///     Gets the ranges of the names of directives that are recognized but not supported.
    /// </summary>
    /// <remarks>
    ///     A directive inside a region where checking is switched off is not included.
    /// </remarks>
    /// <value>The ranges in text order; empty when there are none.</value>
    internal ImmutableArray<TextRange> UnsupportedDirectives { get; }

    /// <summary>
    ///     Gets the patterns that <c>ignoreRegExp</c> directives add to the document.
    /// </summary>
    /// <remarks>
    ///     The patterns apply to the whole text, wherever the directive is, as in cspell. The scan only finds
    ///     them; resolving them to regular expressions, which needs the pattern names of the settings, is the
    ///     caller's work.
    /// </remarks>
    /// <value>The patterns in text order; empty when there are none.</value>
    internal ImmutableArray<PatternDirective> PatternDirectives { get; }

    /// <summary>
    ///     One pattern written in an <c>ignoreRegExp</c> directive.
    /// </summary>
    /// <param name="Range">The range of the pattern as written, in the scanned text.</param>
    /// <param name="Text">The pattern as written: a literal between slashes with flags, a bare expression, or a name.</param>
    /// <param name="IsReportable">
    ///     <see langword="true"/> when a pattern that cannot be used should be reported; <see langword="false"/>
    ///     inside a region where checking is switched off, where directives are not reported.
    /// </param>
    internal readonly record struct PatternDirective(TextRange Range, string Text, bool IsReportable);
}
