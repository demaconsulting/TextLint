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

using System.Text.RegularExpressions;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     Checks the spelling of every file that the settings select under a directory, using all processor
///     cores, and returns one deterministic result.
/// </summary>
/// <remarks>
///     This is the library's top-level operation: select the files, read each one, check its text, and
///     collect everything. Files are independent, so they are checked in parallel; every file's outcome
///     goes into its own slot of an array indexed by the file's position in the (ordinal-sorted) selection,
///     so no lock is needed and the result is the same, in the same order, whatever the degree of
///     parallelism. Problems with a single file (unreadable, binary, too large, timed out) are data in the
///     result and never stop the run; problems with the arguments or the selection (a missing root
///     directory, an invalid pattern) and defects are exceptions, and so is cancellation. The shared
///     state is only the immutable <see cref="TextChecker"/>. The class is stateless and safe to call from
///     several threads at once.
/// </remarks>
public static class SpellChecker
{
    /// <summary>
    ///     Selects the files the settings name under a directory and checks the spelling of each.
    /// </summary>
    /// <remarks>
    ///     The settings are validated before any file is touched, so a bad language or word-list entry fails
    ///     the call at once. File selection is synchronous in the library and runs on a thread-pool thread,
    ///     so the call does not block its caller while it walks the directory tree. Memory use is bounded by
    ///     the number of concurrent checks times the size of a file and its text. Side effects: reads
    ///     directories and files beneath <paramref name="rootDirectory"/>; nothing is written. The first
    ///     lookup loads the language's dictionaries. Cancellation is honored during the directory walk, between
    ///     the files being read, and while files are read; a cancelled call throws and returns no partial result.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings that say what to check and how. Must not be null.</param>
    /// <param name="rootDirectory">The directory the file patterns are relative to. Must name a readable directory.</param>
    /// <param name="options">How to run; null means the defaults.</param>
    /// <param name="cancellationToken">Token that cancels the run, including the directory walk.</param>
    /// <returns>
    ///     The result, ordered by file path (ordinal), then line, then column; never null. Files that could
    ///     not be checked completely are listed in its file diagnostics.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="rootDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="rootDirectory"/> is empty or white space, or when the language or a
    ///     word-list entry of <paramref name="settings"/> is not usable.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="rootDirectory"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the process may not read <paramref name="rootDirectory"/>; it is reported as such, not as missing.
    /// </exception>
    /// <exception cref="IOException">Thrown when <paramref name="rootDirectory"/> cannot be read for another reason.</exception>
    /// <exception cref="InvalidGlobPatternException">Thrown when a pattern of <paramref name="settings"/> is unsupported or malformed.</exception>
    /// <exception cref="InvalidDataException">Thrown when an embedded dictionary is missing or damaged, which indicates a defective build.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<SpellCheckResult> CheckAsync(
        TextLintSettings settings,
        string rootDirectory,
        SpellCheckOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Reject a bad directory argument before touching the file system; the settings are validated by the
        // shared implementation, which also does so before any file is touched
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        // The token is handed to the selector so that canceling stops a long directory walk and not only the checks after it
        return await CheckWithSelectionAsync(
            settings,
            token => FileSelector.Select(rootDirectory, settings.Files, settings.IgnorePaths, settings.EnableGlobDot, token),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Selects the files with the given function and checks the spelling of each.
    /// </summary>
    /// <remarks>
    ///     Internal test seam: the selection is a parameter, so tests can prove that the caller's token reaches the
    ///     file selection, which a real directory cannot show deterministically. The public method passes the
    ///     file selector. The selection runs on a thread-pool thread, because it is synchronous and may take long.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings that say what to check and how. Must not be null.</param>
    /// <param name="selectFiles">Selects the files, given the token it must honor. Must not be null.</param>
    /// <param name="options">How to run; null means the defaults.</param>
    /// <param name="cancellationToken">Token that cancels the run, including the selection.</param>
    /// <param name="matchTimeout">
    ///     The longest time one pattern may spend on one text; null means the production limit of 5 seconds.
    ///     A second internal seam, so that tests can prove the time-out behavior in milliseconds.
    /// </param>
    /// <returns>The result, ordered by file path (ordinal), then line, then column.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="selectFiles"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the language or a word-list entry of the settings is not usable.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static async Task<SpellCheckResult> CheckWithSelectionAsync(
        TextLintSettings settings,
        Func<CancellationToken, IReadOnlyList<SelectedFile>> selectFiles,
        SpellCheckOptions? options,
        CancellationToken cancellationToken,
        TimeSpan? matchTimeout = null)
    {
        // Reject unusable settings before touching the file system
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(selectFiles);
        var checker = matchTimeout is { } limit ? new TextChecker(settings, limit) : new TextChecker(settings);

        // Select the files off the caller's thread; the selection's exceptions (missing root, bad pattern) propagate
        var files = await Task.Run(() => selectFiles(cancellationToken), cancellationToken).ConfigureAwait(false);

        return await CheckFilesAsync(files, settings.MaxFileSize, checker.Check, options ?? new SpellCheckOptions(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks the given files in parallel and assembles the deterministic result.
    /// </summary>
    /// <remarks>
    ///     Internal test seam: the function that checks a text is a parameter, so tests can prove how a timeout
    ///     or a defect in checking is handled without needing hostile input. The public method passes the
    ///     checker's own method. Side effects: reads the files. Thread-safe.
    /// </remarks>
    /// <param name="files">The files to check, in the order the result is to follow. Must not be null.</param>
    /// <param name="maxFileSize">The largest file size in bytes to read; 0 means no limit.</param>
    /// <param name="checkText">Checks a text given its source name and returns its findings. Must not be null.</param>
    /// <param name="options">How to run. Must not be null.</param>
    /// <param name="cancellationToken">Token that cancels the run.</param>
    /// <returns>The result, ordered by the order of <paramref name="files"/> and then position.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static async Task<SpellCheckResult> CheckFilesAsync(
        IReadOnlyList<SelectedFile> files,
        long maxFileSize,
        Func<string, string?, IReadOnlyList<SpellingIssue>> checkText,
        SpellCheckOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(checkText);
        ArgumentNullException.ThrowIfNull(options);

        // Each file's outcome goes into its own slot, so workers share nothing mutable and need no lock
        var outcomes = new FileOutcome[files.Count];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            parallelOptions,
            async (index, token) => outcomes[index] = await CheckFileAsync(files[index], maxFileSize, checkText, token).ConfigureAwait(false))
            .ConfigureAwait(false);

        // Assemble in file order, so the result does not depend on the order the workers finished
        return new SpellCheckResult(
            outcomes.SelectMany(static outcome => outcome.Issues),
            outcomes.Where(static outcome => outcome.Diagnostic is not null).Select(static outcome => outcome.Diagnostic!),
            outcomes.Count(static outcome => outcome.Checked),
            outcomes.Count(static outcome => outcome.Diagnostic is { IsError: false }));
    }

    /// <summary>
    ///     Reads and checks one file, turning its expected problems into a diagnostic.
    /// </summary>
    /// <remarks>
    ///     A file that cannot be read, is binary, or is too large yields a diagnostic from the reader; a file
    ///     whose checking exceeds a pattern's time limit yields a timed-out diagnostic. Any other exception is
    ///     a defect and propagates, so it is never mistaken for a clean file. Side effects: reads the file.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="file">The file to check. Must not be null.</param>
    /// <param name="maxFileSize">The largest file size in bytes to read; 0 means no limit.</param>
    /// <param name="checkText">Checks a text given its source name. Must not be null.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The outcome for the file.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    private static async Task<FileOutcome> CheckFileAsync(
        SelectedFile file,
        long maxFileSize,
        Func<string, string?, IReadOnlyList<SpellingIssue>> checkText,
        CancellationToken cancellationToken)
    {
        // Read the file; a problem with the file is the outcome, not an exception
        var content = await FileTextReader.ReadAsync(file.FullPath, maxFileSize, cancellationToken).ConfigureAwait(false);
        if (content.Text is null)
        {
            return new FileOutcome([], new FileDiagnostic(file.RelativePath, content.Kind!.Value, content.Message!), false);
        }

        // Check the text; only a time-out is an expected failure
        try
        {
            return new FileOutcome(checkText(content.Text, file.RelativePath), null, true);
        }
        catch (RegexMatchTimeoutException exception)
        {
            return new FileOutcome(
                [],
                new FileDiagnostic(file.RelativePath, FileDiagnosticKind.TimedOut, $"Checking took too long and was abandoned: {exception.Message}"),
                false);
        }
    }

    /// <summary>
    ///     The outcome of checking one file.
    /// </summary>
    /// <param name="Issues">The findings of the file; empty when the file was not checked.</param>
    /// <param name="Diagnostic">The reason the file was not checked completely; null when it was.</param>
    /// <param name="Checked"><see langword="true"/> when the file was read and checked completely.</param>
    private readonly record struct FileOutcome(IReadOnlyList<SpellingIssue> Issues, FileDiagnostic? Diagnostic, bool Checked);
}
