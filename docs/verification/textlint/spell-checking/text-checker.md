### TextChecker

#### Verification Approach

`TextChecker` and the `SpellingIssue` findings it returns are verified with unit tests in `TextCheckerTests.cs`. The
tests use the unit's documented dependencies for real: the settings value, the embedded dictionaries behind the
spelling dictionary, and the text scanning units (tokenizer, default patterns, and directives); there is no test
double and no mocking library. Words are chosen to be unambiguous: `wrold` and `speling` are misspellings that no
dictionary holds, `xqzv` and `xqz` are non-words of four and three letters, and `hello` and `world` are ordinary
words. Each rule is proved with a success case, an error case, and a boundary case, and the hostile inputs are a
word of fifty thousand characters, a lone surrogate, a carriage return alone, a byte order mark character inside
text, and several threads that use one checker at once. The rules for patterns, Markdown files, random strings, and
contractions use the real pattern catalog, parser, Markdown patterns, and random-string detector, whose own behavior is
proved by their unit tests; here the tests prove how the checker combines them with the word rules. The unusable
patterns are written in settings built in code, which bypass the checks of the settings file loader. The production
time limit of five seconds cannot be waited for in a test, so the checker has an internal constructor that takes the
limit, and the time-out is proved with a catastrophic pattern, from the settings and from a directive, that makes a limit
of 100 milliseconds throw the time-out exception of the regular expression engine; one test checks that the public
constructor gives the five seconds and that a limit that is not positive is rejected. The checker does not catch the
exception, so that the caller can report the file as not checked. The patterns of ignoreRegExp directives are written in
the text under test, and the unusable ones are reported as findings of their own kind with the reason.
Directive texts of the other tests use the `textlint` prefix, or assemble the
`cspell` prefix from parts, so that the repository's own spell check does not read them as directives; the
ignoreRegExp tests write the `cspell` prefix in full in their texts.

#### Test Environment

No setup beyond the standard test runner. The tests read no files and are independent, so they may run in parallel.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Unknown words are reported with source name, line, and column; known, empty, and punctuation-only texts report
  nothing; a null text is rejected.
- A known compound is not split, and an unknown identifier reports only its unknown pieces.
- Adjacent case-separated pieces that form an accepted word, from a word list or the dictionary, are accepted whatever
  their own spelling; the grouping that leaves the least text unknown wins, ties go to the longer first run, no join
  crosses a separator or exceeds eight pieces, a flagged piece or flagged joined text is never hidden, the escape letter
  of a backslash is allowed only at the start of the word, and findings are unchanged when no join helps.
- Possessives, escapes, capital runs with endings, and repeated letters follow the known-word-form rules.
- The minimum length is counted in UTF-16 code units, and the boundary is exact.
- The word lists apply in the order ignored, flagged, accepted, dictionary, with the case rules of each list.
- Flagged words carry their suggestions, ignore the minimum length, and flag whole tokens when written with digits.
- Lines are counted by line feed alone, columns in UTF-16 code units; findings are in text order.
- Directives and ignored text are honored, and a null setting, an unsupported language, or an unusable entry is
  rejected naming the problem.
- Concurrent use gives identical results; findings are immutable and format in the compiler style.
- Text that matches an expression, a defined name, or a predefined name of the ignore list is not checked, whole
  patterns are applied to the whole text, the text around a match is still checked, the default patterns still apply,
  and a pattern with no match or only empty matches changes nothing.
- A pattern that cannot be used is rejected when the checker is created, naming the entry, and a pattern, from the
  settings or from a directive, that exceeds its time limit makes the check throw the time-out exception; the public
  constructor gives the limit of five seconds and a limit that is not positive is rejected.
- The pattern of an ignoreRegExp directive hides its matches in the whole text, before and after the directive, with
  the flags as written (a literal only its own flags, a bare expression ignoring letter case), and may name a pattern
  of the settings, without regard to letter case, or a predefined pattern.
- An unusable directive pattern, and a name that nothing defines, is reported as an invalid directive at the pattern,
  with the reason, hides nothing, and is reported at each place it is written, in text order with the other findings;
  one inside a disabled block still applies and is not reported; a directive with no pattern is reported as
  unsupported; at most 32 different patterns are accepted in one text and the excess is reported without being read.
- The Markdown patterns apply to a source name with a Markdown extension and not to another name or none.
- A long token that looks like random data, and the unknown words inside a hexadecimal sequence, are not reported
  by default, are reported when the setting is off or the token is below the minimum length, a flagged word is not
  hidden inside a hexadecimal sequence, and a long identifier of words is still checked.
- A contraction is accepted when every part is known, a run of capitals with an ending, or too short, and is reported
  whole when a part is unknown, flagged, or long enough to be checked.

#### Test Scenarios

**TextChecker_Check_UnknownWord_ReportsWordAtPosition**: A misspelled second word is reported with its line, column,
text, and unknown-word kind. This scenario is tested by `TextChecker_Check_UnknownWord_ReportsWordAtPosition`.

**TextChecker_Check_KnownWords_ReportsNothing**: A sentence of known words yields no finding. This scenario is tested
by `TextChecker_Check_KnownWords_ReportsNothing`.

**TextChecker_Check_EmptyText_ReturnsEmpty**: An empty text yields no finding. This scenario is tested by
`TextChecker_Check_EmptyText_ReturnsEmpty`.

