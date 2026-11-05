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
using DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="DictionaryWriter"/> class.
/// </summary>
/// <remarks>
///     The writer is a pure function from a word set to bytes. What it wrote is judged by the
///     independent <see cref="TldReader"/> from the test support, which restates the format from its
///     specification, so an error in the writer's own definitions cannot hide itself. The writer's
///     documented dependency, <see cref="DictionaryFormat"/>, is used as it is.
/// </remarks>
public class DictionaryWriterTests
{
    /// <summary>
    ///     The fixed seed of the random generator that makes the large word set repeatable.
    /// </summary>
    private const int RandomSeed = 20260201;

    /// <summary>
    ///     The number of random words in the large word set.
    /// </summary>
    private const int LargeWordCount = 5000;

    /// <summary>
    ///     The number of letters in a generated word.
    /// </summary>
    private const int GeneratedWordLength = 8;

    /// <summary>
    ///     Builds a word set in which every word has the same case-form flag.
    /// </summary>
    /// <param name="words">The folded words.</param>
    /// <returns>The word set.</returns>
    private static Dictionary<string, uint> LowerWords(params string[] words) =>
        words.ToDictionary(word => word, _ => DictionaryFormat.CaseLower, StringComparer.Ordinal);

    /// <summary>
    ///     Generates a repeatable set of distinct random lower-case words.
    /// </summary>
    /// <returns>The word set, every word flagged lower case.</returns>
    private static Dictionary<string, uint> GenerateWords()
    {
        // A fixed seed makes the set, and so any failure, identical on every run
        var random = new Random(RandomSeed);
        var words = new Dictionary<string, uint>(StringComparer.Ordinal);
        while (words.Count < LargeWordCount)
        {
            var letters = Enumerable.Range(0, GeneratedWordLength).Select(_ => (char)('a' + random.Next(26))).ToArray();
            words[new string(letters)] = DictionaryFormat.CaseLower;
        }

        return words;
    }

    /// <summary>
    ///     Test that a small word set produces the expected header, blob, and file length.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_FewWords_ProducesExpectedHeaderAndBlob()
    {
        // Arrange: two words, which need four slots at the seven-tenths load limit
        var words = LowerWords("banana", "apple");

        // Act: write them and read the result back independently
        var bytes = DictionaryWriter.Write(words);
        var reader = new TldReader(bytes);

        // Assert: header fields, sorted blob, and total length (32 header + 16 blob with padding + 16 slots)
        Assert.Multiple(
            () => Assert.Equal(0u, reader.Flags),
            () => Assert.Equal(2u, reader.WordCount),
            () => Assert.Equal(13, reader.BlobLength),
            () => Assert.Equal(4u, reader.SlotCount),
            () => Assert.Equal(1u, reader.HashId),
            () => Assert.Equal(["apple", "banana"], reader.Words),
            () => Assert.Equal(64, bytes.Length));
    }

    /// <summary>
    ///     Test that every word of a large repeatable set can be found by probing.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_ManyWords_EveryKeyReachableByProbing()
    {
        // Arrange: thousands of random words, which force many probe collisions
        var words = GenerateWords();

        // Act: write them and read the result back independently
        var reader = new TldReader(DictionaryWriter.Write(words));

        // Assert: every word is found, in the blob in sorted order, and an absent word is not found
        Assert.All(words.Keys, word => Assert.True(reader.TryFind(word, out _), word));
        Assert.Equal(words.Keys.Order(StringComparer.Ordinal), reader.Words);
        Assert.False(reader.TryFind("zzzzzzzzzzzz", out _));
    }

    /// <summary>
    ///     Test that the slot count is the smallest power of two within the load limit.
    /// </summary>
    /// <param name="wordCount">The number of words written.</param>
    /// <param name="expectedSlotCount">The expected slot count.</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(7, 16)]
    [InlineData(8, 16)]
    [InlineData(11, 16)]
    [InlineData(12, 32)]
    [InlineData(100, 256)]
    public void DictionaryWriter_Write_SlotCount_PowerOfTwoWithinLoadLimit(int wordCount, int expectedSlotCount)
    {
        // Arrange: a set of the given number of distinct words
        var words = Enumerable.Range(0, wordCount).ToDictionary(
            index => "w" + new string((char)('a' + (index % 26)), (index / 26) + 1),
            _ => DictionaryFormat.CaseLower,
            StringComparer.Ordinal);

        // Act: write them
        var reader = new TldReader(DictionaryWriter.Write(words));

        // Assert: the slot count is the smallest power of two that keeps the load at or below seven tenths
        Assert.Equal((uint)expectedSlotCount, reader.SlotCount);
        Assert.True(wordCount * 10L <= reader.SlotCount * 7L);
    }

    /// <summary>
    ///     Test that the capitalization-form flags are stored in the high bits of the word's slot.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_CaseForms_StoredInSlotBits()
    {
        // Arrange: words with different recorded forms
        var words = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["color"] = DictionaryFormat.CaseLower,
            ["paris"] = DictionaryFormat.CaseLower | DictionaryFormat.CaseCapitalized,
            ["nasa"] = DictionaryFormat.CaseUpper,
            ["iphone"] = DictionaryFormat.CaseMixed
        };

