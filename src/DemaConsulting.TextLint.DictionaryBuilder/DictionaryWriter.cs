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

using System.Buffers.Binary;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     Serializes a set of folded words into the binary dictionary format: a header, a sorted blob
///     of word records, and an open-addressing hash slot table.
/// </summary>
/// <remarks>
///     The layout lets the library answer a lookup without building any data structure at load time:
///     once the file has been read into a managed byte array, the blob is searched through the slot
///     table with bounds-checked spans. Everything here is deterministic: words
///     are sorted in ordinal order, slots are filled in that order, padding is zero, and no timestamp or
///     machine data is written, so identical word sets give byte-identical files on every platform
///     and a rebuild never causes a spurious change. The class holds no state; every call is
///     independent and safe to run concurrently.
/// </remarks>
internal static class DictionaryWriter
{
    /// <summary>
    ///     The slot count a table starts from before it is doubled until the load limit is met.
    /// </summary>
    private const int InitialSlotCount = 1;

    /// <summary>
    ///     The factor by which the slot count grows; a power of two keeps the slot mask valid.
    /// </summary>
    private const int SlotGrowthFactor = 2;

    /// <summary>
    ///     Serializes the words into the binary dictionary format.
    /// </summary>
    /// <remarks>
    ///     Applies the format's own blob size limit. Allocates the output and some temporary arrays
    ///     proportional to the word count. Thread-safe.
    /// </remarks>
    /// <param name="words">
    ///     The distinct words in folded form, each mapped to its capitalization-form flags. Must not be
    ///     null and must not be empty.
    /// </param>
    /// <returns>The complete dictionary file as a new array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="words"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="words"/> is empty, or contains a word that cannot be stored or is not in folded form.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the words need more blob space than the format allows.</exception>
    internal static byte[] Write(IReadOnlyDictionary<string, uint> words) =>
        Write(words, DictionaryFormat.MaxBlobLength);

