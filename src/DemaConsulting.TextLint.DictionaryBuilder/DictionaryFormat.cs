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

namespace DemaConsulting.TextLint.Dictionaries;

/// <summary>
///     The shared definition of the binary dictionary format: its layout constants, the case fold
///     that turns a word into its lookup key, the key hash, and the capitalization classification.
/// </summary>
/// <remarks>
///     The dictionary builder writes dictionary files and the library reads them, so both sides
///     must agree on every one of these definitions to the last bit. Keeping them in one source
///     file that both projects compile (the library links this file from the builder project)
///     makes disagreement impossible, where two copies could silently drift apart. The fold is an
///     explicit table owned by this class instead of a culture or <c>ToLowerInvariant</c> call,
///     so a dictionary built on one operating system is identical to one built on another and
///     lookups never depend on the host's globalization data. The class holds no state and every
///     member is thread-safe.
/// </remarks>
internal static class DictionaryFormat
{
    /// <summary>
    ///     The length in bytes of the header at the start of every dictionary file.
    /// </summary>
    internal const int HeaderLength = 32;

    /// <summary>
    ///     The byte offset of the format identifier in the header.
    /// </summary>
    internal const int MagicOffset = 0;

    /// <summary>
    ///     The byte offset of the 32-bit flags field in the header.
    /// </summary>
    internal const int FlagsOffset = 4;

    /// <summary>
    ///     The byte offset of the 32-bit distinct word count in the header.
    /// </summary>
    internal const int WordCountOffset = 8;

    /// <summary>
    ///     The byte offset of the 32-bit blob length, in bytes, in the header.
    /// </summary>
    internal const int BlobLengthOffset = 12;

    /// <summary>
    ///     The byte offset of the 32-bit slot count in the header.
    /// </summary>
    internal const int SlotCountOffset = 16;

    /// <summary>
    ///     The byte offset of the 32-bit hash identifier in the header.
    /// </summary>
    internal const int HashIdOffset = 20;

    /// <summary>
    ///     The byte offset of the reserved header bytes, which are always zero.
    /// </summary>
    internal const int ReservedOffset = 24;

    /// <summary>
    ///     The number of reserved header bytes.
    /// </summary>
    internal const int ReservedLength = HeaderLength - ReservedOffset;

    /// <summary>
    ///     The flags value of a file that uses no optional features.
    /// </summary>
    internal const uint NoFlags = 0;

    /// <summary>
    ///     The hash identifier of the only supported hash: FNV-1a over the key bytes followed by
    ///     the murmur3 finalizer.
    /// </summary>
    internal const uint SupportedHashId = 1;

    /// <summary>
    ///     The size in bytes of one slot of the slot table.
    /// </summary>
    internal const int SlotSize = 4;

    /// <summary>
    ///     The alignment, in bytes, that the blob is padded to before the slot table begins.
    /// </summary>
    internal const int BlobAlignment = 8;

    /// <summary>
    ///     The byte that ends every word record in the blob.
    /// </summary>
    internal const byte WordTerminator = (byte)'\n';

    /// <summary>
    ///     The maximum length, in characters, of a word that can be stored or looked up.
    /// </summary>
    internal const int MaxWordLength = 64;

    /// <summary>
    ///     The maximum number of UTF-8 bytes one stored character needs (Latin-1 letters need two).
    /// </summary>
    internal const int MaxBytesPerCharacter = 2;

    /// <summary>
    ///     The maximum length, in bytes, of a key: a word of the maximum length made of two-byte characters.
    /// </summary>
    internal const int MaxKeyLength = MaxWordLength * MaxBytesPerCharacter;

    /// <summary>
    ///     The number of low slot bits that hold the blob offset; the remaining high bits hold the case-form flags.
    /// </summary>
    internal const int OffsetBits = 28;

    /// <summary>
    ///     The mask that extracts the stored blob offset (plus one) from a slot value.
    /// </summary>
    internal const uint OffsetMask = (1u << OffsetBits) - 1;

    /// <summary>
    ///     The largest blob length in bytes, so that the stored offset plus one still fits the offset bits.
    /// </summary>
    internal const int MaxBlobLength = (1 << OffsetBits) - 2;

