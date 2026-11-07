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

// cspell:ignore zorbs zorb zorbed ings ning

using System.Collections.Immutable;
using System.Globalization;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     Decides, for one word, whether it is flagged, known, or acceptable for one of the reasons that
///     cspell allows, using the dictionaries and the word lists in force.
/// </summary>
/// <remarks>
///     This holds the word-level rules so that <see cref="TextChecker"/> only has to walk the text. The
///     precedence is the one cspell uses: an ignored word is never reported; a flagged word is always
///     reported unless it is ignored; otherwise a word is accepted when it is in the accepted-word list or
///     the dictionaries. On top of that, a word is accepted when it is the possessive of an accepted word
///     (<c>parser's</c>), when it follows a backslash and is accepted without its first letter (the <c>n</c>
///     of <c>\nfoo</c>), when it is a run of capitals with a plural or verb ending whose capitals are accepted
///     or too short to check (<c>URLs</c>, <c>ZORBs</c> when <c>ZORB</c> is short), and a piece that is one
///     repeated letter (<c>xxxx</c>) is never reported. An instance is created for the settings and can be
///     layered with the words that a document adds, without copying the settings' lists. Instances are
///     immutable and safe for concurrent use.
/// </remarks>
internal sealed class WordRules
{
    /// <summary>
    ///     The ending that makes a word the possessive of another, with the straight apostrophe.
    /// </summary>
    private const string PossessiveEnding = "'s";

    /// <summary>
    ///     The ending that makes a word the possessive of another, with the typographic apostrophe.
    /// </summary>
    private const string TypographicPossessiveEnding = "\u2019s";

    /// <summary>
    ///     The apostrophes that may separate the capitals of a run from its ending.
    /// </summary>
    private static readonly char[] ApostropheCharacters = ['\'', '\u2019'];

    /// <summary>
    ///     The endings that may follow a run of capitals without making it an unknown word, as cspell lists them.
    /// </summary>
    private static readonly string[] CapitalRunEndings = ["s", "ing", "ies", "es", "ings", "ize", "ed", "ning"];

    /// <summary>
    ///     The smallest number of capitals in a run that may carry an ending.
    /// </summary>
    private const int MinimumCapitalRun = 2;

    /// <summary>
    ///     The smallest number of the same letter in a row that makes a piece a repeated-letter run.
    /// </summary>
    private const int MinimumRepeatedLetters = 4;

    /// <summary>
    ///     The dictionaries of the language selection.
    /// </summary>
    private readonly SpellingDictionary _dictionary;

    /// <summary>
    ///     The words that are never reported.
    /// </summary>
    private readonly WordSet _ignore;

    /// <summary>
    ///     The words that are accepted.
    /// </summary>
    private readonly WordSet _accept;

    /// <summary>
    ///     The words that are always reported.
    /// </summary>
    private readonly FlagSet _flags;

