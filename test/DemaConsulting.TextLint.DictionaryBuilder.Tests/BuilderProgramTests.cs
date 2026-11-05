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
///     Unit tests for the <see cref="BuilderProgram"/> class.
/// </summary>
/// <remarks>
///     The tests call <see cref="BuilderProgram.Run"/> in-process with in-memory output streams, so
///     no process is started and the console is not touched. The program's documented dependencies,
///     the word-list reader, the variant filter, and the dictionary writer, are used as they are,
///     with real temporary folders; what was written is read back with the independent
///     <see cref="TldReader"/> from the test support.
/// </remarks>
public class BuilderProgramTests
{
    /// <summary>
    ///     Runs the builder in-process and captures its exit code and both streams.
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns>The exit code, standard output text, and standard error text.</returns>
    private static (int ExitCode, string Output, string Error) Execute(params string[] arguments) =>
        ExecuteWith(null, arguments);

    /// <summary>
    ///     Runs the builder in-process with substituted file operations and captures its exit code and both streams.
    /// </summary>
    /// <param name="operations">The file operations to use; null for the real file system.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns>The exit code, standard output text, and standard error text.</returns>
    private static (int ExitCode, string Output, string Error) ExecuteWith(FileOperations? operations, params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = BuilderProgram.Run(arguments, output, error, operations);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Creates an input folder with one valid dictionary named "alpha".
    /// </summary>
    /// <param name="temp">The temporary directory to create the folder in.</param>
    /// <returns>The absolute path of the input folder.</returns>
    private static string CreateValidInput(TempDirectory temp)
    {
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        return temp.PathOf("input");
    }

    /// <summary>
    ///     Test that a command line without the input option reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_MissingInput_ReturnsUsageError()
    {
        // Arrange: a command line with an output folder and a name but no input folder
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, error) = Execute("--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 2 and the usage text on the error stream
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains("Usage:", error);
        Assert.Contains("--input", error);
    }

    /// <summary>
    ///     Test that a command line without the output option reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_MissingOutput_ReturnsUsageError()
    {
        // Arrange: a command line with an input folder and a name but no output folder
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, _) = Execute("--input", CreateValidInput(temp), "alpha");

        // Assert: exit code 2
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
    }

    /// <summary>
    ///     Test that a command line without any dictionary name reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_NoDictionaryNames_ReturnsUsageError()
    {
        // Arrange: a command line with both folders but no dictionary name
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, _) = Execute("--input", CreateValidInput(temp), "--output", temp.PathOf("out"));

