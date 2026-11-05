## BuilderProgram

### Verification Approach

`BuilderProgram` is verified with unit tests in `BuilderProgramTests.cs`. The tests call `BuilderProgram.Run`
in-process with `StringWriter` instances for the output and error streams, so no process is started and the
console is not touched. The unit's dependencies, `WordListReader`, `VariantFilter`, and `DictionaryWriter`, are used
as they are and not replaced by doubles, because the behavior under test (which dictionaries are produced and
which problems are reported) is their combined effect; each test works with real, temporary folders. The one
substitution is the unit's own file system seam, `FileOperations`: failures that cannot be provoked portably
(a denied folder, a locked file, a backup that cannot be moved back) are injected with `FaultingFileOperations`
from the test support, which performs every other operation for real, so the effect of an injected failure is
observed on a real output folder.

### Test Environment

N/A - standard test environment. Each test creates and removes its own temporary folder.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every malformed command line, including an option with an empty or blank value, returns the usage exit code,
  prints the usage text, and creates no file.
- Valid lists return exit code 0 and one file per declared name.
- Any problem, including a folder or file that exists but cannot be read (told apart from one that does not
  exist), returns exit code 1, names its file or folder, and leaves the output folder uncreated.
- A failed write restores the previous files; when the restoration itself fails, or a temporary file or backup cannot
  be deleted, exit code 3 is returned, every affected file and its backup are named, and the backups that hold
  previous content are kept.
- Names that differ only in letter case are one name: duplicates are rejected, references find the declared name,
  folders and lists are found under their on-disk spelling, and a match that is ambiguous on a case-sensitive file
  system is reported naming every candidate rather than chosen.
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

**BuilderProgram_Run_DuplicateName_ReturnsUsageError** and
**BuilderProgram_Run_DuplicateNameDifferingInCase_ReturnsUsageError**: the same name is declared twice, and
`alpha` and `ALPHA` are declared together; both return the usage exit code, the second names `ALPHA` as a repeat
ignoring case, and nothing is created. These scenarios are tested by the tests of the same names.

**BuilderProgram_Run_TechnicalReferencesDifferingInCase_UseDeclaredSpelling**: the technical and variant
dictionaries are referenced as `TECHNICAL`, `EN-US`, and `en-gb` while declared as `technical`, `en-US`, and `en-GB`;
the run succeeds, the variant rule gives the same words as with the declared spelling, and the files carry the
declared names. This scenario is tested by `BuilderProgram_Run_TechnicalReferencesDifferingInCase_UseDeclaredSpelling`.

**BuilderProgram_Run_TechnicalPartsInDifferentCase_AreFoundAndBuilt**: the technical folder holds `LANGUAGE`,
`General`, and `Variant-Keep.TXT`; the run succeeds with nothing reported as misplaced and the same words as for the
lower-case layout, on every operating system. This scenario is tested by
`BuilderProgram_Run_TechnicalPartsInDifferentCase_AreFoundAndBuilt`.

**BuilderProgram_Run_DeclaredNameDifferingInCaseFromFolder_ReadsFolderOnDisk**: a dictionary declared as `alpha`
whose folder is spelled `ALPHA` on disk is found and read, with nothing reported as missing or undeclared. This
test discriminates on case-sensitive file systems (Linux); on Windows and macOS it passes either way. This scenario
is tested by `BuilderProgram_Run_DeclaredNameDifferingInCaseFromFolder_ReadsFolderOnDisk`.

**BuilderProgram_Run_TechnicalFolderDifferingInCase_ReadsFolderOnDisk**: the same for the technical dictionary,
whose folder is spelled `Technical` on disk. This test also discriminates on case-sensitive file systems. This
scenario is tested by `BuilderProgram_Run_TechnicalFolderDifferingInCase_ReadsFolderOnDisk`.

**BuilderProgram_Run_FoldersDifferingOnlyInCase_ReportsAmbiguityNamingBoth**: the input folder lists both `alpha`
and `Alpha`; the run returns 1 with one problem that names both folders, calls neither missing nor undeclared, and
writes nothing. The listing is substituted so the test runs on every operating system. This scenario is tested by
`BuilderProgram_Run_FoldersDifferingOnlyInCase_ReportsAmbiguityNamingBoth`.

**BuilderProgram_Run_TechnicalPartsDifferingOnlyInCase_ReportsAmbiguityNamingBoth**: the technical folder lists two
spellings of `general`, `language`, and `variant-keep.txt`; the run returns 1 and each problem names both
spellings. The listing is substituted so the test runs on every operating system. This scenario is tested by
`BuilderProgram_Run_TechnicalPartsDifferingOnlyInCase_ReportsAmbiguityNamingBoth`.

