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

using DemaConsulting.TextLint.FileSelection;

namespace DemaConsulting.TextLint.Tests.FileSelection;

/// <summary>
///     Unit tests for the <see cref="OrderedGlobList"/> class.
/// </summary>
/// <remarks>
///     The list is tested without a file system: it decides about relative path strings, so every rule
///     (last match wins, re-inclusion, leading negation, hidden names, case sensitivity, and the pruning
///     decision for directories) can be checked exhaustively and deterministically. The real pattern
///     expander and hidden-path policy it builds on are used, because they are pure.
/// </remarks>
public class OrderedGlobListTests
{
    /// <summary>
    ///     Test that a pattern list without negation selects what its patterns match.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_PlainPatterns_MatchesAnyPattern()
    {
        // Arrange: two plain patterns
        var list = OrderedGlobList.Parse(["docs/**/*.md", "README.md"], false);

        // Act / Assert: either pattern selects a path, and another path is not selected
        Assert.Multiple(
            () => Assert.True(list.IsMatch("docs/a/guide.md")),
            () => Assert.True(list.IsMatch("README.md")),
            () => Assert.False(list.IsMatch("src/a.cs")),
            () => Assert.False(list.HasNegation));
    }

    /// <summary>
    ///     Test that a negation removes what an earlier pattern selected.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_NegationAfterInclude_RemovesFiles()
    {
        // Arrange: markdown files, except those beneath docs
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**"], false);

        // Act / Assert: files outside docs stay and files inside docs are removed
        Assert.Multiple(
            () => Assert.True(list.IsMatch("README.md")),
            () => Assert.False(list.IsMatch("docs/guide.md")),
            () => Assert.True(list.HasNegation));
    }

    /// <summary>
    ///     Test that a later include brings back a file an earlier negation removed.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_IncludeAfterNegation_ReincludesFiles()
    {
        // Arrange: markdown files, except docs, except docs/keep which is brought back
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**", "docs/keep/**"], false);

        // Act / Assert: the last matching pattern decides
        Assert.Multiple(
            () => Assert.True(list.IsMatch("README.md")),
            () => Assert.False(list.IsMatch("docs/other/a.md")),
            () => Assert.True(list.IsMatch("docs/keep/a.md")));
    }

    /// <summary>
    ///     Test that a negation after a re-include removes the file again.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_NegationAfterReinclude_RemovesAgain()
    {
        // Arrange: a pattern, a negation, a re-include, and a final negation
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**", "docs/keep/**", "!**/draft.md"], false);

        // Act / Assert: the final negation wins over the re-include
        Assert.Multiple(
            () => Assert.True(list.IsMatch("docs/keep/final.md")),
            () => Assert.False(list.IsMatch("docs/keep/draft.md")));
    }

    /// <summary>
    ///     Test that a negation with a brace group removes every alternative.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_NegationWithBraceExpansion_RemovesEveryAlternative()
    {
        // Arrange: a negation that names two directories with braces
        var list = OrderedGlobList.Parse(["**/*.md", "!{docs,legacy}/**"], false);

        // Act / Assert: both are removed and others stay
        Assert.Multiple(
            () => Assert.False(list.IsMatch("docs/a.md")),
            () => Assert.False(list.IsMatch("legacy/a.md")),
            () => Assert.True(list.IsMatch("src/a.md")));
    }

    /// <summary>
    ///     Test that a bare name in a negation removes the name at any depth, as a directory or a file.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_BareNameNegation_RemovesAtAnyDepth()
    {
        // Arrange: a negated bare directory name
        var list = OrderedGlobList.Parse(["**/*.md", "!vendor"], false);

        // Act / Assert: the directory is removed wherever it is, and a similar name is not
        Assert.Multiple(
            () => Assert.False(list.IsMatch("vendor/a.md")),
            () => Assert.False(list.IsMatch("src/vendor/a.md")),
            () => Assert.True(list.IsMatch("src/vendors/a.md")));
    }

