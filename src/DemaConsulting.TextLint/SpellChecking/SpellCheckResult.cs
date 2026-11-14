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
///     checked completely, the counts, and the notes about how the settings were applied.
/// </summary>
/// <remarks>
///     The result is complete and ordered deterministically: findings are ordered by file path (ordinal),
///     then by position in the file, and file diagnostics by file path, whatever number of files were
///     checked at the same time. The lists are copied into immutable arrays when the result is created,
///     so no consumer can change them afterwards, even by casting an exposed list to a mutable collection
///     interface. The notes (<see cref="Notes"/>) never count as a problem. Instances are immutable and
///     therefore thread-safe.
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
    /// <param name="filesDisabled">The number of selected files that the settings disabled.</param>
    /// <param name="notes">The notes about how the settings were applied; null means none.</param>
    internal SpellCheckResult(
        IEnumerable<SpellingIssue> issues,
        IEnumerable<FileDiagnostic> fileDiagnostics,
        int filesChecked,
        int filesSkipped,
        int filesDisabled = 0,
        IEnumerable<SpellCheckNote>? notes = null)
    {
        Issues = issues.ToImmutableArray();
        FileDiagnostics = fileDiagnostics.ToImmutableArray();
        FilesChecked = filesChecked;
        FilesSkipped = filesSkipped;
        FilesDisabled = filesDisabled;
        Notes = (notes ?? []).ToImmutableArray();
    }

    /// <summary>
    ///     Creates a copy of this result that also carries what the settings did to the run.
    /// </summary>
    /// <remarks>
    ///     Internal because only the spell checker knows which files the settings disabled and which notes apply;
    ///     the checking of the files themselves does not. The lists are copied. Thread-safe.
    /// </remarks>
    /// <param name="filesDisabled">The number of selected files that the settings disabled.</param>
    /// <param name="notes">The notes of the run in the order to report them. Must not be null.</param>
    /// <returns>A new result with the same findings and counts, and the given disabled count and notes.</returns>
    internal SpellCheckResult WithRunInfo(int filesDisabled, IEnumerable<SpellCheckNote> notes) =>
        new(Issues, FileDiagnostics, FilesChecked, FilesSkipped, filesDisabled, notes);

    /// <summary>
    ///     Gets the number of selected files that the settings disabled, which were therefore not read or checked.
    /// </summary>
    /// <remarks>
    ///     Disabled files are not counted in <see cref="FilesChecked"/> or <see cref="FilesSkipped"/> and
    ///     produce no findings or file diagnostics. A consumer can tell "nothing matched the file selection" from
    ///     "everything selected is disabled" by this number.
    /// </remarks>
    /// <value>The count; zero when no file was disabled.</value>
    public int FilesDisabled { get; }

    /// <summary>
    ///     Gets the notes about how the settings were applied.
    /// </summary>
    /// <remarks>
    ///     In a fixed order: informational notes (in the order of the overrides they concern, then the
    ///     all-files-disabled note), then warnings (in the order of the overrides). Notes never count as a
    ///     problem: see <see cref="HasProblems"/>.
    /// </remarks>
    /// <value>An immutable list; empty when there is nothing to say.</value>
    public IReadOnlyList<SpellCheckNote> Notes { get; }

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
