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

namespace DemaConsulting.TextLint.Tool.Tests.TestSupport;

/// <summary>
///     A uniquely named temporary directory that is deleted when disposed.
/// </summary>
/// <remarks>
///     Every test that needs files creates its own directory, so tests share no state and cannot disturb
///     each other or the current directory of the test process. Disposing removes the directory and
///     everything beneath it.
/// </remarks>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TempDirectory"/> class and creates the directory.
    /// </summary>
    public TempDirectory()
    {
        // A GUID in the name keeps parallel tests from colliding
        FullPath = Path.Combine(Path.GetTempPath(), "TextLintToolTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    /// <summary>
    ///     Gets the absolute path of the temporary directory.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    ///     Creates a text file beneath the directory, creating any missing parent directories.
    /// </summary>
    /// <param name="relativePath">The path relative to the directory, using forward slashes.</param>
    /// <param name="content">The text to write to the file.</param>
    /// <returns>The absolute path of the created file.</returns>
    public string CreateFile(string relativePath, string content = "x")
    {
        // Resolve the platform path, make the parent folders, then write the file
        var path = Path.Combine(FullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Creates a binary file beneath the directory, creating any missing parent directories.
    /// </summary>
    /// <param name="relativePath">The path relative to the directory, using forward slashes.</param>
    /// <param name="content">The bytes to write to the file.</param>
    /// <returns>The absolute path of the created file.</returns>
    public string CreateBinaryFile(string relativePath, byte[] content)
    {
        // Resolve the platform path, make the parent folders, then write the file
        var path = Path.Combine(FullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>
    ///     Deletes the directory and everything beneath it.
    /// </summary>
    public void Dispose()
    {
        Directory.Delete(FullPath, recursive: true);
    }
}