        // Act: write them and look each one up
        var reader = new TldReader(DictionaryWriter.Write(words));

        // Assert: each word's slot carries exactly the flags it was given
        Assert.All(words, pair =>
        {
            Assert.True(reader.TryFind(pair.Key, out var flags));
            Assert.Equal(pair.Value, flags);
        });
    }

    /// <summary>
    ///     Test that writing the same input twice gives identical bytes.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_SameInput_SameBytes()
    {
        // Arrange: a word set
        var words = GenerateWords();

        // Act: write it twice
        var first = DictionaryWriter.Write(words);
        var second = DictionaryWriter.Write(words);

        // Assert: the two files are byte-identical
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Test that the order in which words were added does not change the output.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_DifferentInsertionOrder_SameBytes()
    {
        // Arrange: the same words added in ascending and in descending order
        var ascending = new Dictionary<string, uint>(StringComparer.Ordinal);
        var descending = new Dictionary<string, uint>(StringComparer.Ordinal);
        var names = Enumerable.Range(0, 200).Select(index => "word" + new string('x', index % 7) + new string('y', index / 7)).ToArray();
        foreach (var name in names)
        {
            ascending[name] = DictionaryFormat.CaseLower;
        }

        foreach (var name in names.Reverse())
        {
            descending[name] = DictionaryFormat.CaseLower;
        }

        // Act: write both
        var first = DictionaryWriter.Write(ascending);
        var second = DictionaryWriter.Write(descending);

        // Assert: the files are byte-identical
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Test that an empty word set is rejected.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_EmptyInput_Throws()
    {
        // Arrange: an empty word set
        var words = new Dictionary<string, uint>();

        // Act / Assert: writing it is rejected with an argument error
        Assert.Throws<ArgumentException>(() => DictionaryWriter.Write(words));
    }

    /// <summary>
    ///     Test that a null word set is rejected.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_NullInput_Throws()
    {
        // Arrange: no word set

        // Act / Assert: writing null is rejected with a null argument error
        Assert.Throws<ArgumentNullException>(() => DictionaryWriter.Write(null!));
    }

    /// <summary>
    ///     Test that a blob larger than the limit is rejected.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_BlobTooLarge_Throws()
    {
        // Arrange: three words that need 15 blob bytes, and a limit of 14 bytes
        var words = LowerWords("apple", "pear", "fig");

        // Act / Assert: the over-limit blob is rejected with an error that names the limit
        var exception = Assert.Throws<InvalidOperationException>(() => DictionaryWriter.Write(words, 14));
        Assert.Contains("14", exception.Message);
    }

    /// <summary>
    ///     Test that a blob exactly at the limit is accepted.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_BlobAtLimit_Succeeds()
    {
        // Arrange: three words that need exactly 15 blob bytes, and a limit of 15 bytes
        var words = LowerWords("apple", "pear", "fig");

        // Act: write them
        var reader = new TldReader(DictionaryWriter.Write(words, 15));

        // Assert: the blob has the expected length
        Assert.Equal(15, reader.BlobLength);
    }

    /// <summary>
    ///     Test that the padding after the blob and the reserved header bytes are zero.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_Padding_IsZero()
    {
        // Arrange: one word, whose 6-byte blob needs two bytes of padding
        var words = LowerWords("apple");

        // Act: write it
        var reader = new TldReader(DictionaryWriter.Write(words));

        // Assert: the blob is not a multiple of eight and every padding and reserved byte is zero
        Assert.Equal(6, reader.BlobLength);
        Assert.True(reader.ReservedAndPaddingAreZero);
    }

    /// <summary>
    ///     Test that a word with a Latin-1 letter is stored as UTF-8.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_Latin1Word_EncodedAsUtf8()
    {
        // Arrange: a folded word with an accented letter
        var words = LowerWords("caf\u00E9");

        // Act: write it
        var bytes = DictionaryWriter.Write(words);
        var reader = new TldReader(bytes);

        // Assert: the blob holds the two-byte UTF-8 form, and the word is found in any letter case
        Assert.Equal(Encoding.UTF8.GetByteCount("caf\u00E9\n"), reader.BlobLength);
        Assert.True(reader.TryFind("CAF\u00C9", out _));
    }

    /// <summary>
    ///     Test that a word that is not in folded form is rejected.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_UnfoldedWord_Throws()
    {
        // Arrange: a word set that contains an upper-case letter
        var words = LowerWords("Apple");

        // Act / Assert: the word is rejected
        var exception = Assert.Throws<ArgumentException>(() => DictionaryWriter.Write(words));
        Assert.Contains("Apple", exception.Message);
    }

    /// <summary>
    ///     Test that a word that cannot be a key is rejected.
    /// </summary>
    [Fact]
    public void DictionaryWriter_Write_UnrepresentableWord_Throws()
    {
        // Arrange: a word set that contains a word of Japanese characters
        var words = LowerWords("\u65E5\u672C");

        // Act / Assert: the word is rejected
        Assert.Throws<ArgumentException>(() => DictionaryWriter.Write(words));
    }
}
