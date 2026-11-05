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
using DemaConsulting.TextLint.Tests.TestSupport;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.TextLint.Tests.FileSelection;

/// <summary>
///     Subsystem tests for the FileSelection subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="FileSelector"/>, <see cref="GlobPatternExpander"/>, and
///     <see cref="HiddenPathPolicy"/> together against real temporary trees. The subsystem has no
///     dependencies on other subsystems: its inputs are plain pattern lists and a hidden-file switch.
/// </remarks>
public class FileSelectionSubsystemTests
{
    /// <summary>
    ///     Selects files from a temporary tree and returns their relative paths.
    /// </summary>
    /// <param name="temp">The temporary tree.</param>
    /// <param name="files">The include patterns.</param>
    /// <param name="ignorePaths">The ignore patterns, or null for none.</param>
    /// <param name="enableGlobDot">Whether wildcards may match hidden names.</param>
    /// <returns>The relative paths of the selected files.</returns>
    private static string[] SelectPaths(
        TempDirectory temp,
        string[] files,
        string[]? ignorePaths = null,
        bool enableGlobDot = false) =>
        [.. FileSelector.Select(temp.FullPath, files, ignorePaths ?? [], enableGlobDot).Select(file => file.RelativePath)];

    /// <summary>
    ///     Test that include and ignore patterns together select the expected files.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_IncludeAndIgnorePatterns_SelectsExpectedFiles()
    {
        // Arrange: a tree with source, documentation, and generated files
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("docs/generated/api.md");
        temp.CreateFile("src/Program.cs");

        // Act: include markdown and ignore generated output
        var paths = SelectPaths(temp, ["**/*.md"], ["docs/generated/**"]);

        // Assert: only the non-ignored markdown files remain
        Assert.Equal(["README.md", "docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that a path differing only in case is not selected.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_DifferentCase_IsNotSelected()
    {
        // Arrange: files whose names differ from the patterns only in case
        using var temp = new TempDirectory();
        temp.CreateFile("README.MD");
        temp.CreateFile("Docs/guide.md");

        // Act: select with lower-case patterns
        var paths = SelectPaths(temp, ["**/*.md", "docs/**"]);

        // Assert: only the exact-case match is selected
        Assert.Equal(["Docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that brace alternatives select every alternative.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_BraceAlternatives_SelectsAllAlternatives()
    {
        // Arrange: files with three different extensions
        using var temp = new TempDirectory();
        temp.CreateFile("a.md");
        temp.CreateFile("b.cs");
        temp.CreateFile("c.txt");

        // Act: select two extensions with a brace group
        var paths = SelectPaths(temp, ["**/*.{md,cs}"]);

        // Assert: both alternatives are selected
        Assert.Equal(["a.md", "b.cs"], paths);
    }

    /// <summary>
    ///     Test that a bare name matches at any depth.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_BareName_MatchesAtAnyDepth()
    {
        // Arrange: a file with the same name at the root and nested
        using var temp = new TempDirectory();
        temp.CreateFile("package-lock.json");
        temp.CreateFile("a/b/package-lock.json");
        temp.CreateFile("a/other.json");

        // Act: select by bare name
        var paths = SelectPaths(temp, ["package-lock.json"]);

        // Assert: both copies are selected
        Assert.Equal(["a/b/package-lock.json", "package-lock.json"], paths);
    }

    /// <summary>
    ///     Test that a negated pattern is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_NegatedPattern_ThrowsNamingPattern()
    {
        // Arrange: a root and a negated pattern
        using var temp = new TempDirectory();

        // Act / Assert: selection throws an exception that names the pattern
        var exception = Assert.Throws<InvalidGlobPatternException>(() => FileSelector.Select(temp.FullPath, ["!docs/**"], []));
        Assert.Contains("'!docs/**'", exception.Message);
    }

    /// <summary>
    ///     Test that hidden entries are skipped unless hidden-file matching is enabled.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_HiddenEntries_SkippedUnlessEnabled()
    {
        // Arrange: a visible file, a hidden file, and a file in a hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile(".hidden.md");
        temp.CreateFile(".git/notes.md");

        // Act: select by wildcard with hidden-file matching off, then on
        var off = SelectPaths(temp, ["**/*.md"]);
        var on = SelectPaths(temp, ["**/*.md"], enableGlobDot: true);

        // Assert: hidden entries appear only when enabled
        Assert.Multiple(
            () => Assert.Equal(["visible.md"], off),
            () => Assert.Equal([".git/notes.md", ".hidden.md", "visible.md"], on));
    }

    /// <summary>
    ///     Test that hidden names written literally in a pattern are selected.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_LiteralHiddenPattern_SelectsHiddenEntries()
    {
        // Arrange: a hidden directory with a file and an unrelated hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile(".github/workflows/ci.yml");
        temp.CreateFile(".cache/other.yml");

        // Act: select with a pattern that names .github literally
        var paths = SelectPaths(temp, [".github/**/*.yml"]);

        // Assert: the literally named hidden directory is searched; the other is not
        Assert.Equal([".github/workflows/ci.yml"], paths);
    }

    /// <summary>
    ///     Test that ignored and hidden directories are not enumerated.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_IgnoredDirectory_IsNotEnumerated()
    {
        // Arrange: an ignored directory, a hidden directory, and a visible directory
        using var temp = new TempDirectory();
        temp.CreateFile("node_modules/pkg/readme.md");
        temp.CreateFile(".git/notes.md");
        temp.CreateFile("docs/guide.md");
        var recording = new RecordingDirectoryInfo(new DirectoryInfoWrapper(new DirectoryInfo(temp.FullPath)));

        // Act: select while ignoring node_modules
        _ = FileSelector.Select(recording, ["**/*.md"], ["node_modules"], enableGlobDot: false);

        // Assert: only the visible directory was read besides the root
        Assert.Equal([string.Empty, "docs"], recording.EnumeratedDirectories.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that an unordered tree is returned in ordinal order.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_UnorderedTree_ReturnsOrdinalOrder()
    {
        // Arrange: files created in non-sorted order across directories
        using var temp = new TempDirectory();
        temp.CreateFile("z/last.md");
        temp.CreateFile("a/first.md");
        temp.CreateFile("m.md");

        // Act: select all markdown files
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: ordinal order with forward-slash paths
        Assert.Equal(["a/first.md", "m.md", "z/last.md"], paths);
    }
}
