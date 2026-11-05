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

// cspell:ignore ings ning titlecase nonspacing halfwidth jamo zorb

using System.Globalization;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Finds the tokens, words, and case-change pieces in text, and interprets the entries of word lists.
/// </summary>
/// <remarks>
///     This is the one place that decides what a word is, so the checker, the settings loader, and the
///     document directives all agree. The rules follow cspell's documented behavior (see the verification
///     of the spell-check engine for the corpus that proves it):
///     <list type="bullet">
///         <item>
///             A <em>token</em> is a maximal run of letters, ASCII digits, and the characters
///             <c>_ ' ’ ` . + -</c>. A backslash directly before a straight apostrophe is part of the token.
///             Tokens are what word-list entries that contain digits or punctuation are matched against.
///         </item>
///         <item>
///             A <em>word</em> is a run of letters, each optionally followed by one combining mark, in which a
///             straight or typographic apostrophe (optionally preceded by a backslash) is allowed between two
///             letters. Digits, underscores, hyphens, dots and everything else end a word. Han, Hiragana,
///             Katakana, and Hangul characters are never part of a word.
///         </item>
///         <item>
///             A <em>piece</em> is part of a word split at case changes: before an upper-case letter that
///             follows a lower-case letter (<c>fooBar</c>), and before the last capital of a run of capitals
///             that is followed by a lower-case letter (<c>XMLParser</c>), except that a capital that starts
///             an English suffix (<c>s ing ies es ings ed ning</c>) not followed by a lower-case letter does not
///             split (<c>APIs</c> stays whole).
///         </item>
///     </list>
///     The scanner works on <see cref="string"/> offsets and never allocates per character. It handles
///     surrogate pairs as single characters and treats a lone surrogate as a non-letter, so no input can
///     make it throw or loop. All methods are stateless and thread-safe; the methods that fill a caller's
///     list change only that list.
/// </remarks>
internal static class WordTokenizer
{
    /// <summary>
    ///     The character that stands for any code point that cannot be decoded.
    /// </summary>
    private const int ReplacementCodePoint = 0xFFFD;

    /// <summary>
    ///     The highest code point of the ASCII range, which is decoded without any lookup.
    /// </summary>
    private const int MaxAscii = 0x7F;

    /// <summary>
    ///     The straight apostrophe.
    /// </summary>
    private const char Apostrophe = '\'';

    /// <summary>
    ///     The typographic (curly) right single quotation mark, treated as an apostrophe.
    /// </summary>
    private const char TypographicApostrophe = '\u2019';

    /// <summary>
    ///     The escape character that may precede an apostrophe inside a word.
    /// </summary>
    private const char Backslash = '\\';

    /// <summary>
    ///     The separator between a flagged word and its suggestions in the arrow form of a list entry.
    /// </summary>
    private const string SuggestionArrow = "->";

    /// <summary>
    ///     The separator between a flagged word and its suggestion in the colon form of a list entry.
    /// </summary>
    private const char SuggestionColon = ':';

    /// <summary>
    ///     The separator between several suggestions in the arrow form of a list entry.
    /// </summary>
    private const char SuggestionSeparator = ',';

    /// <summary>
    ///     The prefix of cspell word-list entries that marks a forbidden word, which is not supported.
    /// </summary>
    private const char ForbiddenMarker = '!';

    /// <summary>
    ///     The English suffixes after which a capital letter does not split a word, in the order cspell lists them.
    /// </summary>
    private static readonly string[] EnglishSuffixes = ["s", "ing", "ies", "es", "ings", "ed", "ning"];

