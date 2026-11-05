## BuilderProgram

### Verification Approach

`BuilderProgram` is verified with unit tests in `BuilderProgramTests.cs`. The tests call `BuilderProgram.Run`
in-process with `StringWriter` instances for the output and error streams, so no process is started and the
console is not touched. The unit's dependencies, `WordListReader` and `DictionaryWriter`, are used as they are
and not replaced by doubles, because the behavior under test (which dictionaries are produced and which problems
are reported) is their combined effect; each test works with real, temporary folders.

### Test Environment

N/A - standard test environment. Each test creates and removes its own temporary folder.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every malformed command line returns the usage exit code, prints the usage text, and creates no file.
- Valid lists return exit code 0 and one file per declared name.
- Any problem returns exit code 1, names its file or folder, and leaves the output folder uncreated.
- An unchanged file keeps its timestamp and a changed one is rewritten.

### Test Scenarios

**BuilderProgram_Run_MissingInput_ReturnsUsageError**, **BuilderProgram_Run_MissingOutput_ReturnsUsageError**,
and **BuilderProgram_Run_NoDictionaryNames_ReturnsUsageError**: each omits one of the three required parts;
the exit code is 2 and the first also checks that the usage text mentions `--input`. These scenarios are tested
by the tests of the same names.

**BuilderProgram_Run_UnknownOption_ReturnsUsageError**: an option that does not exist is named in the error.
This scenario is tested by `BuilderProgram_Run_UnknownOption_ReturnsUsageError`.

**BuilderProgram_Run_OptionWithoutValue_ReturnsUsageError**: the last option has no value, and the error names
it. This scenario is tested by `BuilderProgram_Run_OptionWithoutValue_ReturnsUsageError`.

**BuilderProgram_Run_DuplicateOption_ReturnsUsageError**: `--input` is given twice and the error says so. This
scenario is tested by `BuilderProgram_Run_DuplicateOption_ReturnsUsageError`.

**BuilderProgram_Run_NameWithPathSeparator_ReturnsUsageError**: the name `../alpha` is rejected, and no output
folder is created. This scenario is tested by `BuilderProgram_Run_NameWithPathSeparator_ReturnsUsageError`.

**BuilderProgram_Run_DuplicateName_ReturnsUsageError**: the same name is declared twice. This scenario is
tested by `BuilderProgram_Run_DuplicateName_ReturnsUsageError`.

**BuilderProgram_Run_ValidLists_WritesFilesAndReturnsZero**: two valid dictionaries produce two files, a
summary line each, and no errors. This scenario is tested by
`BuilderProgram_Run_ValidLists_WritesFilesAndReturnsZero`.

**BuilderProgram_Run_InvalidEntry_WritesNothingAndReportsFileLine**: a valid and an invalid dictionary are built
together; the error points at line 2 of the invalid file and no output folder exists. This scenario is tested by
`BuilderProgram_Run_InvalidEntry_WritesNothingAndReportsFileLine`.

**BuilderProgram_Run_UndeclaredFolder_ReturnsOneNamingFolder**: a folder with word lists that is not declared
is named, a folder without word lists is not, and nothing is written. This scenario is tested by
`BuilderProgram_Run_UndeclaredFolder_ReturnsOneNamingFolder`.

**BuilderProgram_Run_EmptyDictionary_ReturnsOneNamingDictionary** and
**BuilderProgram_Run_MissingDictionaryFolder_ReturnsOneNamingDictionary**: a declared dictionary with only
comments, and one without a folder, are each named in the error. These scenarios are tested by the tests of the
same names.

**BuilderProgram_Run_MissingInputFolder_ReturnsOne**: an input folder that does not exist is reported as a
problem, not a crash. This scenario is tested by `BuilderProgram_Run_MissingInputFolder_ReturnsOne`.

**BuilderProgram_Run_UnchangedOutput_IsNotRewritten**: the output file's timestamp is set to a past time, the
same lists are built again, and the timestamp is unchanged and the summary says "unchanged". This scenario is
tested by `BuilderProgram_Run_UnchangedOutput_IsNotRewritten`.

**BuilderProgram_Run_ChangedList_RewritesOutput**: a list gains a word and the file is rewritten with the new
count. This scenario is tested by `BuilderProgram_Run_ChangedList_RewritesOutput`.

**BuilderProgram_Run_InconsistentTechnicalDeclaration_ReturnsUsageError**: six inconsistent technical
declarations (`--technical` without `--variants`, `--variants` without `--technical`, an undeclared technical
dictionary, an undeclared variant dictionary, a dictionary that is its own variant source, and a repeated
`--technical`) each return the usage exit code and create no output. This scenario is tested by
`BuilderProgram_Run_InconsistentTechnicalDeclaration_ReturnsUsageError`.

**BuilderProgram_Run_TechnicalDictionary_AppliesVariantRule**: in a small tree the technical dictionary holds the
language words, the keep-list word, and the non-variant general word, and the summary line states 2 variants kept
via language lists, 1 via the keep list, and 2 stripped. This scenario is tested by
`BuilderProgram_Run_TechnicalDictionary_AppliesVariantRule`.

**BuilderProgram_Run_TechnicalDictionary_LeavesVariantDictionariesWhole**: the American-only and British-only
dictionaries written in the same run hold all their own words. This scenario is tested by
`BuilderProgram_Run_TechnicalDictionary_LeavesVariantDictionariesWhole`.

**BuilderProgram_Run_TechnicalWithoutKeepList_Succeeds**: without a keep list the run succeeds and the word the
keep list would have saved is stripped. This scenario is tested by
`BuilderProgram_Run_TechnicalWithoutKeepList_Succeeds`.

**BuilderProgram_Run_TechnicalListOutsideKnownPlaces_ReturnsOneNamingFile**, **BuilderProgram_Run_KeepWordNotVariant_ReturnsOneNamingWord**,
and **BuilderProgram_Run_TechnicalWithoutLanguageLists_ReturnsOneNamingLists**: a stray list in the technical
folder, an ordinary word in the keep list, and a missing language list each return exit code 1, name the file,
word, or lists at fault, and write nothing. These scenarios are tested by the tests of the same names.

**BuilderProgram_Run_LaterOutputBlocked_LeavesEarlierOutputUntouched**: the first dictionary was built earlier,
its list then changes, and a folder occupies the second dictionary's file path; the run returns 1, the first file
keeps its old bytes and timestamp, and the output folder holds nothing but the first file and the blocking
folder. This scenario is tested by `BuilderProgram_Run_LaterOutputBlocked_LeavesEarlierOutputUntouched`.

**BuilderProgram_Run_LaterOutputBlockedWithNoPriorFiles_CreatesNothing**: with no earlier output, a blocked second
file leaves the first file uncreated. This scenario is tested by
`BuilderProgram_Run_LaterOutputBlockedWithNoPriorFiles_CreatesNothing`.

**BuilderProgram_Run_OutputPathIsAFile_ReportsErrorAndReturnsOne**: the output path is an existing file, so
the folder cannot be created; the error names the path and the exit code is 1. This scenario is tested by
`BuilderProgram_Run_OutputPathIsAFile_ReportsErrorAndReturnsOne`.
