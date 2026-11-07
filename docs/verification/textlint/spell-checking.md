## SpellChecking

### Verification Approach

The `SpellChecking` subsystem is verified by integration tests in `SpellCheckingSubsystemTests.cs`. They drive
`TextChecker`, the file reader behind it, and `SpellChecker` together through the public boundary of the subsystem.
Tests that concern a string of text call `TextChecker.Check`; tests that concern files create a per-test temporary
directory and call `SpellChecker.CheckAsync`. The documented dependencies of the subsystem are real in every test:
the settings value of the `Configuration` subsystem, the file selector of the `FileSelection` subsystem, the
embedded dictionaries behind the spelling dictionary of the `Dictionaries` subsystem, and the units of the
`TextScanning` subsystem. No mocking library is used and no test double replaces a dependency. Words are invented
or misspelled on purpose (`wrold`, `speling`) so that no dictionary can accidentally know them. The tests prove the
subsystem requirements; the details of each rule are proved by the unit tests of the three units.

### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test uses
its own uniquely named directory, so tests share no state and may run in parallel. The locked-file scenario opens
a file with an exclusive share mode, which makes a second open fail on Windows, and on Linux and macOS through the
advisory locking that the runtime applies to the same share mode, so it holds on all of them. The tests run on
Windows, Linux, and macOS.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- An unknown word in a text is reported with its line, column, and text, and known words, short words, possessives,
  and escapes are not.
- Identifiers are split and only the unknown pieces are reported.
- The word lists apply in the order ignored, flagged, accepted, dictionary.
- Directives switch checking off and add words, an unsupported directive is reported at its position, and URLs and
  email addresses are not checked.
- The language setting changes the result, and lines are counted by line feed alone.
- Unusable settings are rejected with a message that names the problem.
- Files that the settings select are checked and reported with file, line, column, and word; ignored and unselected
  files are not checked.
- UTF-8 and UTF-16 files, with and without a byte order mark, report identical positions.
- Binary files and files over the size limit are skipped and reported; an unreadable file is reported without
  stopping the run, and a missing root directory is an error.
- Results are identical and in the same order for one worker and for many, also when runs overlap; an invalid degree
  of parallelism is rejected; a canceled run throws and returns nothing.
- Text that matches an expression or a named pattern of the settings, or the pattern of an ignoreRegExp directive of
  the text, is not checked; a pattern that cannot be used is rejected when the checker is created, and a directive
  pattern that cannot be used is reported at its position with the reason.
- The link targets of a Markdown file are not checked, and the same text in a file with another name is.
- A token that looks like random data is not checked unless the setting is off, and a contraction is accepted when its
  parts are, and reported whole otherwise.
- A file on which a pattern exceeds its time limit is reported as a file that took too long and the run continues. No
  subsystem test waits for the limit; this behavior is proved by the unit tests of `TextChecker` and `SpellChecker`
  and by a system test with a catastrophic pattern, which run with a limit of 100 milliseconds through internal seams,
  while the production limit of five seconds is checked by a unit test of `TextChecker`.

### Test Scenarios

**SpellCheckingSubsystem_Check_UnknownWord_ReportsLineColumnAndWord**: A two-line text whose second line holds a
misspelling yields one finding at the right line and column. This scenario is tested by
`SpellCheckingSubsystem_Check_UnknownWord_ReportsLineColumnAndWord`.

**SpellCheckingSubsystem_Check_KnownWords_ReportsNothing**: Ordinary words with punctuation and a contraction yield
no finding. This scenario is tested by `SpellCheckingSubsystem_Check_KnownWords_ReportsNothing`.

**SpellCheckingSubsystem_Check_CamelAndSnakeCase_ReportsEachUnknownWord**: A camel-case and a snake-case identifier
each report only their misspelled piece, at its own column. This scenario is tested by
`SpellCheckingSubsystem_Check_CamelAndSnakeCase_ReportsEachUnknownWord`.

**SpellCheckingSubsystem_Check_ShortWord_IsNotChecked**: A non-word shorter than the minimum length is not
reported. This scenario is tested by `SpellCheckingSubsystem_Check_ShortWord_IsNotChecked`.