    /// <summary>
    ///     Appends the tokens found in a part of a text to a list.
    /// </summary>
    /// <remarks>
    ///     Tokens are the unit that word-list entries with digits or punctuation (<c>k8s</c>, <c>foo-bar</c>)
    ///     are matched against, and the unit within which words are found. The scan never reads outside
    ///     the given part, so a caller can scan the pieces of a text that remain after excluded ranges are
    ///     removed. The list is appended to, never cleared. Thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The text to scan. Must not be null.</param>
    /// <param name="start">The offset of the first code unit to scan. Must be within the text.</param>
    /// <param name="end">The offset just past the last code unit to scan. Must not be before <paramref name="start"/>
    ///     and not past the end of the text.</param>
    /// <param name="tokens">The list that receives the tokens in order. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="tokens"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the part is not inside the text.</exception>
    internal static void FindTokens(string text, int start, int end, List<TextRange> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ValidatePart(text, start, end);

        var index = start;
        while (index < end)
        {
            // Skip characters that cannot begin a token
            var codePoint = CodePointAt(text, index, end, out var length);
            if (!IsTokenStart(codePoint))
            {
                index += length;
                continue;
            }

            // Extend the token over every character that may continue it
            var tokenStart = index;
            index += length;
            while (index < end)
            {
                // A backslash belongs to the token only when it escapes an apostrophe
                if (text[index] == Backslash && index + 1 < end && text[index + 1] == Apostrophe)
                {
                    index += 2;
                    continue;
                }

                codePoint = CodePointAt(text, index, end, out length);
                if (!IsTokenContinuation(codePoint))
                {
                    break;
                }

                index += length;
            }

            tokens.Add(new TextRange(tokenStart, index - tokenStart));
        }
    }

    /// <summary>
    ///     Appends the words found inside a range of a text to a list.
    /// </summary>
    /// <remarks>
    ///     Words are what the spell checker looks up. The reported range is the original text, so a
    ///     typographic apostrophe stays typographic and an escaped apostrophe keeps its backslash. A
    ///     leading or trailing apostrophe is not part of a word. The list is appended to, never cleared.
    ///     Thread-safe as long as the list is not shared.
    /// </remarks>
    /// <param name="text">The text to scan. Must not be null.</param>
    /// <param name="within">The range of the text to scan. Must lie inside the text.</param>
    /// <param name="words">The list that receives the words in order. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="words"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the text.</exception>
    internal static void FindWords(string text, TextRange within, List<TextRange> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        ValidatePart(text, within.Start, within.End);

        var end = within.End;
        var index = within.Start;
        while (index < end)
        {
            // Skip characters that cannot begin a word
            var codePoint = CodePointAt(text, index, end, out var length);
            if (!IsWordLetter(codePoint))
            {
                index += length;
                continue;
            }

            // A word starts with a letter and an optional combining mark
            var wordStart = index;
            index = SkipOptionalMark(text, index + length, end);

            // Keep extending while the next letter follows directly or after one apostrophe
            while (TryExtendWord(text, index, end, out var next))
            {
                index = next;
            }

            words.Add(new TextRange(wordStart, index - wordStart));
        }
    }

    /// <summary>
    ///     Appends the pieces of a word, split at case changes, to a list.
    /// </summary>
    /// <remarks>
    ///     Identifiers are written in camelCase and PascalCase, so an unknown word is checked piece by
    ///     piece. The pieces cover the word completely and in order; a word with no case change yields
    ///     one piece, the word itself. The list is appended to, never cleared. Thread-safe as long as the
    ///     list is not shared.
    /// </remarks>
    /// <param name="text">The text holding the word. Must not be null.</param>
    /// <param name="word">The range of the word. Must lie inside the text.</param>
    /// <param name="pieces">The list that receives the pieces in order. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="pieces"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the text.</exception>
    internal static void SplitCase(string text, TextRange word, List<TextRange> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ValidatePart(text, word.Start, word.End);

        var end = word.End;
        var pieceStart = word.Start;
        var position = word.Start;
        while (position < end)
        {
            // Test every boundary between two characters for a case change
            _ = CodePointAt(text, position, end, out var length);
            position += length;
            if (position < end && IsCaseBreak(text, word.Start, position, end))
            {
                pieces.Add(new TextRange(pieceStart, position - pieceStart));
                pieceStart = position;
            }
        }

        pieces.Add(new TextRange(pieceStart, end - pieceStart));
    }

