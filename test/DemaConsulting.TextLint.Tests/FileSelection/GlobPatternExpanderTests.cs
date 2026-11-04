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
///     Unit tests for the <see cref="GlobPatternExpander"/> class.
/// </summary>
/// <remarks>
///     The expander is pure string logic, so these tests need no file system and no test doubles.
/// </remarks>
public class GlobPatternExpanderTests
{
    /// <summary>
    ///     Test that a brace group expands to one pattern per alternative.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_SingleBraceGroup_ReturnsOnePatternPerAlternative()
    {
        // Arrange: a pattern with one brace group

        // Act: expand it
        var result = GlobPatternExpander.Expand("src/*.{md,cs}");

        // Assert: one pattern per alternative
        Assert.Equal(["src/*.md", "src/*.cs"], result);
    }

    /// <summary>
    ///     Test that several brace groups expand to their cartesian product.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_MultipleBraceGroups_ReturnsCartesianProduct()
    {
        // Arrange: a pattern with two brace groups

        // Act: expand it
        var result = GlobPatternExpander.Expand("{a,b}/{x,y}.md");

        // Assert: every combination, first group varying slowest
        Assert.Equal(["a/x.md", "a/y.md", "b/x.md", "b/y.md"], result);
    }

    /// <summary>
    ///     Test that nested brace groups expand all combinations.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_NestedBraceGroups_ExpandsAllCombinations()
    {
        // Arrange: a pattern with a group nested in an alternative

        // Act: expand it
        var result = GlobPatternExpander.Expand("src/*.{a,b{c,d}}");

        // Assert: the inner group is expanded within its alternative
        Assert.Equal(["src/*.a", "src/*.bc", "src/*.bd"], result);
    }

    /// <summary>
    ///     Test that an empty alternative yields the pattern without it.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_EmptyAlternative_ReturnsPatternWithoutIt()
    {
        // Arrange: a group whose first alternative is empty

        // Act: expand it
        var result = GlobPatternExpander.Expand("docs/file{,2}.md");

        // Assert: both the bare and the suffixed pattern are produced
        Assert.Equal(["docs/file.md", "docs/file2.md"], result);
    }

    /// <summary>
    ///     Test that a brace group without alternatives stays literal.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_BraceWithoutComma_LeavesGroupLiteral()
    {
        // Arrange: a group with no comma

        // Act: expand it
        var result = GlobPatternExpander.Expand("src/{name}.md");

        // Assert: the group is left as written
        Assert.Equal(["src/{name}.md"], result);
    }

    /// <summary>
    ///     Test that alternatives of different shape are anchored independently.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_AlternativesOfDifferentShape_AreAnchoredIndependently()
    {
        // Arrange: one alternative with a separator and one bare name

        // Act: expand it
        var result = GlobPatternExpander.Expand("{src/*.md,README.md}");

        // Assert: the first stays anchored and the second matches at any depth
        Assert.Equal(["src/*.md", "**/README.md", "**/README.md/**"], result);
    }

    /// <summary>
    ///     Test that a bare name matches a file or directory at any depth.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_BareName_MatchesFileOrDirectoryAtAnyDepth()
    {
        // Arrange: a pattern with no separator

        // Act: expand it
        var result = GlobPatternExpander.Expand("node_modules");

        // Assert: the name as a file and as a directory subtree, at any depth
        Assert.Equal(["**/node_modules", "**/node_modules/**"], result);
    }

    /// <summary>
    ///     Test that a pattern with a separator stays anchored.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_PatternWithSeparator_StaysAnchored()
    {
        // Arrange: a pattern with an inner separator

        // Act: expand it
        var result = GlobPatternExpander.Expand("docs/build");

        // Assert: used exactly as written
        Assert.Equal(["docs/build"], result);
    }

    /// <summary>
    ///     Test that a trailing slash alone means a directory at any depth.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_TrailingSlashOnly_MatchesDirectoryAtAnyDepth()
    {
        // Arrange: a name with only a trailing slash

        // Act: expand it
        var result = GlobPatternExpander.Expand("build/");

        // Assert: only the directory subtree form, at any depth
        Assert.Equal(["**/build/**"], result);
    }

    /// <summary>
    ///     Test that a trailing slash on an anchored pattern selects the subtree.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_TrailingSlashOnAnchoredPattern_SelectsSubtree()
    {
        // Arrange: an anchored directory pattern

        // Act: expand it
        var result = GlobPatternExpander.Expand("docs/build/");

        // Assert: the subtree beneath the directory
        Assert.Equal(["docs/build/**"], result);
    }

    /// <summary>
    ///     Test that a leading slash anchors a bare name at the root.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_LeadingSlash_AnchorsToRoot()
    {
        // Arrange: a bare name with a leading slash

        // Act: expand it
        var result = GlobPatternExpander.Expand("/README.md");

        // Assert: the name at the root only
        Assert.Equal(["README.md"], result);
    }

