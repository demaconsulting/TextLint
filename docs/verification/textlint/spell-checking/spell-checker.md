### SpellChecker

#### Verification Approach

`SpellChecker` and the option, result, and diagnostic types it defines are verified with unit tests in
`SpellCheckerTests.cs`. Tests build real directory trees in a per-test temporary directory and call the public
`CheckAsync`, so the documented dependencies (the settings value, the file selector, the file reader, and the text
checker) are real. The internal `CheckFilesAsync` method is a test seam that takes the function that checks a text,
and it is the only place where a test supplies its own code: a function that throws the exception that a pattern
time-out raises, one that throws an exception that is a defect, one that cancels the run, and one that records how
many checks overlap. These prove the handling of a time-out, a defect, a cancellation, and the degree of parallelism
without hostile input and without a timing assumption: the overlap test asserts only an upper bound, which holds
however the threads are scheduled. The selection that the seam receives is made by the real file selector, and a
selected file is deleted afterwards to prove the unreadable outcome of a vanished file. The locked-file scenario holds
a file open with an exclusive share mode. Result types are created directly through their internal constructors to
prove that they copy their inputs.

#### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test uses its
own uniquely named directory, so tests share no state and may run in parallel. The locked-file scenario relies on the
exclusive share mode, which the runtime enforces on Windows and, through advisory locking, on Linux and macOS. Names
that differ only in letter case are avoided, because Windows and macOS file systems do not distinguish them; the
ordering scenario uses an upper-case and a lower-case first letter in different names.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Only the files that the file patterns select, minus the ignore patterns, and hidden files only when enabled, are
  checked, and findings carry relative paths with forward slashes; no selection gives an empty result.
- The spelling settings are applied, and null options mean the defaults.
- Unreadable, binary, too-large, and timed-out files become diagnostics with the right severity and the run
  continues; any other exception from checking propagates.
- The counts and `HasProblems` are right for mixed files, skipped files only, and an error diagnostic only.
- Findings are ordered by ordinal path, line, and column, diagnostics by path, whatever order files finish in.
- The degree of parallelism defaults to the processor count, is validated when the options are created, is never
  exceeded, and does not change the result.
- Cancellation before or during the run, including the selection of the files, throws and returns no result; bad
  arguments and unusable settings throw the
  documented exceptions, the settings before any file is touched.
- Results cannot be changed by the caller and copy the collections they are built from.

#### Test Scenarios

**SpellChecker_CheckAsync_FilePatterns_ChecksOnlyMatchingFiles**: Of a markdown and a text file that both hold a
misspelling, only the one that the pattern selects is checked. This scenario is tested by
`SpellChecker_CheckAsync_FilePatterns_ChecksOnlyMatchingFiles`.

**SpellChecker_CheckAsync_IgnorePaths_SkipsIgnoredFiles**: A file under an ignored directory is not checked. This
scenario is tested by `SpellChecker_CheckAsync_IgnorePaths_SkipsIgnoredFiles`.

**SpellChecker_CheckAsync_HiddenFile_IsNotCheckedByDefault**: A hidden file that only a wildcard selects is not
checked by default. This scenario is tested by `SpellChecker_CheckAsync_HiddenFile_IsNotCheckedByDefault`.

**SpellChecker_CheckAsync_HiddenFileWithGlobDot_IsChecked**: The same file is checked when hidden matching is enabled.
This scenario is tested by `SpellChecker_CheckAsync_HiddenFileWithGlobDot_IsChecked`.

**SpellChecker_CheckAsync_NestedFile_ReportsRelativeForwardSlashPath**: A file two directories deep is named by its
relative path with forward slashes. This scenario is tested by
`SpellChecker_CheckAsync_NestedFile_ReportsRelativeForwardSlashPath`.

**SpellChecker_CheckAsync_NoFilePatterns_ReturnsEmptyResult**: Settings with no file patterns select nothing and give
an empty result with zero counts. This scenario is tested by
`SpellChecker_CheckAsync_NoFilePatterns_ReturnsEmptyResult`.

**SpellChecker_CheckAsync_NoMatchingFiles_ReturnsEmptyResult**: Patterns that match no file give an empty result with
zero counts. This scenario is tested by `SpellChecker_CheckAsync_NoMatchingFiles_ReturnsEmptyResult`.