    /// <summary>
    ///     Removes leading and trailing characters that are neither letters nor digits from a token.
    /// </summary>
    /// <remarks>
    ///     Word-list entries are compared with tokens without the punctuation at their edges, so that
    ///     <c>(k8s)</c> or <c>v1.2.</c> in a sentence match the entries <c>k8s</c> and <c>v1.2</c>.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text holding the token. Must not be null.</param>
    /// <param name="token">The range of the token. Must lie inside the text.</param>
    /// <returns>The range of the token without edge punctuation; empty when the token has no letter or digit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is not inside the text.</exception>
    internal static TextRange TrimToken(string text, TextRange token)
    {
        ValidatePart(text, token.Start, token.End);

        // Move the start forward to the first letter or digit
        var start = token.Start;
        var end = token.End;
        while (start < end)
        {
            var codePoint = CodePointAt(text, start, end, out var length);
            if (IsLetterOrDigit(codePoint))
            {
                break;
            }

            start += length;
        }

        // Move the end backward to just past the last letter or digit
        while (end > start)
        {
            var codePoint = CodePointBefore(text, start, end, out var length);
            if (IsLetterOrDigit(codePoint))
            {
                break;
            }

            end -= length;
        }

        return new TextRange(start, end - start);
    }

    /// <summary>
    ///     Interprets one entry of a word list (<c>words</c>, <c>ignoreWords</c>, <c>flagWords</c>, or a
    ///     directive).
    /// </summary>
    /// <remarks>
    ///     The entry is normalized the way tokens are, so it can be matched: edge punctuation is removed
    ///     and the entry is classified as a single word (matched against words, in any letter case) or as a
    ///     token with digits or punctuation (<c>k8s</c>, <c>foo-bar</c>, matched against whole tokens). An
    ///     entry that could never match is rejected with a reason, because silently keeping it would let a
    ///     setting the user believes is in force have no effect: an entry that is blank, contains white
    ///     space, contains a character that cannot be part of a token (including the cspell forbidden-word
    ///     marker <c>!</c>, which would otherwise be mistaken for an accepted word), or has no letter or digit.
    ///     When suggestions are allowed (the flagged-word list), the cspell forms <c>word-&gt;one, two</c> and
    ///     <c>word: one</c> are accepted and the suggestions are returned with the word. Stateless and thread-safe.
    /// </remarks>
    /// <param name="entry">The entry text; may be null, which is reported as blank.</param>
    /// <param name="allowSuggestions">
    ///     <see langword="true"/> to accept the suggestion forms; otherwise the whole entry is the word.
    /// </param>
    /// <param name="result">The interpreted entry when the method returns true; otherwise the default value.</param>
    /// <param name="problem">The reason the entry cannot be used when the method returns false; otherwise null.</param>
    /// <returns><see langword="true"/> when the entry is usable; otherwise <see langword="false"/>.</returns>
    internal static bool TryParseListEntry(string? entry, bool allowSuggestions, out ListEntry result, out string? problem)
    {
        result = default;
        problem = null;

        // A blank entry has no word at all
        if (string.IsNullOrWhiteSpace(entry))
        {
            problem = "it is blank.";
            return false;
        }

        // Separate the word from its suggestions when the suggestion forms are allowed
        var suggestions = Array.Empty<string>();
        var wordText = entry;
        if (allowSuggestions)
        {
            wordText = SplitSuggestions(entry, out suggestions);
        }

        wordText = wordText.Trim();
        if (wordText.Length == 0)
        {
            problem = "there is no word before its suggestions.";
            return false;
        }

        // Reject entries that could never match a token
        problem = FindEntryProblem(wordText);
        if (problem is not null)
        {
            return false;
        }

        // Normalize like a token and classify as a single word or a token with digits or punctuation
        var trimmed = TrimToken(wordText, new TextRange(0, wordText.Length));
        var normalized = wordText.Substring(trimmed.Start, trimmed.Length);
        result = new ListEntry(normalized, IsSingleWord(normalized), [.. suggestions]);
        return true;
    }

