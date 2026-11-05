## WordListReader

### Verification Approach

`WordListReader` is verified with unit tests in `WordListReaderTests.cs`. The reader works on real files, so each
test writes its lists into its own temporary folder and calls the reader on it. The reader's one dependency,
`DictionaryFormat`, is used as it is; the expected merged keys and capitalization flags are written out
literally in the tests so that a change of the fold or the classification shows up as a failing expectation.

### Test Environment

N/A - standard test environment. Each test creates and removes its own temporary folder.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Formatting that carries no meaning (comments, blank lines, padding, line endings, byte order mark) changes
  neither the words nor the diagnostics.
- Every rule of the entry rule is enforced by an entry that breaks only that rule, and the report names the file,
  the line, and the entry.
- At most 100 problems are kept in detail and the rest are counted.
- Case variants and cross-file duplicates merge into one folded key whose flags record every form.

### Test Scenarios

**WordListReader_Read_CommentsAndBlankLines_AreIgnored**: a list with a comment, blank lines, and entries padded
with spaces and tabs yields exactly the two words. This scenario is tested by
`WordListReader_Read_CommentsAndBlankLines_AreIgnored`.

**WordListReader_Read_CrLfAndBom_Handled**: a list that starts with a byte order mark and uses CRLF yields its
words cleanly. This scenario is tested by `WordListReader_Read_CrLfAndBom_Handled`.

**WordListReader_FindWordListFiles_MixedExtensions_ReturnsOnlyTxtFilesInOrdinalOrder**: of a folder with
lists at two depths, a markdown file, and a `.TXT` file, exactly the three `.txt` files are found in ordinal
order. This scenario is tested by `WordListReader_FindWordListFiles_MixedExtensions_ReturnsOnlyTxtFilesInOrdinalOrder`.

**WordListReader_Read_MissingFolder_ReturnsNoWordsAndNoProblems**: a missing folder reads as empty without a
problem. This scenario is tested by `WordListReader_Read_MissingFolder_ReturnsNoWordsAndNoProblems`.

**WordListReader_ReadFiles_ExplicitFiles_MergesOnlyThoseFiles** and
**WordListReader_ReadFiles_NoFiles_ReturnsNoWordsAndNoProblems**: of three lists in one folder only the two named
files are merged and counted, and an empty list of files reads as empty without a problem. These scenarios are
tested by the tests of the same names.

**WordListReader_Read_SpaceInEntry_ReportsLine**, **WordListReader_Read_Digit_ReportsLine**,
**WordListReader_Read_TypographicApostrophe_ReportsLine**, **WordListReader_Read_LeadingApostrophe_ReportsLine**,
**WordListReader_Read_TrailingApostrophe_ReportsLine**, **WordListReader_Read_DoubleApostrophe_ReportsLine**, and
**WordListReader_Read_OtherScript_ReportsLine**: an entry that breaks exactly one rule is reported once, with the
line and the entry (and, for the space, digit, and other-script cases, the offending character) in the message.
These scenarios are tested by the tests of the same names.

**WordListReader_Read_TooLong_ReportsLine**: an entry of 65 characters is reported on its line while one of 64 is
accepted. This scenario is tested by `WordListReader_Read_TooLong_ReportsLine`.

**WordListReader_Read_InvalidUtf8_ReportsFile**: a file with an invalid byte is reported as one file-level
problem with no line, and contributes no words. This scenario is tested by
`WordListReader_Read_InvalidUtf8_ReportsFile`.

**WordListReader_Read_ManyProblems_CappedAtLimit**: a list in which every line is invalid keeps 100 problems in
detail plus one summary that says how many more were not shown, and counts all of them. This scenario is tested
by `WordListReader_Read_ManyProblems_CappedAtLimit`.

**WordListReader_Read_ProblemsInSeveralFiles_AllReported**: problems in two files are both reported, in file
order. This scenario is tested by `WordListReader_Read_ProblemsInSeveralFiles_AllReported`.

**WordListReader_Read_Diagnostics_FormatAsCompilerErrors**: a diagnostic with a line formats as
`path(line): error: message` and one without as `path: error: message`. This scenario is tested by
`WordListReader_Read_Diagnostics_FormatAsCompilerErrors`.

**WordListReader_Read_CaseVariants_MergedWithForms**: `paris`, `Paris`, and `PARIS` merge into one key whose
flags are lower, capitalized, and upper, and an accented word with two capitals is folded and flagged mixed. This
scenario is tested by `WordListReader_Read_CaseVariants_MergedWithForms`.

**WordListReader_Read_DuplicateAcrossFiles_Merged**: a word in two files, one capitalized, appears once with both
forms, and the file count is reported. This scenario is tested by
`WordListReader_Read_DuplicateAcrossFiles_Merged`.
