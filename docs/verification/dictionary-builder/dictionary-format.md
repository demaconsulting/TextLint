## DictionaryFormat

### Verification Approach

`DictionaryFormat` is verified with unit tests in `DictionaryFormatTests.cs`. The unit is made of pure functions
over characters and bytes with no dependencies, so no file system, test double, or fixture is used. Expected
keys, hashes, and flags are written out literally, so a change to a definition is a visible, deliberate change
of a test.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The fold lowers ASCII and Latin-1 capitals, straightens the typographic apostrophe, and leaves the
  multiplication sign alone.
- Words that cannot be keys are reported as not representable, at the boundaries of length 64 and 65.
- The hash equals the four reference values.
- Each capitalization form, including the single-letter and apostrophe cases, is classified as expected.

### Test Scenarios

**DictionaryFormat_TryFold_UpperAscii_Lowered**: `HeLLo` folds to the bytes of `hello`. This scenario is tested
by `DictionaryFormat_TryFold_UpperAscii_Lowered`.

**DictionaryFormat_TryFold_Latin1Capital_Lowered**: `CAFÉ` folds to the UTF-8 bytes of `café`, the accented
letter taking two bytes. This scenario is tested by `DictionaryFormat_TryFold_Latin1Capital_Lowered`.

**DictionaryFormat_TryFold_TypographicApostrophe_Straightened**: `don’t` folds to the bytes of `don't`. This
scenario is tested by `DictionaryFormat_TryFold_TypographicApostrophe_Straightened`.

**DictionaryFormat_TryFold_MultiplicationSign_Unchanged**: the multiplication sign between two letters is kept
as its two UTF-8 bytes and is not shifted into the division sign. This scenario is tested by
`DictionaryFormat_TryFold_MultiplicationSign_Unchanged`.

**DictionaryFormat_TryFold_AboveLatin1_ReturnsFalse** and **DictionaryFormat_TryFold_Empty_ReturnsFalse**: a word
of Japanese characters and the empty word are not representable and produce no key. These scenarios are tested by
the tests of the same names.

**DictionaryFormat_TryFold_MaximumLength_ReturnsTrue** and **DictionaryFormat_TryFold_65Chars_ReturnsFalse**: a
word of 64 two-byte characters fills the 128-byte key and a word of 65 letters is rejected. These scenarios are
tested by the tests of the same names.

**DictionaryFormat_Hash_KnownVectors_Match**: the empty key, `a`, `hello`, and `café` hash to the four reference
values. This scenario is tested by `DictionaryFormat_Hash_KnownVectors_Match`.

**DictionaryFormat_Hash_SameBytes_SameValue**: two arrays with equal content hash equal. This scenario is tested
by `DictionaryFormat_Hash_SameBytes_SameValue`.

**DictionaryFormat_ClassifyCase_CapitalizationForms_ReturnsExpectedFlag**: eleven words covering lower case,
capitalized (including a single letter, an apostrophe, and an accented first letter), upper case (including an
accented word), and mixed case (`iPhone`, `McDonald`, and `O'Brien`) are each classified as exactly their expected
form. This scenario is tested by `DictionaryFormat_ClassifyCase_CapitalizationForms_ReturnsExpectedFlag`.

**DictionaryFormat_IsLetter_AlphabetBoundaries_ClassifiesCharacters**,
**DictionaryFormat_AlignBlobLength_Lengths_RoundsUpToMultipleOfEight**,
**DictionaryFormat_IsWithinLoadLimit_AtAndAboveLimit_ReportsLimit**, and **DictionaryFormat_Magic_Read_IsTld1**:
the edges of the alphabet, the alignment of lengths, the seven-tenths load limit, and the format identifier are
checked. These defensive scenarios support the unit but are not linked to a requirement.
