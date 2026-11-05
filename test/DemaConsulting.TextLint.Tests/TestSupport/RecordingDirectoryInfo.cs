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

namespace DemaConsulting.TextLint.Tests.TestSupport;

/// <summary>
///     A directory view that records which directories the matcher enumerates.
/// </summary>
/// <remarks>
///     This is the only test double in the library tests. It proves that excluded directories are
///     never read from disk by observing the enumeration calls directly, which is deterministic;
///     a timing-based test would be flaky. Every subdirectory is wrapped so enumeration is recorded
///     at every depth, and all wrappers of one tree share a single list.
/// </remarks>
internal sealed class RecordingDirectoryInfo : DirectoryInfoBase
{
    /// <summary>
    ///     The separator used in recorded relative paths.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The wrapped directory.
    /// </summary>
    private readonly DirectoryInfoBase _inner;

    /// <summary>
    ///     This directory's path relative to the root; empty for the root.
    /// </summary>
    private readonly string _relativePath;

    /// <summary>
    ///     The list shared by every wrapper of the tree.
    /// </summary>
    private readonly List<string> _enumerated;

    /// <summary>
    ///     The callback, shared by every wrapper of the tree, that is told the relative path of each directory
    ///     as its enumeration starts; null when there is none.
    /// </summary>
    private readonly Action<string>? _onEnumerate;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordingDirectoryInfo"/> class for a root directory.
    /// </summary>
    /// <param name="root">The directory to wrap.</param>
    /// <param name="onEnumerate">
    ///     Called with the relative path of each directory as its enumeration starts (after it is recorded),
    ///     so a test can act at a precise point of the walk; null for none.
    /// </param>
    public RecordingDirectoryInfo(DirectoryInfoBase root, Action<string>? onEnumerate = null)
        : this(root, string.Empty, [], onEnumerate)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecordingDirectoryInfo"/> class for a subdirectory.
    /// </summary>
    /// <param name="inner">The directory to wrap.</param>
    /// <param name="relativePath">The directory's path relative to the root.</param>
    /// <param name="enumerated">The list shared by the whole tree.</param>
    /// <param name="onEnumerate">The callback shared by the whole tree, or null.</param>
    private RecordingDirectoryInfo(
        DirectoryInfoBase inner,
        string relativePath,
        List<string> enumerated,
        Action<string>? onEnumerate)
    {
        _inner = inner;
        _relativePath = relativePath;
        _enumerated = enumerated;
        _onEnumerate = onEnumerate;
    }

    /// <summary>
    ///     Gets the root-relative paths of the directories enumerated so far; the root is the empty string.
    /// </summary>
    public IReadOnlyList<string> EnumeratedDirectories => _enumerated;

    /// <inheritdoc />
    public override string Name => _inner.Name;

    /// <inheritdoc />
    public override string FullName => _inner.FullName;

    /// <inheritdoc />
    public override DirectoryInfoBase? ParentDirectory => _inner.ParentDirectory;

    /// <inheritdoc />
    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        // Record this directory, then wrap each subdirectory so its enumeration is recorded too
        _enumerated.Add(_relativePath);
        _onEnumerate?.Invoke(_relativePath);
        foreach (var entry in _inner.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfoBase directory)
            {
                var relative = _relativePath.Length == 0 ? directory.Name : _relativePath + Separator + directory.Name;
                yield return new RecordingDirectoryInfo(directory, relative, _enumerated, _onEnumerate);
            }
            else
            {
                yield return entry;
            }
        }
    }

    /// <inheritdoc />
    public override DirectoryInfoBase? GetDirectory(string path) => _inner.GetDirectory(path);

    /// <inheritdoc />
    public override FileInfoBase? GetFile(string path) => _inner.GetFile(path);
}
