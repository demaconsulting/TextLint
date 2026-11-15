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
///     A directory view that hides subdirectories whose every file an ordered pattern list excludes.
/// </summary>
/// <remarks>
///     The matcher can only exclude a directory natively through a plain exclude pattern, which cannot
///     express "excluded unless a later pattern includes it again". Wrapping the directory the matcher
///     walks lets the ordered rules of <see cref="OrderedGlobList"/> prune such a directory instead: the
///     matcher only descends into what the view enumerates, so a directory that is hidden here is never
///     read from disk. The decision for each subdirectory is delegated to
///     <see cref="OrderedGlobList.IsSubtreeExcluded"/>, which only ever answers <see langword="true"/>
///     when that is certainly safe; files, and every directory it is not certain about, pass through
///     unchanged and are decided exactly after the match. Every subdirectory is wrapped in turn so the
///     rule applies at every depth. The view holds no mutable state of its own and is as thread-safe as
///     the directory it wraps.
/// </remarks>
internal sealed class OrderedPruningDirectory : DirectoryInfoBase
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
    ///     The ordered pattern list that decides which subdirectories are pruned.
    /// </summary>
    private readonly OrderedGlobList _patterns;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrderedPruningDirectory"/> class for the selection root.
    /// </summary>
    /// <remarks>
    ///     Side effects: none; nothing is read until the view is enumerated.
    /// </remarks>
    /// <param name="root">The selection root directory to wrap. Must not be null.</param>
    /// <param name="patterns">The ordered pattern list. Must not be null.</param>
    internal OrderedPruningDirectory(DirectoryInfoBase root, OrderedGlobList patterns)
        : this(root, string.Empty, patterns)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrderedPruningDirectory"/> class for a subdirectory.
    /// </summary>
    /// <remarks>
    ///     Side effects: none; nothing is read until the view is enumerated.
    /// </remarks>
    /// <param name="inner">The directory to wrap. Must not be null.</param>
    /// <param name="relativePath">The directory's path relative to the root. Must not be null.</param>
    /// <param name="patterns">The ordered pattern list. Must not be null.</param>
    private OrderedPruningDirectory(DirectoryInfoBase inner, string relativePath, OrderedGlobList patterns)
    {
        _inner = inner;
        _relativePath = relativePath;
        _patterns = patterns;
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
    ///     Enumerates the wrapped directory, drops each subdirectory whose whole subtree is excluded, and
    ///     wraps the remaining subdirectories so the rule continues beneath them. The wrapped directory is
    ///     read once per call. Files are kept here and decided after the match.
    /// </remarks>
    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        foreach (var entry in _inner.EnumerateFileSystemInfos())
        {
            // Files pass through; the exact decision is made after matching
            if (entry is not DirectoryInfoBase directory)
            {
                yield return entry;
                continue;
            }

            // Skip a directory that no pattern can bring a file back from
            var relative = Combine(directory.Name);
            if (_patterns.IsSubtreeExcluded(relative))
            {
                continue;
            }

            yield return new OrderedPruningDirectory(directory, relative, _patterns);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Delegates to the wrapped directory and wraps the result so the rule still applies beneath it. A
    ///     directory that is pruned is reported as missing, exactly as if it had been left out of the listing.
    /// </remarks>
    public override DirectoryInfoBase? GetDirectory(string path)
    {
        var relative = Combine(path);
        if (_patterns.IsSubtreeExcluded(relative))
        {
            return null;
        }

        var directory = _inner.GetDirectory(path);
        return directory is null ? null : new OrderedPruningDirectory(directory, relative, _patterns);
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
