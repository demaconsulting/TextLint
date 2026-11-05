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

using System.Runtime.InteropServices;
using System.Text;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     Reads the plain-text word lists of one dictionary, validates every entry, and merges the
///     entries into one set of distinct, case-folded words.
/// </summary>
/// <remarks>
///     Word lists are plain text so that people can review, diff, and edit them, which means the
///     builder cannot trust them: a stray digit, a curly apostrophe, or a file saved in the wrong
///     encoding would otherwise become a silently wrong dictionary. The reader therefore applies one
///     strict entry rule and reports every violation with its file and line instead of skipping
///     it. The files of a dictionary are merged in ordinal path order; because the result is a set
///     whose later ordering is imposed by the writer, the order of files never changes the output.
///     The class holds no state; every call is independent and safe to run concurrently.
/// </remarks>
internal static class WordListReader
{
    /// <summary>
    ///     The extension that marks a file as a word list. Matched ignoring case, like every other
    ///     file-name comparison of the builder, so that <c>LIST.TXT</c> is never silently skipped on a
    ///     case-insensitive file system and the set of files read is the same on every operating system.
    /// </summary>
    internal const string FileExtension = ".txt";

    /// <summary>
    ///     The maximum number of detailed problems reported for one dictionary; further problems are
    ///     only counted, so a hopelessly broken list cannot flood the build log.
    /// </summary>
    internal const int MaxDiagnostics = 100;

    /// <summary>
    ///     The character that makes a line a comment when it is the first non-blank character.
    /// </summary>
    private const char CommentMarker = '#';

    /// <summary>
    ///     The byte order mark that some editors write at the start of a UTF-8 file.
    /// </summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    ///     The line separator. Carriage returns are removed by trimming, so both line ending styles work.
    /// </summary>
    private const char LineFeed = '\n';

    /// <summary>
    ///     The characters trimmed from both ends of every line: spaces, tabs, and the carriage return of a CRLF ending.
    /// </summary>
    private const string TrimmedCharacters = " \t\r";

    /// <summary>
    ///     The longest part of an offending entry that is quoted in a message.
    /// </summary>
    private const int MaxQuotedLength = 32;

    /// <summary>
    ///     The decoder that rejects invalid UTF-8 instead of replacing it, so corruption is reported
    ///     rather than hidden; it does not emit a byte order mark.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Finds the word-list files of a dictionary folder.
    /// </summary>
    /// <remarks>
    ///     Searches the folder and all folders beneath it. The extension test ignores case, like every
    ///     other file-name comparison of the builder, and the result is sorted in ordinal order, so the
    ///     same files are found in the same order on every operating system. There is deliberately no
    ///     existence check before the search: such a check cannot tell a missing folder from one that
    ///     is merely unreadable. Instead only a genuine "directory not found" condition counts as a
    ///     missing folder, reported through <paramref name="folderMissing"/> with no problem, and any
    ///     other failure (access denied or an I/O error, possibly part-way through) is reported through
    ///     <paramref name="problem"/> and yields no files, so the caller can never mistake a failed
    ///     search for an empty or absent folder. Reads the file system; thread-safe.
    /// </remarks>
    /// <param name="folder">The dictionary folder. Must not be null.</param>
    /// <param name="problem">Receives the problem when the folder could not be searched; otherwise null.</param>
    /// <param name="folderMissing">Receives <see langword="true"/> when the folder does not exist; otherwise <see langword="false"/>.</param>
    /// <param name="operations">The file system operations to use; null for the real file system.</param>
    /// <returns>The full paths of the word-list files in ordinal order; empty when the folder is missing, holds none, or could not be searched.</returns>
    internal static IReadOnlyList<string> FindWordListFiles(
        string folder,
        out WordListDiagnostic? problem,
        out bool folderMissing,
        FileOperations? operations = null)
    {
        problem = null;
        folderMissing = false;

        // Keep only files with the extension and fix the order; the lazy enumeration can fail at any point
        try
        {
            var files = (operations ?? FileOperations.Default).EnumerateFiles(folder)
                .Where(file => string.Equals(Path.GetExtension(file), FileExtension, StringComparison.OrdinalIgnoreCase))
                .ToList();
            files.Sort(StringComparer.Ordinal);
            return files;
        }
        catch (DirectoryNotFoundException)
        {
            // The one condition that means "absent" rather than "unreadable"; the caller decides whether it is an error
            folderMissing = true;
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem = new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the folder cannot be searched for word lists: {exception.Message}");
            return [];
        }
    }

    /// <summary>
    ///     Reads and merges every word-list file of a dictionary folder.
    /// </summary>
    /// <remarks>
    ///     Every problem found in every file is collected, up to <see cref="MaxDiagnostics"/> in
    ///     detail, so the author can fix a whole list in one pass. A folder that cannot be searched is
    ///     one such problem; a folder that does not exist simply yields an empty result, which the
    ///     caller treats according to whether the folder is required. Reads the file system and
    ///     allocates the merged set; thread-safe because it shares no state.
    /// </remarks>
    /// <param name="folder">The dictionary folder. Must not be null.</param>
    /// <param name="operations">The file system operations to use; null for the real file system.</param>
    /// <returns>The merged words and any problems; never null.</returns>
    internal static WordListReadResult Read(string folder, FileOperations? operations = null)
    {
        var files = FindWordListFiles(folder, out var searchProblem, out _, operations);
        return ReadFiles(files, folder, searchProblem, operations);
    }