**TextChecker_Check_WhiteSpaceAndPunctuationOnly_ReturnsEmpty**: A text of white space and punctuation yields no
finding. This scenario is tested by `TextChecker_Check_WhiteSpaceAndPunctuationOnly_ReturnsEmpty`.

**TextChecker_Check_SourceName_IsCopiedToFindings**: The source name appears on every finding. This scenario is tested
by `TextChecker_Check_SourceName_IsCopiedToFindings`.

**TextChecker_Check_NoSourceName_LeavesFilePathNull**: Without a source name the file path of a finding is null. This
scenario is tested by `TextChecker_Check_NoSourceName_LeavesFilePathNull`.

**TextChecker_Check_NullText_ThrowsArgumentNullException**: A null text throws and the exception names the
parameter. This scenario is tested by `TextChecker_Check_NullText_ThrowsArgumentNullException`.

**TextChecker_Check_Result_IsImmutable**: The returned list cannot be changed by casting it to a mutable interface.
This scenario is tested by `TextChecker_Check_Result_IsImmutable`.

**TextChecker_Check_VeryLongWord_IsReportedOnce**: A word of fifty thousand characters is reported once, whole, at
the first column. This scenario is tested by `TextChecker_Check_VeryLongWord_IsReportedOnce`.

**TextChecker_Check_LoneSurrogate_ReportsFollowingWord**: A lone high surrogate does not stop the check and the word
after it is reported in the right place. This scenario is tested by
`TextChecker_Check_LoneSurrogate_ReportsFollowingWord`.

**TextChecker_Check_KnownCompoundIdentifier_IsNotSplit**: A camel-case identifier that the words setting lists is
accepted as a whole although its pieces are unknown. This scenario is tested by
`TextChecker_Check_KnownCompoundIdentifier_IsNotSplit`.

**TextChecker_Check_UnknownCamelCase_ReportsOnlyUnknownPieces**: An identifier of two known words around a
misspelling reports only the misspelled piece, at its own column. This scenario is tested by
`TextChecker_Check_UnknownCamelCase_ReportsOnlyUnknownPieces`.

**TextChecker_Check_SnakeCase_ReportsEachUnknownWord**: A snake-case identifier with two misspellings reports each at
its own column. This scenario is tested by `TextChecker_Check_SnakeCase_ReportsEachUnknownWord`.

**TextChecker_Check_DigitsBetweenWords_SeparateWords**: Digits separate words, so only the misspelling between them
is reported. This scenario is tested by `TextChecker_Check_DigitsBetweenWords_SeparateWords`.

**TextChecker_Check_HyphenatedWords_AreCheckedSeparately**: A hyphenated phrase reports only its misspelled word.
This scenario is tested by `TextChecker_Check_HyphenatedWords_AreCheckedSeparately`.

**TextChecker_Check_AcronymBeforeWord_SplitsAtTheLastCapital**: An acronym followed by a misspelled capitalized word
reports only that word. This scenario is tested by `TextChecker_Check_AcronymBeforeWord_SplitsAtTheLastCapital`.

**TextChecker_Check_LowerCaseConcatenation_IsReportedWhole**: Two misspellings written together in lower case are
reported as one word, because no case change separates them. This scenario is tested by
`TextChecker_Check_LowerCaseConcatenation_IsReportedWhole`.

**TextChecker_Check_ApostropheWord_StaysWhole**: Contractions are known words and are not split at the apostrophe.
This scenario is tested by `TextChecker_Check_ApostropheWord_StaysWhole`.

**TextChecker_Check_PiecesFormListedWord_AreAccepted**: With a listed word whose first part is unknown alone,
identifiers that contain the word at the start, in the middle, at the end, and twice report nothing. This scenario is
tested by `TextChecker_Check_PiecesFormListedWord_AreAccepted`.

**TextChecker_Check_PiecesFormNoListedWord_ReportsUnknownPiece**: Without the listed word the same identifier reports
only the unknown piece, as before the join rule. This scenario is tested by
`TextChecker_Check_PiecesFormNoListedWord_ReportsUnknownPiece`.

**TextChecker_Check_MisspelledListedWord_ReportsItsUnknownPieces**: An identifier that misspells the listed word is not
joined, and both unknown pieces are reported. This scenario is tested by
`TextChecker_Check_MisspelledListedWord_ReportsItsUnknownPieces`.

**TextChecker_Check_PiecesAcrossSeparator_AreNotJoined**: The parts of a listed word separated by an underscore, a
hyphen, a digit, or a space are not joined, and the first part is reported. This scenario is tested by
`TextChecker_Check_PiecesAcrossSeparator_AreNotJoined`.

**TextChecker_Check_JoinedRunOfListedWord_MatchesAnyCase**: A joined run is matched against the listed words in any
letter case. This scenario is tested by `TextChecker_Check_JoinedRunOfListedWord_MatchesAnyCase`.

**TextChecker_Check_PiecesFormDictionaryWord_AreAccepted**: With no listed words, an identifier whose first parts form
a dictionary word whose first part is unknown alone is accepted. This scenario is tested by
`TextChecker_Check_PiecesFormDictionaryWord_AreAccepted`.

**TextChecker_Check_OverlappingJoins_LeaveTheShortestUnknownPiece**: Of two overlapping listed words, the grouping that
leaves the four-letter piece unknown wins over the leftmost grouping that leaves a five-letter piece. This scenario is
tested by `TextChecker_Check_OverlappingJoins_LeaveTheShortestUnknownPiece`.

