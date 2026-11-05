## DictionaryWriter

### Verification Approach

`DictionaryWriter` is verified with unit tests in `DictionaryWriterTests.cs`. The writer is a pure function from
a word set to bytes, so no file system is used. What it wrote is judged by `TldReader` from the test support, an
independent minimal reader that restates the format from its specification (header offsets, slot encoding, hash,
and case fold) and shares no code with the builder, so an error in the writer's own definitions cannot cancel
itself out. The writer's one dependency, `DictionaryFormat`, is used as it is. The large word set uses a fixed
random seed so that a failure is identical on every run.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Header fields, blob content, padding, and total length match the format for a small known input.
- Every word of a set of thousands is found by probing, and an absent word is not.
- The slot count is always the smallest power of two within the load limit.
- Identical input gives identical bytes, whatever order the words were added in.
- Invalid input and an over-limit blob are rejected, and a blob exactly at the limit is accepted.

### Test Scenarios

**DictionaryWriter_Write_FewWords_ProducesExpectedHeaderAndBlob**: two words give flags 0, word count 2, blob
length 13, four slots, hash identifier 1, the words in sorted order, and a 64-byte file. This scenario is tested
by `DictionaryWriter_Write_FewWords_ProducesExpectedHeaderAndBlob`.

**DictionaryWriter_Write_ManyWords_EveryKeyReachableByProbing**: 5000 random words are all found, are stored in
sorted order, and an absent word is not found. This scenario is tested by
`DictionaryWriter_Write_ManyWords_EveryKeyReachableByProbing`.

**DictionaryWriter_Write_SlotCount_PowerOfTwoWithinLoadLimit**: word counts of 1, 2, 7, 8, 11, 12, and 100 give
2, 4, 16, 16, 16, 32, and 256 slots, each within the load limit. This scenario is tested by
`DictionaryWriter_Write_SlotCount_PowerOfTwoWithinLoadLimit`.

**DictionaryWriter_Write_Padding_IsZero**: a one-word blob of six bytes is padded with two zero bytes and the
reserved header bytes are zero. This scenario is tested by `DictionaryWriter_Write_Padding_IsZero`.

**DictionaryWriter_Write_Latin1Word_EncodedAsUtf8**: an accented word occupies two bytes for its accented letter
and is found in any letter case. This scenario is tested by `DictionaryWriter_Write_Latin1Word_EncodedAsUtf8`.

**DictionaryWriter_Write_CaseForms_StoredInSlotBits**: words with lower, capitalized-and-lower, upper, and mixed
forms are found with exactly those flags. This scenario is tested by
`DictionaryWriter_Write_CaseForms_StoredInSlotBits`.

**DictionaryWriter_Write_SameInput_SameBytes** and **DictionaryWriter_Write_DifferentInsertionOrder_SameBytes**:
the same set written twice, and the same words added in ascending and descending order, give identical bytes.
These scenarios are tested by the tests of the same names.

**DictionaryWriter_Write_EmptyInput_Throws**, **DictionaryWriter_Write_NullInput_Throws**,
**DictionaryWriter_Write_UnfoldedWord_Throws**, and **DictionaryWriter_Write_UnrepresentableWord_Throws**: an
empty set, a null set, a word with an upper-case letter, and a word of Japanese characters are rejected with the
documented exceptions. These scenarios are tested by the tests of the same names.

**DictionaryWriter_Write_BlobTooLarge_Throws** and **DictionaryWriter_Write_BlobAtLimit_Succeeds**: three words
that need 15 blob bytes are rejected with a limit of 14 and accepted with a limit of 15. These scenarios are
tested by the tests of the same names.
