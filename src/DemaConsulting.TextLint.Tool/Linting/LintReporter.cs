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

using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tool.Cli;

namespace DemaConsulting.TextLint.Tool.Linting;

/// <summary>
///     Writes the result of one check to the output: each finding, each file that was skipped or could
///     not be checked, and a one-line summary.
/// </summary>
/// <remarks>
///     The line format itself belongs to the library (<see cref="SpellingIssue.Format"/> and
///     <see cref="FileDiagnostic.Format"/>), so the tool never restates message wording; this unit only
///     decides the order, which file name to show, and the summary. Results are data about the files and go
///     to standard output through <see cref="Context.WriteLine"/>, so that <c>textlint &gt; report.txt</c>
///     captures them; failures of the tool itself are written elsewhere, as errors. The class is stateless
///     and, apart from writing to the context, free of side effects.
/// </remarks>
internal static class LintReporter
{
    /// <summary>
    ///     Writes every finding, every file diagnostic, and the summary of a check.
    /// </summary>
    /// <remarks>
    ///     Findings come first, in the library's order (file path, then position), then the file
    ///     diagnostics (also in file path order), then the summary, so a log reader sees the details before
    ///     the totals. Side effects: writes lines to <paramref name="context"/>. It does not set the exit
    ///     code; the caller decides that from the result.
    /// </remarks>
    /// <param name="context">The context that receives the output. Must not be null.</param>
    /// <param name="result">The result of the check. Must not be null.</param>
    /// <param name="displayPath">
    ///     Maps a file path, relative to the search directory, to the name to show. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static void Write(Context context, SpellCheckResult result, Func<string, string> displayPath)
    {
        // Reject missing arguments before writing anything
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(displayPath);

        // Details first, then the totals
        WriteIssues(context, result, displayPath);
        WriteFileDiagnostics(context, result, displayPath);
        WriteSummary(context, result);
    }

    /// <summary>
    ///     Writes one line for each finding of a check.
    /// </summary>
    /// <remarks>
    ///     The compiler style <c>file(line,column): error: message</c> is understood by editors and CI log
    ///     parsers, so findings become clickable with no tool-specific integration. Side effects: writes
    ///     lines to <paramref name="context"/>.
    /// </remarks>
    /// <param name="context">The context that receives the output. Must not be null.</param>
    /// <param name="result">The result of the check. Must not be null.</param>
    /// <param name="displayPath">Maps a relative file path to the name to show. Must not be null.</param>
    private static void WriteIssues(Context context, SpellCheckResult result, Func<string, string> displayPath)
    {
        foreach (var issue in result.Issues)
        {
            // A finding without a file name cannot be mapped, so it is shown without one
            var name = issue.FilePath is null ? null : displayPath(issue.FilePath);
            context.WriteLine(issue.Format(name));
        }
    }

    /// <summary>
    ///     Writes one line for each file that was skipped or could not be checked.
    /// </summary>
    /// <remarks>
    ///     A deliberate skip (a binary or oversized file) is shown as information, and a file that was meant
    ///     to be checked and was not as an error, so nothing is silently left out of a run. They are results
    ///     about files and so go to standard output. Side effects: writes lines to
    ///     <paramref name="context"/>.
    /// </remarks>
    /// <param name="context">The context that receives the output. Must not be null.</param>
    /// <param name="result">The result of the check. Must not be null.</param>
    /// <param name="displayPath">Maps a relative file path to the name to show. Must not be null.</param>
    private static void WriteFileDiagnostics(Context context, SpellCheckResult result, Func<string, string> displayPath)
    {
        foreach (var diagnostic in result.FileDiagnostics)
        {
            context.WriteLine(diagnostic.Format(displayPath(diagnostic.FilePath)));
        }
    }

    /// <summary>
    ///     Writes the one-line summary of a check.
    /// </summary>
    /// <remarks>
    ///     The summary always has the same shape, so scripts and people can find the totals at the end of the
    ///     output: the files checked, the findings and how many files they are in, the files skipped on
    ///     purpose, and the files that could not be checked. Side effects: writes one line to
    ///     <paramref name="context"/>.
    /// </remarks>
    /// <param name="context">The context that receives the output. Must not be null.</param>
    /// <param name="result">The result of the check. Must not be null.</param>
    private static void WriteSummary(Context context, SpellCheckResult result)
    {
        // Count the files the findings are in, which can be fewer than the findings
        var filesWithIssues = result.Issues.Select(static issue => issue.FilePath).Distinct(StringComparer.Ordinal).Count();
        var notChecked = result.FileDiagnostics.Count(static diagnostic => diagnostic.IsError);

        context.WriteLine(
            $"Checked {result.FilesChecked} file(s): {result.Issues.Count} issue(s) in {filesWithIssues} file(s), " +
            $"{result.FilesSkipped} skipped, {notChecked} could not be checked.");
    }
}
