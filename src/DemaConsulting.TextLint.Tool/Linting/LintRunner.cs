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

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.Tool.Cli;

namespace DemaConsulting.TextLint.Tool.Linting;

/// <summary>
///     Runs one spell check as the command line asked for it: finds the settings, applies the command-line
///     overrides, calls the library, reports the result, and sets the exit status.
/// </summary>
/// <remarks>
///     The tool is a thin front-end over the library, so this unit contains policy and glue, not linting
///     logic: where to look for the settings file, which command-line values override which settings, and how
///     each kind of failure is reported. Everything else (reading and validating settings, interpreting
///     command-line paths, selecting files, checking text) is a library call. Every failure the user can
///     cause (a bad settings file, a bad path, a missing directory, an unreadable file) is reported as an
///     error message and a failure exit code, never as an unhandled exception; a defect, such as a damaged
///     embedded dictionary, is deliberately not caught. The class is stateless and its only side effects
///     are the output written to the context and the files the library reads.
/// </remarks>
internal static class LintRunner
{
    /// <summary>
    ///     The name of the settings file that is looked for in the root directory when none is named.
    /// </summary>
    private const string DefaultSettingsFileName = ".textlint.yaml";

    /// <summary>
    ///     What a look for the default settings file found.
    /// </summary>
    internal enum SettingsFileProbe
    {
        /// <summary>
        ///     The path exists, so it is to be loaded; whether it can be read is for the loader to say.
        /// </summary>
        Found,

        /// <summary>
        ///     Nothing exists at the path, so the default settings apply.
        /// </summary>
        NotFound,

        /// <summary>
        ///     The path could not be examined, which is an error and not a missing file.
        /// </summary>
        Failed,
    }

