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

// cspell:ignore colour

using DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     System-level tests for the dictionary builder.
/// </summary>
/// <remarks>
///     The tests run the compiled builder as a separate process, the way the library's build does,
///     and judge only what is observable from outside: the exit code, the two output streams, and
///     the dictionary files that appear (or do not appear). The files are read back with the
///     independent <see cref="TldReader"/> from the test support, which restates the file format from
///     its specification. One test runs the builder over the repository's real word lists, so a
///     list that the builder would reject is found by the test suite before it breaks a build.
/// </remarks>
public class DictionaryBuilderTests
{
    /// <summary>
    ///     The dictionary names that the repository's word-list folders declare, one per folder.
    /// </summary>
    private static readonly string[] RepositoryDictionaryNames = ["en-common", "en-US", "en-GB", "technical"];

    /// <summary>
    ///     Runs the builder over a temporary input folder and returns where the output went.
    /// </summary>
    /// <param name="temp">The temporary directory holding the input folder.</param>
    /// <param name="names">The dictionary names to declare.</param>
    /// <returns>The run result.</returns>
    private static BuilderRunner.BuilderRunResult Build(TempDirectory temp, params string[] names) =>
        BuilderRunner.Run([.. new[] { "--input", temp.PathOf("input"), "--output", temp.PathOf("out") }, .. names]);

    /// <summary>
    ///     Reads a dictionary file the builder wrote.
    /// </summary>
    /// <param name="temp">The temporary directory holding the output folder.</param>
    /// <param name="name">The dictionary name.</param>
    /// <returns>The independent reader over the file.</returns>
    private static TldReader ReadOutput(TempDirectory temp, string name) =>
        new(File.ReadAllBytes(temp.PathOf($"out/{name}.tld")));

    /// <summary>
    ///     Test that each declared word-list folder is converted into its own dictionary file.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_WordListFolders_WritesOneDictionaryPerFolder()
    {
        // Arrange: two dictionaries, one of them split across two list files
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/one.txt", "apple\nbanana\n");
        temp.CreateFile("input/alpha/two.txt", "cherry\n");
        temp.CreateFile("input/beta/words.txt", "date\n");

        // Act: run the builder for both dictionaries
        var result = Build(temp, "alpha", "beta");

        // Assert: exit code 0, exactly two files, each holding the words of its own folder
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["alpha.tld", "beta.tld"], Directory.GetFiles(temp.PathOf("out")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(["apple", "banana", "cherry"], ReadOutput(temp, "alpha").Words);
        Assert.Equal(["date"], ReadOutput(temp, "beta").Words);
    }

    /// <summary>
    ///     Test that an invalid command line is rejected with the usage text and the usage exit code.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_InvalidCommandLine_ReportsUsage()
    {
        // Arrange: a command line that names no folders and no dictionaries
        using var temp = new TempDirectory();

        // Act: run the builder with only an unknown option
        var result = BuilderRunner.Run("--unknown");

        // Assert: the usage exit code, the usage text on the error stream, and no output created
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage:", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that the builder converts the repository's real word lists and that every entry can be found afterwards.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_RepositoryWordLists_BuildsAllDictionaries()
    {
        // Arrange: the repository's word-list folders, copied beside the test assembly
        using var temp = new TempDirectory();
        var listRoot = Path.Combine(AppContext.BaseDirectory, "dictionaries");

        // Act: run the builder over all four real dictionaries
        var result = BuilderRunner.Run(["--input", listRoot, "--output", temp.PathOf("out"), .. RepositoryDictionaryNames]);

        // Assert: success, and for every dictionary the file holds exactly the distinct folded entries of its lists
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        foreach (var name in RepositoryDictionaryNames)
        {
            var entries = Directory.GetFiles(Path.Combine(listRoot, name), "*.txt", SearchOption.AllDirectories)
                .SelectMany(File.ReadAllLines)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && line[0] != '#')
                .ToList();
            var reader = ReadOutput(temp, name);

            Assert.NotEmpty(entries);
            Assert.Equal(entries.Select(entry => entry.ToLowerInvariant()).Distinct().Count(), (int)reader.WordCount);

            // Every source entry is found as written, in lower case, and in upper case
            Assert.All(entries, entry =>
            {
                Assert.True(reader.TryFind(entry, out _), $"{name}: '{entry}' as written");
                Assert.True(reader.TryFind(entry.ToLowerInvariant(), out _), $"{name}: '{entry}' in lower case");
                Assert.True(reader.TryFind(entry.ToUpperInvariant(), out _), $"{name}: '{entry}' in upper case");
            });
        }

        // The language lists differ where they should: color is American, colour is British, and both are not common
        Assert.True(ReadOutput(temp, "en-US").TryFind("color", out _));
        Assert.False(ReadOutput(temp, "en-US").TryFind("colour", out _));
        Assert.True(ReadOutput(temp, "en-GB").TryFind("colour", out _));
        Assert.True(ReadOutput(temp, "en-common").TryFind("table", out _));
        Assert.True(ReadOutput(temp, "technical").TryFind("textlint", out _));
    }

    /// <summary>
    ///     Test that case variants and repeated entries are stored as a single lower-case word.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_CaseVariantsAndDuplicates_StoresEachWordOnce()
    {
        // Arrange: one word in three cases, a repeated word, and a duplicate in a second file
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/one.txt", "Paris\nPARIS\nparis\napple\napple\n");
        temp.CreateFile("input/alpha/two.txt", "Apple\n");

        // Act: run the builder
        var result = Build(temp, "alpha");

        // Assert: each word is stored once, in lower case
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["apple", "paris"], ReadOutput(temp, "alpha").Words);
    }

