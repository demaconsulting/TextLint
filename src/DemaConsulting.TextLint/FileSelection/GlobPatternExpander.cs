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
///     Validates user-written glob patterns and rewrites them into patterns the file system matcher understands.
/// </summary>
/// <remarks>
///     The file system matcher has no brace alternatives, no notion of "a bare name matches at any
///     depth", and silently mishandles absolute, parent-escaping, and backslash patterns. This unit
///     closes those gaps with pure string logic so the behavior is exhaustively testable without a
///     file system, and so both the settings loader (at load time) and the file selector (for
///     patterns supplied in code) apply the identical rules. The class is stateless and thread-safe.
/// </remarks>
internal static class GlobPatternExpander
{
    /// <summary>
    ///     The most patterns a single user pattern may expand to through brace alternatives.
    /// </summary>
    /// <remarks>
    ///     Chained groups multiply (<c>{a,b}{a,b}{a,b}...</c>), so an unbounded expansion lets a
    ///     short pattern exhaust memory. The bound is far above any realistic pattern.
    /// </remarks>
    private const int MaximumExpandedPatternCount = 256;

    /// <summary>
    ///     The path segment that matches any number of directory levels.
    /// </summary>
    private const string RecursiveSegment = "**";

    /// <summary>
    ///     The segment that names the current directory and is removed from patterns.
    /// </summary>
    private const string CurrentDirectorySegment = ".";

    /// <summary>
    ///     The segment that names the parent directory and is rejected in patterns.
    /// </summary>
    private const string ParentDirectorySegment = "..";

    /// <summary>
    ///     The leading text that anchors a pattern at the selection root.
    /// </summary>
    private const string CurrentDirectoryPrefix = "./";

    /// <summary>
    ///     The path separator used in patterns on every platform.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The character that opens a brace alternative group.
    /// </summary>
    private const char BraceOpen = '{';

    /// <summary>
    ///     The character that closes a brace alternative group.
    /// </summary>
    private const char BraceClose = '}';

    /// <summary>
    ///     The character that separates brace alternatives.
    /// </summary>
    private const char AlternativeSeparator = ',';

    /// <summary>
    ///     The length of a Windows drive prefix such as <c>C:</c>.
    /// </summary>
    private const int DrivePrefixLength = 2;

