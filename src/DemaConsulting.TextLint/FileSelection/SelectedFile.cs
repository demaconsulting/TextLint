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

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     A file chosen by <see cref="FileSelector"/>.
/// </summary>
/// <remarks>
///     The relative path is the stable identity of a file: it always uses forward slashes, so
///     reports and caches built from it are identical on every operating system. The full path is
///     the operating-system path to open the file with. Instances are immutable and thread-safe.
/// </remarks>
public sealed class SelectedFile
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectedFile"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the selector creates selected files.
    /// </remarks>
    /// <param name="relativePath">The root-relative path using forward slashes. Must not be null.</param>
    /// <param name="fullPath">The absolute operating-system path. Must not be null.</param>
    internal SelectedFile(string relativePath, string fullPath)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
    }

    /// <summary>
    ///     Gets the path of the file relative to the selection root, using forward slashes on every platform.
    /// </summary>
    /// <value>The relative path, for example <c>src/Program.cs</c>.</value>
    public string RelativePath { get; }

    /// <summary>
    ///     Gets the absolute operating-system path of the file.
    /// </summary>
    /// <value>The full path using the platform's directory separator.</value>
    public string FullPath { get; }
}
