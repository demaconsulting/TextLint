### DictionaryImage

<!-- cspell:ignore alph alphab colour -->

#### Verification Approach

`DictionaryImage` is verified with unit tests in `DictionaryImageTests.cs`. Lookup and validation tests build tiny
dictionary images in memory with a test helper, `TldImageBuilder`, which restates the file layout from the format
specification (a sorted word block and a linear-probing slot table) and takes only the key hash from the shared
`DictionaryFormat` definitions, a documented dependency of the unit. Small images make each behavior exact and
reviewable: every word of an image can be listed, two words can be chosen so that they start probing at the same
slot, and a single byte can be changed to produce each kind of damage. The embedded-resource tests use the real
library assembly (a real dictionary must load and answer) and a deliberately damaged resource embedded in the test
assembly (it must be reported by name). The real dictionaries are verified end to end, through the public
lookup, by the subsystem and system tests, and the builder's own tests verify the files it writes with an
independent reader.

The validation tests have no linked requirement: a test cannot damage the shipped resource, so damage handling has no
system-level observable behavior, and the tests are defensive.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every stored word is found, including the first and the last in the word block, and words whose keys collide are
  found by probing.
- A word that is absent, a prefix of a stored word, or longer than a stored word is not found, and an image with no
  words finds nothing.
- Every kind of header damage (length, identifier, options, hash, reserved bytes, slot count, load, word block size,
  total length, terminator) is rejected with an `InvalidDataException` that names the dictionary.
- A header whose word count disagrees with the word block or the slot table, including a header that claims words
  but has none, a slot that does not point at the start of a word, and two slots that point at the same word (which
  leaves another word without a slot), are rejected when the image is loaded.
- A real embedded dictionary loads and answers, and a missing or damaged embedded resource is rejected naming the
  resource.

#### Test Scenarios

**DictionaryImage_Contains_EveryWordInImage_IsTrue**: An image of five words is built; every word is found. This
scenario is tested by `DictionaryImage_Contains_EveryWordInImage_IsTrue`.

**DictionaryImage_Contains_WordNotInImage_IsFalse**: A word that is not stored is not found. This scenario is tested
by `DictionaryImage_Contains_WordNotInImage_IsFalse`.

**DictionaryImage_Contains_PrefixOfStoredWord_IsFalse**: With `alpha` and `alphabet` stored, the prefixes `alph` and
`alphab` are not found while both stored words are. This scenario is tested by
`DictionaryImage_Contains_PrefixOfStoredWord_IsFalse`.

**DictionaryImage_Contains_KeyLongerThanStoredWord_IsFalse**: With only `alpha` stored, which ends at the end of the
word block, `alphabets` is not found and no read goes past the block. This scenario is tested by
`DictionaryImage_Contains_KeyLongerThanStoredWord_IsFalse`.

**DictionaryImage_Contains_CollidingKeys_FoundByProbing**: Two words that start probing at the same slot of an
eight-slot table are found by the test helper, the collision is asserted, and both are stored and found. This
scenario is tested by `DictionaryImage_Contains_CollidingKeys_FoundByProbing`.

**DictionaryImage_Contains_EmptyImage_IsFalse**: An image with no words is valid and finds nothing. This scenario
is tested by `DictionaryImage_Contains_EmptyImage_IsFalse`.

**DictionaryImage_New_SlotOffsetOutsideBlob_ThrowsInvalidData**: The occupied slot of a one-word image is changed
to point far beyond the word block; loading the image throws `InvalidDataException` naming the dictionary. This
scenario is tested by `DictionaryImage_New_SlotOffsetOutsideBlob_ThrowsInvalidData`.

**DictionaryImage_New_SlotWithoutOffset_ThrowsInvalidData**: The occupied slot is changed to hold case flags
and no offset; loading throws `InvalidDataException`. This scenario is tested by
`DictionaryImage_New_SlotWithoutOffset_ThrowsInvalidData`.

**DictionaryImage_New_SlotInsideWord_ThrowsInvalidData**: The occupied slot is changed to point at the second
letter of its word; loading throws `InvalidDataException`. This scenario is tested by
`DictionaryImage_New_SlotInsideWord_ThrowsInvalidData`.

**DictionaryImage_New_WordCountMismatchesBlob_ThrowsInvalidData**: The header of a one-word image is changed to
claim two words; loading throws `InvalidDataException` saying the count does not match the word block. This
scenario is tested by `DictionaryImage_New_WordCountMismatchesBlob_ThrowsInvalidData`.

**DictionaryImage_New_WordCountWithoutBlob_ThrowsInvalidData**: A hand-made image that claims one word but has an
empty word block is loaded; it is rejected and cannot pass as an empty image. This scenario is tested by
`DictionaryImage_New_WordCountWithoutBlob_ThrowsInvalidData`.

**DictionaryImage_New_SlotTableMismatchesWordCount_ThrowsInvalidData**: The slot of the second word of a two-word image
is emptied; loading throws `InvalidDataException` saying the count does not match the slot table. This
scenario is tested by `DictionaryImage_New_SlotTableMismatchesWordCount_ThrowsInvalidData`.