    /// <summary>
    ///     Test that a leading slash anchors a negation at the root.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_AnchoredNegation_OnlyRemovesAtRoot()
    {
        // Arrange: a negation anchored at the root
        var list = OrderedGlobList.Parse(["**/*.md", "!/build/**"], false);

        // Act / Assert: only the root-level build directory is removed
        Assert.Multiple(
            () => Assert.False(list.IsMatch("build/a.md")),
            () => Assert.True(list.IsMatch("src/build/a.md")));
    }

    /// <summary>
    ///     Test that a trailing slash limits a negation to a directory's subtree.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_TrailingSlashNegation_RemovesDirectorySubtree()
    {
        // Arrange: a negation with a trailing slash
        var list = OrderedGlobList.Parse(["**/*", "!obj/"], false);

        // Act / Assert: the directory's files are removed and a file named obj is not
        Assert.Multiple(
            () => Assert.False(list.IsMatch("obj/a.txt")),
            () => Assert.True(list.IsMatch("src/obj")));
    }

    /// <summary>
    ///     Test that matching is case-sensitive for both plain patterns and negations.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_DifferentCase_DoesNotMatch()
    {
        // Arrange: a plain pattern and a negation in lower case
        var list = OrderedGlobList.Parse(["docs/**", "!docs/private/**"], false);

        // Act / Assert: a different case does not match the plain pattern, and does not match the negation
        Assert.Multiple(
            () => Assert.False(list.IsMatch("Docs/a.md")),
            () => Assert.True(list.IsMatch("docs/Private/a.md")),
            () => Assert.False(list.IsMatch("docs/private/a.md")));
    }

    /// <summary>
    ///     Test that a wildcard include does not match a hidden name, but a literal one does.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_HiddenName_FollowsHiddenRulePerPattern()
    {
        // Arrange: a wildcard pattern and a literal hidden pattern
        var wildcard = OrderedGlobList.Parse(["**/*.md"], false);
        var literal = OrderedGlobList.Parse([".github/**/*.md"], false);

        // Act / Assert: the wildcard does not reach a hidden directory, and the literal pattern does
        Assert.Multiple(
            () => Assert.False(wildcard.IsMatch(".github/a.md")),
            () => Assert.True(literal.IsMatch(".github/a.md")),
            () => Assert.False(literal.IsMatch(".hidden/a.md")));
    }

    /// <summary>
    ///     Test that a hidden name matches a wildcard when hidden names are enabled.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_HiddenNameAndGlobDot_Matches()
    {
        // Arrange: a wildcard pattern with hidden names enabled
        var list = OrderedGlobList.Parse(["**/*.md", "!.cache/**"], true);

        // Act / Assert: a hidden directory matches, unless it is negated
        Assert.Multiple(
            () => Assert.True(list.IsMatch(".github/a.md")),
            () => Assert.False(list.IsMatch(".cache/a.md")));
    }