**TextChecker_Check_OverlappingJoinsOfEqualCost_PreferTheLongerFirstRun**: Two groupings of equal cost resolve to the
one whose first run is longer. This scenario is tested by
`TextChecker_Check_OverlappingJoinsOfEqualCost_PreferTheLongerFirstRun`.

**TextChecker_Check_FlaggedPieceInsideListedWord_IsStillReported**: A flagged piece inside a listed word is reported as
flagged, and the unknown piece is reported as before. This scenario is tested by
`TextChecker_Check_FlaggedPieceInsideListedWord_IsStillReported`.

**TextChecker_Check_FlaggedJoinedText_IsNotAccepted**: A word that is both listed and flagged is not accepted as a
join, so the pieces are checked alone. This scenario is tested by `TextChecker_Check_FlaggedJoinedText_IsNotAccepted`.

**TextChecker_Check_RunLongerThanMaximum_IsNotJoined**: A listed word of eight pieces is joined and a listed word of
nine pieces is not, so each piece is reported. This scenario is tested by
`TextChecker_Check_RunLongerThanMaximum_IsNotJoined`.

**TextChecker_Check_JoinedRunAtWordStartAfterBackslash_AllowsEscapeLetter**: After a backslash the escape letter in
front of the first piece does not stop it from joining the second. This scenario is tested by
`TextChecker_Check_JoinedRunAtWordStartAfterBackslash_AllowsEscapeLetter`.

**TextChecker_Check_JoinedRunAtWordStartWithoutBackslash_IsNotJoined**: The same identifier without the backslash is
not joined. This scenario is tested by `TextChecker_Check_JoinedRunAtWordStartWithoutBackslash_IsNotJoined`.

**TextChecker_Check_VeryManyPieces_AreCheckedWithBoundedWork**: An identifier of ten thousand pieces is accepted when
pairs of pieces are listed and reports every piece otherwise. This scenario is tested by
`TextChecker_Check_VeryManyPieces_AreCheckedWithBoundedWork`.

**TextChecker_Check_PossessiveOfKnownWord_IsAccepted**: The possessive of a known word is accepted with a straight
and with a typographic apostrophe. This scenario is tested by `TextChecker_Check_PossessiveOfKnownWord_IsAccepted`.

**TextChecker_Check_PossessiveOfUnknownWord_IsReported**: The possessive of a misspelling is reported. This scenario
is tested by `TextChecker_Check_PossessiveOfUnknownWord_IsReported`.

**TextChecker_Check_WordAfterBackslashEscape_IsAccepted**: A known word after the escape letter and a backslash is
accepted. This scenario is tested by `TextChecker_Check_WordAfterBackslashEscape_IsAccepted`.

**TextChecker_Check_UnknownWordAfterBackslashEscape_IsReported**: A misspelling after an escape letter is still
reported, with the escape letter. This scenario is tested by
`TextChecker_Check_UnknownWordAfterBackslashEscape_IsReported`.

**TextChecker_Check_LetterPrefixWithoutBackslash_IsReported**: The same extra letter without a backslash is not
excused. This scenario is tested by `TextChecker_Check_LetterPrefixWithoutBackslash_IsReported`.

**TextChecker_Check_CapitalRunWithEnding_IsAccepted**: Plural acronyms whose capitals are known are accepted. This
scenario is tested by `TextChecker_Check_CapitalRunWithEnding_IsAccepted`.

**TextChecker_Check_ShortCapitalRunWithEnding_IsAccepted**: An unknown acronym that is shorter than the minimum
length keeps its ending acceptable. This scenario is tested by
`TextChecker_Check_ShortCapitalRunWithEnding_IsAccepted`.

**TextChecker_Check_UnknownCapitalRunWithEnding_IsReported**: The same acronym at the default minimum length is
reported. This scenario is tested by `TextChecker_Check_UnknownCapitalRunWithEnding_IsReported`.

**TextChecker_Check_RepeatedLetters_AreNeverReported**: Words of one letter repeated four or more times are never
reported, in either letter case. This scenario is tested by `TextChecker_Check_RepeatedLetters_AreNeverReported`.

**TextChecker_Check_ThreeRepeatedLettersAtLowMinimum_AreReported**: Three repeated letters are below the rule and are
reported once the minimum lets them be checked. This scenario is tested by
`TextChecker_Check_ThreeRepeatedLettersAtLowMinimum_AreReported`.

**TextChecker_Check_WordShorterThanMinimum_IsNotChecked**: A three-letter non-word is not checked at the default
minimum. This scenario is tested by `TextChecker_Check_WordShorterThanMinimum_IsNotChecked`.

**TextChecker_Check_WordAtMinimumLength_IsChecked**: A four-letter non-word is checked at the default minimum. This
scenario is tested by `TextChecker_Check_WordAtMinimumLength_IsChecked`.

**TextChecker_Check_MinimumLengthRaised_SkipsShorterWords**: With the minimum at six, a five-letter misspelling is
skipped and a seven-letter one is reported. This scenario is tested by
`TextChecker_Check_MinimumLengthRaised_SkipsShorterWords`.

**TextChecker_Check_MinimumLengthLowered_ChecksShorterWords**: With the minimum at three, a three-letter non-word is
reported. This scenario is tested by `TextChecker_Check_MinimumLengthLowered_ChecksShorterWords`.

**TextChecker_Check_MinimumLength_CountsUtf16Units**: A word of two characters outside the Basic Multilingual Plane
is long enough for a minimum of four and too short for five. This scenario is tested by
`TextChecker_Check_MinimumLength_CountsUtf16Units`.

