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
///     A directory view that stops the walk when a cancellation token is cancelled.
/// </summary>
/// <remarks>
///     The matcher walks the directory tree synchronously and has no cancellation support of its own, so
///     the walk can only be interrupted from inside the directory abstraction it reads. This view checks the
///     token before every directory is listed and before every entry of a listing is handed to the matcher,
///     which bounds the work done after a cancellation to one directory entry. Every subdirectory is wrapped
///     in turn so the check applies at every depth. The view holds no mutable state of its own and is as
///     thread-safe as the directory it wraps.
/// </remarks>
internal sealed class CancellableDirectory : DirectoryInfoBase
{
    /// <summary>
    ///     The directory being wrapped.
    /// </summary>
    private readonly DirectoryInfoBase _inner;

    /// <summary>
    ///     The token that stops the walk.
    /// </summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CancellableDirectory"/> class.
    /// </summary>
    /// <remarks>
    ///     Side effects: none; nothing is read until the view is enumerated.
    /// </remarks>
    /// <param name="inner">The directory to wrap. Must not be null.</param>
    /// <param name="cancellationToken">The token that stops the walk when it is cancelled.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    internal CancellableDirectory(DirectoryInfoBase inner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _cancellationToken = cancellationToken;
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
    ///     Checks the token, enumerates the wrapped directory, checks the token again before each entry is
    ///     returned, and wraps every subdirectory so the check continues beneath it.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled before or during the listing.</exception>
    public override IEnumerable<FileSystemInfoBase> EnumerateFileSystemInfos()
    {
        // Stop before reading a directory at all when the walk was cancelled
        _cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in _inner.EnumerateFileSystemInfos())
        {
            // Stop between entries, so a large directory does not delay a cancellation until its end
            _cancellationToken.ThrowIfCancellationRequested();
            yield return entry is DirectoryInfoBase directory ? new CancellableDirectory(directory, _cancellationToken) : entry;
        }
    }

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory and wraps the result so the check still applies beneath it.</remarks>
    public override DirectoryInfoBase? GetDirectory(string path)
    {
        var directory = _inner.GetDirectory(path);
        return directory is null ? null : new CancellableDirectory(directory, _cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>Delegates to the wrapped directory.</remarks>
    public override FileInfoBase? GetFile(string path) => _inner.GetFile(path);
}
