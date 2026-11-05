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

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     The outcome of reading one file: its decoded text, or the reason it will not be checked.
/// </summary>
/// <remarks>
///     Reading problems are expected on real repositories (binary files, locked files, huge generated
///     files), so they are values, not exceptions. Exactly one of <see cref="Text"/> and
///     <see cref="Kind"/> is set. Instances are immutable and therefore thread-safe.
/// </remarks>
internal sealed class FileTextContent
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileTextContent"/> class.
    /// </summary>
    /// <remarks>
    ///     Private; use the factory methods, which keep the invariant that exactly one outcome is set.
    /// </remarks>
    /// <param name="text">The decoded text, or null when the file is not to be checked.</param>
    /// <param name="kind">The reason the file is not to be checked, or null when <paramref name="text"/> is set.</param>
    /// <param name="message">The explanation of the reason, or null when <paramref name="text"/> is set.</param>
    private FileTextContent(string? text, FileDiagnosticKind? kind, string? message)
    {
        Text = text;
        Kind = kind;
        Message = message;
    }

    /// <summary>
    ///     Gets the decoded text of the file.
    /// </summary>
    /// <value>The text without its byte-order mark; null when the file is not to be checked.</value>
    internal string? Text { get; }

    /// <summary>
    ///     Gets the reason the file is not to be checked.
    /// </summary>
    /// <value>The reason; null when the file was read and has text.</value>
    internal FileDiagnosticKind? Kind { get; }

    /// <summary>
    ///     Gets the explanation of the reason the file is not to be checked.
    /// </summary>
    /// <value>A human-readable message; null when the file was read and has text.</value>
    internal string? Message { get; }

    /// <summary>
    ///     Creates the outcome of a file that was read.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The decoded text. Must not be null.</param>
    /// <returns>The outcome.</returns>
    internal static FileTextContent FromText(string text) => new(text, null, null);

    /// <summary>
    ///     Creates the outcome of a file that is not to be checked.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="kind">The reason.</param>
    /// <param name="message">The explanation. Must not be null.</param>
    /// <returns>The outcome.</returns>
    internal static FileTextContent NotChecked(FileDiagnosticKind kind, string message) => new(null, kind, message);
}
