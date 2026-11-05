### WordTokenizer

#### Verification Approach

`WordTokenizer` is verified with unit tests in `WordTokenizerTests.cs`. The unit is pure logic over strings and
depends on nothing but the .NET base class library, so no mocks, doubles, or files are used: each test passes a text,
a range, or a list entry and asserts on the returned ranges, texts, or reasons. Positions are asserted as offsets into
the original text, and the expected reasons for rejected list entries are the exact phrases the unit produces, because
users see them. Characters outside ASCII are written as escape sequences in the tests so that the expected values are
unambiguous. Tests that cover many samples of one behavior are theories whose rows are the samples; each theory is one
scenario below. The hostile inputs are an empty text, lone surrogates, a surrogate pair cut by the end of the
requested range, and single lines of one megabyte; the argument errors are a null text or list and ranges outside
the text. Lone surrogates cannot be carried by theory data, so they are built in the test.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Words are runs of letters that end at digits, underscores, hyphens, dots, and other punctuation, and the reported
  range is the original text.
- An apostrophe, in each accepted form, stays between two letters and is left out at the edges of a word.
- Han, Hiragana, Katakana, and Hangul are never words; accented letters and other scripts are, and a combining mark
  stays with its letter.
- Words are split at case changes and at the end of an acronym, the English-suffix exception holds, and accented
  capitals split like plain capitals.
- Positions count UTF-16 code units, also after surrogate pairs and for a range inside the text.
- Tokens keep digits and punctuation, trimming removes edge punctuation, and list entries are classified, normalized,
  and rejected with the documented reasons.
- No input fails or hangs, ranges are appended to the list, and invalid arguments are rejected.

#### Test Scenarios

**WordTokenizer_FindWords_SpaceSeparatedLetters_ReturnsEachWord**: Three words separated by spaces are each reported.

**WordTokenizer_FindWords_NonLetterCharacters_EndWords**: A digit, underscore, hyphen, dot, comma, slash, plus, at sign,
backtick, or backslash between two words separates them, and digits at either end are not part of a word.

**WordTokenizer_FindWords_OnlyDigitsAndPunctuation_ReturnsNoWords**: A text of digits and punctuation has no words.

**WordTokenizer_FindWords_ReportedRange_IsOriginalText**: The range of a word after white space starts at the word and
is as long as the word.

**WordTokenizer_FindWords_RangeInsideText_ReportsOnlyWordsInRange**: With a range that covers the middle word only,
only that word is reported.

**WordTokenizer_FindWords_RangeEndsInsideWord_CutsWordAtRangeEnd**: A range that ends inside a word cuts the word at
the end of the range.

**WordTokenizer_FindWords_ApostropheBetweenLetters_StaysInsideWord**: Straight, typographic, and backslash-escaped
apostrophes, and several apostrophes in one word, stay inside the word and the original text is reported.

**WordTokenizer_FindWords_LeadingAndTrailingApostrophe_AreNotPartOfWord**: Apostrophes and quotes at the start and end
of a word, and a trailing apostrophe of a possessive plural, are left out.

**WordTokenizer_FindWords_DoubleApostrophe_EndsWord**: Two apostrophes between letters make two words.

**WordTokenizer_FindWords_HanKanaAndHangul_AreNotWords**: Han, Hiragana, Katakana, Hangul, half-width Katakana, an
iteration mark, a conjoining Hangul letter, and a Han character outside the basic plane are never reported.

**WordTokenizer_FindWords_HanBetweenLatinWords_SeparatesWords**: Han characters between two Latin words are skipped
and both words are reported.

**WordTokenizer_FindWords_AccentedAndOtherScripts_AreWords**: Accented Latin, Cyrillic, Greek, Hebrew, and Arabic words
are each one word.

**WordTokenizer_FindWords_CombiningMarkAfterLetter_StaysInsideWord**: An accent written as a combining mark is part of
the word before it.

**WordTokenizer_FindWords_CombiningMarkAlone_IsNotWord**: A combining mark with no letter before it is not a word.

**WordTokenizer_IsWordLetter_LettersAndOtherCodePoints_AreClassified**: Letters of checked scripts, including an astral
letter, are word letters; digits, underscore, apostrophes, DEL, a combining mark, the replacement character, and
Han, Hiragana, Katakana, Hangul, and Han extension code points are not.

**WordTokenizer_SplitCase_CaseChanges_SplitIntoPieces**: camelCase, PascalCase, a word with an acronym in the middle or
at the start, and a word that ends in capitals are split at the expected boundaries.

**WordTokenizer_SplitCase_NoCaseChange_ReturnsWholeWord**: Lower case, upper case, capitalized, and single-letter words
are one piece.

**WordTokenizer_SplitCase_AcronymWithEnglishSuffix_StaysWhole**: Acronyms with a plural or other English suffix stay
whole.

**WordTokenizer_SplitCase_AcronymSuffixThenWord_SplitsAtNextWord**: An acronym with a suffix followed by a word is split
before that word, and an acronym followed by a word that starts with a capital is split before the last capital.

