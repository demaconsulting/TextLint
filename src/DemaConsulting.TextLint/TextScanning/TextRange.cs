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

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     A run of UTF-16 code units inside a text, identified by where it starts and how long it is.
/// </summary>
/// <remarks>
///     Every unit of the text scanning subsystem reports its findings as offsets into the original
///     text instead of copies of the text, so excluded ranges, words, and directives can be combined
///     without allocating and so positions can be converted to a line and column in one place. The
///     offsets are counted in UTF-16 code units, which is how .NET strings are indexed and how cspell
///     reports columns. The type is an immutable value and therefore thread-safe.
/// </remarks>
/// <param name="Start">The zero-based offset of the first code unit of the range.</param>
/// <param name="Length">The number of code units in the range; zero for an empty range.</param>
internal readonly record struct TextRange(int Start, int Length)
{
    /// <summary>
    ///     Gets the offset just past the last code unit of the range.
    /// </summary>
    /// <value>The sum of <see cref="Start"/> and <see cref="Length"/>.</value>
    public int End => Start + Length;

    /// <summary>
    ///     Gets a value indicating whether the range holds no code units.
    /// </summary>
    /// <value><see langword="true"/> when <see cref="Length"/> is zero.</value>
    public bool IsEmpty => Length == 0;
}