    /// <summary>
    ///     The rules this instance adds its words to, or null for the rules of the settings themselves.
    /// </summary>
    private readonly WordRules? _parent;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordRules"/> class for the settings.
    /// </summary>
    /// <remarks>
    ///     Does not validate the entries: the caller interprets them first, so it can report a bad one with
    ///     the name of the setting it came from.
    /// </remarks>
    /// <param name="dictionary">The dictionaries of the language selection. Must not be null.</param>
    /// <param name="minWordLength">The minimum length of a word that is checked. Must be at least 1.</param>
    /// <param name="ignore">The words that are never reported. Must not be null.</param>
    /// <param name="accept">The words that are accepted. Must not be null.</param>
    /// <param name="flags">The words that are always reported. Must not be null.</param>
    internal WordRules(SpellingDictionary dictionary, int minWordLength, WordSet ignore, WordSet accept, FlagSet flags)
    {
        _dictionary = dictionary;
        MinWordLength = minWordLength;
        _ignore = ignore;
        _accept = accept;
        _flags = flags;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordRules"/> class that adds document words to other rules.
    /// </summary>
    /// <remarks>
    ///     Shares everything but the two word sets with the parent, so a document with a directive does not
    ///     copy the project's word lists.
    /// </remarks>
    /// <param name="parent">The rules to add to. Must not be null.</param>
    /// <param name="ignore">The words the document ignores. Must not be null.</param>
    /// <param name="accept">The words the document accepts. Must not be null.</param>
    private WordRules(WordRules parent, WordSet ignore, WordSet accept)
    {
        _dictionary = parent._dictionary;
        MinWordLength = parent.MinWordLength;
        _flags = parent._flags;
        _parent = parent;
        _ignore = ignore;
        _accept = accept;
    }

    /// <summary>
    ///     Gets the minimum length of a word or piece that is checked, in UTF-16 code units.
    /// </summary>
    /// <value>A length of at least 1.</value>
    internal int MinWordLength { get; }

    /// <summary>
    ///     Gets a value indicating whether any list holds an entry that contains digits or punctuation.
    /// </summary>
    /// <remarks>
    ///     Lets the checker skip token matching when no such entry exists, which is the common case.
    /// </remarks>
    /// <value><see langword="true"/> when whole tokens have to be matched against the lists.</value>
    internal bool HasTokenEntries =>
        _ignore.HasTokens || _accept.HasTokens || _flags.HasTokens || (_parent?.HasTokenEntries ?? false);

    /// <summary>
    ///     Creates rules that also use the words that a document adds.
    /// </summary>
    /// <remarks>
    ///     Entries that cannot match anything are skipped, because a directive in a comment has no position
    ///     in the settings at which to report them. Thread-safe.
    /// </remarks>
    /// <param name="ignoredWords">The words from <c>ignore</c> directives. Must not be null.</param>
    /// <param name="acceptedWords">The words from <c>words</c> directives. Must not be null.</param>
    /// <returns>The layered rules.</returns>
    internal WordRules WithDocumentWords(IEnumerable<string> ignoredWords, IEnumerable<string> acceptedWords) =>
        new(this, new WordSet(Interpret(ignoredWords)), new WordSet(Interpret(acceptedWords)));

    /// <summary>
    ///     Determines whether a word is flagged and so must be reported.
    /// </summary>
    /// <remarks>
    ///     A word that is also ignored is not flagged. Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <param name="suggestions">Receives the suggestions of the flag entry; empty when not flagged or none were given.</param>
    /// <returns><see langword="true"/> when the word is flagged and not ignored.</returns>
    internal bool IsFlagged(ReadOnlySpan<char> word, out ImmutableArray<string> suggestions)
    {
        suggestions = [];
        if (_flags.IsEmpty || !_flags.TryMatchWord(word, out var matched) || IsIgnored(word))
        {
            return false;
        }

        suggestions = matched;
        return true;
    }

    /// <summary>
    ///     Determines whether a token that contains digits or punctuation is flagged.
    /// </summary>
    /// <remarks>
    ///     A token that is also ignored is not flagged. Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="token">The token to test, without edge punctuation.</param>
    /// <param name="suggestions">Receives the suggestions of the flag entry; empty when not flagged or none were given.</param>
    /// <returns><see langword="true"/> when the token is flagged and not ignored.</returns>
    internal bool IsTokenFlagged(ReadOnlySpan<char> token, out ImmutableArray<string> suggestions)
    {
        suggestions = [];
        if (!_flags.HasTokens || !_flags.TryMatchToken(token, out var matched) || IsTokenIgnored(token))
        {
            return false;
        }

        suggestions = matched;
        return true;
    }

    /// <summary>
    ///     Determines whether a token that contains digits or punctuation is ignored.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="token">The token to test, without edge punctuation.</param>
    /// <returns><see langword="true"/> when an ignore list holds the token.</returns>
    internal bool IsTokenIgnored(ReadOnlySpan<char> token) =>
        _ignore.ContainsToken(token) || (_parent?.IsTokenIgnored(token) ?? false);

    /// <summary>
    ///     Determines whether a token that contains digits or punctuation is accepted.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="token">The token to test, without edge punctuation.</param>
    /// <returns><see langword="true"/> when an accepted-word list holds the token.</returns>
    internal bool IsTokenAccepted(ReadOnlySpan<char> token) =>
        _accept.ContainsToken(token) || (_parent?.IsTokenAccepted(token) ?? false);

    /// <summary>
    ///     Determines whether a word is acceptable as written, or after the allowances for an escape and a possessive.
    /// </summary>
    /// <remarks>
    ///     A backslash inside the word is removed first, because it can only be the escape of an apostrophe.
    ///     When the word follows a backslash and is not accepted, it is retried without its first letter, so
    ///     the <c>n</c> of the escape <c>\nfoo</c> does not make <c>nfoo</c> a misspelling of <c>foo</c>. Does
    ///     not allocate unless the word contains a backslash. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test. Must not be empty.</param>
    /// <param name="followsBackslash"><see langword="true"/> when the character before the word is a backslash.</param>
    /// <returns><see langword="true"/> when the word is acceptable.</returns>
    internal bool IsAccepted(ReadOnlySpan<char> word, bool followsBackslash)
    {
        if (IsAcceptedIgnoringEscapes(word))
        {
            return true;
        }

        // Retry without the first character, which may be the letter of an escape such as the n of \n
        if (!followsBackslash)
        {
            return false;
        }

        var first = char.IsHighSurrogate(word[0]) && word.Length > 1 ? 2 : 1;
        return word.Length > first && IsAcceptedIgnoringEscapes(word[first..]);
    }

    /// <summary>
    ///     Determines whether a word is a run of capitals with an ending whose capitals are acceptable.
    /// </summary>
    /// <remarks>
    ///     Acronyms are written with a lower-case ending (<c>URLs</c>, <c>ZORBed</c>). The word is accepted
    ///     when the capitals are accepted, or are too short to be checked (shorter than the minimum word
    ///     length by cspell's measure); a flagged run of capitals is not accepted. The ending may be preceded
    ///     by an apostrophe. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <param name="followsBackslash"><see langword="true"/> when the character before the word is a backslash.</param>
    /// <returns><see langword="true"/> when the word has this form and its capitals are acceptable.</returns>
    internal bool IsCapitalRunWithAcceptedEnding(ReadOnlySpan<char> word, bool followsBackslash)
    {
        // Measure the run of capitals (and the combining marks that belong to them) at the start of the word
        var capitals = MeasureCapitalRun(word, out var capitalCount);
        if (capitalCount < MinimumCapitalRun || capitals >= word.Length)
        {
            return false;
        }

        // The rest must be an ending, optionally after an apostrophe
        var ending = word[capitals..];
        if (ApostropheCharacters.Contains(ending[0]))
        {
            ending = ending[1..];
        }

        if (!IsCapitalRunEnding(ending))
        {
            return false;
        }

        // The capitals must not be flagged, and must be known or too short to be worth checking
        var capitalRun = word[..capitals];
        if (IsFlagged(capitalRun, out _))
        {
            return false;
        }

        return IsAccepted(capitalRun, followsBackslash) || IsTooShort(capitalRun);
    }

    /// <summary>
    ///     Determines whether a word with apostrophes is a contraction whose parts are all acceptable.
    /// </summary>
    /// <remarks>
    ///     Contractions such as <c>exec'd</c>, <c>OR'd</c>, and <c>rock'n'roll</c> are written with an
    ///     apostrophe that joins ordinary words to a letter or two. cspell accepts such a word when every
    ///     part between the apostrophes is acceptable on its own, and a part is acceptable when it is known,
    ///     is a run of capitals with an ending whose capitals are acceptable, or is too short to be checked
    ///     (the <c>d</c> of <c>exec'd</c>); a part that is flagged never is. When some part is not acceptable
    ///     the word stays unknown and is reported whole, as before. A backslash that escapes an apostrophe
    ///     is not part of the part before it, and only the first part can follow the backslash that precedes
    ///     the word. Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test, which may contain apostrophes (straight or typographic).</param>
    /// <param name="followsBackslash"><see langword="true"/> when the character before the word is a backslash.</param>
    /// <returns><see langword="true"/> when the word has at least one apostrophe and every part is acceptable.</returns>
    internal bool IsContractionAccepted(ReadOnlySpan<char> word, bool followsBackslash)
    {
        // A word without an apostrophe is not a contraction
        if (word.IndexOfAny(ApostropheCharacters) < 0)
        {
            return false;
        }

        // Test each part between apostrophes; one unacceptable part leaves the word unknown
        var isFirst = true;
        var remaining = word;
        while (!remaining.IsEmpty)
        {
            var apostrophe = remaining.IndexOfAny(ApostropheCharacters);
            var part = apostrophe < 0 ? remaining : remaining[..apostrophe];
            remaining = apostrophe < 0 ? default : remaining[(apostrophe + 1)..];

            // The backslash that escapes the apostrophe belongs to the escape, not to the part before it
            if (!part.IsEmpty && part[^1] == '\\')
            {
                part = part[..^1];
            }

            if (!part.IsEmpty && !IsAcceptablePart(part, isFirst && followsBackslash))
            {
                return false;
            }

            isFirst = false;
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a piece of a word is one letter repeated four or more times.
    /// </summary>
    /// <remarks>
    ///     Strings such as <c>xxxx</c> or <c>zzzzz</c> are placeholders, not misspellings, and cspell never
    ///     reports them when a word is split into pieces. Only ASCII letters count, as in cspell, and the
    ///     comparison ignores letter case. Stateless and thread-safe.
    /// </remarks>
    /// <param name="piece">The piece to test.</param>
    /// <returns><see langword="true"/> when the piece is a run of one repeated ASCII letter.</returns>
    internal static bool IsRepeatedLetter(ReadOnlySpan<char> piece)
    {
        if (piece.Length < MinimumRepeatedLetters || !char.IsAsciiLetter(piece[0]))
        {
            return false;
        }

        var first = char.ToLowerInvariant(piece[0]);
        foreach (var character in piece[1..])
        {
            if (char.ToLowerInvariant(character) != first)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether one part of a contraction is acceptable on its own.
    /// </summary>
    /// <remarks>
    ///     Does not allocate unless the part contains a backslash. Thread-safe.
    /// </remarks>
    /// <param name="part">The part between apostrophes. Must not be empty.</param>
    /// <param name="followsBackslash"><see langword="true"/> when a backslash directly precedes the part.</param>
    /// <returns><see langword="true"/> when the part is not flagged and is known, or too short to check.</returns>
    private bool IsAcceptablePart(ReadOnlySpan<char> part, bool followsBackslash) =>
        !IsFlagged(part, out _) &&
        (IsTooShort(part) || IsAccepted(part, followsBackslash) || IsCapitalRunWithAcceptedEnding(part, followsBackslash));

    /// <summary>
    ///     Interprets the entries of a document directive, skipping those that cannot match anything.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="words">The raw entries. Must not be null.</param>
    /// <returns>The usable entries in order.</returns>
    private static IEnumerable<ListEntry> Interpret(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            if (WordTokenizer.TryParseListEntry(word, false, out var entry, out _))
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    ///     Determines whether a word is ignored.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <returns><see langword="true"/> when an ignore list holds the word.</returns>
    private bool IsIgnored(ReadOnlySpan<char> word) => _ignore.ContainsWord(word) || (_parent?.IsIgnored(word) ?? false);

    /// <summary>
    ///     Determines whether an accepted-word list holds a word.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <returns><see langword="true"/> when an accepted-word list holds the word.</returns>
    private bool IsListed(ReadOnlySpan<char> word) => _accept.ContainsWord(word) || (_parent?.IsListed(word) ?? false);

    /// <summary>
    ///     Determines whether a word is acceptable once escapes are removed.
    /// </summary>
    /// <remarks>
    ///     Allocates only when the word contains a backslash. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test. Must not be empty.</param>
    /// <returns><see langword="true"/> when the word, without backslashes, is acceptable.</returns>
    private bool IsAcceptedIgnoringEscapes(ReadOnlySpan<char> word) =>
        word.Contains('\\') ? IsKnown(word.ToString().Replace("\\", string.Empty, StringComparison.Ordinal)) : IsKnown(word);

    /// <summary>
    ///     Determines whether a word is known: in a dictionary or a word list, or the possessive of such a word.
    /// </summary>
    /// <remarks>
    ///     The dictionaries are asked first because almost every word of a text is in them. Does not allocate.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <returns><see langword="true"/> when the word is known.</returns>
    private bool IsKnown(ReadOnlySpan<char> word)
    {
        if (IsKnownAsIs(word))
        {
            return true;
        }

        // A possessive is known when the word it is made from is known
        var hasEnding = word.EndsWith(PossessiveEnding, StringComparison.Ordinal) ||
                        word.EndsWith(TypographicPossessiveEnding, StringComparison.Ordinal);
        return hasEnding && word.Length > PossessiveEnding.Length && IsKnownAsIs(word[..^PossessiveEnding.Length]);
    }

    /// <summary>
    ///     Determines whether a word is in a dictionary, the ignored words, or the accepted words.
    /// </summary>
    /// <remarks>
    ///     Does not allocate. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to test.</param>
    /// <returns><see langword="true"/> when some list or dictionary holds the word.</returns>
    private bool IsKnownAsIs(ReadOnlySpan<char> word) => _dictionary.IsKnown(word) || IsListed(word) || IsIgnored(word);

    /// <summary>
    ///     Measures the run of capital letters at the start of a word.
    /// </summary>
    /// <remarks>
    ///     Combining marks that follow a capital belong to the run. Stateless and thread-safe.
    /// </remarks>
    /// <param name="word">The word to measure.</param>
    /// <param name="capitalCount">Receives the number of characters in the run, marks included.</param>
    /// <returns>The length of the run in UTF-16 code units.</returns>
    private static int MeasureCapitalRun(ReadOnlySpan<char> word, out int capitalCount)
    {
        capitalCount = 0;
        var index = 0;
        while (index < word.Length)
        {
            // Decode one character, which is one or two code units
            var isPair = char.IsHighSurrogate(word[index]) && index + 1 < word.Length && char.IsLowSurrogate(word[index + 1]);
            var length = isPair ? 2 : 1;
            var codePoint = isPair ? char.ConvertToUtf32(word[index], word[index + 1]) : word[index];
            var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
            var isMark = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            if (category != UnicodeCategory.UppercaseLetter && !isMark)
            {
                break;
            }

            index += length;
            capitalCount++;
        }

        return index;
    }

    /// <summary>
    ///     Determines whether a text is one of the endings that may follow a run of capitals.
    /// </summary>
    /// <remarks>
    ///     Matching is case-sensitive: the ending is written in lower case. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ending">The text after the capitals and any apostrophe.</param>
    /// <returns><see langword="true"/> when the text is exactly one of the endings.</returns>
    private static bool IsCapitalRunEnding(ReadOnlySpan<char> ending)
    {
        foreach (var candidate in CapitalRunEndings)
        {
            if (ending.SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a run of capitals is too short to be checked.
    /// </summary>
    /// <remarks>
    ///     cspell treats text as long enough when it has at least the minimum number of characters, or at
    ///     least twice the minimum number of UTF-16 code units, so a run with astral characters is measured
    ///     both ways. Stateless and thread-safe.
    /// </remarks>
    /// <param name="capitals">The run of capitals.</param>
    /// <returns><see langword="true"/> when the run is shorter than the minimum by both measures.</returns>
    private bool IsTooShort(ReadOnlySpan<char> capitals) =>
        capitals.Length < MinWordLength * 2 && WordTokenizer.CountCharacters(capitals) < MinWordLength;
}