**TextChecker_Check_ShortPieceOfUnknownWord_IsNotReported**: An unknown piece of an identifier that is shorter than
the minimum is not reported. This scenario is tested by `TextChecker_Check_ShortPieceOfUnknownWord_IsNotReported`.

**TextChecker_Check_WordsEntry_AcceptsAnyCase**: A lower-case words entry accepts the word in three letter cases.
This scenario is tested by `TextChecker_Check_WordsEntry_AcceptsAnyCase`.

**TextChecker_Check_WordsEntryWithCapital_AcceptsAnyCase**: A words entry with a capital letter also accepts the word
in lower case. This scenario is tested by `TextChecker_Check_WordsEntryWithCapital_AcceptsAnyCase`.

**TextChecker_Check_IgnoreWordsEntry_IsNotReported**: An ignored misspelling is not reported while another is. This
scenario is tested by `TextChecker_Check_IgnoreWordsEntry_IsNotReported`.

**TextChecker_Check_WordBothIgnoredAndFlagged_IsNotReported**: Ignored wins over flagged. This scenario is tested by
`TextChecker_Check_WordBothIgnoredAndFlagged_IsNotReported`.

**TextChecker_Check_WordBothAcceptedAndFlagged_IsReported**: Flagged wins over accepted. This scenario is tested by
`TextChecker_Check_WordBothAcceptedAndFlagged_IsReported`.

**TextChecker_Check_KnownWordThatIsFlagged_IsReported**: Flagged wins over the dictionary, so a known word that is
flagged is reported with the flagged kind. This scenario is tested by
`TextChecker_Check_KnownWordThatIsFlagged_IsReported`.

**TextChecker_Check_WordsEntryWithDigits_MatchesWholeToken**: An entry with a digit accepts the whole token and not
the bare letters. This scenario is tested by `TextChecker_Check_WordsEntryWithDigits_MatchesWholeToken`.

**TextChecker_Check_IgnoreEntryWithPunctuation_MatchesWholeToken**: A hyphenated ignore entry ignores the hyphenated
token and not the two words written apart. This scenario is tested by
`TextChecker_Check_IgnoreEntryWithPunctuation_MatchesWholeToken`.

**TextChecker_Check_FlagWords_ReportsFlaggedKind**: A flagged word is reported with the flagged kind, its position,
and no suggestions. This scenario is tested by `TextChecker_Check_FlagWords_ReportsFlaggedKind`.

**TextChecker_Check_FlagEntryWithArrow_ReportsSuggestions**: The arrow form attaches both suggestions in order. This
scenario is tested by `TextChecker_Check_FlagEntryWithArrow_ReportsSuggestions`.

**TextChecker_Check_FlagEntryWithColon_ReportsSuggestion**: The colon form attaches its suggestion and reports the
word, not the entry. This scenario is tested by `TextChecker_Check_FlagEntryWithColon_ReportsSuggestion`.

**TextChecker_Check_FlaggedWordShorterThanMinimum_IsStillReported**: A three-letter flagged word is reported at the
default minimum. This scenario is tested by `TextChecker_Check_FlaggedWordShorterThanMinimum_IsStillReported`.

**TextChecker_Check_FlagEntryInLowerCase_MatchesAnyCase**: A lower-case flag entry flags the word in three letter
cases. This scenario is tested by `TextChecker_Check_FlagEntryInLowerCase_MatchesAnyCase`.

**TextChecker_Check_FlagEntryWithCapital_MatchesOnlyThatSpelling**: A flag entry with a capital flags only that
spelling. This scenario is tested by `TextChecker_Check_FlagEntryWithCapital_MatchesOnlyThatSpelling`.

**TextChecker_Check_FlaggedPieceOfIdentifier_IsReportedAsFlagged**: A flagged word inside an identifier is reported,
as flagged, at its own position. This scenario is tested by
`TextChecker_Check_FlaggedPieceOfIdentifier_IsReportedAsFlagged`.

**TextChecker_Check_FlagEntryWithDigits_ReportsWholeToken**: A flag entry with a digit reports the whole token as one
flagged finding. This scenario is tested by `TextChecker_Check_FlagEntryWithDigits_ReportsWholeToken`.

**TextChecker_Check_DefaultLanguage_RejectsBritishSpelling**: With the default language a British spelling is
reported and the American one is not. This scenario is tested by
`TextChecker_Check_DefaultLanguage_RejectsBritishSpelling`.

**TextChecker_Check_BritishLanguage_AcceptsBritishNotAmericanSpelling**: With British English the American spelling
is reported and the British one is not. This scenario is tested by
`TextChecker_Check_BritishLanguage_AcceptsBritishNotAmericanSpelling`.

**TextChecker_Check_BothLanguages_AcceptEitherSpelling**: With both languages listed neither spelling is reported.
This scenario is tested by `TextChecker_Check_BothLanguages_AcceptEitherSpelling`.

**TextChecker_Check_LineFeed_StartsNewLine**: A line feed starts line two at column one. This scenario is tested by
`TextChecker_Check_LineFeed_StartsNewLine`.

**TextChecker_Check_CarriageReturnLineFeed_CountsOneLineBreak**: CRLF counts as one break, so lines two and three are
right. This scenario is tested by `TextChecker_Check_CarriageReturnLineFeed_CountsOneLineBreak`.

