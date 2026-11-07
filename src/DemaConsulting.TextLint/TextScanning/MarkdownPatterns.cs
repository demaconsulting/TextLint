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

// cspell:ignore zorb markdn mdown mdtext mdtxt mdwn

using System.Text.RegularExpressions;
using static DemaConsulting.TextLint.TextScanning.JavaScriptSyntax;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Finds the parts of a Markdown document that are not prose: link targets, link reference labels, anchors,
///     and HTML entities.
/// </summary>
/// <remarks>
///     In a Markdown file the target of a link (<c>[text](zorb-target)</c>), the label of a reference link
///     (<c>[text][label]</c>), the definition of a reference (<c>[label]: https://example.org</c>), the id of
///     an anchor (<c>&lt;a id="name"&gt;</c>), and an HTML entity (<c>&amp;nbsp;</c>) are names and addresses,
///     and their fragments would be reported as unknown words. cspell excludes them for Markdown files with
///     patterns of its Markdown dictionary package and its default settings, which are ported here with the
///     same translation rules as the default patterns: ASCII word characters, the JavaScript white space set
///     (see <see cref="JavaScriptSyntax"/>), and the case-sensitivity of the
///     originals. The set
///     applies to files whose name ends in one of the extensions that cspell reads as Markdown, ignoring
///     letter case. Every pattern has a match time limit. The class is stateless and thread-safe; the
///     generated regular expressions are immutable and safe for concurrent matching.
/// </remarks>
internal static partial class MarkdownPatterns
{
    /// <summary>
    ///     The options of the patterns that cspell compiles without the case-insensitive flag.
    /// </summary>
    private const RegexOptions Sensitive = RegexOptions.CultureInvariant;

    /// <summary>
    ///     The longest time, in milliseconds, that a pattern may spend on one text before matching is abandoned.
    /// </summary>
    private const int MatchTimeoutMilliseconds = PatternParser.MatchTimeoutMilliseconds;

    /// <summary>
    ///     The file name extensions of the files that are read as Markdown, as cspell lists them, with the leading dot.
    /// </summary>
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".markdn", ".markdown", ".md", ".mdown", ".mdtext", ".mdtxt", ".mdwn", ".mkd", ".workbook", ".mdx",
    };

    /// <summary>
    ///     Every Markdown pattern, in the order cspell lists them.
    /// </summary>
    private static readonly Regex[] Patterns = [LinkReference(), LinkFooter(), Link(), Anchor(), SymbolEntity()];

    /// <summary>
    ///     Gets every Markdown pattern.
    /// </summary>
    /// <remarks>
    ///     Exposed so the time limit that bounds every pattern can be verified in one place, and so the
    ///     checker can run them with the code that runs the other excluded-text patterns.
    /// </remarks>
    /// <value>The generated regular expressions, in the order cspell lists them.</value>
    internal static IReadOnlyList<Regex> All => Patterns;

    /// <summary>
    ///     Determines whether the Markdown patterns apply to a source.
    /// </summary>
    /// <remarks>
    ///     The decision is made from the name only, so it is the same on every platform and needs no file.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="sourceName">The name or path of the source, normally the path of the file; may be null.</param>
    /// <returns><see langword="true"/> when the name ends in a Markdown extension, ignoring letter case.</returns>
    internal static bool AppliesTo(string? sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            return false;
        }

        // Look at the end of the name myself: a path from another platform may use separators this one does not
        var dot = sourceName.LastIndexOf('.');
        return dot >= 0 && sourceName.IndexOfAny(['/', '\\'], dot) < 0 && Extensions.Contains(sourceName[dot..]);
    }

    /// <summary>
    ///     Matches the label of a reference link: the second pair of brackets of <c>[text][label]</c>.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?<=\\])\\[[-A-Za-z0-9_.`'\"*&;#@ ]+\\]", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex LinkReference();

    /// <summary>
    ///     Matches the definition of a reference: <c>[label]:</c> and the address that follows it on the line.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("\\[[-A-Za-z0-9_.`'\"*&;#@ ]+\\]:( [^" + SpaceBody + "]*)?", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex LinkFooter();

    /// <summary>
    ///     Matches the target of a link: what follows the <c>](</c> of <c>[text](target)</c>.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?<=\\]\\()[^)" + SpaceBody + "]+", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Link();

    /// <summary>
    ///     Matches the id of an anchor: the value of <c>&lt;a id="..."&gt;</c>.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?<=<a" + Space + "+id=\")[^\"" + SpaceBody + "]+", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Anchor();

    /// <summary>
    ///     Matches an HTML symbol entity such as <c>&amp;clubs;</c>.
    /// </summary>
    /// <remarks>
    ///     Case-sensitive: cspell's Markdown dictionary package defines this pattern case-insensitively, but
    ///     the default settings of cspell redefine it without the flag, and the later definition is the one in
    ///     force, so <c>&amp;Zorb;</c> is not excluded.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("&[a-z]+;", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex SymbolEntity();
}