**WordTokenizer_SplitCase_AccentedCapitals_SplitLikePlainCapitals**: Accented capitals, written as one character or as a
letter and a combining mark, split and stay whole like plain capitals.

**WordTokenizer_SplitCase_WordInsideText_PiecesCoverWordInOrder**: For a word at an offset inside a text, the first
piece starts the word, each piece starts where the previous one ended, and the last piece ends the word.

**WordTokenizer_SplitCase_AstralCapital_SplitsBeforeBothCodeUnits**: A capital outside the basic plane splits the word
before both of its code units.

**WordTokenizer_FindWords_AfterSurrogatePair_OffsetsCountUtf16Units**: A word after an emoji starts after the two code
units of the emoji and the space.

**WordTokenizer_FindTokens_AfterSurrogatePair_OffsetsCountUtf16Units**: A token after an emoji is positioned in code
units.

**WordTokenizer_FindWords_AstralLetters_FormOneWord**: Two astral letters form one word of four code units.

**WordTokenizer_FindWords_RangeNotAtStart_ReportsOffsetsIntoWholeText**: Words found in a range that starts at the
second word have offsets counted from the start of the whole text.

**WordTokenizer_FindWords_ReportedRange_EndAndIsEmptyFollowFromStartAndLength**: The end of a range is its start plus
its length, and only a range of length zero is empty.

**WordTokenizer_FindTokens_DigitsAndPunctuation_StayInsideToken**: Tokens with digits, hyphens, dots, underscores,
apostrophes, plus signs, backticks, a leading hyphen, and an astral letter are each one token.

**WordTokenizer_FindTokens_Backslash_IsPartOfTokenOnlyBeforeApostrophe**: A backslash before an apostrophe stays in the
token; a backslash between letters ends it.

**WordTokenizer_FindTokens_SpacesAndOtherPunctuation_SeparateTokens**: Spaces, commas, brackets, a slash, and a
semicolon separate tokens.

**WordTokenizer_FindTokens_TrailingPeriod_IsPartOfToken**: A period after a token is part of it.

**WordTokenizer_FindTokens_CombiningMark_ContinuesButDoesNotStartToken**: A combining mark continues a token, and a lone
mark does not start one.

**WordTokenizer_FindTokens_PartOfText_ReportsOnlyTokensInPart**: Only the token inside the requested part is reported,
at its offset in the whole text.

**WordTokenizer_TrimToken_EdgePunctuation_IsRemoved**: Dots, hyphens, apostrophes, underscores, backticks, plus signs,
and a trailing period are removed from the edges of a token; accented letters and clean tokens are unchanged.

**WordTokenizer_TrimToken_NoLetterOrDigit_ReturnsEmptyRange**: A token of punctuation only trims to an empty range.

**WordTokenizer_TrimToken_TokenInsideText_ReturnsRangeInWholeText**: Trimming a token found in a sentence returns the
range without its period, at its offset in the whole text.

**WordTokenizer_TrimToken_SurrogatePairAtEdges_AreTrimmed**: Emoji at both edges are removed whole and the range is in
code units.

**WordTokenizer_TrimToken_EmptyToken_ReturnsEmptyRange**: An empty token range trims to an empty range.

**WordTokenizer_TryParseListEntry_SingleWord_IsClassifiedAsWord**: Plain, capitalized, apostrophe, and accented words
are single words, keep their letter case, and have no suggestions.

**WordTokenizer_TryParseListEntry_DigitsOrPunctuation_IsClassifiedAsToken**: Entries with digits, hyphens, underscores,
or dots, and an entry of digits only, are token entries.

**WordTokenizer_TryParseListEntry_EdgePunctuationAndWhiteSpace_AreRemoved**: Edge punctuation and surrounding white
space are removed from an entry.

**WordTokenizer_TryParseListEntry_BlankOrNull_IsRejectedAsBlank**: A null, empty, or white space entry is rejected
with the blank reason and no entry.

**WordTokenizer_TryParseListEntry_InnerWhiteSpace_IsRejectedWithReason**: An entry with a space, a tab, or a no-break
space inside is rejected with the white space reason.

**WordTokenizer_TryParseListEntry_ForbiddenMarker_IsRejectedWithReason**: An entry that starts with an exclamation mark
is rejected with the forbidden-word reason.

**WordTokenizer_TryParseListEntry_DisallowedCharacter_IsRejectedNamingCharacter**: An entry with a character outside
the token alphabet, including an emoji, is rejected with a reason that names the first such character.

**WordTokenizer_TryParseListEntry_NoLetterOrDigit_IsRejectedWithReason**: An entry of punctuation only is rejected
because it has no letter or digit.

**WordTokenizer_TryParseListEntry_Outcome_FillsExactlyOneOfResultAndProblem**: A usable entry has a result and no
problem, and an unusable one has a problem and the default result.