**TextChecker_Check_LoneCarriageReturn_IsNotLineBreak**: A carriage return alone does not start a line. This
scenario is tested by `TextChecker_Check_LoneCarriageReturn_IsNotLineBreak`.

**TextChecker_Check_LineSeparatorCharacter_IsNotLineBreak**: The Unicode line separator does not start a line. This
scenario is tested by `TextChecker_Check_LineSeparatorCharacter_IsNotLineBreak`.

**TextChecker_Check_AstralCharacter_CountsTwoColumns**: An emoji before a word moves its column by two. This scenario
is tested by `TextChecker_Check_AstralCharacter_CountsTwoColumns`.

**TextChecker_Check_ByteOrderMarkCharacter_CountsOneColumn**: A byte order mark character inside the text takes one
column and is not removed. This scenario is tested by `TextChecker_Check_ByteOrderMarkCharacter_CountsOneColumn`.

**TextChecker_Check_Tab_CountsOneColumn**: A tab takes one column. This scenario is tested by
`TextChecker_Check_Tab_CountsOneColumn`.

**TextChecker_Check_ManyLines_CountsLinesExactly**: After five thousand clean lines the finding is on the next line.
This scenario is tested by `TextChecker_Check_ManyLines_CountsLinesExactly`.

**TextChecker_Check_SeveralFindings_AreInTextOrder**: Findings on three lines, two on one line, follow the text.
This scenario is tested by `TextChecker_Check_SeveralFindings_AreInTextOrder`.

**TextChecker_Check_UnsupportedDirectiveBetweenWords_IsInTextOrder**: A directive between two misspellings is
reported between them. This scenario is tested by
`TextChecker_Check_UnsupportedDirectiveBetweenWords_IsInTextOrder`.

**TextChecker_Check_UnsupportedDirectiveBeforeWords_IsReportedFirst**: A directive on the first line is reported
before the word after it. This scenario is tested by
`TextChecker_Check_UnsupportedDirectiveBeforeWords_IsReportedFirst`.

**TextChecker_Check_DisableEnableBlock_SkipsRegion**: Words between disable and enable are not reported. This
scenario is tested by `TextChecker_Check_DisableEnableBlock_SkipsRegion`.

**TextChecker_Check_UnterminatedDisable_RunsToEndOfText**: A disable without an enable hides the rest of the text.
This scenario is tested by `TextChecker_Check_UnterminatedDisable_RunsToEndOfText`.

**TextChecker_Check_DisableLine_SkipsLine**: A disable-line directive hides its whole line, including the text before
it. This scenario is tested by `TextChecker_Check_DisableLine_SkipsLine`.

**TextChecker_Check_DisableNextLine_SkipsNextLine**: A disable-next-line directive hides the following line only.
This scenario is tested by `TextChecker_Check_DisableNextLine_SkipsNextLine`.

**TextChecker_Check_WordsDirective_AcceptsWordsInWholeDocument**: A words directive accepts its words before and
after it. This scenario is tested by `TextChecker_Check_WordsDirective_AcceptsWordsInWholeDocument`.

**TextChecker_Check_IgnoreDirective_AcceptsListedWords**: An ignore directive with a comma-separated list accepts
those words. This scenario is tested by `TextChecker_Check_IgnoreDirective_AcceptsListedWords`.

**TextChecker_Check_DocumentWords_DoNotLeakToNextCall**: Words added by a directive do not apply to the next call of
the same checker. This scenario is tested by `TextChecker_Check_DocumentWords_DoNotLeakToNextCall`.

**TextChecker_Check_DirectivePrefixInUpperCase_IsAccepted**: A directive written in capitals works. This scenario is
tested by `TextChecker_Check_DirectivePrefixInUpperCase_IsAccepted`.

**TextChecker_Check_CspellPrefix_IsAccepted**: A directive with the cspell prefix works. This scenario is tested by
`TextChecker_Check_CspellPrefix_IsAccepted`.

**TextChecker_Check_UnsupportedDirective_ReportsKindAndPosition**: An unsupported directive is reported with its kind,
its name, and the position where it is written. This scenario is tested by
`TextChecker_Check_UnsupportedDirective_ReportsKindAndPosition`.

**TextChecker_Check_UnsupportedDirectiveInDisabledRegion_IsNotReported**: An unsupported directive inside a disabled
region is not reported. This scenario is tested by
`TextChecker_Check_UnsupportedDirectiveInDisabledRegion_IsNotReported`.

**TextChecker_Check_UrlEmailAndHash_AreNotChecked**: A URL, an email address, and a SHA-256 value are not checked
while the misspelling after them is. This scenario is tested by `TextChecker_Check_UrlEmailAndHash_AreNotChecked`.

**TextChecker_Check_Uuid_IsNotChecked**: A UUID between two words is not checked. This scenario is tested by
`TextChecker_Check_Uuid_IsNotChecked`.

**TextChecker_Check_TextAroundExcludedRange_IsStillChecked**: Misspellings on both sides of a URL are reported. This
scenario is tested by `TextChecker_Check_TextAroundExcludedRange_IsStillChecked`.

**TextChecker_Check_WordSplitByExcludedRange_IsCheckedAsSeparateSegments**: Letters that a URL cuts off are reported
alone and are not joined with the URL's letters. This scenario is tested by
`TextChecker_Check_WordSplitByExcludedRange_IsCheckedAsSeparateSegments`.

