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
///     The exception thrown when a glob pattern is unsupported or malformed.
/// </summary>
/// <remarks>
///     It derives from <see cref="ArgumentException"/> because an invalid pattern is an invalid
///     argument supplied by the caller, and it carries the offending pattern so a caller can report
///     exactly which entry of the settings is wrong. Instances are immutable and thread-safe.
/// </remarks>
public sealed class InvalidGlobPatternException : ArgumentException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidGlobPatternException"/> class.
    /// </summary>
    /// <remarks>
    ///     The message always names the pattern and the reason, so a bare exception text is enough
    ///     for a user to find and fix the pattern.
    /// </remarks>
    /// <param name="pattern">The original pattern text as supplied by the caller. Must not be null.</param>
    /// <param name="reason">Why the pattern is rejected. Must not be null.</param>
    internal InvalidGlobPatternException(string pattern, string reason)
        : base($"Invalid glob pattern '{pattern}': {reason}")
    {
        Pattern = pattern;
    }

    /// <summary>
    ///     Gets the original pattern text that was rejected.
    /// </summary>
    /// <value>The pattern exactly as the caller supplied it.</value>
    public string Pattern { get; }
}
