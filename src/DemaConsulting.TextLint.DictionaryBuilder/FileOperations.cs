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

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     The file system operations of the builder that can fail in ways a test cannot portably
///     provoke: searching folders, reading files, moving files, and deleting files.
/// </summary>
/// <remarks>
///     A permission error or a locked file is exactly what the builder's all-or-nothing and
///     error-reporting behavior must survive, yet it cannot be created reliably on every operating
///     system and as every kind of user. Routing these few calls through one small class lets the
///     tests substitute a faulting version and so verify that behavior deterministically; production
///     code uses <see cref="Default"/>, which calls the file system directly. Instances hold no state
///     and the default instance is thread-safe.
/// </remarks>
internal class FileOperations
{
    /// <summary>
    ///     Gets the instance that performs the real file system operations.
    /// </summary>
    internal static FileOperations Default { get; } = new();

    /// <summary>
    ///     Enumerates the files of a folder and of all folders beneath it.
    /// </summary>
    /// <remarks>
    ///     The enumeration is lazy, so a permission or I/O error may surface while the result is
    ///     being iterated, not when this method returns. Reads the file system.
    /// </remarks>
    /// <param name="folder">The folder to search. Must not be null.</param>
    /// <returns>The full paths of the files.</returns>
    /// <exception cref="IOException">Thrown when the folder cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the folder or a folder beneath it is denied.</exception>
    internal virtual IEnumerable<string> EnumerateFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);

    /// <summary>
    ///     Enumerates the folders directly beneath a folder.
    /// </summary>
    /// <remarks>
    ///     Lazy in the same way as <see cref="EnumerateFiles"/>. Reads the file system.
    /// </remarks>
    /// <param name="folder">The folder to list. Must not be null.</param>
    /// <returns>The full paths of the sub-folders.</returns>
    /// <exception cref="IOException">Thrown when the folder cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the folder is denied.</exception>
    internal virtual IEnumerable<string> EnumerateDirectories(string folder) =>
        Directory.EnumerateDirectories(folder);

    /// <summary>
    ///     Reads the whole content of a file.
    /// </summary>
    /// <remarks>
    ///     A missing file is reported as <see cref="FileNotFoundException"/> or
    ///     <see cref="DirectoryNotFoundException"/> (both are <see cref="IOException"/>), so a caller
    ///     can tell "not there" from "there but unreadable" without a separate existence check that
    ///     would hide the second case. Reads the file system.
    /// </remarks>
    /// <param name="path">The file to read. Must not be null.</param>
    /// <returns>The bytes of the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a folder on the path does not exist.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    internal virtual byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <summary>
    ///     Moves a file, failing if the destination already exists.
    /// </summary>
    /// <remarks>
    ///     Never overwrites, so the builder always knows what it moved aside. A missing source is
    ///     reported as <see cref="FileNotFoundException"/>. Writes the file system.
    /// </remarks>
    /// <param name="source">The file to move. Must not be null.</param>
    /// <param name="destination">The new path. Must not be null.</param>
    /// <exception cref="IOException">Thrown when the move fails or the destination exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    internal virtual void Move(string source, string destination) => File.Move(source, destination);

    /// <summary>
    ///     Deletes a file; a file that does not exist is not an error.
    /// </summary>
    /// <remarks>
    ///     Writes the file system.
    /// </remarks>
    /// <param name="path">The file to delete. Must not be null.</param>
    /// <exception cref="IOException">Thrown when the file is in use or cannot be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    internal virtual void Delete(string path) => File.Delete(path);
}
