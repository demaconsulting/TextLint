### FileTextReader

#### Verification Approach

`FileTextReader` is verified with unit tests in `FileTextReaderTests.cs`. The pure decoding rules are tested through
the internal `Decode` method with byte arrays, so no file is involved; the reading rules are tested through
`ReadAsync` with real files in a per-test temporary directory. The unit's only documented dependency, the diagnostic
kind that names the reason a file is not read, is real, and there is no test double. File problems are produced for
real: a missing path, the path of a directory, a file that the test holds open with an exclusive share mode, and a
file that a writer holds open with read and write sharing. The size ceiling of 1 GiB is tested without creating a
gigabyte of data: the test extends a new file to one byte more than the ceiling with `SetLength` and never writes to
it, which every supported file system stores sparsely, and the reader decides on the size before it reads anything; a
second test shows that a limit larger than the ceiling does not raise it, and a third that the constant has its
documented value.

#### Test Environment

Tests need permission to create and delete files under the operating system's temporary directory; each test uses its
own uniquely named directory, so tests share no state and may run in parallel. The locked-file scenario relies on the
exclusive share mode, which the runtime enforces on Windows and, through advisory locking, on Linux and macOS, so the
same test holds on all of them. A file system that does not store a file that is extended with `SetLength` sparsely
would need the space of the file temporarily.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- UTF-8, UTF-8 with a mark, and both byte orders of UTF-16 with a mark decode to the same text without the mark; other
  bytes decode as UTF-8 and invalid bytes become the replacement character without failing.
- A NUL character in the first 1,024 decoded characters makes a file binary, one just beyond does not, decoded UTF-16
  is not binary, and UTF-32 is.
- A file at the limit is read, one byte over is too large, zero means no limit, and no file above 1 GiB is read.
- A missing file, a directory, and an exclusively locked file are unreadable outcomes and not exceptions; a file open
  for writing with sharing is read.
- A cancelled token throws, whether or not the file has content; bad arguments throw the documented exceptions.

#### Test Scenarios

**FileTextReader_Decode_Utf8WithoutBom_ReturnsText**: UTF-8 bytes with no mark decode to the text, with no kind and no
message. This scenario is tested by `FileTextReader_Decode_Utf8WithoutBom_ReturnsText`.

**FileTextReader_Decode_Utf8WithBom_StripsBom**: A UTF-8 mark is removed. This scenario is tested by
`FileTextReader_Decode_Utf8WithBom_StripsBom`.

**FileTextReader_Decode_Utf16LittleEndianWithBom_ReturnsText**: Little-endian UTF-16 with a mark decodes without the
mark. This scenario is tested by `FileTextReader_Decode_Utf16LittleEndianWithBom_ReturnsText`.

**FileTextReader_Decode_Utf16BigEndianWithBom_ReturnsText**: Big-endian UTF-16 with a mark decodes without the mark.
This scenario is tested by `FileTextReader_Decode_Utf16BigEndianWithBom_ReturnsText`.

**FileTextReader_Decode_InvalidUtf8_ReplacesInvalidBytes**: Each invalid sequence becomes one replacement character
and decoding does not fail. This scenario is tested by `FileTextReader_Decode_InvalidUtf8_ReplacesInvalidBytes`.

**FileTextReader_Decode_EncodedSurrogate_ReplacesInvalidBytes**: A surrogate encoded in UTF-8 yields only replacement
characters. This scenario is tested by `FileTextReader_Decode_EncodedSurrogate_ReplacesInvalidBytes`.

**FileTextReader_Decode_MultiByteCharacters_AreDecoded**: An accented letter and an emoji round-trip. This scenario is
tested by `FileTextReader_Decode_MultiByteCharacters_AreDecoded`.

**FileTextReader_Decode_EmptyBytes_ReturnsEmptyText**: No bytes decode to an empty text that is not binary. This
scenario is tested by `FileTextReader_Decode_EmptyBytes_ReturnsEmptyText`.

**FileTextReader_Decode_OnlyBom_ReturnsEmptyText**: A file that holds only a mark decodes to an empty text. This
scenario is tested by `FileTextReader_Decode_OnlyBom_ReturnsEmptyText`.

