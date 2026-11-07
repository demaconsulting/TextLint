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

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     Selects the files to check by applying glob patterns to a root directory.
/// </summary>
/// <remarks>
///     This is the single place that turns glob patterns into a list of files, so every consumer
///     selects files identically. It takes the patterns and the hidden-file switch as plain
///     parameters rather than any configuration type, so the file selection subsystem does not
///     depend on the configuration subsystem; a caller that holds settings passes their values
///     (for example <c>settings.Files</c>, <c>settings.IgnorePaths</c>, and
///     <c>settings.EnableGlobDot</c>). Patterns are matched case-sensitively on every operating
///     system, so a configuration selects the same files on Windows, Linux, and macOS. Directories
///     excluded by an ignore pattern, and hidden directories no pattern names literally, are never
///     enumerated, because scanning trees such as <c>node_modules</c> or <c>.git</c> would
///     otherwise dominate the run time on large repositories. The class is stateless and
///     thread-safe; each call builds its own matcher and reads the supplied lists once.
/// </remarks>
public static class FileSelector
{
    /// <summary>
    ///     Selects the files under a directory that match the patterns.
    /// </summary>
    /// <remarks>
    ///     The directory walk is synchronous because the underlying matcher is, and wrapping it in a
    ///     background task would only hide the blocking. Side effects: reads directory listings
    ///     beneath <paramref name="rootDirectory"/>; no file is opened. The result is a snapshot.
    ///     A root that is missing or unreadable is reported (see the exceptions below). Below the root,
    ///     a subdirectory that no longer exists when it is listed is treated by the matcher as empty, and
    ///     any other failure to list a subdirectory propagates.
    ///     The pattern lists are only read during the call and must not be changed by another
    ///     thread while it runs. Cancellation is cooperative: the walk stops at the next directory or
    ///     entry after the token is cancelled, so a very large single directory delays it by at most one entry.
    /// </remarks>
    /// <param name="rootDirectory">The directory that patterns are relative to. Must name a readable directory.</param>
    /// <param name="includePatterns">The include glob patterns. Must not be null; an empty list selects nothing.</param>
    /// <param name="ignorePatterns">The exclude glob patterns, which win over include patterns. Must not be null.</param>
    /// <param name="enableGlobDot">
    ///     <see langword="true"/> to let wildcards match hidden (dot-prefixed) names; otherwise only
    ///     hidden names written literally in a pattern match.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token that stops the call. It is checked before the root is probed and while the directory tree
    ///     is walked, before each directory is listed and before each entry of a listing is used.
    /// </param>
    /// <returns>
    ///     The selected files ordered by ordinal relative path, using forward slashes. Empty when
    ///     <paramref name="includePatterns"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootDirectory"/> is empty or white space.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="rootDirectory"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the process is not allowed to read <paramref name="rootDirectory"/>; it is not reported as missing.
    /// </exception>
    /// <exception cref="IOException">
    ///     Thrown when <paramref name="rootDirectory"/> cannot be read for another reason, for example when it names a file.
    /// </exception>
    /// <exception cref="InvalidGlobPatternException">Thrown when a pattern is unsupported or malformed.</exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is cancelled before or during the call; no partial
    ///     result is returned.
    /// </exception>
    public static IReadOnlyList<SelectedFile> Select(
        string rootDirectory,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> ignorePatterns,
        bool enableGlobDot = false,
        CancellationToken cancellationToken = default)
    {
        // Validate the arguments and every pattern before touching the file system, so an invalid
        // pattern is always reported, even when the root directory is also missing
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(includePatterns);
        ArgumentNullException.ThrowIfNull(ignorePatterns);
        var includes = ExpandAll(includePatterns);
        var excludes = ExpandAll(ignorePatterns);

        // Honor a token that is already cancelled before any directory is read
        cancellationToken.ThrowIfCancellationRequested();

        // Only then probe the root directory. Asking whether it exists would report an unreadable
        // directory as missing, so the directory is read instead and the failure is reported as it is
        var root = new DirectoryInfo(rootDirectory);
        ProbeRoot(rootDirectory, root.EnumerateFileSystemInfos);

        return SelectExpanded(new DirectoryInfoWrapper(root), includes, excludes, enableGlobDot, cancellationToken);
    }

    /// <summary>
    ///     Reads the first entry of the selection root so a missing or unreadable root is reported as such.
    /// </summary>
    /// <remarks>
    ///     The matcher's directory wrapper treats every failure to list a directory as an empty directory,
    ///     so without this probe a missing root, or one the process may not read, would silently select
    ///     nothing. Reading the real directory lets the file system say which failure it is: only a
    ///     directory-not-found failure becomes <see cref="DirectoryNotFoundException"/>, and every other
    ///     failure (for example <see cref="UnauthorizedAccessException"/> or a general
    ///     <see cref="IOException"/>) propagates with its own type. Internal test seam: the enumeration is
    ///     passed in so tests can supply each failure on every platform. Side effects: reads one directory
    ///     entry. Stateless and thread-safe.
    /// </remarks>
    /// <param name="rootDirectory">The root path as the caller gave it, used in the error message. Must not be null.</param>
    /// <param name="enumerateRoot">Lists the root directory. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when the listing fails because the directory does not exist; the original failure is the inner exception.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Propagated when the process may not read the directory.</exception>
    /// <exception cref="IOException">Propagated when the directory cannot be read for any other reason.</exception>
    internal static void ProbeRoot(string rootDirectory, Func<IEnumerable<FileSystemInfo>> enumerateRoot)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(enumerateRoot);

