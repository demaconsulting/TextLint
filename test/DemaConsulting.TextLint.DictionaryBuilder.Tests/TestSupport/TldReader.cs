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

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

/// <summary>
///     An independent, minimal reader of the binary dictionary format, used to check what the
///     builder wrote.
/// </summary>
/// <remarks>
///     This reader deliberately shares no code with the builder: the header offsets, the slot
///     encoding, the hash, and the case fold (the invariant lower-casing of the .NET base class
///     library) are all written out again from the format specification. A mistake in the builder's
///     own definitions therefore cannot cancel itself out in a test. It is test support, not a
///     shipped component, and it validates only what the tests need.
/// </remarks>
internal sealed class TldReader
{
    /// <summary>
    ///     The length of the file header in bytes.
    /// </summary>
    private const int HeaderLength = 32;

    /// <summary>
    ///     The header offset of the flags field, restated from the format specification.
    /// </summary>
    private const int FlagsOffset = 4;

    /// <summary>
    ///     The header offset of the word count field.
    /// </summary>
    private const int WordCountOffset = 8;

    /// <summary>
    ///     The header offset of the blob length field.
    /// </summary>
    private const int BlobLengthOffset = 12;

    /// <summary>
    ///     The header offset of the slot count field.
    /// </summary>
    private const int SlotCountOffset = 16;

    /// <summary>
    ///     The header offset of the hash identifier field.
    /// </summary>
    private const int HashIdOffset = 20;

    /// <summary>
    ///     The header offset of the reserved bytes.
    /// </summary>
    private const int ReservedOffset = 24;

    /// <summary>
    ///     The number of reserved header bytes.
    /// </summary>
    private const int ReservedLength = HeaderLength - ReservedOffset;

    /// <summary>
    ///     The size of one slot in bytes.
    /// </summary>
    private const int SlotSize = 4;

    /// <summary>
    ///     The alignment of the slot table in bytes.
    /// </summary>
    private const int Alignment = 8;

    /// <summary>
    ///     The mask of the slot bits that hold the blob offset plus one.
    /// </summary>
    private const uint OffsetMask = 0x0FFFFFFF;

    /// <summary>
    ///     The FNV-1a offset basis, restated from the format specification.
    /// </summary>
    private const uint FnvOffsetBasis = 2166136261;

    /// <summary>
    ///     The FNV-1a prime.
    /// </summary>
    private const uint FnvPrime = 16777619;

    /// <summary>
    ///     The first multiplier of the murmur3 finalizer.
    /// </summary>
    private const uint MurmurMultiplier1 = 0x85EBCA6B;

    /// <summary>
    ///     The second multiplier of the murmur3 finalizer.
    /// </summary>
    private const uint MurmurMultiplier2 = 0xC2B2AE35;

    /// <summary>
    ///     The long shift distance of the murmur3 finalizer.
    /// </summary>
    private const int ShiftLong = 16;

    /// <summary>
    ///     The short shift distance of the murmur3 finalizer.
    /// </summary>
    private const int ShiftShort = 13;