    /// <summary>
    ///     Test that words are stored in sorted order however the lists are ordered.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_UnsortedLists_StoresSortedWords()
    {
        // Arrange: lists in no particular order, across two files
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/one.txt", "pear\napple\nzebra\n");
        temp.CreateFile("input/alpha/two.txt", "mango\nbanana\n");

        // Act: run the builder
        var result = Build(temp, "alpha");

        // Assert: the stored words are in ordinal order
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["apple", "banana", "mango", "pear", "zebra"], ReadOutput(temp, "alpha").Words);
    }

    /// <summary>
    ///     Test that the capitalization forms of every word are recorded in the output.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_CapitalizationForms_AreRecorded()
    {
        // Arrange: words that occur lower case, capitalized, upper case, and mixed, one of them in several forms
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "color\nParis\nparis\nNASA\niPhone\n");

        // Act: run the builder
        var result = Build(temp, "alpha");

        // Assert: each word carries exactly the forms it had in the lists
        Assert.Equal(0, result.ExitCode);
        var reader = ReadOutput(temp, "alpha");
        Assert.True(reader.TryFind("color", out var color));
        Assert.True(reader.TryFind("paris", out var paris));
        Assert.True(reader.TryFind("nasa", out var nasa));
        Assert.True(reader.TryFind("iphone", out var iphone));
        Assert.Multiple(
            () => Assert.Equal(0x10000000u, color),
            () => Assert.Equal(0x30000000u, paris),
            () => Assert.Equal(0x40000000u, nasa),
            () => Assert.Equal(0x80000000u, iphone));
    }

    /// <summary>
    ///     Test that an invalid entry is reported with its file and line and that no dictionary file is written.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_InvalidEntry_ReportsFileAndLineAndWritesNothing()
    {
        // Arrange: a valid dictionary and one whose third line contains a digit
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        var badFile = temp.CreateFile("input/beta/words.txt", "date\n# comment\nfig2\n");

        // Act: run the builder for both
        var result = Build(temp, "alpha", "beta");

        // Assert: failure, the error names the file and line 3 in compiler format, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"{badFile}(3): error: ", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a declared dictionary without any words fails and is named.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_EmptyDictionary_FailsNamingDictionary()
    {
        // Arrange: a declared dictionary that holds only comments
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "# nothing here\n");

        // Act: run the builder
        var result = Build(temp, "alpha");

        // Assert: failure that names the dictionary, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("dictionary 'alpha' contains no words", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a word-list folder that is not declared fails and is named.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_UndeclaredFolder_FailsNamingFolder()
    {
        // Arrange: a folder of word lists whose name was left off the command line
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        temp.CreateFile("input/mistyped/words.txt", "pear\n");

        // Act: run the builder for the declared dictionary only
        var result = Build(temp, "alpha");

        // Assert: failure that names the undeclared folder, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("'mistyped' is not a declared dictionary", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that building the same lists twice produces byte-identical files.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_SameListsTwice_ProducesIdenticalFiles()
    {
        // Arrange: lists with enough words to exercise many slots
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", string.Join('\n', Enumerable.Range(0, 2000).Select(index => $"word{ToLetters(index)}")) + "\n");
        var first = Build(temp, "alpha");
        var firstBytes = File.ReadAllBytes(temp.PathOf("out/alpha.tld"));
        Directory.Delete(temp.PathOf("out"), recursive: true);

        // Act: build again from the same lists
        var second = Build(temp, "alpha");
        var secondBytes = File.ReadAllBytes(temp.PathOf("out/alpha.tld"));

        // Assert: both runs succeed and the files are identical
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(firstBytes, secondBytes);
    }

    /// <summary>
    ///     Test that lists with CRLF line endings produce the same files as lists with LF line endings.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_CrLfLists_ProduceSameFilesAsLf()
    {
        // Arrange: the same lists written once with LF and once with CRLF line endings
        using var lf = new TempDirectory();
        using var crLf = new TempDirectory();
        const string content = "# header\napple\nBanana\n" + "caf\u00E9\n";
        lf.CreateFile("input/alpha/words.txt", content);
        crLf.CreateFile("input/alpha/words.txt", content.Replace("\n", "\r\n", StringComparison.Ordinal));

        // Act: build both
        var lfResult = Build(lf, "alpha");
        var crLfResult = Build(crLf, "alpha");

        // Assert: both succeed and the files are identical
        Assert.Equal(0, lfResult.ExitCode);
        Assert.Equal(0, crLfResult.ExitCode);
        Assert.Equal(File.ReadAllBytes(lf.PathOf("out/alpha.tld")), File.ReadAllBytes(crLf.PathOf("out/alpha.tld")));
    }

    /// <summary>
    ///     Test that a dictionary file is left untouched when the lists have not changed.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_UnchangedLists_LeavesFilesUntouched()
    {
        // Arrange: a first build, then the output file's timestamp set to a past time
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        Build(temp, "alpha");
        var outputFile = temp.PathOf("out/alpha.tld");
        var pastTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(outputFile, pastTime);

        // Act: build again from the same lists
        var result = Build(temp, "alpha");

        // Assert: success and the file kept its old timestamp
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(pastTime, File.GetLastWriteTimeUtc(outputFile));
    }

    /// <summary>
    ///     Converts a number to a string of letters, so that generated words contain no digits.
    /// </summary>
    /// <param name="number">The number to convert. Not negative.</param>
    /// <returns>A string of letters that is different for every number.</returns>
    private static string ToLetters(int number) =>
        new(number.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (char)('a' + (digit - '0'))).ToArray());
}
