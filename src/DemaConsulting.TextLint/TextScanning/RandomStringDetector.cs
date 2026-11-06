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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Recognizes the strings that look like random data rather than words, such as keys and tokens, and the
///     hexadecimal sequences inside a token, so they are not reported as unknown words.
/// </summary>
/// <remarks>
///     cspell skips a long token when it looks random, and ignores unknown words that lie inside a
///     hexadecimal sequence of the token. Both are ported here so a project migrated from cspell stays quiet
///     where it was quiet. A token looks random when it is long enough and when the string made by
///     replacing its structure with one symbol per part is at least half as long as the token itself: each
///     run of digits becomes one symbol, each word (a lower-case run, optionally after one capital) one,
///     each run of capitals one, each separator run one, and every other character stays. Identifiers and
///     prose, which are made of a few long words, score low; strings of digits, capitals, and punctuation
///     mixed together, such as tokens and keys, score high. The score is a heuristic, so it errs on both
///     sides, exactly as cspell's does. The hexadecimal sequences are runs of hexadecimal digits and hyphens
///     of at least eight characters that are all in one letter case and contain a digit or a hyphen, such as
///     a partial UUID. The categorization is hand-written over code points, with no regular expression, so
///     its cost is linear in the length of the token; the hexadecimal pattern is generated and has a match
///     time limit. The class is stateless and thread-safe.
/// </remarks>
internal static partial class RandomStringDetector
{
    /// <summary>
    ///     The shortest hexadecimal sequence, in characters, that is taken as data.
    /// </summary>
    internal const int MinimumHexSequenceLength = 8;

    /// <summary>
    ///     The symbol that stands for a run of digits.
    /// </summary>
    private const int DigitSymbol = '0';

    /// <summary>
    ///     The symbol that stands for a word: a lower-case run, optionally after one capital.
    /// </summary>
    private const int WordSymbol = '1';

    /// <summary>
    ///     The symbol that stands for a run of capitals.
    /// </summary>
    private const int CapitalsSymbol = '2';

    /// <summary>
    ///     The symbol that stands for a run of separators.
    /// </summary>
    private const int SeparatorSymbol = '3';

    /// <summary>
    ///     The symbol that stands for a combining mark that is not attached to a letter.
    /// </summary>
    private const int MarkSymbol = '4';

    /// <summary>
    ///     The symbol that stands for a lower-case letter with its combining marks.
    /// </summary>
    private const int LowerLetterSymbol = 'a';

    /// <summary>
    ///     The symbol that stands for a capital letter with its combining marks.
    /// </summary>
    private const int UpperLetterSymbol = 'A';

    /// <summary>
    ///     The highest code point of the basic multilingual plane, whose characters take one UTF-16 code unit.
    /// </summary>
    private const int MaxBasicPlane = 0xFFFF;

    /// <summary>
    ///     The pattern text of an ASCII word boundary, which is what <c>\b</c> means in JavaScript.
    /// </summary>
    private const string WordBoundary = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";

    /// <summary>
    ///     The longest time, in milliseconds, that the hexadecimal pattern may spend on one token before matching is abandoned.
    /// </summary>
    private const int MatchTimeoutMilliseconds = PatternParser.MatchTimeoutMilliseconds;

    /// <summary>
    ///     Determines whether a token looks like random data.
    /// </summary>
    /// <remarks>
    ///     The caller decides whether the token is long enough to be judged; this only scores it. An empty
    ///     token is not random. Allocates working storage proportional to the token. Stateless and thread-safe.
    /// </remarks>
    /// <param name="token">The token to score.</param>
    /// <returns><see langword="true"/> when the categorized token is at least half as long as the token.</returns>
    internal static bool IsRandom(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return false;
        }

