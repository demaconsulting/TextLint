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

// cspell:ignore anrvtbf

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Knows the pattern names that cspell defines in advance and turns the <c>ignoreRegExpList</c> and
///     <c>patterns</c> settings into the regular expressions that exclude text from checking.
/// </summary>
/// <remarks>
///     An <c>ignoreRegExpList</c> entry is either a pattern written out (<c>/body/flags</c> or a bare
///     expression, see <see cref="PatternParser"/>) or the name of a pattern. A name is one of the patterns
///     that the <c>patterns</c> setting defines, or one of the twenty-eight names that cspell defines in advance. Of
///     those, nineteen name patterns that are applied to every text anyway (the default ignore list and the
///     directives); listing them is accepted so that a configuration migrated from cspell loads, and changes
///     nothing. The other nine (<c>HexValues</c>, <c>PublicKey</c>, <c>EscapeCharacters</c>, <c>Base64</c>,
///     <c>href</c>, <c>string</c>, <c>CStyleComment</c>, <c>PhpHereDoc</c>, and <c>Everything</c>) are ported
///     here from the cspell originals, with the same translation rules as the default patterns, and exclude
///     text only when listed. Names are compared ignoring letter case, as cspell does. A word written in the
///     shape of a name that is neither defined nor predefined is rejected as an unknown name, where cspell
///     would silently read it as a literal expression; a literal word is written between slashes. A user
///     pattern may not reuse a predefined name, because the same name would then mean two things. The class
///     is stateless and thread-safe; the regular expressions it creates and generates are immutable and safe
///     for concurrent matching.
/// </remarks>
internal static partial class PatternCatalog
{
    /// <summary>
    ///     The pattern text of an ASCII word boundary, which is what <c>\b</c> means in JavaScript.
    /// </summary>
    private const string WordBoundary = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";

    /// <summary>
    ///     The pattern text of the start of a line, which is what <c>^</c> means in JavaScript multiline mode.
    /// </summary>
    private const string LineStart = "(?<![^\\r\\n\\u2028\\u2029])";

    /// <summary>
    ///     The pattern text of a character that is not a JavaScript line terminator, which is what <c>.</c> means.
    /// </summary>
    private const string NotLineTerminator = "[^\\r\\n\\u2028\\u2029]";

    /// <summary>
    ///     The options of the patterns that cspell compiles with the case-insensitive flag.
    /// </summary>
    private const RegexOptions Insensitive = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    ///     The options of the patterns that cspell compiles without the case-insensitive flag.
    /// </summary>
    private const RegexOptions Sensitive = RegexOptions.CultureInvariant;

    /// <summary>
    ///     The longest time, in milliseconds, that a pattern may spend on one text before matching is abandoned.
    /// </summary>
    private const int MatchTimeoutMilliseconds = PatternParser.MatchTimeoutMilliseconds;

    /// <summary>
    ///     The names of the patterns that are applied to every text, so that listing one changes nothing.
    /// </summary>
    /// <remarks>
    ///     The fourteen patterns of <see cref="DefaultPatterns"/>, and the directive patterns that
    ///     <see cref="DocumentDirectives"/> implements.
    /// </remarks>
    private static readonly string[] AlwaysAppliedNames =
    [
        "Urls", "Email", "RsaCert", "SshRsa", "Base64MultiLine", "Base64SingleLine", "CommitHash", "CommitHashLink",
        "CStyleHexValue", "CSSHexValue", "SHA", "HashStrings", "UnicodeRef", "UUID",
        "SpellCheckerDisable", "SpellCheckerDisableBlock", "SpellCheckerDisableLine", "SpellCheckerDisableNext",
        "SpellCheckerIgnoreInDocSetting",
    ];

