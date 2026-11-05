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

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

/// <summary>
///     File operations that perform the real operation except where a test says it must fail.
/// </summary>
/// <remarks>
///     A denied folder or a locked file cannot be created the same way on every operating system and
///     for every kind of user, so the tests inject the failures instead: each failure is chosen by a
///     predicate over the paths involved. Everything not selected by a predicate is done for real, so
///     the builder's reaction is observed against a real file system. It is test support, not a
///     shipped component.
/// </remarks>
internal sealed class FaultingFileOperations : FileOperations
{
    /// <summary>
    ///     Gets or sets the predicate that selects the moves that fail; it receives the source and the destination.
    /// </summary>
    public Func<string, string, bool> FailMove { get; init; } = (_, _) => false;

    /// <summary>
    ///     Gets or sets the predicate that selects the deletions that fail; it receives the path.
    /// </summary>
    public Func<string, bool> FailDelete { get; init; } = _ => false;

    /// <summary>
    ///     Gets or sets the predicate that selects the folders whose file search fails; it receives the folder.
    /// </summary>
    public Func<string, bool> FailEnumerateFiles { get; init; } = _ => false;

    /// <summary>
    ///     Gets or sets the predicate that selects the folders whose listing of sub-folders fails; it receives the folder.
    /// </summary>
    public Func<string, bool> FailEnumerateDirectories { get; init; } = _ => false;

    /// <inheritdoc/>
    internal override IEnumerable<string> EnumerateFiles(string folder) =>
        FailEnumerateFiles(folder) ? FailPartWay(base.EnumerateFiles(folder)) : base.EnumerateFiles(folder);

    /// <inheritdoc/>
    internal override IEnumerable<string> EnumerateDirectories(string folder) =>
        FailEnumerateDirectories(folder) ? throw new UnauthorizedAccessException("injected access denied") : base.EnumerateDirectories(folder);

    /// <inheritdoc/>
    internal override void Move(string source, string destination)
    {
        if (FailMove(source, destination))
        {
            throw new IOException("injected move failure");
        }

        base.Move(source, destination);
    }

    /// <inheritdoc/>
    internal override void Delete(string path)
    {
        if (FailDelete(path))
        {
            throw new IOException("injected delete failure");
        }

        base.Delete(path);
    }

    /// <summary>
    ///     Yields the first entry of a real enumeration and then fails, like a search that is denied part-way through.
    /// </summary>
    /// <param name="real">The real enumeration to take the first entry from.</param>
    /// <returns>An enumeration that throws <see cref="UnauthorizedAccessException"/> after at most one entry.</returns>
    private static IEnumerable<string> FailPartWay(IEnumerable<string> real)
    {
        var first = real.FirstOrDefault();
        if (first is not null)
        {
            yield return first;
        }

        throw new UnauthorizedAccessException("injected access denied");
    }
}
