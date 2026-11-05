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

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Interprets the directives that comments in a text use to control spell checking.
/// </summary>
/// <remarks>
///     Users silence deliberate exceptions with directives such as <c>cspell:disable</c> in a comment, so a
///     migrated project must find them working. A directive starts with a prefix, one of <c>cspell</c>,
///     <c>spell</c>, <c>spellchecker</c>, <c>spell-checker</c>, or <c>textlint</c> (any letter case), then a
///     colon (or two), optional white space, and the directive name; the prefix must not be preceded by a
///     letter, digit, or underscore. The supported directives follow cspell:
///     <list type="bullet">
///         <item>
///             <c>disable</c> hides the text from the directive to the next <c>enable</c> written with the
///             same prefix spelling (case aside), or to the end of the text. Text before the directive on its
///             line and after the <c>enable</c> keyword on its line stays checked.
///         </item>
///         <item><c>disable-line</c> hides the whole line, including text before the directive.</item>
///         <item>
///             <c>disable-next-line</c> (also <c>disable-next</c>) hides the rest of its own line and the whole
///             next line. The pattern covers one or two line-break characters after the directive line, as in
///             cspell: when the next line is blank, a CRLF break hides only that blank line, while with LF breaks
///             the line after the blank line is hidden as well.
///         </item>
///         <item>
///             <c>ignore</c> and <c>words</c> (and their word and hyphenated forms) add the listed words,
///             separated by commas, semicolons, or white space, for the whole document; the text of the
///             directive itself is not checked.
///         </item>
///     </list>
///     Directives that cspell supports and this engine does not (<c>flag</c> and <c>forbid</c>,
///     <c>locale</c> (and its alias <c>local</c>) and <c>language</c>, <c>dictionary</c>, <c>ignoreRegExp</c>, <c>includeRegExp</c>, and the
///     compound-word and case-sensitivity switches) are reported so a user does not believe they are in force;
///     text that merely resembles a directive (<c>disabled</c>, <c>disable-lines</c>, <c>xcspell:disable</c>) and
///     unknown directive names are ignored. A directive inside a region where checking is switched off is not
///     reported. Because the patterns are ports of cspell's, the JavaScript differences listed for
///     <see cref="DefaultPatterns"/> apply here too. A cheap search for the prefix words runs first, so texts
///     without any directive pay almost nothing. The class is stateless and thread-safe.
/// </remarks>
internal static partial class DocumentDirectives
{
    /// <summary>
    ///     The longest time, in milliseconds, that one pattern may spend on one text; see <see cref="DefaultPatterns"/>.
    /// </summary>
    private const int MatchTimeoutMilliseconds = DefaultPatterns.MatchTimeoutMilliseconds;

    /// <summary>
    ///     The words whose presence in a text means the text might contain a directive prefix.
    /// </summary>
    private static readonly string[] PrefixMarkers = ["spell", "textlint"];

    /// <summary>
    ///     Scans a text for directives.
    /// </summary>
    /// <remarks>
    ///     The whole text is scanned, including regions that a directive switches off, because a words
    ///     directive adds its words to the document wherever it is. Thread-safe.
    /// </remarks>
    /// <param name="text">The text to scan. Must not be null.</param>
    /// <returns>What the directives say; <see cref="DirectiveScan.Empty"/> when the text has none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="RegexMatchTimeoutException">Thrown when a pattern exceeds its time limit on this text.</exception>
    internal static DirectiveScan Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Most texts have no directive, so rule that out with a cheap search before any pattern runs
        if (!MayContainDirective(text))
        {
            return DirectiveScan.Empty;
        }

        // Find the regions where checking is switched off by the three disable forms
        var disabled = new List<TextRange>();
        CollectMatches(DisableBlock(), text, disabled);
        CollectMatches(DisableNext(), text, disabled);
        CollectMatches(DisableLine(), text, disabled);

        // The text of a directive that adds words is excluded, so its words are not checked as text
        var excluded = new List<TextRange>(disabled);
        CollectMatches(IgnoreDirective(), text, excluded);

        // Read every directive line for the words it adds and the unsupported directives it names
        var ignoredWords = new List<string>();
        var words = new List<string>();
        var unsupported = new List<TextRange>();
        foreach (var match in DirectiveLine().EnumerateMatches(text))
        {
            ReadDirective(text, match.Index, match.Length, disabled, ignoredWords, words, unsupported);
        }

        if (excluded.Count == 0 && ignoredWords.Count == 0 && words.Count == 0 && unsupported.Count == 0)
        {
            return DirectiveScan.Empty;
        }

