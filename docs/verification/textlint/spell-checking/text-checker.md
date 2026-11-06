### TextChecker

#### Verification Approach

`TextChecker` and the `SpellingIssue` findings it returns are verified with unit tests in `TextCheckerTests.cs`. The
tests use the unit's documented dependencies for real: the settings value, the embedded dictionaries behind the
spelling dictionary, and the text scanning units (tokenizer, default patterns, and directives); there is no test
double and no mocking library. Words are chosen to be unambiguous: `wrold` and `speling` are misspellings that no
dictionary holds, `xqzv` and `xqz` are non-words of four and three letters, and `hello` and `world` are ordinary
words. Each rule is proved with a success case, an error case, and a boundary case, and the hostile inputs are a
word of fifty thousand characters, a lone surrogate, a carriage return alone, a byte order mark character inside
text, and several threads that use one checker at once. Directive texts use the `textlint` prefix, or assemble the
`cspell` prefix from parts, so that the repository's own spell check does not read them as directives.

#### Test Environment

No setup beyond the standard test runner. The tests read no files and are independent, so they may run in parallel.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Unknown words are reported with source name, line, and column; known, empty, and punctuation-only texts report
  nothing; a null text is rejected.
- A known compound is not split, and an unknown identifier reports only its unknown pieces.
- Possessives, escapes, capital runs with endings, and repeated letters follow the known-word-form rules.
- The minimum length is counted in UTF-16 code units, and the boundary is exact.
- The word lists apply in the order ignored, flagged, accepted, dictionary, with the case rules of each list.
- Flagged words carry their suggestions, ignore the minimum length, and flag whole tokens when written with digits.
- Lines are counted by line feed alone, columns in UTF-16 code units; findings are in text order.
- Directives and ignored text are honored, and a null setting, an unsupported language, or an unusable entry is
  rejected naming the problem.
- Concurrent use gives identical results; findings are immutable and format in the compiler style.

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

<!-- cspell:ignore wrold speling xqzv xqz -->