    /// <summary>
    ///     The raw bytes of the file.
    /// </summary>
    private readonly byte[] _data;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TldReader"/> class and validates the file's overall shape.
    /// </summary>
    /// <param name="data">The complete dictionary file.</param>
    /// <exception cref="InvalidDataException">Thrown when the header or the total length is not consistent.</exception>
    public TldReader(byte[] data)
    {
        _data = data;
        if (data.Length < HeaderLength || Encoding.ASCII.GetString(data, 0, 4) != "TLD1")
        {
            throw new InvalidDataException("Not a TLD1 file.");
        }

        Flags = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(FlagsOffset));
        WordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(WordCountOffset));
        BlobLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(BlobLengthOffset));
        SlotCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(SlotCountOffset));
        HashId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HashIdOffset));
        SlotsOffset = HeaderLength + ((BlobLength + Alignment - 1) / Alignment * Alignment);
        if (data.Length != SlotsOffset + ((long)SlotSize * SlotCount))
        {
            throw new InvalidDataException("The file length does not match the header.");
        }
    }

    /// <summary>
    ///     Gets the flags field of the header.
    /// </summary>
    public uint Flags { get; }

    /// <summary>
    ///     Gets the word count field of the header.
    /// </summary>
    public uint WordCount { get; }

    /// <summary>
    ///     Gets the blob length field of the header.
    /// </summary>
    public int BlobLength { get; }

    /// <summary>
    ///     Gets the slot count field of the header.
    /// </summary>
    public uint SlotCount { get; }

    /// <summary>
    ///     Gets the hash identifier field of the header.
    /// </summary>
    public uint HashId { get; }

    /// <summary>
    ///     Gets the byte offset at which the slot table starts.
    /// </summary>
    public int SlotsOffset { get; }

    /// <summary>
    ///     Gets a value indicating whether the reserved header bytes and the padding after the blob are all zero.
    /// </summary>
    public bool ReservedAndPaddingAreZero =>
        _data.AsSpan(ReservedOffset, ReservedLength).IndexOfAnyExcept((byte)0) < 0 &&
        _data.AsSpan(HeaderLength + BlobLength, SlotsOffset - HeaderLength - BlobLength).IndexOfAnyExcept((byte)0) < 0;

    /// <summary>
    ///     Gets the words stored in the blob, in blob order.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the blob does not end with a line feed.</exception>
    public IReadOnlyList<string> Words
    {
        get
        {
            var blob = _data.AsSpan(HeaderLength, BlobLength);
            if (blob.Length == 0 || blob[^1] != (byte)'\n')
            {
                throw new InvalidDataException("The blob is not terminated.");
            }

            return Encoding.UTF8.GetString(blob).TrimEnd('\n').Split('\n');
        }
    }

    /// <summary>
    ///     Looks a word up the way the format specifies: lower-case it, hash it, and probe linearly.
    /// </summary>
    /// <param name="word">The word in any letter case.</param>
    /// <param name="caseFlags">Receives the case-form flags (the high four bits of the slot), or zero when not found.</param>
    /// <returns><see langword="true"/> when the word is stored.</returns>
    public bool TryFind(string word, out uint caseFlags)
    {
        caseFlags = 0;
        var key = Encoding.UTF8.GetBytes(word.ToLowerInvariant());
        var mask = SlotCount - 1;
        var index = Hash(key) & mask;

        // Probe until an empty slot proves the word is absent; the table always has an empty slot
        for (uint probes = 0; probes < SlotCount; probes++)
        {
            var slot = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(SlotsOffset + (int)(index * SlotSize)));
            if (slot == 0)
            {
                return false;
            }

            var offset = (int)(slot & OffsetMask) - 1;
            var candidate = _data.AsSpan(HeaderLength + offset);
            if (candidate.Length > key.Length &&
                candidate[..key.Length].SequenceEqual(key) &&
                candidate[key.Length] == (byte)'\n')
            {
                caseFlags = slot & ~OffsetMask;
                return true;
            }

            index = (index + 1) & mask;
        }

        return false;
    }

    /// <summary>
    ///     Computes the format's key hash: FNV-1a followed by the murmur3 finalizer.
    /// </summary>
    /// <param name="key">The key bytes.</param>
    /// <returns>The 32-bit hash.</returns>
    private static uint Hash(byte[] key)
    {
        uint hash = FnvOffsetBasis;
        foreach (var value in key)
        {
            hash = (hash ^ value) * FnvPrime;
        }

        hash ^= hash >> ShiftLong;
        hash *= MurmurMultiplier1;
        hash ^= hash >> ShiftShort;
        hash *= MurmurMultiplier2;
        hash ^= hash >> ShiftLong;
        return hash;
    }
}
