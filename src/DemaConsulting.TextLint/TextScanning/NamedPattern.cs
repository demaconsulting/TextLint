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
///     A pattern that the <c>patterns</c> setting gives a name, so that <c>ignoreRegExpList</c> can refer to it.
/// </summary>
/// <remarks>
///     The pair is kept exactly as the settings give it; the name is checked and the pattern is parsed when a
///     spell checker is created or a settings file is loaded, not here, so a value can be built and carried
///     before the rules are known. Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed record NamedPattern
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NamedPattern"/> class.
    /// </summary>
    /// <remarks>
    ///     Only null is rejected here, because the rules for a valid name and a valid pattern belong to the
    ///     spell checker and are reported with the setting they came from.
    /// </remarks>
    /// <param name="name">The name that <c>ignoreRegExpList</c> uses to refer to the pattern. Must not be null.</param>
    /// <param name="pattern">
    ///     The pattern, in the form cspell uses: <c>/body/flags</c> or a bare expression. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="pattern"/> is null.</exception>
    public NamedPattern(string name, string pattern)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(pattern);
        Name = name;
        Pattern = pattern;
    }

    /// <summary>
    ///     Gets the name that <c>ignoreRegExpList</c> uses to refer to the pattern.
    /// </summary>
    /// <value>The name as written; names are compared ignoring letter case.</value>
    public string Name { get; }

    /// <summary>
    ///     Gets the pattern.
    /// </summary>
    /// <value>The pattern as written: <c>/body/flags</c> or a bare expression.</value>
    public string Pattern { get; }
}
