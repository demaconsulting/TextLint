## VariantFilter

### Verification Approach

`VariantFilter` is verified with unit tests in `VariantFilterTests.cs`. The unit is a pure function over sets of
folded words, so no file system, test double, or fixture is used: each test builds small word sets in memory,
applies the rule, and asserts on the remaining words, the counts, and the reported keep-list words. The only
other item the tests touch is `DictionaryFormat`, for the capitalization flag constants used to check that flags
are merged.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- A general-list variant that no language list and not the keep list has is removed and counted; every other
  general word is kept.
- Language-list words and keep-list words are kept whether or not they are variants.
- The capitalization flags of a word in several lists are merged.
- Keep-list words that are not variants are reported in ordinal order, and the three counts are correct for a
  mixed input.
- A result is unaffected by later changes to the collections it was built from.

### Test Scenarios

**VariantFilter_Apply_GeneralVariant_IsStripped**: `favorite` is a variant in a general list; it is removed, the
ordinary word and the language word remain, and one word is counted as stripped. This scenario is tested by
`VariantFilter_Apply_GeneralVariant_IsStripped`.

**VariantFilter_Apply_NonVariantGeneralWord_IsKept**: ordinary general words survive and nothing is stripped. This
scenario is tested by `VariantFilter_Apply_NonVariantGeneralWord_IsKept`.

**VariantFilter_Apply_GeneralVariantInLanguageList_IsKept**: `color` is in a general and a language list and is a
variant; it is kept, counted as kept through the language list, and not stripped. This scenario is tested by
`VariantFilter_Apply_GeneralVariantInLanguageList_IsKept`.

**VariantFilter_Apply_GeneralVariantInKeepList_IsKept**: `tokenizer` is a variant in a general list and in the
keep list; it is kept, counted as kept through the keep list, and the keep list reports nothing. This scenario is
tested by `VariantFilter_Apply_GeneralVariantInKeepList_IsKept`.

**VariantFilter_Apply_LanguageVariantOnly_IsKept**: `grey` is a variant in a language list only; it is kept and
counted. This scenario is tested by `VariantFilter_Apply_LanguageVariantOnly_IsKept`.

**VariantFilter_Apply_KeepWordNotVariant_IsReported**: a keep list with `zebra`, `tokenizer`, and `apple`, of
which only `tokenizer` is a variant, reports `apple` and `zebra` in ordinal order and counts one keep-list
variant. This scenario is tested by `VariantFilter_Apply_KeepWordNotVariant_IsReported`.

**VariantFilter_Apply_WordInSeveralLists_MergesFlags**: a word that is capitalized in a general list and lower
case in a language list is kept with both flags. This scenario is tested by
`VariantFilter_Apply_WordInSeveralLists_MergesFlags`.

**VariantFilter_Apply_MixedInput_CountsKeptAndStripped**: two language variants, one keep-list variant, two
stripped variants, and an ordinary word give the counts 2, 1, and 2 and the expected remaining words. This
scenario is tested by `VariantFilter_Apply_MixedInput_CountsKeptAndStripped`.

**VariantFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged**: a result is built from a word dictionary
and a keep-word list; after a word is added to the dictionary and another to the list, the result still holds the
original words and keep-list words, and its words reject modification even through a cast. This scenario is tested
by `VariantFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged`.