    /// <summary>
    ///     Test that a negation applies to hidden names even when the plain pattern could not match them.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_HiddenNameNotReachedByInclude_IsNotSelected()
    {
        // Arrange: an include whose wildcard cannot reach hidden names, followed by a re-include of the same wildcard
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**", "**/*.md"], false);

        // Act / Assert: the hidden file is still not selected, and a visible one beneath docs is selected again
        Assert.Multiple(
            () => Assert.False(list.IsMatch(".git/a.md")),
            () => Assert.True(list.IsMatch("docs/a.md")));
    }

    /// <summary>
    ///     Test that an empty list selects nothing and reports itself empty.
    /// </summary>
    [Fact]
    public void OrderedGlobList_Parse_EmptyList_IsEmptyAndMatchesNothing()
    {
        // Arrange / Act: compile an empty list
        var list = OrderedGlobList.Parse([], false);

        // Assert: it is empty and selects nothing
        Assert.Multiple(
            () => Assert.True(list.IsEmpty),
            () => Assert.False(list.HasNegation),
            () => Assert.Empty(list.PositivePatterns),
            () => Assert.False(list.IsMatch("a.md")));
    }

    /// <summary>
    ///     Test that the positive patterns are the expanded plain patterns, without the negations.
    /// </summary>
    [Fact]
    public void OrderedGlobList_PositivePatterns_PlainAndNegated_ListsOnlyPlainExpansions()
    {
        // Arrange / Act: a list with a brace group, a negation, and a duplicate
        var list = OrderedGlobList.Parse(["{a,b}/**", "!c/**", "a/**"], false);

        // Assert: the plain expansions appear once and the negation does not appear
        Assert.Equal(["a/**", "b/**"], list.PositivePatterns);
    }

    /// <summary>
    ///     Test that a first pattern that is a negation is rejected naming the pattern as written.
    /// </summary>
    [Fact]
    public void OrderedGlobList_Parse_LeadingNegation_ThrowsNamingPattern()
    {
        // Act: compile a list that starts with a negation
        var exception = Assert.Throws<InvalidGlobPatternException>(() => OrderedGlobList.Parse(["!docs/**", "**/*.md"], false));

        // Assert: the exception names the pattern as written and says what to do
        Assert.Multiple(
            () => Assert.Equal("!docs/**", exception.Pattern),
            () => Assert.Contains("must follow a pattern", exception.Message));
    }

    /// <summary>
    ///     Test that a double negation, a lone negation, and an unusable negated pattern are rejected.
    /// </summary>
    [Theory]
    [InlineData("!!docs/**")]
    [InlineData("!")]
    [InlineData("!docs/?.md")]
    [InlineData("!docs\\a")]
    [InlineData("!{a,b")]
    [InlineData("!../x")]
    public void OrderedGlobList_Parse_UnusableNegatedPattern_ThrowsNamingPattern(string negated)
    {
        // Act: compile a list whose second pattern is unusable
        var exception = Assert.Throws<InvalidGlobPatternException>(() => OrderedGlobList.Parse(["**/*.md", negated], false));

        // Assert: the exception names the entry as written, including the leading negation
        Assert.Equal(negated, exception.Pattern);
    }

    /// <summary>
    ///     Test that an unusable plain pattern is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void OrderedGlobList_Parse_UnusablePlainPattern_ThrowsNamingPattern()
    {
        // Act: compile a list with the unsupported question-mark wildcard
        var exception = Assert.Throws<InvalidGlobPatternException>(() => OrderedGlobList.Parse(["docs/?.md"], false));

        // Assert: the pattern is named
        Assert.Equal("docs/?.md", exception.Pattern);
    }

    /// <summary>
    ///     Test that a null list is rejected.
    /// </summary>
    [Fact]
    public void OrderedGlobList_Parse_NullList_ThrowsArgumentNullException()
    {
        // Act / Assert: a null list is a programming error
        Assert.Throws<ArgumentNullException>(() => OrderedGlobList.Parse(null!, false));
    }

    /// <summary>
    ///     Test that one entry can be validated on its own for the first position and later positions.
    /// </summary>
    [Fact]
    public void OrderedGlobList_ValidateEntry_FirstAndLaterPositions_AppliesLeadingNegationRule()
    {
        // Act / Assert: a negation is fine later and rejected first, and a plain pattern is fine anywhere
        OrderedGlobList.ValidateEntry("!docs/**", isFirst: false);
        OrderedGlobList.ValidateEntry("docs/**", isFirst: true);
        var exception = Assert.Throws<InvalidGlobPatternException>(() => OrderedGlobList.ValidateEntry("!docs/**", isFirst: true));
        Assert.Equal("!docs/**", exception.Pattern);
    }

    /// <summary>
    ///     Test that validating an unusable entry throws and a null entry is rejected.
    /// </summary>
    [Fact]
    public void OrderedGlobList_ValidateEntry_UnusableAndNullEntries_Throw()
    {
        // Act / Assert: the unsupported wildcard throws a pattern error, and null is an argument error
        Assert.Throws<InvalidGlobPatternException>(() => OrderedGlobList.ValidateEntry("a?", isFirst: true));
        Assert.Throws<ArgumentNullException>(() => OrderedGlobList.ValidateEntry(null!, isFirst: true));
    }