**FileTextReader_Decode_BomInsideText_IsKept**: Only the first mark is removed; the same character in the middle of
the text is kept. This scenario is tested by `FileTextReader_Decode_BomInsideText_IsKept`.

**FileTextReader_Decode_TruncatedUtf16_ReplacesPartialUnit**: A UTF-16 file cut in the middle of a code unit keeps the
complete letter and ends with a replacement character. This scenario is tested by
`FileTextReader_Decode_TruncatedUtf16_ReplacesPartialUnit`.

**FileTextReader_Decode_Utf16WithoutBom_IsBinary**: UTF-16 without a mark is not recognized, and its NUL bytes make it
binary. This scenario is tested by `FileTextReader_Decode_Utf16WithoutBom_IsBinary`.

**FileTextReader_Decode_NulInsideSniffWindow_IsBinary**: A NUL as the last character of the window makes the file
binary, with no text. This scenario is tested by `FileTextReader_Decode_NulInsideSniffWindow_IsBinary`.

**FileTextReader_Decode_NulJustBeyondSniffWindow_IsText**: A NUL as the first character after the window leaves the
file text, and the character is kept. This scenario is tested by
`FileTextReader_Decode_NulJustBeyondSniffWindow_IsText`.

**FileTextReader_Decode_NulAtStart_IsBinary**: A NUL as the first character makes the file binary. This scenario is
tested by `FileTextReader_Decode_NulAtStart_IsBinary`.

**FileTextReader_Decode_Utf16AsciiText_IsNotBinary**: UTF-16 text of ASCII letters, whose raw bytes hold many NULs, is
text. This scenario is tested by `FileTextReader_Decode_Utf16AsciiText_IsNotBinary`.

**FileTextReader_Decode_Utf16WithNulCharacter_IsBinary**: A NUL character in decoded UTF-16 text makes the file
binary. This scenario is tested by `FileTextReader_Decode_Utf16WithNulCharacter_IsBinary`.

**FileTextReader_Decode_Utf32WithBom_IsBinary**: UTF-32 is not supported and is reported as binary. This scenario is
tested by `FileTextReader_Decode_Utf32WithBom_IsBinary`.

**FileTextReader_Decode_PngImageHeader_IsBinary**: The signature of a PNG image is recognized as binary. This
scenario is tested by `FileTextReader_Decode_PngImageHeader_IsBinary`.

**FileTextReader_Decode_Binary_HasMessageAndNoText**: The binary outcome has a message that names binary data and the
NUL character, and no text. This scenario is tested by `FileTextReader_Decode_Binary_HasMessageAndNoText`.

**FileTextReader_Decode_SameBytesTwice_ReturnsSameResultAndKeepsInput**: Decoding the same bytes twice gives the same
text and leaves the input unchanged. This scenario is tested by
`FileTextReader_Decode_SameBytesTwice_ReturnsSameResultAndKeepsInput`.

**FileTextReader_SizeCeiling_Constant_IsOneGiB**: The ceiling constant is 1,073,741,824 bytes. This scenario is
tested by `FileTextReader_SizeCeiling_Constant_IsOneGiB`.

**FileTextReader_ReadAsync_FileAtLimit_IsRead**: A file exactly as large as the limit is read. This scenario is tested
by `FileTextReader_ReadAsync_FileAtLimit_IsRead`.

**FileTextReader_ReadAsync_FileOneByteOverLimit_IsTooLarge**: A file one byte over the limit is too large and has no
text. This scenario is tested by `FileTextReader_ReadAsync_FileOneByteOverLimit_IsTooLarge`.

**FileTextReader_ReadAsync_TooLarge_MessageNamesSizeAndLimit**: The message states the size of the file and the limit.
This scenario is tested by `FileTextReader_ReadAsync_TooLarge_MessageNamesSizeAndLimit`.

**FileTextReader_ReadAsync_ZeroLimit_ReadsLargeFile**: With a limit of zero a file of two hundred thousand letters is
read whole. This scenario is tested by `FileTextReader_ReadAsync_ZeroLimit_ReadsLargeFile`.