**SpellChecker_CheckAsync_CleanFiles_ReportsNothingAndCountsFiles**: Three clean files, one empty, give no finding,
three files checked, none skipped, and no problem. This scenario is tested by
`SpellChecker_CheckAsync_CleanFiles_ReportsNothingAndCountsFiles`.

**SpellChecker_CheckAsync_SpellingSettings_AreApplied**: A flagged word and an unknown word are reported and an
accepted word is not. This scenario is tested by `SpellChecker_CheckAsync_SpellingSettings_AreApplied`.

**SpellChecker_CheckAsync_NullOptions_UsesDefaults**: Null options check the files with the defaults. This scenario is
tested by `SpellChecker_CheckAsync_NullOptions_UsesDefaults`.

**SpellChecker_Options_Default_UsesProcessorCount**: The default degree of parallelism is the processor count. This
scenario is tested by `SpellChecker_Options_Default_UsesProcessorCount`.

**SpellChecker_Options_ZeroParallelism_ThrowsArgumentOutOfRangeException**: A degree of zero is rejected when the
options are created. This scenario is tested by
`SpellChecker_Options_ZeroParallelism_ThrowsArgumentOutOfRangeException`.

**SpellChecker_Options_NegativeParallelism_ThrowsArgumentOutOfRangeException**: A negative degree is rejected when the
options are created. This scenario is tested by
`SpellChecker_Options_NegativeParallelism_ThrowsArgumentOutOfRangeException`.

**SpellChecker_Options_WithExpression_CopiesAndKeepsOriginal**: A with-expression derives a copy with the new value
and leaves the original unchanged. This scenario is tested by
`SpellChecker_Options_WithExpression_CopiesAndKeepsOriginal`.

**SpellChecker_Options_WithExpressionInvalidValue_ThrowsArgumentOutOfRangeException**: A with-expression with zero is
rejected like the initializer. This scenario is tested by
`SpellChecker_Options_WithExpressionInvalidValue_ThrowsArgumentOutOfRangeException`.

**SpellChecker_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults**: Thirty files in several directories
give identical formatted results with one worker and with eight. This scenario is tested by
`SpellChecker_CheckAsync_DifferentParallelism_ReturnsIdenticalOrderedResults`.

**SpellChecker_CheckFilesAsync_Parallelism_NeverExceedsOption**: With a limit of two, a check that records overlap
never sees more than two at once. This scenario is tested by
`SpellChecker_CheckFilesAsync_Parallelism_NeverExceedsOption`.

**SpellChecker_CheckFilesAsync_ParallelismOne_ChecksOneFileAtATime**: With a limit of one the checks never overlap.
This scenario is tested by `SpellChecker_CheckFilesAsync_ParallelismOne_ChecksOneFileAtATime`.

**SpellChecker_CheckFilesAsync_SlowFirstFile_KeepsFileOrder**: When the first file is checked slowly and finishes
last, the findings are still in file order. This scenario is tested by
`SpellChecker_CheckFilesAsync_SlowFirstFile_KeepsFileOrder`.

**SpellChecker_CheckAsync_Findings_AreOrderedByPathLineAndColumn**: Files created out of order, with an upper-case name
that sorts before lower case, give findings in ordinal path, line, and column order. This scenario is tested by
`SpellChecker_CheckAsync_Findings_AreOrderedByPathLineAndColumn`.

**SpellChecker_CheckAsync_Diagnostics_AreOrderedByPath**: Binary files created out of order give diagnostics in path
order. This scenario is tested by `SpellChecker_CheckAsync_Diagnostics_AreOrderedByPath`.

**SpellChecker_CheckAsync_LockedFile_IsReportedUnreadableAndRunContinues**: A locked file is an unreadable error, the
readable file is checked, and the counts and `HasProblems` say so. This scenario is tested by
`SpellChecker_CheckAsync_LockedFile_IsReportedUnreadableAndRunContinues`.

**SpellChecker_CheckAsync_BinaryFile_IsSkippedAndReported**: A binary file is a diagnostic that is not an error and is
counted as skipped. This scenario is tested by `SpellChecker_CheckAsync_BinaryFile_IsSkippedAndReported`.

