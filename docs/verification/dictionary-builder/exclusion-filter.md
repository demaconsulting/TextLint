## ExclusionFilter

### Verification Approach

`ExclusionFilter` is verified with unit tests in `ExclusionFilterTests.cs`. The unit is a pure function over sets of
folded words, so no file system, test double, or fixture is used: each test builds small word sets in memory,
applies the exclusions, and asserts on the remaining words, the removal counts, and the unused excluded words. The
only other item the tests touch is `DictionaryFormat`, for the capitalization flag constants used to check that
flags are preserved.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- An excluded word is removed from every dictionary that has it and no other word is removed.
- The number of words removed from each dictionary is counted, in input order.
- An excluded word that no dictionary has is reported, in ordinal order, and one that removed a word is not.
- With no exclusions every word is kept with its flags, and the flags of the words that remain are unchanged.
- The inputs are not modified, and a result is unaffected by later changes to the collections it was built from.

### Test Scenarios

**ExclusionFilter_Apply_ExcludedWordInSeveralDictionaries_IsRemovedFromEach**: two dictionaries have the excluded
word and a third does not; the word is gone from the first two, every other word remains, and the third is
unchanged. This scenario is tested by `ExclusionFilter_Apply_ExcludedWordInSeveralDictionaries_IsRemovedFromEach`.

**ExclusionFilter_Apply_ExcludedWords_CountsRemovalsPerDictionary**: two excluded words are removed from three
dictionaries in amounts of two, one, and none, and the counts follow the input order. This scenario is tested by
`ExclusionFilter_Apply_ExcludedWords_CountsRemovalsPerDictionary`.

**ExclusionFilter_Apply_ExclusionInNoDictionary_IsReportedInOrder**: of three excluded words given out of order,
one is in the dictionary; the two that no dictionary has are reported in ordinal order. This scenario is tested by
`ExclusionFilter_Apply_ExclusionInNoDictionary_IsReportedInOrder`.

**ExclusionFilter_Apply_NoExclusions_KeepsEveryWord**: an empty exclusions list keeps every word with its flags,
removes nothing, and reports nothing unused. This scenario is tested by
`ExclusionFilter_Apply_NoExclusions_KeepsEveryWord`.

**ExclusionFilter_Apply_RemainingWords_KeepTheirFlags**: a word that remains keeps exactly its capitalization
flags. This scenario is tested by `ExclusionFilter_Apply_RemainingWords_KeepTheirFlags`.

**ExclusionFilter_Apply_Inputs_AreNotModified**: after the filter runs, the dictionary and the exclusions it was
given still hold their original words. This scenario is tested by `ExclusionFilter_Apply_Inputs_AreNotModified`.

**ExclusionFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged**: a result is built from a word
dictionary, a count list, and an unused-word list; after each is changed, the result still holds the original
contents, and its words reject modification even through a cast. This scenario is tested by
`ExclusionFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged`.

**ExclusionFilter_Result_ListsCastToMutableInterfaces_CannotBeChanged**: the lists of a result (dictionaries,
removal counts, unused words) are cast back to the mutable list interface; every attempt to add, replace, or clear
is refused with `NotSupportedException` and the contents stay as built. This scenario is tested by
`ExclusionFilter_Result_ListsCastToMutableInterfaces_CannotBeChanged`.