**BuilderProgram_Run_KeepListCannotBeRead_ReturnsOneNamingFile**: an injected access-denied failure when reading the
existing keep list returns exit code 1, names the keep list, and creates no output, so approved words are never
silently stripped. This scenario is tested by `BuilderProgram_Run_KeepListCannotBeRead_ReturnsOneNamingFile`.

**BuilderProgram_Run_ExistingOutputCannotBeRead_ReportsErrorAndChangesNothing**: an injected failure when reading an
existing output file returns exit code 1 with the write failure reported, and the file and the folder are unchanged.
This scenario is tested by `BuilderProgram_Run_ExistingOutputCannotBeRead_ReportsErrorAndChangesNothing`.

**BuilderProgram_Run_StagedFileCannotBeDeleted_ReportsLeftoverAndReturnsFatal**: the second installation fails and
deleting any temporary file fails; the run returns exit code 3, the error names the staged file that is still in the
output folder, and the first file is restored. This scenario is tested by
`BuilderProgram_Run_StagedFileCannotBeDeleted_ReportsLeftoverAndReturnsFatal`.

**BuilderProgram_Run_BackupCannotBeDeleted_ReportsLeftoverAndReturnsFatal**: the installation succeeds but deleting
a backup fails; the run returns exit code 3, names the backup, which still holds the previous content, and the new
files are in place. This scenario is tested by `BuilderProgram_Run_BackupCannotBeDeleted_ReportsLeftoverAndReturnsFatal`.

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

**BuilderProgram_Run_InconsistentTechnicalDeclaration_ReturnsUsageError**: eight inconsistent technical
declarations (`--technical` without `--variants`, `--variants` without `--technical`, an undeclared technical
dictionary, an undeclared variant dictionary, a dictionary that is its own variant source, a repeated
`--technical`, a variant repeated in another case, and a technical dictionary that is its own variant source in
another case) each return the usage exit code and create no output. This scenario is tested by
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

**BuilderProgram_Run_EmptyOptionValue_ReturnsUsageError**: an empty or blank value for `--input`, `--output`,
`--technical`, or `--variants` returns the usage exit code, names the option, and creates nothing (in particular
an empty `--output` does not fall back to the working folder). This scenario is tested by
`BuilderProgram_Run_EmptyOptionValue_ReturnsUsageError`.

**BuilderProgram_Run_InstallFails_RestoresPreviousFiles**: file operations that fail when the second staged file is
moved into place make the run return 1 after the first file, already replaced, is restored to its original
content, with no other file left in the output folder. This scenario is tested by
`BuilderProgram_Run_InstallFails_RestoresPreviousFiles`.

**BuilderProgram_Run_RestoreFails_ReportsInconsistentOutputAndKeepsBackup**: as above, but moving the first file's
backup back also fails; the run returns the distinct fatal exit code 3, the error stream says the output folder is
inconsistent and names the first file and its backup, and the backup still holds the original content. This
scenario is tested by `BuilderProgram_Run_RestoreFails_ReportsInconsistentOutputAndKeepsBackup`.

**BuilderProgram_Run_NewFileCannotBeDeleted_ReportsInconsistentOutputAndKeepsBackup**: as above, but deleting the
first file's new content fails; the run returns exit code 3, names the file as holding the new content and names
the backup that keeps the original, which is not deleted. This scenario is tested by
`BuilderProgram_Run_NewFileCannotBeDeleted_ReportsInconsistentOutputAndKeepsBackup`.

**BuilderProgram_Run_FolderCannotBeSearched_ReturnsOneNamingFolder**,
**BuilderProgram_Run_InputFolderCannotBeListed_ReturnsOne**,
**BuilderProgram_Run_UndeclaredFolderCannotBeSearched_ReturnsOneNamingFolder**, and
**BuilderProgram_Run_TechnicalFolderCannotBeSearched_ReturnsOneNamingFolder**: injected access-denied failures while
searching a dictionary folder, listing the input folder, searching an undeclared folder, and searching the
technical folder are each reported as a problem naming the folder, return exit code 1, and create no output. These
scenarios are tested by the tests of the same names.

**BuilderProgram_Run_OutputPathIsAFile_ReportsErrorAndReturnsOne**: the output path is an existing file, so
the folder cannot be created; the error names the path and the exit code is 1. This scenario is tested by
`BuilderProgram_Run_OutputPathIsAFile_ReportsErrorAndReturnsOne`.
