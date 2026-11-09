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

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     Turns the path and pattern arguments a user types on a command line into a search base and
///     validated portable patterns for <see cref="FileSelector"/>.
/// </summary>
/// <remarks>
///     Patterns in a settings file are strict on purpose: they use <c>/</c>, and absolute paths are
///     rejected, so one file selects the same files on every platform. A person at a shell works
///     differently: PowerShell completes <c>.\docs\guide.md</c>, an editor passes
///     <c>C:\work\project\README.md</c>, and a shell expands <c>~</c> before the program sees it. This
///     unit is the forgiving front door for that input, so every program built on the library
///     interprets command-line paths the same way, and the strict rules of <see cref="FileSelector"/>
///     stay untouched. The rules:
///     <list type="bullet">
///         <item>
///             On Windows a backslash is a path separator and is converted to <c>/</c>; elsewhere a backslash
///             is an ordinary character, so the strict pattern rules reject it with their own message.
///         </item>
///         <item>
///             A relative argument is a pattern relative to the root directory, exactly as it would be in a
///             settings file after the separator conversion. <c>~</c> is never expanded.
///         </item>
///         <item>
///             An absolute argument (a drive path or <c>//server/share</c> network path on Windows, a path
///             that starts with <c>/</c> elsewhere) is split at its first wildcard. The literal leading
///             part is a directory and the rest is a pattern within it. An argument without a wildcard that
///             names an existing directory, or that ends with a separator, selects everything below that
///             directory, as a bare directory name does in a relative pattern; otherwise it names one file
///             (a path that does not exist is taken as a file name, so it selects nothing).
///         </item>
///         <item>
///             An absolute argument inside the root directory means the same as the equivalent relative
///             argument, so settings such as the ignore patterns apply to it as to any other file. When
///             any argument lies outside the root directory, the search base becomes the deepest directory
///             that contains every absolute argument, and a relative argument cannot be combined with it.
///         </item>
///         <item>
///             When any argument is absolute the files are reported by absolute path (see
///             <see cref="CommandLineSelection.GetDisplayPath(string)"/>); otherwise by relative path.
///         </item>
///     </list>
///     The work is textual except for one question, whether an absolute argument without a wildcard names a
///     directory, which is a parameter of an internal overload, so the Windows and the other rule sets can
///     both be exercised on any operating system without a file system. Stateless and thread-safe.
/// </remarks>
public static class CommandLinePathResolver
{
    /// <summary>
    ///     The path separator used in portable patterns on every platform.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The path separator character of Windows, which is converted to <see cref="Separator"/> there.
    /// </summary>
    private const char WindowsSeparator = '\\';

    /// <summary>
    ///     The segment that names the current directory.
    /// </summary>
    private const string CurrentDirectorySegment = ".";

    /// <summary>
    ///     The segment that names the parent directory.
    /// </summary>
    private const string ParentDirectorySegment = "..";

    /// <summary>
    ///     The pattern that selects every file beneath a directory.
    /// </summary>
    private const string EverythingPattern = "**";

    /// <summary>
    ///     The length of a Windows drive prefix such as <c>C:</c>.
    /// </summary>
    private const int DrivePrefixLength = 2;

    /// <summary>
    ///     The leading text of a network path such as <c>//server/share</c>, after separator conversion.
    /// </summary>
    private const string NetworkPathPrefix = "//";

    /// <summary>
    ///     The number of leading segments that make up the root of a network path: the server and the share.
    /// </summary>
    private const int NetworkRootSegmentCount = 2;

    /// <summary>
    ///     The characters that make a path segment a pattern rather than a literal name.
    /// </summary>
    /// <remarks>
    ///     <c>?</c> is included even though the pattern rules reject it, so that an argument using it is
    ///     reported by those rules against the argument as typed instead of being taken as a file name.
    /// </remarks>
    private static readonly char[] WildcardCharacters = ['*', '?', '{', '}'];