**TextChecker_Constructor_NullSettings_ThrowsArgumentNullException**: Null settings are rejected and the exception
names the parameter. This scenario is tested by `TextChecker_Constructor_NullSettings_ThrowsArgumentNullException`.

**TextChecker_Constructor_UnsupportedLanguage_ThrowsNamingLanguage**: An unsupported language is rejected with a
message that names it. This scenario is tested by `TextChecker_Constructor_UnsupportedLanguage_ThrowsNamingLanguage`.

**TextChecker_Constructor_UnsupportedLanguageAndBadEntry_ReportsLanguage**: When both are wrong the language is the
problem reported. This scenario is tested by
`TextChecker_Constructor_UnsupportedLanguageAndBadEntry_ReportsLanguage`.

**TextChecker_Constructor_BlankEntry_ThrowsNamingSetting**: An empty entry is rejected naming the setting. This
scenario is tested by `TextChecker_Constructor_BlankEntry_ThrowsNamingSetting`.

**TextChecker_Constructor_WhiteSpaceEntry_ThrowsNamingSettingAndEntry**: An entry with two words is rejected naming
the setting and the entry. This scenario is tested by
`TextChecker_Constructor_WhiteSpaceEntry_ThrowsNamingSettingAndEntry`.

**TextChecker_Constructor_ForbiddenMarkerEntry_ThrowsNamingSettingAndEntry**: An entry that begins with the
forbidden-word marker is rejected. This scenario is tested by
`TextChecker_Constructor_ForbiddenMarkerEntry_ThrowsNamingSettingAndEntry`.

**TextChecker_Constructor_DisallowedCharacterEntry_ThrowsNamingSettingAndEntry**: An entry with a character that
cannot be part of a word is rejected. This scenario is tested by
`TextChecker_Constructor_DisallowedCharacterEntry_ThrowsNamingSettingAndEntry`.

**TextChecker_Constructor_EntryWithoutLetterOrDigit_ThrowsNamingSettingAndEntry**: An entry of hyphens only is
rejected. This scenario is tested by
`TextChecker_Constructor_EntryWithoutLetterOrDigit_ThrowsNamingSettingAndEntry`.

**TextChecker_Constructor_FlagEntryWithoutWord_ThrowsNamingSetting**: A flag entry that has suggestions and no word
is rejected. This scenario is tested by `TextChecker_Constructor_FlagEntryWithoutWord_ThrowsNamingSetting`.

**TextChecker_Constructor_SuggestionFormInWords_ThrowsNamingSetting**: The suggestion form is not accepted in the
words setting. This scenario is tested by `TextChecker_Constructor_SuggestionFormInWords_ThrowsNamingSetting`.

**TextChecker_Check_ConcurrentCalls_GiveIdenticalResults**: Eight threads, started together, make a hundred calls
each on one checker with a text that has flags, directive words, and an unsupported directive; every result equals
the sequential result. This scenario is tested by `TextChecker_Check_ConcurrentCalls_GiveIdenticalResults`.

**TextChecker_Check_Issue_ExposesAllProperties**: A flagged finding exposes its file path, line, column, text, kind,
and suggestions. This scenario is tested by `TextChecker_Check_Issue_ExposesAllProperties`.

**TextChecker_Check_IssueWithFileName_FormatsCompilerStyle**: A finding with a file name formats as
`file(line,column): error: message`. This scenario is tested by
`TextChecker_Check_IssueWithFileName_FormatsCompilerStyle`.

**TextChecker_Check_IssueWithoutFileName_FormatsPositionOnly**: Without a file name only the position is given. This
scenario is tested by `TextChecker_Check_IssueWithoutFileName_FormatsPositionOnly`.

**TextChecker_Check_FlaggedIssueWithSuggestions_FormatsSuggestions**: The message of a flagged finding lists its
suggestions. This scenario is tested by `TextChecker_Check_FlaggedIssueWithSuggestions_FormatsSuggestions`.

**TextChecker_Check_UnsupportedDirectiveIssue_FormatsDirectiveMessage**: The message of an unsupported directive
names the directive. This scenario is tested by `TextChecker_Check_UnsupportedDirectiveIssue_FormatsDirectiveMessage`.

**TextChecker_Check_IssueSuggestions_AreImmutable**: The suggestions of a finding cannot be changed by casting them
to a mutable interface. This scenario is tested by `TextChecker_Check_IssueSuggestions_AreImmutable`.

**TextChecker_Format_CallerFileName_ReplacesRecordedFileName**: A finding recorded under a relative name is formatted
under an absolute name chosen by the caller, and with no name at all; only the file name changes. This scenario is
tested by `TextChecker_Format_CallerFileName_ReplacesRecordedFileName`.

**TextChecker_Check_IgnoreRegExpListExpression_SkipsMatches**: An expression of the ignore list skips the text it
matches, and a near miss next to it is reported at its position. This scenario is tested by
`TextChecker_Check_IgnoreRegExpListExpression_SkipsMatches`.

**TextChecker_Check_IgnoreRegExpListNames_ApplyNamedAndPredefinedPatterns**: A defined name and the predefined `string`
name each skip their matches, and the one word that neither matches is reported. This scenario is tested by
`TextChecker_Check_IgnoreRegExpListNames_ApplyNamedAndPredefinedPatterns`.

**TextChecker_Check_PatternSpanningLinesAndCuttingAWord_ExcludesOnlyTheMatch**: A pattern that runs from one marker to
another across lines skips the whole block, and a pattern that cuts a word in two leaves the rest of the word to be
checked as its own word, at its own column. This scenario is tested by
`TextChecker_Check_PatternSpanningLinesAndCuttingAWord_ExcludesOnlyTheMatch`.