    /// <summary>
    ///     Test that a leading dot-slash is removed and anchors at the root.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_LeadingDotSlash_IsStrippedAndAnchors()
    {
        // Arrange: a bare name with a leading ./

        // Act: expand it
        var result = GlobPatternExpander.Expand("./README.md");

        // Assert: the prefix is removed and the name is anchored
        Assert.Equal(["README.md"], result);
    }

    /// <summary>
    ///     Test that current-directory segments are removed.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_CurrentDirectorySegment_IsRemoved()
    {
        // Arrange: a pattern with a . segment in the middle

        // Act: expand it
        var result = GlobPatternExpander.Expand("docs/./guide.md");

        // Assert: the segment is dropped
        Assert.Equal(["docs/guide.md"], result);
    }

    /// <summary>
    ///     Test that a recursive pattern is unchanged.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_RecursivePattern_IsUnchanged()
    {
        // Arrange: a typical recursive include

        // Act: expand it
        var result = GlobPatternExpander.Expand("**/*.md");

        // Assert: used exactly as written
        Assert.Equal(["**/*.md"], result);
    }

    /// <summary>
    ///     Test that repeated recursive segments are merged.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_RepeatedRecursiveSegments_AreMerged()
    {
        // Arrange: a pattern with two adjacent recursive segments

        // Act: expand it
        var result = GlobPatternExpander.Expand("src/**/**/*.cs");

        // Assert: they collapse to one
        Assert.Equal(["src/**/*.cs"], result);
    }

    /// <summary>
    ///     Test that a lone recursive segment is unchanged.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_LoneRecursiveSegment_IsUnchanged()
    {
        // Arrange: a pattern that means everything

        // Act: expand it
        var result = GlobPatternExpander.Expand("**");

        // Assert: used exactly as written
        Assert.Equal(["**"], result);
    }

    /// <summary>
    ///     Test that an empty pattern is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_EmptyPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: an empty pattern is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand(string.Empty));
    }

    /// <summary>
    ///     Test that a white-space pattern is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_WhitespacePattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a pattern of spaces is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("   "));
    }

    /// <summary>
    ///     Test that a negated pattern is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_NegatedPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a leading ! is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("!docs/**"));
    }

    /// <summary>
    ///     Test that a pattern with a backslash is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_BackslashPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a backslash separator is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("docs\\*.md"));
    }

    /// <summary>
    ///     Test that a pattern using the question-mark wildcard is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_QuestionMarkPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: the unsupported ? wildcard is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("file?.md"));
    }

    /// <summary>
    ///     Test that a drive-rooted pattern is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_DriveRootedPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a Windows drive prefix is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("C:/docs/**"));
    }

    /// <summary>
    ///     Test that a double-slash root is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_DoubleSlashRoot_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a network-style root is rejected as an empty segment
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("//server/share/**"));
    }

    /// <summary>
    ///     Test that a pattern with a parent segment is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_ParentSegment_ThrowsInvalidGlobPattern()
    {
        // Arrange: no setup required

        // Act / Assert: a .. segment is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("../outside/**"));
    }

    /// <summary>
    ///     Test that a parent segment produced by brace expansion is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_ParentSegmentFromBraces_ThrowsInvalidGlobPattern()
    {
        // Arrange: a pattern that only becomes a parent reference after expansion

        // Act / Assert: the expanded .. segment is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("{src,..}/**"));
    }

    /// <summary>
    ///     Test that a missing closing brace is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_UnclosedBrace_ThrowsInvalidGlobPattern()
    {
        // Arrange: a pattern with a missing closing brace

        // Act / Assert: the pattern is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("src/*.{md,cs"));
    }

    /// <summary>
    ///     Test that a missing opening brace is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_UnopenedBrace_ThrowsInvalidGlobPattern()
    {
        // Arrange: a pattern with a missing opening brace

        // Act / Assert: the pattern is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("src/*.md,cs}"));
    }

    /// <summary>
    ///     Test that a pattern that expands without bound is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_ExcessiveExpansion_ThrowsInvalidGlobPattern()
    {
        // Arrange: nine two-way groups expand to 512 patterns, more than allowed
        var pattern = string.Concat(Enumerable.Repeat("{a,b}", 9));

        // Act / Assert: the expansion is rejected
        Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand(pattern));
    }

    /// <summary>
    ///     Test that the exception names the pattern and the reason.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_ExceptionMessage_NamesPattern()
    {
        // Arrange: a negated pattern

        // Act: expand it and capture the exception
        var exception = Assert.Throws<InvalidGlobPatternException>(() => GlobPatternExpander.Expand("!docs/**"));

        // Assert: the pattern is exposed and named in the message with the reason
        Assert.Multiple(
            () => Assert.Equal("!docs/**", exception.Pattern),
            () => Assert.Contains("'!docs/**'", exception.Message),
            () => Assert.Contains("negated", exception.Message));
    }

    /// <summary>
    ///     Test that a null pattern is rejected.
    /// </summary>
    [Fact]
    public void GlobPatternExpander_Expand_NullPattern_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: a null pattern is a programming error
        Assert.Throws<ArgumentNullException>(() => GlobPatternExpander.Expand(null!));
    }
}
