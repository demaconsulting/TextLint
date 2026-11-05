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

using DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="BuilderProgram"/> class.
/// </summary>
/// <remarks>
///     The tests call <see cref="BuilderProgram.Run"/> in-process with in-memory output streams, so
///     no process is started and the console is not touched. The program's documented dependencies,
///     the word-list reader and the dictionary writer, are used as they are, with real temporary
///     folders.
/// </remarks>
public class BuilderProgramTests
{
    /// <summary>
    ///     Runs the builder in-process and captures its exit code and both streams.
    /// </summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <returns>The exit code, standard output text, and standard error text.</returns>
    private static (int ExitCode, string Output, string Error) Execute(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = BuilderProgram.Run(arguments, output, error);
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
}
