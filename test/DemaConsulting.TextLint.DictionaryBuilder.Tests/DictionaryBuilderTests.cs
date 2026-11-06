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
        // Arrange: the repository's word-list folders and exclusions list, copied beside the test assembly
        using var temp = new TempDirectory();
        var listRoot = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var excluded = ReadEntries([RepositoryExclusionsFile(listRoot)]).Select(entry => entry.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

        // Act: run the builder over all four real dictionaries with the spelling-variant rule and the exclusions
        var result = BuilderRunner.Run(RepositoryArguments(listRoot, temp.PathOf("out")));

        // Assert: success, and every dictionary file holds exactly the distinct folded entries it should, except the excluded words
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        foreach (var name in new[] { "en-common", "en-US", "en-GB" })
        {
            var entries = ReadEntries(Directory.GetFiles(Path.Combine(listRoot, name), "*.txt", SearchOption.AllDirectories))
                .Where(entry => !excluded.Contains(entry.ToLowerInvariant())).ToList();
            var reader = ReadOutput(temp, name);

            Assert.NotEmpty(entries);
            Assert.Equal(entries.Select(entry => entry.ToLowerInvariant()).Distinct().Count(), (int)reader.WordCount);
            AssertAllFound(reader, name, entries);
        }

        // The technical dictionary holds the language and keep words and the general words that are not stripped variants
        var technical = ReadOutput(temp, "technical");
        var (kept, stripped) = ClassifyTechnicalEntries(listRoot);
        kept = [.. kept.Where(entry => !excluded.Contains(entry.ToLowerInvariant()))];
        Assert.NotEmpty(kept);
        Assert.NotEmpty(stripped);
        Assert.Equal(kept.Select(entry => entry.ToLowerInvariant()).Distinct().Count(), (int)technical.WordCount);
        AssertAllFound(technical, "technical", kept);
        Assert.All(stripped, word => Assert.False(technical.TryFind(word, out _), $"technical: '{word}' should be stripped"));

        // The language lists differ where they should: color is American, colour is British, and neither is common
        Assert.True(ReadOutput(temp, "en-US").TryFind("color", out _));
        Assert.False(ReadOutput(temp, "en-US").TryFind("colour", out _));
        Assert.True(ReadOutput(temp, "en-GB").TryFind("colour", out _));
        Assert.True(ReadOutput(temp, "en-common").TryFind("table", out _));

        // The variant rule in practice: favorite is stripped, color and grey are programming terms, tokenizer is reviewed
        Assert.False(technical.TryFind("favorite", out _));
        Assert.True(technical.TryFind("color", out _));
        Assert.True(technical.TryFind("grey", out _));
        Assert.True(technical.TryFind("tokenizer", out _));
        Assert.True(technical.TryFind("textlint", out _));
    }

    /// <summary>
    ///     Test that no word of the repository's reviewed exclusions list is known in any dictionary the builder produces.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_RepositoryExclusions_AreUnknownInEveryDictionary()
    {
        // Arrange: the repository's lists and exclusions, read independently of the builder
        using var temp = new TempDirectory();
        var listRoot = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var excluded = ReadEntries([RepositoryExclusionsFile(listRoot)]);

        // Act: run the builder over the real dictionaries
        var result = BuilderRunner.Run(RepositoryArguments(listRoot, temp.PathOf("out")));

        // Assert: success, and each excluded word is unknown, in every letter case, in all four dictionaries
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(excluded);
        foreach (var name in RepositoryDictionaryNames)
        {
            var reader = ReadOutput(temp, name);
            Assert.All(excluded, word =>
            {
                Assert.False(reader.TryFind(word.ToLowerInvariant(), out _), $"{name}: '{word}' is known");
                Assert.False(reader.TryFind(word.ToUpperInvariant(), out _), $"{name}: '{word}' is known in upper case");
            });
        }
    }

    /// <summary>
    ///     Test that an excluded word is removed from every dictionary the builder produces, whatever list it came from.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_ExcludedWord_IsUnknownInEveryDictionary()
    {
        // Arrange: the variant fixture, where tokenizer is in en-US, a general list, and the reviewed list, and an exclusions list
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var exclusions = temp.CreateFile("exclusions.txt", "tokenizer\n");

        // Act: run the builder with the exclusions
        var result = BuilderRunner.Run([.. VariantFixture.Arguments(temp), "--exclusions", exclusions]);

        // Assert: success, the word is in no dictionary, and the other words remain
        Assert.Equal(0, result.ExitCode);
        Assert.False(ReadOutput(temp, "en-US").TryFind("tokenizer", out _));
        Assert.False(ReadOutput(temp, "technical").TryFind("tokenizer", out _));
        Assert.True(ReadOutput(temp, "en-US").TryFind("favorite", out _));
        Assert.True(ReadOutput(temp, "technical").TryFind("kubernetes", out _));
    }

    /// <summary>
    ///     Test that an exclusion that no dictionary has fails the build and is named.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_ExclusionInNoDictionary_FailsNamingWord()
    {
        // Arrange: the variant fixture and an exclusions list with a word that no list has
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var exclusions = temp.CreateFile("exclusions.txt", "zebra\n");

        // Act: run the builder with the exclusions
        var result = BuilderRunner.Run([.. VariantFixture.Arguments(temp), "--exclusions", exclusions]);

        // Assert: failure that names the word and the list, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"{exclusions}: error: 'zebra' is not in any dictionary", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that an exclusions list that cannot be read fails the build and is named, instead of being treated as empty.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_ExclusionsFileMissing_FailsNamingFile()
    {
        // Arrange: the variant fixture and an exclusions option that names a file that does not exist
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var missing = temp.PathOf("missing-exclusions.txt");

        // Act: run the builder with the exclusions
        var result = BuilderRunner.Run([.. VariantFixture.Arguments(temp), "--exclusions", missing]);

        // Assert: failure that names the file, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"{missing}: error: the file cannot be read", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Gets the path of the repository's exclusions list beside the copied word lists.
    /// </summary>
    /// <param name="listRoot">The folder that holds the repository's word-list folders.</param>
    /// <returns>The path of the exclusions list.</returns>
    private static string RepositoryExclusionsFile(string listRoot) => Path.Combine(listRoot, "exclusions.txt");

    /// <summary>
    ///     Test that every word of the repository's reviewed keep list is a variant that would otherwise be stripped.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_RepositoryVariantKeepList_ContainsOnlyOtherwiseStrippedVariants()
    {
        // Arrange: the repository's lists, read independently of the builder
        var listRoot = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var variants = ReadLowerWords(Path.Combine(listRoot, "en-US"), Path.Combine(listRoot, "en-GB"));
        var language = ReadLowerWords(Path.Combine(listRoot, "technical", "language"));
        var general = ReadLowerWords(Path.Combine(listRoot, "technical", "general"));
        var keep = ReadEntries([Path.Combine(listRoot, "technical", "variant-keep.txt")]).Select(entry => entry.ToLowerInvariant()).ToList();

        // Act / Assert: each keep word is a variant, is in a general list, and is in no language list
        Assert.NotEmpty(keep);
        Assert.All(keep, word =>
        {
            Assert.True(variants.Contains(word), $"'{word}' is not a spelling variant");
            Assert.True(general.Contains(word), $"'{word}' is in no general list, so keeping it has no effect");
            Assert.False(language.Contains(word), $"'{word}' is already kept by a language list");
        });
    }

    /// <summary>
    ///     Builds the command-line arguments that convert the repository's real lists.
    /// </summary>
    /// <param name="listRoot">The folder that holds the repository's word-list folders.</param>
    /// <param name="outputFolder">The folder that receives the dictionary files.</param>
    /// <returns>The arguments for the builder.</returns>
    private static string[] RepositoryArguments(string listRoot, string outputFolder) =>
    [
        "--input", listRoot, "--output", outputFolder, .. RepositoryDictionaryNames,
        "--technical", "technical", "--variants", "en-US", "--variants", "en-GB",
        "--exclusions", RepositoryExclusionsFile(listRoot)
    ];

    /// <summary>
    ///     Reads the entries of word-list files without using the builder: trimmed, non-blank, non-comment lines.
    /// </summary>
    /// <param name="files">The files to read.</param>
    /// <returns>The entries as written, in file order.</returns>
    private static List<string> ReadEntries(IEnumerable<string> files) =>
        [.. files.SelectMany(File.ReadAllLines).Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] != '#')];

    /// <summary>
    ///     Reads the lower-cased distinct entries of every word list beneath some folders.
    /// </summary>
    /// <param name="folders">The folders to read.</param>
    /// <returns>The distinct lower-case words.</returns>
    private static HashSet<string> ReadLowerWords(params string[] folders) =>
        ReadEntries(folders.SelectMany(folder => Directory.GetFiles(folder, "*.txt", SearchOption.AllDirectories)))
            .Select(entry => entry.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    ///     Works out, independently of the builder, which technical entries the variant rule keeps and which it strips.
    /// </summary>
    /// <param name="listRoot">The folder that holds the repository's word-list folders.</param>
    /// <returns>The kept entries as written, and the lower-case words of the stripped variants.</returns>
    private static (List<string> Kept, List<string> Stripped) ClassifyTechnicalEntries(string listRoot)
    {
        var technical = Path.Combine(listRoot, "technical");
        var variants = ReadLowerWords(Path.Combine(listRoot, "en-US"), Path.Combine(listRoot, "en-GB"));
        var languageEntries = ReadEntries(Directory.GetFiles(Path.Combine(technical, "language"), "*.txt", SearchOption.AllDirectories));
        var keepEntries = ReadEntries([Path.Combine(technical, "variant-keep.txt")]);
        var exempt = languageEntries.Concat(keepEntries).Select(entry => entry.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var generalEntries = ReadEntries(Directory.GetFiles(Path.Combine(technical, "general"), "*.txt", SearchOption.AllDirectories));

        bool IsStripped(string entry) => variants.Contains(entry.ToLowerInvariant()) && !exempt.Contains(entry.ToLowerInvariant());
        var kept = languageEntries.Concat(keepEntries).Concat(generalEntries.Where(entry => !IsStripped(entry))).ToList();
        var stripped = generalEntries.Where(IsStripped).Select(entry => entry.ToLowerInvariant()).Distinct().ToList();
        return (kept, stripped);
    }

    /// <summary>
    ///     Asserts that every entry is found as written, in lower case, and in upper case.
    /// </summary>
    /// <param name="reader">The independent reader over a dictionary file.</param>
    /// <param name="name">The dictionary name, for failure messages.</param>
    /// <param name="entries">The entries that must be found.</param>
    private static void AssertAllFound(TldReader reader, string name, IEnumerable<string> entries) =>
        Assert.All(entries, entry =>
        {
            Assert.True(reader.TryFind(entry, out _), $"{name}: '{entry}' as written");
            Assert.True(reader.TryFind(entry.ToLowerInvariant(), out _), $"{name}: '{entry}' in lower case");
            Assert.True(reader.TryFind(entry.ToUpperInvariant(), out _), $"{name}: '{entry}' in upper case");
        });

    /// <summary>
    ///     Test that a general-list spelling variant is removed from the technical dictionary.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_GeneralListVariantSpelling_IsStripped()
    {
        // Arrange: the shared variant fixture, in which favorite and colour are variants in the general list only
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);

        // Act: run the builder with the variant rule
        var result = BuilderRunner.Run(VariantFixture.Arguments(temp));

        // Assert: both variants are gone and the ordinary technical word remains
        Assert.Equal(0, result.ExitCode);
        var technical = ReadOutput(temp, "technical");
        Assert.False(technical.TryFind("favorite", out _));
        Assert.False(technical.TryFind("colour", out _));
        Assert.True(technical.TryFind("kubernetes", out _));
    }

    /// <summary>
    ///     Test that a programming-language list keeps both American and British variant spellings.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_LanguageListVariantSpelling_IsKept()
    {
        // Arrange: the shared variant fixture, in which color (American) and grey (British) are in the language list
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);

        // Act: run the builder with the variant rule
        var result = BuilderRunner.Run(VariantFixture.Arguments(temp));

        // Assert: both are known in the technical dictionary
        Assert.Equal(0, result.ExitCode);
        var technical = ReadOutput(temp, "technical");
        Assert.True(technical.TryFind("color", out _));
        Assert.True(technical.TryFind("grey", out _));
    }

    /// <summary>
    ///     Test that a variant spelling in the reviewed keep list is kept.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_KeepListVariantSpelling_IsKept()
    {
        // Arrange: the shared variant fixture, in which tokenizer is a variant in the general list and the keep list
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);

        // Act: run the builder with the variant rule
        var result = BuilderRunner.Run(VariantFixture.Arguments(temp));

        // Assert: tokenizer is known in the technical dictionary
        Assert.Equal(0, result.ExitCode);
        Assert.True(ReadOutput(temp, "technical").TryFind("tokenizer", out _));
    }

    /// <summary>
    ///     Test that a keep-list word that is not a spelling variant fails the build and is named.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_KeepListWordNotVariant_FailsNamingWord()
    {
        // Arrange: the shared variant fixture with an ordinary word added to the keep list
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        temp.CreateFile("input/technical/variant-keep.txt", "tokenizer\nkubernetes\n");

        // Act: run the builder with the variant rule
        var result = BuilderRunner.Run(VariantFixture.Arguments(temp));

        // Assert: failure that names the word, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("'kubernetes' is not a spelling variant", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a technical word list outside the general, language, and keep places fails the build and is named.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_TechnicalListOutsideKnownPlaces_FailsNamingFile()
    {
        // Arrange: the shared variant fixture plus a list directly in the technical folder
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var stray = temp.CreateFile("input/technical/stray.txt", "nuget\n");

        // Act: run the builder with the variant rule
        var result = BuilderRunner.Run(VariantFixture.Arguments(temp));

        // Assert: failure that names the file, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(stray, result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
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
    ///     Test that two dictionary names that differ only in letter case are rejected rather than allowed to overwrite each other.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_NamesDifferingOnlyInCase_RejectedAsDuplicate()
    {
        // Arrange: one dictionary, declared under two names that are the same folder on a case-insensitive file system
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");

        // Act: run the builder with both spellings
        var result = Build(temp, "alpha", "ALPHA");

        // Assert: the usage exit code naming the repeated name, and nothing was written
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("'ALPHA'", result.Error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a declared dictionary whose folder does not exist fails and is named as missing.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_MissingDictionaryFolder_FailsNamingFolder()
    {
        // Arrange: one dictionary with a folder and one declared dictionary with none
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");

        // Act: run the builder for both
        var result = Build(temp, "alpha", "beta");

        // Assert: failure that names the missing folder, and nothing was written
        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"{temp.PathOf("input/beta")}: error: the word-list folder of dictionary 'beta' does not exist", result.Error);
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
    ///     Test that a dictionary file that cannot be written leaves an earlier, changed dictionary untouched.
    /// </summary>
    [Fact]
    public void DictionaryBuilder_Build_LaterOutputBlocked_LeavesEarlierOutputUntouched()
    {
        // Arrange: a first build of alpha, then changed lists and a folder where beta's output file must go
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        Build(temp, "alpha");
        var alphaFile = temp.PathOf("out/alpha.tld");
        var before = File.ReadAllBytes(alphaFile);
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");
        Directory.CreateDirectory(temp.PathOf("out/beta.tld"));

        // Act: build both dictionaries
        var result = Build(temp, "alpha", "beta");

        // Assert: failure, alpha kept its old content, and no staged or backup file was left behind
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(alphaFile));
        Assert.Equal(["alpha.tld", "beta.tld"], Directory.GetFileSystemEntries(temp.PathOf("out")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Converts a number to a string of letters, so that generated words contain no digits.
    /// </summary>
    /// <param name="number">The number to convert. Not negative.</param>
    /// <returns>A string of letters that is different for every number.</returns>
    private static string ToLetters(int number) =>
        new(number.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => (char)('a' + (digit - '0'))).ToArray());
}
