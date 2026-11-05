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
///     dictionary's word lists into a binary dictionary file, applies the spelling-variant rule to
///     the technical dictionary, and reports every problem in the compiler's error format.
/// </summary>
/// <remarks>
///     The builder runs inside the build of the library, so its contract with the build is small
///     and strict: exit code 0 means every file was produced; exit code 1 means the word lists or
///     the dictionary declarations are wrong (or the files could not be written) and no output was
///     changed; exit code 2 means the command line was wrong; exit code 3 means a write failed and the
///     file system also prevented restoring the previous files, so the output may be inconsistent (the
///     error stream then names the affected files and the backups that were kept). Errors go to the
///     error stream as <c>path(line): error: message</c>, which MSBuild turns into build errors that
///     point at the offending line. All files are produced in memory first, then staged beside their
///     targets and installed together with a rollback, so a failed write leaves the previous set of
///     dictionaries in place; only when the file system itself prevents the rollback is the result
///     inconsistent, and that is then reported as a fatal condition rather than hidden. A file whose
///     content would not change is left untouched so that incremental builds stay incremental. The
///     class holds no state; <see cref="Run"/> is safe to call concurrently as long as the callers
///     use different output folders.
/// </remarks>
internal static class BuilderProgram
{
    /// <summary>
    ///     The exit code returned when every dictionary was produced.
    /// </summary>
    internal const int ExitSuccess = 0;

    /// <summary>
    ///     The exit code returned when the word lists or the dictionary declarations are invalid, or
    ///     the output cannot be written.
    /// </summary>
    internal const int ExitInvalidWordLists = 1;

    /// <summary>
    ///     The exit code returned when the command line is invalid.
    /// </summary>
    internal const int ExitUsage = 2;

    /// <summary>
    ///     The exit code returned when writing the dictionary files failed and the file system also
    ///     prevented restoring the previous files, so the output folder may be inconsistent. The error
    ///     stream names every affected file and every backup that was kept.
    /// </summary>
    internal const int ExitOutputInconsistent = 3;

    /// <summary>
    ///     The extension of a produced dictionary file.
    /// </summary>
    internal const string OutputExtension = ".tld";

    /// <summary>
    ///     The name of the folder, inside the technical dictionary's folder, that holds the general technical lists.
    /// </summary>
    internal const string GeneralFolderName = "general";

    /// <summary>
    ///     The name of the folder, inside the technical dictionary's folder, that holds the programming-language lists.
    /// </summary>
    internal const string LanguageFolderName = "language";

    /// <summary>
    ///     The name of the reviewed list, inside the technical dictionary's folder, of spelling variants that are allowed.
    /// </summary>
    internal const string KeepListFileName = "variant-keep.txt";

    /// <summary>
    ///     The command-line option that names the folder holding one sub-folder of word lists per dictionary.
    /// </summary>
    private const string InputOption = "--input";

    /// <summary>
    ///     The command-line option that names the folder that receives the dictionary files.
    /// </summary>
    private const string OutputOption = "--output";

    /// <summary>
    ///     The command-line option that names the declared dictionary that is the technical dictionary.
    /// </summary>
    private const string TechnicalOption = "--technical";

    /// <summary>
    ///     The command-line option that names a declared dictionary whose words are spelling variants; may be repeated.
    /// </summary>
    private const string VariantsOption = "--variants";

    /// <summary>
    ///     The prefix that marks a command-line argument as an option.
    /// </summary>
    private const string OptionPrefix = "-";

    /// <summary>
    ///     The extension of a staged output file that has not been installed yet.
    /// </summary>
    private const string StagedExtension = ".tmp";

    /// <summary>
    ///     The extension of the backup of an output file that is being replaced.
    /// </summary>
    private const string BackupExtension = ".bak";

