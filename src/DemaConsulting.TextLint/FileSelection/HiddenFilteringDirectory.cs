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

using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     A directory view that hides hidden subdirectories the include patterns cannot reach.
/// </summary>
/// <remarks>
///     Wrapping the directory the matcher walks is what lets hidden directories such as <c>.git</c>
///     be skipped without ever being enumerated: the matcher only descends into what the view
///     enumerates, so a hidden directory that is hidden here is never read from disk. The decision
///     for each hidden subdirectory is delegated to <see cref="HiddenPathPolicy"/>; non-hidden
///     entries and all files pass through unchanged (hidden files are decided exactly after the
///     match). Every subdirectory is wrapped in turn so the filter applies at every depth. The
///     view holds no mutable state of its own and is as thread-safe as the directory it wraps.
/// </remarks>
internal sealed class HiddenFilteringDirectory : DirectoryInfoBase
{
    /// <summary>
    ///     The separator used in relative paths on every platform.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The directory being wrapped.
    /// </summary>
    private readonly DirectoryInfoBase _inner;

    /// <summary>
    ///     The path of this directory relative to the selection root, using forward slashes; empty for the root.
    /// </summary>
    private readonly string _relativePath;

    /// <summary>
    ///     The expanded include patterns that decide which hidden directories may be entered.
    /// </summary>
    private readonly IReadOnlyList<string> _includePatterns;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HiddenFilteringDirectory"/> class for the selection root.
    /// </summary>
    /// <remarks>
    ///     Side effects: none; nothing is read until the view is enumerated.
    /// </remarks>
    /// <param name="root">The selection root directory to wrap. Must not be null.</param>
    /// <param name="includePatterns">The expanded include patterns. Must not be null.</param>
    internal HiddenFilteringDirectory(DirectoryInfoBase root, IReadOnlyList<string> includePatterns)
        : this(root, string.Empty, includePatterns)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="HiddenFilteringDirectory"/> class for a subdirectory.
    /// </summary>
    /// <remarks>
    ///     Side effects: none; nothing is read until the view is enumerated.
    /// </remarks>
    /// <param name="inner">The directory to wrap. Must not be null.</param>
    /// <param name="relativePath">The directory's path relative to the root. Must not be null.</param>
    /// <param name="includePatterns">The expanded include patterns. Must not be null.</param>
    private HiddenFilteringDirectory(DirectoryInfoBase inner, string relativePath, IReadOnlyList<string> includePatterns)
    {
        _inner = inner;
        _relativePath = relativePath;
        _includePatterns = includePatterns;
    }

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory.</remarks>
    public override string Name => _inner.Name;

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory.</remarks>
    public override string FullName => _inner.FullName;

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory; the parent is not wrapped because the walk never ascends.</remarks>
    public override DirectoryInfoBase? ParentDirectory => _inner.ParentDirectory;

    /// <inheritdoc />
    /// <remarks>
    ///     Enumerates the wrapped directory, drops hidden subdirectories that no include pattern can
    ///     reach, and wraps the remaining subdirectories so the filter continues beneath them. The
    ///     wrapped directory is read once per call. Hidden files are kept here and decided after the match.
    /// </remarks>
    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        foreach (var entry in _inner.EnumerateFileSystemInfos())
        {
            // Files pass through; the exact hidden-file decision is made after matching
            if (entry is not DirectoryInfoBase directory)
            {
                yield return entry;
                continue;
            }

            // Skip a hidden directory when no include pattern could reach beneath it
            var relative = Combine(directory.Name);
            if (HiddenPathPolicy.IsHidden(directory.Name) &&
                !HiddenPathPolicy.CanContainPermittedPaths(relative, _includePatterns))
            {
                continue;
            }

            yield return new HiddenFilteringDirectory(directory, relative, _includePatterns);
        }
    }

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory and wraps the result so the filter still applies beneath it.</remarks>
    public override DirectoryInfoBase? GetDirectory(string path)
    {
        var directory = _inner.GetDirectory(path);
        return directory is null ? null : new HiddenFilteringDirectory(directory, Combine(path), _includePatterns);
    }

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory.</remarks>
    public override FileInfoBase? GetFile(string path) => _inner.GetFile(path);

    /// <summary>
    ///     Builds the root-relative path of a child of this directory.
    /// </summary>
    /// <remarks>
    ///     Stateless apart from reading this directory's relative path; thread-safe.
    /// </remarks>
    /// <param name="childName">The child's name. Must not be null.</param>
    /// <returns>The child's path relative to the selection root, using forward slashes.</returns>
    private string Combine(string childName) =>
        _relativePath.Length == 0 ? childName : _relativePath + Separator + childName;
}
