## DictionaryFormat

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`DictionaryFormat` is the single definition of everything the writer of a dictionary file and the reader of
one must agree on, to the last bit: the file layout constants, the case fold that turns a word into its lookup
key, the hash of a key, and the classification of a word's capitalization. It is a separate unit, in a
source file with no dependency on anything else of the builder, so that the library can compile the very same
file; two copies could silently drift apart and turn every lookup into a miss, one file cannot. The fold is an
explicit table owned by this unit instead of a call to a culture-sensitive library function, so a dictionary
built on one operating system is identical to one built on another and a lookup never depends on the host's
globalization data.

### Data Model

`DictionaryFormat` is an internal static class with no state, in the namespace
`DemaConsulting.TextLint.Dictionaries`. It holds constants and pure functions. All integers in a file are
little-endian, so that the format reads the same on every host.

**File layout.** A file is the following, in order:

1. The **header**, 32 bytes: the format identifier `TLD1` (4 bytes), the flags (4), the distinct word count (4),
   the blob length in bytes (4), the slot count (4), the hash identifier (4), and reserved bytes that are
   always zero (8).
2. The **blob**, as long as the blob length: one record per word, which is the key bytes followed by a line feed,
   sorted by ordinal bytes, each key unique.
3. The **padding**, 0 to 7 zero bytes that align the slot table to 8 bytes.
4. The **slot table**, 4 bytes per slot, each slot a 32-bit unsigned integer.

The format identifier carries the layout generation, so a future layout change is a different identifier that
an old reader rejects; because dictionaries are rebuilt from the lists on every build and are never stored or
exchanged, no migration is ever needed. The flags field is zero; its lowest bit is reserved for an optional
compressed blob, and a reader must reject a file with any flag set that it does not support. The hash
identifier is 1, meaning the hash defined below.

**Slot encoding.** A slot value of zero is empty. Otherwise its low 28 bits are the offset of the word's record
in the blob plus one, and its high four bits are the capitalization-form flags: bit 28 lower case, bit 29
capitalized, bit 30 upper case, bit 31 mixed. A blob may therefore be at most 2^28 minus 2 bytes long. The slot
count is a power of two, and the number of words is at most seven tenths of the slot count, which keeps linear
probing short.

**Lookup procedure** (what the library's reader does, stated here because the writer's layout exists to serve
it): fold the query to key bytes; hash them; start at the slot selected by the hash and the slot count minus one
as a mask; for each slot, stop with "not found" at an empty slot, or compare the key against the record at the
slot's offset (it matches when the bytes are equal and the next byte is the line feed), and otherwise move to the
next slot, wrapping.

**Key fold.** A word is converted to its key by lowering each ASCII capital letter and each Latin-1 capital
letter (U+00C0 to U+00DE except the multiplication sign), turning the typographic apostrophe U+2019 into the
straight apostrophe, and encoding the result as UTF-8, where characters from U+0080 to U+00FF take two bytes. A
word is not representable, and never a key, when it is empty, longer than 64 characters, or contains any
character above U+00FF after folding. The longest key is therefore 128 bytes.

**Key hash.** FNV-1a over the key bytes (32-bit offset basis and prime), followed by the murmur3 32-bit
finalizer, so that the low bits, which the slot mask uses, are well mixed. Reference values for the UTF-8 bytes
of a word: the empty key hashes to `0xAB3E7C0B`, `a` to `0x1A80B1B3`, `hello` to `0x888D766E`, and `café` to
`0xDF518D52`. Changing the hash invalidates every file, so these values are fixed by tests.

**Capitalization forms.** A word as written in a list is lower case when it has no upper-case letter;
capitalized when its only upper-case letter is its first character (a single upper-case letter counts);
upper case when it has two or more letters that are all upper case; and mixed otherwise. Apostrophes are neither
upper nor lower case. The builder ORs the forms of every spelling a word had into its slot, so the file records
that `Paris` was a name even though lookups ignore case. Whether this information is used is left to a
later case-sensitive check.

### Key Methods

**TryFold**: converts a word to key bytes.

- _Parameters_: the word, the destination buffer (at least 128 bytes), and the number of bytes written.
- _Returns_: `true` when the word is representable; `false` otherwise, with the length zero.
- _Postconditions_: no allocation; the buffer holds the key when `true` is returned.

**Fold**: folds one character as described above. **IsUpperLetter**, **IsLowerLetter**, and **IsLetter**
classify characters of the supported alphabet (the ASCII letters and the Latin-1 letters, without the
multiplication and division signs).

**Hash**: computes the key hash. Pure, allocation-free.

**ClassifyCase**: returns the single capitalization-form flag of a written word, for a word that is not empty.

**AlignBlobLength** rounds a length up to the next multiple of 8, and **IsWithinLoadLimit** tests a word count
against a slot count; both use 64-bit arithmetic so that a hostile length cannot overflow, which matters to the
library's validation of files it did not just build.

### Error Handling

None. Every member is a total, pure function: a word that cannot be a key is reported by the result of
`TryFold` rather than by an exception, which is what lets lookups answer "unknown" for any input. A destination
buffer that is too small is a programming error and surfaces as an index error.

### Dependencies

- **.NET BCL** - no types beyond primitives and spans. The unit deliberately depends on no other unit, so its
  source file can be compiled into other projects.

### Callers

- **WordListReader** - the alphabet test, the fold, the maximum word length, and the capitalization
  classification.
- **DictionaryWriter** - the layout constants, the key fold, the hash, the load limit, and the alignment.
- **Library** - the library's dictionary reader (`DictionaryImage`) and its word lookup (`SpellingDictionary`)
  compile this source file and use the same definitions to validate a file's header and to look words up. The
  library reads each embedded dictionary once into a managed byte array and uses spans and `BinaryPrimitives`
  over that array, so this unit contains only safe code.
