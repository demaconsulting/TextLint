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
///     One problem found in, or about, the word lists while building a dictionary.
/// </summary>
/// <remarks>
///     Problems are returned as data rather than thrown so that a build can report every problem
///     at once and so that the output has one uniform format. The format is the compiler style
///     <c>path(line): error: message</c> (or <c>path: error: message</c> when the problem has no
///     line), which MSBuild, editors, and CI log parsers turn into clickable errors. Every
///     diagnostic is an error. Instances are immutable and thread-safe.
/// </remarks>
internal sealed class WordListDiagnostic
{
    /// <summary>
    ///     The line value of a problem that concerns a whole file or folder rather than one line.
    /// </summary>
    internal const int NoLine = 0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordListDiagnostic"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the builder's own units produce diagnostics.
    /// </remarks>
    /// <param name="path">The file or folder the problem concerns. Must not be null.</param>
    /// <param name="line">The 1-based line of the problem, or <see cref="NoLine"/> when it has no line.</param>
    /// <param name="message">The description of the problem. Must not be null.</param>
    internal WordListDiagnostic(string path, int line, string message)
    {
        Path = path;
        Line = line;
        Message = message;
    }

    /// <summary>
    ///     Gets the file or folder the problem concerns.
    /// </summary>
    /// <value>The path exactly as the builder resolved it.</value>
    internal string Path { get; }

    /// <summary>
    ///     Gets the 1-based line of the problem.
    /// </summary>
    /// <value>The line number, or <see cref="NoLine"/> when the problem concerns the whole file or folder.</value>
    internal int Line { get; }

    /// <summary>
    ///     Gets the description of the problem.
    /// </summary>
    /// <value>A human-readable message that names the offending entry where relevant.</value>
    internal string Message { get; }

    /// <summary>
    ///     Formats the diagnostic in the compiler style <c>path(line): error: message</c>.
    /// </summary>
    /// <remarks>
    ///     Stateless formatting of immutable data; thread-safe.
    /// </remarks>
    /// <returns>The formatted diagnostic, without a trailing newline.</returns>
    public override string ToString() =>
        Line == NoLine ? $"{Path}: error: {Message}" : $"{Path}({Line}): error: {Message}";
}
