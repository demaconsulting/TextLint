# DictionaryBuilder

## Verification Approach

System-level verification runs the compiled builder as a real process (`dotnet` with the builder
assembly), the way the library's build does, through the `BuilderRunner` test support in
`DictionaryBuilderTests.cs`. Each test writes word lists into its own temporary folder, runs the builder, and
judges only what is observable from outside: the exit code, the two output streams, and which dictionary files
appear. The dictionary files are read back with `TldReader`, an independent minimal reader in the test support
that restates the file format from its specification and shares no code with the builder, so a mistake in the
builder's own definitions cannot cancel itself out. One test runs the builder over the repository's real word
lists (copied beside the test assembly) and checks that every entry of every list that is not excluded, in its
original, lower, and
upper case, can be found in the resulting dictionary, and that every excluded word cannot, which proves the
committed lists, the exclusions, and the builder agree.

## Test Environment

The tests run on .NET 10 on Windows, Linux, and macOS and need the `dotnet` host on the path. Temporary
folders are created and removed within each test. The word lists of the repository are copied to the test
output folder by the test project, so no repository root has to be located.

## Acceptance Criteria

- All system tests pass with zero failures on every supported platform.
- The exit code is 0 for valid lists, 1 for invalid lists or declarations, and 2 for an invalid command line. The
  fatal exit code 3, for a write failure whose restoration also fails or for a temporary file or backup that cannot
  be deleted, needs an injected file system failure and is verified by the unit tests of `BuilderProgram`.
- No dictionary file exists after a run that reported a problem.
- Every entry of every committed word list is found in the dictionary built from it, except the stripped
  spelling variants of the technical dictionary and the words of the exclusions list, which are not found.
- A general technical variant spelling is removed unless a language list or the reviewed list has it.
- An excluded word is unknown in every dictionary; an excluded word that no dictionary has, and an exclusions list
  that cannot be read, fail the run and are named.
- A failure leaves every dictionary file as it was.
- Two builds of the same lists are byte-identical, and CRLF lists build the same bytes as LF lists.

## Test Scenarios

**DictionaryBuilder_Build_InvalidCommandLine_ReportsUsage**: The builder is started with only an unknown
option; it exits with code 2, prints the usage text on standard error, and creates nothing. This scenario is
tested by `DictionaryBuilder_Build_InvalidCommandLine_ReportsUsage`.

**DictionaryBuilder_Build_WordListFolders_WritesOneDictionaryPerFolder**: Two dictionaries are declared, one of
them split across two list files; exactly two files appear, each holding the words of its own folder. This
scenario is tested by `DictionaryBuilder_Build_WordListFolders_WritesOneDictionaryPerFolder`, which is also the
evidence for the platform requirements on each operating system and on .NET 10.

**DictionaryBuilder_Build_RepositoryWordLists_BuildsAllDictionaries**: The four real dictionaries are built
from the repository's lists with the spelling-variant rule and the reviewed exclusions list; the run succeeds with
empty standard error, the
word count of each ordinary dictionary equals the number of distinct folded entries of its lists that are not
excluded, and that of the
technical dictionary equals the distinct folded entries of the language lists, the keep list, and the general
lists that the rule does not strip (and that are not excluded), computed independently of the builder. Every kept
entry is found as written, in
lower case, and in upper case; every stripped variant is not found; American and British spellings land in their
own dictionaries; and `favorite` is stripped while `color`, `grey`, and `tokenizer` are known. This scenario is
tested by `DictionaryBuilder_Build_RepositoryWordLists_BuildsAllDictionaries`.

**DictionaryBuilder_Build_RepositoryExclusions_AreUnknownInEveryDictionary**: Each word of the repository's
exclusions list is not found, in lower or upper case, in any of the four dictionaries built from the real lists.
This scenario is tested by `DictionaryBuilder_Build_RepositoryExclusions_AreUnknownInEveryDictionary`.

**DictionaryBuilder_Build_ExcludedWord_IsUnknownInEveryDictionary**: In the small variant tree, `tokenizer` is in
the American list, a general list, and the keep list and is named by an exclusions list; the run succeeds, the word
is in neither the American nor the technical dictionary, and the other words remain. This scenario is tested by
`DictionaryBuilder_Build_ExcludedWord_IsUnknownInEveryDictionary`.

**DictionaryBuilder_Build_ExclusionInNoDictionary_FailsNamingWord**: An exclusions list with a word that no list
has fails the run with an error that names the exclusions file and the word and writes nothing. This scenario is
tested by `DictionaryBuilder_Build_ExclusionInNoDictionary_FailsNamingWord`.

**DictionaryBuilder_Build_WordInCommonAndRegionalFolders_IsInBothDictionaries**: A word that is in a reviewed list of the
common folder and also in the British folder builds successfully with no error, and both the common and the British
dictionary contain it. This scenario is tested by `DictionaryBuilder_Build_WordInCommonAndRegionalFolders_IsInBothDictionaries`.

**DictionaryBuilder_Build_ExclusionsFileMissing_FailsNamingFile**: An exclusions option that names a file that does
not exist fails the run with an error that names the file, rather than treating it as an empty list, and writes
nothing. This scenario is tested by `DictionaryBuilder_Build_ExclusionsFileMissing_FailsNamingFile`.

**DictionaryBuilder_Build_RepositoryVariantKeepList_ContainsOnlyOtherwiseStrippedVariants**: Every word of the
repository's reviewed keep list is a variant spelling, is in a general list, and is in no language list, so the
list contains only words whose reviewed status has an effect. This defensive scenario guards the curated data and
is not linked to a requirement.

