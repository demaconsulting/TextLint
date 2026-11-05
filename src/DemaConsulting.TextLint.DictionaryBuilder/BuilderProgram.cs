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

namespace DemaConsulting.TextLint.DictionaryBuilder;

/// <summary>
///     The entry point of the dictionary builder: parses the command line, converts each named
///     dictionary's word lists into a binary dictionary file, and reports every problem in the
///     compiler's error format.
/// </summary>
/// <remarks>
///     The builder runs inside the build of the library, so its contract with the build is small
///     and strict: exit code 0 means every file was produced; exit code 1 means the word lists or
///     the dictionary declarations are wrong and nothing was written; exit code 2 means the command
///     line was wrong. Errors go to the error stream as <c>path(line): error: message</c>, which MSBuild
///     turns into build errors that point at the offending line. All files are produced in memory
///     before any is written, so a failure can never leave a half-updated set of dictionaries, and a
///     file whose content would not change is left untouched so that incremental builds stay
///     incremental. The class holds no state; <see cref="Run"/> is safe to call concurrently as long as
///     the callers use different output folders.
/// </remarks>
internal static class BuilderProgram
{
    /// <summary>
    ///     The exit code returned when every dictionary was produced.
    /// </summary>
    internal const int ExitSuccess = 0;

    /// <summary>
    ///     The exit code returned when the word lists or the dictionary declarations are invalid.
    /// </summary>
    internal const int ExitInvalidWordLists = 1;

    /// <summary>
    ///     The exit code returned when the command line is invalid.
    /// </summary>
    internal const int ExitUsage = 2;

    /// <summary>
    ///     The extension of a produced dictionary file.
    /// </summary>
    internal const string OutputExtension = ".tld";

    /// <summary>
    ///     The command-line option that names the folder holding one sub-folder of word lists per dictionary.
    /// </summary>
    private const string InputOption = "--input";

    /// <summary>
    ///     The command-line option that names the folder that receives the dictionary files.
    /// </summary>
    private const string OutputOption = "--output";

    /// <summary>
    ///     The prefix that marks a command-line argument as an option.
    /// </summary>
    private const string OptionPrefix = "-";

    /// <summary>
    ///     The usage text printed after a command-line error.
    /// </summary>
    private const string Usage =
        "Usage: DemaConsulting.TextLint.DictionaryBuilder --input <word-list-folder> --output <folder> <name> [<name> ...]";

    /// <summary>
    ///     The entry point of the builder process.
    /// </summary>
    /// <remarks>
    ///     A thin wrapper that connects <see cref="Run"/> to the process's console streams.
    /// </remarks>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>
    ///     Runs the builder with explicit output streams.
    /// </summary>
    /// <remarks>
    ///     Taking the streams as parameters makes the builder testable without redirecting the
    ///     process's console. Reads the word-list folders and, on success, writes the dictionary
    ///     files; writes progress to <paramref name="output"/> and errors to <paramref name="error"/>.
    /// </remarks>
    /// <param name="args">The command-line arguments. Must not be null.</param>
    /// <param name="output">The stream that receives the summary of what was produced. Must not be null.</param>
    /// <param name="error">The stream that receives error messages. Must not be null.</param>
    /// <returns><see cref="ExitSuccess"/>, <see cref="ExitInvalidWordLists"/>, or <see cref="ExitUsage"/>.</returns>
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        // Reject a malformed command line before touching the file system
        var options = ParseArguments(args, out var argumentProblem);
        if (options is null)
        {
            error.WriteLine($"DictionaryBuilder: error: {argumentProblem}");
            error.WriteLine(Usage);
            return ExitUsage;
        }

        // Read and validate every declared dictionary, collecting every problem before deciding
        var problems = new List<WordListDiagnostic>();
        var dictionaries = ReadDictionaries(options, problems);
        ReportUndeclaredFolders(options, problems);

        // Serialize in memory so that a failure can never leave a half-updated set of files
        var files = new List<(string Name, int WordCount, byte[] Content)>();
        if (problems.Count == 0)
        {
            foreach (var (name, result) in dictionaries)
            {
                try
                {
                    files.Add((name, result.Words.Count, DictionaryWriter.Write(result.Words)));
                }
                catch (InvalidOperationException exception)
                {
                    problems.Add(new WordListDiagnostic(Path.Combine(options.InputFolder, name), WordListDiagnostic.NoLine, $"dictionary '{name}' is too large: {exception.Message}"));
                }
            }
        }