    /// <summary>
    ///     Runs the check that the command line describes and reports its outcome through the context.
    /// </summary>
    /// <remarks>
    ///     The library is asynchronous but the program entry point is synchronous, so the call is waited for
    ///     once, here, at the boundary of a console program that has no synchronization context to deadlock
    ///     on. Cancellation is honored throughout: cancelling the token stops the settings read, the directory
    ///     walk, and the file checks, and the run ends with a message and a failure exit code instead of a
    ///     partial result. Side effects: writes to <paramref name="context"/> and reads the settings file,
    ///     directories, and files.
    /// </remarks>
    /// <param name="context">The parsed command line and the output channels. Must not be null.</param>
    /// <param name="cancellationToken">Token that cancels the run, for example when the user presses Ctrl+C.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Propagated when an embedded dictionary is missing or damaged, which is a defect of the build and not
    ///     something the user can correct.
    /// </exception>
    public static void Run(Context context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            RunCheck(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user asked to stop, so say so and fail rather than leave a half-written result
            context.WriteError("The check was cancelled before it finished.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // These are the ways a user's input can be wrong or the file system can refuse: a missing or
            // unreadable directory, an invalid path, a bad pattern or language. Each carries a message that
            // names the cause, so it is shown as an error and the exit code reports the failure.
            context.WriteError(exception.Message);
        }
    }

    /// <summary>
    ///     Performs the steps of one check, letting expected failures propagate to <see cref="Run"/>.
    /// </summary>
    /// <remarks>
    ///     Side effects: writes to <paramref name="context"/> and reads the file system.
    /// </remarks>
    /// <param name="context">The parsed command line and the output channels. Must not be null.</param>
    /// <param name="cancellationToken">Token that cancels the run.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    /// <exception cref="ArgumentException">Thrown when a path, pattern, or language is invalid.</exception>
    /// <exception cref="IOException">Thrown when the root directory is missing or cannot be read.</exception>
    private static void RunCheck(Context context, CancellationToken cancellationToken)
    {
        // Resolve the one directory that settings discovery and relative paths are measured from
        var root = Path.GetFullPath(context.Root ?? Directory.GetCurrentDirectory());

        // Check the root before anything else uses it: it decides where the settings are looked for, and an
        // absolute command-line path must not be able to bypass a root the user named wrongly
        if (!IsRootUsable(context, root))
        {
            return;
        }

        // Find and load the settings; a problem has already been reported, and nothing is checked
        var settings = LoadSettings(context, root, cancellationToken);
        if (settings is null)
        {
            return;
        }

        // Command-line patterns replace the 'files' setting; the library turns what was typed into a
        // search directory and portable patterns, and says how to name the files it finds
        var searchRoot = root;
        Func<string, string> displayPath = static path => path;
        if (context.Globs.Count > 0)
        {
            var selection = CommandLinePathResolver.Resolve(root, context.Globs);
            settings = settings with { Files = selection.Patterns };
            searchRoot = selection.RootDirectory;
            displayPath = selection.GetDisplayPath;
        }
        else if (settings.Files.Count == 0)
        {
            // A run that was asked to check nothing must not pass by accident, so it is a usage error
            context.WriteError(
                "No files to check: give file patterns on the command line, or a 'files' setting in a settings file. " +
                "Example: textlint \"**/*.md\"");
            return;
        }

        // The language option wins over the settings, as the more specific request
        if (context.Language is not null)
        {
            settings = settings with { Language = context.Language };
        }

        // Run the check; the library default for parallelism applies unless the user chose a number
        var options = context.MaxParallelism is { } parallelism
            ? new SpellCheckOptions { MaxDegreeOfParallelism = parallelism }
            : new SpellCheckOptions();
        var result = SpellChecker.CheckAsync(settings, searchRoot, options, cancellationToken).GetAwaiter().GetResult();

        // An empty selection is almost always a mistyped pattern, so it fails unless it was allowed
        if (result.FilesChecked == 0 && result.FileDiagnostics.Count == 0 && !context.AllowEmpty)
        {
            context.WriteError(
                "No files matched the file selection, so nothing was checked. Use --allow-empty to accept an empty selection.");
            return;
        }

        // Report, and fail the run when anything the user should act on was found
        LintReporter.Write(context, result, displayPath);
        if (result.HasProblems)
        {
            context.MarkFailed();
        }
    }

    /// <summary>
    ///     Checks that the root directory exists, is a directory, and can be read, reporting why not.
    /// </summary>
    /// <remarks>
    ///     The directory is read rather than asked about with <see cref="Directory.Exists(string?)"/>, which
    ///     answers "no" for a directory the process may not read and would report it as missing. Reading one
    ///     entry lets the file system say which failure it is: only a directory-not-found failure means the
    ///     root does not exist; a path that names a file, or a directory that cannot be read, is reported with
    ///     the file system's own explanation. Side effects: writes to <paramref name="context"/> and reads
    ///     one directory entry.
    /// </remarks>
    /// <param name="context">The output channels. Must not be null.</param>
    /// <param name="root">The absolute root directory. Must not be null.</param>
    /// <returns><see langword="true"/> when the root can be used; otherwise an error was written.</returns>
    private static bool IsRootUsable(Context context, string root)
    {
        try
        {
            // The listing is lazy, so the first step is what reaches the file system
            using var entries = new DirectoryInfo(root).EnumerateFileSystemInfos().GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            context.WriteError($"The root directory '{root}' does not exist.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.WriteError($"The root directory '{root}' cannot be read: {exception.Message}");
        }

        return false;
    }

    /// <summary>
    ///     Finds and loads the settings, or reports why that was not possible.
    /// </summary>
    /// <remarks>
    ///     An explicit <c>--config</c> is loaded as typed and must exist. Otherwise the settings file in the
    ///     root directory is used when there is one, and no parent directory is searched, so a file from an
    ///     unrelated directory can never be picked up by accident. When there is none the default settings
    ///     apply and one information line says so, which is noisy only until a settings file is added. Side
    ///     effects: writes to <paramref name="context"/> and reads the settings file.
    /// </remarks>
    /// <param name="context">The parsed command line and the output channels. Must not be null.</param>
    /// <param name="root">The absolute root directory. Must not be null.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The settings, or <see langword="null"/> when a problem was reported and nothing is to be checked.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    private static TextLintSettings? LoadSettings(Context context, string root, CancellationToken cancellationToken)
    {
        // An explicit settings file is used as typed; otherwise the default file in the root is looked for
        var path = context.ConfigFile;
        if (path is null)
        {
            // Keep the path as short as the user's own arguments, so diagnostics read '.textlint.yaml(3,1)'
            path = context.Root is null ? DefaultSettingsFileName : Path.Combine(context.Root, DefaultSettingsFileName);
            var probe = ProbeSettingsFile(path, File.GetAttributes, out var problem);
            if (probe == SettingsFileProbe.Failed)
            {
                context.WriteError($"{path}: error: Cannot examine the settings file: {problem}");
                return null;
            }

            if (probe == SettingsFileProbe.NotFound)
            {
                context.WriteLine($"info: No settings file found in '{root}'; using the default settings.");
                return new TextLintSettings();
            }
        }

        // Load it; every problem is reported, because a half-understood file must never be used
        var loaded = SettingsLoader.LoadAsync(path, cancellationToken).GetAwaiter().GetResult();
        foreach (var diagnostic in loaded.Diagnostics)
        {
            context.WriteError(diagnostic.ToString());
        }

        return loaded.Settings;
    }

    /// <summary>
    ///     Finds out whether the default settings file exists, without mistaking a failure for absence.
    /// </summary>
    /// <remarks>
    ///     Asking <see cref="File.Exists(string?)"/> would answer "no" for a file the process is not allowed
    ///     to examine, and the run would quietly ignore the user's settings. Reading the attributes instead
    ///     lets the file system say which of the cases it is: only a missing file or directory means absence,
    ///     and any other failure is reported. Whether the file can then be read is for the loader to
    ///     answer. Internal test seam: the attribute read is a parameter, so tests can make the file system
    ///     fail in each way on every platform, which a real directory cannot do portably. Side effects: reads
    ///     the attributes of one path through <paramref name="getAttributes"/>.
    /// </remarks>
    /// <param name="path">The path of the default settings file. Must not be null.</param>
    /// <param name="getAttributes">Reads the attributes of a path, as <see cref="File.GetAttributes(string)"/> does. Must not be null.</param>
    /// <param name="problem">Receives the file system's explanation when the result is <see cref="SettingsFileProbe.Failed"/>.</param>
    /// <returns>What was found.</returns>
    internal static SettingsFileProbe ProbeSettingsFile(string path, Func<string, FileAttributes> getAttributes, out string problem)
    {
        problem = string.Empty;
        try
        {
            _ = getAttributes(path);
            return SettingsFileProbe.Found;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Only a path that does not exist is absence; the default settings apply
            return SettingsFileProbe.NotFound;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // Anything else means a file may be there that cannot be examined, which is the user's to fix
            problem = exception.Message;
            return SettingsFileProbe.Failed;
        }
    }
}
