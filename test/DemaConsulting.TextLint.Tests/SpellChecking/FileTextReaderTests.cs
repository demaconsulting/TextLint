// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// cspell:ignore FFFD

using System.Text;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.SpellChecking;

/// <summary>
///     Unit tests for the <see cref="FileTextReader"/> class.
/// </summary>
/// <remarks>
///     The reader is tested with real files in a per-test temporary directory and, for the pure decoding
///     rules, with byte arrays. Its only documented dependency is the file diagnostic kind that names the
///     reason a file is not read. The size ceiling is tested with a file that is extended to the ceiling
///     with <see cref="FileStream.SetLength"/> and never written, which every supported file system stores
///     sparsely, so no gigabyte of data is created and the reader decides on the size before reading.
/// </remarks>
public class FileTextReaderTests
{
    /// <summary>
    ///     The text that most tests write and expect back.
    /// </summary>
    private const string Sample = "Hello world";

    /// <summary>
    ///     The number of characters that the reader examines for a NUL character.
    /// </summary>
    private const int SniffLength = 1024;

    /// <summary>
    ///     The ceiling above which no file is read: 1 GiB.
    /// </summary>
    private const long OneGiB = 1024L * 1024L * 1024L;

    /// <summary>
    ///     The number of files read at the same time in the parallel test.
    /// </summary>
    private const int ParallelFiles = 16;

    /// <summary>
    ///     The size of the larger file used to show that no limit means no limit.
    /// </summary>
    private const int LargeFileSize = 200_000;

    /// <summary>
    ///     The UTF-8 byte-order mark.
    /// </summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    ///     The little-endian UTF-16 byte-order mark.
    /// </summary>
    private static readonly byte[] Utf16LittleEndianBom = [0xFF, 0xFE];

    /// <summary>
    ///     The big-endian UTF-16 byte-order mark.
    /// </summary>
    private static readonly byte[] Utf16BigEndianBom = [0xFE, 0xFF];