    /// <summary>
    ///     The slot flag recording that a word occurred entirely in lower case in the source lists.
    /// </summary>
    internal const uint CaseLower = 1u << OffsetBits;

    /// <summary>
    ///     The slot flag recording that a word occurred with only its first letter in upper case.
    /// </summary>
    internal const uint CaseCapitalized = 1u << (OffsetBits + 1);

    /// <summary>
    ///     The slot flag recording that a word of two or more letters occurred entirely in upper case.
    /// </summary>
    internal const uint CaseUpper = 1u << (OffsetBits + 2);

    /// <summary>
    ///     The slot flag recording that a word occurred with any other mixture of upper and lower case.
    /// </summary>
    internal const uint CaseMixed = 1u << (OffsetBits + 3);

    /// <summary>
    ///     The mask that extracts all case-form flags from a slot value.
    /// </summary>
    internal const uint CaseFormMask = CaseLower | CaseCapitalized | CaseUpper | CaseMixed;

    /// <summary>
    ///     The numerator of the maximum load factor: at most seven tenths of the slots may be occupied.
    /// </summary>
    internal const int MaxLoadNumerator = 7;

    /// <summary>
    ///     The denominator of the maximum load factor.
    /// </summary>
    internal const int MaxLoadDenominator = 10;

    /// <summary>
    ///     The straight apostrophe, which is the only punctuation a word may contain.
    /// </summary>
    internal const char Apostrophe = '\'';

    /// <summary>
    ///     The typographic (curly) apostrophe, which lookups treat as the straight apostrophe.
    /// </summary>
    internal const char TypographicApostrophe = '\u2019';

    /// <summary>
    ///     The highest character a key may contain; every character of a word must be Latin-1 or below.
    /// </summary>
    private const char MaxKeyCharacter = '\u00FF';

    /// <summary>
    ///     The first character of the Latin-1 upper-case letters.
    /// </summary>
    private const char FirstLatin1Upper = '\u00C0';

    /// <summary>
    ///     The last character of the Latin-1 upper-case letters.
    /// </summary>
    private const char LastLatin1Upper = '\u00DE';

    /// <summary>
    ///     The multiplication sign, which sits among the Latin-1 upper-case letters but is not a letter.
    /// </summary>
    private const char MultiplicationSign = '\u00D7';

    /// <summary>
    ///     The first character of the Latin-1 lower-case letters (the sharp s, which has no upper-case partner).
    /// </summary>
    private const char FirstLatin1Lower = '\u00DF';

    /// <summary>
    ///     The division sign, which sits among the Latin-1 lower-case letters but is not a letter.
    /// </summary>
    private const char DivisionSign = '\u00F7';

    /// <summary>
    ///     The distance from an ASCII or Latin-1 upper-case letter to its lower-case partner.
    /// </summary>
    private const int CaseOffset = 32;

    /// <summary>
    ///     The first character that needs two UTF-8 bytes.
    /// </summary>
    private const char FirstTwoByteCharacter = '\u0080';

    /// <summary>
    ///     The UTF-8 lead byte prefix of a two-byte sequence.
    /// </summary>
    private const int TwoByteLeadPrefix = 0xC0;

    /// <summary>
    ///     The UTF-8 prefix of a continuation byte.
    /// </summary>
    private const int ContinuationPrefix = 0x80;

    /// <summary>
    ///     The number of payload bits in a UTF-8 continuation byte.
    /// </summary>
    private const int ContinuationBits = 6;

    /// <summary>
    ///     The mask of the payload bits in a UTF-8 continuation byte.
    /// </summary>
    private const int ContinuationMask = 0x3F;

    /// <summary>
    ///     The FNV-1a 32-bit offset basis.
    /// </summary>
    private const uint FnvOffsetBasis = 2166136261;

    /// <summary>
    ///     The FNV-1a 32-bit prime.
    /// </summary>
    private const uint FnvPrime = 16777619;

    /// <summary>
    ///     The first multiplier of the murmur3 32-bit finalizer.
    /// </summary>
    private const uint MurmurMultiplier1 = 0x85EBCA6B;

    /// <summary>
    ///     The second multiplier of the murmur3 32-bit finalizer.
    /// </summary>
    private const uint MurmurMultiplier2 = 0xC2B2AE35;

