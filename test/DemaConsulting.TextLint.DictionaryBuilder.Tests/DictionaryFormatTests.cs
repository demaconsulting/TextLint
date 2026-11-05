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

using System.Text;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="DictionaryFormat"/> class.
/// </summary>
/// <remarks>
///     The format definitions are pure functions over characters and bytes, so these tests need no
///     file system and no test doubles. Expected keys and hashes are written out literally so that a
///     change to the definitions is a visible, deliberate change to a test.
/// </remarks>
public class DictionaryFormatTests
{
    /// <summary>
    ///     Folds a word and returns the resulting key bytes.
    /// </summary>
    /// <param name="word">The word to fold.</param>
    /// <param name="succeeded">Receives whether the word was representable.</param>
    /// <returns>The key bytes, or an empty array when the word was not representable.</returns>
    private static byte[] Fold(string word, out bool succeeded)
    {
        var buffer = new byte[DictionaryFormat.MaxKeyLength];
        succeeded = DictionaryFormat.TryFold(word, buffer, out var length);
        return buffer[..length].ToArray();
    }

    /// <summary>
    ///     Test that upper-case ASCII letters are folded to lower case.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_UpperAscii_Lowered()
    {
        // Arrange: a word with upper-case ASCII letters

        // Act: fold it
        var key = Fold("HeLLo", out var succeeded);

        // Assert: the key is the lower-case word
        Assert.True(succeeded);
        Assert.Equal("hello"u8.ToArray(), key);
    }

    /// <summary>
    ///     Test that Latin-1 capitals are folded to their lower-case partners and encoded as UTF-8.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_Latin1Capital_Lowered()
    {
        // Arrange: a word whose last letter is a Latin-1 capital (E with acute)
        var word = "CAF\u00C9";

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the key is "café" encoded as UTF-8 (the accented letter takes two bytes)
        Assert.True(succeeded);
        Assert.Equal(Encoding.UTF8.GetBytes("caf\u00E9"), key);
    }

    /// <summary>
    ///     Test that the typographic apostrophe is folded to the straight apostrophe.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_TypographicApostrophe_Straightened()
    {
        // Arrange: a contraction written with the typographic apostrophe
        var word = "don\u2019t";

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the key uses the straight apostrophe
        Assert.True(succeeded);
        Assert.Equal("don't"u8.ToArray(), key);
    }

    /// <summary>
    ///     Test that the multiplication sign, which sits in the Latin-1 capitals block, is not changed.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_MultiplicationSign_Unchanged()
    {
        // Arrange: a string with the multiplication sign between two letters
        var word = "a\u00D7b";

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the sign is kept (two UTF-8 bytes) and not shifted into the division sign
        Assert.True(succeeded);
        Assert.Equal(new byte[] { 0x61, 0xC3, 0x97, 0x62 }, key);
    }

    /// <summary>
    ///     Test that a character above Latin-1 makes the word not representable.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_AboveLatin1_ReturnsFalse()
    {
        // Arrange: a word of Japanese characters
        var word = "\u65E5\u672C";

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the word is not representable and no key is produced
        Assert.False(succeeded);
        Assert.Empty(key);
    }

    /// <summary>
    ///     Test that an empty word is not representable.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_Empty_ReturnsFalse()
    {
        // Arrange: the empty word

        // Act: fold it
        var key = Fold(string.Empty, out var succeeded);

        // Assert: the word is not representable
        Assert.False(succeeded);
        Assert.Empty(key);
    }

    /// <summary>
    ///     Test that a word of the maximum length is representable, including when every character needs two bytes.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_MaximumLength_ReturnsTrue()
    {
        // Arrange: a word of 64 characters that each need two UTF-8 bytes
        var word = new string('\u00E9', DictionaryFormat.MaxWordLength);

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the key fills the whole key buffer
        Assert.True(succeeded);
        Assert.Equal(DictionaryFormat.MaxKeyLength, key.Length);
    }

    /// <summary>
    ///     Test that a word one character longer than the maximum is not representable.
    /// </summary>
    [Fact]
    public void DictionaryFormat_TryFold_65Chars_ReturnsFalse()
    {
        // Arrange: a word of 65 letters
        var word = new string('a', DictionaryFormat.MaxWordLength + 1);

        // Act: fold it
        var key = Fold(word, out var succeeded);

        // Assert: the word is not representable
        Assert.False(succeeded);
        Assert.Empty(key);
    }

    /// <summary>
    ///     Test that the hash of known keys matches the values fixed by the format.
    /// </summary>
    /// <param name="word">The word whose UTF-8 bytes are hashed.</param>
    /// <param name="expected">The hash value fixed by the format.</param>
    [Theory]
    [InlineData("", 0xAB3E7C0Bu)]
    [InlineData("a", 0x1A80B1B3u)]
    [InlineData("hello", 0x888D766Eu)]
    [InlineData("caf\u00E9", 0xDF518D52u)]
    public void DictionaryFormat_Hash_KnownVectors_Match(string word, uint expected)
    {
        // Arrange: the UTF-8 bytes of the word
        var bytes = Encoding.UTF8.GetBytes(word);

        // Act: hash them
        var hash = DictionaryFormat.Hash(bytes);

        // Assert: the hash equals the published vector
        Assert.Equal(expected, hash);
    }