**SpellCheckingSubsystem_Check_PossessiveAndBackslashEscape_AreAccepted**: A possessive of a known word and a known
word after a backslash escape yield no finding. This scenario is tested by
`SpellCheckingSubsystem_Check_PossessiveAndBackslashEscape_AreAccepted`.

**SpellCheckingSubsystem_Check_WordLists_ApplyPrecedence**: With a word in all three lists, an accepted word, a
flagged known word, and an unknown word, only the flagged known word and the unknown word are reported. This
scenario is tested by `SpellCheckingSubsystem_Check_WordLists_ApplyPrecedence`.

**SpellCheckingSubsystem_Check_Directives_ControlChecking**: A disabled region and a words directive hide their
words and the remaining misspellings are reported with the right positions. This scenario is tested by
`SpellCheckingSubsystem_Check_Directives_ControlChecking`.

**SpellCheckingSubsystem_Check_UnsupportedDirective_IsReportedAtItsPosition**: A directive that is not supported is
reported after the misspelling that precedes it, with the kind unsupported directive. This scenario is tested by
`SpellCheckingSubsystem_Check_UnsupportedDirective_IsReportedAtItsPosition`.

**SpellCheckingSubsystem_Check_UrlsAndEmails_AreNotChecked**: Misspellings inside a URL and an email address are not
reported while the one after them is. This scenario is tested by
`SpellCheckingSubsystem_Check_UrlsAndEmails_AreNotChecked`.

**SpellCheckingSubsystem_Check_BritishLanguage_ChangesResult**: A British spelling is reported with the default
language and accepted with the British language. This scenario is tested by
`SpellCheckingSubsystem_Check_BritishLanguage_ChangesResult`.

**SpellCheckingSubsystem_Check_CrLfAndLoneCr_CountLinesByLineFeed**: A CRLF break starts a line and a carriage
return alone does not. This scenario is tested by `SpellCheckingSubsystem_Check_CrLfAndLoneCr_CountLinesByLineFeed`.

**SpellCheckingSubsystem_Check_UnusableSettings_Throw**: An unsupported language and a word entry that holds two
words are each rejected with a message that names the problem. This scenario is tested by
`SpellCheckingSubsystem_Check_UnusableSettings_Throw`.

**SpellCheckingSubsystem_CheckAsync_FilesWithUnknownWords_ReportsFileLineColumnAndWord**: Two markdown files, one in a
subdirectory, are checked and each finding is formatted with its relative path, line, column, and word. This scenario
is tested by `SpellCheckingSubsystem_CheckAsync_FilesWithUnknownWords_ReportsFileLineColumnAndWord`.

**SpellCheckingSubsystem_CheckAsync_CleanFiles_ReportsNothing**: Files of known words yield no finding, count as
checked, and are no problem. This scenario is tested by `SpellCheckingSubsystem_CheckAsync_CleanFiles_ReportsNothing`.

**SpellCheckingSubsystem_CheckAsync_IgnoredAndUnselectedFiles_AreNotChecked**: Of a selected, an ignored, and an
unselected file that each hold a misspelling only the selected one is reported. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_IgnoredAndUnselectedFiles_AreNotChecked`.

**SpellCheckingSubsystem_CheckAsync_Utf8Utf16AndBom_ReportSamePositions**: The same text, with a character outside the
Basic Multilingual Plane, in UTF-8, UTF-8 with a mark, and both UTF-16 byte orders yields the same position in every
file. This scenario is tested by `SpellCheckingSubsystem_CheckAsync_Utf8Utf16AndBom_ReportSamePositions`.

**SpellCheckingSubsystem_CheckAsync_BinaryFile_IsSkippedAndReported**: A file with a NUL byte is reported as binary
and counted as skipped, and the text file beside it is checked. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_BinaryFile_IsSkippedAndReported`.

