## Dictionaries

### Verification Approach

The `Dictionaries` subsystem is verified by integration tests in `DictionariesSubsystemTests.cs`. They use the public
`SpellingDictionary` class, which in turn uses `DictionaryImage` to load and probe the real embedded dictionaries
of the library assembly, so the two units are exercised together exactly as they ship. The subsystem's one
documented dependency outside itself, the shared `DictionaryFormat` source file that the library compiles from the
`DictionaryBuilder` system, is used for real. Nothing is mocked, because the data is a fixed part of the assembly and
the behavior under test is the answers it gives.

Words are chosen so that they are unambiguous in the data: a word that is only an American spelling and appears in
no technical list, a word that is only a British spelling, a word common to both, and a technical term.

### Test Environment

Standard test environment. The tests need no files, because the dictionaries are in the library assembly that the test
project references.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Listed words are known, unlisted words are not, and empty, blank, and overlong text is not known and never throws.
- Letter case does not matter, the typographic apostrophe matches the straight one, accented and unaccented
  spellings of a source word are both known while a wrong accent is not, and text in another script is not known.
- The American, British, and combined selections each know their own spellings, and an unsupported language is
  rejected naming it.
- Technical terms are known in every selection.
- Equal selections are the same instance, and many threads get consistent answers.
- Each of the four parts of the shipped vocabulary contributes words that the selection needs.

### Test Scenarios

**DictionariesSubsystem_IsKnown_ListedAndUnlistedWords_ReportsCorrectly**: The default selection is asked about
common and American words, a nonsense word, and a misspelling; only the listed words are known. This scenario is
tested by `DictionariesSubsystem_IsKnown_ListedAndUnlistedWords_ReportsCorrectly`.

**DictionariesSubsystem_IsKnown_EmptyWhitespaceAndOverlongWords_AreUnknown**: Empty text, blank text, and a word
of 65 letters are looked up; none is known and nothing throws. This scenario is tested by
`DictionariesSubsystem_IsKnown_EmptyWhitespaceAndOverlongWords_AreUnknown`.

**DictionariesSubsystem_IsKnown_DifferentCase_IsKnown**: A word is looked up in lower, capitalized, and upper case,
and a name listed with a capital in lower and upper case; all are known. This scenario is tested by
`DictionariesSubsystem_IsKnown_DifferentCase_IsKnown`.

**DictionariesSubsystem_IsKnown_TypographicApostrophe_IsKnown**: A contraction is looked up with the straight and
with the curly apostrophe; both are known. This scenario is tested by
`DictionariesSubsystem_IsKnown_TypographicApostrophe_IsKnown`.

**DictionariesSubsystem_IsKnown_AccentedAndUnaccentedSpelling_BothKnown**: An accented word and its unaccented
spelling are both known, and the same word with the wrong accent is not. This scenario is tested by
`DictionariesSubsystem_IsKnown_AccentedAndUnaccentedSpelling_BothKnown`.

**DictionariesSubsystem_IsKnown_OtherScript_IsUnknown**: Words in Japanese and Greek letters are looked up; neither
is known. This scenario is tested by `DictionariesSubsystem_IsKnown_OtherScript_IsUnknown`.

**DictionariesSubsystem_ForLanguages_AmericanBritishAndBoth_SelectExpectedSpellings**: An American-only and a
British-only spelling are looked up in the three selections; each is known exactly where its dictionary is
selected, and the combined selection knows both. This scenario is tested by
`DictionariesSubsystem_ForLanguages_AmericanBritishAndBoth_SelectExpectedSpellings`.

**DictionariesSubsystem_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage**: The language `en-AU` is requested;
an `ArgumentException` that names it is thrown. This scenario is tested by
`DictionariesSubsystem_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage`.

**DictionariesSubsystem_IsKnown_TechnicalTermInEveryLanguage_IsKnown**: A technical term is looked up in the three
selections; it is known in each. This scenario is tested by
`DictionariesSubsystem_IsKnown_TechnicalTermInEveryLanguage_IsKnown`.

**DictionariesSubsystem_ForLanguages_SameSelection_ReturnsSameInstance**: The same selection is requested with
different spelling and spacing; the same instance is returned. This scenario is tested by
`DictionariesSubsystem_ForLanguages_SameSelection_ReturnsSameInstance`.

**DictionariesSubsystem_IsKnown_ParallelCallers_ReturnConsistentAnswers**: Twenty thousand lookups spread over the
three selections are made from many threads at once; every answer matches the selection. This scenario is tested by
`DictionariesSubsystem_IsKnown_ParallelCallers_ReturnConsistentAnswers`.

**DictionariesSubsystem_ForLanguages_ShippedVocabulary_ContainsSampleWordsOfEachPart**: A common word, an
American-only word, a British-only word, and a technical term are looked up in the American and British selections;
each is known exactly where the part of the vocabulary that holds it is selected, which shows that all four parts of
the shipped vocabulary are loaded and used. This scenario is tested by
`DictionariesSubsystem_ForLanguages_ShippedVocabulary_ContainsSampleWordsOfEachPart`.