    /// <summary>
    ///     The predefined patterns that exclude text only when listed, by name.
    /// </summary>
    private static readonly Dictionary<string, Regex> OptionalPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HexValues"] = HexValues(),
        ["PublicKey"] = PublicKey(),
        ["EscapeCharacters"] = EscapeCharacters(),
        ["Base64"] = Base64(),
        ["href"] = Href(),
        ["string"] = StringLiteral(),
        ["CStyleComment"] = CStyleComment(),
        ["PhpHereDoc"] = PhpHereDoc(),
        ["Everything"] = Everything(),
    };

    /// <summary>
    ///     The names of all predefined patterns, ignoring letter case, for the test of whether a name is predefined.
    /// </summary>
    private static readonly HashSet<string> PredefinedNameSet =
        new([.. AlwaysAppliedNames, .. OptionalPatterns.Keys], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the names of all predefined patterns.
    /// </summary>
    /// <remarks>
    ///     Exposed so that the documentation and the tests can state the complete list in one place.
    /// </remarks>
    /// <value>The twenty-eight names, in no particular order.</value>
    internal static IReadOnlyCollection<string> PredefinedNames => PredefinedNameSet;

    /// <summary>
    ///     Gets the regular expressions of the predefined patterns that exclude text only when listed.
    /// </summary>
    /// <remarks>
    ///     Exposed so that the time limit that bounds every one of them can be verified in one place.
    /// </remarks>
    /// <value>The generated regular expressions of the nine optional patterns.</value>
    internal static IReadOnlyCollection<Regex> OptionalRegexes => OptionalPatterns.Values;

    /// <summary>
    ///     Determines whether an entry is written in the shape of a pattern name rather than a pattern.
    /// </summary>
    /// <remarks>
    ///     A name starts with a letter and holds only letters, digits, underscores, and hyphens. An entry of
    ///     that shape is always read as a name, never as a literal word, so that a misspelled name is
    ///     reported instead of silently matching nothing. Surrounding white space is ignored. Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="entry">The entry. Must not be null.</param>
    /// <returns><see langword="true"/> when the entry has the shape of a name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    internal static bool IsNameShaped(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var text = entry.Trim();
        return text.Length > 0 &&
               char.IsAsciiLetter(text[0]) &&
               text.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    }

    /// <summary>
    ///     Determines whether a name is one of the predefined pattern names.
    /// </summary>
    /// <remarks>
    ///     The comparison ignores letter case. Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The name. Must not be null.</param>
    /// <returns><see langword="true"/> when the name is predefined.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    internal static bool IsPredefined(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PredefinedNameSet.Contains(name.Trim());
    }

    /// <summary>
    ///     Checks that a name can be given to a pattern of the <c>patterns</c> setting.
    /// </summary>
    /// <remarks>
    ///     The name must have the shape of a name, so that <c>ignoreRegExpList</c> can refer to it, and must
    ///     not be a predefined name. Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The name. Must not be null.</param>
    /// <returns>The reason the name cannot be used, as a sentence; null when it can be used.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    internal static string? CheckDefinitionName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!IsNameShaped(name) || name.Trim() != name)
        {
            return "a pattern name must start with a letter and hold only letters, digits, '_' and '-'.";
        }

        return IsPredefined(name)
            ? "it is the name of a predefined pattern; choose another name."
            : null;
    }

    /// <summary>
    ///     Checks that a name written in <c>ignoreRegExpList</c> refers to a pattern.
    /// </summary>
    /// <remarks>
    ///     A name is known when it is predefined or defined by the <c>patterns</c> setting. Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="name">The name. Must not be null.</param>
    /// <param name="definedNames">The names that the <c>patterns</c> setting defines, compared ignoring letter case. Must not be null.</param>
    /// <returns>The reason the name cannot be used, as a sentence; null when it can be used.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    internal static string? CheckReference(string name, IReadOnlySet<string> definedNames)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(definedNames);
        return IsPredefined(name) || definedNames.Contains(name.Trim())
            ? null
            : $"'{name.Trim()}' is not a defined or predefined pattern name; define it in 'patterns', " +
              "or write a regular expression between slashes, such as /text/, to match literal text.";
    }

    /// <summary>
    ///     Builds the regular expressions that the <c>ignoreRegExpList</c> and <c>patterns</c> settings describe.
    /// </summary>
    /// <remarks>
    ///     Every definition is checked, used or not, so a mistake is found where it is made. The result holds
    ///     one regular expression per pattern that excludes text, in the order of the list; a name that is
    ///     listed twice counts once, and a predefined name that is always applied contributes nothing. The
    ///     result is immutable. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ignoreRegExpList">The entries of <c>ignoreRegExpList</c>. Must not be null.</param>
    /// <param name="patterns">The definitions of <c>patterns</c>. Must not be null.</param>
    /// <returns>The regular expressions to run over each text; empty when the lists are empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidPatternException">
    ///     Thrown for the first definition or entry that cannot be used: an invalid or duplicate name, a name
    ///     that is not defined, or a pattern that cannot be parsed. The message names the entry and the reason.
    /// </exception>
    internal static ImmutableArray<Regex> Resolve(IReadOnlyList<string> ignoreRegExpList, IReadOnlyList<NamedPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(ignoreRegExpList);
        ArgumentNullException.ThrowIfNull(patterns);

        // Parse every definition, rejecting a bad or repeated name, so mistakes surface even for unused patterns
        var defined = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in patterns)
        {
            var nameProblem = CheckDefinitionName(definition.Name);
            if (nameProblem is not null)
            {
                throw new InvalidPatternException(definition.Name, nameProblem);
            }

            if (defined.ContainsKey(definition.Name))
            {
                throw new InvalidPatternException(definition.Name, "a pattern with this name is already defined.");
            }

            defined[definition.Name] = PatternParser.Parse(definition.Pattern);
        }

        // Resolve the list: names to their patterns, expressions parsed, each name used once
        var result = ImmutableArray.CreateBuilder<Regex>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definedNames = new HashSet<string>(defined.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ignoreRegExpList)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (!IsNameShaped(entry))
            {
                result.Add(PatternParser.Parse(entry));
                continue;
            }

            var reference = CheckReference(entry, definedNames);
            if (reference is not null)
            {
                throw new InvalidPatternException(entry, reference);
            }

            // A user pattern is looked up first, but it cannot clash with a predefined name
            var name = entry.Trim();
            if (!used.Add(name))
            {
                continue;
            }

            if (defined.TryGetValue(name, out var userPattern))
            {
                result.Add(userPattern);
            }
            else if (OptionalPatterns.TryGetValue(name, out var predefined))
            {
                result.Add(predefined);
            }
        }

        return result.ToImmutable();
    }

    /// <summary>
    ///     Appends the ranges of a text that match any of the given regular expressions to a list.
    /// </summary>
    /// <remarks>
    ///     The ranges are reported pattern by pattern, so they are neither sorted nor free of overlaps; the
    ///     caller merges them. A match with no characters excludes nothing and is skipped. The list is
    ///     appended to, never cleared. Each pattern is bounded by its match time limit. Thread-safe as long
    ///     as the list is not shared.
    /// </remarks>
    /// <param name="patterns">The regular expressions to run, from <see cref="Resolve"/> or <see cref="MarkdownPatterns"/>. Must not be default.</param>
    /// <param name="text">The text to search. Must not be null.</param>
    /// <param name="ranges">The list that receives the matching ranges. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="ranges"/> is null.</exception>
    /// <exception cref="RegexMatchTimeoutException">
    ///     Thrown when a pattern exceeds its time limit on this text. The ranges found so far remain in the list,
    ///     but the caller should treat the text as not checked.
    /// </exception>
    internal static void FindExcludedRanges(IEnumerable<Regex> patterns, string text, List<TextRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ranges);

        foreach (var pattern in patterns)
        {
            foreach (var match in pattern.EnumerateMatches(text))
            {
                // A pattern such as .* also matches the empty string at every position, which excludes nothing
                if (match.Length > 0)
                {
                    ranges.Add(new TextRange(match.Index, match.Length));
                }
            }
        }
    }

    /// <summary>
    ///     Matches a hexadecimal color, literal, or escape in any of the common forms.
    /// </summary>
    /// <remarks>
    ///     <c>#</c> and three to eight hexadecimal digits, <c>0x</c> and hexadecimal digits, <c>\u</c> and four,
    ///     or <c>\x{</c> and four and <c>}</c>.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?:#[0-9a-f]{3,8})|(?:0x[0-9a-f]+)|(?:\\\\u[0-9a-f]{4})|(?:\\\\x\\{[0-9a-f]{4}\\})", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex HexValues();

    /// <summary>
    ///     Matches a public key block, from its BEGIN line to the END line of the same kind.
    /// </summary>
    /// <remarks>
    ///     Case-sensitive, as in cspell. <c>\s</c> keeps the .NET meaning, which differs from JavaScript's
    ///     only for two characters that do not occur in keys.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("-{5}BEGIN\\s+((?:RSA\\s+)?PUBLIC\\s+KEY)[A-Za-z0-9_=+\\-/\\\\\\s]+?END\\s+\\1-{5}", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex PublicKey();

    /// <summary>
    ///     Matches the escape of a character: a backslash and one of <c>anrvtbf</c>, or <c>x</c> or <c>u</c> and hexadecimal digits.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("\\\\(?:[anrvtbf]|[xu][a-f0-9]+)", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex EscapeCharacters();

    /// <summary>
    ///     Matches Base64 data of at least 40 characters, continued over several lines when each line is that long.
    /// </summary>
    /// <remarks>
    ///     Case-sensitive, as in cspell. The line-start tests use the JavaScript meaning of a line start.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?<![A-Za-z0-9/+])(?:[A-Za-z0-9/+]{40,})(?:\\s" + LineStart + "\\s*[A-Za-z0-9/+]{40,})*" +
        "(?:\\s" + LineStart + "\\s*[A-Za-z0-9/+]+=*)?(?![A-Za-z0-9/+=])",
        Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Base64();

    /// <summary>
    ///     Matches an HTML <c>href</c> attribute with its quoted value.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(WordBoundary + "href\\s*=\\s*\"" + NotLineTerminator + "*?\"", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Href();

    /// <summary>
    ///     Matches a quoted string literal: single or double quotes on one line, or backticks over several lines,
    ///     with a backslash escaping a quote.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?:(['\"])" + NotLineTerminator + "*?(?<![^\\\\]\\\\(\\\\\\\\)*)\\1)|(?:`[\\s\\S]*?(?<![^\\\\]\\\\(\\\\\\\\)*)`)",
        Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex StringLiteral();

    /// <summary>
    ///     Matches a C-style comment: a line comment to the end of the line, or a block comment.
    /// </summary>
    /// <remarks>
    ///     A <c>//</c> directly after a word character and a colon, as in <c>http://</c>, is not a comment.
    /// </remarks>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("(?<![A-Za-z0-9_]:)(?://" + NotLineTerminator + "*)|(?:/\\*[\\s\\S]*?\\*/)", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex CStyleComment();

    /// <summary>
    ///     Matches a PHP here-document, from its <c>&lt;&lt;&lt;</c> marker to the line that ends with the same name.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex("<<<['\"]?([A-Za-z0-9_]+)['\"]?[\\s\\S]+?" + LineStart + "\\1;", Sensitive, MatchTimeoutMilliseconds)]
    private static partial Regex PhpHereDoc();

    /// <summary>
    ///     Matches every line of text, without its line terminator.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(NotLineTerminator + "*", Insensitive, MatchTimeoutMilliseconds)]
    private static partial Regex Everything();
}