**DictionaryBuilder_Build_GeneralListVariantSpelling_IsStripped**: In a small tree, `favorite` (American) and a British
variant spelling appear only in the general list; neither is known in the technical dictionary, while the ordinary
`kubernetes` is. This scenario is tested by `DictionaryBuilder_Build_GeneralListVariantSpelling_IsStripped`.

**DictionaryBuilder_Build_LanguageListVariantSpelling_IsKept**: `color` (American) and `grey` (British) are in the
language list and are both known in the technical dictionary. This scenario is tested by
`DictionaryBuilder_Build_LanguageListVariantSpelling_IsKept`.

**DictionaryBuilder_Build_KeepListVariantSpelling_IsKept**: `tokenizer` is a variant in the general list and is in
the keep list, so it is known in the technical dictionary. This scenario is tested by
`DictionaryBuilder_Build_KeepListVariantSpelling_IsKept`.

**DictionaryBuilder_Build_KeepListWordNotVariant_FailsNamingWord**: An ordinary word added to the keep list fails
the run with an error that names the word and writes nothing. This scenario is tested by
`DictionaryBuilder_Build_KeepListWordNotVariant_FailsNamingWord`.

**DictionaryBuilder_Build_TechnicalListOutsideKnownPlaces_FailsNamingFile**: A word list directly in the technical
folder fails the run with an error that names the file and writes nothing. This scenario is tested by
`DictionaryBuilder_Build_TechnicalListOutsideKnownPlaces_FailsNamingFile`.

**DictionaryBuilder_Build_LaterOutputBlocked_LeavesEarlierOutputUntouched**: After a first build of one
dictionary, its list changes, a second dictionary is added, and a folder occupies the second dictionary's file
path; the run fails with exit code 1, the first dictionary file keeps its old content, and no temporary file is
left. This scenario is tested by `DictionaryBuilder_Build_LaterOutputBlocked_LeavesEarlierOutputUntouched`.

**DictionaryBuilder_Build_CaseVariantsAndDuplicates_StoresEachWordOnce**: A word in three cases, a repeated
word, and a duplicate in a second file are stored as single lower-case words. This scenario is tested by
`DictionaryBuilder_Build_CaseVariantsAndDuplicates_StoresEachWordOnce`.

**DictionaryBuilder_Build_UnsortedLists_StoresSortedWords**: Lists in no particular order across two files
produce a dictionary whose words are in ordinal order. This scenario is tested by
`DictionaryBuilder_Build_UnsortedLists_StoresSortedWords`.

**DictionaryBuilder_Build_CapitalizationForms_AreRecorded**: Words that occur lower case, capitalized, upper
case, and mixed, one of them in two forms, carry exactly those forms in their slots. This scenario is tested by
`DictionaryBuilder_Build_CapitalizationForms_AreRecorded`.

**DictionaryBuilder_Build_InvalidEntry_ReportsFileAndLineAndWritesNothing**: A valid dictionary and one with an
invalid third line are built together; the run fails with exit code 1, the error names the file and line 3 in
compiler format, and the output folder is not created, so even the valid dictionary was not written. This
scenario is tested by `DictionaryBuilder_Build_InvalidEntry_ReportsFileAndLineAndWritesNothing`.

**DictionaryBuilder_Build_EmptyDictionary_FailsNamingDictionary**: A declared dictionary that holds only
comments fails with an error that names the dictionary and writes nothing. This scenario is tested by
`DictionaryBuilder_Build_EmptyDictionary_FailsNamingDictionary`.

**DictionaryBuilder_Build_UndeclaredFolder_FailsNamingFolder**: A folder of word lists whose name was left off
the command line fails with an error that names the folder and writes nothing. This scenario is tested by
`DictionaryBuilder_Build_UndeclaredFolder_FailsNamingFolder`.

**DictionaryBuilder_Build_NamesDifferingOnlyInCase_RejectedAsDuplicate**: One dictionary is declared as `alpha` and
`ALPHA`; the run exits with code 2, the error names `ALPHA`, and nothing is written. This scenario is tested by
`DictionaryBuilder_Build_NamesDifferingOnlyInCase_RejectedAsDuplicate`.

**DictionaryBuilder_Build_MissingDictionaryFolder_FailsNamingFolder**: A declared dictionary whose folder does not
exist fails the run with an error that names the missing folder and writes nothing. The unreadable-folder and
unreadable-file cases need an injected file system failure and are verified by the unit tests. This scenario is
tested by `DictionaryBuilder_Build_MissingDictionaryFolder_FailsNamingFolder`.

**DictionaryBuilder_Build_SameListsTwice_ProducesIdenticalFiles**: Two thousand words are built twice from the
same lists, with the output deleted between runs; the files are byte-identical. This scenario is tested by
`DictionaryBuilder_Build_SameListsTwice_ProducesIdenticalFiles`.

**DictionaryBuilder_Build_CrLfLists_ProduceSameFilesAsLf**: The same list written with LF and with CRLF line
endings builds identical files. This scenario is tested by
`DictionaryBuilder_Build_CrLfLists_ProduceSameFilesAsLf`.

**DictionaryBuilder_Build_UnchangedLists_LeavesFilesUntouched**: After a build the output file's timestamp is
set to a past time; building the same lists again leaves that timestamp. This scenario is tested by
`DictionaryBuilder_Build_UnchangedLists_LeavesFilesUntouched`.