**TextChecker_Check_PatternMatchingEmptyText_ExcludesOnlyRealMatches**: A pattern that can match the empty string skips
only its runs of characters, and the words around them are still reported. This scenario is tested by
`TextChecker_Check_PatternMatchingEmptyText_ExcludesOnlyRealMatches`.

**TextChecker_Check_CustomPatterns_AreAddedToTheDefaultPatterns**: With a custom pattern set, a URL is still skipped, as
is the custom match, and the unknown word is reported. This scenario is tested by
`TextChecker_Check_CustomPatterns_AreAddedToTheDefaultPatterns`.

**TextChecker_Check_PatternWithoutMatch_ChangesNothing**: A pattern that matches nothing in the text gives the same
findings as no pattern. This scenario is tested by `TextChecker_Check_PatternWithoutMatch_ChangesNothing`.

**TextChecker_Constructor_InvalidIgnoreRegExpListEntry_ThrowsInvalidPatternException**: An invalid expression, an
unsupported flag, an unsupported escape, an unknown name, and an empty entry, in settings built in code, are each
rejected when the checker is created, with an exception that names the entry and is an argument exception. This
scenario is tested by `TextChecker_Constructor_InvalidIgnoreRegExpListEntry_ThrowsInvalidPatternException`.

**TextChecker_Constructor_InvalidNamedPatterns_ThrowInvalidPatternException**: A definition that reuses a predefined
name, two definitions whose names differ only in letter case, and a definition with an invalid expression are each
rejected. This scenario is tested by `TextChecker_Constructor_InvalidNamedPatterns_ThrowInvalidPatternException`.

**TextChecker_Check_CatastrophicCustomPattern_ThrowsRegexMatchTimeoutException**: A nested quantifier that backtracks
without end, on a text that almost matches it, makes the check throw the time-out exception of the regular expression
engine at the limit instead of hanging. The checker is made with the internal limit of 100 milliseconds, which a
catastrophic pattern reaches on a text of sixty-one characters, and the exception is the evidence that the limit is
applied. This scenario is tested by `TextChecker_Check_CatastrophicCustomPattern_ThrowsRegexMatchTimeoutException`.

**TextChecker_Check_CatastrophicDirectivePattern_ThrowsRegexMatchTimeoutException**: A directive in the text writes a
nested quantifier that backtracks without end, and a line that almost matches it follows; a checker with the internal
limit of 100 milliseconds throws the time-out exception instead of hanging. This scenario is tested by
`TextChecker_Check_CatastrophicDirectivePattern_ThrowsRegexMatchTimeoutException`.

**TextChecker_New_MatchTimeout_IsFiveSecondsAndMustBePositive**: A checker made with the public constructor has the
production limit of five seconds, the constructor with a limit rejects a limit of zero with an out-of-range error, and
a null setting throws a null-argument error. This scenario is tested by
`TextChecker_New_MatchTimeout_IsFiveSecondsAndMustBePositive`.

**TextChecker_Check_IgnoreRegExpDirective_HidesMatchesInTheWholeText**: A directive with a pattern between a
misspelling and a second one hides the invented word before it and the one after it, and the two misspellings are
reported. This scenario is tested by `TextChecker_Check_IgnoreRegExpDirective_HidesMatchesInTheWholeText`.

**TextChecker_Check_IgnoreRegExpDirectiveForms_UseTheFlagsAsWritten**: A literal without flags, a literal with the case
flag, and a bare word are each written in a directive; the words in both letter cases are hidden except the capitals
that the literal without the flag cannot match, which is the only finding. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveForms_UseTheFlagsAsWritten`.

**TextChecker_Check_IgnoreRegExpDirectiveNames_RefersToSettingsAndPredefinedPatterns**: A directive that names a pattern
of the settings in another letter case and one that names a predefined pattern each hide their matches, and the
unknown word beside them is the only finding. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveNames_RefersToSettingsAndPredefinedPatterns`.

**TextChecker_Check_IgnoreRegExpDirectiveInvalid_IsReportedAtItsPosition**: A directive with an inline option, which
cannot be used, is reported as an invalid directive at the position of its pattern, with the reason as its detail and a
message in the compiler style that names the pattern and the directive; the word the pattern would have hidden is still
reported, and an ordinary finding has no detail. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveInvalid_IsReportedAtItsPosition`.

**TextChecker_Check_IgnoreRegExpDirectiveUnknownName_IsReportedAndHidesNothing**: A directive with a misspelled
predefined name is reported at the name, with a reason that says it is not a defined or predefined pattern name, and
the word it spells is still reported. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveUnknownName_IsReportedAndHidesNothing`.

**TextChecker_Check_IgnoreRegExpDirectiveInDisabledBlock_AppliesButIsNotReported**: A valid directive and an invalid one
inside a disabled block; after the block the valid pattern hides its word, nothing is reported for the invalid one, and
the unknown word is the only finding. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveInDisabledBlock_AppliesButIsNotReported`.

**TextChecker_Check_IgnoreRegExpDirectiveRepeated_IsReportedAtEachPlaceInOrder**: An unusable pattern written twice,
with a misspelling after each, gives four findings in text order, the pattern at each of its places. This scenario is
tested by `TextChecker_Check_IgnoreRegExpDirectiveRepeated_IsReportedAtEachPlaceInOrder`.

**TextChecker_Check_IgnoreRegExpDirectiveWithoutPattern_IsReportedAsUnsupported**: A directive whose pattern is on the
next line has no pattern, and the one finding is an unsupported directive named ignoreRegExp. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveWithoutPattern_IsReportedAsUnsupported`.

