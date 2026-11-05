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

// cspell:ignore subresource

using System.Text.RegularExpressions;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Finds the text that is never spell-checked: URLs, email addresses, hexadecimal values, UUIDs,
///     hashes, and encoded data.
/// </summary>
/// <remarks>
///     These strings are never prose, so reporting their fragments as unknown words would bury real
///     findings. The set of patterns is the default ignore list of cspell (its <c>ignoreRegExpList</c>
///     defaults), ported so that a project migrated from cspell stays quiet where it was quiet. The port
///     is by hand, because the two engines differ: in JavaScript (without the <c>u</c> flag) <c>\w</c>, <c>\d</c>,
///     and <c>\b</c> are ASCII-only while in .NET they are Unicode-aware, so every pattern spells out ASCII
///     classes and an ASCII word boundary; <c>^</c> in multiline mode starts a line after any of <c>\n</c>,
///     <c>\r</c>, U+2028, and U+2029; and <c>.</c> excludes all four of those line terminators. The patterns
///     that cspell applies case-insensitively are case-insensitive here, with the invariant culture, and
///     the encoded-data patterns stay case-sensitive as in cspell. Every pattern has a match timeout, so
///     hostile input surfaces as a <see cref="RegexMatchTimeoutException"/> instead of hanging a run. The
///     patterns are compiled at build time by the regular-expression source generator, which keeps the
///     library trim and Native AOT safe. The class is stateless and thread-safe; the generated regular
///     expressions are immutable and safe for concurrent matching.
/// </remarks>
internal static partial class DefaultPatterns
{
    /// <summary>
    ///     The longest time, in milliseconds, that one pattern may spend on one text before matching is abandoned.
    /// </summary>
    /// <remarks>
    ///     Five seconds is far above the cost of any legitimate file and far below the time a build would be
    ///     held up by catastrophic backtracking.
    /// </remarks>
    internal const int MatchTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The pattern text of an ASCII word boundary: the place where an ASCII word character meets a non-word
    ///     character or an edge of the text, which is what <c>\b</c> means in JavaScript.
    /// </summary>
    private const string WordBoundary = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";

    /// <summary>
    ///     The pattern text of the start of a line: the start of the text, or the place after a line terminator
    ///     (<c>\n</c>, <c>\r</c>, U+2028, or U+2029), which is what <c>^</c> means in JavaScript multiline mode.
    /// </summary>
    private const string LineStart = "(?<![^\\r\\n\\u2028\\u2029])";

    /// <summary>
    ///     The options shared by the patterns that cspell compiles with the case-insensitive flag.
    /// </summary>
    private const RegexOptions Insensitive = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    ///     The options shared by the patterns that cspell compiles without the case-insensitive flag.
    /// </summary>
    private const RegexOptions Sensitive = RegexOptions.CultureInvariant;

    /// <summary>
    ///     Every default pattern, in the order cspell lists them.
    /// </summary>
    private static readonly Regex[] Patterns =
    [
        Urls(),
        Email(),
        RsaCert(),
        SshRsa(),
        Base64MultiLine(),
        Base64SingleLine(),
        CommitHash(),
        CommitHashLink(),
        CStyleHexValue(),
        CssHexValue(),
        Sha(),
        HashStrings(),
        UnicodeRef(),
        Uuid(),
    ];

    /// <summary>
    ///     Gets every default pattern.
    /// </summary>
    /// <remarks>
    ///     Exposed so the timeout that bounds every pattern can be verified in one place. The list is fixed
    ///     for the life of the process.
    /// </remarks>
    /// <value>The generated regular expressions, in the order cspell lists them.</value>
    internal static IReadOnlyList<Regex> All => Patterns;

    /// <summary>
    ///     Appends the ranges of a text that match any default pattern to a list.
    /// </summary>
    /// <remarks>
    ///     The ranges are reported pattern by pattern, so they are neither sorted nor free of overlaps; the
    ///     caller merges them. The list is appended to, never cleared. Each pattern is bounded by
    ///     <see cref="MatchTimeoutMilliseconds"/>. Thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The text to search. Must not be null.</param>
    /// <param name="ranges">The list that receives the matching ranges. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="ranges"/> is null.</exception>
    /// <exception cref="RegexMatchTimeoutException">
    ///     Thrown when a pattern exceeds <see cref="MatchTimeoutMilliseconds"/> on this text. The ranges found
    ///     so far remain in the list, but the caller should treat the text as not checked.
    /// </exception>
    internal static void FindExcludedRanges(string text, List<TextRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ranges);