**SpellChecker_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported**: A file over the maximum file size is a too-large
diagnostic, counted as skipped, with no findings. This scenario is tested by
`SpellChecker_CheckAsync_FileOverMaxFileSize_IsSkippedAndReported`.

**SpellChecker_CheckAsync_MixedFiles_CountsCheckedAndSkipped**: Two small text files, a binary file, and an oversize
file give two checked and two skipped. This scenario is tested by
`SpellChecker_CheckAsync_MixedFiles_CountsCheckedAndSkipped`.

**SpellChecker_CheckFilesAsync_FileRemovedAfterSelection_IsUnreadable**: A selected file that was deleted is an
unreadable error with no findings. This scenario is tested by
`SpellChecker_CheckFilesAsync_FileRemovedAfterSelection_IsUnreadable`.

**SpellChecker_CheckFilesAsync_TimeoutWhileChecking_IsReportedTimedOut**: A check that throws the time-out exception
for one file gives a timed-out error for it, the other file is checked, and the run has problems. This scenario is
tested by `SpellChecker_CheckFilesAsync_TimeoutWhileChecking_IsReportedTimedOut`.

**SpellChecker_CheckFilesAsync_DefectWhileChecking_Propagates**: Any other exception from checking reaches the caller
and is not swallowed. This scenario is tested by `SpellChecker_CheckFilesAsync_DefectWhileChecking_Propagates`.

**SpellChecker_CheckAsync_FindingPresent_HasProblems**: A finding makes the run have problems. This scenario is tested
by `SpellChecker_CheckAsync_FindingPresent_HasProblems`.

**SpellChecker_CheckAsync_OnlySkippedFiles_HasNoProblems**: A run with only a skipped file has no problem. This
scenario is tested by `SpellChecker_CheckAsync_OnlySkippedFiles_HasNoProblems`.

**SpellChecker_CheckFilesAsync_ErrorDiagnosticOnly_HasProblems**: An error diagnostic alone makes the run have
problems. This scenario is tested by `SpellChecker_CheckFilesAsync_ErrorDiagnosticOnly_HasProblems`.

**SpellChecker_FileDiagnostic_UnreadableAndTimedOut_AreErrors**: The unreadable and timed-out kinds are errors. This
scenario is tested by `SpellChecker_FileDiagnostic_UnreadableAndTimedOut_AreErrors`.

**SpellChecker_FileDiagnostic_BinaryAndTooLarge_AreNotErrors**: The binary and too-large kinds are not errors. This
scenario is tested by `SpellChecker_FileDiagnostic_BinaryAndTooLarge_AreNotErrors`.

**SpellChecker_FileDiagnostic_ErrorToString_UsesErrorSeverity**: An error diagnostic formats as `file: error:
message`. This scenario is tested by `SpellChecker_FileDiagnostic_ErrorToString_UsesErrorSeverity`.

**SpellChecker_FileDiagnostic_SkipToString_UsesInfoSeverity**: A skip diagnostic formats as `file: info: message`.
This scenario is tested by `SpellChecker_FileDiagnostic_SkipToString_UsesInfoSeverity`.

**SpellChecker_CheckAsync_CancelledToken_ThrowsOperationCanceledException**: A token that is already cancelled stops
the run. This scenario is tested by `SpellChecker_CheckAsync_CancelledToken_ThrowsOperationCanceledException`.

**SpellChecker_CheckFilesAsync_CancelledToken_ChecksNoFile**: With a token that is already cancelled the run throws
and the check function is never called. This scenario is tested by
`SpellChecker_CheckFilesAsync_CancelledToken_ChecksNoFile`.

**SpellChecker_CheckFilesAsync_CancelledDuringRun_ThrowsOperationCanceledException**: A check that cancels the run
makes it throw before every file was checked, and no result is returned. This scenario is tested by
`SpellChecker_CheckFilesAsync_CancelledDuringRun_ThrowsOperationCanceledException`.