    /// <summary>
    ///     The first shift distance of the murmur3 finalizer (also the last).
    /// </summary>
    private const int MurmurShiftLong = 16;

    /// <summary>
    ///     The middle shift distance of the murmur3 finalizer.
    /// </summary>
    private const int MurmurShiftShort = 13;

    /// <summary>
    ///     Gets the format identifier at the start of every file.
    /// </summary>
    /// <remarks>
    ///     The trailing digit is the layout generation, so a future layout change is a different
    ///     identifier that old readers reject.
    /// </remarks>
    internal static ReadOnlySpan<byte> Magic => "TLD1"u8;

    /// <summary>
    ///     Determines whether a character is an upper-case letter of the supported alphabet.
    /// </summary>
    /// <remarks>
    ///     The supported alphabet is the ASCII letters and the Latin-1 letters. Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> for A-Z and the Latin-1 capitals other than the multiplication sign.</returns>
    internal static bool IsUpperLetter(char character) =>
        character is >= 'A' and <= 'Z' ||
        (character is >= FirstLatin1Upper and <= LastLatin1Upper && character != MultiplicationSign);

    /// <summary>
    ///     Determines whether a character is a lower-case letter of the supported alphabet.
    /// </summary>
    /// <remarks>
    ///     The supported alphabet is the ASCII letters and the Latin-1 letters. Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> for a-z and the Latin-1 small letters other than the division sign.</returns>
    internal static bool IsLowerLetter(char character) =>
        character is >= 'a' and <= 'z' ||
        (character is >= FirstLatin1Lower and <= MaxKeyCharacter && character != DivisionSign);

    /// <summary>
    ///     Determines whether a character is a letter of the supported alphabet.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> when the character is an upper-case or lower-case letter of the alphabet.</returns>
    internal static bool IsLetter(char character) => IsUpperLetter(character) || IsLowerLetter(character);

    /// <summary>
    ///     Applies the case fold to one character.
    /// </summary>
    /// <remarks>
    ///     The fold lowers the ASCII and Latin-1 capitals and straightens the typographic apostrophe;
    ///     every other character is returned unchanged. It is the only definition of "the same word
    ///     in a different case" the system has, shared by the builder and the library. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="character">The character to fold.</param>
    /// <returns>The folded character.</returns>
    internal static char Fold(char character)
    {
        // Lower the capitals the alphabet knows, leaving the non-letters that share the Latin-1 block alone
        if (IsUpperLetter(character))
        {
            return (char)(character + CaseOffset);
        }

        // Treat the curly apostrophe as the straight one so pasted text matches
        return character == TypographicApostrophe ? Apostrophe : character;
    }

    /// <summary>
    ///     Converts a word to its lookup key: the folded characters encoded as UTF-8.
    /// </summary>
    /// <remarks>
    ///     A word that cannot be a key (empty, longer than <see cref="MaxWordLength"/> characters, or
    ///     containing a character above Latin-1 after folding) is reported as not representable, so
    ///     lookups answer "unknown" rather than throwing. The caller supplies the destination buffer
    ///     so that no allocation happens; a buffer of <see cref="MaxKeyLength"/> bytes always suffices.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="word">The word to convert.</param>
    /// <param name="key">The destination for the key bytes. Must hold at least <see cref="MaxKeyLength"/> bytes.</param>
    /// <param name="keyLength">Receives the number of key bytes written, or zero when the word is not representable.</param>
    /// <returns><see langword="true"/> when the word is representable; otherwise <see langword="false"/>.</returns>
    internal static bool TryFold(ReadOnlySpan<char> word, Span<byte> key, out int keyLength)
    {
        keyLength = 0;

        // Reject the words that can never be stored before touching the buffer
        if (word.IsEmpty || word.Length > MaxWordLength)
        {
            return false;
        }

        // Fold and encode each character; Latin-1 characters above ASCII take two bytes
        var length = 0;
        foreach (var original in word)
        {
            var folded = Fold(original);
            if (folded > MaxKeyCharacter)
            {
                return false;
            }

            if (folded < FirstTwoByteCharacter)
            {
                key[length++] = (byte)folded;
            }
            else
            {
                key[length++] = (byte)(TwoByteLeadPrefix | (folded >> ContinuationBits));
                key[length++] = (byte)(ContinuationPrefix | (folded & ContinuationMask));
            }
        }

        keyLength = length;
        return true;
    }

