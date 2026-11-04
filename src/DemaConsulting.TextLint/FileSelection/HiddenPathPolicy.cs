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
///     consumed by a literal-dot pattern segment. Only the <c>*</c> wildcard exists in a segment,
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
        return includePatterns.Any(pattern => Align(segments, 0, pattern.Split(Separator), 0, complete));
    }

    /// <summary>
    ///     Aligns path segments with pattern segments so that hidden segments meet literal-dot pattern segments.
    /// </summary>
    /// <remarks>
    ///     A recursive pattern segment consumes zero segments, or one non-hidden segment and then
    ///     tries again. Any other pattern segment consumes exactly one path segment it matches.
    ///     Stateless and thread-safe; patterns are short, so plain backtracking is sufficient.
    /// </remarks>
    /// <param name="path">The path segments. Must not be null.</param>
    /// <param name="pathIndex">The index of the next path segment.</param>
    /// <param name="pattern">The pattern segments. Must not be null.</param>
    /// <param name="patternIndex">The index of the next pattern segment.</param>
    /// <param name="complete">Whether the pattern must also be fully consumed when the path ends.</param>
    /// <returns><see langword="true"/> when a valid alignment exists.</returns>
    private static bool Align(string[] path, int pathIndex, string[] pattern, int patternIndex, bool complete)
    {
        // When the path is used up, a prefix test succeeds; a full test needs only recursive segments left
        if (pathIndex == path.Length)
        {
            return !complete || pattern.Skip(patternIndex).All(segment => segment == RecursiveSegment);
        }

        // When the pattern is used up first, the path is longer than the pattern can match
        if (patternIndex == pattern.Length)
        {
            return false;
        }

        // A recursive segment spans zero levels, or one visible level and then continues
        if (pattern[patternIndex] == RecursiveSegment)
        {
            return Align(path, pathIndex, pattern, patternIndex + 1, complete) ||
                (!IsHidden(path[pathIndex]) && Align(path, pathIndex + 1, pattern, patternIndex, complete));
        }

        // Any other segment must match this path segment and the rest must align
        return SegmentMatches(path[pathIndex], pattern[patternIndex]) &&
            Align(path, pathIndex + 1, pattern, patternIndex + 1, complete);
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