    /// <summary>
    ///     Test that matching rejects a null path and the pruning decision rejects a null directory.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatchAndIsSubtreeExcluded_NullArgument_ThrowArgumentNullException()
    {
        // Arrange: a list
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**"], false);

        // Act / Assert: both decisions reject null
        Assert.Throws<ArgumentNullException>(() => list.IsMatch(null!));
        Assert.Throws<ArgumentNullException>(() => list.IsSubtreeExcluded(null!));
    }

    /// <summary>
    ///     Test that a negated bare name excludes the whole subtree of a directory of that name.
    /// </summary>
    [Theory]
    [InlineData("node_modules")]
    [InlineData("src/node_modules")]
    [InlineData("a/b/node_modules")]
    public void OrderedGlobList_IsSubtreeExcluded_NegatedBareName_ExcludesDirectoryAtAnyDepth(string directory)
    {
        // Arrange: markdown files except any node_modules directory
        var list = OrderedGlobList.Parse(["**/*.md", "!node_modules"], false);

        // Act / Assert: such a directory is pruned and an unrelated one is not
        Assert.Multiple(
            () => Assert.True(list.IsSubtreeExcluded(directory)),
            () => Assert.False(list.IsSubtreeExcluded("src")));
    }

    /// <summary>
    ///     Test that a directory beneath an excluded subtree is excluded as well.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_DirectoryBeneathExcludedSubtree_IsExcluded()
    {
        // Arrange: a negated directory subtree
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**"], false);

        // Act / Assert: the directory and its descendants are pruned, while a sibling is not
        Assert.Multiple(
            () => Assert.True(list.IsSubtreeExcluded("docs")),
            () => Assert.True(list.IsSubtreeExcluded("docs/a/b")),
            () => Assert.False(list.IsSubtreeExcluded("other")));
    }

    /// <summary>
    ///     Test that a later include that can reach into a directory vetoes the pruning.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_LaterIncludeCanReach_IsNotExcluded()
    {
        // Arrange: docs is removed, and a later include brings back docs/keep
        var list = OrderedGlobList.Parse(["**/*.md", "!docs", "docs/keep/**"], false);

        // Act / Assert: docs must still be walked, docs/keep too, but docs/other can be pruned
        Assert.Multiple(
            () => Assert.False(list.IsSubtreeExcluded("docs")),
            () => Assert.False(list.IsSubtreeExcluded("docs/keep")),
            () => Assert.True(list.IsSubtreeExcluded("docs/other")));
    }

    /// <summary>
    ///     Test that a negation of particular files never prunes a directory.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_FileFormNegation_IsNotExcluded()
    {
        // Arrange: a negation that names markdown files beneath docs
        var list = OrderedGlobList.Parse(["**/*", "!docs/**/*.md"], false);

        // Act / Assert: the directory still holds files that are selected, so it is walked
        Assert.False(list.IsSubtreeExcluded("docs"));
    }

    /// <summary>
    ///     Test that a later include that cannot reach beneath a directory does not veto the pruning.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_UnrelatedLaterInclude_StillExcluded()
    {
        // Arrange: docs/a is removed, and a later include is limited to another directory
        var list = OrderedGlobList.Parse(["docs/**", "!docs/a/**", "x/**"], false);

        // Act / Assert: docs/a is pruned and x is untouched
        Assert.Multiple(
            () => Assert.True(list.IsSubtreeExcluded("docs/a")),
            () => Assert.False(list.IsSubtreeExcluded("x")),
            () => Assert.False(list.IsSubtreeExcluded("docs")));
    }

    /// <summary>
    ///     Test that an anchored negation without a trailing slash names only that path and so never prunes.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_AnchoredNegationWithoutSlash_IsNotExcluded()
    {
        // Arrange: a negation that, as an anchored pattern, matches only a file named docs/a
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/a"], false);

        // Act / Assert: files beneath a directory docs/a stay selected and the directory is walked
        Assert.Multiple(
            () => Assert.False(list.IsSubtreeExcluded("docs/a")),
            () => Assert.True(list.IsMatch("docs/a/b.md")));
    }

