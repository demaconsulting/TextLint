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

using System.Buffers;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using static DemaConsulting.TextLint.TextScanning.JavaScriptSyntax;

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
///         <item>
///             <c>ignoreRegExp</c> adds one pattern, written like an <c>ignoreRegExpList</c> entry, for the
///             whole document, wherever the directive is, as in cspell. This class only finds the pattern, the
///             way cspell reads it from the line (see <c>FindPattern</c>); the checker turns it into a regular
///             expression and reports a pattern that cannot be used. The text of the directive is not checked.
///         </item>
///     </list>
///     Every occurrence of a prefix is classified exactly once, by one pattern, and both what the occurrence
///     hides and whether it is reported are derived from that classification, so the two can never disagree.
///     A name is a directive only when it ends where cspell's own patterns end it: after the keyword, a
///     <c>disable-line</c>, <c>disable-next</c>, or <c>enable</c> may be followed by a hyphen (so
///     <c>disable-next-line</c> and <c>disable-line-x</c> are the real directives, as in cspell, which hides
///     text for them), but <c>disable</c>, <c>ignore</c>, and <c>words</c> must not be followed by a letter,
///     digit, underscore, or hyphen. A name that begins with a directive keyword but is not a directive
///     (<c>disabled</c>, <c>disable-lines</c>, <c>ignored</c>, <c>wordsmith</c>, <c>words-x</c>) hides nothing
///     and adds nothing, and is reported like an unsupported directive, because the author evidently meant a
///     directive that is not in force; cspell hides the rest of the line for the <c>ignore</c> and
///     <c>word</c> forms, which this engine deliberately does not. Directives that cspell supports and this
///     engine does not (<c>flag</c> and <c>forbid</c>, <c>locale</c> (and its alias <c>local</c>) and
///     <c>language</c>, <c>dictionary</c>, <c>includeRegExp</c>, and the compound-word and
///     case-sensitivity switches) are reported so a user does not believe they are in force. Names that
///     do not begin with a directive keyword (<c>mystery</c>), a prefix that follows a letter
///     (<c>xcspell:disable</c>), and a prefix without a name are ignored. A directive inside a region where
///     checking is switched off is not reported. Because the patterns are ports of cspell's, the JavaScript
///     differences listed for <see cref="DefaultPatterns"/> apply here too. A cheap search for the prefix words
///     runs first, so texts without any directive pay almost nothing. The class is stateless and thread-safe.
/// </remarks>
internal static partial class DocumentDirectives
{
    /// <summary>
    ///     The longest time, in milliseconds, that one pattern may spend on one text; see <see cref="DefaultPatterns"/>.
    /// </summary>
    private const int MatchTimeoutMilliseconds = DefaultPatterns.MatchTimeoutMilliseconds;

    /// <summary>
    ///     The flag letters that cspell reads after the closing slash of a pattern written in a directive.
    /// </summary>
    private const string CspellPatternFlags = "gimuy";

    /// <summary>
    ///     The words whose presence in a text means the text might contain a directive prefix.
    /// </summary>
    private static readonly string[] PrefixMarkers = ["spell", "textlint"];

    /// <summary>
    ///     The characters that end a line, as cspell's patterns understand it.
    /// </summary>
    private static readonly SearchValues<char> LineTerminators = SearchValues.Create("\r\n\u2028\u2029");

    /// <summary>
    ///     The pairs of a pattern group name and the kind of directive the group stands for, in the order
    ///     they are tried; a name that matches no group but begins with a directive keyword is a near miss.
    /// </summary>
    private static readonly (string Group, DirectiveKind Kind)[] KindGroups =
    [
        ("next", DirectiveKind.DisableNext),
        ("line", DirectiveKind.DisableLine),
        ("disable", DirectiveKind.Disable),
        ("enable", DirectiveKind.Enable),
        ("ignore", DirectiveKind.Ignore),
        ("words", DirectiveKind.Words),
        ("regexp", DirectiveKind.IgnoreRegExp),
        ("unsupported", DirectiveKind.Unsupported),
        ("nearMiss", DirectiveKind.NearMiss),
    ];

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

