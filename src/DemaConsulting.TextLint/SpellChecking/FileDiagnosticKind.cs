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
///     The reason a file produced a <see cref="FileDiagnostic"/> instead of being checked completely.
/// </summary>
/// <remarks>
///     A run over many files must not fail because of one file, so each problem is reported as data. The
///     kinds fall into two groups: files that were deliberately skipped (<see cref="Binary"/> and
///     <see cref="TooLarge"/>), which are information, and files that could not be checked
///     (<see cref="Unreadable"/> and <see cref="TimedOut"/>), which are errors.
/// </remarks>
public enum FileDiagnosticKind
{
    /// <summary>
    ///     The file could not be opened or read, for example because it was removed, is locked, or access was denied.
    /// </summary>
    Unreadable,

    /// <summary>
    ///     The file looks like binary data (it contains a NUL character near its start) and was skipped.
    /// </summary>
    Binary,

    /// <summary>
    ///     The file is larger than the <c>maxFileSize</c> setting, or too large to hold in memory, and was skipped.
    /// </summary>
    TooLarge,

    /// <summary>
    ///     Checking the file took longer than the time limit of the ignore patterns, which only hostile
    ///     content can cause, so the file was not checked.
    /// </summary>
    TimedOut,
}
