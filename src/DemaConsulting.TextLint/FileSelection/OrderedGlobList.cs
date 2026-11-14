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

namespace DemaConsulting.TextLint.FileSelection;

/// <summary>
///     An ordered list of glob patterns in which a leading <c>!</c> excludes and the last matching pattern wins.
/// </summary>
/// <remarks>
///     This is the one place that gives a list of globs the ordered meaning users know from ignore files
///     and ReviewMark: a path is selected when the last pattern that matches it is a plain (included)
///     pattern, so a later include can bring back a path that an earlier <c>!</c> pattern removed. It is
///     used for the <c>files</c> setting and for the <c>filename</c> of a settings override. Every pattern
///     keeps the rules of <see cref="GlobPatternExpander"/> (brace alternatives, bare names at any depth,
///     a trailing <c>/</c> for directories, a leading <c>/</c> anchor, no <c>?</c>), the case-sensitive
///     comparison of <see cref="FileSelector"/>, and the hidden-name rule of <see cref="HiddenPathPolicy"/>
///     (a wildcard never matches a dot-prefixed name unless wildcards were allowed to). The expander itself
///     still rejects <c>!</c>, which is what keeps the ignore patterns and command-line patterns, where
///     negation is not supported, rejected: this class strips one leading <c>!</c> before delegating.
///     The idea of ordered matching is taken from ReviewMark's glob matcher, but nothing is shared with it,
///     and matching here is case-sensitive. Every pattern is compiled once when the list is created, so
///     matching a path only evaluates precompiled matchers. Instances are immutable and thread-safe.
/// </remarks>
internal sealed class OrderedGlobList
{
    /// <summary>
    ///     The character that marks a pattern as an exclusion.
    /// </summary>
    private const char NegationPrefix = '!';

    /// <summary>
    ///     The path separator used in relative paths and patterns on every platform.
    /// </summary>
    private const char Separator = '/';

    /// <summary>
    ///     The path segment that matches any number of directory levels.
    /// </summary>
    private const string RecursiveSegment = "**";

    /// <summary>
    ///     The suffix that marks an expanded pattern as covering a whole subtree.
    /// </summary>
    private const string SubtreeSuffix = "/**";

    /// <summary>
    ///     The name of an imaginary file used to probe whether a pattern covers every file beneath a directory.
    /// </summary>
    /// <remarks>
    ///     A control character cannot be written in a pattern and is not a plausible file name, so the only
    ///     patterns that match a path ending in it are those whose trailing recursive segment swallowed it.
    /// </remarks>
    private const string ProbeFileName = "\u0001";

    /// <summary>
    ///     The reason given when the first pattern of a list is a negation.
    /// </summary>
    private const string LeadingNegationReason =
        "'!' must follow a pattern it can remove files from; put it after the pattern it excludes";

    /// <summary>
    ///     The root directory the matchers are asked about; the matched paths are relative, so any absolute directory works.
    /// </summary>
    private static readonly string MatchRoot = Path.GetFullPath(Path.DirectorySeparatorChar.ToString());

    /// <summary>
    ///     The compiled patterns, in the order they were written.
    /// </summary>
    private readonly Entry[] _entries;

