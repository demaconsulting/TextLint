### DictionaryImage

![Dictionaries Structure](DictionariesView.svg)

#### Purpose

`DictionaryImage` holds one binary dictionary in memory and answers whether a prepared key is stored in it. Its
single responsibility is the dictionary file: loading it from an embedded resource, validating that it is what
this reader supports, and probing it safely. It is internal to the library and is used only by
`SpellingDictionary`.

#### Data Model

`DictionaryImage` is a sealed class that holds the complete file as a managed byte array, the dictionary's name
for error messages, and three values derived from the header when it is created: the length of the word block,
the offset at which the slot table starts, and the slot count. All of them are fixed at construction, so an
instance is immutable and may be shared between threads.

The file layout, defined in `DictionaryFormat` and written by the builder, is a 32-byte header, a word block, and
a slot table. All integers are little endian. The header holds the format identifier, the option flags (none
are supported), the number of words, the length of the word block, the number of slots, the identifier of the
hash, and reserved bytes that are zero. The word block holds every distinct word in lower case, encoded as UTF-8,
sorted, each followed by a line feed. Zero padding then brings the block to a multiple of eight bytes, and the
slot table follows, one 32-bit value for each slot: zero for an empty slot, otherwise the offset of a word in the
block plus one in the low 28 bits and the capitalization forms of the word in the high four bits. A word is stored
in the first empty slot at or after the slot its hash selects, so a lookup starts at that slot and walks forward
until it finds the word or an empty slot. The reader does not use the capitalization bits: lookups ignore case,
and the bits are kept for a later case-sensitive check.

#### Key Methods

**Constructor**: Validates the bytes of a file and takes ownership of the array.

- _Parameters_: `string name` - the dictionary's name for messages; `byte[] data` - the complete file.
- _Preconditions_: neither argument is null, and the array is not modified afterwards.
- _Postconditions_: the image is valid, or `InvalidDataException` was thrown.

The header checks take constant time and are, in order: the data is at least as long as the header; the identifier is
the supported one; the option flags are zero; the hash identifier is the supported one; the reserved bytes are
zero; the slot count is a power of two (which excludes zero); the word count is within the load limit of
the slot count; the word block is no larger than the format allows; the total length equals exactly what the header
implies (header, padded word block, and slot table); and a non-empty word block ends with a terminator. Two linear
passes over the data already in memory then tie the header to the content: the number of terminators in the word
block must equal the word count, and a scan of the slot table must find exactly that many occupied slots, each holding
an offset that lies inside the word block at the start of a word (the first byte, or the byte after a terminator).
This rejects a header that claims words the block does not hold, which would otherwise load as a valid but wrong
dictionary, and it means a lookup can trust every offset it reads. The scans do not prove that the words are sorted,
distinct, or reachable from their hash position; the builder guarantees those properties and its tests verify them with
an independent reader. There is deliberately no checksum: the bytes come from the build of this repository.

**LoadEmbedded**: Reads an embedded resource and builds an image.

- _Parameters_: `Assembly assembly`, `string resourceName`.
- _Returns_: the validated image.
- _Postconditions_: the resource stream is closed and the resource was read exactly once.

Steps: (1) open the resource stream, and report a missing resource by name; (2) refuse a stream longer than the
largest array; (3) allocate an array of exactly the stream's length and read the stream into it completely; (4) pass the
array to the constructor, with the resource name as the dictionary name.

**Contains**: Reports whether a key is stored.

- _Parameters_: `ReadOnlySpan<byte> key` - the folded key bytes; `uint hash` - their hash.
- _Returns_: `true` when the key is stored.
- _Postconditions_: nothing is allocated and nothing changes.

Steps: (1) take the home slot as the hash masked to the slot count; (2) read the slot with a bounds-checked
little-endian read, and answer `false` at an empty slot; (3) decode the stored offset, which loading has already
proved lies at the start of a word; (4) the slot matches when more bytes remain in the block than the key is long, the byte
after those is the terminator, and the bytes equal the key, so a stored word that merely starts with the key does not
match, and a key longer than what remains cannot match; (5) otherwise move to the next slot, wrapping at the end,
and stop after one full cycle of the table, which makes the loop finite.

#### Error Handling

Every failure is an `InvalidDataException` whose message names the dictionary and gives the reason: a missing
resource, a resource that is too large to read, or a file that fails validation (a damaged header, a word count that
disagrees with the content, or a slot that does not point at a word). All of them are raised when the image is created;
a lookup of a validated image does not throw. Null arguments throw `ArgumentNullException`. Nothing is caught: a damaged
dictionary means a defective build, and it is propagated to the caller with its name so that the file that must be
rebuilt can be identified. A missing resource is reported the same way as a damaged one, because no caller can
recover from either.

#### Dependencies

- **DictionaryFormat** (a unit of the DictionaryBuilder system, compiled into the library) - the layout constants,
  the load limit and alignment arithmetic, and the supported identifier and hash.
- **.NET BCL** - spans, `BinaryPrimitives`, bit operations, and manifest resource streams.
- No unsafe code, pointer, or unsafe compiler option is used anywhere.

#### Callers

- **SpellingDictionary** - loads one image per embedded dictionary and calls `Contains` with a prepared key.