    /// <summary>
    ///     Reads and merges an explicit list of word-list files.
    /// </summary>
    /// <remarks>
    ///     Used when the files of a dictionary come from several places (the technical dictionary
    ///     reads its general lists, its language lists, and its keep list separately), and by
    ///     <see cref="Read"/> for a whole folder. The caps and the diagnostics behave exactly as for a
    ///     folder. A listed file that cannot be read is a problem, never an absent file. Reads the
    ///     file system; thread-safe because it shares no state.
    /// </remarks>
    /// <param name="files">The full paths of the files, in the order to read them. Must not be null.</param>
    /// <param name="summaryPath">The folder or file named by the cap summary diagnostic. Must not be null.</param>
    /// <param name="earlierProblem">A problem found before the files were listed, to report first; null for none.</param>
    /// <param name="operations">The file system operations to use; null for the real file system.</param>
    /// <returns>The merged words and any problems; never null.</returns>
    internal static WordListReadResult ReadFiles(
        IReadOnlyList<string> files,
        string summaryPath,
        WordListDiagnostic? earlierProblem = null,
        FileOperations? operations = null)
    {
        var words = new Dictionary<string, uint>(StringComparer.Ordinal);
        var problems = new ProblemLog(summaryPath);
        if (earlierProblem is not null)
        {
            problems.Add(earlierProblem.Path, earlierProblem.Line, earlierProblem.Message);
        }

        // Merge the files one after another; duplicates across files collapse in the dictionary
        var fileSystem = operations ?? FileOperations.Default;
        foreach (var file in files)
        {
            ReadFile(file, words, problems, fileSystem);
        }

        return new WordListReadResult(words, problems.Finish(), problems.Count, files.Count);
    }

    /// <summary>
    ///     Reads one word-list file into the merged set.
    /// </summary>
    /// <remarks>
    ///     A file that cannot be read or decoded is reported as one file-level problem and
    ///     contributes no words. Reads the file system; thread-safe.
    /// </remarks>
    /// <param name="file">The full path of the file. Must not be null.</param>
    /// <param name="words">The merged set to add to. Must not be null.</param>
    /// <param name="problems">The log that receives every problem. Must not be null.</param>
    /// <param name="operations">The file system operations to use. Must not be null.</param>
    private static void ReadFile(string file, Dictionary<string, uint> words, ProblemLog problems, FileOperations operations)
    {
        // Decode strictly so that a file saved in another encoding is reported, not misread
        string text;
        try
        {
            text = StrictUtf8.GetString(operations.ReadAllBytes(file));
        }
        catch (DecoderFallbackException)
        {
            problems.Add(file, WordListDiagnostic.NoLine, "the file is not valid UTF-8");
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(file, WordListDiagnostic.NoLine, $"the file cannot be read: {exception.Message}");
            return;
        }

        // Tolerate a byte order mark
        var remaining = text.AsSpan();
        if (!remaining.IsEmpty && remaining[0] == ByteOrderMark)
        {
            remaining = remaining[1..];
        }

        // Walk the lines, counting from one so diagnostics match what an editor shows
        var lineNumber = 0;
        while (!remaining.IsEmpty)
        {
            lineNumber++;
            var end = remaining.IndexOf(LineFeed);
            ReadOnlySpan<char> line;
            if (end < 0)
            {
                line = remaining;
                remaining = default;
            }
            else
            {
                line = remaining[..end];
                remaining = remaining[(end + 1)..];
            }

            ReadLine(line, file, lineNumber, words, problems);
        }
    }

    /// <summary>
    ///     Processes one line: skips blanks and comments, validates an entry, and merges a valid one.
    /// </summary>
    /// <remarks>
    ///     Thread-safe because it shares no state beyond the collections the caller passes in.
    /// </remarks>
    /// <param name="line">The raw line without its line feed.</param>
    /// <param name="file">The file the line came from, for diagnostics.</param>
    /// <param name="lineNumber">The 1-based number of the line.</param>
    /// <param name="words">The merged set to add to. Must not be null.</param>
    /// <param name="problems">The log that receives a problem. Must not be null.</param>
    private static void ReadLine(
        ReadOnlySpan<char> line,
        string file,
        int lineNumber,
        Dictionary<string, uint> words,
        ProblemLog problems)
    {
        // Ignore blank lines and comment lines
        var entry = line.Trim(TrimmedCharacters);
        if (entry.IsEmpty || entry[0] == CommentMarker)
        {
            return;
        }

        // Report an invalid entry with its position rather than guessing what was meant
        var reason = FindEntryProblem(entry);
        if (reason is not null)
        {
            var quoted = entry.Length > MaxQuotedLength ? string.Concat(entry[..MaxQuotedLength], "...") : entry.ToString();
            problems.Add(file, lineNumber, $"'{quoted}' is not a valid word: {reason}");
            return;
        }

        AddWord(entry, words);
    }

