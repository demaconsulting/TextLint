# DictionaryBuilder

## Verification Approach

System-level verification runs the compiled builder as a real process (`dotnet` with the builder
assembly), the way the library's build does, through the `BuilderRunner` test support in
`DictionaryBuilderTests.cs`. Each test writes word lists into its own temporary folder, runs the builder, and
judges only what is observable from outside: the exit code, the two output streams, and which dictionary files
appear. The dictionary files are read back with `TldReader`, an independent minimal reader in the test support
that restates the file format from its specification and shares no code with the builder, so a mistake in the
builder's own definitions cannot cancel itself out. One test runs the builder over the repository's real word
lists (copied beside the test assembly) and checks that every entry of every list, in its original, lower, and
upper case, can be found in the resulting dictionary, which proves the committed lists and the builder agree.

## Test Environment

The tests run on .NET 10 on Windows, Linux, and macOS and need the `dotnet` host on the path. Temporary
folders are created and removed within each test. The word lists of the repository are copied to the test
output folder by the test project, so no repository root has to be located.

## Acceptance Criteria

- All system tests pass with zero failures on every supported platform.
- The exit code is 0 for valid lists, 1 for invalid lists or declarations, and 2 for an invalid command line.
- No dictionary file exists after a run that reported a problem.
- Every entry of every committed word list is found in the dictionary built from it.
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
from the repository's lists; the run succeeds with empty standard error, the word count of each file equals
the number of distinct folded entries of its lists, every entry is found as written, in lower case, and in upper
case, American and British spellings land in their own dictionaries, and a common word and a technical term are
found where expected. This scenario is tested by `DictionaryBuilder_Build_RepositoryWordLists_BuildsAllDictionaries`.

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

**DictionaryBuilder_Build_SameListsTwice_ProducesIdenticalFiles**: Two thousand words are built twice from the
same lists, with the output deleted between runs; the files are byte-identical. This scenario is tested by
`DictionaryBuilder_Build_SameListsTwice_ProducesIdenticalFiles`.

**DictionaryBuilder_Build_CrLfLists_ProduceSameFilesAsLf**: The same list written with LF and with CRLF line
endings builds identical files. This scenario is tested by
`DictionaryBuilder_Build_CrLfLists_ProduceSameFilesAsLf`.

**DictionaryBuilder_Build_UnchangedLists_LeavesFilesUntouched**: After a build the output file's timestamp is
set to a past time; building the same lists again leaves that timestamp. This scenario is tested by
`DictionaryBuilder_Build_UnchangedLists_LeavesFilesUntouched`.
