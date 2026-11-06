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

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     The exception thrown when a pattern of the <c>ignoreRegExpList</c> or <c>patterns</c> settings cannot be used.
/// </summary>
/// <remarks>
///     It derives from <see cref="ArgumentException"/> because an unusable pattern is an invalid argument
///     supplied by the caller (settings built in code), and it carries the offending entry and the reason
///     so that a caller can report exactly which entry is wrong. The settings file loader reports the same
///     problems as positioned diagnostics instead of throwing. Instances are immutable and thread-safe.
/// </remarks>
public sealed class InvalidPatternException : ArgumentException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidPatternException"/> class.
    /// </summary>
    /// <remarks>
    ///     The message always names the entry and the reason, so a bare exception text is enough for a user
    ///     to find and fix the entry.
    /// </remarks>
    /// <param name="pattern">The entry as supplied by the caller. Must not be null.</param>
    /// <param name="reason">Why the entry is rejected. Must not be null.</param>
    /// <param name="innerException">The exception of the regular expression engine that caused the rejection, if any.</param>
    internal InvalidPatternException(string pattern, string reason, Exception? innerException = null)
        : base($"Invalid pattern '{pattern}': {reason}", innerException)
    {
        Pattern = pattern;
        Reason = reason;
    }

    /// <summary>
    ///     Gets the entry that was rejected.
    /// </summary>
    /// <value>The entry exactly as the caller supplied it.</value>
    public string Pattern { get; }

    /// <summary>
    ///     Gets the reason the entry was rejected, without the entry text.
    /// </summary>
    /// <remarks>
    ///     Internal so that the settings file loader can put the reason in a message that names the setting
    ///     and the position of the entry.
    /// </remarks>
    /// <value>The reason, for example <c>the flag 'x' is not supported</c>.</value>
    internal string Reason { get; }
}
