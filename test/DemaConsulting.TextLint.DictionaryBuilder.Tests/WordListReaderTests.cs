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
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="WordListReader"/> class.
/// </summary>
/// <remarks>
///     The reader works on real files, so each test writes its lists into its own temporary
///     folder. The reader's one documented dependency, <see cref="DictionaryFormat"/>, is used as
///     it is, not replaced by a test double.
/// </remarks>
public class WordListReaderTests
{
    /// <summary>
    ///     Reads a folder that contains a single list file with the given content.
    /// </summary>
    /// <param name="temp">The temporary directory to write into.</param>
    /// <param name="content">The text of the list file.</param>
    /// <returns>The read result.</returns>
    private static WordListReadResult ReadSingleList(TempDirectory temp, string content)
    {
        temp.CreateFile("list/words.txt", content);
        return WordListReader.Read(temp.PathOf("list"));
    }

    /// <summary>
    ///     Test that comment lines, blank lines, and surrounding spaces and tabs are ignored or trimmed.
    /// </summary>
    [Fact]
    public void WordListReader_Read_CommentsAndBlankLines_AreIgnored()
    {
        // Arrange: a list with a comment, blank lines, and padded entries
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "# a comment\n\n   \n  apple\t\n\t# indented comment\nbanana\n");

        // Assert: only the two words are present and nothing is reported
        Assert.False(result.HasProblems);
        Assert.Equal(["apple", "banana"], result.Words.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that CRLF line endings and a byte order mark are accepted.
    /// </summary>
    [Fact]
    public void WordListReader_Read_CrLfAndBom_Handled()
    {
        // Arrange: a list that starts with a byte order mark and uses CRLF line endings
        using var temp = new TempDirectory();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("apple\r\nbanana\r\n")).ToArray();
        temp.CreateFile("list/words.txt", bytes);

        // Act: read it
        var result = WordListReader.Read(temp.PathOf("list"));

        // Assert: both words are read cleanly, including the first one after the byte order mark
        Assert.False(result.HasProblems);
        Assert.Equal(["apple", "banana"], result.Words.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that only files with the exact lower-case extension are found, in ordinal order, including in sub-folders.
    /// </summary>
    [Fact]
    public void WordListReader_FindWordListFiles_MixedExtensions_ReturnsOnlyTxtFilesInOrdinalOrder()
    {
        // Arrange: lists in the folder and a sub-folder, plus files that must be ignored
        using var temp = new TempDirectory();
        temp.CreateFile("list/b.txt", "b\n");
        temp.CreateFile("list/a.txt", "a\n");
        temp.CreateFile("list/sub/c.txt", "c\n");
        temp.CreateFile("list/readme.md", "x\n");
        temp.CreateFile("list/upper.TXT", "x\n");

        // Act: find the files
        var files = WordListReader.FindWordListFiles(temp.PathOf("list"));

        // Assert: exactly the three .txt files, sorted in ordinal order by full path
        Assert.Equal(
            [temp.PathOf("list/a.txt"), temp.PathOf("list/b.txt"), temp.PathOf("list/sub/c.txt")],
            files.Order(StringComparer.Ordinal));
        Assert.Equal(files.Order(StringComparer.Ordinal), files);
    }

    /// <summary>
    ///     Test that a missing folder has no word-list files and reads as an empty result.
    /// </summary>
    [Fact]
    public void WordListReader_Read_MissingFolder_ReturnsNoWordsAndNoProblems()
    {
        // Arrange: a folder path that does not exist
        using var temp = new TempDirectory();

        // Act: read it
        var result = WordListReader.Read(temp.PathOf("missing"));

        // Assert: nothing was read and nothing was reported (the caller decides that this is an error)
        Assert.Empty(result.Words);
        Assert.False(result.HasProblems);
        Assert.Equal(0, result.FileCount);
    }

    /// <summary>
    ///     Test that an entry containing a space is reported with its file and line.
    /// </summary>
    [Fact]
    public void WordListReader_Read_SpaceInEntry_ReportsLine()
    {
        // Arrange: a valid word followed by an entry with an inner space
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "apple\ncolo ur\n");

        // Assert: one problem is reported on line 2 of the file
        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal(2, problem.Line);
        Assert.Equal(temp.PathOf("list/words.txt"), problem.Path);
        Assert.Contains("'colo ur'", problem.Message);
    }

    /// <summary>
    ///     Test that an entry containing a digit is reported with its line.
    /// </summary>
    [Fact]
    public void WordListReader_Read_Digit_ReportsLine()
    {
        // Arrange: an entry that mixes a digit into a word
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "color1\n");

        // Assert: the problem names the entry and its line
        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal(1, problem.Line);
        Assert.Contains("'color1'", problem.Message);
        Assert.Contains("U+0031", problem.Message);
    }