        return new DirectiveScan([.. excluded], [.. ignoredWords], [.. words], [.. unsupported]);
    }

    /// <summary>
    ///     Determines whether a text could contain a directive.
    /// </summary>
    /// <remarks>
    ///     A directive needs one of the prefix words, so a text without them cannot have one. The test is a
    ///     pure optimization: it never changes the result, only avoids running the patterns. Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="text">The text to test. Must not be null.</param>
    /// <returns><see langword="true"/> when the text contains a prefix word in any letter case.</returns>
    private static bool MayContainDirective(string text) =>
        PrefixMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Appends the range of every match of a pattern to a list.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="pattern">The pattern to run. Must not be null.</param>
    /// <param name="text">The text to search. Must not be null.</param>
    /// <param name="ranges">The list that receives the ranges. Must not be null.</param>
    private static void CollectMatches(Regex pattern, string text, List<TextRange> ranges)
    {
        foreach (var match in pattern.EnumerateMatches(text))
        {
            ranges.Add(new TextRange(match.Index, match.Length));
        }
    }

    /// <summary>
    ///     Reads one directive line and records the words it adds or the unsupported directive it names.
    /// </summary>
    /// <remarks>
    ///     The line is the prefix and everything after it up to the end of the line. Side effects: appends
    ///     to the three lists. Stateless and thread-safe as long as the lists are not shared.
    /// </remarks>
    /// <param name="text">The scanned text. Must not be null.</param>
    /// <param name="start">The offset of the prefix of the directive line.</param>
    /// <param name="length">The length of the directive line from the prefix to the end of the line.</param>
    /// <param name="disabled">The regions where checking is switched off. Must not be null.</param>
    /// <param name="ignoredWords">The list that receives words from <c>ignore</c> directives. Must not be null.</param>
    /// <param name="words">The list that receives words from <c>words</c> directives. Must not be null.</param>
    /// <param name="unsupported">The list that receives the names of unsupported directives. Must not be null.</param>
    private static void ReadDirective(
        string text,
        int start,
        int length,
        List<TextRange> disabled,
        List<string> ignoredWords,
        List<string> words,
        List<TextRange> unsupported)
    {
        // The directive is what follows the prefix and its colon (or two colons), without surrounding white space
        var line = text.Substring(start, length);
        var restStart = line.IndexOf(':') + 1;
        if (restStart < line.Length && line[restStart] == ':')
        {
            restStart++;
        }

        var rest = line[restStart..];
        var directive = rest.Trim();
        if (directive.Length == 0)
        {
            return;
        }

        // Classify the directive; names that are not recognized are ignored
        var match = DirectiveKind().Match(directive);
        if (!match.Success)
        {
            return;
        }

        if (match.Groups["words"].Success)
        {
            SplitWords(directive, words);
        }
        else if (match.Groups["ignore"].Success)
        {
            SplitWords(directive, ignoredWords);
        }
        else
        {
            // An unsupported directive is reported at its name, unless checking is switched off there
            var nameStart = start + restStart + (rest.Length - rest.TrimStart().Length);
            var name = new TextRange(nameStart, NameLength(directive));
            if (!IsInside(disabled, name.Start))
            {
                unsupported.Add(name);
            }
        }
    }

    /// <summary>
    ///     Gets the length of the directive name at the start of a directive.
    /// </summary>
    /// <remarks>
    ///     The name is the leading run of letters, digits, underscores, and hyphens. Stateless and thread-safe.
    /// </remarks>
    /// <param name="directive">The directive text without surrounding white space. Must not be null.</param>
    /// <returns>The number of characters in the name.</returns>
    private static int NameLength(string directive)
    {
        var length = 0;
        while (length < directive.Length && (char.IsAsciiLetterOrDigit(directive[length]) || directive[length] is '_' or '-'))
        {
            length++;
        }

        return length;
    }

    /// <summary>
    ///     Splits the words that follow the name of a words directive and appends them to a list.
    /// </summary>
    /// <remarks>
    ///     Words are separated by commas, semicolons, and white space; the first item, the directive name,
    ///     is dropped. Stateless and thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="directive">The directive text without surrounding white space. Must not be null.</param>
    /// <param name="into">The list that receives the words. Must not be null.</param>
    private static void SplitWords(string directive, List<string> into)
    {
        var index = 0;
        var isName = true;
        while (index < directive.Length)
        {
            // Skip the separators, then read to the next one
            while (index < directive.Length && IsWordSeparator(directive[index]))
            {
                index++;
            }

            var itemStart = index;
            while (index < directive.Length && !IsWordSeparator(directive[index]))
            {
                index++;
            }

            if (index > itemStart)
            {
                if (isName)
                {
                    isName = false;
                }
                else
                {
                    into.Add(directive[itemStart..index]);
                }
            }
        }
    }

    /// <summary>
    ///     Determines whether a character separates the words of a words directive.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> for a comma, a semicolon, or white space.</returns>
    private static bool IsWordSeparator(char character) => character is ',' or ';' || char.IsWhiteSpace(character);

    /// <summary>
    ///     Determines whether an offset lies inside any of a list of ranges.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="ranges">The ranges to test. Must not be null.</param>
    /// <param name="offset">The offset to test.</param>
    /// <returns><see langword="true"/> when some range starts at or before the offset and ends after it.</returns>
    private static bool IsInside(List<TextRange> ranges, int offset)
    {
        foreach (var range in ranges)
        {
            if (offset >= range.Start && offset < range.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Matches a disable block: from a disable directive to the enable directive with the same prefix
    ///     spelling, or to the end of the text.
    /// </summary>
    /// <remarks>
    ///     The enable directive must repeat the prefix of the disable directive (the backreference is
    ///     case-insensitive), so a block opened with one prefix is not closed by another. The block includes
    ///     the enable keyword. <c>disabled</c>, <c>disable-line</c>, <c>disable-next</c>, and any other
    ///     <c>disable-</c> name are not blocks.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9_])((?:c?spell(?:-?checker)?|textlint)::?)\\s*disable(?!-line|-next)(?![A-Za-z0-9_])(?!-)" +
        "[\\s\\S]*?(?:\\1\\s*enable(?![A-Za-z0-9_])|\\z)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DisableBlock();

    /// <summary>
    ///     Matches a next-line directive: the rest of its own line and the whole next line.
    /// </summary>
    /// <remarks>
    ///     The next line is covered through its end-of-line; when the directive line is the last line, so
    ///     that no line terminator follows, nothing matches, as in cspell. Up to two line-break characters
    ///     follow the directive line, so a CRLF break is one unit; consequently, when the next line is
    ///     blank, a CRLF break covers only that blank line while with LF breaks the following line is
    ///     covered too, exactly as in cspell.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9_])(?:c?spell(?:-?checker)?|textlint)::?\\s*disable-next(?![A-Za-z0-9_])" +
        "[^\\r\\n\\u2028\\u2029]*\\s\\s?[^\\r\\n\\u2028\\u2029]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DisableNext();

    /// <summary>
    ///     Matches a disable-line directive: the whole line that holds it, including text before it.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![^\\r\\n\\u2028\\u2029])[^\\r\\n\\u2028\\u2029]*" +
        "(?<![A-Za-z0-9_])(?:c?spell(?:-?checker)?|textlint)::?\\s*disable-line(?![A-Za-z0-9_])[^\\r\\n\\u2028\\u2029]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DisableLine();

    /// <summary>
    ///     Matches the text of a directive that adds words or an expression, from its prefix to the end of the line.
    /// </summary>
    /// <remarks>
    ///     The words listed in such a directive are data, not prose, so the whole directive is excluded from checking.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9_])(?:c?spell(?:-?checker)?|textlint)::?\\s*(?:ignoreRegExp|word|ignore)[^\\r\\n\\u2028\\u2029]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex IgnoreDirective();

    /// <summary>
    ///     Matches a directive line: a prefix, a colon, and the rest of the line.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9_])(?:spell-?checker|c?spell|textlint)::?[^\\r\\n\\u2028\\u2029]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DirectiveLine();

    /// <summary>
    ///     Classifies the text after a directive prefix as an <c>ignore</c> directive, a <c>words</c> directive, or a
    ///     recognized directive that is not supported.
    /// </summary>
    /// <remarks>
    ///     The text is matched from its start; a name must be followed by something other than a letter, digit,
    ///     underscore, or hyphen, so <c>ignored</c> and <c>words-x</c> are not directives. The unsupported group
    ///     lists the directives cspell has and this engine does not. Each name is spelled out in full, so a name
    ///     that merely starts like a directive (<c>locals</c>, <c>locales</c>) is not one; the only alias is
    ///     <c>local</c> for <c>locale</c>, which cspell lists among its official directives. The enable and
    ///     disable forms are not listed because their effect is a range, which the other patterns produce.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "^(?:(?<unsupported>(?:enable|disable)(?:allow)?CompoundWords(?![A-Za-z0-9_-])" +
        "|(?:enable|disable)CaseSensitive(?![A-Za-z0-9_-])" +
        "|(?:flag|forbid)(?:-?words?)?(?![A-Za-z0-9_-])" +
        "|ignore_?Reg_?Exp\\s+\\S" +
        "|include_?Reg_?Exp\\s+\\S" +
        "|(?:locale|local)(?![A-Za-z0-9_-])" +
        "|language\\s(?=[A-Za-z0-9_])" +
        "|dictionar(?:y|ies)(?![A-Za-z0-9_-]))" +
        "|(?<ignore>ignore(?:-?words?)?(?![A-Za-z0-9_-]))" +
        "|(?<words>words?(?![A-Za-z0-9_-])))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DirectiveKind();
}