**FileTextReader_ReadAsync_FileAboveCeilingWithNoLimit_IsTooLarge**: A sparse file one byte above the ceiling is too
large even with no limit, and the message names the ceiling. This scenario is tested by
`FileTextReader_ReadAsync_FileAboveCeilingWithNoLimit_IsTooLarge`.

**FileTextReader_ReadAsync_LimitAboveCeiling_StillRefusesAboveCeiling**: The same file is still too large when the
limit is the largest possible number. This scenario is tested by
`FileTextReader_ReadAsync_LimitAboveCeiling_StillRefusesAboveCeiling`.

**FileTextReader_ReadAsync_Utf8File_ReturnsText**: A UTF-8 file is read, with no kind and no message. This scenario is
tested by `FileTextReader_ReadAsync_Utf8File_ReturnsText`.

**FileTextReader_ReadAsync_Utf8BomFile_StripsBom**: A UTF-8 file with a mark is read without it. This scenario is
tested by `FileTextReader_ReadAsync_Utf8BomFile_StripsBom`.

**FileTextReader_ReadAsync_Utf16File_ReturnsText**: A UTF-16 file with a mark is read. This scenario is tested by
`FileTextReader_ReadAsync_Utf16File_ReturnsText`.

**FileTextReader_ReadAsync_EmptyFile_ReturnsEmptyText**: An empty file is read as empty text and is not skipped. This
scenario is tested by `FileTextReader_ReadAsync_EmptyFile_ReturnsEmptyText`.

**FileTextReader_ReadAsync_BinaryFile_IsBinary**: A file that starts with NUL bytes is recognized as binary when it
is read. This scenario is tested by `FileTextReader_ReadAsync_BinaryFile_IsBinary`.

**FileTextReader_ReadAsync_MissingFile_IsUnreadable**: A path with no file is an unreadable outcome with a message and
no text, not an exception. This scenario is tested by `FileTextReader_ReadAsync_MissingFile_IsUnreadable`.

**FileTextReader_ReadAsync_DirectoryPath_IsUnreadable**: The path of a directory is an unreadable outcome. This
scenario is tested by `FileTextReader_ReadAsync_DirectoryPath_IsUnreadable`.

**FileTextReader_ReadAsync_ExclusivelyLockedFile_IsUnreadable**: A file that the test holds open without sharing is an
unreadable outcome, not an exception. This scenario is tested by
`FileTextReader_ReadAsync_ExclusivelyLockedFile_IsUnreadable`.

**FileTextReader_ReadAsync_FileOpenForWriting_IsStillReadable**: A file that a writer holds open with read and write
sharing is read, with the text written so far. This scenario is tested by
`FileTextReader_ReadAsync_FileOpenForWriting_IsStillReadable`.

**FileTextReader_ReadAsync_ParallelReads_ReturnEachFilesOwnText**: Sixteen files read at the same time each return
their own text. This scenario is tested by `FileTextReader_ReadAsync_ParallelReads_ReturnEachFilesOwnText`.

**FileTextReader_ReadAsync_CancelledToken_ThrowsOperationCanceledException**: A token that is already cancelled stops
the read of a file that has content. This scenario is tested by
`FileTextReader_ReadAsync_CancelledToken_ThrowsOperationCanceledException`.

**FileTextReader_ReadAsync_CancelledTokenWithEmptyFile_ThrowsOperationCanceledException**: A cancelled token stops the
read of an empty file too, so the contract does not depend on the size. This scenario is tested by
`FileTextReader_ReadAsync_CancelledTokenWithEmptyFile_ThrowsOperationCanceledException`.

**FileTextReader_ReadAsync_NullPath_ThrowsArgumentNullException**: A null path throws. This scenario is tested by
`FileTextReader_ReadAsync_NullPath_ThrowsArgumentNullException`.

**FileTextReader_ReadAsync_EmptyPath_ThrowsArgumentException**: An empty path throws. This scenario is tested by
`FileTextReader_ReadAsync_EmptyPath_ThrowsArgumentException`.

**FileTextReader_ReadAsync_NegativeLimit_ThrowsArgumentOutOfRangeException**: A negative size limit throws. This
scenario is tested by `FileTextReader_ReadAsync_NegativeLimit_ThrowsArgumentOutOfRangeException`.
