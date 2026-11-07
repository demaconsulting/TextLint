### FileTextReader

![SpellChecking Structure](SpellCheckingView.svg)

#### Purpose

`FileTextReader` turns a file into text for checking, or into the reason the file will not be checked. It chooses
the encoding, removes the byte order mark, recognizes binary files, enforces the size limit, and converts the
problems that a real file system produces into data. Reading is its own unit because real repositories hold files in
several encodings and files that are not text at all, and these rules must be testable with bytes and temporary files
without any spelling involved. It also defines the internal result type `FileTextContent`, which has no unit of its
own.

#### Data Model

`FileTextReader` is an internal static class with no state. It has the internal constants
`SizeCeilingBytes` (2 to the power of 30, which is 1 GiB) and `BinarySniffLength` (1,024), and private constants for
the byte order marks.

**FileTextContent** is an internal immutable class with `Text` (the decoded text without its byte order mark, or
null), `Kind` (a `FileDiagnosticKind`, or null), and `Message` (a human-readable explanation, or null). The invariant
is that exactly one outcome is set: either `Text`, or `Kind` and `Message` together. Its constructor is private; the
two factory methods `FromText` and `NotChecked` keep the invariant. Reading problems are values, not exceptions,
because they are expected on real repositories.

#### Key Methods

**ReadAsync (internal)**: Reads a file.

- _Parameters_: `string fullPath`; `long maxFileSize` (0 means no limit other than the ceiling);
  `CancellationToken cancellationToken`.
- _Returns_: a `FileTextContent`; never null.
- _Preconditions_: the path is not null or empty, the size limit is not negative.
- _Postconditions_: the file has been read, not written; the file handle is closed.

Steps: (1) validate the arguments and honor a token that is already canceled, so the contract does not depend on
whether the file has content; (2) open the file for reading while allowing other readers and writers and deletion, so
that a log that another program has open for writing can still be read; (3) take the size from the open handle, so
the size and the content agree; (4) when the size exceeds the smaller of the limit (if there is one) and the ceiling,
return the too-large outcome with both numbers, before any memory is committed; (5) read the whole file in one
asynchronous call (the stream's own buffering is turned off because it would only copy the bytes again), taking
only the bytes that were actually read, so a file that was truncated meanwhile is read up to its new end and a file
that grew is read only up to the size it had when it was opened; (6) decode the bytes.

**Decode (internal)**: Decodes bytes or recognizes binary data.

It is a pure function, which makes the encoding rules testable without any file. A UTF-8 byte order mark selects
UTF-8, a little-endian or big-endian UTF-16 mark selects that UTF-16, and the mark is dropped; any other bytes are
decoded as UTF-8. A byte sequence that is not valid in the encoding, including a UTF-16 file cut in the middle of a
code unit and a surrogate that is encoded in UTF-8, becomes the replacement character, so decoding never fails. When
a NUL character appears among the first 1,024 decoded characters the file is binary. The test is made on the decoded
text, not on the bytes, so a UTF-16 file, half of whose bytes are NUL, is text, while UTF-16 without a mark and
UTF-32 (which are not recognized) decode to text full of NULs and are reported as binary.

#### Error Handling

The failures that a real file system produces while opening or reading (`IOException`, which includes a missing file
and a file locked by another program, `UnauthorizedAccessException`, which includes a directory path and denied
access, and `NotSupportedException`) are caught and returned as the unreadable outcome, with the system's message.
This is the only place where such exceptions are swallowed, and the list is deliberately short: any other exception
is a defect and propagates, so a bug cannot be mistaken for an unreadable file. Cancellation is not a read failure
and propagates as `OperationCanceledException`. A null or empty path throws `ArgumentNullException` or
`ArgumentException`, and a negative size limit throws `ArgumentOutOfRangeException`, because these are caller
mistakes. Nothing is logged.

#### Dependencies

- **FileDiagnosticKind** - names the reason a file is not checked; defined with `SpellChecker`, in the same subsystem.
- **.NET BCL** - file streams and text encodings.

#### Callers

- **SpellChecker** - reads each selected file on a worker, and turns the outcome into text to check or into a
  `FileDiagnostic`.

#### Design Notes

**Why the whole file is read at once.** The checker needs a string, and the encoding is known only after the first
bytes are seen, so streaming would add complexity without lowering the peak memory of the step that follows. The
memory in use for one file is its bytes plus its text, and a run holds that for as many files as there are workers.

**Why 1 GiB is a technical ceiling and not a setting.** A .NET string cannot hold more than about a billion
characters, and the bytes and the text are in memory together, so no file larger than 1 GiB can be checked whatever
the project wants; the limit that a project chooses can only lower it. The ceiling is therefore enforced even when
the maximum file size is zero (no limit), and a limit above it does not raise it. The tests prove this without
creating a gigabyte of data: they extend a new file to just over the ceiling with `SetLength` without writing to it,
which the supported file systems store sparsely, and the reader decides on the size before it reads anything.

**Why a NUL in the first 1,024 characters.** It is the cheapest reliable signal that a file is not text, it is
independent of the file extension, and the window is large enough that no real text file has a NUL inside it and
small enough that the test never costs a noticeable fraction of the read.

**Why UTF-32 is unsupported.** A byte order mark for UTF-32 cannot be told from one for UTF-16 by its first two
bytes, and UTF-32 text files do not occur in the repositories this tool targets, so such a file is reported as binary
instead of being guessed at.
