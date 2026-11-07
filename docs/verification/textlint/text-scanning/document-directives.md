### DocumentDirectives

#### Verification Approach

`DocumentDirectives` is verified with unit tests in `DocumentDirectivesTests.cs`. The unit is pure logic over strings
and uses nothing but the .NET base class library and the time-limit constant of `DefaultPatterns`, so no mocks,
doubles, or files are used: each test passes a text to `Scan` and asserts on the text of the excluded ranges, the
words, the patterns of the ignoreRegExp directives, or the unsupported directives. The expected excluded text is cut
from the same text that is scanned, so the
expectations never repeat an offset. Behavior that depends on the line terminator runs with both LF and CRLF as the
rows of one theory, which is how the LF and CRLF twins of a scenario are verified; the one deliberate difference, the
blank line after a next-line directive, has a scenario for each style. The behavior of a blank next line was verified
against cspell itself, and the tests record it. Tests with many samples of one behavior are theories whose rows are
the samples; each theory is one scenario below.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A disable block, a disable line, and a disable-next-line directive exclude exactly the documented text, with the same
  prefix pairing, unterminated, and last-line behavior as cspell.
- Every ignore and words form adds its words, in order and in their letter case, and the text of the directive is
  excluded.
- Every prefix, in any letter case, with a colon, two colons, or a space after the colon, is accepted.
- Text that resembles a directive (a prefix after a word character, a prefix with no name, a name that does not begin
  with a directive keyword) is not a directive and returns the shared empty scan.
- A hyphenated range form (`disable-next-foo`) acts as its keyword and is not reported; a name that only begins like a
  directive (`disabled`, `ignored`, `wordsmith`) hides nothing, adds nothing, and is reported at its name.
- Words and unsupported directives are read only from the first readable prefix of a line.
- The recorded cspell output for the directive-boundaries corpus agrees with the library except for the listed
  divergences (see the parity corpus verification in the system document).
- Every unsupported directive family is reported at its name, and one inside a disabled region is not.
- Every ignoreRegExp directive, with any prefix and spelling, adds its pattern as cspell's own reader reads it, with the
  range where it is written, in text order and also inside a disabled region, where it is not reportable; its text is
  excluded, it is not reported as unsupported unless it has no pattern on its line, and a second prefix on a line adds
  no pattern.
- LF and CRLF give the same results, and positions count UTF-16 code units.
- A text of one megabyte is scanned completely and a null text is rejected.

#### Test Scenarios

**DocumentDirectives_Scan_DisableEnableBlock_ExcludesFromDisableToEnable**:
With LF and with CRLF, the range runs from the
disable keyword to the enable keyword, and the text before the disable and after the enable stays checked.

**DocumentDirectives_Scan_DisableAndEnableOnOneLine_ExcludesOnlyBetweenThem**:
A disable and an enable on one line exclude
only the text between them.

**DocumentDirectives_Scan_SeveralBlocks_AreEachExcluded**:
Two blocks are each excluded and the text between them is not.

**DocumentDirectives_Scan_EnableWithoutDisable_ExcludesNothing**: An enable directive with no disable returns the empty
scan.

**DocumentDirectives_Scan_EnableInDifferentLetterCase_ClosesBlock**: An upper-case disable is closed by a lower-case
enable.

**DocumentDirectives_Scan_EnableWithOtherPrefix_DoesNotCloseBlock**: A block opened with the spell-checker prefix is not
closed by an enable with the spell prefix, only by one with the same prefix.

**DocumentDirectives_Scan_EnableWithShorterPrefix_DoesNotCloseBlock**:
A block opened with the cspell prefix is not closed
by an enable with the spell prefix and runs to the end of the text.

**DocumentDirectives_Scan_DisableWithoutEnable_ExcludesToEndOfText**: With LF and with CRLF, a disable with no enable
excludes everything from the directive to the end.

**DocumentDirectives_Scan_DisableLine_ExcludesWholeLine**:
With LF and with CRLF, only the line that holds the directive is
excluded, including the text before and after the directive and excluding the line terminator.

**DocumentDirectives_Scan_DisableLineOnLastLineWithoutTerminator_ExcludesLine**: A directive on a single line with no
terminator excludes the whole text.

**DocumentDirectives_Scan_DisableLineBeforeLineSeparator_EndsAtSeparator**: A Unicode line separator ends the excluded
line.

**DocumentDirectives_Scan_DisableNextLine_ExcludesRestOfLineAndNextLine**:
With LF and with CRLF, the range starts at the
directive after code on its line and ends with the next line.

**DocumentDirectives_Scan_DisableNextShortForm_ExcludesNextLine**: The short form excludes the next line like the long
form.

**DocumentDirectives_Scan_DisableNextLineOnLastLineWithoutTerminator_ExcludesNothing**:
A directive on the last line with
no terminator excludes nothing.

