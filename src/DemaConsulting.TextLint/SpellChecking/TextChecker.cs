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
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     Checks the spelling of a string of text against the dictionaries and word lists of a settings value.
/// </summary>
/// <remarks>
///     This is the heart of the spell checker, usable without any file: an editor, a build step, or the
///     file checker passes text and gets every finding with its line and column. It combines the units of
///     the text scanning subsystem with the dictionaries and the settings the way cspell does. The text is
///     first cleared of what is never checked (URLs, email addresses, hexadecimal values, hashes and encoded
///     data, and the regions that directives switch off); what remains is split into words, and each word is
///     looked up: a word on the flagged list is reported; a word shorter than the minimum length is skipped;
///     a known word, or an acceptable form of one, passes; an unknown word is split at case changes
///     (<c>fooBar</c>) and each piece is checked on its own. Findings are returned in text order. A checker
///     validates its settings once, when it is created, and is then immutable and safe to use from any
///     number of threads at the same time; each call keeps its state on its own stack and heap, so
///     concurrent calls do not interfere.
/// </remarks>
public sealed class TextChecker
{
    /// <summary>
    ///     The word rules built from the settings, shared by every call.
    /// </summary>
    private readonly WordRules _rules;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TextChecker"/> class from settings.
    /// </summary>
    /// <remarks>
    ///     The settings are validated here, once, because settings built in code bypass the checks that the
    ///     settings file loader makes: the language must be supported and every entry of the word lists must
    ///     be able to match something. The checker does not keep the settings; the word lists are copied into
    ///     sets, so later changes to the caller's lists have no effect. The language's dictionaries are
    ///     loaded when the first word is looked up, not here.
    /// </remarks>
    /// <param name="settings">The settings to check with. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the language of <paramref name="settings"/> is not supported, or when an entry of
    ///     <c>words</c>, <c>ignoreWords</c>, or <c>flagWords</c> is blank, contains white space or a character
    ///     that cannot be part of a word, or has no letter or digit. The message names the setting and the entry.
    /// </exception>
    public TextChecker(TextLintSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Resolve the language first, so an unsupported language is reported before any list entry
        SpellingDictionary dictionary;
        try
        {
            dictionary = SpellingDictionary.ForLanguages(settings.Language);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                $"The language '{settings.Language}' in the settings is not supported. " +
                $"Supported languages: {string.Join(", ", SpellingDictionary.SupportedLanguages)}.",
                nameof(settings),
                exception);
        }