    /// <summary>
    ///     Joins byte sequences into one array.
    /// </summary>
    /// <param name="parts">The sequences to join.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Join(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>
    ///     Writes bytes to a new file in a temporary directory.
    /// </summary>
    /// <param name="temp">The directory to write in.</param>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The absolute path of the file.</returns>
    private static string WriteFile(TempDirectory temp, string name, byte[] bytes)
    {
        var path = Path.Combine(temp.FullPath, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    ///     Builds bytes of the stated number of ASCII letters with a NUL byte at one position.
    /// </summary>
    /// <param name="length">The total number of bytes.</param>
    /// <param name="nulIndex">The index of the NUL byte.</param>
    /// <returns>The bytes.</returns>
    private static byte[] LettersWithNulAt(int length, int nulIndex)
    {
        var bytes = Enumerable.Repeat((byte)'a', length).ToArray();
        bytes[nulIndex] = 0;
        return bytes;
    }

    /// <summary>
    ///     Test that bytes without a byte-order mark are decoded as UTF-8.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf8WithoutBom_ReturnsText()
    {
        // Arrange: UTF-8 bytes with no mark
        var bytes = Encoding.UTF8.GetBytes(Sample);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the text is returned and there is no reason to skip the file
        Assert.Multiple(
            () => Assert.Equal(Sample, content.Text),
            () => Assert.Null(content.Kind),
            () => Assert.Null(content.Message));
    }

    /// <summary>
    ///     Test that a UTF-8 byte-order mark is removed.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf8WithBom_StripsBom()
    {
        // Arrange: UTF-8 bytes that begin with the mark
        var bytes = Join(Utf8Bom, Encoding.UTF8.GetBytes(Sample));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the text has no leading mark character
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that little-endian UTF-16 with a byte-order mark is decoded.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf16LittleEndianWithBom_ReturnsText()
    {
        // Arrange: little-endian UTF-16 bytes with the mark
        var bytes = Join(Utf16LittleEndianBom, Encoding.Unicode.GetBytes(Sample));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the text is returned without the mark
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that big-endian UTF-16 with a byte-order mark is decoded.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf16BigEndianWithBom_ReturnsText()
    {
        // Arrange: big-endian UTF-16 bytes with the mark
        var bytes = Join(Utf16BigEndianBom, Encoding.BigEndianUnicode.GetBytes(Sample));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the text is returned without the mark
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that bytes that are not valid UTF-8 become the replacement character.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_InvalidUtf8_ReplacesInvalidBytes()
    {
        // Arrange: a lead byte followed by an ASCII character, then a stray continuation byte
        byte[] bytes = [0xC3, (byte)'(', 0x80];

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: each invalid sequence is one replacement character and decoding does not fail
        Assert.Equal("\uFFFD(\uFFFD", content.Text);
    }

    /// <summary>
    ///     Test that a surrogate encoded in UTF-8, which is not valid, becomes replacement characters.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_EncodedSurrogate_ReplacesInvalidBytes()
    {
        // Arrange: the three bytes that would encode the surrogate U+D800
        byte[] bytes = [0xED, 0xA0, 0x80];

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: no surrogate comes out and the result is only replacement characters
        Assert.All(content.Text!, character => Assert.Equal('\uFFFD', character));
    }

    /// <summary>
    ///     Test that multi-byte characters, including one outside the Basic Multilingual Plane, are decoded.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_MultiByteCharacters_AreDecoded()
    {
        // Arrange: UTF-8 bytes of an accented letter and an emoji
        const string text = "caf\u00E9 \U0001F600";
        var bytes = Encoding.UTF8.GetBytes(text);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the characters round-trip
        Assert.Equal(text, content.Text);
    }

    /// <summary>
    ///     Test that no bytes decode to an empty text that is not binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_EmptyBytes_ReturnsEmptyText()
    {
        // Arrange: no bytes

        // Act: decode them
        var content = FileTextReader.Decode([]);

        // Assert: the text is empty and the file is not skipped
        Assert.Multiple(
            () => Assert.Equal(string.Empty, content.Text),
            () => Assert.Null(content.Kind));
    }

    /// <summary>
    ///     Test that a file that holds only a byte-order mark decodes to an empty text.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_OnlyBom_ReturnsEmptyText()
    {
        // Arrange: only the UTF-8 mark

        // Act: decode it
        var content = FileTextReader.Decode(Utf8Bom);

        // Assert: the text is empty
        Assert.Equal(string.Empty, content.Text);
    }

    /// <summary>
    ///     Test that only the first byte-order mark is removed.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_BomInsideText_IsKept()
    {
        // Arrange: a mark at the start and the same character again in the middle of the text
        var bytes = Join(Utf8Bom, Encoding.UTF8.GetBytes("a\uFEFFb"));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the mark in the middle is an ordinary character
        Assert.Equal("a\uFEFFb", content.Text);
    }

    /// <summary>
    ///     Test that a UTF-16 file cut in the middle of a code unit does not fail.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_TruncatedUtf16_ReplacesPartialUnit()
    {
        // Arrange: a little-endian mark, one complete letter, and a single stray byte
        byte[] bytes = [0xFF, 0xFE, (byte)'a', 0x00, (byte)'b'];

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the letter is kept and the stray byte becomes the replacement character
        Assert.Equal("a\uFFFD", content.Text);
    }

    /// <summary>
    ///     Test that UTF-16 without a byte-order mark is not recognized and looks binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf16WithoutBom_IsBinary()
    {
        // Arrange: little-endian UTF-16 bytes of ASCII text, with no mark
        var bytes = Encoding.Unicode.GetBytes(Sample);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the NUL bytes of the second halves make the file binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that a NUL character inside the examined window makes a file binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_NulInsideSniffWindow_IsBinary()
    {
        // Arrange: letters with a NUL as the last character of the window
        var bytes = LettersWithNulAt(SniffLength + 1, SniffLength - 1);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is binary and has no text
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.Binary, content.Kind),
            () => Assert.Null(content.Text));
    }

    /// <summary>
    ///     Test that a NUL character just beyond the examined window does not make a file binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_NulJustBeyondSniffWindow_IsText()
    {
        // Arrange: letters with a NUL as the first character after the window
        var bytes = LettersWithNulAt(SniffLength + 1, SniffLength);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is text and keeps the NUL character
        Assert.Multiple(
            () => Assert.Null(content.Kind),
            () => Assert.Equal(SniffLength + 1, content.Text!.Length));
    }

    /// <summary>
    ///     Test that a NUL as the very first character makes a file binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_NulAtStart_IsBinary()
    {
        // Arrange: a NUL byte followed by letters
        var bytes = LettersWithNulAt(Sample.Length, 0);

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that UTF-16 text of ASCII letters is not binary although half its bytes are NUL.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf16AsciiText_IsNotBinary()
    {
        // Arrange: UTF-16 bytes with the mark, whose raw bytes contain many NUL bytes
        var bytes = Join(Utf16LittleEndianBom, Encoding.Unicode.GetBytes(Sample));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the decoded text has no NUL character, so the file is text
        Assert.Multiple(
            () => Assert.Null(content.Kind),
            () => Assert.Equal(Sample, content.Text));
    }

    /// <summary>
    ///     Test that a NUL character in decoded UTF-16 text makes the file binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf16WithNulCharacter_IsBinary()
    {
        // Arrange: UTF-16 text that contains the character U+0000
        var bytes = Join(Utf16LittleEndianBom, Encoding.Unicode.GetBytes("a\0b"));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that UTF-32 is not supported and the file is reported as binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Utf32WithBom_IsBinary()
    {
        // Arrange: little-endian UTF-32 bytes with the mark, which contain NUL bytes in every character
        var bytes = Join(Encoding.UTF32.GetPreamble(), Encoding.UTF32.GetBytes(Sample));

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that the signature of a PNG image is recognized as binary.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_PngImageHeader_IsBinary()
    {
        // Arrange: the PNG signature followed by the length field of the first chunk
        byte[] bytes = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: the file is binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that the binary outcome carries an explanation and no text.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_Binary_HasMessageAndNoText()
    {
        // Arrange: bytes that start with NUL
        byte[] bytes = [0x00, 0x01];

        // Act: decode them
        var content = FileTextReader.Decode(bytes);

        // Assert: there is a message that names the cause and no text
        Assert.Multiple(
            () => Assert.Null(content.Text),
            () => Assert.Contains("binary", content.Message, StringComparison.OrdinalIgnoreCase),
            () => Assert.Contains("NUL", content.Message));
    }

    /// <summary>
    ///     Test that decoding the same bytes twice gives the same result and leaves the input unchanged.
    /// </summary>
    [Fact]
    public void FileTextReader_Decode_SameBytesTwice_ReturnsSameResultAndKeepsInput()
    {
        // Arrange: bytes with a mark, and a copy to compare with afterwards
        var bytes = Join(Utf8Bom, Encoding.UTF8.GetBytes(Sample));
        var copy = bytes.ToArray();

        // Act: decode the bytes twice
        var first = FileTextReader.Decode(bytes);
        var second = FileTextReader.Decode(bytes);

        // Assert: the results are equal and the input is unchanged
        Assert.Multiple(
            () => Assert.Equal(first.Text, second.Text),
            () => Assert.Equal(copy, bytes));
    }

    /// <summary>
    ///     Test that the size ceiling is 1 GiB.
    /// </summary>
    [Fact]
    public void FileTextReader_SizeCeiling_Constant_IsOneGiB()
    {
        // Arrange: the documented ceiling

        // Act / Assert: the constant has the documented value
        Assert.Equal(OneGiB, FileTextReader.SizeCeilingBytes);
    }

    /// <summary>
    ///     Test that a file whose size equals the limit is read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_FileAtLimit_IsRead()
    {
        // Arrange: a file exactly as large as the limit
        using var temp = new TempDirectory();
        var bytes = Encoding.UTF8.GetBytes(Sample);
        var path = WriteFile(temp, "a.txt", bytes);

        // Act: read it with the limit equal to its size
        var content = await FileTextReader.ReadAsync(path, bytes.Length, TestContext.Current.CancellationToken);

        // Assert: the text is returned
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that a file one byte over the limit is reported as too large and is not read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_FileOneByteOverLimit_IsTooLarge()
    {
        // Arrange: a file one byte larger than the limit
        using var temp = new TempDirectory();
        var bytes = Encoding.UTF8.GetBytes(Sample);
        var path = WriteFile(temp, "a.txt", bytes);

        // Act: read it with a limit one byte smaller than its size
        var content = await FileTextReader.ReadAsync(path, bytes.Length - 1, TestContext.Current.CancellationToken);

        // Assert: the file is too large and no text is returned
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.TooLarge, content.Kind),
            () => Assert.Null(content.Text));
    }

    /// <summary>
    ///     Test that the message for a file that is too large names its size and the limit.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_TooLarge_MessageNamesSizeAndLimit()
    {
        // Arrange: a file larger than a limit of four bytes
        const long limit = 4;
        using var temp = new TempDirectory();
        var bytes = Encoding.UTF8.GetBytes(Sample);
        var path = WriteFile(temp, "a.txt", bytes);

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, limit, TestContext.Current.CancellationToken);

        // Assert: the message states both numbers
        Assert.Multiple(
            () => Assert.Contains(((long)bytes.Length).ToString("N0"), content.Message),
            () => Assert.Contains(limit.ToString("N0"), content.Message));
    }

    /// <summary>
    ///     Test that a limit of zero means no limit.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_ZeroLimit_ReadsLargeFile()
    {
        // Arrange: a file of two hundred thousand letters
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "large.txt", Enumerable.Repeat((byte)'a', LargeFileSize).ToArray());

        // Act: read it with no limit
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the whole file is read
        Assert.Equal(LargeFileSize, content.Text!.Length);
    }

    /// <summary>
    ///     Test that a file above the ceiling is refused even when there is no limit.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_FileAboveCeilingWithNoLimit_IsTooLarge()
    {
        // Arrange: a sparse file one byte above the ceiling, created without writing any data
        using var temp = new TempDirectory();
        var path = CreateSparseFile(temp, "huge.bin", OneGiB + 1);

        // Act: read it with no limit
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the file is too large and the message names the ceiling
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.TooLarge, content.Kind),
            () => Assert.Contains(OneGiB.ToString("N0"), content.Message));
    }

    /// <summary>
    ///     Test that a limit above the ceiling does not raise the ceiling.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_LimitAboveCeiling_StillRefusesAboveCeiling()
    {
        // Arrange: the same sparse file and a limit that exceeds the ceiling
        using var temp = new TempDirectory();
        var path = CreateSparseFile(temp, "huge.bin", OneGiB + 1);

        // Act: read it with the largest possible limit
        var content = await FileTextReader.ReadAsync(path, long.MaxValue, TestContext.Current.CancellationToken);

        // Assert: the file is still too large
        Assert.Equal(FileDiagnosticKind.TooLarge, content.Kind);
    }

    /// <summary>
    ///     Test that a UTF-8 file is read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_Utf8File_ReturnsText()
    {
        // Arrange: a UTF-8 file without a mark
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "a.txt", Encoding.UTF8.GetBytes(Sample));

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the text is returned, with no reason to skip it
        Assert.Multiple(
            () => Assert.Equal(Sample, content.Text),
            () => Assert.Null(content.Kind),
            () => Assert.Null(content.Message));
    }

    /// <summary>
    ///     Test that a UTF-8 file with a byte-order mark is read without the mark.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_Utf8BomFile_StripsBom()
    {
        // Arrange: a UTF-8 file with a mark
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "a.txt", Join(Utf8Bom, Encoding.UTF8.GetBytes(Sample)));

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the text has no mark
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that a UTF-16 file with a byte-order mark is read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_Utf16File_ReturnsText()
    {
        // Arrange: a UTF-16 file with a mark
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "a.txt", Join(Utf16LittleEndianBom, Encoding.Unicode.GetBytes(Sample)));

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the text is returned
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that an empty file is read as empty text.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_EmptyFile_ReturnsEmptyText()
    {
        // Arrange: a file with no bytes
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "empty.txt", []);

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the text is empty and the file is not skipped
        Assert.Multiple(
            () => Assert.Equal(string.Empty, content.Text),
            () => Assert.Null(content.Kind));
    }

    /// <summary>
    ///     Test that a binary file is recognized when it is read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_BinaryFile_IsBinary()
    {
        // Arrange: a file that starts with NUL bytes
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "data.bin", [0x00, 0x01, 0x02]);

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the file is binary
        Assert.Equal(FileDiagnosticKind.Binary, content.Kind);
    }

