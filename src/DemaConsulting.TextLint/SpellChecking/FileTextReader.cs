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

using System.Text;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     Reads a file into text for checking: it chooses the encoding, strips the byte-order mark, and
///     recognizes files that should not be checked.
/// </summary>
/// <remarks>
///     Real repositories hold files in several encodings and files that are not text at all, so reading is
///     its own unit with its own rules. UTF-8 with or without a byte-order mark, and UTF-16 (either byte
///     order) with a byte-order mark, are decoded; a file with no byte-order mark is decoded as UTF-8, and a
///     byte sequence that is not valid in the encoding becomes the replacement character, so decoding never
///     fails. UTF-32 is not supported: such a file contains NUL characters and is reported as binary. A file
///     is binary when a NUL character appears within its first 1,024 decoded characters (UTF-16 files are
///     decoded before the test, so their NUL bytes do not count). A file larger than the configured limit,
///     or than a fixed ceiling of 1 GiB that no file may exceed because a string cannot hold more, is not read.
///     The size is taken from the open file, so a file that grows while it is read is read only up to the
///     size it had when it was opened. Problems that are expected while reading (the file is gone, locked,
///     or access is denied) are returned as an outcome; any other exception is a defect and propagates, and
///     so does cancellation. The class is stateless and thread-safe.
/// </remarks>
internal static class FileTextReader
{
    /// <summary>
    ///     The size, in bytes, above which no file is read: 2 to the power of 30, which is 1 GiB.
    /// </summary>
    /// <remarks>
    ///     A .NET string cannot hold more than about a billion characters, and the file's bytes and its text
    ///     are in memory together, so a larger file is refused before any memory is committed.
    /// </remarks>
    internal const long SizeCeilingBytes = 1L << 30;

    /// <summary>
    ///     The number of decoded characters examined for a NUL character to decide whether a file is binary.
    /// </summary>
    internal const int BinarySniffLength = 1024;

    /// <summary>
    ///     The length of the UTF-8 byte-order mark.
    /// </summary>
    private const int Utf8BomLength = 3;

    /// <summary>
    ///     The length of the UTF-16 byte-order marks.
    /// </summary>
    private const int Utf16BomLength = 2;

    /// <summary>
    ///     The first byte of the UTF-8 byte-order mark.
    /// </summary>
    private const byte Utf8Bom0 = 0xEF;

    /// <summary>
    ///     The second byte of the UTF-8 byte-order mark.
    /// </summary>
    private const byte Utf8Bom1 = 0xBB;

    /// <summary>
    ///     The third byte of the UTF-8 byte-order mark.
    /// </summary>
    private const byte Utf8Bom2 = 0xBF;

    /// <summary>
    ///     The first byte of the little-endian UTF-16 byte-order mark and the second of the big-endian one.
    /// </summary>
    private const byte BomFf = 0xFF;

    /// <summary>
    ///     The second byte of the little-endian UTF-16 byte-order mark and the first of the big-endian one.
    /// </summary>
    private const byte BomFe = 0xFE;

    /// <summary>
    ///     The buffer size that turns the stream's own buffering off; the whole file is read in one call, so it needs none.
    /// </summary>
    private const int NoBuffer = 1;

    /// <summary>
    ///     Reads a file into text, or reports why it will not be checked.
    /// </summary>
    /// <remarks>
    ///     The file is opened for reading while allowing other readers and writers, so a file that another
    ///     program has open for writing can still be read. Side effects: reads the file; nothing is
    ///     written. Thread-safe.
    /// </remarks>
    /// <param name="fullPath">The absolute path of the file. Must not be null or empty.</param>
    /// <param name="maxFileSize">
    ///     The largest size in bytes to read; 0 means no limit other than the fixed ceiling. Must not be negative.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The text, or the reason the file will not be checked; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fullPath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullPath"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxFileSize"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static async Task<FileTextContent> ReadAsync(string fullPath, long maxFileSize, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullPath);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFileSize);

        // Honor a token that is already cancelled even when there is nothing to read, so the contract does not depend on file size
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Open the file and take its size from the open handle, so the size and the content agree
            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                NoBuffer,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (stream.ConfigureAwait(false))
            {
                var limit = maxFileSize > 0 ? Math.Min(maxFileSize, SizeCeilingBytes) : SizeCeilingBytes;
                var size = stream.Length;
                if (size > limit)
                {
                    return FileTextContent.NotChecked(
                        FileDiagnosticKind.TooLarge,
                        $"The file is {size:N0} bytes, which is larger than the limit of {limit:N0} bytes.");
                }

                // Read the whole file; it is shorter than the size if it was truncated meanwhile
                var bytes = new byte[size];
                var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken).ConfigureAwait(false);
                return Decode(bytes.AsSpan(0, count));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // These are the failures a real file system produces; anything else is a defect and propagates
            return FileTextContent.NotChecked(FileDiagnosticKind.Unreadable, $"The file cannot be read: {exception.Message}");
        }
    }

    /// <summary>
    ///     Decodes the bytes of a file, or recognizes binary data.
    /// </summary>
    /// <remarks>
    ///     Pure: no file is involved, which makes the rules testable on their own. Stateless and thread-safe.
    /// </remarks>
    /// <param name="bytes">The content of the file.</param>
    /// <returns>The decoded text without its byte-order mark, or the binary outcome.</returns>
    internal static FileTextContent Decode(ReadOnlySpan<byte> bytes)
    {
        // Choose the encoding from the byte-order mark, defaulting to UTF-8, and drop the mark
        string text;
        if (bytes.Length >= Utf8BomLength && bytes[0] == Utf8Bom0 && bytes[1] == Utf8Bom1 && bytes[2] == Utf8Bom2)
        {
            text = Encoding.UTF8.GetString(bytes[Utf8BomLength..]);
        }
        else if (bytes.Length >= Utf16BomLength && bytes[0] == BomFf && bytes[1] == BomFe)
        {
            text = Encoding.Unicode.GetString(bytes[Utf16BomLength..]);
        }
        else if (bytes.Length >= Utf16BomLength && bytes[0] == BomFe && bytes[1] == BomFf)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes[Utf16BomLength..]);
        }
        else
        {
            text = Encoding.UTF8.GetString(bytes);
        }

        // A NUL character near the start means the file is not text
        var head = text.AsSpan(0, Math.Min(text.Length, BinarySniffLength));
        return head.Contains('\0')
            ? FileTextContent.NotChecked(
                FileDiagnosticKind.Binary,
                $"The file looks like binary data: it contains a NUL character in its first {BinarySniffLength:N0} characters.")
            : FileTextContent.FromText(text);
    }
}