        // Interpret every list entry; an entry that could never match is an error, not silently ignored
        var accepted = new WordSet(InterpretEntries("words", settings.Words, false, nameof(settings)));
        var ignored = new WordSet(InterpretEntries("ignoreWords", settings.IgnoreWords, false, nameof(settings)));
        var flagged = new FlagSet(InterpretEntries("flagWords", settings.FlagWords, true, nameof(settings)));
        _rules = new WordRules(dictionary, settings.MinWordLength, ignored, accepted, flagged);
    }

    /// <summary>
    ///     Checks the spelling of a text and returns every finding in text order.
    /// </summary>
    /// <remarks>
    ///     Never reads or writes a file. The first call for a language loads its dictionaries, which takes a
    ///     few milliseconds. The text is not modified. The cost is proportional to the length of the text
    ///     except for pathological input, which is bounded by a match timeout. Thread-safe.
    /// </remarks>
    /// <param name="text">The text to check. Must not be null; an empty text has no findings.</param>
    /// <param name="sourceName">
    ///     The name to put on each finding, normally the path of the file the text came from; may be null.
    /// </param>
    /// <returns>The findings ordered by position; empty when the text is correct. Never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="RegexMatchTimeoutException">
    ///     Thrown when one of the ignore patterns takes longer than its time limit on this text, which only
    ///     hostile input can cause; the text should then be treated as not checked.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when an embedded dictionary is missing or damaged, which indicates a defective build of the library.
    /// </exception>
    public IReadOnlyList<SpellingIssue> Check(string text, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return [];
        }

        // Find the directives and the text that is never checked, and sort the excluded ranges by position
        var directives = DocumentDirectives.Scan(text);
        var excluded = new List<TextRange>(directives.ExcludedRanges);
        DefaultPatterns.FindExcludedRanges(text, excluded);
        excluded.Sort(static (first, second) => first.Start.CompareTo(second.Start));

        // Words added by directives apply to this text only, so they are layered over the shared rules
        var rules = directives.IgnoredWords.IsEmpty && directives.Words.IsEmpty
            ? _rules
            : _rules.WithDocumentWords(directives.IgnoredWords, directives.Words);

        // Check the stretches of text between the excluded ranges
        var run = new CheckRun(text, rules);
        var position = 0;
        foreach (var range in excluded)
        {
            if (range.Start > position)
            {
                run.CheckSegment(position, range.Start);
            }

            position = Math.Max(position, range.End);
        }

        if (position < text.Length)
        {
            run.CheckSegment(position, text.Length);
        }

        // Report the directives that have no effect, then put every finding in text order
        foreach (var directive in directives.UnsupportedDirectives)
        {
            run.Add(directive, SpellingIssueKind.UnsupportedDirective, []);
        }

        return ToIssues(text, sourceName, run.Findings, !directives.UnsupportedDirectives.IsEmpty);
    }

    /// <summary>
    ///     Interprets the entries of one word list, rejecting any that could never match.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="settingName">The name of the setting, used in the message. Must not be null.</param>
    /// <param name="entries">The raw entries. Must not be null.</param>
    /// <param name="allowSuggestions"><see langword="true"/> to accept the suggestion forms of flagged words.</param>
    /// <param name="parameterName">The name of the argument to report in the exception. Must not be null.</param>
    /// <returns>The interpreted entries in order.</returns>
    /// <exception cref="ArgumentException">Thrown when an entry cannot be used; the message names the setting and the entry.</exception>
    private static List<ListEntry> InterpretEntries(
        string settingName,
        IReadOnlyList<string> entries,
        bool allowSuggestions,
        string parameterName)
    {
        var interpreted = new List<ListEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (!WordTokenizer.TryParseListEntry(entry, allowSuggestions, out var result, out var problem))
            {
                throw new ArgumentException($"Cannot use the '{settingName}' entry '{entry}': {problem}", parameterName);
            }

            interpreted.Add(result);
        }

        return interpreted;
    }

    /// <summary>
    ///     Converts findings, which carry offsets, into issues that carry lines and columns.
    /// </summary>
    /// <remarks>
    ///     Findings are produced in text order except that unsupported directives are appended last, so the
    ///     list is sorted (stably) only when it contains one. Lines are counted by line feeds alone and
    ///     columns in UTF-16 code units, as cspell does; the conversion walks the text once because the
    ///     findings are in order. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The checked text. Must not be null.</param>
    /// <param name="sourceName">The name to put on each issue; may be null.</param>
    /// <param name="findings">The findings. Must not be null.</param>
    /// <param name="needsSort"><see langword="true"/> when the findings are not already in text order.</param>
    /// <returns>The issues in text order.</returns>
    private static ImmutableArray<SpellingIssue> ToIssues(string text, string? sourceName, List<Finding> findings, bool needsSort)
    {
        IEnumerable<Finding> ordered = needsSort ? findings.OrderBy(static finding => finding.Start) : findings;
        var issues = ImmutableArray.CreateBuilder<SpellingIssue>(findings.Count);

        // Advance a line counter through the text as the findings move forward
        var line = 1;
        var lineStart = 0;
        var scanned = 0;
        foreach (var finding in ordered)
        {
            var skipped = text.AsSpan(scanned, finding.Start - scanned);
            var lineFeeds = skipped.Count('\n');
            if (lineFeeds > 0)
            {
                line += lineFeeds;
                lineStart = scanned + skipped.LastIndexOf('\n') + 1;
            }

            scanned = finding.Start;
            issues.Add(new SpellingIssue(
                sourceName,
                line,
                finding.Start - lineStart + 1,
                text.Substring(finding.Start, finding.Length),
                finding.Kind,
                finding.Suggestions.IsDefault ? [] : finding.Suggestions));
        }

        return issues.MoveToImmutable();
    }

    /// <summary>
    ///     A finding before its offset is converted to a line and column.
    /// </summary>
    /// <param name="Start">The offset of the finding in the text.</param>
    /// <param name="Length">The length of the finding in UTF-16 code units.</param>
    /// <param name="Kind">The kind of finding.</param>
    /// <param name="Suggestions">The suggestions; the default array when there are none.</param>
    private readonly record struct Finding(int Start, int Length, SpellingIssueKind Kind, ImmutableArray<string> Suggestions);

    /// <summary>
    ///     The state of one call to <see cref="Check"/>: the text, the rules in force, and the findings so far.
    /// </summary>
    /// <remarks>
    ///     Keeping the state in one object per call is what makes the checker safe for concurrent use: nothing
    ///     is shared between calls except the immutable rules. The scratch lists are reused across the
    ///     segments, tokens, and words of the call to avoid allocating for each. Not thread-safe; it never
    ///     leaves the call that created it.
    /// </remarks>
    private sealed class CheckRun
    {
        /// <summary>
        ///     The checked text.
        /// </summary>
        private readonly string _text;

        /// <summary>
        ///     The word rules in force for this text.
        /// </summary>
        private readonly WordRules _rules;

        /// <summary>
        ///     The tokens of the segment being checked.
        /// </summary>
        private readonly List<TextRange> _tokens = [];

        /// <summary>
        ///     The words of the token being checked.
        /// </summary>
        private readonly List<TextRange> _words = [];

        /// <summary>
        ///     The pieces of the word being checked.
        /// </summary>
        private readonly List<TextRange> _pieces = [];

        /// <summary>
        ///     Initializes a new instance of the <see cref="CheckRun"/> class.
        /// </summary>
        /// <remarks>
        ///     Cheap: it allocates only the empty scratch lists.
        /// </remarks>
        /// <param name="text">The text to check. Must not be null.</param>
        /// <param name="rules">The word rules in force. Must not be null.</param>
        internal CheckRun(string text, WordRules rules)
        {
            _text = text;
            _rules = rules;
        }

        /// <summary>
        ///     Gets the findings recorded so far.
        /// </summary>
        /// <value>The findings, in the order they were recorded.</value>
        internal List<Finding> Findings { get; } = [];

        /// <summary>
        ///     Records a finding.
        /// </summary>
        /// <remarks>
        ///     Side effect: appends to <see cref="Findings"/>.
        /// </remarks>
        /// <param name="range">The range of the finding in the text.</param>
        /// <param name="kind">The kind of finding.</param>
        /// <param name="suggestions">The suggestions; empty when there are none.</param>
        internal void Add(TextRange range, SpellingIssueKind kind, ImmutableArray<string> suggestions) =>
            Findings.Add(new Finding(range.Start, range.Length, kind, suggestions));

        /// <summary>
        ///     Checks one stretch of text that no excluded range touches.
        /// </summary>
        /// <remarks>
        ///     The stretch is scanned as if it were the whole text, so a word is cut where an excluded range
        ///     begins or ends. Side effect: records findings.
        /// </remarks>
        /// <param name="start">The offset of the first code unit of the stretch.</param>
        /// <param name="end">The offset just past the last code unit of the stretch.</param>
        internal void CheckSegment(int start, int end)
        {
            _tokens.Clear();
            WordTokenizer.FindTokens(_text, start, end, _tokens);
            foreach (var token in _tokens)
            {
                CheckToken(token);
            }
        }

        /// <summary>
        ///     Checks one token: first against the list entries that contain digits or punctuation, then word by word.
        /// </summary>
        /// <remarks>
        ///     The first step is skipped altogether when no list has such an entry. A token that is ignored or
        ///     accepted as a whole is not checked further; a token that is flagged as a whole is reported as one.
        ///     Side effect: records findings.
        /// </remarks>
        /// <param name="token">The range of the token.</param>
        private void CheckToken(TextRange token)
        {
            if (_rules.HasTokenEntries)
            {
                var trimmed = WordTokenizer.TrimToken(_text, token);
                if (!trimmed.IsEmpty)
                {
                    var span = _text.AsSpan(trimmed.Start, trimmed.Length);
                    if (_rules.IsTokenIgnored(span))
                    {
                        return;
                    }

                    if (_rules.IsTokenFlagged(span, out var suggestions))
                    {
                        Add(trimmed, SpellingIssueKind.FlaggedWord, suggestions);
                        return;
                    }

                    if (_rules.IsTokenAccepted(span))
                    {
                        return;
                    }
                }
            }

            // Otherwise check each word of the token
            _words.Clear();
            WordTokenizer.FindWords(_text, token, _words);
            foreach (var word in _words)
            {
                CheckWord(word);
            }
        }

        /// <summary>
        ///     Checks one word, and its pieces when it is unknown.
        /// </summary>
        /// <remarks>
        ///     A flagged word is reported whatever its length. A word that is too short is skipped. A known
        ///     word, or an acceptable form of one, passes as a whole before any splitting, so identifiers made
        ///     of known words are never taken apart. Only an unknown word is split at case changes, and each
        ///     piece that is flagged, or is long enough and unknown, is reported, except a piece that is one
        ///     repeated letter. Side effect: records findings.
        /// </remarks>
        /// <param name="word">The range of the word.</param>
        private void CheckWord(TextRange word)
        {
            var span = _text.AsSpan(word.Start, word.Length);
            if (_rules.IsFlagged(span, out var wordSuggestions))
            {
                Add(word, SpellingIssueKind.FlaggedWord, wordSuggestions);
                return;
            }

            // Short words are skipped, and a whole word that is acceptable needs no further work
            var followsBackslash = word.Start > 0 && _text[word.Start - 1] == '\\';
            if (word.Length < _rules.MinWordLength ||
                _rules.IsAccepted(span, followsBackslash) ||
                _rules.IsCapitalRunWithAcceptedEnding(span, followsBackslash))
            {
                return;
            }

            // Split the unknown word at case changes and check each piece
            _pieces.Clear();
            WordTokenizer.SplitCase(_text, word, _pieces);
            foreach (var piece in _pieces)
            {
                CheckPiece(piece, followsBackslash && piece.Start == word.Start);
            }
        }

        /// <summary>
        ///     Checks one piece of an unknown word.
        /// </summary>
        /// <remarks>
        ///     Side effect: records a finding when the piece is flagged, or is long enough, unknown, and not a
        ///     repeated letter.
        /// </remarks>
        /// <param name="piece">The range of the piece.</param>
        /// <param name="followsBackslash"><see langword="true"/> when a backslash directly precedes the piece.</param>
        private void CheckPiece(TextRange piece, bool followsBackslash)
        {
            var span = _text.AsSpan(piece.Start, piece.Length);
            var isFlagged = _rules.IsFlagged(span, out var suggestions);
            if (!isFlagged && (piece.Length < _rules.MinWordLength || _rules.IsAccepted(span, followsBackslash)))
            {
                return;
            }

            // A run of one repeated letter is a placeholder, not a misspelling, even when flagged
            if (WordRules.IsRepeatedLetter(span))
            {
                return;
            }

            Add(piece, isFlagged ? SpellingIssueKind.FlaggedWord : SpellingIssueKind.UnknownWord, suggestions);
        }
    }
}