    /// <summary>
    ///     Resolves command-line path arguments into a search base and portable patterns for the current
    ///     operating system.
    /// </summary>
    /// <remarks>
    ///     The root directory is made absolute against the current directory, but it is not checked for
    ///     existence; <see cref="FileSelector"/> reports a missing or unreadable root when it is used.
    ///     An absolute argument without a wildcard is looked up in the file system once, to learn whether it
    ///     names a directory (then it selects everything below it) or a file. Only a missing path counts as "not
    ///     a directory"; a path that cannot be examined is reported, not guessed at.
    ///     Side effects: reads the current directory when <paramref name="rootDirectory"/> is relative, and the
    ///     attributes of such arguments. Cancellation is not offered because there is no waiting. Thread-safe.
    /// </remarks>
    /// <param name="rootDirectory">
    ///     The directory that relative arguments are relative to; may itself be relative to the current
    ///     directory. Must not be null, empty, or white space.
    /// </param>
    /// <param name="arguments">The path and pattern arguments as typed. Must not be null or empty, and must not contain null.</param>
    /// <returns>The search base, the validated patterns, and how to report the files; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootDirectory"/> or <paramref name="arguments"/> is null.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the process may not examine an absolute argument that has no wildcard.</exception>
    /// <exception cref="IOException">Thrown when an absolute argument that has no wildcard cannot be examined for another reason.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="rootDirectory"/> is empty or white space, when <paramref name="arguments"/> is empty
    ///     or contains null, or when relative arguments are combined with an absolute argument outside the root
    ///     directory or the absolute arguments are on different drives or network shares.
    /// </exception>
    /// <exception cref="InvalidGlobPatternException">
    ///     Thrown, naming the argument as typed, when an argument is empty, a drive-relative path, a path that
    ///     starts with a separator but names no Windows drive, or is otherwise rejected by the pattern rules
    ///     (for example a negated pattern, the <c>?</c> wildcard, or a <c>..</c> segment after a wildcard).
    /// </exception>
    public static CommandLineSelection Resolve(string rootDirectory, IReadOnlyList<string> arguments)
    {
        // Make the root absolute for the current platform, then apply the rules of that platform
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        var resolved = ResolveWithStyle(Path.GetFullPath(rootDirectory), arguments, OperatingSystem.IsWindows(), IsExistingDirectory);

        // The core works in portable form; the platform form of the search base is what callers open
        return new CommandLineSelection(Path.GetFullPath(resolved.RootDirectory), resolved.Patterns, resolved.ReportsAbsolutePaths);
    }