        try
        {
            // The listing is lazy, so the first step is what reaches the file system
            using var entries = enumerateRoot().GetEnumerator();
            _ = entries.MoveNext();
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new DirectoryNotFoundException(
                $"The selection root directory '{rootDirectory}' does not exist.", exception);
        }
    }

    /// <summary>
    ///     Selects the files under an abstract directory that match the patterns.
    /// </summary>
    /// <remarks>
    ///     Internal test seam: it lets tests supply a directory that records which directories are
    ///     enumerated, which proves pruning deterministically instead of by timing, and that cancels the
    ///     token while the walk is running, which proves cancellation deterministically. Stateless and thread-safe.
    /// </remarks>
    /// <param name="root">The directory that patterns are relative to. Must not be null.</param>
    /// <param name="includePatterns">The include glob patterns. Must not be null.</param>
    /// <param name="ignorePatterns">The exclude glob patterns. Must not be null.</param>
    /// <param name="enableGlobDot">Whether wildcards may match hidden (dot-prefixed) names.</param>
    /// <param name="cancellationToken">Token that stops the walk.</param>
    /// <returns>The selected files ordered by ordinal relative path, using forward slashes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidGlobPatternException">Thrown when a pattern is unsupported or malformed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static IReadOnlyList<SelectedFile> Select(
        DirectoryInfoBase root,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> ignorePatterns,
        bool enableGlobDot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(includePatterns);
        ArgumentNullException.ThrowIfNull(ignorePatterns);

        // Validate and expand every pattern first so a bad pattern fails even when nothing is selected
        return SelectExpanded(root, ExpandAll(includePatterns), ExpandAll(ignorePatterns), enableGlobDot, cancellationToken);
    }

    /// <summary>
    ///     Selects the files under a directory that match already-validated, expanded patterns.
    /// </summary>
    /// <remarks>
    ///     Shared core of both selection entry points: each validates and expands its patterns first,
    ///     so pattern errors are reported before any directory is read. Stateless and thread-safe.
    /// </remarks>
    /// <param name="root">The directory that patterns are relative to. Must not be null.</param>
    /// <param name="includes">The expanded include patterns. Must not be null.</param>
    /// <param name="excludes">The expanded exclude patterns. Must not be null.</param>
    /// <param name="enableGlobDot">Whether wildcards may match hidden (dot-prefixed) names.</param>
    /// <param name="cancellationToken">Token that stops the walk.</param>
    /// <returns>The selected files ordered by ordinal relative path, using forward slashes.</returns>
    private static IReadOnlyList<SelectedFile> SelectExpanded(
        DirectoryInfoBase root,
        List<string> includes,
        List<string> excludes,
        bool enableGlobDot,
        CancellationToken cancellationToken)
    {
        if (includes.Count == 0)
        {
            return [];
        }

        // Walk through a hidden-directory filter unless wildcards may match hidden names, and through a
        // view that stops the walk on cancellation, because the matcher cannot be cancelled itself
        var matcher = BuildMatcher(includes, excludes);
        DirectoryInfoBase filtered = enableGlobDot ? root : new HiddenFilteringDirectory(root, includes);
        var walked = new CancelableDirectory(filtered, cancellationToken);
        IEnumerable<string> paths = matcher.Execute(walked).Files.Select(match => match.Path);

        // Decide hidden files exactly, now that the matcher has found every candidate
        if (!enableGlobDot)
        {
            paths = paths.Where(path => HiddenPathPolicy.IsPathPermitted(path, includes));
        }

        // Order by ordinal relative path so the result is identical on every platform
        return [.. paths.Order(StringComparer.Ordinal).Select(path => ToSelectedFile(root, path))];
    }

    /// <summary>
    ///     Expands every user pattern into matcher patterns.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="patterns">The user patterns. Must not be null.</param>
    /// <returns>The matcher patterns for all user patterns, in order.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when any pattern is unsupported or malformed.</exception>
    private static List<string> ExpandAll(IReadOnlyList<string> patterns) =>
        [.. patterns.SelectMany(GlobPatternExpander.Expand)];

    /// <summary>
    ///     Builds a case-sensitive matcher from expanded include and exclude patterns.
    /// </summary>
    /// <remarks>
    ///     The matcher defaults to case-insensitive comparison, so ordinal comparison is requested
    ///     explicitly to make selection identical on every operating system. Stateless and thread-safe.
    /// </remarks>
    /// <param name="includes">The expanded include patterns. Must not be null.</param>
    /// <param name="excludes">The expanded exclude patterns. Must not be null.</param>
    /// <returns>A new matcher configured with the patterns.</returns>
    private static Matcher BuildMatcher(List<string> includes, List<string> excludes)
    {
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddIncludePatterns(includes);
        matcher.AddExcludePatterns(excludes);
        return matcher;
    }

    /// <summary>
    ///     Creates the result entry for a matched relative path.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="root">The selection root. Must not be null.</param>
    /// <param name="relativePath">The matched root-relative path. Must not be null.</param>
    /// <returns>The selected file with its relative and full paths.</returns>
    private static SelectedFile ToSelectedFile(DirectoryInfoBase root, string relativePath) =>
        new(relativePath, Path.GetFullPath(Path.Combine(root.FullName, relativePath)));
}