    /// <summary>
    ///     Serializes the words into the binary dictionary format with an explicit blob size limit.
    /// </summary>
    /// <remarks>
    ///     The limit is a parameter so that the over-limit error can be verified without a word list
    ///     of a quarter of a gigabyte; production callers use the overload that applies the format's
    ///     limit. Allocates the output and some temporary arrays proportional to the word count.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="words">
    ///     The distinct words in folded form, each mapped to its capitalization-form flags. Must not be
    ///     null and must not be empty.
    /// </param>
    /// <param name="maxBlobLength">The largest blob length in bytes that is acceptable.</param>
    /// <returns>The complete dictionary file as a new array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="words"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="words"/> is empty, or contains a word that cannot be stored or is not in folded form.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the words need more than <paramref name="maxBlobLength"/> blob bytes.</exception>
    internal static byte[] Write(IReadOnlyDictionary<string, uint> words, int maxBlobLength)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count == 0)
        {
            throw new ArgumentException("At least one word is required.", nameof(words));
        }

        // Sort the words in ordinal order; for these characters that equals ordering by their UTF-8 bytes
        var sorted = words.Keys.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);

        // Build the blob and remember where each word starts, its hash, and its case-form flags
        var offsets = new int[sorted.Length];
        var hashes = new uint[sorted.Length];
        using var blobStream = new MemoryStream();
        var key = new byte[DictionaryFormat.MaxKeyLength];
        for (var i = 0; i < sorted.Length; i++)
        {
            var word = sorted[i];
            if (!IsFolded(word))
            {
                throw new ArgumentException($"The word '{word}' is not in folded form.", nameof(words));
            }

            if (!DictionaryFormat.TryFold(word, key, out var keyLength))
            {
                throw new ArgumentException($"The word '{word}' cannot be stored in a dictionary.", nameof(words));
            }

            offsets[i] = (int)blobStream.Length;
            blobStream.Write(key[..keyLength]);
            blobStream.WriteByte(DictionaryFormat.WordTerminator);
            hashes[i] = DictionaryFormat.Hash(key[..keyLength]);

            // Stop as soon as the blob outgrows what a slot can address
            if (blobStream.Length > maxBlobLength)
            {
                throw new InvalidOperationException($"The words need more than {maxBlobLength} bytes of blob space.");
            }
        }

        // Fill the slot table in sorted order so the result never depends on dictionary enumeration order
        var slots = BuildSlots(sorted, words, offsets, hashes);

        return Assemble(blobStream, sorted.Length, slots);
    }

    /// <summary>
    ///     Determines whether a word is already in folded form.
    /// </summary>
    /// <remarks>
    ///     The writer stores folded keys, so an unfolded word would either duplicate its folded twin
    ///     or be stored under a key nobody can look up. Rejecting it protects the uniqueness the
    ///     lookup relies on. Pure function; thread-safe.
    /// </remarks>
    /// <param name="word">The word to check. Must not be null.</param>
    /// <returns><see langword="true"/> when folding would not change any character of the word.</returns>
    private static bool IsFolded(string word) => word.All(character => DictionaryFormat.Fold(character) == character);

    /// <summary>
    ///     Builds the open-addressing slot table for the sorted words.
    /// </summary>
    /// <remarks>
    ///     The slot count is the smallest power of two that keeps the table within the format's load
    ///     limit. Each word goes to the slot its hash selects, or the next free slot after it
    ///     (wrapping), which is exactly the sequence a lookup follows. A slot holds the blob offset
    ///     plus one (so zero means empty) in its low bits and the word's case-form flags in its high
    ///     bits. Allocates the table; thread-safe.
    /// </remarks>
    /// <param name="sorted">The words in ordinal order. Must not be null.</param>
    /// <param name="words">The words with their flags, for the case-form bits. Must not be null.</param>
    /// <param name="offsets">The blob offset of each sorted word. Must not be null.</param>
    /// <param name="hashes">The hash of each sorted word. Must not be null.</param>
    /// <returns>The slot table; its length is a power of two.</returns>
    private static uint[] BuildSlots(string[] sorted, IReadOnlyDictionary<string, uint> words, int[] offsets, uint[] hashes)
    {
        // Choose the smallest power-of-two table that respects the load limit
        long slotCount = InitialSlotCount;
        while (!DictionaryFormat.IsWithinLoadLimit(sorted.Length, slotCount))
        {
            slotCount *= SlotGrowthFactor;
        }

        // Insert in sorted order, probing linearly past occupied slots
        var slots = new uint[slotCount];
        var mask = (uint)(slotCount - 1);
        for (var i = 0; i < sorted.Length; i++)
        {
            var index = hashes[i] & mask;
            while (slots[index] != 0)
            {
                index = (index + 1) & mask;
            }

            slots[index] = (uint)(offsets[i] + 1) | (words[sorted[i]] & DictionaryFormat.CaseFormMask);
        }

        return slots;
    }

    /// <summary>
    ///     Writes the header, the blob with its padding, and the slot table into the final array.
    /// </summary>
    /// <remarks>
    ///     All integers are little endian regardless of the host. The padding between the blob and
    ///     the slot table is left as zero bytes. Allocates the output; thread-safe.
    /// </remarks>
    /// <param name="blobStream">The stream holding the blob. Must not be null.</param>
    /// <param name="wordCount">The number of distinct words.</param>
    /// <param name="slots">The slot table. Must not be null.</param>
    /// <returns>The complete dictionary file.</returns>
    private static byte[] Assemble(MemoryStream blobStream, int wordCount, uint[] slots)
    {
        var blobLength = (int)blobStream.Length;
        var slotsOffset = DictionaryFormat.HeaderLength + (int)DictionaryFormat.AlignBlobLength(blobLength);
        var output = new byte[slotsOffset + (slots.Length * DictionaryFormat.SlotSize)];

        // Header: identifier, flags, counts, hash identifier; the reserved bytes stay zero
        DictionaryFormat.Magic.CopyTo(output.AsSpan(DictionaryFormat.MagicOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(DictionaryFormat.FlagsOffset), DictionaryFormat.NoFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(DictionaryFormat.WordCountOffset), (uint)wordCount);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(DictionaryFormat.BlobLengthOffset), (uint)blobLength);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(DictionaryFormat.SlotCountOffset), (uint)slots.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(DictionaryFormat.HashIdOffset), DictionaryFormat.SupportedHashId);

        // Blob, then the zero padding that aligns the slot table
        blobStream.GetBuffer().AsSpan(0, blobLength).CopyTo(output.AsSpan(DictionaryFormat.HeaderLength));

        // Slot table
        for (var i = 0; i < slots.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(slotsOffset + (i * DictionaryFormat.SlotSize)), slots[i]);
        }

        return output;
    }
}
