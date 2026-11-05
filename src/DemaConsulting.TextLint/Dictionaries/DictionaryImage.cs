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
using System.Collections;
using System.Numerics;
using System.Reflection;

namespace DemaConsulting.TextLint.Dictionaries;

/// <summary>
///     One binary dictionary held in memory: a validated image of the dictionary file format that
///     answers whether a prepared lookup key is stored in it.
/// </summary>
/// <remarks>
///     The image exists so that a dictionary of well over a hundred thousand words can be loaded
///     without building any data structure: the build has already sorted the words into a block
///     and precomputed a hash table over them, so loading is one read of the bytes plus one linear
///     consistency pass, and a lookup is one hash and a short probe. The bytes are read once
///     into a managed array and every access goes through bounds-checked spans and
///     <see cref="BinaryPrimitives"/>, so a damaged image can only produce an
///     <see cref="InvalidDataException"/> at load, never an out-of-range read; no unsafe code is involved
///     anywhere. Safety was chosen over the small extra speed that reading the resource in place
///     would give. The layout and the hash are defined once, in <see cref="DictionaryFormat"/>,
///     which the dictionary builder compiles as well. An instance never changes after
///     construction, so it is safe to share between threads.
/// </remarks>
internal sealed class DictionaryImage
{
    /// <summary>
    ///     The complete dictionary file, owned by this instance and never modified.
    /// </summary>
    private readonly byte[] _data;

    /// <summary>
    ///     The name of the dictionary, used to identify it in error messages.
    /// </summary>
    private readonly string _name;

    /// <summary>
    ///     The length in bytes of the word block that follows the header.
    /// </summary>
    private readonly int _blobLength;

    /// <summary>
    ///     The offset in the image at which the slot table starts.
    /// </summary>
    private readonly int _slotsOffset;

    /// <summary>
    ///     The number of slots, which is a power of two.
    /// </summary>
    private readonly uint _slotCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DictionaryImage"/> class from the bytes of a
    ///     dictionary file after validating its header.
    /// </summary>
    /// <remarks>
    ///     Validation checks the header, the overall length, and the agreement of the header's word count with
    ///     the word block and the slot table, in one linear pass over the bytes that were just read, so every
    ///     offset a lookup later reads is already known to be inside the word block. There is deliberately no
    ///     checksum: the bytes come from a build of this very repository. The array is kept, not copied, so the
    ///     caller must not modify it afterwards.
    /// </remarks>
    /// <param name="name">The name of the dictionary, used in error messages. Must not be null.</param>
    /// <param name="data">The complete dictionary file. Must not be null and must not be modified after the call.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown, naming the dictionary and the reason, when the data is shorter than the header, has
    ///     the wrong identifier, uses an unsupported option or hash, has non-zero reserved bytes, has a
    ///     slot count that is zero or not a power of two, is loaded beyond the load limit, does not
    ///     have exactly the length that the header implies, has a word block that is not terminated, or
    ///     has a word count that disagrees with the word block or the slot table, a slot that does not
    ///     point at the start of a word, or two slots that point at the same word (which leaves another
    ///     word without a slot).
    /// </exception>
    internal DictionaryImage(string name, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        _name = name;
        _data = data;

        // A file shorter than the header cannot say anything about itself
        if (data.Length < DictionaryFormat.HeaderLength)
        {
            throw Invalid("it is shorter than the header");
        }

        // The identifier, the optional-feature flags, the hash, and the reserved bytes must be exactly
        // what this reader supports, because anything else was written by a different format
        if (!data.AsSpan(DictionaryFormat.MagicOffset, DictionaryFormat.Magic.Length).SequenceEqual(DictionaryFormat.Magic))
        {
            throw Invalid("it does not start with the dictionary identifier");
        }

        if (ReadHeaderField(DictionaryFormat.FlagsOffset) != DictionaryFormat.NoFlags)
        {
            throw Invalid("it uses options this reader does not support");
        }

        if (ReadHeaderField(DictionaryFormat.HashIdOffset) != DictionaryFormat.SupportedHashId)
        {
            throw Invalid("it uses a hash this reader does not support");
        }

        if (data.AsSpan(DictionaryFormat.ReservedOffset, DictionaryFormat.ReservedLength).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw Invalid("its reserved header bytes are not zero");
        }

        // The slot count must allow the cheap mask arithmetic of the lookup, and the words must fit the
        // table within the load limit so that probing always ends at an empty slot
        var wordCount = ReadHeaderField(DictionaryFormat.WordCountOffset);
        var blobLength = ReadHeaderField(DictionaryFormat.BlobLengthOffset);
        _slotCount = ReadHeaderField(DictionaryFormat.SlotCountOffset);
        if (!BitOperations.IsPow2(_slotCount))
        {
            throw Invalid("its slot count is not a power of two");
        }

        if (!DictionaryFormat.IsWithinLoadLimit(wordCount, _slotCount))
        {
            throw Invalid("its word count exceeds the load limit of its slot table");
        }

        if (blobLength > DictionaryFormat.MaxBlobLength)
        {
            throw Invalid("its word block is larger than the format allows");
        }

        // The header fixes the exact total length, so any difference means truncation or trailing data
        _blobLength = (int)blobLength;
        var slotsOffset = DictionaryFormat.HeaderLength + DictionaryFormat.AlignBlobLength(_blobLength);
        if (data.Length != slotsOffset + ((long)DictionaryFormat.SlotSize * _slotCount))
        {
            throw Invalid("its length does not match its header");
        }

        _slotsOffset = (int)slotsOffset;

        // A non-empty word block ends with the terminator of its last word
        var blob = data.AsSpan(DictionaryFormat.HeaderLength, _blobLength);
        if (!blob.IsEmpty && blob[^1] != DictionaryFormat.WordTerminator)
        {
            throw Invalid("its word block does not end with a word terminator");
        }

        // The header's word count must agree with the word block (one terminator per word) and with the slot
        // table (one occupied slot per word), every occupied slot must point at the start of a word, and each
        // word must be referenced by exactly one slot. A
        // header that disagrees with its content is damage even when each field looks plausible, and checking
        // it here means that lookups can trust every offset they read. The scan is one linear pass over data
        // that has already been read into memory.
        if (blob.Count(DictionaryFormat.WordTerminator) != wordCount)
        {
            throw Invalid("its word count does not match its word block");
        }

        ValidateSlots(wordCount);
    }