    /// <summary>
    ///     Test that hashing the same bytes twice gives the same value.
    /// </summary>
    [Fact]
    public void DictionaryFormat_Hash_SameBytes_SameValue()
    {
        // Arrange: two separate arrays with equal content
        var first = "dictionary"u8.ToArray();
        var second = "dictionary"u8.ToArray();

        // Act: hash both
        var firstHash = DictionaryFormat.Hash(first);
        var secondHash = DictionaryFormat.Hash(second);

        // Assert: the hashes are equal
        Assert.Equal(firstHash, secondHash);
    }

    /// <summary>
    ///     Test that each capitalization form is classified as its own flag.
    /// </summary>
    /// <param name="word">The word as written in a list.</param>
    /// <param name="expected">The expected case-form flag.</param>
    [Theory]
    [InlineData("color", DictionaryFormat.CaseLower)]
    [InlineData("don't", DictionaryFormat.CaseLower)]
    [InlineData("Color", DictionaryFormat.CaseCapitalized)]
    [InlineData("I'm", DictionaryFormat.CaseCapitalized)]
    [InlineData("A", DictionaryFormat.CaseCapitalized)]
    [InlineData("\u00C9cole", DictionaryFormat.CaseCapitalized)]
    [InlineData("COLOR", DictionaryFormat.CaseUpper)]
    [InlineData("\u00C9COLE", DictionaryFormat.CaseUpper)]
    [InlineData("iPhone", DictionaryFormat.CaseMixed)]
    [InlineData("McDonald", DictionaryFormat.CaseMixed)]
    [InlineData("O'Brien", DictionaryFormat.CaseMixed)]
    public void DictionaryFormat_ClassifyCase_CapitalizationForms_ReturnsExpectedFlag(string word, uint expected)
    {
        // Arrange: a word in one capitalization form

        // Act: classify it
        var form = DictionaryFormat.ClassifyCase(word);

        // Assert: exactly the expected flag is returned
        Assert.Equal(expected, form);
    }

    /// <summary>
    ///     Test that letter classification follows the alphabet boundaries, including the Latin-1 exceptions.
    /// </summary>
    [Fact]
    public void DictionaryFormat_IsLetter_AlphabetBoundaries_ClassifiesCharacters()
    {
        // Arrange: characters at the edges of the ASCII and Latin-1 letter ranges

        // Act / Assert: letters are accepted and the adjacent non-letters are rejected
        Assert.Multiple(
            () => Assert.True(DictionaryFormat.IsLetter('a')),
            () => Assert.True(DictionaryFormat.IsLetter('Z')),
            () => Assert.True(DictionaryFormat.IsUpperLetter('\u00C0')),
            () => Assert.True(DictionaryFormat.IsUpperLetter('\u00DE')),
            () => Assert.True(DictionaryFormat.IsLowerLetter('\u00DF')),
            () => Assert.True(DictionaryFormat.IsLowerLetter('\u00FF')),
            () => Assert.False(DictionaryFormat.IsLetter('\u00D7')),
            () => Assert.False(DictionaryFormat.IsLetter('\u00F7')),
            () => Assert.False(DictionaryFormat.IsLetter('@')),
            () => Assert.False(DictionaryFormat.IsLetter('0')),
            () => Assert.False(DictionaryFormat.IsLetter('\u00AA')));
    }

    /// <summary>
    ///     Test that the blob length is rounded up to the alignment.
    /// </summary>
    /// <param name="length">The length to round.</param>
    /// <param name="expected">The expected rounded length.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    public void DictionaryFormat_AlignBlobLength_Lengths_RoundsUpToMultipleOfEight(long length, long expected)
    {
        // Arrange: a length at or around a multiple of the alignment

        // Act: round it up
        var aligned = DictionaryFormat.AlignBlobLength(length);

        // Assert: the result is the next multiple of eight
        Assert.Equal(expected, aligned);
    }

    /// <summary>
    ///     Test that the load limit allows exactly seven tenths of the slots.
    /// </summary>
    [Fact]
    public void DictionaryFormat_IsWithinLoadLimit_AtAndAboveLimit_ReportsLimit()
    {
        // Arrange: ten slots, which hold at most seven words

        // Act: test seven and eight words against ten slots
        var atLimit = DictionaryFormat.IsWithinLoadLimit(7, 10);
        var aboveLimit = DictionaryFormat.IsWithinLoadLimit(8, 10);

        // Assert: seven fit, eight do not
        Assert.True(atLimit);
        Assert.False(aboveLimit);
    }

    /// <summary>
    ///     Test that the format identifier is the four ASCII characters the reader expects.
    /// </summary>
    [Fact]
    public void DictionaryFormat_Magic_Read_IsTld1()
    {
        // Arrange: the identifier property

        // Act: read its bytes
        var magic = DictionaryFormat.Magic.ToArray();

        // Assert: the bytes spell TLD1
        Assert.Equal("TLD1"u8.ToArray(), magic);
    }
}