        // The ratio of the categorized length to the length is at least one half when twice the one is at least the other
        return 2L * CategorizedLength(token) >= token.Length;
    }

    /// <summary>
    ///     Appends the ranges of the hexadecimal sequences of a token that cspell treats as data.
    /// </summary>
    /// <remarks>
    ///     A sequence is a run of hexadecimal digits and hyphens that starts and ends with a hexadecimal digit,
    ///     starts at the token's start or after a character that is not an ASCII letter or digit, and ends at
    ///     the token's end or before one. It must have at least <see cref="MinimumHexSequenceLength"/>
    ///     characters, must not follow a letter or be followed by one, must be written in one letter case, and
    ///     must contain a digit or a hyphen. The list is appended to, never cleared. Thread-safe as long as
    ///     the list is not shared.
    /// </remarks>
    /// <param name="text">The text holding the token. Must not be null.</param>
    /// <param name="token">The range of the token. Must lie inside the text.</param>
    /// <param name="sequences">The list that receives the ranges, as offsets into the text. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="sequences"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the token is not inside the text.</exception>
    /// <exception cref="RegexMatchTimeoutException">Thrown when the pattern exceeds its time limit on this token.</exception>
    internal static void FindHexSequences(string text, TextRange token, List<TextRange> sequences)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sequences);
        if (token.Start < 0 || token.Length < 0 || token.End > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(token), "The token must lie inside the text.");
        }

        var span = text.AsSpan(token.Start, token.Length);
        foreach (var match in HexSequence().EnumerateMatches(span))
        {
            var found = span.Slice(match.Index, match.Length);
            if (match.Length >= MinimumHexSequenceLength &&
                !FollowsLetter(span, match.Index) &&
                !IsFollowedByLetter(span, match.Index + match.Length) &&
                IsOneCase(found) &&
                (found.IndexOfAnyInRange('0', '9') >= 0 || found.Contains('-')))
            {
                sequences.Add(new TextRange(token.Start + match.Index, match.Length));
            }
        }
    }

    /// <summary>
    ///     Matches a run of hexadecimal digits and hyphens that starts and ends with a hexadecimal digit and is
    ///     delimited by anything that is not an ASCII letter or digit.
    /// </summary>
    /// <returns>The generated regular expression.</returns>
    [GeneratedRegex(
        "(?:" + WordBoundary + "|(?<=[^A-Za-z0-9]))[0-9a-fA-F][-0-9a-fA-F]*[0-9a-fA-F](?:" + WordBoundary + "|(?=[^A-Za-z0-9]))",
        RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex HexSequence();

    /// <summary>
    ///     Determines whether the code unit before an offset is a letter.
    /// </summary>
    /// <remarks>
    ///     Judged by the single code unit, as cspell does, so the second half of a surrogate pair is never a
    ///     letter. Stateless.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset whose predecessor is tested.</param>
    /// <returns><see langword="true"/> when the offset is not zero and the code unit before it is a letter.</returns>
    private static bool FollowsLetter(ReadOnlySpan<char> text, int offset) => offset > 0 && char.IsLetter(text[offset - 1]);

    /// <summary>
    ///     Determines whether the character at an offset is a letter.
    /// </summary>
    /// <remarks>
    ///     Judged by the whole character, so a letter outside the basic plane counts. Stateless.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset of the character; may be the end of the text.</param>
    /// <returns><see langword="true"/> when there is a character at the offset and it is a letter.</returns>
    private static bool IsFollowedByLetter(ReadOnlySpan<char> text, int offset) =>
        offset < text.Length && Rune.DecodeFromUtf16(text[offset..], out var rune, out _) == System.Buffers.OperationStatus.Done &&
        Rune.IsLetter(rune);

    /// <summary>
    ///     Determines whether a hexadecimal sequence is written in one letter case.
    /// </summary>
    /// <remarks>
    ///     Digits and hyphens have no case, so a sequence with no letters is in one case. Stateless.
    /// </remarks>
    /// <param name="sequence">The sequence of ASCII hexadecimal digits and hyphens.</param>
    /// <returns><see langword="true"/> when it has no capital letters or has no lower-case letters.</returns>
    private static bool IsOneCase(ReadOnlySpan<char> sequence) =>
        sequence.IndexOfAnyInRange('A', 'F') < 0 || sequence.IndexOfAnyInRange('a', 'f') < 0;

    /// <summary>
    ///     Computes the length, in UTF-16 code units, of the categorized form of a token.
    /// </summary>
    /// <remarks>
    ///     Follows the same steps as cspell, in the same order, because each step sees the result of the one
    ///     before: digit runs; letters with their combining marks; words (a lower-case run, optionally after
    ///     one capital); runs of capitals; remaining marks; removal of underscores; runs of the separators
    ///     hyphen, dot, and apostrophe. Every other character stays as it is. Work is linear in the token.
    /// </remarks>
    /// <param name="token">The token.</param>
    /// <returns>The length of the categorized form.</returns>
    private static int CategorizedLength(ReadOnlySpan<char> token)
    {
        var symbols = Decode(token);
        symbols = ReplaceDigitRuns(symbols);
        symbols = ReplaceLettersWithMarks(symbols);
        symbols = ReplaceWords(symbols);
        symbols = ReplaceCapitalRuns(symbols);
        symbols = ReplaceMarks(symbols);
        symbols = ReplaceSeparatorRuns(symbols);

        var length = 0;
        foreach (var symbol in symbols)
        {
            length += symbol > MaxBasicPlane ? 2 : 1;
        }

        return length;
    }

    /// <summary>
    ///     Splits a token into its code points, keeping an unpaired surrogate as it is.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="token">The token.</param>
    /// <returns>The code points in order.</returns>
    private static List<int> Decode(ReadOnlySpan<char> token)
    {
        var symbols = new List<int>(token.Length);
        var index = 0;
        while (index < token.Length)
        {
            if (char.IsHighSurrogate(token[index]) && index + 1 < token.Length && char.IsLowSurrogate(token[index + 1]))
            {
                symbols.Add(char.ConvertToUtf32(token[index], token[index + 1]));
                index += 2;
            }
            else
            {
                symbols.Add(token[index]);
                index++;
            }
        }

        return symbols;
    }

    /// <summary>
    ///     Replaces each run of ASCII digits by one digit symbol.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with digit runs collapsed.</returns>
    private static List<int> ReplaceDigitRuns(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        var inRun = false;
        foreach (var symbol in symbols)
        {
            var isDigit = symbol is >= '0' and <= '9';
            if (!isDigit || !inRun)
            {
                result.Add(isDigit ? DigitSymbol : symbol);
            }

            inRun = isDigit;
        }

        return result;
    }

    /// <summary>
    ///     Replaces each letter that is followed by combining marks, with the marks, by one plain letter.
    /// </summary>
    /// <remarks>
    ///     A lower-case letter becomes a lower-case symbol and a capital a capital symbol, so the later steps
    ///     see an ordinary letter. Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with such letters replaced.</returns>
    private static List<int> ReplaceLettersWithMarks(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        var index = 0;
        while (index < symbols.Count)
        {
            var category = Category(symbols[index]);
            if (category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter &&
                index + 1 < symbols.Count &&
                IsMark(symbols[index + 1]))
            {
                // Consume the letter and every mark that follows it
                result.Add(category == UnicodeCategory.LowercaseLetter ? LowerLetterSymbol : UpperLetterSymbol);
                index++;
                while (index < symbols.Count && IsMark(symbols[index]))
                {
                    index++;
                }
            }
            else
            {
                result.Add(symbols[index]);
                index++;
            }
        }

        return result;
    }

    /// <summary>
    ///     Replaces each word, a run of lower-case letters optionally after one capital, by one word symbol.
    /// </summary>
    /// <remarks>
    ///     A capital that is not followed by a lower-case letter is left alone, as a regular expression
    ///     matching an optional capital and then lower-case letters would leave it. Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with words collapsed.</returns>
    private static List<int> ReplaceWords(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        var index = 0;
        while (index < symbols.Count)
        {
            // A word starts at a lower-case letter, or at a capital that is directly followed by one
            var start = index;
            var cursor = index;
            if (Category(symbols[cursor]) == UnicodeCategory.UppercaseLetter)
            {
                cursor++;
            }

            var lowerStart = cursor;
            while (cursor < symbols.Count && Category(symbols[cursor]) == UnicodeCategory.LowercaseLetter)
            {
                cursor++;
            }

            if (cursor > lowerStart)
            {
                result.Add(WordSymbol);
                index = cursor;
            }
            else
            {
                result.Add(symbols[start]);
                index = start + 1;
            }
        }

        return result;
    }

    /// <summary>
    ///     Replaces each run of capitals by one capitals symbol.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with runs of capitals collapsed.</returns>
    private static List<int> ReplaceCapitalRuns(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        var inRun = false;
        foreach (var symbol in symbols)
        {
            var isCapital = Category(symbol) == UnicodeCategory.UppercaseLetter;
            if (!isCapital || !inRun)
            {
                result.Add(isCapital ? CapitalsSymbol : symbol);
            }

            inRun = isCapital;
        }

        return result;
    }

    /// <summary>
    ///     Replaces each combining mark that is left by one mark symbol.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with marks replaced.</returns>
    private static List<int> ReplaceMarks(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        foreach (var symbol in symbols)
        {
            result.Add(IsMark(symbol) ? MarkSymbol : symbol);
        }

        return result;
    }

    /// <summary>
    ///     Removes underscores and replaces each run of hyphens, dots, and apostrophes by one separator symbol.
    /// </summary>
    /// <remarks>
    ///     Underscores are removed before the runs are formed, so an underscore between two separators joins
    ///     them into one run and an underscore alone vanishes. Stateless.
    /// </remarks>
    /// <param name="symbols">The code points. Must not be null.</param>
    /// <returns>The code points with separators collapsed.</returns>
    private static List<int> ReplaceSeparatorRuns(List<int> symbols)
    {
        var result = new List<int>(symbols.Count);
        var inRun = false;
        foreach (var symbol in symbols)
        {
            if (symbol == '_')
            {
                continue;
            }

            var isSeparator = symbol is '-' or '.' or '\'';
            if (!isSeparator || !inRun)
            {
                result.Add(isSeparator ? SeparatorSymbol : symbol);
            }

            inRun = isSeparator;
        }

        return result;
    }

    /// <summary>
    ///     Gets the Unicode category of a code point.
    /// </summary>
    /// <remarks>
    ///     An unpaired surrogate has the surrogate category. Stateless.
    /// </remarks>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The category.</returns>
    private static UnicodeCategory Category(int codePoint) => CharUnicodeInfo.GetUnicodeCategory(codePoint);

    /// <summary>
    ///     Determines whether a code point is a combining mark.
    /// </summary>
    /// <remarks>
    ///     Stateless.
    /// </remarks>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> for a non-spacing, spacing, or enclosing mark.</returns>
    private static bool IsMark(int codePoint) =>
        Category(codePoint) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
}