**DocumentDirectives_Scan_BlankNextLineWithLf_ExcludesBlankAndFollowingLine**: With LF, a blank next line is excluded
together with the line after it.

**DocumentDirectives_Scan_BlankNextLineWithCrLf_ExcludesOnlyBlankLine**: With CRLF, only the blank line is excluded.

**DocumentDirectives_Scan_IgnoreDirectiveForms_AddIgnoredWords**: The five ignore forms add two ignored words and no
document words.

**DocumentDirectives_Scan_WordsDirectiveForms_AddWords**: Both words forms add two document words and no ignored words.

**DocumentDirectives_Scan_WordSeparators_AreCommaSemicolonAndWhiteSpace**: Words separated by commas, semicolons, tabs,
spaces, and doubled separators give exactly the listed words.

**DocumentDirectives_Scan_WordDirectiveLetterCase_IsInsensitiveForKeywordsOnly**: An upper-case ignore and a mixed-case
words directive are recognized, and the words keep their letter case.

**DocumentDirectives_Scan_SeveralWordDirectives_AppendWordsInOrder**: The words of interleaved directives are collected
in text order in the list of their kind.

**DocumentDirectives_Scan_WordsDirectiveAtLineEnd_EndsAtLineTerminator**: With LF and with CRLF, the words of the next
line are not taken and no terminator stays in a word.

**DocumentDirectives_Scan_WordDirectiveWithoutWords_AddsNoWords**: A directive with a keyword and no words, or only
separators, adds nothing.

**DocumentDirectives_Scan_WordDirectiveText_IsExcluded**:
With LF and with CRLF, the directive from its prefix to the end
of its line is excluded and the text before it is not.

**DocumentDirectives_Scan_WordsDirectiveInsideDisabledBlock_StillAddsWords**: A words directive inside a disabled block
still adds its word.

**DocumentDirectives_Scan_KeywordContinuedByLetters_AddsNoWords**: A keyword continued by letters or a hyphen adds no
word.

**DocumentDirectives_Scan_AnyPrefixForm_StartsDisableLine**:
Each of the five prefixes, in mixed case, with a space after
the colon, and with two colons, starts a disable-line directive that excludes the whole line.

**DocumentDirectives_Scan_AnyPrefixForm_StartsAndEndsBlock**: Each prefix form opens a block that its own enable closes.

**DocumentDirectives_Scan_AnyPrefixForm_StartsWordsDirective**: Each prefix form starts a words directive.

**DocumentDirectives_Scan_TextResemblingDirective_IsNotDirective**:
A prefix after a word character, a prefix without a colon, an enable alone, a prefix with no name, and a name
that does not begin with a directive keyword return the empty scan.

**DocumentDirectives_Scan_HyphenatedRangeForm_HidesLikeCspellAndIsNotReported**: A disable-next, disable-next-line, or
disable-line keyword continued by a hyphen hides the same text as the keyword and is not reported.

**DocumentDirectives_Scan_EnableContinuedByHyphen_ClosesBlockAndIsNotReported**: An enable keyword continued by a hyphen
closes the block and is not reported.

**DocumentDirectives_Scan_NameBeginningLikeDirective_IsReportedAndHasNoEffect**: Each of sixteen names that begin like
a directive (such as disabled, disable-foo, ignored, wordsmith, words-x, locals) is reported at its name, excludes
nothing, and adds no word.

**DocumentDirectives_Scan_NameBeginningLikeDirectiveInsideBlock_IsNotReported**: Such a name inside a disabled block is
not reported.

**DocumentDirectives_Scan_IgnoreRegExpWithArgument_HidesItsLineAndAddsItsPattern**: An ignoreRegExp directive with an
argument excludes its text to the end of the line, adds its pattern with the range where it is written, which is
reportable, and is not reported as unsupported.

**DocumentDirectives_Scan_IgnoreRegExpArgument_IsReadLikeCspell**: Each row, an argument and the pattern that cspell's
own reader returns for it, gives the pattern that is found, and the range holds exactly that text. The rows cover a
slash pattern, one followed by trailing words, a pattern followed by letters that are not flags, a bare word, a bare
word followed by other words, a longest stretch from the first slash to the last slash, a pattern that follows a word,
a word that holds a slash, the flag letters g, i, m, u, and y, an s that is not a flag letter and is left out, an inline
option, a trailing backslash, leading white space and a tab, and a pattern with no closing slash.

**DocumentDirectives_Scan_IgnoreRegExpPrefixesAndSpellings_AllAddThePattern**: Each of nine forms, with the cspell,
spell, spell-checker, and spellchecker prefixes, the textlint prefix, upper case, two colons, a space after the colon,
and a name spelled with underscores, adds its pattern and is not reported as unsupported.

**DocumentDirectives_Scan_IgnoreRegExpInsideDisabledBlock_AddsPatternButIsNotReportable**: Three directives, one before
a disabled block, one inside it, and one after it, all add their patterns in text order, and only the one inside the
block is not reportable.

