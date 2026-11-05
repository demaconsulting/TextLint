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
///     Decides which hidden (dot-prefixed) paths the include patterns are allowed to reach.
/// </summary>
/// <remarks>
///     The file system matcher knows nothing about hidden names, so wildcards would happily descend
///     into <c>.git</c>. The rule implemented here is the one users know from cspell: a wildcard
///     (<c>*</c> or <c>**</c>) never matches a segment that starts with a dot, but a pattern
///     segment that itself starts with a literal dot (for example <c>.github</c> or <c>.*rc</c>)
///     does. Excluding hidden paths with an exclude pattern cannot express this, because such an
///     exclusion would also suppress literal hidden includes. Instead the policy aligns the path
///     segment by segment with each include pattern and requires every hidden segment to be
///     consumed by a literal-dot pattern segment (a dynamic program whose cost is bounded by the
///     product of the path and pattern lengths). Only the <c>*</c> wildcard exists in a segment,
///     matching what the matcher supports. The class is stateless and thread-safe.
/// </remarks>
internal static class HiddenPathPolicy
{
    /// <summary>
    ///     The character that marks a name as hidden.
    /// </summary>
    private const char HiddenPrefix = '.';

    /// <summary>
    ///     The path separator used in relative paths and patterns on every platform.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The path segment that matches any number of directory levels.
    /// </summary>
    private const string RecursiveSegment = "**";

    /// <summary>
    ///     The wildcard that matches any run of characters within a segment.
    /// </summary>
    private const char Wildcard = '*';

    /// <summary>
    ///     Determines whether a file or directory name is hidden.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The single path segment to test. Must not be null.</param>
    /// <returns><see langword="true"/> when the name starts with a dot.</returns>
    internal static bool IsHidden(string name) => name.Length > 0 && name[0] == HiddenPrefix;

    /// <summary>
    ///     Determines whether the include patterns may select the given file path.
    /// </summary>
    /// <remarks>
    ///     A path with no hidden segment is always permitted, because the matcher has already
    ///     matched it. A path with a hidden segment is permitted only when some single include
    ///     pattern matches the whole path with every hidden segment consumed by a literal-dot
    ///     pattern segment, so one pattern's wildcard is never excused by another pattern's
    ///     literal. Stateless and thread-safe.
    /// </remarks>
    /// <param name="relativePath">The root-relative path using forward slashes. Must not be null.</param>
    /// <param name="includePatterns">The expanded include patterns. Must not be null.</param>
    /// <returns><see langword="true"/> when the path is permitted.</returns>
    internal static bool IsPathPermitted(string relativePath, IReadOnlyList<string> includePatterns) =>
        Permits(relativePath, includePatterns, complete: true);

    /// <summary>
    ///     Determines whether a directory may contain files the include patterns could select.
    /// </summary>
    /// <remarks>
    ///     This lets the traversal skip hidden directories such as <c>.git</c> without enumerating
    ///     them, while still entering a hidden directory that a pattern names literally. The test
    ///     is deliberately loose (it checks only that some pattern could continue beneath the
    ///     directory); the exact decision is made later by <see cref="IsPathPermitted"/>. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="relativeDirectoryPath">The root-relative directory path using forward slashes. Must not be null.</param>
    /// <param name="includePatterns">The expanded include patterns. Must not be null.</param>
    /// <returns><see langword="true"/> when the directory should be entered.</returns>
    internal static bool CanContainPermittedPaths(string relativeDirectoryPath, IReadOnlyList<string> includePatterns) =>
        Permits(relativeDirectoryPath, includePatterns, complete: false);

    /// <summary>
    ///     Applies the hidden-segment rule to a path against every include pattern.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="relativePath">The root-relative path using forward slashes. Must not be null.</param>
    /// <param name="includePatterns">The expanded include patterns. Must not be null.</param>
    /// <param name="complete">
    ///     <see langword="true"/> to require a pattern to match the whole path; <see langword="false"/>
    ///     to require only that a pattern can continue beneath it.
    /// </param>
    /// <returns><see langword="true"/> when the path has no hidden segment or some pattern permits it.</returns>
    private static bool Permits(string relativePath, IReadOnlyList<string> includePatterns, bool complete)
    {
        // The common case needs no alignment: wildcards may match any non-hidden path
        var segments = relativePath.Split(Separator);
        if (!segments.Any(IsHidden))
        {
            return true;
        }

        // A hidden path is permitted when any one pattern can consume it legitimately
        return includePatterns.Any(pattern => CanAlign(segments, pattern.Split(Separator), complete));
    }