    /// <summary>
    ///     The usage text printed after a command-line error.
    /// </summary>
    private const string Usage =
        "Usage: DemaConsulting.TextLint.DictionaryBuilder --input <word-list-folder> --output <folder> <name> [<name> ...] " +
        "[--technical <name> --variants <name> [--variants <name> ...]]";

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
    ///     process's console, and the optional file operations let tests provoke file system failures
    ///     that cannot be created portably. Reads the word-list folders and, on success, writes the
    ///     dictionary files; writes progress to <paramref name="output"/> and errors to
    ///     <paramref name="error"/>.
    /// </remarks>
    /// <param name="args">The command-line arguments. Must not be null.</param>
    /// <param name="output">The stream that receives the summary of what was produced. Must not be null.</param>
    /// <param name="error">The stream that receives error messages. Must not be null.</param>
    /// <param name="operations">The file system operations to use; null for the real file system.</param>
    /// <returns>
    ///     <see cref="ExitSuccess"/>, <see cref="ExitInvalidWordLists"/>, <see cref="ExitUsage"/>, or
    ///     <see cref="ExitOutputInconsistent"/>.
    /// </returns>
    internal static int Run(string[] args, TextWriter output, TextWriter error, FileOperations? operations = null)
    {
        // Reject a malformed command line before touching the file system
        var parsed = ParseArguments(args, out var argumentProblem);
        if (parsed is null)
        {
            error.WriteLine($"DictionaryBuilder: error: {argumentProblem}");
            error.WriteLine(Usage);
            return ExitUsage;
        }

        var options = operations is null ? parsed : parsed with { Operations = operations };

        // Read and validate every declared dictionary, collecting every problem before deciding
        var problems = new List<WordListDiagnostic>();
        var dictionaries = ReadDictionaries(options, problems);
        ReportUndeclaredFolders(options, problems);

        // Serialize in memory so that a failure can never leave a half-updated set of files
        var files = new List<OutputFile>();
        if (problems.Count == 0)
        {
            foreach (var dictionary in dictionaries)
            {
                try
                {
                    files.Add(new OutputFile(dictionary.Name, dictionary.Words.Count, dictionary.Detail, DictionaryWriter.Write(dictionary.Words)));
                }
                catch (InvalidOperationException exception)
                {
                    problems.Add(new WordListDiagnostic(Path.Combine(options.InputFolder, dictionary.Name), WordListDiagnostic.NoLine, $"dictionary '{dictionary.Name}' is too large: {exception.Message}"));
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
        string? technical = null;
        var names = new List<string>();
        var variants = new List<string>();

        var index = 0;
        while (index < args.Length)
        {
            var argument = args[index++];

            // A plain argument is a dictionary name
            if (!argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                if (!IsValidName(argument) || names.Contains(argument, StringComparer.Ordinal))
                {
                    problem = $"'{argument}' is not a valid or unique dictionary name (use letters, digits, '-' and '_')";
                    return null;
                }

                names.Add(argument);
                continue;
            }

            // Every option takes one value that is present, not blank, and not itself an option
            if (argument is not (InputOption or OutputOption or TechnicalOption or VariantsOption))
            {
                problem = $"unknown option '{argument}'";
                return null;
            }

            if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                problem = $"the option {argument} needs a non-empty value";
                return null;
            }

            var value = args[index++];
            var accepted = argument switch
            {
                InputOption => AssignOnce(ref input, value),
                OutputOption => AssignOnce(ref outputFolder, value),
                TechnicalOption => AssignOnce(ref technical, value),
                _ => AddOnce(variants, value)
            };
            if (!accepted)
            {
                problem = $"the option {argument} was given more than once or with a repeated value";
                return null;
            }
        }

        // The three basic parts are required
        if (input is null || outputFolder is null || names.Count == 0)
        {
            problem = $"{InputOption}, {OutputOption}, and at least one dictionary name are required";
            return null;
        }

        // The technical declaration is all or nothing and must refer to declared dictionaries
        problem = FindTechnicalDeclarationProblem(names, technical, variants);
        return problem is null ? new BuildOptions(Path.GetFullPath(input), Path.GetFullPath(outputFolder), names, technical, variants, FileOperations.Default) : null;
    }

    /// <summary>
    ///     Sets an option value that may be given only once.
    /// </summary>
    /// <remarks>
    ///     Pure function over its arguments; thread-safe.
    /// </remarks>
    /// <param name="target">The option value so far; null until the option is seen.</param>
    /// <param name="value">The value of the option occurrence being processed.</param>
    /// <returns><see langword="true"/> when the value was stored; <see langword="false"/> when the option was already set.</returns>
    private static bool AssignOnce(ref string? target, string value)
    {
        if (target is not null)
        {
            return false;
        }

        target = value;
        return true;
    }

    /// <summary>
    ///     Adds a value to a repeatable option's list unless it is already there.
    /// </summary>
    /// <remarks>
    ///     Pure function over its arguments; thread-safe.
    /// </remarks>
    /// <param name="values">The values so far. Must not be null.</param>
    /// <param name="value">The value of the option occurrence being processed.</param>
    /// <returns><see langword="true"/> when the value was added; <see langword="false"/> when it was a repeat.</returns>
    private static bool AddOnce(List<string> values, string value)
    {
        if (values.Contains(value, StringComparer.Ordinal))
        {
            return false;
        }

        values.Add(value);
        return true;
    }

    /// <summary>
    ///     Checks that the technical declaration is complete and refers to declared dictionaries.
    /// </summary>
    /// <remarks>
    ///     The technical dictionary and the dictionaries of spelling variants only make sense
    ///     together: the variant rule needs both, and each must be one of the declared dictionaries
    ///     (the technical dictionary cannot be its own variant source). Pure function; thread-safe.
    /// </remarks>
    /// <param name="names">The declared dictionary names. Must not be null.</param>
    /// <param name="technical">The technical dictionary name, or null.</param>
    /// <param name="variants">The variant dictionary names. Must not be null.</param>
    /// <returns>A description of the first problem, or <see langword="null"/> when the declaration is valid.</returns>
    private static string? FindTechnicalDeclarationProblem(List<string> names, string? technical, List<string> variants)
    {
        if (technical is null)
        {
            return variants.Count > 0 ? $"{VariantsOption} needs {TechnicalOption}" : null;
        }

        if (!names.Contains(technical, StringComparer.Ordinal))
        {
            return $"the technical dictionary '{technical}' is not a declared dictionary name";
        }

        if (variants.Count == 0)
        {
            return $"{TechnicalOption} needs at least one {VariantsOption}";
        }

        var invalid = variants.FirstOrDefault(variant => string.Equals(variant, technical, StringComparison.Ordinal) || !names.Contains(variant, StringComparer.Ordinal));
        return invalid is null ? null : $"the variant dictionary '{invalid}' must be a declared dictionary name other than the technical dictionary";
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
    ///     misconfiguration and is reported like an invalid entry. The technical dictionary is read
    ///     last because the variant rule needs the words of the variant dictionaries. Reads the file
    ///     system.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="problems">The list that receives every problem. Must not be null.</param>
    /// <returns>The successfully read dictionaries in declaration order (only meaningful when no problem was recorded).</returns>
    private static List<BuiltDictionary> ReadDictionaries(BuildOptions options, List<WordListDiagnostic> problems)
    {
        // Read every ordinary dictionary first
        var read = new Dictionary<string, BuiltDictionary>(StringComparer.Ordinal);
        foreach (var name in options.Names.Where(name => !IsTechnical(options, name)))
        {
            // A declared dictionary must have a folder
            var folder = Path.Combine(options.InputFolder, name);
            if (!Directory.Exists(folder))
            {
                problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the word-list folder of dictionary '{name}' does not exist"));
                continue;
            }

            // Collect the reader's problems, and require at least one word when the lists were readable
            var result = WordListReader.Read(folder, options.Operations);
            problems.AddRange(result.Diagnostics);
            ReportMissingWords(result, folder, $"dictionary '{name}'", problems);
            read[name] = new BuiltDictionary(name, result.Words, string.Empty);
        }

        // Then combine the technical dictionary, which depends on the variant dictionaries' words
        if (options.TechnicalName is not null)
        {
            var folder = Path.Combine(options.InputFolder, options.TechnicalName);
            var variantWords = options.VariantNames
                .Where(read.ContainsKey)
                .SelectMany(variant => read[variant].Words.Keys)
                .ToHashSet(StringComparer.Ordinal);
            var technical = ReadTechnicalDictionary(options.TechnicalName, folder, variantWords, options.Operations, problems);
            if (technical is not null)
            {
                read[options.TechnicalName] = technical;
            }
        }

        return [.. options.Names.Where(read.ContainsKey).Select(name => read[name])];
    }

    /// <summary>
    ///     Determines whether a declared dictionary is the technical dictionary.
    /// </summary>
    /// <remarks>
    ///     Pure function; thread-safe.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="name">The dictionary name. Must not be null.</param>
    /// <returns><see langword="true"/> when the name is the declared technical dictionary.</returns>
    private static bool IsTechnical(BuildOptions options, string name) =>
        string.Equals(options.TechnicalName, name, StringComparison.Ordinal);

    /// <summary>
    ///     Records a problem when the lists of a dictionary were readable but contain no words.
    /// </summary>
    /// <remarks>
    ///     An empty dictionary would ship silently and no later check could notice it. Pure
    ///     function over its arguments apart from adding to the list.
    /// </remarks>
    /// <param name="result">The read result to check. Must not be null.</param>
    /// <param name="folder">The folder the lists were read from, named by the problem. Must not be null.</param>
    /// <param name="description">How the problem names the dictionary or list set. Must not be null.</param>
    /// <param name="problems">The list that receives the problem. Must not be null.</param>
    private static void ReportMissingWords(WordListReadResult result, string folder, string description, List<WordListDiagnostic> problems)
    {
        if (!result.HasProblems && result.Words.Count == 0)
        {
            problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"{description} contains no words"));
        }
    }

    /// <summary>
    ///     Reads the technical dictionary from its general lists, language lists, and keep list, applying the variant rule.
    /// </summary>
    /// <remarks>
    ///     The folder is the tag: the lists under <c>language</c> are programming-language keyword and
    ///     API lists, the lists under <c>general</c> are everything else, and <c>variant-keep.txt</c> is
    ///     a reviewed list of variant spellings that are programming terms. A word list anywhere else
    ///     in the folder is reported, so a misplaced list cannot silently drop out of the product. The
    ///     rule itself is applied by <see cref="VariantFilter"/>. Reads the file system.
    /// </remarks>
    /// <param name="name">The technical dictionary name. Must not be null.</param>
    /// <param name="folder">The technical dictionary folder. Must not be null.</param>
    /// <param name="variantWords">The folded words of the variant dictionaries. Must not be null.</param>
    /// <param name="operations">The file system operations to use. Must not be null.</param>
    /// <param name="problems">The list that receives every problem. Must not be null.</param>
    /// <returns>The combined dictionary, or <see langword="null"/> when a problem prevented it.</returns>
    private static BuiltDictionary? ReadTechnicalDictionary(string name, string folder, HashSet<string> variantWords, FileOperations operations, List<WordListDiagnostic> problems)
    {
        if (!Directory.Exists(folder))
        {
            problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the word-list folder of dictionary '{name}' does not exist"));
            return null;
        }

        var generalFolder = Path.Combine(folder, GeneralFolderName);
        var languageFolder = Path.Combine(folder, LanguageFolderName);
        var keepFile = Path.Combine(folder, KeepListFileName);
        var problemsBefore = problems.Count;

        // Every list must be in one of the three places the rule knows about
        var allFiles = WordListReader.FindWordListFiles(folder, out var searchProblem, operations);
        if (searchProblem is not null)
        {
            problems.Add(searchProblem);
        }

        foreach (var file in allFiles.Where(file => !IsUnder(file, generalFolder) && !IsUnder(file, languageFolder) && !string.Equals(file, keepFile, StringComparison.Ordinal)))
        {
            problems.Add(new WordListDiagnostic(file, WordListDiagnostic.NoLine, $"the word list is not in the '{GeneralFolderName}' or '{LanguageFolderName}' folder and is not {KeepListFileName}"));
        }

        // Read the three sources; the general and language lists must each hold words, the keep list is optional
        var general = WordListReader.Read(generalFolder, operations);
        var language = WordListReader.Read(languageFolder, operations);
        var keep = WordListReader.ReadFiles(File.Exists(keepFile) ? [keepFile] : [], keepFile);
        problems.AddRange(general.Diagnostics);
        problems.AddRange(language.Diagnostics);
        problems.AddRange(keep.Diagnostics);
        ReportMissingWords(general, generalFolder, $"the general lists of dictionary '{name}'", problems);
        ReportMissingWords(language, languageFolder, $"the language lists of dictionary '{name}'", problems);
        if (problems.Count > problemsBefore)
        {
            return null;
        }

        // Apply the variant rule, and report keep-list words that are not variants so the list stays tight
        var filtered = VariantFilter.Apply(general.Words, language.Words, keep.Words, variantWords);
        foreach (var word in filtered.KeepWordsNotVariants)
        {
            problems.Add(new WordListDiagnostic(keepFile, WordListDiagnostic.NoLine, $"'{word}' is not a spelling variant, so it does not belong in the keep list"));
        }

        var detail = $" ({filtered.KeptViaLanguage} variants kept via language lists, {filtered.KeptViaKeepList} via {KeepListFileName}, {filtered.Stripped} stripped)";
        return problems.Count > problemsBefore ? null : new BuiltDictionary(name, filtered.Words, detail);
    }

    /// <summary>
    ///     Determines whether a file lies beneath a folder.
    /// </summary>
    /// <remarks>
    ///     Compares full paths in ordinal order with the directory separator, so a folder whose name merely
    ///     starts with another folder's name is not mistaken for a child. Pure function; thread-safe.
    /// </remarks>
    /// <param name="file">The full path of the file. Must not be null.</param>
    /// <param name="folder">The full path of the folder. Must not be null.</param>
    /// <returns><see langword="true"/> when the file is inside the folder or one of its sub-folders.</returns>
    private static bool IsUnder(string file, string folder) =>
        file.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal);

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

