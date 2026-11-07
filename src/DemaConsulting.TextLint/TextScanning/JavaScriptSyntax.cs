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

// cspell:ignore feff

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     The pieces of regular-expression text that give .NET patterns the meaning that the same constructs have in
///     JavaScript, shared by every pattern that is ported from cspell.
/// </summary>
/// <remarks>
///     cspell's patterns run in the JavaScript engine, and the two engines read several constructs differently:
///     <c>\s</c> is a different set of characters, <c>\w</c>, <c>\d</c>, and <c>\b</c> are ASCII-only in JavaScript,
///     <c>.</c> stops at four line terminators, and <c>^</c> in multiline mode starts a line after any of them.
///     Every pattern that is written out by hand in the library (the default patterns, the predefined
///     patterns, the Markdown patterns, the directive patterns, and the random-string pattern) is therefore built
///     from these constants, never from <c>\s</c>, <c>\w</c>, <c>\d</c>, <c>\b</c>, <c>.</c>, <c>^</c>, or
///     <c>$</c>, so that the set of characters has one definition. The patterns that a user writes are
///     translated by <see cref="JavaScriptRegexTranslator"/>, which uses the same sets. Contains constants only
///     and is therefore thread-safe.
/// </remarks>
internal static class JavaScriptSyntax
{
    /// <summary>
    ///     The body of a character class that holds the characters JavaScript counts as white space, which is
    ///     what <c>\s</c> means there: the characters U+0009 to U+000D, U+0020, U+00A0, U+1680, U+2000 to U+200A,
    ///     U+2028, U+2029, U+202F, U+205F, U+3000, and U+FEFF.
    /// </summary>
    /// <remarks>
    ///     .NET's <c>\s</c> differs: it adds U+0085 and leaves out U+FEFF. Every code unit is written as an
    ///     escape so that no character can be read as class syntax.
    /// </remarks>
    internal const string SpaceBody =
        "\\u0009-\\u000D\\u0020\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000\\uFEFF";

    /// <summary>
    ///     The pattern text of one character of JavaScript white space, which is what <c>\s</c> means there.
    /// </summary>
    internal const string Space = "[" + SpaceBody + "]";

    /// <summary>
    ///     The pattern text of one character that is not JavaScript white space, which is what <c>\S</c> means there.
    /// </summary>
    internal const string NotSpace = "[^" + SpaceBody + "]";

    /// <summary>
    ///     The pattern text of an ASCII word boundary: the place where an ASCII word character meets a non-word
    ///     character or an edge of the text, which is what <c>\b</c> means in JavaScript without the Unicode flag.
    /// </summary>
    internal const string WordBoundary = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";

    /// <summary>
    ///     The pattern text of the start of a line: the start of the text, or the place after a line terminator
    ///     (<c>\n</c>, <c>\r</c>, U+2028, or U+2029), which is what <c>^</c> means in JavaScript multiline mode.
    /// </summary>
    internal const string LineStart = "(?<![^\\r\\n\\u2028\\u2029])";

    /// <summary>
    ///     The pattern text of one character that is not a JavaScript line terminator, which is what <c>.</c> means there.
    /// </summary>
    internal const string NotLineTerminator = "[^\\r\\n\\u2028\\u2029]";

    /// <summary>
    ///     Determines whether a character is JavaScript white space, the same set as <see cref="Space"/>, which is
    ///     also what the JavaScript <c>trim</c> function removes.
    /// </summary>
    /// <remarks>
    ///     <see cref="char.IsWhiteSpace(char)"/> differs: it adds U+0085 and leaves out U+FEFF. Used where the
    ///     library reads text the way cspell's code does. Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> when the character is in the JavaScript white space set.</returns>
    internal static bool IsSpace(char character) => character is
        (>= '\u0009' and <= '\u000D') or '\u0020' or '\u00A0' or '\u1680' or (>= '\u2000' and <= '\u200A') or
        '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000' or '\uFEFF';

    /// <summary>
    ///     Removes the JavaScript white space from both ends of a text, as the JavaScript <c>trim</c> function does.
    /// </summary>
    /// <remarks>
    ///     <see cref="string.Trim()"/> differs: it also removes U+0085 and keeps U+FEFF. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <returns>The text without leading and trailing JavaScript white space.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    internal static string Trim(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }
}