    /// <summary>
    ///     Test that a file that does not exist is reported as unreadable and does not throw.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_MissingFile_IsUnreadable()
    {
        // Arrange: a path in an existing directory with no file at it
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, "missing.txt");

        // Act: read it
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the outcome is unreadable with a message and no text
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.Unreadable, content.Kind),
            () => Assert.Null(content.Text),
            () => Assert.Contains("cannot be read", content.Message));
    }

    /// <summary>
    ///     Test that a directory path is reported as unreadable and does not throw.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_DirectoryPath_IsUnreadable()
    {
        // Arrange: the path of a directory
        using var temp = new TempDirectory();

        // Act: read it as a file
        var content = await FileTextReader.ReadAsync(temp.FullPath, 0, TestContext.Current.CancellationToken);

        // Assert: the outcome is unreadable
        Assert.Equal(FileDiagnosticKind.Unreadable, content.Kind);
    }

    /// <summary>
    ///     Test that a file that another stream holds exclusively is reported as unreadable and does not throw.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_ExclusivelyLockedFile_IsUnreadable()
    {
        // Arrange: a file that this test holds open without sharing
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "locked.txt", Encoding.UTF8.GetBytes(Sample));
        using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act: read it while it is locked
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the outcome is unreadable and not an exception
        Assert.Multiple(
            () => Assert.Equal(FileDiagnosticKind.Unreadable, content.Kind),
            () => Assert.Null(content.Text));
    }

    /// <summary>
    ///     Test that a file that another stream has open for writing, allowing sharing, is still read.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_FileOpenForWriting_IsStillReadable()
    {
        // Arrange: a file that a writer holds open with read and write sharing, after writing and flushing
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, "log.txt");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await writer.WriteAsync(Encoding.UTF8.GetBytes(Sample), TestContext.Current.CancellationToken);
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        // Act: read it while the writer is open
        var content = await FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken);

        // Assert: the written text is returned
        Assert.Equal(Sample, content.Text);
    }

    /// <summary>
    ///     Test that many files are read at the same time, each with its own content.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_ParallelReads_ReturnEachFilesOwnText()
    {
        // Arrange: files whose content names the file
        using var temp = new TempDirectory();
        var paths = Enumerable.Range(0, ParallelFiles)
            .Select(index => WriteFile(temp, $"f{index}.txt", Encoding.UTF8.GetBytes($"file {index}")))
            .ToList();

        // Act: read them all at once
        var contents = await Task.WhenAll(paths.Select(path => FileTextReader.ReadAsync(path, 0, TestContext.Current.CancellationToken)));

        // Assert: every result belongs to its own file
        Assert.Equal(
            Enumerable.Range(0, ParallelFiles).Select(index => $"file {index}"),
            contents.Select(content => content.Text));
    }

    /// <summary>
    ///     Test that a token that is already cancelled stops the read of a file that has content.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange: a file with content and a token that is already cancelled
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "a.txt", Encoding.UTF8.GetBytes(Sample));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the read is cancelled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileTextReader.ReadAsync(path, 0, source.Token));
    }

    /// <summary>
    ///     Test that a token that is already cancelled stops the read of an empty file too.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_CancelledTokenWithEmptyFile_ThrowsOperationCanceledException()
    {
        // Arrange: an empty file and a token that is already cancelled
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "empty.txt", []);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the read is cancelled although there is nothing to read
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileTextReader.ReadAsync(path, 0, source.Token));
    }

    /// <summary>
    ///     Test that a null path is rejected.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_NullPath_ThrowsArgumentNullException()
    {
        // Arrange: no path

        // Act / Assert: the read throws
        await Assert.ThrowsAsync<ArgumentNullException>(() => FileTextReader.ReadAsync(null!, 0, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an empty path is rejected.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_EmptyPath_ThrowsArgumentException()
    {
        // Arrange: an empty path

        // Act / Assert: the read throws
        await Assert.ThrowsAsync<ArgumentException>(() => FileTextReader.ReadAsync(string.Empty, 0, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a negative size limit is rejected.
    /// </summary>
    [Fact]
    public async Task FileTextReader_ReadAsync_NegativeLimit_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a valid path and a negative limit
        using var temp = new TempDirectory();
        var path = WriteFile(temp, "a.txt", Encoding.UTF8.GetBytes(Sample));

        // Act / Assert: the read throws
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FileTextReader.ReadAsync(path, -1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Creates a file of the stated length without writing its content.
    /// </summary>
    /// <param name="temp">The directory to create the file in.</param>
    /// <param name="name">The file name.</param>
    /// <param name="length">The length in bytes.</param>
    /// <returns>The absolute path of the file.</returns>
    private static string CreateSparseFile(TempDirectory temp, string name, long length)
    {
        var path = Path.Combine(temp.FullPath, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.SetLength(length);
        return path;
    }
}