        // List the folders; a listing failure is a problem, not a crash and not an empty input
        List<string> folders;
        try
        {
            folders = [.. options.Operations.EnumerateDirectories(options.InputFolder)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(new WordListDiagnostic(options.InputFolder, WordListDiagnostic.NoLine, $"the input folder cannot be listed: {exception.Message}"));
            return;
        }

        folders.Sort(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);
            if (options.Names.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            // An undeclared folder is a problem when it holds word lists, or when it cannot be searched to find out
            var lists = WordListReader.FindWordListFiles(folder, out var searchProblem, options.Operations);
            if (searchProblem is not null)
            {
                problems.Add(searchProblem);
            }
            else if (lists.Count > 0)
            {
                problems.Add(new WordListDiagnostic(folder, WordListDiagnostic.NoLine, $"the folder holds word lists but '{name}' is not a declared dictionary"));
            }
        }
    }

    /// <summary>
    ///     Writes the dictionary files all-or-nothing, leaving a file untouched when its content would not change.
    /// </summary>
    /// <remarks>
    ///     First a target path that is occupied by a folder is refused before anything is written.
    ///     Then every changed file is written to a staged file beside its target; only when all of
    ///     them are written are they installed, each by moving the old file aside and the staged file
    ///     into place. If any step fails, the files already installed are removed or restored from
    ///     their backups and the staged files are deleted, so the output folder ends as it began. If
    ///     the file system also prevents that restoration, the run reports a fatal condition with its
    ///     own exit code, names every file left inconsistent and every backup that was kept, and
    ///     deletes none of those backups. Not
    ///     rewriting an identical file keeps its timestamp, so an incremental build that uses the
    ///     files as outputs does not see a change. File system failures are reported as errors rather
    ///     than thrown. Writes the file system.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="files">The files to write. Must not be null.</param>
    /// <param name="output">The stream that receives the summary lines. Must not be null.</param>
    /// <param name="error">The stream that receives error messages. Must not be null.</param>
    /// <returns><see cref="ExitSuccess"/>, <see cref="ExitInvalidWordLists"/>, or <see cref="ExitOutputInconsistent"/>.</returns>
    private static int WriteFiles(BuildOptions options, List<OutputFile> files, TextWriter output, TextWriter error)
    {
        var staged = new List<StagedFile>();
        try
        {
            Directory.CreateDirectory(options.OutputFolder);

            // Refuse up front when a target path is occupied by a folder, which no replacement could fix
            var occupied = files.FirstOrDefault(file => Directory.Exists(TargetPath(options, file)));
            if (occupied is not null)
            {
                throw new IOException($"'{TargetPath(options, occupied)}' is a folder");
            }

            // Stage every changed file beside its target; nothing is installed until all are written
            var unchanged = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var path = TargetPath(options, file);
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Content))
                {
                    unchanged.Add(path);
                    continue;
                }