**SpellCheckingSubsystem_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported**: A file larger than the maximum file
size is reported as too large and has no findings. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported`.

**SpellCheckingSubsystem_CheckAsync_UnreadableFile_IsReportedAndRunContinues**: A file held open exclusively is
reported as an unreadable error, the readable file is still checked, and the run has problems. This scenario is
tested by `SpellCheckingSubsystem_CheckAsync_UnreadableFile_IsReportedAndRunContinues`.

**SpellCheckingSubsystem_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException**: A root directory that does not
exist throws instead of returning an empty result. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException`.

**SpellCheckingSubsystem_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults**: Forty files in several
directories are checked with one worker and with eight; the formatted results are equal and the findings are in
ordinal path order. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults`.

**SpellCheckingSubsystem_CheckAsync_ConcurrentRuns_ReturnIdenticalResults**: Several runs started together over the
same tree return the same result. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_ConcurrentRuns_ReturnIdenticalResults`.

**SpellCheckingSubsystem_Options_InvalidParallelism_ThrowsArgumentOutOfRangeException**: A degree of parallelism of
zero and a negative one are rejected when the options are created. This scenario is tested by
`SpellCheckingSubsystem_Options_InvalidParallelism_ThrowsArgumentOutOfRangeException`.

**SpellCheckingSubsystem_CheckAsync_CanceledToken_ThrowsOperationCanceledException**: A token that is already
canceled stops the run with a cancellation and no result. This scenario is tested by
`SpellCheckingSubsystem_CheckAsync_CanceledToken_ThrowsOperationCanceledException`.

**SpellCheckingSubsystem_CheckAsync_CallerFileNames_NameIssueAndDiagnostic**: A run over a text file with a
misspelling and a binary file is formatted under absolute names chosen by the caller; the names replace the
relative names, and formatting under the recorded names gives the same text as the default formatting. This
scenario is tested by `SpellCheckingSubsystem_CheckAsync_CallerFileNames_NameIssueAndDiagnostic`.

**SpellCheckingSubsystem_CheckText_CustomPatternsFromSettings_SkipMatches**: Settings with an expression and a named
pattern, a text with a match of each, and a word that neither matches yield a finding only for that word. This scenario
is tested by `SpellCheckingSubsystem_CheckText_CustomPatternsFromSettings_SkipMatches`.

**SpellCheckingSubsystem_Constructor_UnusablePattern_ThrowsInvalidPatternException**: Settings with an invalid
expression and settings with a name that nothing defines each fail when the checker is created, with the documented
exception. This scenario is tested by `SpellCheckingSubsystem_Constructor_UnusablePattern_ThrowsInvalidPatternException`.

**SpellCheckingSubsystem_CheckText_IgnoreRegExpDirective_HidesMatchesAndReportsUnusablePatterns**: Settings define a
pattern, and a text holds a directive that names it, a directive with an expression, a word each of them matches, one
before and one after its directive, and a directive with a pattern that cannot be used; the matched words are hidden,
wherever the directives are, and the findings are one misspelling and one invalid directive at the position of the
unusable pattern. This scenario is tested by
`SpellCheckingSubsystem_CheckText_IgnoreRegExpDirective_HidesMatchesAndReportsUnusablePatterns`.

**SpellCheckingSubsystem_CheckAsync_MarkdownFile_SkipsLinkTargetsOfThatFileOnly**: A Markdown file and a text file hold
the same link and the same misspelling; the link target is skipped in the Markdown file and reported in the text file.
This scenario is tested by `SpellCheckingSubsystem_CheckAsync_MarkdownFile_SkipsLinkTargetsOfThatFileOnly`.

**SpellCheckingSubsystem_CheckText_RandomLookingToken_IsSkippedUnlessSwitchedOff**: A long token of non-words, digits,
and hyphens yields no finding with the default settings and findings when the random-string setting is off. This
scenario is tested by `SpellCheckingSubsystem_CheckText_RandomLookingToken_IsSkippedUnlessSwitchedOff`.

**SpellCheckingSubsystem_CheckText_Contraction_IsAcceptedWhenItsPartsAre**: A contraction of a known word and a short
part is accepted, and one with an unknown part is reported whole with its apostrophe. This scenario is tested by
`SpellCheckingSubsystem_CheckText_Contraction_IsAcceptedWhenItsPartsAre`.

<!-- cspell:ignore wrold speling -->