    /// <summary>
    ///     Whether wildcards may match hidden (dot-prefixed) names.
    /// </summary>
    private readonly bool _enableGlobDot;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OrderedGlobList"/> class from compiled entries.
    /// </summary>
    /// <remarks>
    ///     Private: instances are created by <see cref="Parse"/>, which validates the patterns first.
    /// </remarks>
    /// <param name="entries">The compiled entries. Must not be null.</param>
    /// <param name="enableGlobDot">Whether wildcards may match hidden names.</param>
    private OrderedGlobList(Entry[] entries, bool enableGlobDot)
    {
        _entries = entries;
        _enableGlobDot = enableGlobDot;
        HasNegation = entries.Any(static entry => entry.Negated);
        PositivePatterns =
        [
            .. entries.Where(static entry => !entry.Negated).SelectMany(static entry => entry.Expanded).Distinct(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    ///     Gets a value indicating whether the list has no patterns.
    /// </summary>
    /// <value><see langword="true"/> when nothing can be selected by this list.</value>
    internal bool IsEmpty => _entries.Length == 0;

    /// <summary>
    ///     Gets a value indicating whether the list contains at least one <c>!</c> pattern.
    /// </summary>
    /// <remarks>
    ///     A list without negation selects exactly what the same patterns select as plain includes, so the
    ///     file selector keeps its original, simpler walk for it.
    /// </remarks>
    /// <value><see langword="true"/> when some pattern excludes.</value>
    internal bool HasNegation { get; }

    /// <summary>
    ///     Gets the expanded matcher patterns of every plain (included) pattern, without duplicates.
    /// </summary>
    /// <remarks>
    ///     A superset of what the list can select, so the file selector can use it to prefilter the walk and
    ///     to decide which hidden directories may be entered; the exact decision is <see cref="IsMatch"/>.
    /// </remarks>
    /// <value>The matcher patterns in the order written; empty when the list is empty.</value>
    internal IReadOnlyList<string> PositivePatterns { get; }

    /// <summary>
    ///     Compiles a list of patterns.
    /// </summary>
    /// <remarks>
    ///     Every pattern is validated, so an unusable pattern fails before any file system access. Side
    ///     effects: none. Stateless and thread-safe.
    /// </remarks>
    /// <param name="patterns">The patterns in order, each optionally prefixed with <c>!</c>. Must not be null.</param>
    /// <param name="enableGlobDot">Whether wildcards may match hidden (dot-prefixed) names.</param>
    /// <returns>The compiled list; empty when <paramref name="patterns"/> is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="patterns"/> is null.</exception>
    /// <exception cref="InvalidGlobPatternException">
    ///     Thrown when a pattern is unsupported or malformed, or when the first pattern is a negation; the
    ///     exception names the pattern as it was written, including a leading <c>!</c>.
    /// </exception>
    internal static OrderedGlobList Parse(IReadOnlyList<string> patterns, bool enableGlobDot)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        // Compile each pattern in order; the first one may not be a negation because there is nothing to remove
        var entries = new Entry[patterns.Count];
        for (var index = 0; index < patterns.Count; index++)
        {
            entries[index] = Compile(patterns[index], index == 0);
        }

        return new OrderedGlobList(entries, enableGlobDot);
    }

    /// <summary>
    ///     Validates one pattern of a list without keeping it.
    /// </summary>
    /// <remarks>
    ///     Lets the settings loader report each bad pattern at its own position instead of only the first
    ///     bad pattern of the list. Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The pattern as written, optionally prefixed with <c>!</c>. Must not be null.</param>
    /// <param name="isFirst"><see langword="true"/> when the pattern is the first of its list.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
    /// <exception cref="InvalidGlobPatternException">Thrown when the pattern is unsupported or malformed, or is a leading negation.</exception>
    internal static void ValidateEntry(string pattern, bool isFirst)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _ = Compile(pattern, isFirst);
    }

    /// <summary>
    ///     Determines whether the list selects a file path.
    /// </summary>
    /// <remarks>
    ///     The patterns are examined from the last to the first and the first one that matches decides: a
    ///     negation rejects the path, a plain pattern accepts it. A plain pattern whose wildcard could only
    ///     reach the path through a hidden name does not count as matching, so a later negation never
    ///     matters for it and an earlier plain pattern may still select it. A negation applies to hidden
    ///     names too, because a pattern that excludes something should not be defeated by a dot. Pure and
    ///     thread-safe.
    /// </remarks>
    /// <param name="relativePath">The path relative to the root, using forward slashes. Must not be null.</param>
    /// <returns><see langword="true"/> when the last matching pattern is a plain pattern.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativePath"/> is null.</exception>
    internal bool IsMatch(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        // The last matching pattern decides, so look from the end
        for (var index = _entries.Length - 1; index >= 0; index--)
        {
            var entry = _entries[index];
            if (!entry.Matcher.Match(MatchRoot, relativePath).HasMatches)
            {
                continue;
            }

            // A negation always wins when it is the last to match
            if (entry.Negated)
            {
                return false;
            }

            // A plain pattern may reach a hidden name only through a literal dot, unless wildcards may match it
            if (_enableGlobDot || HiddenPathPolicy.IsPathPermitted(relativePath, entry.Expanded))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Determines whether no file beneath a directory can be selected, so the directory need not be read.
    /// </summary>
    /// <remarks>
    ///     The decision is deliberately conservative: answering <see langword="false"/> only costs time,
    ///     while answering <see langword="true"/> wrongly would silently drop files. The patterns are
    ///     examined from the last to the first. A plain pattern that could reach beneath the directory
    ///     vetoes the pruning, because it might bring back a file an earlier negation removed. A negation
    ///     excludes the whole subtree when one of its expanded forms ends in a recursive segment and matches
    ///     a probe file inside the directory; with the subtree forms every negation gets (see
    ///     <see cref="ExtendToSubtree"/>), that is any negation whose pattern names the directory itself.
    ///     Pure and thread-safe.
    /// </remarks>
    /// <param name="relativeDirectoryPath">The directory path relative to the root, using forward slashes. Must not be null.</param>
    /// <returns><see langword="true"/> when every file beneath the directory is excluded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="relativeDirectoryPath"/> is null.</exception>
    internal bool IsSubtreeExcluded(string relativeDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(relativeDirectoryPath);

        // Without a negation nothing can be excluded here; the file selector does not even ask
        if (!HasNegation)
        {
            return false;
        }

        var probe = relativeDirectoryPath + Separator + ProbeFileName;
        var directorySegments = relativeDirectoryPath.Split(Separator);
        for (var index = _entries.Length - 1; index >= 0; index--)
        {
            var entry = _entries[index];

            // A later plain pattern that can reach into the directory might re-include something
            if (!entry.Negated)
            {
                if (entry.PatternSegments.Any(pattern => CouldReachBeneath(directorySegments, pattern)))
                {
                    return false;
                }

                continue;
            }

            // The nearest negation that covers the whole subtree decides for every file beneath it
            if (entry.SubtreeMatcher is { } subtree && subtree.Match(MatchRoot, probe).HasMatches)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Compiles one pattern of a list.
    /// </summary>
    /// <remarks>
    ///     Strips one leading negation, delegates the rest to <see cref="GlobPatternExpander"/>, gives a negation
    ///     the subtree meaning of an exclude pattern, and
    ///     reports any rejection against the pattern as written, so a message shows what the user typed.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="pattern">The pattern as written. Must not be null.</param>
    /// <param name="isFirst"><see langword="true"/> when the pattern is the first of its list.</param>
    /// <returns>The compiled entry.</returns>
    /// <exception cref="InvalidGlobPatternException">Thrown when the pattern is unsupported, malformed, or a leading negation.</exception>
    private static Entry Compile(string pattern, bool isFirst)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        // A negation needs an earlier pattern to remove files from
        var negated = pattern.StartsWith(NegationPrefix);
        if (negated && isFirst)
        {
            throw new InvalidGlobPatternException(pattern, LeadingNegationReason);
        }

        // Expand the remainder with the shared rules; the expander rejects any remaining '!'
        IReadOnlyList<string> expanded;
        try
        {
            expanded = GlobPatternExpander.Expand(negated ? pattern[1..] : pattern);
        }
        catch (InvalidGlobPatternException exception)
        {
            throw new InvalidGlobPatternException(pattern, exception.Reason);
        }

        // A negation excludes like an ignore pattern does, so a path-like pattern names a directory as well as a file
        if (negated)
        {
            expanded = ExtendToSubtree(expanded);
        }

        // Build the whole-pattern matcher; case-sensitive so a selection is identical on every platform
        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddIncludePatterns(expanded);

        // A negation also gets a matcher for only its subtree forms, which tell whether it covers a directory
        Matcher? subtreeMatcher = null;
        if (negated)
        {
            var subtreeForms = expanded.Where(IsSubtreeForm).ToList();
            if (subtreeForms.Count > 0)
            {
                subtreeMatcher = new Matcher(StringComparison.Ordinal);
                subtreeMatcher.AddIncludePatterns(subtreeForms);
            }
        }

        // A plain pattern keeps its segments so a directory can be tested against its prefix
        var segments = negated ? Array.Empty<string[]>() : expanded.Select(static form => form.Split(Separator)).ToArray();
        return new Entry(negated, expanded, matcher, subtreeMatcher, segments);
    }

    /// <summary>
    ///     Gives each expanded form of a negation the meaning an exclude pattern has in <c>ignorePaths</c>.
    /// </summary>
    /// <remarks>
    ///     An exclude pattern such as <c>docs/a</c> removes a file of that name and also the directory of that
    ///     name with everything below it, whereas the same pattern as an include selects only a file. A negation
    ///     is an exclude, so every form that does not already cover a subtree gets a second form that does.
    ///     Forms that already end in a recursive segment, which is what a bare name or a trailing slash expands
    ///     to, are unchanged. Stateless and thread-safe.
    /// </remarks>
    /// <param name="expanded">The expanded forms of the negated pattern. Must not be null.</param>
    /// <returns>The forms, each followed by its subtree form where it had none, without duplicates.</returns>
    private static IReadOnlyList<string> ExtendToSubtree(IReadOnlyList<string> expanded) =>
    [
        .. expanded
            .SelectMany(static form => IsSubtreeForm(form) ? [form] : new[] { form, form + SubtreeSuffix })
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>
    ///     Determines whether an expanded pattern matches everything beneath some directory.
    /// </summary>
    /// <remarks>
    ///     True for <c>**</c> and for any pattern ending in <c>/**</c>, because the trailing recursive
    ///     segment consumes at least one more path segment and then any number of them. Stateless and thread-safe.
    /// </remarks>
    /// <param name="expanded">The expanded pattern. Must not be null.</param>
    /// <returns><see langword="true"/> when the pattern covers whole subtrees.</returns>
    private static bool IsSubtreeForm(string expanded) =>
        expanded == RecursiveSegment || expanded.EndsWith(SubtreeSuffix, StringComparison.Ordinal);

    /// <summary>
    ///     Determines whether a pattern could match some file beneath a directory.
    /// </summary>
    /// <remarks>
    ///     A prefix alignment of the directory's segments with the pattern's, as in
    ///     <see cref="HiddenPathPolicy"/> but without the hidden-name rule. Ignoring that rule makes the
    ///     answer a superset of the truth, which is what pruning needs: an unnecessary <see langword="true"/>
    ///     only keeps a directory in the walk. The cost is bounded by the product of the two lengths however
    ///     many recursive segments the pattern has. Stateless and thread-safe; allocates one table per call.
    /// </remarks>
    /// <param name="directory">The directory's segments. Must not be null.</param>
    /// <param name="pattern">The pattern's segments. Must not be null.</param>
    /// <returns><see langword="true"/> when the pattern may match beneath the directory.</returns>
    private static bool CouldReachBeneath(string[] directory, string[] pattern)
    {
        // reachable[i, j] is true when the first i directory segments can be consumed by the first j pattern segments
        var reachable = new bool[directory.Length + 1, pattern.Length + 1];
        reachable[0, 0] = true;

        for (var directoryIndex = 0; directoryIndex <= directory.Length; directoryIndex++)
        {
            for (var patternIndex = 0; patternIndex < pattern.Length; patternIndex++)
            {
                if (!reachable[directoryIndex, patternIndex])
                {
                    continue;
                }

                if (pattern[patternIndex] == RecursiveSegment)
                {
                    // A recursive segment spans zero levels, or one level and then possibly more
                    reachable[directoryIndex, patternIndex + 1] = true;
                    if (directoryIndex < directory.Length)
                    {
                        reachable[directoryIndex + 1, patternIndex] = true;
                    }
                }
                else if (directoryIndex < directory.Length && WildcardMatches(directory[directoryIndex], pattern[patternIndex]))
                {
                    // Any other segment consumes exactly one matching directory segment
                    reachable[directoryIndex + 1, patternIndex + 1] = true;
                }
            }
        }

        // The directory is consumed once any pattern prefix has absorbed all of it
        for (var patternIndex = 0; patternIndex <= pattern.Length; patternIndex++)
        {
            if (reachable[directory.Length, patternIndex])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Matches a name against a pattern segment in which <c>*</c> stands for any run of characters.
    /// </summary>
    /// <remarks>
    ///     Uses the two-pointer algorithm that remembers the last wildcard so a mismatch backs up to it,
    ///     which is linear in practice and needs no recursion. Comparison is ordinal. Stateless and thread-safe.
    /// </remarks>
    /// <param name="name">The directory segment. Must not be null.</param>
    /// <param name="pattern">The pattern segment. Must not be null.</param>
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
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
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
        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    /// <summary>
    ///     One compiled pattern of the list.
    /// </summary>
    /// <param name="Negated"><see langword="true"/> when the pattern was written with a leading <c>!</c>.</param>
    /// <param name="Expanded">The matcher patterns the written pattern expands to.</param>
    /// <param name="Matcher">A matcher holding all of <paramref name="Expanded"/>.</param>
    /// <param name="SubtreeMatcher">For a negation, a matcher of only the subtree forms; null when it has none.</param>
    /// <param name="PatternSegments">For a plain pattern, the segments of each expanded form; empty for a negation.</param>
    private sealed record Entry(
        bool Negated,
        IReadOnlyList<string> Expanded,
        Matcher Matcher,
        Matcher? SubtreeMatcher,
        string[][] PatternSegments);
}