    /// <summary>
    ///     Computes the hash of a key: FNV-1a over the bytes followed by the murmur3 finalizer.
    /// </summary>
    /// <remarks>
    ///     FNV-1a is cheap and byte oriented; the finalizer spreads its weak low bits so that the
    ///     power-of-two slot mask picks well-distributed slots. The result is part of the file
    ///     format: changing it invalidates every existing file. Stateless and thread-safe.
    /// </remarks>
    /// <param name="key">The key bytes produced by <see cref="TryFold"/>.</param>
    /// <returns>The 32-bit hash.</returns>
    internal static uint Hash(ReadOnlySpan<byte> key)
    {
        // FNV-1a: xor each byte in, then multiply by the prime
        var hash = FnvOffsetBasis;
        foreach (var value in key)
        {
            hash = (hash ^ value) * FnvPrime;
        }

        // Murmur3 finalizer: avalanche the bits so the low bits used by the slot mask are well mixed
        hash ^= hash >> MurmurShiftLong;
        hash *= MurmurMultiplier1;
        hash ^= hash >> MurmurShiftShort;
        hash *= MurmurMultiplier2;
        hash ^= hash >> MurmurShiftLong;
        return hash;
    }

    /// <summary>
    ///     Classifies the capitalization of a source word as one case-form flag.
    /// </summary>
    /// <remarks>
    ///     Recording the form lets a future case-sensitive check distinguish <c>paris</c> from
    ///     <c>Paris</c> without changing the lists. A word with no upper-case letter is lower; a word
    ///     whose only upper-case letter is the first character is capitalized (a single upper-case
    ///     letter counts); a word of two or more letters that are all upper case is upper; anything
    ///     else is mixed. Apostrophes are neither upper nor lower. Stateless and thread-safe.
    /// </remarks>
    /// <param name="word">The word as written in the source list. Must not be empty.</param>
    /// <returns>Exactly one of <see cref="CaseLower"/>, <see cref="CaseCapitalized"/>, <see cref="CaseUpper"/>, or <see cref="CaseMixed"/>.</returns>
    internal static uint ClassifyCase(ReadOnlySpan<char> word)
    {
        // Count the upper-case letters and the letters overall
        var upperCount = 0;
        var letterCount = 0;
        foreach (var character in word)
        {
            if (IsUpperLetter(character))
            {
                upperCount++;
                letterCount++;
            }
            else if (IsLowerLetter(character))
            {
                letterCount++;
            }
        }

        // Pick the form from the counts and the position of the first letter
        if (upperCount == 0)
        {
            return CaseLower;
        }

        if (upperCount == 1 && IsUpperLetter(word[0]))
        {
            return CaseCapitalized;
        }

        return upperCount == letterCount && letterCount >= 2 ? CaseUpper : CaseMixed;
    }

    /// <summary>
    ///     Rounds a length up to the next multiple of <see cref="BlobAlignment"/>.
    /// </summary>
    /// <remarks>
    ///     The slot table starts at an aligned offset after the blob. Uses 64-bit arithmetic so a
    ///     hostile length cannot overflow. Stateless and thread-safe.
    /// </remarks>
    /// <param name="length">The length to round up. Must not be negative.</param>
    /// <returns>The smallest multiple of the alignment that is not less than the length.</returns>
    internal static long AlignBlobLength(long length) =>
        (length + BlobAlignment - 1) / BlobAlignment * BlobAlignment;

    /// <summary>
    ///     Determines whether a word count fits a slot count within the maximum load factor.
    /// </summary>
    /// <remarks>
    ///     Linear probing degrades quickly as the table fills, so the format caps the load. Uses
    ///     64-bit arithmetic so large counts cannot overflow. Stateless and thread-safe.
    /// </remarks>
    /// <param name="wordCount">The number of stored words.</param>
    /// <param name="slotCount">The number of slots.</param>
    /// <returns><see langword="true"/> when the words occupy no more than the maximum fraction of the slots.</returns>
    internal static bool IsWithinLoadLimit(long wordCount, long slotCount) =>
        wordCount * MaxLoadDenominator <= slotCount * MaxLoadNumerator;
}