    /// <summary>
    ///     Test that a typographic apostrophe is reported with advice to use the straight one.
    /// </summary>
    [Fact]
    public void WordListReader_Read_TypographicApostrophe_ReportsLine()
    {
        // Arrange: an entry written with the typographic apostrophe
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "don\u2019t\n");

        // Assert: the problem explains the typographic apostrophe
        var problem = Assert.Single(result.Diagnostics);
        Assert.Contains("typographic apostrophe", problem.Message);
    }

    /// <summary>
    ///     Test that an entry that starts with an apostrophe is reported.
    /// </summary>
    [Fact]
    public void WordListReader_Read_LeadingApostrophe_ReportsLine()
    {
        // Arrange: an entry that starts with an apostrophe
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "'tis\n");

        // Assert: the problem says it must start with a letter
        var problem = Assert.Single(result.Diagnostics);
        Assert.Contains("must start with a letter", problem.Message);
    }

    /// <summary>
    ///     Test that an entry that ends with an apostrophe is reported.
    /// </summary>
    [Fact]
    public void WordListReader_Read_TrailingApostrophe_ReportsLine()
    {
        // Arrange: an entry that ends with an apostrophe
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "dogs'\n");

        // Assert: the problem says it must end with a letter
        var problem = Assert.Single(result.Diagnostics);
        Assert.Contains("must end with a letter", problem.Message);
    }

    /// <summary>
    ///     Test that two apostrophes in a row are reported.
    /// </summary>
    [Fact]
    public void WordListReader_Read_DoubleApostrophe_ReportsLine()
    {
        // Arrange: an entry with two apostrophes in a row
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "don''t\n");

        // Assert: the problem names the double apostrophe
        var problem = Assert.Single(result.Diagnostics);
        Assert.Contains("two apostrophes in a row", problem.Message);
    }

    /// <summary>
    ///     Test that a word in another script is reported.
    /// </summary>
    [Fact]
    public void WordListReader_Read_OtherScript_ReportsLine()
    {
        // Arrange: an entry of Japanese characters
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "\u65E5\u672C\n");

        // Assert: the problem names the first offending character
        var problem = Assert.Single(result.Diagnostics);
        Assert.Contains("U+65E5", problem.Message);
    }

    /// <summary>
    ///     Test that an entry of 65 characters is reported and one of 64 characters is accepted.
    /// </summary>
    [Fact]
    public void WordListReader_Read_TooLong_ReportsLine()
    {
        // Arrange: a 64-letter entry and a 65-letter entry
        using var temp = new TempDirectory();
        var accepted = new string('a', DictionaryFormat.MaxWordLength);
        var rejected = new string('b', DictionaryFormat.MaxWordLength + 1);

        // Act: read them
        var result = ReadSingleList(temp, $"{accepted}\n{rejected}\n");

        // Assert: only the longer entry is reported, on line 2, and the shorter one is kept
        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal(2, problem.Line);
        Assert.Contains("longer than 64 characters", problem.Message);
        Assert.Contains(accepted, result.Words.Keys);
    }

    /// <summary>
    ///     Test that a file that is not valid UTF-8 is reported against the file as a whole.
    /// </summary>
    [Fact]
    public void WordListReader_Read_InvalidUtf8_ReportsFile()
    {
        // Arrange: a list file that contains a lone continuation byte
        using var temp = new TempDirectory();
        temp.CreateFile("list/words.txt", new byte[] { 0x61, 0x0A, 0x80, 0x0A });

        // Act: read it
        var result = WordListReader.Read(temp.PathOf("list"));

        // Assert: one problem names the file, has no line, and no words were taken from it
        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal(WordListDiagnostic.NoLine, problem.Line);
        Assert.Equal(temp.PathOf("list/words.txt"), problem.Path);
        Assert.Contains("UTF-8", problem.Message);
        Assert.Empty(result.Words);
    }

    /// <summary>
    ///     Test that a list with many problems keeps only the first problems in detail and counts the rest.
    /// </summary>
    [Fact]
    public void WordListReader_Read_ManyProblems_CappedAtLimit()
    {
        // Arrange: a list in which every line is invalid, well beyond the cap
        using var temp = new TempDirectory();
        const int extra = 5;
        var content = string.Concat(Enumerable.Repeat("bad1\n", WordListReader.MaxDiagnostics + extra));

        // Act: read it
        var result = ReadSingleList(temp, content);

        // Assert: the cap of detailed problems plus one summary that says how many were hidden
        Assert.Equal(WordListReader.MaxDiagnostics + extra, result.ProblemCount);
        Assert.Equal(WordListReader.MaxDiagnostics + 1, result.Diagnostics.Count);
        Assert.Contains($"{extra} more problems not shown", result.Diagnostics[^1].Message);
    }

    /// <summary>
    ///     Test that case variants of a word are merged into one folded key whose flags record every form.
    /// </summary>
    [Fact]
    public void WordListReader_Read_CaseVariants_MergedWithForms()
    {
        // Arrange: one word spelled in lower case, capitalized, and upper case, and a Latin-1 word
        using var temp = new TempDirectory();

        // Act: read it
        var result = ReadSingleList(temp, "paris\nParis\nPARIS\nCaf\u00C9\n");

        // Assert: one folded key carries the three forms; the accented word is folded with UTF-16 characters intact
        Assert.Equal(2, result.Words.Count);
        Assert.Equal(
            DictionaryFormat.CaseLower | DictionaryFormat.CaseCapitalized | DictionaryFormat.CaseUpper,
            result.Words["paris"]);
        Assert.Equal(DictionaryFormat.CaseMixed, result.Words["caf\u00E9"]);
    }

    /// <summary>
    ///     Test that the same word in different files is merged and counted once.
    /// </summary>
    [Fact]
    public void WordListReader_Read_DuplicateAcrossFiles_Merged()
    {
        // Arrange: two files that both list "apple", one of them capitalized
        using var temp = new TempDirectory();
        temp.CreateFile("list/a.txt", "apple\nbanana\n");
        temp.CreateFile("list/sub/b.txt", "Apple\ncherry\n");

        // Act: read the folder
        var result = WordListReader.Read(temp.PathOf("list"));

        // Assert: three distinct words, and "apple" carries both forms
        Assert.Equal(["apple", "banana", "cherry"], result.Words.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(DictionaryFormat.CaseLower | DictionaryFormat.CaseCapitalized, result.Words["apple"]);
        Assert.Equal(2, result.FileCount);
    }

    /// <summary>
    ///     Test that problems in several files are all reported, in file order.
    /// </summary>
    [Fact]
    public void WordListReader_Read_ProblemsInSeveralFiles_AllReported()
    {
        // Arrange: two files that each hold one invalid entry
        using var temp = new TempDirectory();
        temp.CreateFile("list/a.txt", "ok\nbad1\n");
        temp.CreateFile("list/b.txt", "bad2\n");

        // Act: read the folder
        var result = WordListReader.Read(temp.PathOf("list"));

        // Assert: both problems are reported, the first file's first
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(temp.PathOf("list/a.txt"), result.Diagnostics[0].Path);
        Assert.Equal(temp.PathOf("list/b.txt"), result.Diagnostics[1].Path);
    }

    /// <summary>
    ///     Test that a diagnostic is formatted in the compiler style with and without a line.
    /// </summary>
    [Fact]
    public void WordListReader_Read_Diagnostics_FormatAsCompilerErrors()
    {
        // Arrange: a list with one invalid line and a file that is not valid UTF-8
        using var temp = new TempDirectory();
        temp.CreateFile("list/a.txt", "bad1\n");
        temp.CreateFile("list/b.txt", new byte[] { 0xFF });

        // Act: read it and format the two diagnostics
        var result = WordListReader.Read(temp.PathOf("list"));
        var withLine = result.Diagnostics[0].ToString();
        var withoutLine = result.Diagnostics[1].ToString();

        // Assert: the line form has a parenthesized line; the file form has none
        Assert.StartsWith($"{temp.PathOf("list/a.txt")}(1): error: ", withLine);
        Assert.StartsWith($"{temp.PathOf("list/b.txt")}: error: ", withoutLine);
    }
}
