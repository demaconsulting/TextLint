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

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     The outcome of checking the files of a directory: every finding, every file that could not be
///     checked completely, and the counts.
/// </summary>
/// <remarks>
///     The result is complete and ordered deterministically: findings are ordered by file path (ordinal),
///     then by position in the file, and file diagnostics by file path, whatever number of files were
///     checked at the same time. Both lists are copied into immutable arrays when the result is created,
///     so no consumer can change them afterwards, even by casting an exposed list to a mutable collection
///     interface. Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed class SpellCheckResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpellCheckResult"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the spell checker produces results. The lists are copied.
    /// </remarks>
    /// <param name="issues">The findings in result order. Must not be null.</param>
    /// <param name="fileDiagnostics">The per-file problems in result order. Must not be null.</param>
    /// <param name="filesChecked">The number of files that were read and checked completely.</param>
    /// <param name="filesSkipped">The number of files skipped because they were binary or too large.</param>
    internal SpellCheckResult(
        IEnumerable<SpellingIssue> issues,
        IEnumerable<FileDiagnostic> fileDiagnostics,
        int filesChecked,
        int filesSkipped)
    {
        Issues = issues.ToImmutableArray();
        FileDiagnostics = fileDiagnostics.ToImmutableArray();
        FilesChecked = filesChecked;
        FilesSkipped = filesSkipped;
    }

    /// <summary>
    ///     Gets the findings of the run.
    /// </summary>
    /// <value>
    ///     An immutable list ordered by file path (ordinal), then line, then column; empty when nothing was found.
    /// </value>
    public IReadOnlyList<SpellingIssue> Issues { get; }

    /// <summary>
    ///     Gets the files that were skipped or could not be checked.
    /// </summary>
    /// <value>An immutable list ordered by file path (ordinal); empty when every selected file was checked.</value>
    public IReadOnlyList<FileDiagnostic> FileDiagnostics { get; }

    /// <summary>
    ///     Gets the number of files that were read and checked completely.
    /// </summary>
    /// <value>The count; files with findings are included.</value>
    public int FilesChecked { get; }

    /// <summary>
    ///     Gets the number of files that were skipped because they looked binary or were too large.
    /// </summary>
    /// <value>The count of binary and too-large files; unreadable and timed-out files are not included.</value>
    public int FilesSkipped { get; }

    /// <summary>
    ///     Gets a value indicating whether the run found anything a consumer should fail on.
    /// </summary>
    /// <remarks>
    ///     True when there is any finding or any file diagnostic that is an error; skipped files alone are
    ///     not a problem. Exit codes and CI gates should be based on this.
    /// </remarks>
    /// <value><see langword="true"/> when there is a finding or an error diagnostic.</value>
    public bool HasProblems => Issues.Count > 0 || FileDiagnostics.Any(static diagnostic => diagnostic.IsError);
}