        // Run every pattern over the whole text; a pattern cannot match the empty string, so every match has a length
        foreach (var pattern in Patterns)
        {
            foreach (var match in pattern.EnumerateMatches(text))
            {
                ranges.Add(new TextRange(match.Index, match.Length));
            }
        }
    }

    /// <summary>
    ///     Matches a web or file-transfer URL: a scheme, <c>://</c>, and everything up to white space or a double quote.
    /// </summary>
    /// <remarks>
    ///     The host and path of a URL are not prose. A bare <c>www.</c> host without a scheme is not matched.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?:https?|ftp)://[^\\s\"]+", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Urls();

    /// <summary>
    ///     Matches an email address, optionally wrapped in angle brackets.
    /// </summary>
    /// <remarks>
    ///     The local part is at most 128 characters and each domain label at most 63, with one to four
    ///     labels, which bounds the work a hostile text can cause.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("<?" + WordBoundary + "[A-Za-z0-9_.\\-+]{1,128}@[A-Za-z0-9_]{1,63}(?:\\.[A-Za-z0-9_]{1,63}){1,4}" + WordBoundary + ">?",
        Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Email();

    /// <summary>
    ///     Matches a PEM block: a certificate, or a private or public key, from its BEGIN line to its END line.
    /// </summary>
    /// <remarks>
    ///     Case-sensitive, as in cspell. The END line must name the same kind of block as the BEGIN line.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("-{5}BEGIN\\s+(CERTIFICATE|(?:RSA\\s+)?(?:PRIVATE|PUBLIC)\\s+KEY)[A-Za-z0-9_=+\\-/\\\\\\s]+?END\\s+\\1-{5}",
        Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex RsaCert();

    /// <summary>
    ///     Matches an SSH public key line: <c>ssh-rsa</c> and its Base64 key.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("ssh-rsa\\s+[a-z0-9/+]{28,}={0,3}(?![a-z0-9/+=])", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex SshRsa();

    /// <summary>
    ///     Matches Base64 data that continues over several lines, each of at least 40 characters, optionally
    ///     quoted.
    /// </summary>
    /// <remarks>
    ///     Case-sensitive, as in cspell. The line-start tests use the JavaScript meaning of a line start.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9/+])[\"']?(?:[A-Za-z0-9/+]{40,})[\"']?" +
        "(?:\\s" + LineStart + "\\s*[\"']?[A-Za-z0-9/+]{40,}[\"']?)+" +
        "(?:\\s" + LineStart + "\\s*[\"']?[A-Za-z0-9/+]+={0,3}[\"']?)?(?![A-Za-z0-9/+=])",
        Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Base64MultiLine();

    /// <summary>
    ///     Matches one line of Base64 data of at least 40 characters that looks encoded rather than like words.
    /// </summary>
    /// <remarks>
    ///     To be taken as Base64 the run must contain a digit, a plus, or a slash within its first 80
    ///     characters, a capital-lower-capital sequence, a digit between letters or a letter between digits,
    ///     and three lower-case or three upper-case letters in a row. Case-sensitive, as in cspell.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9/+_])" +
        "(?=[A-Za-z]{0,80}[0-9+/])" +
        "(?=[A-Za-z0-9/+]{0,80}?[A-Z][a-z][A-Z])" +
        "(?=[A-Za-z0-9/+]{0,80}?(?:[A-Z][0-9][A-Z]|[a-z][0-9][a-z]|[A-Z][0-9][a-z]|[a-z][0-9][A-Z]|[0-9][A-Za-z][0-9]))" +
        "(?=[A-Za-z0-9/+]{0,80}?(?:[a-z]{3}|[A-Z]{3}))" +
        "(?:[A-Za-z0-9/+]{40,})=*",
        Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Base64SingleLine();

    /// <summary>
    ///     Matches a commit hash: seven or more hexadecimal digits, optionally after <c>0x</c>, that are not all letters.
    /// </summary>
    /// <remarks>
    ///     The requirement for at least one digit keeps ordinary words made of the letters a to f, such as
    ///     <c>defaced</c>, from being taken as hashes.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(WordBoundary + "(?![a-f]+" + WordBoundary + ")(?:0x)?[0-9a-f]{7,}" + WordBoundary, Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex CommitHash();

    /// <summary>
    ///     Matches a Markdown commit-hash link: seven or more hexadecimal digits in square brackets.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("\\[[0-9a-f]{7,}\\]", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex CommitHashLink();

    /// <summary>
    ///     Matches a C-style hexadecimal literal: <c>0x</c>, hexadecimal digits and underscores, and an optional <c>n</c>.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(WordBoundary + "0x[0-9a-f_]+n?" + WordBoundary, Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex CStyleHexValue();

    /// <summary>
    ///     Matches a CSS hexadecimal color: a number sign and three to eight hexadecimal digits.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("#[0-9a-f]{3,8}" + WordBoundary, Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex CssHexValue();

    /// <summary>
    ///     Matches a subresource-integrity style digest: <c>sha</c>, a number, a hyphen, and at least 25 Base64 characters.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(WordBoundary + "sha[0-9]+-[a-z0-9+/]{25,}={0,3}", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Sha();

    /// <summary>
    ///     Matches a hash or token string introduced by a scheme name such as <c>sha256</c>, <c>md5</c>,
    ///     <c>bcrypt</c>, or <c>assertion</c>, with the characters that follow.
    /// </summary>
    /// <remarks>
    ///     The scheme name is followed by a separator and at least 25 characters of the data alphabet; the
    ///     data may continue as concatenated quoted strings.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?:" + WordBoundary + "(?:sha[0-9]+|md5|base64|crypt|bcrypt|scrypt|security-token|assertion)[-,:$=]|#code[/])" +
        "[-A-Za-z0-9_/+%.]{25,}={0,3}" +
        "(?:(['\"])\\s*\\+?\\s*\\1?[-A-Za-z0-9_/+%.]+={0,3})*(?![-A-Za-z0-9_/+=%.])",
        Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex HashStrings();

    /// <summary>
    ///     Matches a Unicode code point reference such as <c>U+1F600</c> or a range <c>U+0041-005A</c>.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(WordBoundary + "U\\+[0-9a-f]{4,5}(?:-[0-9a-f]{4,5})?", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex UnicodeRef();

    /// <summary>
    ///     Matches a UUID in its 8-4-4-4-12 form; an <c>x</c> may stand for a placeholder digit.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        WordBoundary + "[0-9a-fx]{8}-[0-9a-fx]{4}-[0-9a-fx]{4}-[0-9a-fx]{4}-[0-9a-fx]{12}" + WordBoundary,
        Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Uuid();
}
