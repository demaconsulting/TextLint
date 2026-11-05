### SpellingDictionary

<!-- cspell:ignore neighbour organise colour favourite -->

#### Verification Approach

`SpellingDictionary` is verified with unit tests in `SpellingDictionaryTests.cs`. The class selects among three fixed
selections of embedded dictionaries and has no seam for substituting them, so the tests use the real embedded
dictionaries and the real `DictionaryImage` behind them, which are its documented dependencies, together with the
shared `DictionaryFormat` definitions. This is deliberate: the unit's behavior is the answers given for real words,
and a test double for the data would not exercise the language rules that matter.

Words are chosen to be unambiguous in the data: `neighbor` and `favorite` are American spellings that the British
selection does not know (`favorite` appears in a general technical list, but the builder strips it as a spelling
variant), `neighbour`, `organise`, `colour`, and `favourite` are British, `table` is common to both, and
`kubernetes`, `powershell`, `nuget`, and `textlint` are technical terms. `color` is deliberately not used as
American-only evidence, because the CSS list makes it a technical term.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The default selection is American English, `en` means `en-US`, names ignore case and spaces, and equivalent
  selections are the same instance.
- Unsupported, empty, missing, and list-with-empty-name languages are rejected, the first and the last by naming the
  value.
- Each regional spelling is known only where its dictionary is selected, common words and technical terms are known
  everywhere, a programming term in a regional spelling that is on the reviewed list is known everywhere, and a
  general technical word in a regional spelling follows the language.
- Case, apostrophe, accent, punctuation, script, length, digit, blank, and empty rules behave as designed, and a span
  of larger text is judged on its own.
- A lookup allocates nothing after warm-up.
- Many threads get correct answers.

#### Test Scenarios

**SpellingDictionary_Default_IsAmerican**: The default selection knows `neighbor` and not `neighbour`, and is the
same instance as the `en-US` selection. This scenario is tested by `SpellingDictionary_Default_IsAmerican`.

**SpellingDictionary_ForLanguages_En_SameAsEnUs**: `en` and `en-US` return the same instance. This scenario is
tested by `SpellingDictionary_ForLanguages_En_SameAsEnUs`.

**SpellingDictionary_ForLanguages_LowerCaseAndSpaces_Accepted**: `EN-us` is the default selection and
` en-gb , EN-US ` is the combined selection. This scenario is tested by
`SpellingDictionary_ForLanguages_LowerCaseAndSpaces_Accepted`.

**SpellingDictionary_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage**: `en-AU`, `fr`, a list whose second
name is `de`, and the text `en;en-GB` (a wrong separator) each throw an `ArgumentException` whose message names the
offending value in quotes, lists the supported languages, and names the `languages` parameter. This scenario is
tested by `SpellingDictionary_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage`.

**SpellingDictionary_ForLanguages_Empty_Throws**: An empty and a blank list each throw `ArgumentException`. This
scenario is tested by `SpellingDictionary_ForLanguages_Empty_Throws`.

**SpellingDictionary_ForLanguages_EmptyNameInList_Throws**: A list with an empty name in the middle and one with a
trailing comma each throw `ArgumentException`. This scenario is tested by
`SpellingDictionary_ForLanguages_EmptyNameInList_Throws`.

**SpellingDictionary_ForLanguages_Null_Throws**: A null list throws `ArgumentNullException`. This scenario is
tested by `SpellingDictionary_ForLanguages_Null_Throws`.

**SpellingDictionary_SupportedLanguages_ListsNames_CannotBeModified**: The supported languages are `en`, `en-GB`,
and `en-US`, and adding to the list through the list interface throws. This scenario is tested by
`SpellingDictionary_SupportedLanguages_ListsNames_CannotBeModified`.

**SpellingDictionary_IsKnown_WordOnlyInOneDictionary_IsKnownWhenSelected**: An American-only and a British-only
spelling are looked up in the three selections; each is known exactly where its dictionary is selected. This
scenario is tested by `SpellingDictionary_IsKnown_WordOnlyInOneDictionary_IsKnownWhenSelected`.

**SpellingDictionary_IsKnown_GbOnlyWordWithAmerican_IsUnknown**: The British spelling `organise` is looked up in
the selection named `en`; it is not known. This scenario is tested by
`SpellingDictionary_IsKnown_GbOnlyWordWithAmerican_IsUnknown`.

**SpellingDictionary_IsKnown_CommonWord_KnownInEverySelection**: Two common English words are known in the
American, British, and combined selections. This scenario is tested by
`SpellingDictionary_IsKnown_CommonWord_KnownInEverySelection`.