        // Classify every directive prefix once; everything below is derived from this list
        var directives = FindDirectives(text);
        if (directives.Count == 0)
        {
            return DirectiveScan.Empty;
        }

        // Find the regions where checking is switched off by the three disable forms
        var disabled = new List<TextRange>();
        CollectDisabledBlocks(text, directives, disabled);
        CollectDisabledNextLines(text, directives, disabled);
        CollectDisabledLines(text, directives, disabled);

        // The text of a directive that adds words is excluded, so its words are not checked as text
        var excluded = new List<TextRange>(disabled);
        foreach (var directive in directives.Where(directive => directive.Kind is DirectiveKind.Ignore or DirectiveKind.Words or DirectiveKind.IgnoreRegExp))
        {
            excluded.Add(new TextRange(directive.PrefixStart, LineEnd(text, directive.NameEnd) - directive.PrefixStart));
        }

        // Read the directive lines for the words they add, the patterns they add, and the unsupported directives they name
        var ignoredWords = new List<string>();
        var words = new List<string>();
        var unsupported = new List<TextRange>();
        var patterns = new List<DirectiveScan.PatternDirective>();
        ReadDirectiveLines(text, directives, disabled, ignoredWords, words, unsupported, patterns);

        if (excluded.Count == 0 && ignoredWords.Count == 0 && words.Count == 0 && unsupported.Count == 0 && patterns.Count == 0)
        {
            return DirectiveScan.Empty;
        }

        return new DirectiveScan([.. excluded], [.. ignoredWords], [.. words], [.. unsupported], [.. patterns]);
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
    ///     Finds every directive prefix in a text and classifies the name that follows it.
    /// </summary>
    /// <remarks>
    ///     This is the only place that decides what a directive is. A prefix inside the text of another
    ///     directive on the same line is found too, because cspell's range patterns act on it. Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="text">The text to scan. Must not be null.</param>
    /// <returns>The directives in text order; empty when the text has no prefix.</returns>
    private static List<Directive> FindDirectives(string text)
    {
        // The patterns ignore letter case with the .NET rules, which also equate the Kelvin sign with k; JavaScript does
        // not, so the sign is replaced by a character of the same length that no pattern reads as a letter
        var view = JavaScriptCaseFolding.MaskKelvinSign(text);
        var directives = new List<Directive>();
        foreach (Match prefix in DirectivePrefix().Matches(view))
        {
            // Classify the name that follows the prefix and its optional white space
            var nameStart = prefix.Index + prefix.Length;
            var name = DirectiveName().Match(view, nameStart);
            var kind = DirectiveKind.None;
            var nameEnd = nameStart;
            if (name.Success)
            {
                kind = KindOf(name);
                nameEnd = name.Index + name.Length;
            }

            directives.Add(new Directive(prefix.Index, prefix.Groups["prefix"].Value, nameStart, nameEnd, kind));
        }

        return directives;
    }

    /// <summary>
    ///     Gets the kind of directive that a successful name match stands for.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">A successful match of the directive name pattern. Must not be null.</param>
    /// <returns>The kind of the first group that took part in the match.</returns>
    private static DirectiveKind KindOf(Match name)
    {
        foreach (var (group, kind) in KindGroups)
        {
            if (name.Groups[group].Success)
            {
                return kind;
            }
        }

        return DirectiveKind.None;
    }

