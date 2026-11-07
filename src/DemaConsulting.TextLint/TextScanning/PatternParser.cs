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

// cspell:ignore dgimsuvxy gimsu

using System.Text.RegularExpressions;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Turns a pattern written the way cspell writes it, <c>/body/flags</c> or a bare expression, into a
///     .NET regular expression that is safe to run on any text.
/// </summary>
/// <remarks>
///     A project migrated from cspell brings its <c>ignoreRegExpList</c> entries unchanged, so the entry
///     forms are cspell's: a JavaScript literal written between slashes with optional flags, or a bare
///     expression that cspell reads with the flags <c>g</c>, <c>i</c>, and <c>m</c>. The body is translated
///     to .NET by <see cref="JavaScriptRegexTranslator"/>, which keeps the JavaScript meaning of the
///     constructs it supports and rejects the rest. Only the flags that have a safe equivalent are
///     accepted: <c>g</c> (always in force, because every match is wanted), <c>i</c>, <c>m</c>, <c>s</c>
///     (the dot also matches line terminators), and <c>u</c>. The flags <c>x</c>, <c>y</c>, <c>d</c>, and
///     <c>v</c> are rejected by name, because
///     cspell drops some of them silently and a pattern that does not mean what its author wrote must not
///     load. Every regular expression is built with a match time limit, so a pattern that backtracks
///     catastrophically on some file ends with a <see cref="RegexMatchTimeoutException"/> instead of hanging
///     a run; cspell, which has no limit, would hang. The limit is 5 seconds in production; an internal
///     overload lets the tests use a short one. Failure is reported with a reason as an
///     <see cref="InvalidPatternException"/>, which the settings loader turns into a positioned
///     diagnostic. The class is stateless and thread-safe; the regular expressions it creates are immutable
///     and safe for concurrent matching.
/// </remarks>
internal static class PatternParser
{
    /// <summary>
    ///     The flags that cspell gives a bare expression, which has no slashes to carry its own.
    /// </summary>
    private const string BareFlags = "im";

    /// <summary>
    ///     The letters that are flags of a JavaScript regular expression literal, whether or not they are supported.
    /// </summary>
    /// <remarks>
    ///     Text after the last slash is read as flags only when it is made of these letters, so that a bare
    ///     expression that happens to start with a slash, such as a path, is not mistaken for a literal.
    ///     <c>x</c> is included because cspell accepts it as a flag.
    /// </remarks>
    private const string FlagLetters = "dgimsuvxy";

    /// <summary>
    ///     The flags that are accepted.
    /// </summary>
    private const string SupportedFlags = "gimsu";

    /// <summary>
    ///     The character that opens and closes a regular expression literal.
    /// </summary>
    private const char Slash = '/';

    /// <summary>
    ///     The longest time, in milliseconds, that a pattern may spend on one text before matching is abandoned.
    /// </summary>
    /// <remarks>
    ///     The same limit as the default patterns, so every pattern the library runs is bounded alike.
    /// </remarks>
    internal const int MatchTimeoutMilliseconds = DefaultPatterns.MatchTimeoutMilliseconds;

    /// <summary>
    ///     Parses a pattern entry into a regular expression.
    /// </summary>
    /// <remarks>
    ///     A literal is recognized by a leading slash and a last slash that is followed only by flag letters;
    ///     the body may contain unescaped slashes. Anything else is a bare expression with the flags
    ///     <c>g</c>, <c>i</c>, and <c>m</c>. Surrounding white space is ignored. The returned regular
    ///     expression is built with the invariant culture and the match time limit, and the case option of
    ///     the flags; the multiline behavior is written into the translated pattern. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="entry">The pattern entry. Must not be null.</param>
    /// <returns>The regular expression; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="InvalidPatternException">
    ///     Thrown when the entry is blank, has an unsupported flag, or has a body that cannot be translated or
    ///     is not a valid regular expression. The exception names the entry and the reason.
    /// </exception>
    internal static Regex Parse(string entry) => Parse(entry, TimeSpan.FromMilliseconds(MatchTimeoutMilliseconds));

    /// <summary>
    ///     Parses a pattern entry into a regular expression with a given match time limit.
    /// </summary>
    /// <remarks>
    ///     The same as <see cref="Parse(string)"/>, except for the limit. Production code uses the default
    ///     limit; the overload exists so that tests can prove the time-out behavior in milliseconds instead of
    ///     seconds. Stateless and thread-safe.
    /// </remarks>
    /// <param name="entry">The pattern entry. Must not be null.</param>
    /// <param name="matchTimeout">The longest time one match operation may take. Must be positive; the infinite time span is not allowed.</param>
    /// <returns>The regular expression; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="matchTimeout"/> is not positive or is infinite.</exception>
    /// <exception cref="InvalidPatternException">
    ///     Thrown when the entry is blank, has an unsupported flag, or has a body that cannot be translated or
    ///     is not a valid regular expression. The exception names the entry and the reason.
    /// </exception>
    internal static Regex Parse(string entry, TimeSpan matchTimeout)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matchTimeout, TimeSpan.Zero);

        // Separate the body from the flags, with the forms cspell recognizes
        var text = JavaScriptSyntax.Trim(entry);
        var body = text;
        var flags = BareFlags;
        if (text.Length > 0 && text[0] == Slash)
        {
            var last = text.LastIndexOf(Slash);
            var flagText = text[(last + 1)..];
            if (last > 0 && flagText.All(static letter => FlagLetters.Contains(letter, StringComparison.Ordinal)))
            {
                body = text[1..last];
                flags = flagText;
            }
        }

        if (text.Length == 0)
        {
            throw new InvalidPatternException(entry, "the pattern is blank.");
        }

        // Reject the flags that have no safe equivalent, naming the first one
        var unsupported = flags.Where(static flag => !SupportedFlags.Contains(flag, StringComparison.Ordinal)).ToArray();
        if (unsupported.Length > 0)
        {
            throw new InvalidPatternException(entry, $"the flag '{unsupported[0]}' is not supported; use only g, i, m, s, and u.");
        }

        // Translate the body, then build the regular expression with the time limit
        var ignoreCase = flags.Contains('i', StringComparison.Ordinal);
        var multiline = flags.Contains('m', StringComparison.Ordinal);
        var unicode = flags.Contains('u', StringComparison.Ordinal);
        var dotAll = flags.Contains('s', StringComparison.Ordinal);
        if (!JavaScriptRegexTranslator.TryTranslate(body, multiline, unicode, dotAll, ignoreCase, out var translated, out var problem))
        {
            throw new InvalidPatternException(entry, problem!);
        }

        // The case flag is written into the translation, because the .NET option follows other rules than JavaScript
        var options = RegexOptions.CultureInvariant;
        try
        {
            return new Regex(translated, options, matchTimeout);
        }
        catch (ArgumentException exception)
        {
            // The translation is built to be valid, so this is a construct that only the engine itself can reject
            throw new InvalidPatternException(entry, $"it is not a valid regular expression: {exception.Message}", exception);
        }
    }
}
