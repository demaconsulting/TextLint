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

using System.Globalization;
using System.Text.RegularExpressions;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Dictionaries;
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
///     directory, an invalid pattern) and defects are exceptions, and so is cancellation. The settings'
///     <c>overrides</c> and <c>enabled</c> are applied here: a pre-pass over the selected files (sequential and
///     cheap, because it only matches paths) works out which overrides match each file, drops the files that
///     the settings disable before any is read, and picks the checker built for the file's combination of
///     overrides, so every checker exists before the parallel phase starts. The shared state is only the
///     immutable <see cref="TextChecker"/> instances and a lookup that is not changed while files are being
///     checked. The class is stateless and safe to call from several threads at once.
/// </remarks>
public static class SpellChecker
{
    /// <summary>
    ///     Selects the files the settings name under a directory and checks the spelling of each.
    /// </summary>
    /// <remarks>
    ///     The settings and their overrides are validated before any file is touched, so a bad language,
    ///     word-list entry, pattern, or override fails the call at once, even when no file matches the override.
    ///     File selection is synchronous in the library and runs on a thread-pool thread,
    ///     so the call does not block its caller while it walks the directory tree. Memory use is bounded by
    ///     the number of concurrent checks times the size of a file and its text. Side effects: reads
    ///     directories and files beneath <paramref name="rootDirectory"/>; nothing is written. The first
    ///     lookup loads the language's dictionaries. Cancellation is honored during the directory walk, between
    ///     the files being read, and while files are read; a canceled call throws and returns no partial result.
    ///     Files that the settings disable are found by the walk but are not read, reported, or counted; the
    ///     result says how many there were. Thread-safe.
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
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
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
            cancellationToken,
            searchRootDirectory: rootDirectory).ConfigureAwait(false);
    }

    /// <summary>
    ///     Selects the files with the given function, applies the overrides and <c>enabled</c> of the settings,
    ///     and checks the spelling of each enabled file.
    /// </summary>
    /// <remarks>
    ///     Internal test seam: the selection is a parameter, so tests can prove that the caller's token reaches the
    ///     file selection, which a real directory cannot show deterministically. The public method passes the
    ///     file selector. The selection runs on a thread-pool thread, because it is synchronous and may take long.
    ///     The steps are: build the resolver and every checker that an override can need, so that anything
    ///     unusable fails before a file is touched; select the files; resolve each file against the overrides
    ///     (matching its path relative to the settings root) and drop the disabled ones; check the rest with the
    ///     checker for their overrides; and finally describe what the settings did as notes. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings that say what to check and how. Must not be null.</param>
    /// <param name="selectFiles">Selects the files, given the token it must honor. Must not be null.</param>
    /// <param name="options">How to run; null means the defaults.</param>
    /// <param name="cancellationToken">Token that cancels the run, including the selection.</param>
    /// <param name="matchTimeout">
    ///     The longest time one pattern may spend on one text; null means the production limit of 5 seconds.
    ///     A second internal seam, so that tests can prove the time-out behavior in milliseconds.
    /// </param>
    /// <param name="searchRootDirectory">
    ///     The directory the selected files' relative paths are relative to, used with the settings root from the
    ///     options to match overrides; null when the caller does not know it, in which case the relative paths
    ///     of the files are matched as they are.
    /// </param>
    /// <returns>The result, ordered by file path (ordinal), then line, then column.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="selectFiles"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the language, a word-list entry, a pattern, or an override of the settings, or the language of the options, is not usable.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    internal static async Task<SpellCheckResult> CheckWithSelectionAsync(
        TextLintSettings settings,
        Func<CancellationToken, IReadOnlyList<SelectedFile>> selectFiles,
        SpellCheckOptions? options,
        CancellationToken cancellationToken,
        TimeSpan? matchTimeout = null,
        string? searchRootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(selectFiles);
        options ??= new SpellCheckOptions();

        // Reject unusable settings and overrides before touching the file system: build the checker of the
        // top-level settings, and one for each override that has settings, even one that no file will match
        var resolver = new SettingsOverrideResolver(settings, options.Language);
        TextChecker CreateChecker(TextLintSettings effective) =>
            matchTimeout is { } limit ? new TextChecker(effective, limit) : new TextChecker(effective);
        var baseChecker = CreateChecker(resolver.GetEffectiveSettings(resolver.ResolveUnmatched()));
        var checkers = new Dictionary<string, TextChecker>(StringComparer.Ordinal);
        for (var index = 0; index < resolver.OverrideCount; index++)
        {
            if (resolver.HasCheckerSettings(index))
            {
                checkers[index.ToString(CultureInfo.InvariantCulture)] = CreateChecker(resolver.GetEffectiveSettingsForOverride(index));
            }
        }

        // Select the files off the caller's thread; the selection's exceptions (missing root, bad pattern) propagate
        var files = await Task.Run(() => selectFiles(cancellationToken), cancellationToken).ConfigureAwait(false);

        // Resolve every file against the overrides before any is read: drop the disabled files, remember which
        // overrides were used, and give each file the checker of its combination of overrides
        var settingsRoot = options.SettingsRootDirectory ?? searchRootDirectory;
        var matched = new bool[resolver.OverrideCount];
        var usedOnChecked = new bool[resolver.OverrideCount];
        var enabledFiles = new List<SelectedFile>(files.Count);
        var checkerByPath = new Dictionary<string, TextChecker>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var relative = GetSettingsRelativePath(file, settingsRoot, searchRootDirectory);
            var resolution = relative is null ? resolver.ResolveUnmatched() : resolver.Resolve(relative);
            foreach (var index in resolution.MatchedOverrides)
            {
                matched[index] = true;
            }

            // A disabled file is not read, reported, or counted
            if (!resolution.Enabled)
            {
                continue;
            }

            enabledFiles.Add(file);
            foreach (var index in resolution.MatchedOverrides)
            {
                usedOnChecked[index] = true;
            }

            // Most files match no override with settings and share the checker of the top-level settings
            if (resolution.SettingsKey.Length > 0)
            {
                if (!checkers.TryGetValue(resolution.SettingsKey, out var checker))
                {
                    checker = CreateChecker(resolver.GetEffectiveSettings(resolution));
                    checkers[resolution.SettingsKey] = checker;
                }

                checkerByPath[file.RelativePath] = checker;
            }
        }

        // The lookups are complete and are only read from here on, so the parallel phase needs no lock
        var result = await CheckFilesAsync(
            enabledFiles,
            settings.MaxFileSize,
            (text, path) => (path is not null && checkerByPath.TryGetValue(path, out var checker) ? checker : baseChecker).Check(text, path),
            options,
            cancellationToken).ConfigureAwait(false);

        var disabledCount = files.Count - enabledFiles.Count;
        return result.WithRunInfo(disabledCount, BuildNotes(resolver, options, files.Count, disabledCount, enabledFiles.Count, matched, usedOnChecked));
    }

    /// <summary>
    ///     Works out the path of a selected file relative to the settings root, which is what overrides match.
    /// </summary>
    /// <remarks>
    ///     When the settings root is the directory the files were searched in, the selection's relative path is
    ///     already the answer. Otherwise (a caller that searched from another directory, for example because it
    ///     was given an absolute path) the path is recomputed from the file's full path. A file outside the
    ///     settings root has no path relative to it and matches no override, which the null answer says.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="file">The selected file. Must not be null.</param>
    /// <param name="settingsRoot">The settings root; null when unknown, in which case the selection's relative path is used.</param>
    /// <param name="searchRoot">The directory the selection's relative paths are relative to; null when unknown.</param>
    /// <returns>The path using forward slashes, or <see langword="null"/> when the file is outside the settings root.</returns>
    private static string? GetSettingsRelativePath(SelectedFile file, string? settingsRoot, string? searchRoot)
    {
        // Without a different root, the path the selection produced is the settings-relative path
        if (settingsRoot is null)
        {
            return file.RelativePath;
        }

        var settingsFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settingsRoot));
        if (searchRoot is not null &&
            string.Equals(settingsFull, Path.TrimEndingDirectorySeparator(Path.GetFullPath(searchRoot)), StringComparison.Ordinal))
        {
            return file.RelativePath;
        }

        // Recompute from the full path; a path that climbs out of the root, or lies on another drive, is outside it
        var relative = Path.GetRelativePath(settingsFull, file.FullPath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return Path.DirectorySeparatorChar == '/' ? relative : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    ///     Describes what the settings did to the run, as notes.
    /// </summary>
    /// <remarks>
    ///     The order is fixed so results are deterministic: first the information notes (a language that replaced
    ///     an override's, by override, then that every selected file is disabled), then the warnings (an override
    ///     that matched none of the selected files, by override). No warning is made when nothing was selected,
    ///     because every override would then be unmatched for a reason that has nothing to do with it. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="resolver">The resolver that holds the overrides. Must not be null.</param>
    /// <param name="options">The options of the run. Must not be null.</param>
    /// <param name="selectedCount">The number of files the selection returned.</param>
    /// <param name="disabledCount">The number of those that the settings disabled.</param>
    /// <param name="enabledCount">The number of those that remained to be checked.</param>
    /// <param name="matched">For each override, whether it matched any selected file. Must not be null.</param>
    /// <param name="usedOnChecked">For each override, whether it matched a file that was checked. Must not be null.</param>
    /// <returns>The notes in report order; empty when there is nothing to say.</returns>
    private static List<SpellCheckNote> BuildNotes(
        SettingsOverrideResolver resolver,
        SpellCheckOptions options,
        int selectedCount,
        int disabledCount,
        int enabledCount,
        bool[] matched,
        bool[] usedOnChecked)
    {
        var notes = new List<SpellCheckNote>();

        // A language from the options replaces an override's language only when the dictionaries differ
        for (var index = 0; index < resolver.OverrideCount; index++)
        {
            var settingsOverride = resolver.GetOverride(index);
            if (options.Language is { } requested &&
                settingsOverride.Language is { } replaced &&
                usedOnChecked[index] &&
                !ReferenceEquals(SpellingDictionary.ForLanguages(requested), SpellingDictionary.ForLanguages(replaced)))
            {
                var location = settingsOverride.Line is { } line && settingsOverride.Column is { } column
                    ? $"{settingsOverride.SourceName}({line},{column})"
                    : $"number {index + 1}";
                notes.Add(new SpellCheckNote(
                    SpellCheckNoteKind.LanguageReplaced,
                    $"--language {requested} replaces {replaced} for the override at {location}.",
                    settingsOverride.SourceName,
                    settingsOverride.Line,
                    settingsOverride.Column));
            }
        }

        // Files were selected but the settings switched every one of them off
        if (disabledCount > 0 && enabledCount == 0)
        {
            notes.Add(new SpellCheckNote(
                SpellCheckNoteKind.AllFilesDisabled,
                $"Spell checking is disabled by the settings for all {disabledCount} selected file(s).",
                null,
                null,
                null));
        }

        // An override that matches nothing is probably a mistake, but only a full selection can say so
        if (selectedCount > 0)
        {
            for (var index = 0; index < resolver.OverrideCount; index++)
            {
                if (!matched[index])
                {
                    var settingsOverride = resolver.GetOverride(index);
                    notes.Add(new SpellCheckNote(
                        SpellCheckNoteKind.OverrideUnmatched,
                        "The override matches none of the selected files.",
                        settingsOverride.SourceName,
                        settingsOverride.Line,
                        settingsOverride.Column));
                }
            }
        }

        return notes;
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
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
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
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
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
