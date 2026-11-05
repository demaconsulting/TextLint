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
///     A problem with one file that kept it from being checked completely: it was skipped on purpose, or
///     it could not be read or checked.
/// </summary>
/// <remarks>
///     Per-file problems are returned as data, not thrown, so that one locked or binary file does not fail
///     a whole repository run. Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed class FileDiagnostic
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileDiagnostic"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the spell checker produces diagnostics; consumers only read them.
    /// </remarks>
    /// <param name="filePath">The path of the file relative to the root directory, with forward slashes. Must not be null.</param>
    /// <param name="kind">The reason the file was not checked completely.</param>
    /// <param name="message">A human-readable explanation. Must not be null.</param>
    internal FileDiagnostic(string filePath, FileDiagnosticKind kind, string message)
    {
        FilePath = filePath;
        Kind = kind;
        Message = message;
    }

    /// <summary>
    ///     Gets the path of the file the problem concerns.
    /// </summary>
    /// <value>The path relative to the root directory, with forward slashes.</value>
    public string FilePath { get; }

    /// <summary>
    ///     Gets the reason the file was not checked completely.
    /// </summary>
    /// <value>The reason.</value>
    public FileDiagnosticKind Kind { get; }

    /// <summary>
    ///     Gets the explanation of the problem.
    /// </summary>
    /// <value>A human-readable message; for an unreadable file it includes the reason the system gave.</value>
    public string Message { get; }

    /// <summary>
    ///     Gets a value indicating whether the problem is an error rather than a deliberate skip.
    /// </summary>
    /// <remarks>
    ///     A file that is unreadable or timed out was meant to be checked and was not, so a consumer should
    ///     treat it as a failure. A binary or too-large file was skipped by design and is only reported.
    /// </remarks>
    /// <value><see langword="true"/> for <see cref="FileDiagnosticKind.Unreadable"/> and <see cref="FileDiagnosticKind.TimedOut"/>.</value>
    public bool IsError => Kind is FileDiagnosticKind.Unreadable or FileDiagnosticKind.TimedOut;

    /// <summary>
    ///     Formats the diagnostic in the compiler style <c>file: severity: message</c>.
    /// </summary>
    /// <remarks>
    ///     The file has no line or column, so only the file is named; the severity word is
    ///     <c>error</c> or <c>info</c>, as <see cref="IsError"/> says.
    /// </remarks>
    /// <returns>The formatted diagnostic.</returns>
    public override string ToString() => $"{FilePath}: {(IsError ? "error" : "info")}: {Message}";
}