**DocumentDirectives_Scan_IgnoreRegExpWithoutPattern_IsReportedAtItsName**: A directive with nothing after its name on
its line, and its argument on the next line, adds no pattern and is reported as unsupported at its name.

**DocumentDirectives_Scan_IgnoreRegExpLineTerminators_EndThePattern**: With LF, CRLF, a lone carriage return, and a
Unicode line separator, the pattern of each of two directives ends at the end of its own line.

**DocumentDirectives_Scan_IgnoreRegExpAfterAnotherPrefixOnTheLine_IsNotRead**: A second prefix on a line, after a words
directive, adds no pattern.

**DocumentDirectives_Scan_IgnoreRegExpInLargeText_IsFound**: A directive at the end of a text of one megabyte is found
at its position.

**DocumentDirectives_Scan_RangeOnlyPrefix_HidesButIsNotRead**: A prefix that only the range patterns accept still hides
a line but a name after it is not reported.

**DocumentDirectives_Scan_SecondPrefixOnLine_IsNotReadForWords**: A second prefix on a line adds no words of its own;
the words of the first directive include its text.

**DocumentDirectives_Scan_EnablePrecededByLetter_DoesNotCloseBlock**: An enable keyword whose prefix follows a letter
does not close the block, which runs to the end of the text.

**DocumentDirectives_Scan_UnsupportedDirective_IsReportedAtItsName**: Each unsupported family (flag, forbid, locale
and its alias local, language, dictionary, includeRegExp, and the compound-word and case-sensitivity
switches), in several letter cases and prefix forms, is reported once, with a range that is exactly its name.

**DocumentDirectives_Scan_UnsupportedDirective_HasNoOtherEffect**:
An unsupported directive neither excludes text nor adds
words.

**DocumentDirectives_Scan_UnsupportedDirectiveAfterSurrogatePair_PositionCountsUtf16Units**: The name of a directive
after an emoji is positioned in code units.

**DocumentDirectives_Scan_SeveralUnsupportedDirectives_AreReportedInTextOrder**: Three unsupported directives are
reported in the order written.

**DocumentDirectives_Scan_UnsupportedDirectiveInsideDisabledBlock_IsNotReported**: An unsupported directive between a
disable and an enable is not reported.

**DocumentDirectives_Scan_UnsupportedDirectiveOnHiddenNextLine_IsNotReported**: An unsupported directive on the line
hidden by a next-line directive is not reported.

**DocumentDirectives_Scan_UnsupportedDirectivesAroundDisabledBlock_AreReported**:
Unsupported directives before and after a
block are reported and the one inside is not.

**DocumentDirectives_Scan_TextWithoutDirectives_ReturnsEmptyScan**:
An empty text, plain text, and text that mentions the
words the search looks for, without a directive, return the shared empty scan.

**DocumentDirectives_Scan_EmptyScan_HoldsNothing**: The shared empty scan has no ranges, no words, and no unsupported
directives.

**DocumentDirectives_Scan_BlockAfterSurrogatePair_RangeCountsUtf16Units**: The range of a block after an emoji starts
after its two code units.

**DocumentDirectives_Scan_OneMegabyteWithoutDirective_ReturnsEmptyScan**: A single line of one megabyte that holds the
search words but no directive returns the empty scan.

**DocumentDirectives_Scan_OneMegabyteWithDirectiveAtEnd_FindsDirective**:
A directive at the end of a text of one megabyte
is found and only its line is excluded.

**DocumentDirectives_Scan_NullText_ThrowsArgumentNullException**: A null text throws.

**DocumentDirectives_Scan_JavaScriptWhiteSpace_SeparatesLikeCspell**: A prefix is followed by the JavaScript white
space (the byte order mark and the no-break space separate; the next-line character does not), the words of an `ignore`
directive are split on that set, and a pattern name is trimmed of it; the rows were checked against cspell.

**DocumentDirectives_Scan_KelvinSignInPrefix_IsNotReadAsLetterK**: The prefix `spell-checker` is read with the
letter k in either case and is not read when the Kelvin sign takes the place of the k.

**DocumentDirectives_Scan_LookalikeLetters_AreNotLettersOfADirective**: A prefix or a name with the long s, the dotless
i, the dotted capital I, or the Kelvin sign in place of a letter (and `spell-checker` with the Kelvin sign for the k) is
not a directive and hides nothing, while an upper-case or mixed-case ASCII directive, and a Kelvin sign before a prefix
(which is not a letter), are read; each row was recorded from cspell.

**DocumentDirectives_Scan_LongSAfterWordKeyword_IsNotTheLetterS**: A words directive whose keyword is followed by the
long s adds the same word as one followed by the letter s.

**DocumentDirectives_Scan_DotlessIInIgnoreRegExp_IsNotThatDirective**: A pattern directive name that begins with the
dotless i adds no pattern, as in cspell.

<!-- cspell:ignore dotless -->
