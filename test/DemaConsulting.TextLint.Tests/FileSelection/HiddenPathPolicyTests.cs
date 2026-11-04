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
///     Unit tests for the <see cref="HiddenPathPolicy"/> class.
/// </summary>
/// <remarks>
///     The policy is pure logic over strings, so these tests need no file system and no test doubles.
/// </remarks>
public class HiddenPathPolicyTests
{
    /// <summary>
    ///     Test that a path with no hidden segment is permitted by any pattern set.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_NoHiddenSegment_IsPermitted()
    {
        // Arrange: an ordinary path and a wildcard pattern

        // Act: ask whether the path is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted("src/docs/guide.md", ["**/*.md"]);

        // Assert: it is permitted
        Assert.True(permitted);
    }

    /// <summary>
    ///     Test that a wildcard does not match a hidden file name.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenFile_IsNotPermitted()
    {
        // Arrange: a wildcard pattern and a hidden file

        // Act: ask whether the hidden file is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".hidden.md", ["**/*.md"]);

        // Assert: the wildcard does not reach it
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that a leading star does not match a hidden file name.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_LeadingStarAndHiddenFile_IsNotPermitted()
    {
        // Arrange: a pattern that is a single star and a hidden file in the root

        // Act: ask whether the hidden file is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".env", ["*"]);

        // Assert: the star does not match the leading dot
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that a wildcard does not descend through a hidden directory.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_WildcardAndHiddenDirectory_IsNotPermitted()
    {
        // Arrange: a recursive pattern and a file inside a hidden directory

        // Act: ask whether the file is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted("src/.git/readme.md", ["**/*.md"]);

        // Assert: the recursive segment does not pass through the hidden directory
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that a literal hidden directory in a pattern permits files beneath it.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_LiteralHiddenDirectory_IsPermitted()
    {
        // Arrange: a pattern that names a hidden directory literally

        // Act: ask whether a file beneath it is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".github/workflows/ci.yml", [".github/**/*.yml"]);

        // Assert: the literal dot segment permits it
        Assert.True(permitted);
    }

    /// <summary>
    ///     Test that a literal hidden file name in a pattern permits that file at depth.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_LiteralHiddenFileName_IsPermitted()
    {
        // Arrange: a pattern that names a hidden file literally after a recursive segment

        // Act: ask whether the file is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted("src/.eslintrc.json", ["**/.eslintrc.json"]);

        // Assert: the literal dot name permits it
        Assert.True(permitted);
    }

    /// <summary>
    ///     Test that a segment starting with a dot and containing a wildcard matches hidden names.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_DotPrefixedWildcardSegment_IsPermitted()
    {
        // Arrange: a pattern whose segment starts with a literal dot and then a wildcard

        // Act: ask whether a hidden file is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".bashrc", [".*rc"]);

        // Assert: the literal leading dot permits it
        Assert.True(permitted);
    }

    /// <summary>
    ///     Test that a wildcard beneath a literal hidden directory still skips nested hidden directories.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_WildcardBeneathLiteralHidden_SkipsNestedHidden()
    {
        // Arrange: a pattern with a literal hidden directory followed by a recursive segment

        // Act: ask whether a file inside a second, nested hidden directory is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".github/.cache/x.yml", [".github/**/*.yml"]);

        // Assert: the recursive segment does not pass through the nested hidden directory
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that one pattern's literal does not excuse another pattern's wildcard.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_LiteralInOtherPattern_DoesNotExcuseWildcard()
    {
        // Arrange: a wildcard pattern and an unrelated pattern naming the same hidden directory

        // Act: ask whether a file the wildcard pattern alone would reach is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".github/foo.md", ["**/*.md", ".github/x"]);

        // Assert: neither pattern permits the path on its own
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that a hidden segment matched literally is compared case-sensitively.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsPathPermitted_LiteralHiddenDifferentCase_IsNotPermitted()
    {
        // Arrange: a pattern naming a hidden directory in lower case

        // Act: ask whether the same directory in upper case is permitted
        var permitted = HiddenPathPolicy.IsPathPermitted(".GitHub/ci.yml", [".github/**"]);

        // Assert: the comparison is ordinal
        Assert.False(permitted);
    }

    /// <summary>
    ///     Test that a hidden directory no pattern can reach is not entered.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_CanContainPermittedPaths_WildcardOnlyAndHiddenDirectory_IsFalse()
    {
        // Arrange: wildcard-only patterns and a hidden directory

        // Act: ask whether the directory may contain permitted paths
        var canContain = HiddenPathPolicy.CanContainPermittedPaths(".git", ["**/*.md", "**/*.cs"]);

        // Assert: it should not be entered
        Assert.False(canContain);
    }

    /// <summary>
    ///     Test that a hidden directory named literally is entered.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_CanContainPermittedPaths_LiteralHiddenDirectory_IsTrue()
    {
        // Arrange: a pattern naming the hidden directory

        // Act: ask whether the directory may contain permitted paths
        var canContain = HiddenPathPolicy.CanContainPermittedPaths(".github", [".github/**/*.yml"]);

        // Assert: it should be entered
        Assert.True(canContain);
    }

    /// <summary>
    ///     Test that a non-hidden directory is always entered.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_CanContainPermittedPaths_NonHiddenDirectory_IsTrue()
    {
        // Arrange: wildcard-only patterns and an ordinary directory

        // Act: ask whether the directory may contain permitted paths
        var canContain = HiddenPathPolicy.CanContainPermittedPaths("src/docs", ["**/*.md"]);

        // Assert: it should be entered
        Assert.True(canContain);
    }

    /// <summary>
    ///     Test that a hidden directory beneath a literal prefix is entered only when named literally.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_CanContainPermittedPaths_HiddenBeneathLiteralPrefix_FollowsPattern()
    {
        // Arrange: a pattern that names a nested hidden directory beneath a plain one

        // Act: ask about the named hidden directory and about a different one
        var named = HiddenPathPolicy.CanContainPermittedPaths("a/.b", ["a/.b/**"]);
        var other = HiddenPathPolicy.CanContainPermittedPaths("a/.c", ["a/.b/**"]);

        // Assert: only the named one is entered
        Assert.Multiple(
            () => Assert.True(named),
            () => Assert.False(other));
    }

    /// <summary>
    ///     Test that hidden names are recognized by their leading dot.
    /// </summary>
    [Fact]
    public void HiddenPathPolicy_IsHidden_DotPrefixedName_IsTrueOtherwiseFalse()
    {
        // Arrange: no setup required

        // Act: classify a hidden name, an ordinary name, and an empty name
        var hidden = HiddenPathPolicy.IsHidden(".git");
        var ordinary = HiddenPathPolicy.IsHidden("git");
        var empty = HiddenPathPolicy.IsHidden(string.Empty);

        // Assert: only the dot-prefixed name is hidden
        Assert.Multiple(
            () => Assert.True(hidden),
            () => Assert.False(ordinary),
            () => Assert.False(empty));
    }
}
