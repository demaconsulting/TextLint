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

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     One configuration error found while loading a settings file.
/// </summary>
/// <remarks>
///     Configuration problems are returned as data rather than thrown so that a caller (such as the
///     command-line tool) can print every problem in one uniform format. Every diagnostic describes
///     an error: a settings file with any diagnostic produces no settings. Instances are immutable
///     and thread-safe.
/// </remarks>
public sealed class SettingsDiagnostic
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsDiagnostic"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the loader produces diagnostics; consumers only read them.
    /// </remarks>
    /// <param name="message">Description of the problem. Must not be null.</param>
    /// <param name="filePath">The settings file path or source name the problem was found in, or null.</param>
    /// <param name="line">The 1-based line of the problem, or null when it has no position.</param>
    /// <param name="column">The 1-based column of the problem, or null when it has no position.</param>
    internal SettingsDiagnostic(string message, string? filePath, int? line, int? column)
    {
        Message = message;
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    /// <summary>
    ///     Gets the description of the problem.
    /// </summary>
    /// <value>A human-readable message that names the offending key, value, or pattern where relevant.</value>
    public string Message { get; }

    /// <summary>
    ///     Gets the path or source name of the settings file the problem was found in.
    /// </summary>
    /// <value>The path supplied to the loader, or <see langword="null"/> when no name was supplied.</value>
    public string? FilePath { get; }

    /// <summary>
    ///     Gets the 1-based line of the problem within the settings file.
    /// </summary>
    /// <value>The line number, or <see langword="null"/> when the problem has no position (for example an unreadable file).</value>
    public int? Line { get; }

    /// <summary>
    ///     Gets the 1-based column of the problem within the settings file.
    /// </summary>
    /// <value>The column number, or <see langword="null"/> when the problem has no position.</value>
    public int? Column { get; }

    /// <summary>
    ///     Formats the diagnostic in the compiler style <c>file(line,column): error: message</c>.
    /// </summary>
    /// <remarks>
    ///     The compiler style is understood by editors, build servers, and CI log parsers, so
    ///     configuration errors become clickable without any tool-specific integration. Parts that
    ///     are unknown are omitted: without a position the location is just the file, without a
    ///     file it is just the position, and with neither it is omitted entirely.
    /// </remarks>
    /// <returns>The formatted diagnostic text.</returns>
    public override string ToString()
    {
        // Build the location prefix from whichever parts are known
        var location = FilePath ?? string.Empty;
        if (Line is not null && Column is not null)
        {
            location += $"({Line},{Column})";
        }

        // Without any location the message stands alone after the severity word
        return location.Length == 0 ? $"error: {Message}" : $"{location}: error: {Message}";
    }
}