                var stagedFile = new StagedFile(path, options.Operations);
                staged.Add(stagedFile);
                File.WriteAllBytes(stagedFile.StagedPath, file.Content);
            }

            // Install them together; a failure is rolled back, and a rollback that fails is a fatal report
            var failure = Install(staged, out var inconsistencies);
            if (inconsistencies.Count > 0)
            {
                error.WriteLine($"{options.OutputFolder}: error: the dictionary files cannot be written ({failure?.Message}) and the previous files could not be restored; the output folder is inconsistent");
                foreach (var inconsistency in inconsistencies)
                {
                    error.WriteLine(inconsistency);
                }

                return ExitOutputInconsistent;
            }

            if (failure is not null)
            {
                error.WriteLine($"{options.OutputFolder}: error: the dictionary files cannot be written: {failure.Message}");
                return ExitInvalidWordLists;
            }

            // Report what happened to each file
            foreach (var file in files)
            {
                var path = TargetPath(options, file);
                output.WriteLine($"{path}: {file.WordCount} words{file.Detail}, {file.Content.Length} bytes ({(unchanged.Contains(path) ? "unchanged" : "written")})");
            }

            return ExitSuccess;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{options.OutputFolder}: error: the dictionary files cannot be written: {exception.Message}");
            return ExitInvalidWordLists;
        }
        finally
        {
            // Whatever happened, no staged file may be left behind
            foreach (var stagedFile in staged)
            {
                stagedFile.DeleteStaged();
            }
        }
    }

    /// <summary>
    ///     Gets the full path of an output file.
    /// </summary>
    /// <remarks>
    ///     Pure function; thread-safe.
    /// </remarks>
    /// <param name="options">The parsed options. Must not be null.</param>
    /// <param name="file">The output file. Must not be null.</param>
    /// <returns>The path of the dictionary file in the output folder.</returns>
    private static string TargetPath(BuildOptions options, OutputFile file) =>
        Path.Combine(options.OutputFolder, file.Name + OutputExtension);

    /// <summary>
    ///     Installs the staged files, restoring the previous state if any installation fails.
    /// </summary>
    /// <remarks>
    ///     Each existing target is moved aside to a backup before the staged file takes its place.
    ///     On failure the files touched so far are put back in reverse order (the new file is deleted
    ///     and the backup restored). If the file system prevents a restoration, the description of
    ///     what is left behind, including where the backup remains, is returned in
    ///     <paramref name="inconsistencies"/> and that backup is never deleted. The backups are
    ///     deleted on success, on a best-effort basis, because a leftover backup is harmless and must
    ///     not turn a completed installation into a reported failure. An exception that is not a file
    ///     system failure is rethrown after a clean rollback. Writes the file system.
    /// </remarks>
    /// <param name="staged">The staged files to install. Must not be null.</param>
    /// <param name="inconsistencies">Receives one description per file that could not be restored; empty when the previous state is intact.</param>
    /// <returns>The failure that stopped the installation, or <see langword="null"/> when every file was installed.</returns>
    private static Exception? Install(List<StagedFile> staged, out List<string> inconsistencies)
    {
        inconsistencies = [];
        try
        {
            foreach (var file in staged)
            {
                file.MoveIntoPlace();
            }
        }
        catch (Exception exception)
        {
            // Put the output folder back as it was, and remember every file that could not be put back
            for (var i = staged.Count - 1; i >= 0; i--)
            {
                var unresolved = staged[i].Rollback();
                if (unresolved is not null)
                {
                    inconsistencies.Add(unresolved);
                }
            }

            if (inconsistencies.Count == 0 && exception is not (IOException or UnauthorizedAccessException))
            {
                throw;
            }

            return exception;
        }

        foreach (var file in staged)
        {
            file.DeleteBackup();
        }

        return null;
    }

    /// <summary>
    ///     A dictionary that has been read and combined, ready to be serialized.
    /// </summary>
    /// <param name="Name">The dictionary name.</param>
    /// <param name="Words">The distinct folded words with their capitalization-form flags.</param>
    /// <param name="Detail">Extra text for the summary line (the variant counts of the technical dictionary), or empty.</param>
    private sealed record BuiltDictionary(string Name, IReadOnlyDictionary<string, uint> Words, string Detail);

    /// <summary>
    ///     A serialized dictionary file waiting to be written.
    /// </summary>
    /// <param name="Name">The dictionary name.</param>
    /// <param name="WordCount">The number of words in the file.</param>
    /// <param name="Detail">Extra text for the summary line, or empty.</param>
    /// <param name="Content">The complete file content.</param>
    private sealed record OutputFile(string Name, int WordCount, string Detail, byte[] Content);

    /// <summary>
    ///     The parsed command line.
    /// </summary>
    /// <param name="InputFolder">The absolute path of the folder holding one sub-folder of word lists per dictionary.</param>
    /// <param name="OutputFolder">The absolute path of the folder that receives the dictionary files.</param>
    /// <param name="Names">The declared dictionary names, in command-line order.</param>
    /// <param name="TechnicalName">The declared dictionary that is the technical dictionary, or null.</param>
    /// <param name="VariantNames">The declared dictionaries whose words are spelling variants; empty when there is no technical dictionary.</param>
    /// <param name="Operations">The file system operations to use.</param>
    private sealed record BuildOptions(
        string InputFolder,
        string OutputFolder,
        IReadOnlyList<string> Names,
        string? TechnicalName,
        IReadOnlyList<string> VariantNames,
        FileOperations Operations);

    /// <summary>
    ///     An output file that is written beside its target and then installed in place of it.
    /// </summary>
    /// <remarks>
    ///     Keeps the three paths of one replacement together (target, staged file, backup) and
    ///     remembers how far the installation got, so a rollback can undo exactly what was done. Not
    ///     thread-safe; one instance belongs to one write.
    /// </remarks>
    private sealed class StagedFile
    {
        /// <summary>
        ///     The path of the backup of the original file while it is being replaced.
        /// </summary>
        private readonly string _backupPath;

        /// <summary>
        ///     The file system operations to use.
        /// </summary>
        private readonly FileOperations _operations;

        /// <summary>
        ///     The path of the target file.
        /// </summary>
        private readonly string _targetPath;

        /// <summary>
        ///     Whether the original file was moved aside to the backup path.
        /// </summary>
        private bool _backedUp;

        /// <summary>
        ///     Whether the staged file was moved onto the target path.
        /// </summary>
        private bool _installed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="StagedFile"/> class.
        /// </summary>
        /// <remarks>
        ///     A random token in the staged and backup names keeps concurrent builds and stale
        ///     leftovers from colliding.
        /// </remarks>
        /// <param name="targetPath">The full path of the file to replace. Must not be null.</param>
        /// <param name="operations">The file system operations to use. Must not be null.</param>
        public StagedFile(string targetPath, FileOperations operations)
        {
            var token = Guid.NewGuid().ToString("N");
            _targetPath = targetPath;
            _operations = operations;
            StagedPath = $"{targetPath}.{token}{StagedExtension}";
            _backupPath = $"{targetPath}.{token}{BackupExtension}";
        }

        /// <summary>
        ///     Gets the path the new content is written to before installation.
        /// </summary>
        public string StagedPath { get; }

        /// <summary>
        ///     Moves the original (if any) aside and the staged file into place.
        /// </summary>
        public void MoveIntoPlace()
        {
            if (File.Exists(_targetPath))
            {
                _operations.Move(_targetPath, _backupPath);
                _backedUp = true;
            }

            _operations.Move(StagedPath, _targetPath);
            _installed = true;
        }

        /// <summary>
        ///     Undoes whatever part of the installation was done, and says so when it cannot.
        /// </summary>
        /// <remarks>
        ///     Never throws. When the new file cannot be deleted the original cannot be put back
        ///     either, so it stays in its backup; when the original cannot be moved back it also stays
        ///     in its backup. In both cases the backup is kept and named in the returned description.
        /// </remarks>
        /// <returns>A description of the file's inconsistent state in the compiler's error format, or <see langword="null"/> when the previous state was restored.</returns>
        public string? Rollback()
        {
            if (_installed)
            {
                try
                {
                    _operations.Delete(_targetPath);
                    _installed = false;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    return _backedUp
                        ? $"{_targetPath}: error: it holds the new content and could not be deleted ({exception.Message}); the previous content is kept in the backup '{_backupPath}'"
                        : $"{_targetPath}: error: it holds the new content and could not be deleted ({exception.Message}); it did not exist before";
                }
            }

            if (!_backedUp)
            {
                return null;
            }

            try
            {
                _operations.Move(_backupPath, _targetPath);
                _backedUp = false;
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return $"{_targetPath}: error: the previous content could not be restored ({exception.Message}); it is kept in the backup '{_backupPath}'";
            }
        }

        /// <summary>
        ///     Deletes the backup of the original; best effort, never throws.
        /// </summary>
        public void DeleteBackup() => DeleteQuietly(_backupPath);

        /// <summary>
        ///     Deletes the staged file if it is still there; best effort, never throws.
        /// </summary>
        public void DeleteStaged() => DeleteQuietly(StagedPath);

        /// <summary>
        ///     Deletes a file, ignoring file system errors.
        /// </summary>
        /// <param name="path">The file to delete. Must not be null.</param>
        private void DeleteQuietly(string path)
        {
            try
            {
                _operations.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temporary file is harmless and must not hide the real outcome
            }
        }
    }
}
