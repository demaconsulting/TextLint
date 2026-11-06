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

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     The outcome of turning command-line path arguments into a search base and portable patterns: the
///     directory to search, the validated patterns relative to it, and how to name the files found.
/// </summary>
/// <remarks>
///     It is the hand-over value between <see cref="CommandLinePathResolver"/> and
///     <see cref="FileSelector"/>: pass <see cref="RootDirectory"/> and <see cref="Patterns"/> to
///     <see cref="FileSelector.Select(string, IReadOnlyList{string}, IReadOnlyList{string}, bool, CancellationToken)"/>
///     (or to the spell checker, as the root directory and as the <c>Files</c> setting). The patterns are
///     copied into an immutable array when the value is created, so no consumer can change them afterwards.
///     Instances are immutable and thread-safe.
/// </remarks>
public sealed class CommandLineSelection
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandLineSelection"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the resolver creates selections. The patterns are copied.
    /// </remarks>
    /// <param name="rootDirectory">The absolute directory the patterns are relative to. Must not be null.</param>
    /// <param name="patterns">The validated patterns, relative to the directory. Must not be null.</param>
    /// <param name="reportsAbsolutePaths">Whether files are to be named by absolute path.</param>
    internal CommandLineSelection(string rootDirectory, IEnumerable<string> patterns, bool reportsAbsolutePaths)
    {
        RootDirectory = rootDirectory;
        Patterns = patterns.ToImmutableArray();
        ReportsAbsolutePaths = reportsAbsolutePaths;
    }

    /// <summary>
    ///     Gets the directory to search: the root that <see cref="Patterns"/> are relative to.
    /// </summary>
    /// <value>An absolute path in the form of the current operating system.</value>
    public string RootDirectory { get; }

    /// <summary>
    ///     Gets the validated patterns, relative to <see cref="RootDirectory"/>, ready for the file selector.
    /// </summary>
    /// <value>An immutable list of portable patterns that use <c>/</c> as the separator; never empty.</value>
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>
    ///     Gets a value indicating whether the files found are to be named by absolute path.
    /// </summary>
    /// <remarks>
    ///     It is <see langword="true"/> exactly when at least one argument was an absolute path, because a
    ///     user who types a full path expects to see a full path in the output.
    /// </remarks>
    /// <value><see langword="true"/> when an absolute path was given; otherwise <see langword="false"/>.</value>
    public bool ReportsAbsolutePaths { get; }

    /// <summary>
    ///     Gets the name under which a selected file is to be reported.
    /// </summary>
    /// <remarks>
    ///     Reports and the files they name must agree, so this is the single place that decides the form:
    ///     the root-relative path unchanged when relative paths are reported, and otherwise the absolute
    ///     operating-system path of the file. Performs no file system access. Stateless and thread-safe.
    /// </remarks>
    /// <param name="relativePath">
    ///     The path relative to <see cref="RootDirectory"/>, using forward slashes, as
    ///     <see cref="SelectedFile.RelativePath"/> gives it. Must not be null.
    /// </param>
    /// <returns>The relative path as given, or the absolute path of the file when <see cref="ReportsAbsolutePaths"/> is set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativePath"/> is null.</exception>
    public string GetDisplayPath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        // Relative reporting leaves the path exactly as the selector produced it
        if (!ReportsAbsolutePaths)
        {
            return relativePath;
        }

        // Absolute reporting joins the path to the search base and lets the platform normalize the separators
        return Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
    }
}