**SpellingDictionary_IsKnown_UnknownWord_IsFalse**: A nonsense word is not known. This scenario is tested by
`SpellingDictionary_IsKnown_UnknownWord_IsFalse`.

**SpellingDictionary_IsKnown_TechnicalWord_KnownForEveryLanguage**: Technical terms from a language list, a general
list, and the project's own list are known in all three selections. This scenario is tested by
`SpellingDictionary_IsKnown_TechnicalWord_KnownForEveryLanguage`.

**SpellingDictionary_IsKnown_ProgrammingTermInVariantSpelling_KnownForEveryLanguage**: `tokenizer`, an American
spelling that the reviewed list keeps, is known in the American and the British selection. This scenario is tested
by `SpellingDictionary_IsKnown_ProgrammingTermInVariantSpelling_KnownForEveryLanguage`.

**SpellingDictionary_IsKnown_VariantSpellingInGeneralList_FollowsLanguage**: `favorite`, which a general technical
list contains but the builder strips as an American spelling, is known only to the American selection, and
`favourite` is known to the British one. This scenario is tested by
`SpellingDictionary_IsKnown_VariantSpellingInGeneralList_FollowsLanguage`.

**SpellingDictionary_IsKnown_UpperCase_IsKnown**: A British word is known in lower, capitalized, upper, and
alternating case. This scenario is tested by `SpellingDictionary_IsKnown_UpperCase_IsKnown`.

**SpellingDictionary_IsKnown_WordListedWithCapital_KnownInAnyCase**: A name that the lists write with a capital is
known in lower, capitalized, and upper case. This scenario is tested by
`SpellingDictionary_IsKnown_WordListedWithCapital_KnownInAnyCase`.

**SpellingDictionary_IsKnown_TypographicApostrophe_IsKnown**: A contraction and a possessive are known with both
apostrophes. This scenario is tested by `SpellingDictionary_IsKnown_TypographicApostrophe_IsKnown`.

**SpellingDictionary_IsKnown_PunctuationAroundWord_IsLookedUpAsWritten**: A word with a leading apostrophe, a
word with a trailing period, and a lone apostrophe are not known. This scenario is tested by
`SpellingDictionary_IsKnown_PunctuationAroundWord_IsLookedUpAsWritten`.

**SpellingDictionary_IsKnown_Accented_IsKnown**: An accented word is known in lower and upper case and in its
unaccented spelling, and the wrong accent is not known. This scenario is tested by
`SpellingDictionary_IsKnown_Accented_IsKnown`.

**SpellingDictionary_IsKnown_OtherScript_IsUnknown**: Japanese and Greek text, a lone surrogate, and a
letter followed by a combining accent are not known and nothing throws. This scenario is tested by
`SpellingDictionary_IsKnown_OtherScript_IsUnknown`.

**SpellingDictionary_IsKnown_VeryLongWord_IsUnknown**: A run of 65 letters and a run of ten thousand letters are not
known and nothing throws. This scenario is tested by `SpellingDictionary_IsKnown_VeryLongWord_IsUnknown`.

**SpellingDictionary_IsKnown_DigitsSpacesAndEmpty_AreUnknown**: Empty text, blank text, a word followed by a digit,
only digits, and a word with an inner space are not known. This scenario is tested by
`SpellingDictionary_IsKnown_DigitsSpacesAndEmpty_AreUnknown`.

**SpellingDictionary_IsKnown_SpanOfLargerText_ChecksOnlyTheSpan**: A slice of larger text holding a known word is
known, and a slice holding a nonsense word is not, which shows that only the span passed in is looked up. This
scenario is tested by `SpellingDictionary_IsKnown_SpanOfLargerText_ChecksOnlyTheSpan`.

**SpellingDictionary_IsKnown_AfterWarmUp_DoesNotAllocate**: After one hit and one miss have loaded and exercised
everything, a thousand hits and a thousand misses are looked up and the bytes allocated by the thread are
measured; the count is zero. This is a design guard and is not linked to a requirement. This scenario is tested by
`SpellingDictionary_IsKnown_AfterWarmUp_DoesNotAllocate`.

**SpellingDictionary_ForLanguages_EquivalentSelections_ReturnSameInstance**: Several spellings of each of the
three selections, with repeated names and different order, return one instance per selection, and the three
instances differ. This scenario is tested by `SpellingDictionary_ForLanguages_EquivalentSelections_ReturnSameInstance`.

**SpellingDictionary_IsKnown_ParallelLookups_AllCorrect**: Twenty thousand lookups of eight words with known answers
are made from many threads at once against the British selection; every answer is correct. This scenario is tested
by `SpellingDictionary_IsKnown_ParallelLookups_AllCorrect`.