        // Assert: exit code 2
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
    }

    /// <summary>
    ///     Test that an unknown option reports a usage error that names the option.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_UnknownOption_ReturnsUsageError()
    {
        // Arrange: a command line with an unrecognized option
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, error) = Execute("--input", CreateValidInput(temp), "--output", temp.PathOf("out"), "--bogus", "alpha");

        // Assert: exit code 2 and an error that names the option
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains("--bogus", error);
    }

    /// <summary>
    ///     Test that an option without a value reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_OptionWithoutValue_ReturnsUsageError()
    {
        // Arrange: a command line that ends with an option that has no value
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, error) = Execute("alpha", "--input", CreateValidInput(temp), "--output");

        // Assert: exit code 2 and an error that names the option
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains("--output", error);
    }

    /// <summary>
    ///     Test that an option given twice reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_DuplicateOption_ReturnsUsageError()
    {
        // Arrange: a command line that gives the input option twice
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);

        // Act: run the builder
        var (exitCode, _, error) = Execute("--input", input, "--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 2 and an error that says the option was repeated
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains("more than once", error);
    }

    /// <summary>
    ///     Test that a dictionary name that could leave the input folder is rejected.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_NameWithPathSeparator_ReturnsUsageError()
    {
        // Arrange: a dictionary name that tries to climb out of the input folder
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, error) = Execute("--input", CreateValidInput(temp), "--output", temp.PathOf("out"), "../alpha");

        // Assert: exit code 2 and nothing was written
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains("../alpha", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a dictionary name given twice reports a usage error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_DuplicateName_ReturnsUsageError()
    {
        // Arrange: a command line that declares the same dictionary twice
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, _) = Execute("--input", CreateValidInput(temp), "--output", temp.PathOf("out"), "alpha", "alpha");

        // Assert: exit code 2
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
    }

    /// <summary>
    ///     Test that valid lists produce one file per declared dictionary and exit code zero.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_ValidLists_WritesFilesAndReturnsZero()
    {
        // Arrange: two dictionaries with valid lists
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");

        // Act: run the builder for both
        var (exitCode, output, error) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: exit code 0, one file per dictionary, a summary line per file, and no errors
        Assert.Equal(BuilderProgram.ExitSuccess, exitCode);
        Assert.True(File.Exists(temp.PathOf("out/alpha.tld")));
        Assert.True(File.Exists(temp.PathOf("out/beta.tld")));
        Assert.Contains("alpha.tld: 2 words", output);
        Assert.Contains("beta.tld: 1 words", output);
        Assert.Equal(string.Empty, error);
    }

    /// <summary>
    ///     Test that an invalid entry is reported with file and line and nothing is written, even for valid dictionaries.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_InvalidEntry_WritesNothingAndReportsFileLine()
    {
        // Arrange: one valid dictionary and one whose second line is invalid
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        var badFile = temp.CreateFile("input/beta/words.txt", "cherry\nbad1\n");

        // Act: run the builder for both
        var (exitCode, _, error) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: exit code 1, the error points at line 2 of the file, and no output was created
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{badFile}(2): error: ", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a folder of word lists that was not declared is reported by name.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_UndeclaredFolder_ReturnsOneNamingFolder()
    {
        // Arrange: two folders with word lists but only one declared
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        temp.CreateFile("input/forgotten/words.txt", "pear\n");
        temp.CreateFile("input/notes/readme.md", "not a word list\n");

        // Act: run the builder for the declared folder only
        var (exitCode, _, error) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1, the undeclared folder is named, the folder without lists is not, and nothing was written
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("'forgotten' is not a declared dictionary", error);
        Assert.DoesNotContain("notes", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a declared dictionary whose lists contain no words is reported by name.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_EmptyDictionary_ReturnsOneNamingDictionary()
    {
        // Arrange: a declared dictionary whose only file holds comments
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "# only a comment\n\n");

        // Act: run the builder
        var (exitCode, _, error) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1 and the dictionary is named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("dictionary 'alpha' contains no words", error);
    }

    /// <summary>
    ///     Test that a declared dictionary without a folder is reported by name.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_MissingDictionaryFolder_ReturnsOneNamingDictionary()
    {
        // Arrange: an input folder that has no folder for the declared dictionary
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");

        // Act: declare an extra dictionary that has no folder
        var (exitCode, _, error) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha", "gamma");

        // Assert: exit code 1 and the missing dictionary is named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("dictionary 'gamma' does not exist", error);
    }

    /// <summary>
    ///     Test that a missing input folder is reported as an error rather than a crash.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_MissingInputFolder_ReturnsOne()
    {
        // Arrange: an input folder that does not exist
        using var temp = new TempDirectory();

        // Act: run the builder
        var (exitCode, _, error) = Execute("--input", temp.PathOf("missing"), "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1 and an error that names the dictionary
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("dictionary 'alpha' does not exist", error);
    }

    /// <summary>
    ///     Test that an output file whose content would be identical is left untouched.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_UnchangedOutput_IsNotRewritten()
    {
        // Arrange: a first run, then the output file's timestamp set to a past time
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        Execute("--input", input, "--output", temp.PathOf("out"), "alpha");
        var outputFile = temp.PathOf("out/alpha.tld");
        var pastTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(outputFile, pastTime);

        // Act: run again with the same lists
        var (exitCode, output, _) = Execute("--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: success, the file keeps its old timestamp, and the summary says it was unchanged
        Assert.Equal(BuilderProgram.ExitSuccess, exitCode);
        Assert.Equal(pastTime, File.GetLastWriteTimeUtc(outputFile));
        Assert.Contains("(unchanged)", output);
    }

    /// <summary>
    ///     Test that an output file is rewritten when a list changes.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_ChangedList_RewritesOutput()
    {
        // Arrange: a first run, then a changed list and an old timestamp on the output
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        Execute("--input", input, "--output", temp.PathOf("out"), "alpha");
        var outputFile = temp.PathOf("out/alpha.tld");
        var pastTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(outputFile, pastTime);
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\ncherry\n");

        // Act: run again
        var (exitCode, output, _) = Execute("--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: the file was written again with the new word count
        Assert.Equal(BuilderProgram.ExitSuccess, exitCode);
        Assert.NotEqual(pastTime, File.GetLastWriteTimeUtc(outputFile));
        Assert.Contains("3 words", output);
        Assert.Contains("(written)", output);
    }

    /// <summary>
    ///     Test that an output path that cannot be created is reported as an error.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_OutputPathIsAFile_ReportsErrorAndReturnsOne()
    {
        // Arrange: valid lists, and an output "folder" that is really a file
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        var blockingFile = temp.CreateFile("blocker", "x");

        // Act: run the builder with that output path
        var (exitCode, _, error) = Execute("--input", input, "--output", blockingFile, "alpha");

        // Assert: exit code 1 and an error that names the output path
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{blockingFile}: error: the dictionary files cannot be written", error);
    }

    /// <summary>
    ///     Test that a later output that cannot be written leaves an earlier, changed output untouched.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_LaterOutputBlocked_LeavesEarlierOutputUntouched()
    {
        // Arrange: a first build, a changed first list, and a folder where the second output file must go
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        var input = temp.PathOf("input");
        Execute("--input", input, "--output", temp.PathOf("out"), "alpha");
        var alphaFile = temp.PathOf("out/alpha.tld");
        var pastTime = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(alphaFile, pastTime);
        var before = File.ReadAllBytes(alphaFile);
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");
        Directory.CreateDirectory(temp.PathOf("out/beta.tld"));

        // Act: build both dictionaries
        var (exitCode, _, error) = Execute("--input", input, "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: failure, the first file has its old bytes and timestamp, and no staged or backup file remains
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("the dictionary files cannot be written", error);
        Assert.Equal(before, File.ReadAllBytes(alphaFile));
        Assert.Equal(pastTime, File.GetLastWriteTimeUtc(alphaFile));
        Assert.Equal(["alpha.tld", "beta.tld"], Directory.GetFileSystemEntries(temp.PathOf("out")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that a later output that cannot be written leaves no earlier output created.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_LaterOutputBlockedWithNoPriorFiles_CreatesNothing()
    {
        // Arrange: valid lists and a folder where the second output file must go
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");
        Directory.CreateDirectory(temp.PathOf("out/beta.tld"));

        // Act: build both dictionaries
        var (exitCode, _, _) = Execute("--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: failure and the output folder holds only the blocking folder
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Equal(["beta.tld"], Directory.GetFileSystemEntries(temp.PathOf("out")).Select(Path.GetFileName));
    }

    /// <summary>
    ///     Test that the technical dictionary is built from its general, language, and keep lists under the variant rule.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalDictionary_AppliesVariantRule()
    {
        // Arrange: the shared variant fixture
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);

        // Act: build the three dictionaries with the variant rule
        var (exitCode, output, error) = Execute(VariantFixture.Arguments(temp));

        // Assert: success, the summary counts the variants, and the technical words are the expected ones
        Assert.Equal(BuilderProgram.ExitSuccess, exitCode);
        Assert.Equal(string.Empty, error);
        Assert.Contains("2 variants kept via language lists, 1 via variant-keep.txt, 2 stripped", output);
        var technical = new TldReader(File.ReadAllBytes(temp.PathOf("out/technical.tld")));
        Assert.Equal(["color", "grey", "kubernetes", "tokenizer", "void"], technical.Words);
    }

    /// <summary>
    ///     Test that the variant dictionaries themselves are written unchanged by the rule.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalDictionary_LeavesVariantDictionariesWhole()
    {
        // Arrange: the shared variant fixture
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);

        // Act: build with the variant rule
        Execute(VariantFixture.Arguments(temp));

        // Assert: the American and British dictionaries hold all their own words
        Assert.Equal(["color", "favorite", "tokenizer"], new TldReader(File.ReadAllBytes(temp.PathOf("out/en-US.tld"))).Words);
        Assert.Equal(["colour", "grey"], new TldReader(File.ReadAllBytes(temp.PathOf("out/en-GB.tld"))).Words);
    }

    /// <summary>
    ///     Test that a technical declaration with an unusable combination of options is a usage error.
    /// </summary>
    /// <param name="extra">The extra arguments to add to a valid command line, separated by spaces.</param>
    [Theory]
    [InlineData("--technical alpha")]
    [InlineData("--variants alpha")]
    [InlineData("--technical missing --variants alpha")]
    [InlineData("--technical alpha --variants missing")]
    [InlineData("--technical alpha --variants alpha")]
    [InlineData("--technical alpha --technical alpha --variants beta")]
    public void BuilderProgram_Run_InconsistentTechnicalDeclaration_ReturnsUsageError(string extra)
    {
        // Arrange: two valid dictionaries and an inconsistent technical declaration
        using var temp = new TempDirectory();
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");
        string[] arguments = ["--input", temp.PathOf("input"), "--output", temp.PathOf("out"), "alpha", "beta", .. extra.Split(' ')];

        // Act: run the builder
        var (exitCode, _, _) = Execute(arguments);

        // Assert: exit code 2 and nothing was created
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a technical word list outside the general, language, and keep places is reported by file.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalListOutsideKnownPlaces_ReturnsOneNamingFile()
    {
        // Arrange: the fixture plus a list that sits directly in the technical folder
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var stray = temp.CreateFile("input/technical/stray.txt", "nuget\n");

        // Act: build with the variant rule
        var (exitCode, _, error) = Execute(VariantFixture.Arguments(temp));

        // Assert: exit code 1, the stray file is named, and nothing was written
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{stray}: error: the word list is not in the 'general' or 'language' folder", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a keep-list word that is not a spelling variant is reported by word.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_KeepWordNotVariant_ReturnsOneNamingWord()
    {
        // Arrange: the fixture with an ordinary word added to the keep list
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var keepFile = temp.CreateFile("input/technical/variant-keep.txt", "tokenizer\nkubernetes\n");

        // Act: build with the variant rule
        var (exitCode, _, error) = Execute(VariantFixture.Arguments(temp));

        // Assert: exit code 1 and the word and the keep list are named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{keepFile}: error: 'kubernetes' is not a spelling variant", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that a technical dictionary without language lists is reported.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalWithoutLanguageLists_ReturnsOneNamingLists()
    {
        // Arrange: the fixture with the language list removed
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        File.Delete(temp.PathOf("input/technical/language/lang.txt"));

        // Act: build with the variant rule
        var (exitCode, _, error) = Execute(VariantFixture.Arguments(temp));

        // Assert: exit code 1 and the language lists are named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("the language lists of dictionary 'technical' contains no words", error);
    }

    /// <summary>
    ///     Test that a technical dictionary without a keep list is accepted.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalWithoutKeepList_Succeeds()
    {
        // Arrange: the fixture with the keep list removed, so tokenizer is stripped
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        File.Delete(temp.PathOf("input/technical/variant-keep.txt"));

        // Act: build with the variant rule
        var (exitCode, _, _) = Execute(VariantFixture.Arguments(temp));

        // Assert: success, with tokenizer stripped as well
        Assert.Equal(BuilderProgram.ExitSuccess, exitCode);
        Assert.Equal(["color", "grey", "kubernetes", "void"], new TldReader(File.ReadAllBytes(temp.PathOf("out/technical.tld"))).Words);
    }

    /// <summary>
    ///     Test that an empty or blank option value is a usage error and creates nothing.
    /// </summary>
    /// <param name="option">The option that is given an empty value.</param>
    /// <param name="value">The empty or blank value.</param>
    [Theory]
    [InlineData("--input", "")]
    [InlineData("--output", "")]
    [InlineData("--output", "   ")]
    [InlineData("--technical", "")]
    [InlineData("--variants", " ")]
    public void BuilderProgram_Run_EmptyOptionValue_ReturnsUsageError(string option, string value)
    {
        // Arrange: valid lists, with one option given an empty value (the output folder is the one that must never default to the working folder)
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        var arguments = new List<string> { "--input", input, "--output", temp.PathOf("out"), "alpha" };
        var index = arguments.IndexOf(option);
        if (index >= 0)
        {
            arguments[index + 1] = value;
        }
        else
        {
            arguments.AddRange([option, value]);
        }

        // Act: run the builder
        var (exitCode, _, error) = Execute([.. arguments]);

        // Assert: exit code 2, the option is named, and nothing was created
        Assert.Equal(BuilderProgram.ExitUsage, exitCode);
        Assert.Contains($"{option} needs a non-empty value", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Creates two dictionaries, builds the first, and then prepares a change that needs both files written.
    /// </summary>
    /// <param name="temp">The temporary directory to work in.</param>
    /// <returns>The input folder, the path of the first output file, and the first output file's original bytes.</returns>
    private static (string Input, string AlphaFile, byte[] OriginalBytes) PrepareChangedFirstAndNewSecond(TempDirectory temp)
    {
        temp.CreateFile("input/alpha/words.txt", "apple\n");
        var input = temp.PathOf("input");
        Execute("--input", input, "--output", temp.PathOf("out"), "alpha");
        var alphaFile = temp.PathOf("out/alpha.tld");
        var original = File.ReadAllBytes(alphaFile);
        temp.CreateFile("input/alpha/words.txt", "apple\nbanana\n");
        temp.CreateFile("input/beta/words.txt", "cherry\n");
        return (input, alphaFile, original);
    }

    /// <summary>
    ///     Test that a failure while installing the second file restores the first file's previous content.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_InstallFails_RestoresPreviousFiles()
    {
        // Arrange: a changed first file and a second file whose installation fails
        using var temp = new TempDirectory();
        var (input, alphaFile, original) = PrepareChangedFirstAndNewSecond(temp);
        var operations = new FaultingFileOperations { FailMove = (source, destination) => source.EndsWith(".tmp", StringComparison.Ordinal) && destination.EndsWith("beta.tld", StringComparison.Ordinal) };

        // Act: build both dictionaries
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: an ordinary failure, the first file has its original content, and nothing else is left
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains("the dictionary files cannot be written", error);
        Assert.DoesNotContain("inconsistent", error);
        Assert.Equal(original, File.ReadAllBytes(alphaFile));
        Assert.Equal(["alpha.tld"], Directory.GetFileSystemEntries(temp.PathOf("out")).Select(Path.GetFileName));
    }

    /// <summary>
    ///     Test that a failure to restore a backup is reported as a distinct fatal condition and keeps the backup.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_RestoreFails_ReportsInconsistentOutputAndKeepsBackup()
    {
        // Arrange: the second installation fails, and the first file's backup cannot be moved back
        using var temp = new TempDirectory();
        var (input, alphaFile, original) = PrepareChangedFirstAndNewSecond(temp);
        var operations = new FaultingFileOperations
        {
            FailMove = (source, destination) =>
                (source.EndsWith(".tmp", StringComparison.Ordinal) && destination.EndsWith("beta.tld", StringComparison.Ordinal)) ||
                (source.EndsWith(".bak", StringComparison.Ordinal) && destination.EndsWith("alpha.tld", StringComparison.Ordinal))
        };

        // Act: build both dictionaries
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: the fatal exit code, the file and its backup are named, and the backup still holds the original
        Assert.Equal(BuilderProgram.ExitOutputInconsistent, exitCode);
        Assert.Contains("the output folder is inconsistent", error);
        Assert.Contains($"{alphaFile}: error: the previous content could not be restored", error);
        var backup = Assert.Single(Directory.GetFiles(temp.PathOf("out"), "*.bak"));
        Assert.Contains(backup, error);
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Empty(Directory.GetFiles(temp.PathOf("out"), "*.tmp"));
    }

    /// <summary>
    ///     Test that a failure to delete a newly installed file is reported as a distinct fatal condition and keeps the backup.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_NewFileCannotBeDeleted_ReportsInconsistentOutputAndKeepsBackup()
    {
        // Arrange: the second installation fails, and the first file's new content cannot be deleted
        using var temp = new TempDirectory();
        var (input, alphaFile, original) = PrepareChangedFirstAndNewSecond(temp);
        var operations = new FaultingFileOperations
        {
            FailMove = (source, destination) => source.EndsWith(".tmp", StringComparison.Ordinal) && destination.EndsWith("beta.tld", StringComparison.Ordinal),
            FailDelete = path => string.Equals(path, alphaFile, StringComparison.Ordinal)
        };

        // Act: build both dictionaries
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha", "beta");

        // Assert: the fatal exit code, the file holds the new content, and the backup that keeps the original is named
        Assert.Equal(BuilderProgram.ExitOutputInconsistent, exitCode);
        Assert.Contains($"{alphaFile}: error: it holds the new content and could not be deleted", error);
        var backup = Assert.Single(Directory.GetFiles(temp.PathOf("out"), "*.bak"));
        Assert.Contains(backup, error);
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.NotEqual(original, File.ReadAllBytes(alphaFile));
    }

    /// <summary>
    ///     Test that a dictionary folder whose search fails is reported by name and nothing is written.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_FolderCannotBeSearched_ReturnsOneNamingFolder()
    {
        // Arrange: valid lists whose folder search is denied
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        var folder = temp.PathOf("input/alpha");
        var operations = new FaultingFileOperations { FailEnumerateFiles = path => string.Equals(path, folder, StringComparison.Ordinal) };

        // Act: build
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1, the folder is named, and no output exists
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{folder}: error: the folder cannot be searched for word lists", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that an input folder that cannot be listed is reported and nothing is written.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_InputFolderCannotBeListed_ReturnsOne()
    {
        // Arrange: valid lists whose input folder cannot be listed
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        var operations = new FaultingFileOperations { FailEnumerateDirectories = _ => true };

        // Act: build
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1 and the input folder is named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{input}: error: the input folder cannot be listed", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }

    /// <summary>
    ///     Test that an undeclared folder that cannot be searched is reported rather than assumed empty.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_UndeclaredFolderCannotBeSearched_ReturnsOneNamingFolder()
    {
        // Arrange: a declared dictionary and another folder whose search is denied
        using var temp = new TempDirectory();
        var input = CreateValidInput(temp);
        var other = temp.PathOf("input/other");
        Directory.CreateDirectory(other);
        var operations = new FaultingFileOperations { FailEnumerateFiles = path => string.Equals(path, other, StringComparison.Ordinal) };

        // Act: build only the declared dictionary
        var (exitCode, _, error) = ExecuteWith(operations, "--input", input, "--output", temp.PathOf("out"), "alpha");

        // Assert: exit code 1 and the unsearchable folder is named
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{other}: error: the folder cannot be searched for word lists", error);
    }

    /// <summary>
    ///     Test that a technical folder whose search fails is reported and nothing is written.
    /// </summary>
    [Fact]
    public void BuilderProgram_Run_TechnicalFolderCannotBeSearched_ReturnsOneNamingFolder()
    {
        // Arrange: the shared variant fixture with the technical folder's search denied
        using var temp = new TempDirectory();
        VariantFixture.Create(temp);
        var technical = temp.PathOf("input/technical");
        var operations = new FaultingFileOperations { FailEnumerateFiles = path => string.Equals(path, technical, StringComparison.Ordinal) };

        // Act: build with the variant rule
        var (exitCode, _, error) = ExecuteWith(operations, VariantFixture.Arguments(temp));

        // Assert: exit code 1, the folder is named, and no output exists
        Assert.Equal(BuilderProgram.ExitInvalidWordLists, exitCode);
        Assert.Contains($"{technical}: error: the folder cannot be searched for word lists", error);
        Assert.False(Directory.Exists(temp.PathOf("out")));
    }
}
