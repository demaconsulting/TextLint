## DictionaryWriter

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`DictionaryWriter` serializes a set of folded words into the binary dictionary format. It exists so that the
shape of a dictionary file is decided in one place and is a pure function of its input: the same words always
give the same bytes, which lets a rebuild be recognized as unchanged and lets builds on different machines be
compared by hash.

### Data Model

`DictionaryWriter` is an internal static class with no state. Its input is a read-only dictionary that maps
each distinct folded word to its capitalization-form flags. Its output is one byte array holding the complete
file, laid out as defined in the _DictionaryFormat_ design: the header, the blob of words, zero padding, and the
slot table. Two private constants control the slot table size: it starts from one slot and doubles.

### Key Methods

**Write**: serializes the words.

- _Parameters_: the words, and optionally the largest blob length in bytes that is acceptable (the overload
  without it applies the format's own limit; the limit is a parameter so that the over-limit error can be
  verified without a word list of a quarter of a gigabyte).
- _Returns_: the complete dictionary file.
- _Preconditions_: the words are not empty, each is in folded form, and each can be stored (at most 64
  characters, none above Latin-1).
- _Postconditions_: the header describes the data; the words are in the blob in ordinal order, each followed by
  a line feed; every word can be found by probing from the slot its hash selects.

The words are sorted in ordinal order, which for these characters equals ordering by their UTF-8 bytes. Each is
converted to its key bytes with `DictionaryFormat.TryFold`, appended to the blob, and hashed. The slot count is
the smallest power of two that keeps the table within the format's load limit. The words are inserted in sorted
order: each goes into the slot its hash selects, or the next free slot after it, wrapping at the end, which is
exactly the sequence a lookup follows. A slot holds the blob offset of the word plus one in its low 28 bits,
so zero means empty, and the capitalization-form flags in its high four bits. The header, the blob, the
padding, and the slot table are then written into one array, with every integer in little-endian byte order
whatever the host.

Insertion in sorted order, not in the order of the input dictionary, is what makes the output independent of
how the caller built its collection.

### Error Handling

The writer rejects bad input with exceptions, because bad input here is a programming error of the caller (the
reader already guarantees valid words): `ArgumentNullException` for a null set, `ArgumentException` for an empty
set, for a word that is not in folded form (it would duplicate its folded twin or be stored under a key nobody
can look up), and for a word that cannot be stored. `InvalidOperationException` is thrown when the blob would be
larger than the format's offset field can address. `BuilderProgram` turns the last case into a reported problem.

### Dependencies

- **DictionaryFormat** - the layout constants, the key fold, the hash, the load limit, and the alignment.
- **.NET BCL** - byte-order conversion, memory streams, and sorting.

### Callers

- **BuilderProgram** - writes each dictionary after all word lists have been read successfully.