**WordTokenizer_TryParseListEntry_SuggestionFormNotAllowed_IsRejected**: With suggestions switched off, the arrow and
colon separators are invalid characters and no entry results.

**WordTokenizer_TryParseListEntry_ArrowForm_SeparatesWordFromSuggestions**: The arrow form gives the word and its
trimmed suggestions, drops blank suggestions, and keeps a colon inside a suggestion.

**WordTokenizer_TryParseListEntry_ColonForm_SeparatesWordFromSuggestion**: The colon form gives the word and its
suggestion.

**WordTokenizer_TryParseListEntry_SeparatorWithoutSuggestions_ReturnsWordOnly**: A separator with nothing, or only
blanks, after it gives the word with no suggestions.

**WordTokenizer_TryParseListEntry_NoSeparatorWithSuggestionsAllowed_ReturnsPlainWord**: An entry with no separator is a
plain word when suggestions are allowed.

**WordTokenizer_TryParseListEntry_SuggestionsWithoutWord_IsRejectedWithReason**: A separator with no word before it is
rejected with the no-word reason.

**WordTokenizer_TryParseListEntry_InvalidWordWithSuggestions_IsRejectedWithReason**: A word with the forbidden-word
marker or with white space is rejected as it is without suggestions.

**WordTokenizer_TryParseListEntry_NullWithSuggestionsAllowed_IsRejectedAsBlank**: A null entry is blank when
suggestions are allowed.

**WordTokenizer_CountCharacters_Text_CountsSurrogatePairsOnce**: An empty text, ASCII text, and text with an emoji or a
combining mark are counted with a pair as one character.

**WordTokenizer_CountCharacters_LoneSurrogates_CountAsOneEach**:
A lone high surrogate, a lone low surrogate, and the two
in the wrong order each count as one character.

**WordTokenizer_FindWords_ExistingListContent_IsKeptAndAppendedTo**: A list that already holds a range keeps it and the
word follows.

**WordTokenizer_FindTokens_ExistingListContent_IsKeptAndAppendedTo**: A list that already holds a range keeps it and the
token follows.

**WordTokenizer_SplitCase_ExistingListContent_IsKeptAndAppendedTo**: A list that already holds a range keeps it and the
two pieces follow.

**WordTokenizer_FindWords_EmptyText_ReturnsNothing**: An empty text has no words or tokens and trims to an empty range.

**WordTokenizer_FindWords_LoneSurrogates_AreSkipped**: Lone surrogates around and between words are neither letters nor
errors.

**WordTokenizer_FindTokens_LoneSurrogates_AreSkipped**: Lone surrogates end tokens and are not reported.

**WordTokenizer_FindWords_PairCutByRangeEnd_IsNotPartOfWord**: A surrogate pair cut by the end of the range is not read
as a letter.

**WordTokenizer_SplitCase_LoneSurrogateInWord_CoversWholeWord**: A word with lone surrogates is split without error and
the pieces cover it completely.

**WordTokenizer_TrimToken_LoneSurrogatesAtEdges_AreTrimmed**: Lone surrogates at the edges of a token are trimmed.

**WordTokenizer_FindWords_OneMegabyteOfText_ReturnsEveryWord**: A single line of one megabyte made of two words repeated
yields every word.

**WordTokenizer_FindWords_OneMegabyteWord_ReturnsOneWord**: A word of one megabyte is one word.

**WordTokenizer_FindTokens_OneMegabyteToken_ReturnsOneToken**: A token of one megabyte with a period at both edges is
one token and trims to the range without the periods.

**WordTokenizer_SplitCase_OneMegabyteWord_SplitsEveryCaseChange**: A word of one megabyte with a case change at every
second letter is split at every change.

**WordTokenizer_TryParseListEntry_OneMegabyteEntry_IsOneWord**: A list entry of one megabyte of letters is one word.

**WordTokenizer_FindWords_NullArguments_ThrowArgumentNullException**: A null text and a null list each throw.

**WordTokenizer_FindWords_RangeOutsideText_ThrowsArgumentOutOfRangeException**: Ranges that start before the text, end
after it, or have a negative length throw.

**WordTokenizer_FindTokens_NullArguments_ThrowArgumentNullException**: A null text and a null list each throw.

**WordTokenizer_FindTokens_PartOutsideText_ThrowsArgumentOutOfRangeException**: Parts that start before the text, end
after it, or end before they start throw.

**WordTokenizer_SplitCase_NullArguments_ThrowArgumentNullException**: A null text and a null list each throw.

**WordTokenizer_SplitCase_RangeOutsideText_ThrowsArgumentOutOfRangeException**: Ranges that start before the text, end
after it, or have a negative length throw.

**WordTokenizer_TrimToken_NullText_ThrowsArgumentNullException**: A null text throws.

**WordTokenizer_TrimToken_RangeOutsideText_ThrowsArgumentOutOfRangeException**: Ranges that start before the text, end
after it, or have a negative length throw.