    /// <summary>
    ///     Determines whether a code point is a letter that belongs to a word.
    /// </summary>
    /// <remarks>
    ///     Han, Hiragana, Katakana, and Hangul letters are excluded because they are written without
    ///     spaces and cannot be checked against a Latin-script dictionary. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to test.</param>
    /// <returns><see langword="true"/> for a letter of a script that is spell-checked.</returns>
    internal static bool IsWordLetter(int codePoint) =>
        codePoint <= MaxAscii ? char.IsAsciiLetter((char)codePoint) : IsLetterCategory(CategoryOf(codePoint)) && !IsIgnoredScript(codePoint);

    /// <summary>
    ///     Counts the Unicode scalar values (characters, with surrogate pairs counted once) in a text.
    /// </summary>
    /// <remarks>
    ///     Used where cspell measures a length in characters rather than in UTF-16 code units. A lone
    ///     surrogate counts as one. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to measure.</param>
    /// <returns>The number of characters.</returns>
    internal static int CountCharacters(ReadOnlySpan<char> text)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            // A high surrogate followed by a low surrogate is one character
            index += char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Checks that a part of a text lies inside the text.
    /// </summary>
    /// <remarks>
    ///     Every public entry point validates its range once so that the scanning loops can rely on the
    ///     bounds. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text. Must not be null.</param>
    /// <param name="start">The first offset of the part.</param>
    /// <param name="end">The offset just past the part.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the part is not inside the text.</exception>
    private static void ValidatePart(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || end < start || end > text.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(start),
                $"The part {start}..{end} is not inside a text of length {text.Length}.");
        }
    }

    /// <summary>
    ///     Reads the code point at an offset, treating anything that cannot be decoded as a replacement character.
    /// </summary>
    /// <remarks>
    ///     A well-formed surrogate pair is one code point of two code units. A lone surrogate, or a high
    ///     surrogate whose partner lies beyond <paramref name="end"/>, decodes as the replacement
    ///     character, which is a symbol and never a letter, so malformed text is skipped safely.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to read. Must not be null.</param>
    /// <param name="index">The offset to read. Must be before <paramref name="end"/>.</param>
    /// <param name="end">The offset just past the readable part of the text.</param>
    /// <param name="length">Receives the number of code units the code point occupies (1 or 2).</param>
    /// <returns>The code point.</returns>
    private static int CodePointAt(string text, int index, int end, out int length)
    {
        var first = text[index];
        length = 1;

        // The common case is a code unit that is a whole character
        if (!char.IsSurrogate(first))
        {
            return first;
        }

        // A surrogate pair is one character; anything else is malformed
        if (char.IsHighSurrogate(first) && index + 1 < end && char.IsLowSurrogate(text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(first, text[index + 1]);
        }

        return ReplacementCodePoint;
    }

    /// <summary>
    ///     Reads the code point that ends at an offset, treating anything that cannot be decoded as a replacement character.
    /// </summary>
    /// <remarks>
    ///     The mirror of <see cref="CodePointAt"/>, used to look backwards from a boundary. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to read. Must not be null.</param>
    /// <param name="start">The offset of the first readable code unit.</param>
    /// <param name="index">The offset just past the code point to read. Must be after <paramref name="start"/>.</param>
    /// <param name="length">Receives the number of code units the code point occupies (1 or 2).</param>
    /// <returns>The code point.</returns>
    private static int CodePointBefore(string text, int start, int index, out int length)
    {
        var last = text[index - 1];
        length = 1;

        if (!char.IsSurrogate(last))
        {
            return last;
        }

        if (char.IsLowSurrogate(last) && index - 2 >= start && char.IsHighSurrogate(text[index - 2]))
        {
            length = 2;
            return char.ConvertToUtf32(text[index - 2], last);
        }

        return ReplacementCodePoint;
    }

    /// <summary>
    ///     Gets the general category of a code point.
    /// </summary>
    /// <remarks>
    ///     Wraps the framework lookup so the scanning code states intent. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to classify.</param>
    /// <returns>The Unicode general category.</returns>
    private static UnicodeCategory CategoryOf(int codePoint) => CharUnicodeInfo.GetUnicodeCategory(codePoint);

    /// <summary>
    ///     Determines whether a category is one of the letter categories.
    /// </summary>
    /// <remarks>
    ///     Matches the <c>\p{L}</c> class that cspell uses: upper, lower, title, modifier, and other letters.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="category">The category to test.</param>
    /// <returns><see langword="true"/> for a letter category.</returns>
    private static bool IsLetterCategory(UnicodeCategory category) =>
        category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter;

    /// <summary>
    ///     Determines whether a category is one of the combining-mark categories.
    /// </summary>
    /// <remarks>
    ///     Matches the <c>\p{M}</c> class that cspell uses, so an accent written as a separate combining
    ///     character stays with its letter. Stateless and thread-safe.
    /// </remarks>
    /// <param name="category">The category to test.</param>
    /// <returns><see langword="true"/> for a nonspacing, spacing, or enclosing mark.</returns>
    private static bool IsMarkCategory(UnicodeCategory category) =>
        category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>
    ///     Determines whether a code point is a letter of any script, or an ASCII digit.
    /// </summary>
    /// <remarks>
    ///     The test that decides what counts as the substance of a token when edge punctuation is trimmed.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to test.</param>
    /// <returns><see langword="true"/> for a letter or an ASCII digit.</returns>
    private static bool IsLetterOrDigit(int codePoint) =>
        codePoint <= MaxAscii ? char.IsAsciiLetterOrDigit((char)codePoint) : IsLetterCategory(CategoryOf(codePoint));

    /// <summary>
    ///     Determines whether a code point may begin a token.
    /// </summary>
    /// <remarks>
    ///     A token starts with a letter, an ASCII digit, an underscore, an apostrophe of either kind, a
    ///     backtick, a dot, a plus, or a hyphen. Combining marks cannot start a token. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to test.</param>
    /// <returns><see langword="true"/> when a token may start with the code point.</returns>
    private static bool IsTokenStart(int codePoint) =>
        codePoint <= MaxAscii
            ? char.IsAsciiLetterOrDigit((char)codePoint) || IsTokenPunctuation(codePoint)
            : codePoint == TypographicApostrophe || IsLetterCategory(CategoryOf(codePoint));

    /// <summary>
    ///     Determines whether a code point may continue a token.
    /// </summary>
    /// <remarks>
    ///     The characters that start a token, plus combining marks. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to test.</param>
    /// <returns><see langword="true"/> when a token may continue with the code point.</returns>
    private static bool IsTokenContinuation(int codePoint) =>
        IsTokenStart(codePoint) || (codePoint > MaxAscii && IsMarkCategory(CategoryOf(codePoint)));

    /// <summary>
    ///     Determines whether an ASCII code point is one of the punctuation characters allowed inside a token.
    /// </summary>
    /// <remarks>
    ///     The characters are underscore, straight apostrophe, backtick, dot, plus, and hyphen; the
    ///     typographic apostrophe is outside ASCII and is tested separately. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The ASCII code point to test.</param>
    /// <returns><see langword="true"/> for an allowed punctuation character.</returns>
    private static bool IsTokenPunctuation(int codePoint) => codePoint is '_' or Apostrophe or '`' or '.' or '+' or '-';

    /// <summary>
    ///     Determines whether a code point belongs to a script whose letters are never spell-checked.
    /// </summary>
    /// <remarks>
    ///     The ranges are the blocks of the Han, Hiragana, Katakana, and Hangul scripts that contain
    ///     letters, including the compatibility, supplementary, and halfwidth forms. The ranges are
    ///     deliberately coarse (whole blocks): a block holds no Latin-script letters, and a non-letter
    ///     inside a block is already excluded by the letter test. Stateless and thread-safe.
    /// </remarks>
    /// <param name="codePoint">The code point to test.</param>
    /// <returns><see langword="true"/> for a code point in one of those blocks.</returns>
    private static bool IsIgnoredScript(int codePoint) =>
        codePoint is (>= 0x1100 and <= 0x11FF)          // Hangul Jamo
            or (>= 0x3005 and <= 0x3007)                // Han iteration marks and numerals
            or (>= 0x3021 and <= 0x3029)                // Hangzhou numerals
            or (>= 0x3038 and <= 0x303B)                // Hangzhou numerals and Han iteration mark
            or (>= 0x3041 and <= 0x30FF)                // Hiragana and Katakana
            or (>= 0x3131 and <= 0x318E)                // Hangul compatibility Jamo
            or (>= 0x31F0 and <= 0x31FF)                // Katakana phonetic extensions
            or (>= 0x3400 and <= 0x4DBF)                // Han extension A
            or (>= 0x4E00 and <= 0x9FFF)                // Han unified ideographs
            or (>= 0xA960 and <= 0xA97F)                // Hangul Jamo extended A
            or (>= 0xAC00 and <= 0xD7FF)                // Hangul syllables and Jamo extended B
            or (>= 0xF900 and <= 0xFAFF)                // Han compatibility ideographs
            or (>= 0xFF66 and <= 0xFF9F)                // Halfwidth Katakana
            or (>= 0xFFA0 and <= 0xFFDC)                // Halfwidth Hangul
            or (>= 0x1B000 and <= 0x1B16F)              // Kana supplement and extensions
            or (>= 0x20000 and <= 0x323AF);             // Han extensions B to H

    /// <summary>
    ///     Skips one combining mark at an offset, if there is one.
    /// </summary>
    /// <remarks>
    ///     A letter may be followed by one combining mark that belongs to it. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to read. Must not be null.</param>
    /// <param name="index">The offset to test.</param>
    /// <param name="end">The offset just past the readable part of the text.</param>
    /// <returns>The offset after the mark, or <paramref name="index"/> when no mark is there.</returns>
    private static int SkipOptionalMark(string text, int index, int end)
    {
        if (index >= end)
        {
            return index;
        }

        var codePoint = CodePointAt(text, index, end, out var length);
        return codePoint > MaxAscii && IsMarkCategory(CategoryOf(codePoint)) ? index + length : index;
    }

    /// <summary>
    ///     Tries to extend a word by one more letter, which may follow one apostrophe.
    /// </summary>
    /// <remarks>
    ///     An apostrophe belongs to the word only when a letter follows it, so <c>don't</c> is one word
    ///     but the closing quote of <c>'zorb'</c> is not part of it. A backslash may precede a straight
    ///     apostrophe (the escape in a quoted string). Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to read. Must not be null.</param>
    /// <param name="index">The offset just past the word so far.</param>
    /// <param name="end">The offset just past the readable part of the text.</param>
    /// <param name="next">Receives the offset just past the extended word when the method returns true.</param>
    /// <returns><see langword="true"/> when the word was extended.</returns>
    private static bool TryExtendWord(string text, int index, int end, out int next)
    {
        next = index;
        var letterAt = index;

        // An optional backslash and an apostrophe may sit between two letters
        if (letterAt < end && text[letterAt] == Backslash && letterAt + 1 < end && IsApostrophe(text[letterAt + 1]))
        {
            letterAt++;
        }

        if (letterAt < end && IsApostrophe(text[letterAt]))
        {
            letterAt++;
        }
        else
        {
            letterAt = index;
        }

        // The extension must end in a letter, with an optional combining mark
        if (letterAt >= end)
        {
            return false;
        }

        var codePoint = CodePointAt(text, letterAt, end, out var length);
        if (!IsWordLetter(codePoint))
        {
            return false;
        }

        next = SkipOptionalMark(text, letterAt + length, end);
        return true;
    }

    /// <summary>
    ///     Determines whether a character is a straight or typographic apostrophe.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> for <c>'</c> or <c>’</c>.</returns>
    private static bool IsApostrophe(char character) => character is Apostrophe or TypographicApostrophe;

    /// <summary>
    ///     Determines whether a word is to be split at a boundary because of a case change.
    /// </summary>
    /// <remarks>
    ///     Implements the two split rules of the class remarks at the boundary before
    ///     <paramref name="boundary"/>. Combining marks after a letter are looked through, because an
    ///     accented capital is written as a letter and a mark. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text holding the word. Must not be null.</param>
    /// <param name="start">The offset of the first code unit of the word.</param>
    /// <param name="boundary">The offset of the boundary, which is inside the word.</param>
    /// <param name="end">The offset just past the word.</param>
    /// <returns><see langword="true"/> when the word is split at the boundary.</returns>
    private static bool IsCaseBreak(string text, int start, int boundary, int end)
    {
        // The character after the boundary must be an upper-case letter for either rule
        var nextCodePoint = CodePointAt(text, boundary, end, out var nextLength);
        if (CategoryOf(nextCodePoint) != UnicodeCategory.UppercaseLetter)
        {
            return false;
        }

        var before = CategoryBeforeBoundary(text, start, boundary);

        // Rule one: a lower-case letter, then an upper-case letter (fooBar)
        if (before == UnicodeCategory.LowercaseLetter)
        {
            return true;
        }

        // Rule two: a capital, then a capital that starts a capitalized word (XMLParser), unless that
        // capital starts an English suffix that ends the word (APIs)
        if (before != UnicodeCategory.UppercaseLetter)
        {
            return false;
        }

        var afterCapital = SkipOptionalMark(text, boundary + nextLength, end);
        if (afterCapital >= end || CategoryOf(CodePointAt(text, afterCapital, end, out _)) != UnicodeCategory.LowercaseLetter)
        {
            return false;
        }

        return !StartsEnglishSuffixThatEndsWord(text, afterCapital, end);
    }

    /// <summary>
    ///     Gets the category of the last letter before a boundary, looking through one combining mark.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text holding the word. Must not be null.</param>
    /// <param name="start">The offset of the first code unit of the word.</param>
    /// <param name="boundary">The offset of the boundary, which is after <paramref name="start"/>.</param>
    /// <returns>The category of the character before the boundary, or of the character before a mark that precedes it.</returns>
    private static UnicodeCategory CategoryBeforeBoundary(string text, int start, int boundary)
    {
        var codePoint = CodePointBefore(text, start, boundary, out var length);
        var category = CategoryOf(codePoint);
        if (IsMarkCategory(category) && boundary - length > start)
        {
            return CategoryOf(CodePointBefore(text, start, boundary - length, out _));
        }

        return category;
    }

    /// <summary>
    ///     Determines whether the text at an offset is an English suffix that is not followed by a lower-case letter.
    /// </summary>
    /// <remarks>
    ///     This is the exception to the capital-run rule: in <c>APIs</c> the final <c>s</c> belongs to
    ///     the acronym, so it is not split off. Matching is case-sensitive and ordinal. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text holding the word. Must not be null.</param>
    /// <param name="index">The offset just past the capital that is being tested.</param>
    /// <param name="end">The offset just past the word.</param>
    /// <returns><see langword="true"/> when a suffix starts at the offset and is not followed by a lower-case letter.</returns>
    private static bool StartsEnglishSuffixThatEndsWord(string text, int index, int end)
    {
        foreach (var suffix in EnglishSuffixes)
        {
            if (index + suffix.Length > end || string.CompareOrdinal(text, index, suffix, 0, suffix.Length) != 0)
            {
                continue;
            }

            // The suffix counts only when the word ends there or goes on with something other than a lower-case letter
            var after = index + suffix.Length;
            if (after >= end || CategoryOf(CodePointAt(text, after, end, out _)) != UnicodeCategory.LowercaseLetter)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Splits the suggestion part off the text of a flagged-word entry.
    /// </summary>
    /// <remarks>
    ///     Accepts <c>word-&gt;one, two</c> and <c>word: one</c>; the arrow form is tried first so that a
    ///     colon inside a suggestion list is not misread. Stateless and thread-safe.
    /// </remarks>
    /// <param name="entry">The entry text. Must not be null.</param>
    /// <param name="suggestions">Receives the suggestions, without blanks; empty when the entry has none.</param>
    /// <returns>The text before the suggestion separator, or the whole entry when it has none.</returns>
    private static string SplitSuggestions(string entry, out string[] suggestions)
    {
        suggestions = [];
        var arrow = entry.IndexOf(SuggestionArrow, StringComparison.Ordinal);
        if (arrow >= 0)
        {
            suggestions = SplitList(entry[(arrow + SuggestionArrow.Length)..]);
            return entry[..arrow];
        }

        var colon = entry.IndexOf(SuggestionColon);
        if (colon >= 0)
        {
            suggestions = SplitList(entry[(colon + 1)..]);
            return entry[..colon];
        }

        return entry;
    }

    /// <summary>
    ///     Splits a comma-separated list of suggestions and drops blank items.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="list">The list text. Must not be null.</param>
    /// <returns>The trimmed, non-blank items in order.</returns>
    private static string[] SplitList(string list) =>
        list.Split(SuggestionSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Finds the reason a word-list entry could never match, if there is one.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="word">The entry text without suggestions. Must not be empty.</param>
    /// <returns>The reason, or null when the entry can match.</returns>
    private static string? FindEntryProblem(string word)
    {
        if (word[0] == ForbiddenMarker)
        {
            return "the forbidden-word marker '!' is not supported; list the word in flagWords instead.";
        }

        // Every character must be one that can occur inside a token
        var index = 0;
        var hasLetterOrDigit = false;
        while (index < word.Length)
        {
            var codePoint = CodePointAt(word, index, word.Length, out var length);
            if (char.IsWhiteSpace(word, index))
            {
                return "it contains white space, so it can never match a word; list each word separately.";
            }

            if (!IsTokenContinuation(codePoint))
            {
                return $"it contains the character '{word.Substring(index, length)}', which cannot be part of a word, " +
                       "so it can never match.";
            }

            hasLetterOrDigit |= IsLetterOrDigit(codePoint);
            index += length;
        }

        return hasLetterOrDigit ? null : "it has no letter or digit, so it can never match.";
    }

    /// <summary>
    ///     Determines whether a normalized entry is exactly one word.
    /// </summary>
    /// <remarks>
    ///     An entry such as <c>zorb</c> or <c>don't</c> is one word and is matched against words; an entry
    ///     such as <c>k8s</c> or <c>foo-bar</c> contains something that ends a word and is matched against
    ///     tokens. Stateless and thread-safe.
    /// </remarks>
    /// <param name="entry">The normalized entry. Must not be empty.</param>
    /// <returns><see langword="true"/> when the whole entry is a single word.</returns>
    private static bool IsSingleWord(string entry)
    {
        var words = new List<TextRange>(1);
        FindWords(entry, new TextRange(0, entry.Length), words);
        return words.Count == 1 && words[0].Start == 0 && words[0].Length == entry.Length;
    }
}