    /// <summary>
    ///     Test that a later include with a leading wildcard always vetoes the pruning.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_LaterRecursiveInclude_IsNotExcluded()
    {
        // Arrange: a negated directory followed by a recursive include of markdown files
        var list = OrderedGlobList.Parse(["**/*.md", "!docs", "**/*.md"], false);

        // Act / Assert: the later include can reach anywhere, so nothing is pruned
        Assert.False(list.IsSubtreeExcluded("docs"));
    }

    /// <summary>
    ///     Test that a list without negation never prunes.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_NoNegation_IsNotExcluded()
    {
        // Arrange: a plain list
        var list = OrderedGlobList.Parse(["docs/**"], false);

        // Act / Assert: nothing is excluded
        Assert.False(list.IsSubtreeExcluded("src"));
    }

    /// <summary>
    ///     Test that the pruning decision never disagrees with the exact decision for any file.
    /// </summary>
    /// <remarks>
    ///     A pruned directory must hold no selected file: this is the safety property of pruning. It is checked
    ///     over every combination of a small set of lists, directories, and files, which is a differential test
    ///     against the exact per-file decision.
    /// </remarks>
    [Fact]
    public void OrderedGlobList_IsSubtreeExcluded_AgreesWithIsMatchForEveryFile()
    {
        // Arrange: several lists and a tree of paths that touches every pattern shape
        string[][] lists =
        [
            ["**/*.md", "!docs"],
            ["**/*.md", "!docs", "docs/keep/**"],
            ["**/*.md", "!docs/**", "docs/a/**", "!docs/a/b/**"],
            ["**/*", "!{docs,src}/**", "src/x/**"],
            ["docs/**", "!docs/a/**", "x/**"],
            ["**/*.md", "!**/build/**", "build/keep.md"],
            ["**/*", "!/docs/*/", "docs/z/a.md"],
            ["**/*.md", "!docs/**/*.md", "docs/a/b.md"],
        ];
        string[] directories = ["docs", "docs/a", "docs/a/b", "docs/keep", "docs/z", "src", "src/x", "x", "build", "src/build"];
        string[] files =
        [
            "a.md",
            "docs/a.md",
            "docs/a/b.md",
            "docs/a/b/c.md",
            "docs/keep/a.md",
            "docs/z/a.md",
            "docs/z/b.md",
            "src/a.md",
            "src/x/a.md",
            "x/a.md",
            "build/keep.md",
            "build/other.md",
            "src/build/a.md",
        ];

        foreach (var patterns in lists)
        {
            var list = OrderedGlobList.Parse(patterns, false);
            foreach (var directory in directories.Where(list.IsSubtreeExcluded))
            {
                // Act / Assert: no selected file lies beneath a pruned directory
                var selected = files.Where(file => file.StartsWith(directory + "/", StringComparison.Ordinal) && list.IsMatch(file));
                Assert.Empty(selected);
            }
        }
    }

    /// <summary>
    ///     Test that a compiled list can be used from several threads at once.
    /// </summary>
    [Fact]
    public void OrderedGlobList_IsMatch_ConcurrentUse_GivesTheSameAnswers()
    {
        // Arrange: a list and the answers it gives on one thread
        var list = OrderedGlobList.Parse(["**/*.md", "!docs/**", "docs/keep/**"], false);
        string[] paths = ["a.md", "docs/a.md", "docs/keep/a.md", "src/b.md", "src/b.cs"];
        var expected = paths.Select(list.IsMatch).ToArray();

        // Act: ask the same questions from many threads
        var results = new bool[8][];
        Parallel.For(0, results.Length, index => results[index] = [.. paths.Select(list.IsMatch)]);

        // Assert: every thread got the same answers
        Assert.All(results, result => Assert.Equal(expected, result));
    }
}