**DictionaryImage_New_DuplicateSlotOffset_ThrowsInvalidData**: In a two-word image the slot of the second word is
overwritten with the slot of the first, so every count and offset is valid but two slots hold the same offset; loading
throws `InvalidDataException` saying two slots point at the same word. This scenario is tested by
`DictionaryImage_New_DuplicateSlotOffset_ThrowsInvalidData`.

**DictionaryImage_New_WordWithoutSlot_ThrowsInvalidData**: In a three-word image the slot of the last word is
redirected to the first word, leaving the last word unreferenced; loading throws `InvalidDataException`. This scenario
is tested by `DictionaryImage_New_WordWithoutSlot_ThrowsInvalidData`.

**DictionaryImage_New_ValidImage_IsAccepted**: A valid image passes validation and answers a lookup. This scenario
is tested by `DictionaryImage_New_ValidImage_IsAccepted`.

**DictionaryImage_New_NullName_ThrowsArgumentNullException**: A null name throws. This scenario is tested by
`DictionaryImage_New_NullName_ThrowsArgumentNullException`.

**DictionaryImage_New_NullData_ThrowsArgumentNullException**: Null data throws. This scenario is tested by
`DictionaryImage_New_NullData_ThrowsArgumentNullException`.

**DictionaryImage_New_TruncatedHeader_ThrowsInvalidData**: Data one byte shorter than the header is rejected, naming
the dictionary and saying it is shorter than the header. This scenario is tested by
`DictionaryImage_New_TruncatedHeader_ThrowsInvalidData`.

**DictionaryImage_New_BadMagic_ThrowsInvalidData**: An image whose identifier is changed is rejected. This scenario
is tested by `DictionaryImage_New_BadMagic_ThrowsInvalidData`.

**DictionaryImage_New_UnsupportedFlags_ThrowsInvalidData**: An image with an option flag set is rejected. This
scenario is tested by `DictionaryImage_New_UnsupportedFlags_ThrowsInvalidData`.

**DictionaryImage_New_UnsupportedHash_ThrowsInvalidData**: An image with another hash identifier is rejected. This
scenario is tested by `DictionaryImage_New_UnsupportedHash_ThrowsInvalidData`.

**DictionaryImage_New_ReservedBytesNotZero_ThrowsInvalidData**: An image with a non-zero reserved byte is rejected.
This scenario is tested by `DictionaryImage_New_ReservedBytesNotZero_ThrowsInvalidData`.

**DictionaryImage_New_SlotCountZero_ThrowsInvalidData**: An image with a slot count of zero is rejected as not a power
of two. This scenario is tested by `DictionaryImage_New_SlotCountZero_ThrowsInvalidData`.

**DictionaryImage_New_SlotCountNotPowerOfTwo_ThrowsInvalidData**: An image with a slot count of six is rejected. This
scenario is tested by `DictionaryImage_New_SlotCountNotPowerOfTwo_ThrowsInvalidData`.

**DictionaryImage_New_WordCountOverLoadLimit_ThrowsInvalidData**: An image that claims as many words as slots is
rejected for exceeding the load limit. This scenario is tested by
`DictionaryImage_New_WordCountOverLoadLimit_ThrowsInvalidData`.

**DictionaryImage_New_BlobTooLarge_ThrowsInvalidData**: An image that claims a word block beyond the format's limit
is rejected. This scenario is tested by `DictionaryImage_New_BlobTooLarge_ThrowsInvalidData`.

**DictionaryImage_New_TruncatedImage_ThrowsInvalidData**: An image missing its last byte is rejected because its
length does not match its header. This scenario is tested by `DictionaryImage_New_TruncatedImage_ThrowsInvalidData`.

**DictionaryImage_New_TrailingBytes_ThrowsInvalidData**: An image with an extra byte at the end is rejected. This
scenario is tested by `DictionaryImage_New_TrailingBytes_ThrowsInvalidData`.

**DictionaryImage_New_BlobNotTerminated_ThrowsInvalidData**: An image whose last word terminator is changed to a
letter is rejected. This scenario is tested by `DictionaryImage_New_BlobNotTerminated_ThrowsInvalidData`.

**DictionaryImage_LoadEmbedded_ExistingResource_Loads**: The British dictionary of the library assembly is loaded;
`colour` is found and a nonsense word is not. This scenario is tested by
`DictionaryImage_LoadEmbedded_ExistingResource_Loads`.

**DictionaryImage_LoadEmbedded_MissingResource_ThrowsNamingResource**: A resource name that the assembly does not
have is loaded; an `InvalidDataException` names it. This scenario is tested by
`DictionaryImage_LoadEmbedded_MissingResource_ThrowsNamingResource`.

**DictionaryImage_LoadEmbedded_CorruptResource_ThrowsNamingResource**: A resource of the test assembly that is not a
dictionary is loaded; an `InvalidDataException` names it. This scenario is tested by
`DictionaryImage_LoadEmbedded_CorruptResource_ThrowsNamingResource`.

**DictionaryImage_LoadEmbedded_NullAssembly_ThrowsArgumentNullException**: A null assembly throws. This scenario is
tested by `DictionaryImage_LoadEmbedded_NullAssembly_ThrowsArgumentNullException`.

**DictionaryImage_LoadEmbedded_NullResourceName_ThrowsArgumentNullException**: A null resource name throws. This
scenario is tested by `DictionaryImage_LoadEmbedded_NullResourceName_ThrowsArgumentNullException`.