    /// <summary>
    ///     Validates a user pattern and rewrites it into one or more matcher patterns.
    /// </summary>
    /// <remarks>
    ///     Brace alternatives are expanded first because each alternative may have a different
    ///     shape (<c>{src/*.md,README.md}</c>), and each alternative is then anchored on its own.
    ///     A pattern without a slash names a file or directory at any depth, matching the
    ///     behavior users know from ignore files. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The user-written pattern. Must not be null.</param>
    /// <returns>The matcher patterns, without duplicates, in a stable order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
    /// <exception cref="InvalidGlobPatternException">
    ///     Thrown when the pattern is empty, negated, uses a backslash or the <c>?</c> wildcard, is
    ///     drive-rooted, has an empty or parent segment, has unbalanced braces, or expands to too many patterns.
    /// </exception>
    internal static IReadOnlyList<string> Expand(string pattern)
    {
        // Reject patterns that cannot be interpreted before any rewriting
        ArgumentNullException.ThrowIfNull(pattern);
        Validate(pattern);

        // Expand brace alternatives into plain patterns
        var alternatives = new List<string>();
        ExpandBraces(pattern, pattern, alternatives);

        // Anchor each alternative and collect the distinct matcher patterns
        return [.. alternatives.SelectMany(alternative => Anchor(pattern, alternative)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Rejects patterns that are unsupported regardless of brace expansion.
    /// </summary>
    /// <remarks>
    ///     Each rejection exists because the underlying matcher would otherwise silently select
    ///     the wrong files: it ignores negation, treats <c>?</c> literally, accepts backslashes
    ///     only on Windows, and returns bogus results for rooted patterns. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The user-written pattern. Must not be null.</param>
    /// <exception cref="InvalidGlobPatternException">Thrown when the pattern is unsupported.</exception>
    private static void Validate(string pattern)
    {
        // Each check names the specific reason so the user can correct the pattern
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidGlobPatternException(pattern, "the pattern is empty");
        }

        if (pattern.StartsWith('!'))
        {
            throw new InvalidGlobPatternException(pattern, "negated patterns ('!') are not supported");
        }

        if (pattern.Contains('\\'))
        {
            throw new InvalidGlobPatternException(pattern, "use '/' as the path separator instead of '\\'");
        }

        if (pattern.Contains('?'))
        {
            throw new InvalidGlobPatternException(pattern, "the '?' wildcard is not supported");
        }

        if (IsDriveRooted(pattern))
        {
            throw new InvalidGlobPatternException(pattern, "absolute paths are not supported");
        }

        if (!HasBalancedBraces(pattern))
        {
            throw new InvalidGlobPatternException(pattern, "the braces are not balanced");
        }
    }

    /// <summary>
    ///     Determines whether a pattern starts with a Windows drive specifier such as <c>C:</c>.
    /// </summary>
    /// <remarks>
    ///     Checked textually so the answer is the same on every operating system. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The pattern to test. Must not be null.</param>
    /// <returns><see langword="true"/> when the pattern begins with a drive letter and a colon.</returns>
    private static bool IsDriveRooted(string pattern) =>
        pattern.Length >= DrivePrefixLength && char.IsAsciiLetter(pattern[0]) && pattern[1] == ':';

    /// <summary>
    ///     Determines whether every opening brace has a matching closing brace and vice versa.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The pattern to test. Must not be null.</param>
    /// <returns><see langword="true"/> when the braces are balanced.</returns>
    private static bool HasBalancedBraces(string pattern)
    {
        // Track nesting depth; a closing brace with no opener is unbalanced immediately
        var depth = 0;
        foreach (var character in pattern)
        {
            if (character == BraceOpen)
            {
                depth++;
            }
            else if (character == BraceClose && --depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    /// <summary>
    ///     Expands the leftmost brace group that has alternatives, recursively, into plain patterns.
    /// </summary>
    /// <remarks>
    ///     Follows shell semantics: a group expands only when it has a top-level comma, a group
    ///     without one stays literal, nested groups expand once the outer group is spliced in,
    ///     and several groups produce the cartesian product. Stateless and thread-safe; appends to
    ///     <paramref name="results"/>.
    /// </remarks>
    /// <param name="original">The original user pattern, used to name it in errors. Must not be null.</param>
    /// <param name="text">The text still to expand. Must not be null.</param>
    /// <param name="results">The list that receives each fully expanded pattern. Must not be null.</param>
    /// <exception cref="InvalidGlobPatternException">Thrown when the expansion exceeds the allowed count.</exception>
    private static void ExpandBraces(string original, string text, List<string> results)
    {
        // With no expandable group left, the text is a finished pattern
        if (!TryFindExpandableGroup(text, out var open, out var close))
        {
            if (results.Count >= MaximumExpandedPatternCount)
            {
                throw new InvalidGlobPatternException(original, "the braces expand to too many patterns");
            }

            results.Add(text);
            return;
        }

        // Splice each alternative between the surrounding text and expand what remains
        var prefix = text[..open];
        var suffix = text[(close + 1)..];
        foreach (var alternative in SplitAlternatives(text[(open + 1)..close]))
        {
            ExpandBraces(original, prefix + alternative + suffix, results);
        }
    }

    /// <summary>
    ///     Finds the leftmost brace group that contains a top-level alternative separator.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to search. Must not be null.</param>
    /// <param name="open">Receives the index of the opening brace when a group is found.</param>
    /// <param name="close">Receives the index of the matching closing brace when a group is found.</param>
    /// <returns><see langword="true"/> when an expandable group exists.</returns>
    private static bool TryFindExpandableGroup(string text, out int open, out int close)
    {
        // Try every opening brace left to right; a group without alternatives is skipped
        for (open = text.IndexOf(BraceOpen); open >= 0; open = text.IndexOf(BraceOpen, open + 1))
        {
            close = FindClosingBrace(text, open);
            if (close >= 0 && SplitAlternatives(text[(open + 1)..close]).Count > 1)
            {
                return true;
            }
        }

        close = -1;
        return false;
    }

    /// <summary>
    ///     Finds the closing brace that matches the opening brace at a given index.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The text to search. Must not be null.</param>
    /// <param name="open">The index of the opening brace.</param>
    /// <returns>The index of the matching closing brace, or -1 when there is none.</returns>
    private static int FindClosingBrace(string text, int open)
    {
        // Count nested groups so an inner closing brace is not mistaken for the match
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == BraceOpen)
            {
                depth++;
            }
            else if (text[index] == BraceClose && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Splits the inside of a brace group at its top-level alternative separators.
    /// </summary>
    /// <remarks>
    ///     Separators inside nested groups belong to those groups and are left in place. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="content">The text between the braces. Must not be null.</param>
    /// <returns>The alternatives; a single item when there is no top-level separator.</returns>
    private static List<string> SplitAlternatives(string content)
    {
        // Cut at each separator that is not nested inside an inner group
        var alternatives = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == BraceOpen)
            {
                depth++;
            }
            else if (content[index] == BraceClose)
            {
                depth--;
            }
            else if (content[index] == AlternativeSeparator && depth == 0)
            {
                alternatives.Add(content[start..index]);
                start = index + 1;
            }
        }

        alternatives.Add(content[start..]);
        return alternatives;
    }

    /// <summary>
    ///     Converts one brace-free pattern into the matcher patterns that give it gitignore-like anchoring.
    /// </summary>
    /// <remarks>
    ///     A pattern with a separator, or one written with a leading <c>/</c> or <c>./</c>, is
    ///     anchored at the root and used as written. A pattern naming a single segment matches a
    ///     file or a directory (and so everything beneath it) at any depth. A trailing slash limits
    ///     the pattern to directories. Current-directory segments are removed and repeated
    ///     recursive segments are merged so equivalent patterns compare equal. Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="original">The original user pattern, used to name it in errors. Must not be null.</param>
    /// <param name="alternative">The brace-free pattern to convert. Must not be null.</param>
    /// <returns>One or two matcher patterns.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when the pattern has no segments, an empty segment, or a parent segment.</exception>
    private static IReadOnlyList<string> Anchor(string original, string alternative)
    {
        // Read the anchoring and directory-only markers, then drop them from the text
        var text = alternative;
        var anchored = false;
        while (text.StartsWith(CurrentDirectoryPrefix, StringComparison.Ordinal))
        {
            text = text[CurrentDirectoryPrefix.Length..];
            anchored = true;
        }

        if (text.StartsWith(Separator))
        {
            text = text[1..];
            anchored = true;
        }

        var directoryOnly = text.EndsWith(Separator);
        if (directoryOnly)
        {
            text = text[..^1];
        }

        // Clean the segments, rejecting those the matcher cannot handle safely
        var segments = CleanSegments(original, text);

        // A single unanchored segment names a file or directory at any depth
        if (!anchored && segments.Count == 1)
        {
            return BareNameForms(segments[0], directoryOnly);
        }

        // Otherwise the pattern is used as written, extended to the subtree for a directory pattern
        var joined = string.Join(Separator, segments);
        if (directoryOnly && segments[^1] != RecursiveSegment)
        {
            joined += Separator + RecursiveSegment;
        }

        return [joined];
    }

    /// <summary>
    ///     Splits a pattern into segments, validating them and removing redundant ones.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="original">The original user pattern, used to name it in errors. Must not be null.</param>
    /// <param name="text">The pattern text without anchoring markers. Must not be null.</param>
    /// <returns>The cleaned segments; never empty.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when a segment is empty or a parent segment, or no segments remain.</exception>
    private static List<string> CleanSegments(string original, string text)
    {
        var segments = new List<string>();
        foreach (var segment in text.Split(Separator))
        {
            // Empty and parent segments cannot be matched reliably
            if (segment.Length == 0 && text.Length > 0)
            {
                throw new InvalidGlobPatternException(original, "the pattern contains an empty path segment ('//')");
            }

            if (segment == ParentDirectorySegment)
            {
                throw new InvalidGlobPatternException(original, "parent directory segments ('..') are not supported");
            }

            // Current-directory segments add nothing, and a repeated recursive segment is the same as one
            if (segment == CurrentDirectorySegment || segment.Length == 0 ||
                (segment == RecursiveSegment && segments.Count > 0 && segments[^1] == RecursiveSegment))
            {
                continue;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new InvalidGlobPatternException(original, "the pattern names no file or directory");
        }

        return segments;
    }

    /// <summary>
    ///     Builds the matcher patterns for a single unanchored name.
    /// </summary>
    /// <remarks>
    ///     The second form (<c>**/name/**</c>) makes a bare directory name select, or exclude, the
    ///     whole subtree, which is what ignore-style patterns such as <c>node_modules</c> are
    ///     expected to do; excluding it also lets the matcher skip the directory without
    ///     enumerating it. Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The single segment. Must not be null.</param>
    /// <param name="directoryOnly">Whether the pattern was written with a trailing slash.</param>
    /// <returns>The matcher patterns for the name.</returns>
    private static IReadOnlyList<string> BareNameForms(string name, bool directoryOnly)
    {
        // A lone recursive segment already means everything
        if (name == RecursiveSegment)
        {
            return [RecursiveSegment];
        }

        var subtree = RecursiveSegment + Separator + name + Separator + RecursiveSegment;
        return directoryOnly ? [subtree] : [RecursiveSegment + Separator + name, subtree];
    }
}