**SpellChecker_CheckWithSelectionAsync_Selection_ReceivesCallersToken**: The selection function is given the caller's
own token, so a cancellation reaches the directory walk. This scenario is tested by
`SpellChecker_CheckWithSelectionAsync_Selection_ReceivesCallersToken`.

**SpellChecker_CheckWithSelectionAsync_SelectionCancelled_ThrowsOperationCanceledException**: A selection that is
cancelled while it runs ends the call with a cancellation, and nothing is checked afterwards. This scenario is tested
by `SpellChecker_CheckWithSelectionAsync_SelectionCancelled_ThrowsOperationCanceledException`.

**SpellChecker_CheckWithSelectionAsync_NullSelection_ThrowsArgumentNullException**: A null selection function throws and
the exception names the parameter. This scenario is tested by
`SpellChecker_CheckWithSelectionAsync_NullSelection_ThrowsArgumentNullException`.

**SpellChecker_CheckAsync_NullSettings_ThrowsArgumentNullException**: Null settings throw and the exception names the
parameter. This scenario is tested by `SpellChecker_CheckAsync_NullSettings_ThrowsArgumentNullException`.

**SpellChecker_CheckAsync_NullRoot_ThrowsArgumentNullException**: A null root throws. This scenario is tested by
`SpellChecker_CheckAsync_NullRoot_ThrowsArgumentNullException`.

**SpellChecker_CheckAsync_EmptyRoot_ThrowsArgumentException**: An empty root throws. This scenario is tested by
`SpellChecker_CheckAsync_EmptyRoot_ThrowsArgumentException`.

**SpellChecker_CheckAsync_WhiteSpaceRoot_ThrowsArgumentException**: A root of spaces throws. This scenario is tested
by `SpellChecker_CheckAsync_WhiteSpaceRoot_ThrowsArgumentException`.

**SpellChecker_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException**: A root that does not exist throws. This
scenario is tested by `SpellChecker_CheckAsync_MissingRoot_ThrowsDirectoryNotFoundException`.

**SpellChecker_CheckAsync_InvalidPattern_ThrowsInvalidGlobPatternException**: A negated file pattern throws and the
message names it. This scenario is tested by `SpellChecker_CheckAsync_InvalidPattern_ThrowsInvalidGlobPatternException`.

**SpellChecker_CheckAsync_UnusableLanguage_ThrowsBeforeTouchingFiles**: A bad language is reported even when the root
directory is also missing, which shows that the settings are checked first. This scenario is tested by
`SpellChecker_CheckAsync_UnusableLanguage_ThrowsBeforeTouchingFiles`.

**SpellChecker_CheckAsync_UnusableWordEntry_ThrowsBeforeTouchingFiles**: A blank word entry is reported even when the
root directory is also missing. This scenario is tested by
`SpellChecker_CheckAsync_UnusableWordEntry_ThrowsBeforeTouchingFiles`.

**SpellChecker_CheckFilesAsync_NullFiles_ThrowsArgumentNullException**: A null file list throws. This scenario is
tested by `SpellChecker_CheckFilesAsync_NullFiles_ThrowsArgumentNullException`.

**SpellChecker_CheckFilesAsync_NullCheckFunction_ThrowsArgumentNullException**: A null check function throws. This
scenario is tested by `SpellChecker_CheckFilesAsync_NullCheckFunction_ThrowsArgumentNullException`.

**SpellChecker_CheckFilesAsync_NullOptions_ThrowsArgumentNullException**: Null options throw. This scenario is tested
by `SpellChecker_CheckFilesAsync_NullOptions_ThrowsArgumentNullException`.

**SpellChecker_CheckAsync_Issues_AreImmutable**: The findings of a result cannot be changed by casting them to a
mutable interface. This scenario is tested by `SpellChecker_CheckAsync_Issues_AreImmutable`.

**SpellChecker_CheckAsync_FileDiagnostics_AreImmutable**: The diagnostics of a result cannot be changed by casting
them to a mutable interface. This scenario is tested by `SpellChecker_CheckAsync_FileDiagnostics_AreImmutable`.

**SpellChecker_Result_CallerCollections_AreCopied**: A result does not change when the lists it was created from are
cleared. This scenario is tested by `SpellChecker_Result_CallerCollections_AreCopied`.