        // Report all problems at once and write nothing
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                error.WriteLine(problem.ToString());
            }

            return ExitInvalidWordLists;
        }

        return WriteFiles(options, files, output, error);
    }

    /// <summary>
    ///     Parses the command line.
    /// </summary>
    /// <remarks>
    ///     Dictionary names become folder and file names, so they are restricted to letters, digits,
    ///     hyphens, and underscores: a name can therefore never climb out of the input or output
    ///     folder. Pure function; thread-safe.
    /// </remarks>
    /// <param name="args">The command-line arguments. Must not be null.</param>
    /// <param name="problem">Receives the description of the first problem, or null when the command line is valid.</param>
    /// <returns>The parsed options, or <see langword="null"/> when the command line is invalid.</returns>
    private static BuildOptions? ParseArguments(string[] args, out string? problem)
    {
        problem = null;
        string? input = null;
        string? outputFolder = null;
        var names = new List<string>();

        var index = 0;
        while (index < args.Length)
        {
            var argument = args[index++];
            if (argument is InputOption or OutputOption)
            {
                // An option needs a value that is not itself an option
                if (index >= args.Length || args[index].StartsWith(OptionPrefix, StringComparison.Ordinal))
                {
                    problem = $"the option {argument} needs a value";
                    return null;
                }

                var value = args[index++];
                if (argument == InputOption ? input is not null : outputFolder is not null)
                {
                    problem = $"the option {argument} was given more than once";
                    return null;
                }

                if (argument == InputOption)
                {
                    input = value;
                }
                else
                {
                    outputFolder = value;
                }
            }
            else if (argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                problem = $"unknown option '{argument}'";
                return null;
            }
            else if (!IsValidName(argument) || names.Contains(argument, StringComparer.Ordinal))
            {
                problem = $"'{argument}' is not a valid or unique dictionary name (use letters, digits, '-' and '_')";
                return null;
            }
            else
            {
                names.Add(argument);
            }
        }

        // All three parts are required
        if (input is null || outputFolder is null || names.Count == 0)
        {
            problem = $"{InputOption}, {OutputOption}, and at least one dictionary name are required";
            return null;
        }

        return new BuildOptions(Path.GetFullPath(input), Path.GetFullPath(outputFolder), names);
    }

    /// <summary>
    ///     Determines whether a dictionary name is a plain, safe folder and file name.
    /// </summary>
    /// <remarks>
    ///     Pure function; thread-safe.
    /// </remarks>
    /// <param name="name">The candidate name. Must not be null.</param>
    /// <returns><see langword="true"/> when the name is not empty and holds only ASCII letters, digits, hyphens, and underscores.</returns>
    private static bool IsValidName(string name) =>
        name.Length > 0 && name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    /// <summary>
    ///     Reads the word lists of every declared dictionary and records the problems found.
    /// </summary>
    /// <remarks>
    ///     A declared dictionary whose folder is missing, or whose lists contain no words, is a
    ///     misconfiguration and is reported like an invalid entry. Reads the file system.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="problems">The list that receives every problem. Must not be null.</param>
    /// <returns>The successfully read dictionaries in declaration order (only meaningful when no problem was recorded).</returns>
    private static List<(string Name, WordListReadResult Result)> ReadDictionaries(
        BuildOptions options,
        List<WordListDiagnostic> problems)
    {
        var dictionaries = new List<(string Name, WordListReadResult Result)>();
        foreach (var name in options.Names)
        {
            // A declared dictionary must have a folder
            var folder = Path.Combine(options.InputFolder, name);
            if (!Directory.Exists(folder))
            {
                problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the word-list folder of dictionary '{name}' does not exist"));
                continue;
            }

            // Collect the reader's problems, and require at least one word when the lists were readable
            var result = WordListReader.Read(folder);
            problems.AddRange(result.Diagnostics);
            if (!result.HasProblems && result.Words.Count == 0)
            {
                problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"dictionary '{name}' contains no words"));
            }

            dictionaries.Add((name, result));
        }

        return dictionaries;
    }

    /// <summary>
    ///     Reports each folder that holds word lists but was not declared on the command line.
    /// </summary>
    /// <remarks>
    ///     A mistyped or forgotten dictionary name would otherwise silently drop a whole list from
    ///     the product, which no later check could notice. Reads the file system.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="problems">The list that receives every problem. Must not be null.</param>
    private static void ReportUndeclaredFolders(BuildOptions options, List<WordListDiagnostic> problems)
    {
        // A missing input folder has no sub-folders to inspect; the declared dictionaries already reported it
        if (!Directory.Exists(options.InputFolder))
        {
            return;
        }

        var folders = Directory.EnumerateDirectories(options.InputFolder).ToList();
        folders.Sort(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);
            if (!options.Names.Contains(name, StringComparer.Ordinal) && WordListReader.FindWordListFiles(folder).Count > 0)
            {
                problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the folder holds word lists but '{name}' is not a declared dictionary"));
            }
        }
    }

    /// <summary>
    ///     Writes the dictionary files, leaving a file untouched when its content would not change.
    /// </summary>
    /// <remarks>
    ///     Not rewriting an identical file keeps its timestamp, so an incremental build that uses
    ///     the files as outputs does not see a change. File system failures are reported as errors
    ///     rather than thrown. Writes the file system.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="files">The files to write. Must not be null.</param>
    /// <param name="output">The stream that receives the summary lines. Must not be null.</param>
    /// <param name="error">The stream that receives error messages. Must not be null.</param>
    /// <returns><see cref="ExitSuccess"/> or <see cref="ExitInvalidWordLists"/>.</returns>
    private static int WriteFiles(
        BuildOptions options,
        List<(string Name, int WordCount, byte[] Content)> files,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            Directory.CreateDirectory(options.OutputFolder);
            foreach (var (name, wordCount, content) in files)
            {
                var path = Path.Combine(options.OutputFolder, name + OutputExtension);
                var unchanged = File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(content);
                if (!unchanged)
                {
                    File.WriteAllBytes(path, content);
                }

                output.WriteLine($"{path}: {wordCount} words, {content.Length} bytes ({(unchanged ? "unchanged" : "written")})");
            }

            return ExitSuccess;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{options.OutputFolder}: error: the dictionary files cannot be written: {exception.Message}");
            return ExitInvalidWordLists;
        }
    }

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="InputFolder">The absolute path of the folder holding one sub-folder of word lists per dictionary.</param>
    /// <param name="OutputFolder">The absolute path of the folder that receives the dictionary files.</param>
    /// <param name="Names">The declared dictionary names, in command-line order.</param>
    private sealed record BuildOptions(string InputFolder, string OutputFolder, IReadOnlyList<string> Names);
}