    /// <summary>
    ///     Tells whether a path names an existing directory, without mistaking a failure for absence.
    /// </summary>
    /// <remarks>
    ///     <see cref="Directory.Exists(string?)"/> answers "no" for a path the process may not examine, which would
    ///     quietly turn a directory the user named into a file name that matches nothing. Reading the attributes
    ///     lets the file system say which case it is: only a missing file or directory is "no", and any other failure
    ///     propagates. Side effects: reads the attributes of one path. Thread-safe.
    /// </remarks>
    /// <param name="path">The path to examine. Must not be null.</param>
    /// <returns><see langword="true"/> when the path exists and is a directory.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the process may not examine the path.</exception>
    /// <exception cref="IOException">Thrown when the path cannot be examined for another reason.</exception>
    private static bool IsExistingDirectory(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // Only a path that does not exist is "not a directory"
            return false;
        }
    }

    /// <summary>
    ///     Resolves command-line path arguments using the path rules of a chosen platform.
    /// </summary>
    /// <remarks>
    ///     Internal test seam: the platform is a parameter, so tests can prove both rule sets on every
    ///     operating system, which the public method cannot show on one machine. Unlike the public
    ///     method, the returned search base is in portable form (forward slashes, for example <c>C:/work</c>).
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="absoluteRoot">The absolute root directory in the form of the chosen platform. Must not be null or white space.</param>
    /// <param name="arguments">The path and pattern arguments as typed. Must not be null or empty.</param>
    /// <param name="windowsPaths"><see langword="true"/> for Windows path rules; <see langword="false"/> for Unix path rules.</param>
    /// <param name="isDirectory">
    ///     Tells whether a portable absolute path (forward slashes, for example <c>C:/work/docs</c>) names an
    ///     existing directory, so that tests need no file system; null means nothing does. It is called only for an
    ///     absolute argument that has no wildcard and does not already end in a separator, and its exceptions propagate.
    /// </param>
    /// <returns>The resolved selection, with the search base in portable form.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the root is not absolute, the argument list is empty or contains null, the arguments cannot
    ///     be combined, or an argument is rejected by the pattern rules (see <see cref="InvalidGlobPatternException"/>).
    /// </exception>
    internal static CommandLineSelection ResolveWithStyle(
        string absoluteRoot,
        IReadOnlyList<string> arguments,
        bool windowsPaths,
        Func<string, bool>? isDirectory = null)
    {
        // Reject arguments that could only come from a programming error
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteRoot);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            throw new ArgumentException("At least one path or pattern is required.", nameof(arguments));
        }

        // Understand the root first, because every argument is judged against it
        var comparer = windowsPaths ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var root = ParseRoot(absoluteRoot, windowsPaths);

        // Read each argument in isolation; whether it can be combined with the others is decided afterwards
        var parsed = new List<ParsedArgument>(arguments.Count);
        foreach (var argument in arguments)
        {
            parsed.Add(ParseArgument(argument ?? throw new ArgumentException("An argument is null.", nameof(arguments)), windowsPaths, isDirectory));
        }

        // Choose the search base: the root when every absolute argument is inside it, else the common ancestor
        var absolute = parsed.Where(static argument => argument.Base is not null).ToList();
        var searchBase = absolute.All(argument => IsInside(argument.Base!.Value, root, comparer))
            ? root
            : FindCommonAncestor(absolute, parsed, root, comparer);

        // Express every argument as a pattern relative to the search base, and validate it as typed
        var patterns = parsed.Select(argument => ToPattern(argument, searchBase)).ToList();
        return new CommandLineSelection(searchBase.Render(), patterns, absolute.Count > 0);
    }

    /// <summary>
    ///     Reads the root directory into its prefix and segments.
    /// </summary>
    /// <remarks>
    ///     The root is a literal directory, so wildcard characters in its name are ordinary characters and
    ///     are not interpreted. Stateless and thread-safe.
    /// </remarks>
    /// <param name="absoluteRoot">The root directory path. Must not be null.</param>
    /// <param name="windowsPaths">Whether Windows path rules apply.</param>
    /// <returns>The parsed root.</returns>
    /// <exception cref="ArgumentException">Thrown when the root is not an absolute path under the chosen rules.</exception>
    private static PathParts ParseRoot(string absoluteRoot, bool windowsPaths)
    {
        // A relative or unsupported root cannot anchor anything
        string prefix;
        string remainder;
        try
        {
            if (!TrySplitRoot(absoluteRoot, windowsPaths, out prefix, out remainder))
            {
                throw new ArgumentException($"The root directory '{absoluteRoot}' must be an absolute path.", nameof(absoluteRoot));
            }
        }
        catch (InvalidGlobPatternException exception)
        {
            throw new ArgumentException($"The root directory '{absoluteRoot}' must be an absolute path: {exception.Reason}.", nameof(absoluteRoot), exception);
        }

        // Keep the literal segments, dropping empty and current-directory ones
        var segments = remainder.Split(Separator)
            .Where(static segment => segment.Length > 0 && segment != CurrentDirectorySegment)
            .ToList();
        return new PathParts(prefix, segments);
    }

    /// <summary>
    ///     Reads one command-line argument as either a relative pattern or an absolute path split at its first wildcard.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="argument">The argument as typed. Must not be null.</param>
    /// <param name="windowsPaths">Whether Windows path rules apply.</param>
    /// <param name="isDirectory">
    ///     Tells whether a portable absolute path names an existing directory; null means nothing does.
    /// </param>
    /// <returns>The parsed argument.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when the argument is an unsupported absolute form or climbs above the file system root.</exception>
    private static ParsedArgument ParseArgument(string argument, bool windowsPaths, Func<string, bool>? isDirectory)
    {
        // Convert Windows separators first, so every later step sees only forward slashes
        var text = windowsPaths ? argument.Replace(WindowsSeparator, Separator) : argument;

        // A relative argument is a pattern as it stands
        if (!TrySplitRoot(argument, text, windowsPaths, out var prefix, out var remainder))
        {
            return new ParsedArgument(argument, null, text);
        }

        // An absolute argument is a literal directory followed by a pattern within it
        var raw = remainder.Split(Separator);
        var literal = new List<string>();
        var wildcardIndex = Array.FindIndex(raw, static segment => segment.IndexOfAny(WildcardCharacters) >= 0);
        var literalCount = wildcardIndex >= 0 ? wildcardIndex : raw.Length;
        for (var index = 0; index < literalCount; index++)
        {
            ApplyLiteralSegment(argument, raw[index], literal);
        }

        // With a wildcard, everything from the first wildcard segment on is the pattern, kept as typed
        if (wildcardIndex >= 0)
        {
            return new ParsedArgument(argument, new PathParts(prefix, literal), string.Join(Separator, raw[wildcardIndex..]));
        }

        // Without one, the argument names a directory when it ends in a separator or names no segment,
        // and otherwise the last literal segment is the file within the directory before it
        var last = raw[^1];
        var namesDirectory = last.Length == 0 || last is CurrentDirectorySegment or ParentDirectorySegment || literal.Count == 0;
        if (namesDirectory)
        {
            return new ParsedArgument(argument, new PathParts(prefix, literal), EverythingPattern);
        }

        // A path that exists as a directory selects everything below it, as a bare directory name does in a
        // relative pattern; anything else, including a path that does not exist, is a file name
        if (isDirectory is not null && isDirectory(new PathParts(prefix, literal).Render()))
        {
            return new ParsedArgument(argument, new PathParts(prefix, literal), EverythingPattern);
        }

        var file = literal[^1];
        literal.RemoveAt(literal.Count - 1);
        return new ParsedArgument(argument, new PathParts(prefix, literal), file);
    }

    /// <summary>
    ///     Applies one literal segment of an absolute path to the directory being built.
    /// </summary>
    /// <remarks>
    ///     Empty and current-directory segments add nothing and a parent segment removes the previous one,
    ///     which is lexical normalization only: links are never resolved. Side effects: changes
    ///     <paramref name="literal"/>. Stateless and thread-safe.
    /// </remarks>
    /// <param name="argument">The argument as typed, used in the error. Must not be null.</param>
    /// <param name="segment">The segment to apply. Must not be null.</param>
    /// <param name="literal">The directory segments so far. Must not be null.</param>
    /// <exception cref="InvalidGlobPatternException">Thrown when a parent segment would climb above the file system root.</exception>
    private static void ApplyLiteralSegment(string argument, string segment, List<string> literal)
    {
        // Empty and current-directory segments name nothing
        if (segment.Length == 0 || segment == CurrentDirectorySegment)
        {
            return;
        }

        // A parent segment removes the directory before it, unless there is none to remove
        if (segment == ParentDirectorySegment)
        {
            if (literal.Count == 0)
            {
                throw new InvalidGlobPatternException(argument, "the path climbs above the root of the file system");
            }

            literal.RemoveAt(literal.Count - 1);
            return;
        }

        literal.Add(segment);
    }

    /// <summary>
    ///     Splits text that has Windows separators already converted into its root prefix and the remainder, when it is absolute.
    /// </summary>
    /// <remarks>
    ///     The Windows rules recognize a drive (<c>C:/x</c>) and a network path (<c>//server/share/x</c>)
    ///     as absolute, and reject the two forms that look absolute but are not well defined: a drive-relative
    ///     path (<c>C:x</c>) and a path that begins with one separator and names no drive. The other rules
    ///     recognize a leading <c>/</c>. Stateless and thread-safe.
    /// </remarks>
    /// <param name="argument">The text as typed, used in errors. Must not be null.</param>
    /// <param name="text">The text with separators converted. Must not be null.</param>
    /// <param name="windowsPaths">Whether Windows path rules apply.</param>
    /// <param name="prefix">Receives the root prefix (<c>C:</c>, <c>//server/share</c>, or empty) when the text is absolute.</param>
    /// <param name="remainder">Receives the text after the prefix when the text is absolute.</param>
    /// <returns><see langword="true"/> when the text is absolute; <see langword="false"/> when it is relative.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when the text is an unsupported absolute form.</exception>
    private static bool TrySplitRoot(string argument, string text, bool windowsPaths, out string prefix, out string remainder)
    {
        prefix = string.Empty;
        remainder = text;

        // Without Windows rules only a leading slash makes the text absolute
        if (!windowsPaths)
        {
            return text.StartsWith(Separator);
        }

        // A drive letter and colon start a drive path; without a following separator it is drive-relative
        if (text.Length >= DrivePrefixLength && char.IsAsciiLetter(text[0]) && text[1] == ':')
        {
            if (text.Length == DrivePrefixLength || text[DrivePrefixLength] != Separator)
            {
                throw new InvalidGlobPatternException(argument, "a drive-relative path ('C:x') is not supported; write 'C:/x' or a relative path");
            }

            prefix = text[..DrivePrefixLength];
            remainder = text[DrivePrefixLength..];
            return true;
        }

        // Two leading separators start a network path whose root is the server and the share
        if (text.StartsWith(NetworkPathPrefix, StringComparison.Ordinal))
        {
            var parts = text[NetworkPathPrefix.Length..].Split(Separator);
            if (parts.Length < NetworkRootSegmentCount || parts[0].Length == 0 || parts[1].Length == 0 ||
                parts.Take(NetworkRootSegmentCount).Any(static part => part.IndexOfAny(WildcardCharacters) >= 0))
            {
                throw new InvalidGlobPatternException(argument, "a network path must start with a literal server and share name ('//server/share')");
            }

            prefix = NetworkPathPrefix + parts[0] + Separator + parts[1];
            remainder = text[prefix.Length..];
            return true;
        }

        // A single leading separator on Windows means the root of whichever drive is current, which is ambiguous
        if (text.StartsWith(Separator))
        {
            throw new InvalidGlobPatternException(argument, "a path that starts with a separator but names no drive is ambiguous on Windows; write the drive letter or a relative path");
        }

        return false;
    }

    /// <summary>
    ///     Splits the root directory, which has not had its separators converted, using the same rules as an argument.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="absoluteRoot">The root directory path. Must not be null.</param>
    /// <param name="windowsPaths">Whether Windows path rules apply.</param>
    /// <param name="prefix">Receives the root prefix when the path is absolute.</param>
    /// <param name="remainder">Receives the text after the prefix when the path is absolute.</param>
    /// <returns><see langword="true"/> when the path is absolute.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when the path is an unsupported absolute form.</exception>
    private static bool TrySplitRoot(string absoluteRoot, bool windowsPaths, out string prefix, out string remainder) =>
        TrySplitRoot(absoluteRoot, windowsPaths ? absoluteRoot.Replace(WindowsSeparator, Separator) : absoluteRoot, windowsPaths, out prefix, out remainder);

    /// <summary>
    ///     Determines whether a directory is the root directory or lies beneath it.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="directory">The directory to test.</param>
    /// <param name="root">The root directory.</param>
    /// <param name="comparer">The comparer that decides whether two names are the same on the chosen platform. Must not be null.</param>
    /// <returns><see langword="true"/> when <paramref name="directory"/> is inside or equal to <paramref name="root"/>.</returns>
    private static bool IsInside(PathParts directory, PathParts root, StringComparer comparer) =>
        comparer.Equals(directory.Prefix, root.Prefix) &&
        directory.Segments.Count >= root.Segments.Count &&
        directory.Segments.Take(root.Segments.Count).SequenceEqual(root.Segments, comparer);

    /// <summary>
    ///     Finds the search base when some absolute argument lies outside the root directory.
    /// </summary>
    /// <remarks>
    ///     The base is the deepest directory that contains every absolute argument. Relative arguments
    ///     cannot be re-based without changing what they mean, and arguments on different drives or shares
    ///     have no common directory, so both are errors. Stateless and thread-safe.
    /// </remarks>
    /// <param name="absolute">The absolute arguments. Must not be empty.</param>
    /// <param name="all">Every argument. Must not be null.</param>
    /// <param name="root">The root directory, used in the error. Must not be null.</param>
    /// <param name="comparer">The comparer that decides whether two names are the same on the chosen platform. Must not be null.</param>
    /// <returns>The common directory.</returns>
    /// <exception cref="ArgumentException">Thrown when relative arguments are present or the arguments have no common root.</exception>
    private static PathParts FindCommonAncestor(
        List<ParsedArgument> absolute,
        List<ParsedArgument> all,
        PathParts root,
        StringComparer comparer)
    {
        // A relative argument means "relative to the root", which is not where this search will start
        var outside = absolute.First(argument => !IsInside(argument.Base!.Value, root, comparer));
        var relative = all.FirstOrDefault(static argument => argument.Base is null);
        if (relative is not null)
        {
            throw new ArgumentException(
                $"The path '{relative.Original}' is relative to the root directory '{root.Render()}', but '{outside.Original}' " +
                "lies outside it, so the two cannot be combined in one selection; select them separately, " +
                "or choose a root directory that contains all of them.",
                nameof(all));
        }

        // Without a shared drive or share there is no directory that contains both
        var first = absolute[0].Base!.Value;
        if (absolute.Any(argument => !comparer.Equals(argument.Base!.Value.Prefix, first.Prefix)))
        {
            throw new ArgumentException(
                "The absolute paths are on different drives or network shares, so no single directory contains them all.",
                nameof(all));
        }

        // Keep the leading segments that every absolute argument shares
        var common = first.Segments.ToList();
        foreach (var segments in absolute.Skip(1).Select(static argument => argument.Base!.Value.Segments))
        {
            var length = 0;
            while (length < common.Count && length < segments.Count && comparer.Equals(common[length], segments[length]))
            {
                length++;
            }

            common.RemoveRange(length, common.Count - length);
        }

        return new PathParts(first.Prefix, common);
    }

    /// <summary>
    ///     Expresses an argument as a validated pattern relative to the search base.
    /// </summary>
    /// <remarks>
    ///     An absolute argument becomes a pattern anchored at the base with a leading <c>/</c>, so a file name
    ///     matches only there and not at any depth. A relative argument keeps its meaning. The pattern is
    ///     validated by the same rules as a settings file pattern, and a rejection is reported against the
    ///     argument as the user typed it. Stateless and thread-safe.
    /// </remarks>
    /// <param name="argument">The parsed argument. Must not be null.</param>
    /// <param name="searchBase">The search base the pattern must be relative to.</param>
    /// <returns>The pattern.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown, naming the argument as typed, when the pattern rules reject it.</exception>
    private static string ToPattern(ParsedArgument argument, PathParts searchBase)
    {
        // Relative arguments are used as written; absolute ones are anchored beneath the search base
        var pattern = argument.Text;
        if (argument.Base is { } directory)
        {
            var below = directory.Segments.Skip(searchBase.Segments.Count).Append(argument.Text);
            pattern = Separator + string.Join(Separator, below);
        }

        // Report a rejection against what the user typed, not against the rewritten form
        try
        {
            _ = GlobPatternExpander.Expand(pattern);
        }
        catch (InvalidGlobPatternException exception)
        {
            throw new InvalidGlobPatternException(argument.Original, exception.Reason);
        }

        return pattern;
    }

    /// <summary>
    ///     A path split into its root prefix and directory segments.
    /// </summary>
    /// <param name="Prefix">The root prefix: a drive such as <c>C:</c>, a network root such as <c>//server/share</c>, or empty.</param>
    /// <param name="Segments">The directory names beneath the prefix, in order.</param>
    private readonly record struct PathParts(string Prefix, IReadOnlyList<string> Segments)
    {
        /// <summary>
        ///     Writes the path in portable form.
        /// </summary>
        /// <remarks>
        ///     A path with no segments is the root itself, so it ends with a separator (<c>C:/</c>, <c>/</c>).
        /// </remarks>
        /// <returns>The path with forward slashes.</returns>
        public string Render() => Prefix + Separator + string.Join(Separator, Segments);
    }

    /// <summary>
    ///     One command-line argument after it has been read.
    /// </summary>
    /// <param name="Original">The argument exactly as typed.</param>
    /// <param name="Base">The literal directory of an absolute argument; <see langword="null"/> for a relative argument.</param>
    /// <param name="Text">The pattern of a relative argument, or the pattern within the directory for an absolute one.</param>
    private sealed record ParsedArgument(string Original, PathParts? Base, string Text);
}
