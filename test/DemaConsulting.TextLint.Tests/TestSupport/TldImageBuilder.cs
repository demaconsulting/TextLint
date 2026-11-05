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
using System.Text;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.Tests.TestSupport;

/// <summary>
///     Builds tiny dictionary images for tests, so that the reader can be given exact, small, and
///     deliberately damaged inputs.
/// </summary>
/// <remarks>
///     The real dictionaries are produced by the dictionary builder, which this project does not
///     reference. This helper restates the file layout from the format specification in the
///     simplest possible way (a sorted word block and a linear-probing slot table) and takes only
///     the key hash from the shared format definition, which is a documented dependency of the
///     dictionary reader. Tests call <see cref="Build"/> and then change individual bytes to
///     produce each kind of damaged image. Stateless and thread-safe.
/// </remarks>
internal static class TldImageBuilder
{
    /// <summary>
    ///     The header offset of the flags field.
    /// </summary>
    internal const int FlagsOffset = 4;

    /// <summary>
    ///     The header offset of the word count field.
    /// </summary>
    internal const int WordCountOffset = 8;

    /// <summary>
    ///     The header offset of the blob length field.
    /// </summary>
    internal const int BlobLengthOffset = 12;

    /// <summary>
    ///     The header offset of the slot count field.
    /// </summary>
    internal const int SlotCountOffset = 16;

    /// <summary>
    ///     The header offset of the hash identifier field.
    /// </summary>
    internal const int HashIdOffset = 20;

    /// <summary>
    ///     The header offset of the first reserved byte.
    /// </summary>
    internal const int ReservedOffset = 24;

    /// <summary>
    ///     The length of the header in bytes.
    /// </summary>
    internal const int HeaderLength = 32;

    /// <summary>
    ///     The size of one slot in bytes.
    /// </summary>
    internal const int SlotSize = 4;

    /// <summary>
    ///     The alignment of the slot table in bytes.
    /// </summary>
    private const int Alignment = 8;

    /// <summary>
    ///     The smallest slot count the helper chooses; keeping the table at least this large keeps it sparse.
    /// </summary>
    private const int MinimumSlotCount = 8;

    /// <summary>
    ///     The number of slots the helper provides per word, which keeps the load far below the format's limit.
    /// </summary>
    private const int SlotsPerWord = 4;

    /// <summary>
    ///     The identifier of the hash that the format uses, restated from the format specification.
    /// </summary>
    private const uint HashIdentifier = 1;

    /// <summary>
    ///     The slot flag that records a lower-case word, restated from the format specification.
    /// </summary>
    private const uint CaseLowerFlag = 1u << 28;

    /// <summary>
    ///     Builds a valid dictionary image holding the given words.
    /// </summary>
    /// <param name="words">The words to store; must already be in folded (lower-case) form. Duplicates are stored once.</param>
    /// <param name="slotCount">The number of slots, a power of two; zero selects a sparse table automatically.</param>
    /// <returns>The bytes of the dictionary file.</returns>
    internal static byte[] Build(IEnumerable<string> words, int slotCount = 0)
    {
        // Sort the distinct words by their bytes and lay them out as terminated records
        var sorted = words.Distinct().Order(StringComparer.Ordinal).ToArray();
        var blob = new List<byte>();
        var offsets = new int[sorted.Length];
        for (var i = 0; i < sorted.Length; i++)
        {
            offsets[i] = blob.Count;
            blob.AddRange(Encoding.UTF8.GetBytes(sorted[i]));
            blob.Add((byte)'\n');
        }

        // Choose the slot count: several slots per word keep the load far below the limit
        var slots = slotCount > 0 ? slotCount : ChooseSlotCount(sorted.Length);
        var padded = (blob.Count + Alignment - 1) / Alignment * Alignment;
        var image = new byte[HeaderLength + padded + (SlotSize * slots)];

        // Write the header
        "TLD1"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(WordCountOffset), (uint)sorted.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(BlobLengthOffset), (uint)blob.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(SlotCountOffset), (uint)slots);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(HashIdOffset), HashIdentifier);
        blob.CopyTo(image, HeaderLength);

        // Insert each word at its home slot, probing forward past occupied slots
        var slotsOffset = HeaderLength + padded;
        for (var i = 0; i < sorted.Length; i++)
        {
            var index = HomeSlot(sorted[i], slots);
            while (BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(slotsOffset + (index * SlotSize))) != 0)
            {
                index = (index + 1) & (slots - 1);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(
                image.AsSpan(slotsOffset + (index * SlotSize)),
                (uint)(offsets[i] + 1) | CaseLowerFlag);
        }

        return image;
    }

    /// <summary>
    ///     Gets the offset in an image at which the slot table starts.
    /// </summary>
    /// <param name="image">An image produced by <see cref="Build"/>.</param>
    /// <returns>The byte offset of the first slot.</returns>
    internal static int SlotsOffset(byte[] image) =>
        HeaderLength + ((int)BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(BlobLengthOffset)) + Alignment - 1) / Alignment * Alignment;

    /// <summary>
    ///     Gets the slot at which a word starts probing in a table of a given size.
    /// </summary>
    /// <param name="word">The folded word.</param>
    /// <param name="slotCount">The number of slots, a power of two.</param>
    /// <returns>The index of the home slot.</returns>
    internal static int HomeSlot(string word, int slotCount) =>
        (int)(DictionaryFormat.Hash(Encoding.UTF8.GetBytes(word)) & (uint)(slotCount - 1));

    /// <summary>
    ///     Chooses a power-of-two slot count that keeps the table sparse.
    /// </summary>
    /// <param name="wordCount">The number of words to store.</param>
    /// <returns>The slot count.</returns>
    private static int ChooseSlotCount(int wordCount)
    {
        var slots = MinimumSlotCount;
        while (slots < wordCount * SlotsPerWord)
        {
            slots *= 2;
        }

        return slots;
    }
}
