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

// cspell:ignore alph alphab colour xyzzyq

using System.Buffers.Binary;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.Dictionaries;

/// <summary>
///     Unit tests for the <see cref="DictionaryImage"/> class.
/// </summary>
/// <remarks>
///     The tests build tiny dictionary images with <see cref="TldImageBuilder"/> so each behavior
///     can be checked on an exact, small input, and damage the images byte by byte to prove that
///     every kind of corruption is reported by name. The documented dependencies are the shared
///     <see cref="DictionaryFormat"/> definitions and, for the embedded-resource tests, the
///     resources of the library assembly and of this test assembly.
/// </remarks>
public class DictionaryImageTests
{
    /// <summary>
    ///     The name given to images built in tests, which damage messages must contain.
    /// </summary>
    private const string ImageName = "test-dictionary";

    /// <summary>
    ///     The logical name of a real embedded dictionary of the library.
    /// </summary>
    private const string BritishResource = "DemaConsulting.TextLint.Dictionaries.en-GB.tld";

    /// <summary>
    ///     The logical name of the damaged resource embedded in this test assembly.
    /// </summary>
    private const string CorruptResource = "DemaConsulting.TextLint.Tests.corrupt-dictionary.tld";

    /// <summary>
    ///     The slot count used by the collision test, small enough that collisions are easy to find.
    /// </summary>
    private const int SmallSlotCount = 8;

    /// <summary>
    ///     A slot value whose offset bits are far beyond any word block.
    /// </summary>
    private const uint FarOffsetSlot = 0x00FFFFFF;

    /// <summary>
    ///     A slot value with case flags but no offset, which cannot point anywhere.
    /// </summary>
    private const uint NoOffsetSlot = 1u << 28;

    /// <summary>
    ///     A slot value that points at the second byte of the first word, which is not the start of a word.
    /// </summary>
    private const uint SecondByteSlot = 2;

    /// <summary>
    ///     The word-block offset of "beta" in an image of "alpha" and "beta" (after "alpha" and its terminator).
    /// </summary>
    private const uint BetaOffset = 6;

    /// <summary>
    ///     The word-block offset of "gamma" in an image of "alpha", "beta", and "gamma".
    /// </summary>
    private const uint GammaOffset = 11;

    /// <summary>
    ///     The mask of the slot bits that hold the word-block offset plus one.
    /// </summary>
    private const uint OffsetBitsMask = 0x0FFFFFFF;

    /// <summary>
    ///     A blob length beyond what the format allows.
    /// </summary>
    private const uint OversizedBlobLength = 0x10000000;

    /// <summary>
    ///     Looks a word up in an image the way the spelling dictionary does: fold, hash, then probe.
    /// </summary>
    /// <param name="image">The image to ask.</param>
    /// <param name="word">The word to look up; must be representable.</param>
    /// <returns><see langword="true"/> when the image stores the word.</returns>
    private static bool Contains(DictionaryImage image, string word)
    {
        Span<byte> buffer = stackalloc byte[DictionaryFormat.MaxKeyLength];
        Assert.True(DictionaryFormat.TryFold(word, buffer, out var length));
        var key = buffer[..length];
        return image.Contains(key, DictionaryFormat.Hash(key));
    }

    /// <summary>
    ///     Asserts that an image is rejected and that the error names the dictionary and gives the reason.
    /// </summary>
    /// <param name="data">The damaged image.</param>
    /// <param name="reason">A fragment of the reason the error must state.</param>
    private static void AssertRejected(byte[] data, string reason)
    {
        var exception = Assert.Throws<InvalidDataException>(() => new DictionaryImage(ImageName, data));
        Assert.Multiple(
            () => Assert.Contains(ImageName, exception.Message),
            () => Assert.Contains(reason, exception.Message));
    }

    /// <summary>
    ///     Finds two distinct words that start probing at the same slot of a small table.
    /// </summary>
    /// <param name="slotCount">The number of slots of the table.</param>
    /// <returns>Two words with the same home slot.</returns>
    private static (string First, string Second) FindCollidingWords(int slotCount)
    {
        // Try two-letter words in order until a second word lands on a slot that is already taken
        var homes = new Dictionary<int, string>();
        for (var first = 'a'; first <= 'z'; first++)
        {
            for (var second = 'a'; second <= 'z'; second++)
            {
                var word = $"{first}{second}";
                var home = TldImageBuilder.HomeSlot(word, slotCount);
                if (!homes.TryAdd(home, word))
                {
                    return (homes[home], word);
                }
            }
        }

        throw new InvalidOperationException("No colliding pair exists.");
    }