    /// <summary>
    ///     Adds a valid entry to the merged set under its folded key, recording its capitalization form.
    /// </summary>
    /// <remarks>
    ///     Merging by folded key is what makes the dictionary case-insensitive and de-duplicated;
    ///     OR-ing the form flags keeps the knowledge of every spelling the lists used. Thread-safe
    ///     because it shares no state beyond the collection passed in.
    /// </remarks>
    /// <param name="entry">The valid entry as written. Must satisfy the entry rule.</param>
    /// <param name="words">The merged set to add to. Must not be null.</param>
    private static void AddWord(ReadOnlySpan<char> entry, Dictionary<string, uint> words)
    {
        // Fold into a small array: a valid entry is never longer than the maximum word length
        var folded = new char[entry.Length];
        for (var i = 0; i < entry.Length; i++)
        {
            folded[i] = DictionaryFormat.Fold(entry[i]);
        }

        // Merge the form of this spelling into the key's flags
        ref var flags = ref CollectionsMarshal.GetValueRefOrAddDefault(words, new string(folded), out _);
        flags |= DictionaryFormat.ClassifyCase(entry);
    }

    /// <summary>
    ///     Checks an entry against the entry rule and describes the first violation.
    /// </summary>
    /// <remarks>
    ///     The rule: one to <see cref="DictionaryFormat.MaxWordLength"/> characters; ASCII letters,
    ///     Latin-1 letters, and the straight apostrophe only; the first and last characters are
    ///     letters; no two apostrophes in a row. Pure function; thread-safe.
    /// </remarks>
    /// <param name="entry">The trimmed, non-empty entry.</param>
    /// <returns>A description of the first violation, or <see langword="null"/> when the entry is valid.</returns>
    private static string? FindEntryProblem(ReadOnlySpan<char> entry)
    {
        if (entry.Length > DictionaryFormat.MaxWordLength)
        {
            return $"it is longer than {DictionaryFormat.MaxWordLength} characters";
        }

        // Scan left to right so the reported violation is the first one a reader would see
        for (var i = 0; i < entry.Length; i++)
        {
            var character = entry[i];
            if (character == DictionaryFormat.Apostrophe)
            {
                if (i == 0)
                {
                    return "it must start with a letter";
                }

                if (i == entry.Length - 1)
                {
                    return "it must end with a letter";
                }

                if (entry[i - 1] == DictionaryFormat.Apostrophe)
                {
                    return "it contains two apostrophes in a row";
                }
            }
            else if (character == DictionaryFormat.TypographicApostrophe)
            {
                return "it contains a typographic apostrophe (U+2019); use the straight apostrophe (')";
            }
            else if (!DictionaryFormat.IsLetter(character))
            {
                return $"it contains '{character}' (U+{(int)character:X4}), which is not a letter or an apostrophe";
            }
        }

        return null;
    }

    /// <summary>
    ///     Collects the problems found while reading one dictionary, keeping only the first few in detail.
    /// </summary>
    /// <remarks>
    ///     Separating the cap from the reading logic keeps both simple. Not thread-safe; one log
    ///     belongs to one read.
    /// </remarks>
    private sealed class ProblemLog
    {
        /// <summary>
        ///     The dictionary folder, used as the path of the cap summary diagnostic.
        /// </summary>
        private readonly string _folder;

        /// <summary>
        ///     The detailed diagnostics kept so far, at most <see cref="MaxDiagnostics"/>.
        /// </summary>
        private readonly List<WordListDiagnostic> _kept = [];

        /// <summary>
        ///     Initializes a new instance of the <see cref="ProblemLog"/> class.
        /// </summary>
        /// <param name="folder">The dictionary folder. Must not be null.</param>
        public ProblemLog(string folder)
        {
            _folder = folder;
        }

        /// <summary>
        ///     Gets the total number of problems added, including those not kept in detail.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        ///     Records one problem, keeping its detail only while under the cap.
        /// </summary>
        /// <param name="path">The file the problem concerns.</param>
        /// <param name="line">The 1-based line, or <see cref="WordListDiagnostic.NoLine"/>.</param>
        /// <param name="message">The description of the problem.</param>
        public void Add(string path, int line, string message)
        {
            Count++;
            if (_kept.Count < MaxDiagnostics)
            {
                _kept.Add(new WordListDiagnostic(path, line, message));
            }
        }

        /// <summary>
        ///     Produces the final diagnostics: the kept problems plus a summary when some were dropped.
        /// </summary>
        /// <returns>The diagnostics in the order the problems were found.</returns>
        public IReadOnlyList<WordListDiagnostic> Finish()
        {
            // Tell the reader how many problems the cap hid, so the log is never misleadingly short
            var dropped = Count - _kept.Count;
            if (dropped > 0)
            {
                _kept.Add(new WordListDiagnostic(_folder, WordListDiagnostic.NoLine, $"{dropped} more problems not shown"));
            }

            return _kept;
        }
    }
}