**TextChecker_Check_IgnoreRegExpDirectivePatternLimit_RejectsTheExcessWithoutReadingIt**: A text with one more different
pattern than the limit of 32, each written twice, and a word that the last pattern would hide, reports the last
pattern at both of its places with a reason that names the limit, and the word it would have hidden is reported, so the
excess was not read. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectivePatternLimit_RejectsTheExcessWithoutReadingIt`.

**TextChecker_Check_IgnoreRegExpDirectiveNamesDifferingInCase_CountOnceTowardsTheLimit**: A text that names 31 patterns
of the settings in three letter cases each, and a predefined name in two, is within the limit of 32 because names that
differ only in case are one pattern; no directive is rejected and only the misspelling is reported. This scenario is
tested by `TextChecker_Check_IgnoreRegExpDirectiveNamesDifferingInCase_CountOnceTowardsTheLimit`.

**TextChecker_Check_IgnoreRegExpDirectiveExpressionsDifferingInCase_CountSeparatelyTowardsTheLimit**: A text with 31
different literal expressions and then one expression in two letter cases has 33 different patterns, because an
expression is case-significant; the second letter case is the one reported over the limit. This scenario is tested by
`TextChecker_Check_IgnoreRegExpDirectiveExpressionsDifferingInCase_CountSeparatelyTowardsTheLimit`.

**TextChecker_Check_MarkdownFile_SkipsLinkTargetsLabelsAnchorsAndEntities**: A line with a link, a reference, an anchor,
and an entity checked under a Markdown name, in upper case, reports only the word outside them; under a text name the
same line reports more. This scenario is tested by
`TextChecker_Check_MarkdownFile_SkipsLinkTargetsLabelsAnchorsAndEntities`.

**TextChecker_Check_NoSourceName_DoesNotApplyMarkdownPatterns**: A link target is reported when the text has no source
name. This scenario is tested by `TextChecker_Check_NoSourceName_DoesNotApplyMarkdownPatterns`.

**TextChecker_Check_LongRandomLookingToken_IsSkipped**: A token of forty-six characters made of invented words, digits,
and hyphens, with the default settings, yields no finding. This scenario is tested by
`TextChecker_Check_LongRandomLookingToken_IsSkipped`.

**TextChecker_Check_RandomLookingTokenWhenNotSkipped_IsCheckedAsWords**: The same token is checked word by word when
the random-string setting is off or the minimum random length is one above its length, and is skipped when the minimum
equals its length. This scenario is tested by `TextChecker_Check_RandomLookingTokenWhenNotSkipped_IsCheckedAsWords`.

**TextChecker_Check_LongIdentifierOfWords_IsStillChecked**: A long identifier made of three invented words repeated
does not look random, so each of its twelve pieces is reported. This scenario is tested by
`TextChecker_Check_LongIdentifierOfWords_IsStillChecked`.

**TextChecker_Check_FlaggedWordInsideRandomLookingToken_IsNotReported**: A flagged word inside a random-looking token is
not reported, and the same word alone is. This scenario is tested by
`TextChecker_Check_FlaggedWordInsideRandomLookingToken_IsNotReported`.

**TextChecker_Check_WordsInsideHexSequence_AreNotReported**: In a token whose tail is a hexadecimal sequence of two
unknown words and a number, only the word before the sequence is reported by default, and all three are reported when
random strings are not ignored. This scenario is tested by `TextChecker_Check_WordsInsideHexSequence_AreNotReported`.

**TextChecker_Check_MixedCaseHexSequenceOrFlaggedWord_StaysReported**: A hexadecimal sequence in mixed letter case hides
no word, and a flagged word inside a sequence is still reported as flagged. This scenario is tested by
`TextChecker_Check_MixedCaseHexSequenceOrFlaggedWord_StaysReported`.

**TextChecker_Check_ContractionOfAcceptableParts_IsAccepted**: Contractions whose parts are known, too short, accepted
by the words setting, or a run of capitals with an ending are accepted, whether the apostrophe is straight, curly, or
escaped, and whether the word is in camel case. This scenario is tested by
`TextChecker_Check_ContractionOfAcceptableParts_IsAccepted`.

**TextChecker_Check_ContractionWithUnknownPart_IsReportedWhole**: Contractions that have an unknown part of the minimum
length or more, in any position, are reported as one word with their apostrophes. This scenario is tested by
`TextChecker_Check_ContractionWithUnknownPart_IsReportedWhole`.

**TextChecker_Check_ContractionWithFlaggedOrCheckedPart_IsReported**: A contraction with a flagged part, and one whose
short part is long enough to be checked once the minimum word length is lowered, are reported whole. This scenario is
tested by `TextChecker_Check_ContractionWithFlaggedOrCheckedPart_IsReported`.

**TextChecker_Check_ContractionAfterBackslash_RetriesWithoutEscapeLetter**: A contraction that follows a backslash and
whose first part is a known word behind an escape letter is accepted, so nothing is reported. This scenario is tested
by `TextChecker_Check_ContractionAfterBackslash_RetriesWithoutEscapeLetter`.

<!-- cspell:ignore wrold speling xqzv xqz -->