    /// <summary>
    ///     Test that every stored word is found, including the first and the last in the word block.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_EveryWordInImage_IsTrue()
    {
        // Arrange: an image of several words
        string[] words = ["alpha", "beta", "delta", "epsilon", "gamma"];
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build(words));

        // Act / Assert: every stored word is found
        Assert.All(words, word => Assert.True(Contains(image, word), word));
    }

    /// <summary>
    ///     Test that a word that is not stored is not found.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_WordNotInImage_IsFalse()
    {
        // Arrange: an image that does not hold "omega"
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build(["alpha", "beta", "gamma"]));

        // Act: look up a word that is not stored
        var found = Contains(image, "omega");

        // Assert: it is not found
        Assert.False(found);
    }

    /// <summary>
    ///     Test that a prefix of a stored word, and a word that extends a stored word, are not found.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_PrefixOfStoredWord_IsFalse()
    {
        // Arrange: an image of a word with both a shorter and a longer neighbor in spelling
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build(["alpha", "alphabet"]));

        // Act / Assert: a prefix is not a match, but the stored words are
        Assert.Multiple(
            () => Assert.False(Contains(image, "alph")),
            () => Assert.False(Contains(image, "alphab")),
            () => Assert.True(Contains(image, "alpha")),
            () => Assert.True(Contains(image, "alphabet")));
    }

    /// <summary>
    ///     Test that a key longer than everything that remains in the word block is not found and does not read past it.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_KeyLongerThanStoredWord_IsFalse()
    {
        // Arrange: an image whose only word ends at the end of the word block
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build(["alpha"]));

        // Act: look up a longer word that starts with the stored word
        var found = Contains(image, "alphabets");

        // Assert: it is not found
        Assert.False(found);
    }

    /// <summary>
    ///     Test that words whose keys start probing at the same slot are both found.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_CollidingKeys_FoundByProbing()
    {
        // Arrange: two words with the same home slot in a small table
        var (first, second) = FindCollidingWords(SmallSlotCount);
        Assert.Equal(TldImageBuilder.HomeSlot(first, SmallSlotCount), TldImageBuilder.HomeSlot(second, SmallSlotCount));
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build([first, second], SmallSlotCount));

        // Act / Assert: the probe finds both
        Assert.Multiple(
            () => Assert.True(Contains(image, first)),
            () => Assert.True(Contains(image, second)));
    }

    /// <summary>
    ///     Test that an image with no words answers false for every word.
    /// </summary>
    [Fact]
    public void DictionaryImage_Contains_EmptyImage_IsFalse()
    {
        // Arrange: an image with no words
        var image = new DictionaryImage(ImageName, TldImageBuilder.Build([]));

        // Act: look up a word
        var found = Contains(image, "alpha");

        // Assert: it is not found
        Assert.False(found);
    }

    /// <summary>
    ///     Test that a slot that points outside the word block is rejected when the image is loaded.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotOffsetOutsideBlob_ThrowsInvalidData()
    {
        // Arrange: an image whose only occupied slot is changed to point far beyond the word block
        var data = TldImageBuilder.Build(["alpha"]);
        SetOccupiedSlot(data, TldImageBuilder.SlotsOffset(data), FarOffsetSlot);

        // Act / Assert: the image is rejected
        AssertRejected(data, "start of a word");
    }

    /// <summary>
    ///     Test that a slot with case flags but no offset is rejected when the image is loaded.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotWithoutOffset_ThrowsInvalidData()
    {
        // Arrange: an image whose only occupied slot has no offset
        var data = TldImageBuilder.Build(["alpha"]);
        SetOccupiedSlot(data, TldImageBuilder.SlotsOffset(data), NoOffsetSlot);

        // Act / Assert: the image is rejected
        AssertRejected(data, "start of a word");
    }

    /// <summary>
    ///     Test that a slot that points into the middle of a word is rejected when the image is loaded.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotInsideWord_ThrowsInvalidData()
    {
        // Arrange: an image whose only occupied slot is changed to point at the second letter of its word
        var data = TldImageBuilder.Build(["alpha"]);
        SetOccupiedSlot(data, TldImageBuilder.SlotsOffset(data), SecondByteSlot);

        // Act / Assert: the image is rejected
        AssertRejected(data, "start of a word");
    }

    /// <summary>
    ///     Test that a header word count that does not match the terminators of the word block is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_WordCountMismatchesBlob_ThrowsInvalidData()
    {
        // Arrange: a valid image whose header claims two words although the block holds one
        var data = TldImageBuilder.Build(["alpha"], SmallSlotCount);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.WordCountOffset), 2);

        // Act / Assert: the image is rejected
        AssertRejected(data, "does not match its word block");
    }

    /// <summary>
    ///     Test that a header that claims a word but has an empty word block is rejected, so it cannot pass as an empty image.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_WordCountWithoutBlob_ThrowsInvalidData()
    {
        // Arrange: a header of a hand-made image with one word claimed, no word block, and two empty slots
        var data = new byte[TldImageBuilder.HeaderLength + (2 * TldImageBuilder.SlotSize)];
        "TLD1"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.WordCountOffset), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.SlotCountOffset), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.HashIdOffset), 1);

        // Act / Assert: the image is rejected
        AssertRejected(data, "does not match its word block");
    }

    /// <summary>
    ///     Test that a slot table whose occupied slots do not match the word count is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotTableMismatchesWordCount_ThrowsInvalidData()
    {
        // Arrange: a two-word image in which the slot of the second word is emptied, so only one slot is occupied
        var data = TldImageBuilder.Build(["alpha", "beta"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(FindSlot(data, BetaOffset)), 0);

        // Act / Assert: the image is rejected
        AssertRejected(data, "does not match its slot table");
    }

    /// <summary>
    ///     Test that two slots that hold the same offset are rejected, even though the counts all agree.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_DuplicateSlotOffset_ThrowsInvalidData()
    {
        // Arrange: a two-word image in which the slot of the second word is overwritten with the slot of the first
        var data = TldImageBuilder.Build(["alpha", "beta"]);
        CopySlot(data, fromOffset: 0, toOffset: BetaOffset);

        // Act / Assert: the image is rejected
        AssertRejected(data, "same word");
    }

    /// <summary>
    ///     Test that a word that no slot references is rejected, which a duplicated slot elsewhere would otherwise hide.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_WordWithoutSlot_ThrowsInvalidData()
    {
        // Arrange: a three-word image in which the slot of the last word is redirected to the first word, so the
        // last word is unreferenced while every count and every offset is still valid
        var data = TldImageBuilder.Build(["alpha", "beta", "gamma"]);
        CopySlot(data, fromOffset: 0, toOffset: GammaOffset);

        // Act / Assert: the image is rejected
        AssertRejected(data, "another word has no slot");
    }

    /// <summary>
    ///     Test that a valid image is accepted by validation.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_ValidImage_IsAccepted()
    {
        // Arrange: a valid image
        var data = TldImageBuilder.Build(["alpha", "beta"]);

        // Act: construct the dictionary image
        var image = new DictionaryImage(ImageName, data);

        // Assert: the stored words are found, so the image is usable
        Assert.True(Contains(image, "beta"));
    }

    /// <summary>
    ///     Test that a null name is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_NullName_ThrowsArgumentNullException()
    {
        // Arrange: a valid image
        var data = TldImageBuilder.Build(["alpha"]);

        // Act / Assert: a null name is rejected
        Assert.Throws<ArgumentNullException>(() => new DictionaryImage(null!, data));
    }

    /// <summary>
    ///     Test that null data is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_NullData_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: null data is rejected
        Assert.Throws<ArgumentNullException>(() => new DictionaryImage(ImageName, null!));
    }

    /// <summary>
    ///     Test that data shorter than the header is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_TruncatedHeader_ThrowsInvalidData()
    {
        // Arrange: fewer bytes than a header
        var data = new byte[TldImageBuilder.HeaderLength - 1];

        // Act / Assert: the image is rejected, naming the dictionary
        AssertRejected(data, "shorter than the header");
    }

    /// <summary>
    ///     Test that data with the wrong identifier is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_BadMagic_ThrowsInvalidData()
    {
        // Arrange: a valid image whose identifier is changed
        var data = TldImageBuilder.Build(["alpha"]);
        data[0] = (byte)'X';

        // Act / Assert: the image is rejected
        AssertRejected(data, "identifier");
    }

    /// <summary>
    ///     Test that an image that asks for an unsupported option is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_UnsupportedFlags_ThrowsInvalidData()
    {
        // Arrange: a valid image with an option flag set
        var data = TldImageBuilder.Build(["alpha"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.FlagsOffset), 1);

        // Act / Assert: the image is rejected
        AssertRejected(data, "options");
    }

    /// <summary>
    ///     Test that an image that uses an unsupported hash is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_UnsupportedHash_ThrowsInvalidData()
    {
        // Arrange: a valid image with another hash identifier
        var data = TldImageBuilder.Build(["alpha"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.HashIdOffset), 2);

        // Act / Assert: the image is rejected
        AssertRejected(data, "hash");
    }

    /// <summary>
    ///     Test that an image with a non-zero reserved byte is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_ReservedBytesNotZero_ThrowsInvalidData()
    {
        // Arrange: a valid image with a reserved byte set
        var data = TldImageBuilder.Build(["alpha"]);
        data[TldImageBuilder.ReservedOffset] = 1;

        // Act / Assert: the image is rejected
        AssertRejected(data, "reserved");
    }

    /// <summary>
    ///     Test that a slot count of zero is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotCountZero_ThrowsInvalidData()
    {
        // Arrange: a valid image whose slot count is changed to zero
        var data = TldImageBuilder.Build(["alpha"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.SlotCountOffset), 0);

        // Act / Assert: the image is rejected
        AssertRejected(data, "power of two");
    }

    /// <summary>
    ///     Test that a slot count that is not a power of two is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_SlotCountNotPowerOfTwo_ThrowsInvalidData()
    {
        // Arrange: a valid image whose slot count is changed to six
        var data = TldImageBuilder.Build(["alpha"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.SlotCountOffset), 6);

        // Act / Assert: the image is rejected
        AssertRejected(data, "power of two");
    }

    /// <summary>
    ///     Test that a word count beyond the load limit of the slot table is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_WordCountOverLoadLimit_ThrowsInvalidData()
    {
        // Arrange: a valid image that claims as many words as it has slots
        var data = TldImageBuilder.Build(["alpha"], SmallSlotCount);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.WordCountOffset), SmallSlotCount);

        // Act / Assert: the image is rejected
        AssertRejected(data, "load limit");
    }

    /// <summary>
    ///     Test that a word block larger than the format allows is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_BlobTooLarge_ThrowsInvalidData()
    {
        // Arrange: a valid image that claims an oversized word block
        var data = TldImageBuilder.Build(["alpha"]);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(TldImageBuilder.BlobLengthOffset), OversizedBlobLength);

        // Act / Assert: the image is rejected
        AssertRejected(data, "larger than the format allows");
    }

    /// <summary>
    ///     Test that an image cut short is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_TruncatedImage_ThrowsInvalidData()
    {
        // Arrange: a valid image missing its last byte
        var data = TldImageBuilder.Build(["alpha"]);
        var truncated = data[..^1];

        // Act / Assert: the image is rejected
        AssertRejected(truncated, "length does not match");
    }

    /// <summary>
    ///     Test that an image with trailing bytes is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_TrailingBytes_ThrowsInvalidData()
    {
        // Arrange: a valid image with an extra byte at the end
        var data = TldImageBuilder.Build(["alpha"]);
        var extended = new byte[data.Length + 1];
        data.CopyTo(extended, 0);

        // Act / Assert: the image is rejected
        AssertRejected(extended, "length does not match");
    }

    /// <summary>
    ///     Test that a word block whose last byte is not a terminator is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_New_BlobNotTerminated_ThrowsInvalidData()
    {
        // Arrange: a valid image whose last word terminator is changed to a letter
        var data = TldImageBuilder.Build(["alpha"]);
        var blobLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(TldImageBuilder.BlobLengthOffset));
        data[TldImageBuilder.HeaderLength + blobLength - 1] = (byte)'x';

        // Act / Assert: the image is rejected
        AssertRejected(data, "terminator");
    }

    /// <summary>
    ///     Test that a real embedded dictionary of the library loads and answers lookups.
    /// </summary>
    [Fact]
    public void DictionaryImage_LoadEmbedded_ExistingResource_Loads()
    {
        // Arrange: the library assembly, which embeds the British dictionary
        var assembly = typeof(DictionaryImage).Assembly;

        // Act: load the dictionary by its resource name
        var image = DictionaryImage.LoadEmbedded(assembly, BritishResource);

        // Assert: a British spelling is found and a nonsense word is not
        Assert.Multiple(
            () => Assert.True(Contains(image, "colour")),
            () => Assert.False(Contains(image, "xyzzyq")));
    }

    /// <summary>
    ///     Test that a resource that does not exist is reported by name.
    /// </summary>
    [Fact]
    public void DictionaryImage_LoadEmbedded_MissingResource_ThrowsNamingResource()
    {
        // Arrange: a resource name the assembly does not have
        const string missing = "DemaConsulting.TextLint.Dictionaries.en-XX.tld";

        // Act: try to load it
        var exception = Assert.Throws<InvalidDataException>(
            () => DictionaryImage.LoadEmbedded(typeof(DictionaryImage).Assembly, missing));

        // Assert: the error names the resource
        Assert.Contains(missing, exception.Message);
    }

    /// <summary>
    ///     Test that an embedded resource that is not a valid dictionary is reported by name.
    /// </summary>
    [Fact]
    public void DictionaryImage_LoadEmbedded_CorruptResource_ThrowsNamingResource()
    {
        // Arrange: the damaged resource embedded in this test assembly
        var assembly = typeof(DictionaryImageTests).Assembly;

        // Act: try to load it
        var exception = Assert.Throws<InvalidDataException>(() => DictionaryImage.LoadEmbedded(assembly, CorruptResource));

        // Assert: the error names the resource and says it is damaged (not that it is missing)
        Assert.Multiple(
            () => Assert.Contains(CorruptResource, exception.Message),
            () => Assert.Contains("damaged or unsupported", exception.Message));
    }

    /// <summary>
    ///     Test that a null assembly is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_LoadEmbedded_NullAssembly_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: a null assembly is rejected
        Assert.Throws<ArgumentNullException>(() => DictionaryImage.LoadEmbedded(null!, BritishResource));
    }

    /// <summary>
    ///     Test that a null resource name is rejected.
    /// </summary>
    [Fact]
    public void DictionaryImage_LoadEmbedded_NullResourceName_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: a null resource name is rejected
        Assert.Throws<ArgumentNullException>(() => DictionaryImage.LoadEmbedded(typeof(DictionaryImage).Assembly, null!));
    }

    /// <summary>
    ///     Copies the whole value of the slot that references one word over the slot that references another.
    /// </summary>
    /// <param name="data">The image, which is changed in place.</param>
    /// <param name="fromOffset">The word-block offset of the word whose slot is copied.</param>
    /// <param name="toOffset">The word-block offset of the word whose slot is overwritten.</param>
    private static void CopySlot(byte[] data, uint fromOffset, uint toOffset)
    {
        var from = FindSlot(data, fromOffset);
        var to = FindSlot(data, toOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(to), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(from)));
    }

    /// <summary>
    ///     Finds the position of the slot that references the word at a word-block offset.
    /// </summary>
    /// <param name="data">The image.</param>
    /// <param name="wordOffset">The word-block offset of the word.</param>
    /// <returns>The byte position of its slot within the image.</returns>
    private static int FindSlot(byte[] data, uint wordOffset)
    {
        // Search the slot table for the slot that holds the offset plus one in its low bits
        for (var position = TldImageBuilder.SlotsOffset(data); position < data.Length; position += TldImageBuilder.SlotSize)
        {
            var slot = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
            if (slot != 0 && (slot & OffsetBitsMask) - 1 == wordOffset)
            {
                return position;
            }
        }

        throw new InvalidOperationException($"No slot references the word at offset {wordOffset}.");
    }

    /// <summary>
    ///     Replaces every occupied slot of an image with a given value.
    /// </summary>
    /// <param name="data">The image, which is changed in place.</param>
    /// <param name="slotsOffset">The offset at which the slot table starts.</param>
    /// <param name="value">The value to store in each occupied slot.</param>
    private static void SetOccupiedSlot(byte[] data, int slotsOffset, uint value)
    {
        // Overwrite each slot that is not empty
        for (var offset = slotsOffset; offset < data.Length; offset += TldImageBuilder.SlotSize)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            }
        }
    }
}