    /// <summary>
    ///     Determines whether path segments can be aligned with pattern segments so that hidden
    ///     segments meet only literal-dot pattern segments.
    /// </summary>
    /// <remarks>
    ///     The search is a dynamic program over the cells (path segments consumed, pattern
    ///     segments consumed), so its cost is bounded by the product of the two lengths however
    ///     many recursive segments the pattern has; plain backtracking would be exponential in
    ///     that case. A cell is reachable from the start cell by consuming: nothing for a
    ///     recursive pattern segment, one non-hidden path segment for a recursive pattern segment,
    ///     or one path segment that an ordinary pattern segment matches. Cells are filled in an
    ///     order in which every cell is visited after all the cells that lead to it. Stateless and
    ///     thread-safe; allocates one table per call.
    /// </remarks>
    /// <param name="path">The path segments. Must not be null.</param>
    /// <param name="pattern">The pattern segments. Must not be null.</param>
    /// <param name="complete">
    ///     <see langword="true"/> when the whole pattern must be consumed as well as the whole path;
    ///     <see langword="false"/> when consuming the whole path is enough.
    /// </param>
    /// <returns><see langword="true"/> when a valid alignment exists.</returns>
    private static bool CanAlign(string[] path, string[] pattern, bool complete)
    {
        // reachable[i, j] is true when the first i path segments can be consumed by the first j pattern segments
        var reachable = new bool[path.Length + 1, pattern.Length + 1];
        reachable[0, 0] = true;

        for (var pathIndex = 0; pathIndex <= path.Length; pathIndex++)
        {
            for (var patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                if (!reachable[pathIndex, patternIndex])
                {
                    continue;
                }

                // A recursive segment spans zero levels, or one visible level and then may span more
                if (pattern[patternIndex] == RecursiveSegment)
                {
                    reachable[pathIndex, patternIndex + 1] = true;
                    if (pathIndex < path.Length && !IsHidden(path[pathIndex]))
                    {
                        reachable[pathIndex + 1, patternIndex] = true;
                    }
                }
                else if (pathIndex < path.Length && SegmentMatches(path[pathIndex], pattern[patternIndex]))
                {
                    // Any other segment consumes exactly one matching path segment
                    reachable[pathIndex + 1, patternIndex + 1] = true;
                }
            }
        }

        // A prefix test succeeds once the path is consumed under any pattern segment; a full test
        // needs the pattern consumed too (trailing recursive segments consume nothing)
        return complete
            ? reachable[path.Length, pattern.Length]
            : Enumerable.Range(0, pattern.Length + 1).Any(patternIndex => reachable[path.Length, patternIndex]);
    }

    /// <summary>
    ///     Determines whether one pattern segment matches one path segment under the hidden-name rule.
    /// </summary>
    /// <remarks>
    ///     A hidden name is matched only by a pattern segment that itself starts with a dot, so a
    ///     leading wildcard never matches it. Comparison is ordinal (case-sensitive). Stateless and
    ///     thread-safe.
    /// </remarks>
    /// <param name="name">The path segment. Must not be null.</param>
    /// <param name="pattern">The pattern segment, which may contain <c>*</c>. Must not be null.</param>
    /// <returns><see langword="true"/> when the segment matches.</returns>
    private static bool SegmentMatches(string name, string pattern) =>
        (!IsHidden(name) || IsHidden(pattern)) && WildcardMatches(name, pattern);

    /// <summary>
    ///     Matches a name against a pattern in which <c>*</c> stands for any run of characters.
    /// </summary>
    /// <remarks>
    ///     Uses the classic two-pointer algorithm that remembers the last wildcard so a mismatch
    ///     backs up to it, which is linear in practice and needs no recursion. Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The text to match. Must not be null.</param>
    /// <param name="pattern">The pattern. Must not be null.</param>
    /// <returns><see langword="true"/> when the whole name matches.</returns>
    private static bool WildcardMatches(string name, string pattern)
    {
        var nameIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var starNameIndex = 0;

        // Consume the name, backing up to the last wildcard whenever a literal fails
        while (nameIndex < name.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == Wildcard)
            {
                starIndex = patternIndex++;
                starNameIndex = nameIndex;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == name[nameIndex])
            {
                nameIndex++;
                patternIndex++;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                nameIndex = ++starNameIndex;
            }
            else
            {
                return false;
            }
        }

        // Only wildcards may remain once the name is used up
        while (patternIndex < pattern.Length && pattern[patternIndex] == Wildcard)
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