    /// <summary>
    ///     Loads a dictionary that is embedded as a resource in an assembly.
    /// </summary>
    /// <remarks>
    ///     The resource is read once, completely, into a managed array, so later lookups need no
    ///     further I/O and no access to the assembly. A missing resource means the library was
    ///     built or packaged without its data, which no caller can recover from, so it is reported
    ///     the same way as a damaged one. Reading allocates one array of the resource's size.
    ///     Thread-safe: it uses no shared state, but callers normally load each resource once
    ///     under a lock or a lazy initializer.
    /// </remarks>
    /// <param name="assembly">The assembly that holds the resource. Must not be null.</param>
    /// <param name="resourceName">The logical name of the embedded resource. Must not be null.</param>
    /// <returns>The validated dictionary image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> or <paramref name="resourceName"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown, naming the resource, when the assembly has no such resource, the resource is too
    ///     large to read into one array, or its content fails validation.
    /// </exception>
    internal static DictionaryImage LoadEmbedded(Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resourceName);

        // Open the resource; the runtime answers null when the assembly has no resource of that name
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"The embedded dictionary '{resourceName}' is missing from the assembly.");

        // Read the whole resource into one managed array of exactly its size
        if (stream.Length > Array.MaxLength)
        {
            throw new InvalidDataException($"The embedded dictionary '{resourceName}' is too large to load.");
        }

        var data = new byte[(int)stream.Length];
        stream.ReadExactly(data);

        // Validate the header before the image is used
        return new DictionaryImage(resourceName, data);
    }

    /// <summary>
    ///     Determines whether a prepared lookup key is stored in the dictionary.
    /// </summary>
    /// <remarks>
    ///     The caller folds the word to its key and hashes it once, and may then ask several images
    ///     with the same key and hash. The lookup starts at the slot the hash selects and probes
    ///     forward until it finds the word or an empty slot, which the load limit guarantees exists.
    ///     A slot matches when the stored word equals the key and is followed by its terminator, so a
    ///     stored word that merely starts with the key does not match. Every read is bounds checked.
    ///     The probe count is capped at the slot count, so it cannot loop forever. Loading has already
    ///     proved that every occupied slot points at a word, so a lookup never throws for a validated
    ///     image. Allocation-free and thread-safe.
    /// </remarks>
    /// <param name="key">The folded key bytes, as produced by <see cref="DictionaryFormat.TryFold"/>.</param>
    /// <param name="hash">The hash of <paramref name="key"/>, as produced by <see cref="DictionaryFormat.Hash"/>.</param>
    /// <returns><see langword="true"/> when the dictionary stores the key; otherwise <see langword="false"/>.</returns>
    internal bool Contains(ReadOnlySpan<byte> key, uint hash)
    {
        var blob = _data.AsSpan(DictionaryFormat.HeaderLength, _blobLength);
        var slots = _data.AsSpan(_slotsOffset);
        var mask = _slotCount - 1;
        var index = hash & mask;

        // Probe from the home slot until the word is found or an empty slot proves it is absent. Loading
        // proved that every occupied slot points at a word and that empty slots exist, so the probe ends;
        // the cap at one cycle of the table is a second guarantee that it cannot loop.
        for (uint probes = 0; probes < _slotCount; probes++)
        {
            var slot = BinaryPrimitives.ReadUInt32LittleEndian(
                slots.Slice((int)index * DictionaryFormat.SlotSize, DictionaryFormat.SlotSize));
            if (slot == 0)
            {
                return false;
            }

            // The slot matches when the key is stored here in full: the same bytes, then the terminator
            var remaining = blob[((int)(slot & DictionaryFormat.OffsetMask) - 1)..];
            if (remaining.Length > key.Length &&
                remaining[key.Length] == DictionaryFormat.WordTerminator &&
                remaining[..key.Length].SequenceEqual(key))
            {
                return true;
            }

            index = (index + 1) & mask;
        }

        return false;
    }

    /// <summary>
    ///     Checks that the slot table agrees with the header and with the word block.
    /// </summary>
    /// <remarks>
    ///     Every occupied slot must hold an offset that lies inside the word block and falls at the start
    ///     of a word (the first byte of the block, or the byte after a terminator), no two slots may hold the
    ///     same offset, and the number of occupied slots must equal the word count. A word block with a
    ///     given terminator count has exactly that many word starts, so when the word count equals the
    ///     terminator count, distinct valid offsets in exactly that many slots mean that every word is
    ///     referenced by exactly one slot: a corrupted slot that duplicates another word's offset therefore
    ///     cannot load, and cannot leave a word that can never be found. The pass is linear in the size of the data and
    ///     uses a temporary bit array with one bit per byte of the word block (about 190 KB for the
    ///     largest dictionary), which is garbage once loading ends. It does not prove that the words are
    ///     sorted, distinct, or reachable from their hash, which the builder guarantees and its tests verify.
    ///     Thread-safe, as it only reads the image.
    /// </remarks>
    /// <param name="wordCount">The word count from the header.</param>
    /// <exception cref="InvalidDataException">Thrown, naming the dictionary, when a slot or the slot count is inconsistent.</exception>
    private void ValidateSlots(uint wordCount)
    {
        var blob = _data.AsSpan(DictionaryFormat.HeaderLength, _blobLength);
        var slots = _data.AsSpan(_slotsOffset);
        var referenced = new BitArray(_blobLength);
        uint occupied = 0;
        for (var position = 0; position < slots.Length; position += DictionaryFormat.SlotSize)
        {
            var slot = BinaryPrimitives.ReadUInt32LittleEndian(slots.Slice(position, DictionaryFormat.SlotSize));
            if (slot == 0)
            {
                continue;
            }

            // The offset is stored plus one, so a slot with no offset bits cannot point anywhere
            occupied++;
            var offset = (int)(slot & DictionaryFormat.OffsetMask) - 1;
            if ((uint)offset >= (uint)blob.Length || (offset > 0 && blob[offset - 1] != DictionaryFormat.WordTerminator))
            {
                throw Invalid("a slot does not point at the start of a word");
            }

            // A word may be referenced once; a second reference means another word has no slot
            if (referenced[offset])
            {
                throw Invalid("two slots point at the same word, so another word has no slot");
            }

            referenced[offset] = true;
        }

        if (occupied != wordCount)
        {
            throw Invalid("its word count does not match its slot table");
        }
    }

    /// <summary>
    ///     Reads one 32-bit little-endian field of the header.
    /// </summary>
    /// <remarks>
    ///     The caller has already checked that the data is at least as long as the header. All
    ///     integers in the format are little endian whatever the host is, and the read does not
    ///     require the offset to be aligned. Pure and thread-safe.
    /// </remarks>
    /// <param name="offset">The offset of the field within the header.</param>
    /// <returns>The value of the field.</returns>
    private uint ReadHeaderField(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));

    /// <summary>
    ///     Creates the exception that reports a damaged dictionary.
    /// </summary>
    /// <remarks>
    ///     Every failure names the dictionary, so that a damaged or mismatched resource can be traced
    ///     to the file that has to be rebuilt. Pure and thread-safe.
    /// </remarks>
    /// <param name="reason">What is wrong, as a phrase that completes the sentence.</param>
    /// <returns>An <see cref="InvalidDataException"/> that names the dictionary and the reason.</returns>
    private InvalidDataException Invalid(string reason) =>
        new($"The dictionary '{_name}' is damaged or unsupported: {reason}.");
}