    /// <summary>
    ///     Adds the range of every disable block: from a disable directive to the enable directive with the
    ///     same prefix spelling, or to the end of the text.
    /// </summary>
    /// <remarks>
    ///     The enable directive must repeat the prefix of the disable directive, so a block opened with one
    ///     prefix is not closed by another. The block includes the enable keyword, and a directive inside the
    ///     block does not start another block. Stateless and thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The scanned text. Must not be null.</param>
    /// <param name="directives">The classified directives. Must not be null.</param>
    /// <param name="ranges">The list that receives the ranges. Must not be null.</param>
    private static void CollectDisabledBlocks(string text, List<Directive> directives, List<TextRange> ranges)
    {
        var blockEnd = 0;
        for (var index = 0; index < directives.Count; index++)
        {
            var start = directives[index];
            if (start.Kind != DirectiveKind.Disable || start.PrefixStart < blockEnd)
            {
                continue;
            }

            // The block ends after the first enable with the same prefix, or at the end of the text
            blockEnd = text.Length;
            for (var next = index + 1; next < directives.Count; next++)
            {
                if (directives[next].Kind == DirectiveKind.Enable &&
                    string.Equals(directives[next].Prefix, start.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    blockEnd = directives[next].NameEnd;
                    break;
                }
            }

            ranges.Add(new TextRange(start.PrefixStart, blockEnd - start.PrefixStart));
        }
    }

    /// <summary>
    ///     Adds the range of every next-line directive: the rest of its own line and the whole next line.
    /// </summary>
    /// <remarks>
    ///     The next line is covered through its end-of-line; when the directive line is the last line, so
    ///     that no line terminator follows, nothing is covered, as in cspell. Up to two line-break characters
    ///     follow the directive line, so a CRLF break is one unit; consequently, when the next line is
    ///     blank, a CRLF break covers only that blank line while with LF breaks the following line is
    ///     covered too, exactly as in cspell. Stateless and thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The scanned text. Must not be null.</param>
    /// <param name="directives">The classified directives. Must not be null.</param>
    /// <param name="ranges">The list that receives the ranges. Must not be null.</param>
    private static void CollectDisabledNextLines(string text, List<Directive> directives, List<TextRange> ranges)
    {
        foreach (var directive in directives.Where(directive => directive.Kind == DirectiveKind.DisableNext))
        {
            var tail = NextLineTail().Match(text, directive.NameEnd);
            if (tail.Success)
            {
                ranges.Add(new TextRange(directive.PrefixStart, tail.Index + tail.Length - directive.PrefixStart));
            }
        }
    }

    /// <summary>
    ///     Adds the range of every disable-line directive: the whole line that holds it, including text before it.
    /// </summary>
    /// <remarks>
    ///     Two directives on one line add the line once. Stateless and thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The scanned text. Must not be null.</param>
    /// <param name="directives">The classified directives. Must not be null.</param>
    /// <param name="ranges">The list that receives the ranges. Must not be null.</param>
    private static void CollectDisabledLines(string text, List<Directive> directives, List<TextRange> ranges)
    {
        var lastLineStart = -1;
        foreach (var directive in directives.Where(directive => directive.Kind == DirectiveKind.DisableLine))
        {
            var lineStart = text.AsSpan(0, directive.PrefixStart).LastIndexOfAny(LineTerminators) + 1;
            if (lineStart != lastLineStart)
            {
                ranges.Add(new TextRange(lineStart, LineEnd(text, directive.NameEnd) - lineStart));
                lastLineStart = lineStart;
            }
        }
    }

    /// <summary>
    ///     Reads the directive lines and records the words they add and the unsupported directives they name.
    /// </summary>
    /// <remarks>
    ///     Like cspell, only the first prefix of a line is read, and the directive is everything after it up
    ///     to the end of the line; a prefix inside that text, and a prefix spelled
    ///     <c>cspellchecker</c> or <c>cspell-checker</c>, which cspell's range patterns accept but its
    ///     directive reader does not, are not read. The name must be on the same line as its prefix. Side
    ///     effects: appends to the three lists. Stateless and thread-safe as long as the lists are not shared.
    /// </remarks>
    /// <param name="text">The scanned text. Must not be null.</param>
    /// <param name="directives">The classified directives. Must not be null.</param>
    /// <param name="disabled">The regions where checking is switched off. Must not be null.</param>
    /// <param name="ignoredWords">The list that receives words from <c>ignore</c> directives. Must not be null.</param>
    /// <param name="words">The list that receives words from <c>words</c> directives. Must not be null.</param>
    /// <param name="unsupported">The list that receives the names of unsupported directives. Must not be null.</param>
    /// <param name="patterns">The list that receives the patterns of <c>ignoreRegExp</c> directives. Must not be null.</param>
    private static void ReadDirectiveLines(
        string text,
        List<Directive> directives,
        List<TextRange> disabled,
        List<string> ignoredWords,
        List<string> words,
        List<TextRange> unsupported,
        List<DirectiveScan.PatternDirective> patterns)
    {
        var readEnd = 0;
        foreach (var directive in directives)
        {
            // Only the first readable prefix of a line is read; the rest of the line belongs to it
            if (directive.PrefixStart < readEnd || !ReadablePrefix().IsMatch(directive.Prefix))
            {
                continue;
            }

            var lineEnd = LineEnd(text, directive.PrefixStart + directive.Prefix.Length);
            readEnd = lineEnd;
            if (directive.Kind == DirectiveKind.None || directive.NameStart > lineEnd)
            {
                continue;
            }

            switch (directive.Kind)
            {
                case DirectiveKind.Words:
                    SplitWords(text[directive.NameStart..lineEnd], words);
                    break;

                case DirectiveKind.Ignore:
                    SplitWords(text[directive.NameStart..lineEnd], ignoredWords);
                    break;

                case DirectiveKind.IgnoreRegExp:
                    // The pattern is the rest of the line, as cspell reads it; a directive with nothing after
                    // its name on the line has no pattern, which is reported like an unsupported directive
                    var pattern = FindPattern(text, directive.NameEnd, lineEnd);
                    var reportable = !IsInside(disabled, directive.NameStart);
                    if (pattern.Length > 0)
                    {
                        patterns.Add(new DirectiveScan.PatternDirective(pattern, text.Substring(pattern.Start, pattern.Length), reportable));
                    }
                    else if (reportable)
                    {
                        unsupported.Add(new TextRange(directive.NameStart, NameLength(text, directive.NameStart, lineEnd)));
                    }

                    break;

                case DirectiveKind.Unsupported or DirectiveKind.NearMiss:
                    // Reported at its name, unless checking is switched off there
                    if (!IsInside(disabled, directive.NameStart))
                    {
                        unsupported.Add(new TextRange(directive.NameStart, NameLength(text, directive.NameStart, lineEnd)));
                    }

                    break;

                default:
                    // The range directives have their effect through the ranges, and need no reading
                    break;
            }
        }
    }

    /// <summary>
    ///     Finds the pattern that an <c>ignoreRegExp</c> directive names, the way cspell reads the rest of the line.
    /// </summary>
    /// <remarks>
    ///     After the directive name and its white space, cspell takes the longest stretch that starts at the
    ///     first slash that has another slash after it and ends at the last slash, together with any of the
    ///     flag letters <c>g</c>, <c>i</c>, <c>m</c>, <c>u</c>, and <c>y</c> that follow it, wherever it is on the line
    ///     (so two literals on one line are read as one, which is cspell's behavior). When the line has no such
    ///     stretch, the pattern is its first word. Anything after the pattern is ignored. The flag letters
    ///     are cspell's, so a <c>/x/s</c> written in a directive is read as <c>/x/</c>; the other flags are
    ///     available in the settings. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <param name="nameEnd">The offset just past the directive name.</param>
    /// <param name="lineEnd">The offset of the end of the line that holds the directive.</param>
    /// <returns>The range of the pattern; an empty range when the line has nothing after the name.</returns>
    private static TextRange FindPattern(string text, int nameEnd, int lineEnd)
    {
        // Trim white space around the rest of the line, as cspell trims the directive text
        var start = nameEnd;
        while (start < lineEnd && JavaScriptSyntax.IsSpace(text[start]))
        {
            start++;
        }

        var end = lineEnd;
        while (end > start && JavaScriptSyntax.IsSpace(text[end - 1]))
        {
            end--;
        }

        if (start >= end)
        {
            return new TextRange(start, 0);
        }

        // A stretch between a slash and a later slash, with the flags that follow it, is the pattern
        var rest = text.AsSpan(start, end - start);
        var first = rest.IndexOf('/');
        var last = rest.LastIndexOf('/');
        if (first >= 0 && last > first)
        {
            var patternEnd = last + 1;
            while (patternEnd < rest.Length && CspellPatternFlags.Contains(rest[patternEnd]))
            {
                patternEnd++;
            }

            return new TextRange(start + first, patternEnd - first);
        }

        // Otherwise the pattern is the first word
        var length = 0;
        while (length < rest.Length && !JavaScriptSyntax.IsSpace(rest[length]))
        {
            length++;
        }

        return new TextRange(start, length);
    }

    /// <summary>
    ///     Gets the offset of the end of the line that holds an offset.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <param name="offset">An offset in the text, at most its length.</param>
    /// <returns>The offset of the next line terminator at or after the offset, or the length of the text.</returns>
    private static int LineEnd(string text, int offset)
    {
        var distance = text.AsSpan(offset).IndexOfAny(LineTerminators);
        return distance < 0 ? text.Length : offset + distance;
    }

    /// <summary>
    ///     Gets the length of the directive name that starts at an offset.
    /// </summary>
    /// <remarks>
    ///     The name is the leading run of letters, digits, underscores, and hyphens, and does not extend past
    ///     the given end. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <param name="start">The offset of the first character of the name.</param>
    /// <param name="end">The offset the name may not extend past, normally the end of its line.</param>
    /// <returns>The number of characters in the name.</returns>
    private static int NameLength(string text, int start, int end)
    {
        var length = 0;
        while (start + length < end && (char.IsAsciiLetterOrDigit(text[start + length]) || text[start + length] is '_' or '-'))
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
    /// <param name="directive">The directive text from its name to the end of its line. Must not be null.</param>
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
    private static bool IsWordSeparator(char character) => character is ',' or ';' || JavaScriptSyntax.IsSpace(character);

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
    ///     Matches a directive prefix and the white space after it.
    /// </summary>
    /// <remarks>
    ///     This is the wide set of prefixes that cspell's range patterns accept. The prefix group holds the
    ///     prefix with its colon or colons, which a disable block repeats to find its enable. The white space
    ///     may cross a line break, as in cspell's range patterns.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9_])(?<prefix>(?:c?spell(?:-?checker)?|textlint)::?)" + Space + "*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DirectivePrefix();

    /// <summary>
    ///     Matches a prefix that cspell's directive reader accepts, which is a narrower set than the one its
    ///     range patterns accept (it lacks <c>cspellchecker</c> and <c>cspell-checker</c>).
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "^(?:spell-?checker|c?spell|textlint)::?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex ReadablePrefix();

    /// <summary>
    ///     Matches what follows a next-line directive's name: the rest of its line, one or two line-break
    ///     characters, and the whole next line.
    /// </summary>
    /// <remarks>
    ///     It is anchored where the search starts, which is the end of the directive's name.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "\\G" + NotLineTerminator + "*" + Space + Space + "?" + NotLineTerminator + "*",
        RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex NextLineTail();

    /// <summary>
    ///     Classifies the text at the start of a directive name, which is the only classification of directive
    ///     names in this class.
    /// </summary>
    /// <remarks>
    ///     It is anchored where the search starts, which is the first character after the prefix and its
    ///     white space. The groups are, in order of precedence: <c>next</c> (<c>disable-next</c>, which also
    ///     covers <c>disable-next-line</c>), <c>line</c>, <c>disable</c>, <c>enable</c>, <c>ignore</c>,
    ///     <c>words</c>, <c>regexp</c> (<c>ignoreRegExp</c> with an argument), <c>unsupported</c> (directives
    ///     that cspell has and this engine does not), and <c>nearMiss</c> (a name that only begins with a
    ///     directive keyword). As in cspell's range patterns, <c>disable-next</c>, <c>disable-line</c>, and
    ///     <c>enable</c> end at any character other than a letter, digit, or underscore, so a hyphen may
    ///     follow them; <c>disable</c>, <c>ignore</c>, and <c>words</c> must not be followed by a hyphen
    ///     either. Each name is spelled out in full; the only alias is <c>local</c> for <c>locale</c>, which
    ///     cspell lists among its official directives.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "\\G(?:(?<next>disable-next(?![A-Za-z0-9_]))" +
        "|(?<line>disable-line(?![A-Za-z0-9_]))" +
        "|(?<disable>disable(?![A-Za-z0-9_-]))" +
        "|(?<enable>enable(?![A-Za-z0-9_]))" +
        "|(?<ignore>ignore(?:-?words?)?(?![A-Za-z0-9_-]))" +
        "|(?<words>words?(?![A-Za-z0-9_-]))" +
        "|(?<regexp>ignore_?Reg_?Exp(?=" + Space + "+" + NotSpace + "))" +
        "|(?<unsupported>(?:enable|disable)(?:allow)?CompoundWords(?![A-Za-z0-9_-])" +
        "|(?:enable|disable)CaseSensitive(?![A-Za-z0-9_-])" +
        "|(?:flag|forbid)(?:-?words?)?(?![A-Za-z0-9_-])" +
        "|include_?Reg_?Exp(?=" + Space + "+" + NotSpace + ")" +
        "|(?:locale|local)(?![A-Za-z0-9_-])" +
        "|language(?=" + Space + "[A-Za-z0-9_])" +
        "|dictionar(?:y|ies)(?![A-Za-z0-9_-]))" +
        "|(?<nearMiss>(?:disable|enable|ignore|word|flag|forbid|local|language|dictionar|include)))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex DirectiveName();

    /// <summary>
    ///     The kinds of directive that one classification distinguishes.
    /// </summary>
    private enum DirectiveKind
    {
        /// <summary>The prefix is not followed by any recognizable name.</summary>
        None,

        /// <summary>Starts a disable block.</summary>
        Disable,

        /// <summary>Ends a disable block.</summary>
        Enable,

        /// <summary>Hides the line that holds it.</summary>
        DisableLine,

        /// <summary>Hides the rest of its line and the next line.</summary>
        DisableNext,

        /// <summary>Adds ignored words to the document.</summary>
        Ignore,

        /// <summary>Adds words to the document.</summary>
        Words,

        /// <summary>Adds a pattern whose matches are not checked, for the whole document.</summary>
        IgnoreRegExp,

        /// <summary>A directive that cspell has and this engine does not.</summary>
        Unsupported,

        /// <summary>A name that begins with a directive keyword but is not a directive.</summary>
        NearMiss,
    }

    /// <summary>
    ///     One classified directive prefix.
    /// </summary>
    /// <param name="PrefixStart">The offset of the prefix.</param>
    /// <param name="Prefix">The prefix with its colon or colons, as written.</param>
    /// <param name="NameStart">The offset of the first character after the prefix and its white space.</param>
    /// <param name="NameEnd">The offset after the recognized name keyword; equal to the start when there is none.</param>
    /// <param name="Kind">The kind of directive, which decides both what it hides and whether it is reported.</param>
    private readonly record struct Directive(int PrefixStart, string Prefix, int NameStart, int NameEnd, DirectiveKind Kind);
}
