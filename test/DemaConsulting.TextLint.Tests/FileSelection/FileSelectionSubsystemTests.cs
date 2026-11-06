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
        [.. FileSelector.Select(temp.FullPath, files, ignorePaths ?? [], enableGlobDot, TestContext.Current.CancellationToken).Select(file => file.RelativePath)];

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
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["!docs/**"], [], false, TestContext.Current.CancellationToken));
        Assert.Contains("'!docs/**'", exception.Message);
    }

    /// <summary>
    ///     Test that absolute path patterns are rejected naming the pattern.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_AbsolutePathPattern_ThrowsNamingPattern()
    {
        // Arrange: a root and absolute drive and network patterns
        using var temp = new TempDirectory();

        // Act / Assert: each is rejected with an error that names the pattern and says it is absolute
        var drive = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["C:/docs/**"], [], false, TestContext.Current.CancellationToken));
        var network = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["//server/share/**"], [], false, TestContext.Current.CancellationToken));
        Assert.Multiple(
            () => Assert.Contains("'C:/docs/**'", drive.Message),
            () => Assert.Contains("absolute paths", drive.Message),
            () => Assert.Contains("'//server/share/**'", network.Message),
            () => Assert.Contains("absolute paths", network.Message));
    }

    /// <summary>
    ///     Test that a leading slash anchors a pattern at the root directory rather than being an absolute path.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_LeadingSlashPattern_AnchorsToRoot()
    {
        // Arrange: a docs directory at the root and another nested deeper
        using var temp = new TempDirectory();
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("src/docs/guide.md");

        // Act: select with a leading-slash directory pattern
        var paths = SelectPaths(temp, ["/docs/**"]);

        // Assert: only the docs directory directly under the root is selected
        Assert.Equal(["docs/guide.md"], paths);
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
        _ = FileSelector.Select(recording, ["**/*.md"], ["node_modules"], enableGlobDot: false, TestContext.Current.CancellationToken);

        // Assert: only the visible directory was read besides the root
        Assert.Equal([string.Empty, "docs"], recording.EnumeratedDirectories.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that cancelling while the directory tree is read stops the selection and returns no selection.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_Select_CancelledDuringWalk_StopsAndThrows()
    {
        // Arrange: several directories, and a recording directory that cancels the token when the first
        // subdirectory starts to be read
        using var temp = new TempDirectory();
        temp.CreateFile("a/one.md");
        temp.CreateFile("b/two.md");
        temp.CreateFile("c/three.md");
        using var cancellation = new CancellationTokenSource();
        var recording = new RecordingDirectoryInfo(
            new DirectoryInfoWrapper(new DirectoryInfo(temp.FullPath)),
            relative => { if (relative.Length > 0) { cancellation.Cancel(); } });

        // Act: select through the recording directory
        var exception = Record.Exception(
            () => FileSelector.Select(recording, ["**/*.md"], [], enableGlobDot: false, cancellation.Token));

        // Assert: the selection ended with a cancellation, having read only the root and one subdirectory
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(2, recording.EnumeratedDirectories.Count);
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

    /// <summary>
    ///     Test that an absolute pattern inside the root selects the same files as its relative form and honors the ignore patterns.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_ResolveCommandLine_AbsolutePatternInsideRoot_SelectsAndHonorsIgnore()
    {
        // Arrange: a tree with an old subtree, and a pattern written as an absolute path of this platform
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md");
        temp.CreateFile("docs/old/b.md");
        temp.CreateFile("docs/c.txt");
        var argument = Path.Combine(temp.FullPath, "docs", "**", "*.md");

        // Act: resolve the argument, then select with an ignore pattern that is relative to the root
        var selection = CommandLinePathResolver.Resolve(temp.FullPath, [argument]);
        var files = FileSelector.Select(
            selection.RootDirectory,
            selection.Patterns,
            ["docs/old/**"],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the old subtree is ignored, and the file is named by its absolute path
        Assert.Multiple(
            () => Assert.Equal(["docs/a.md"], files.Select(file => file.RelativePath)),
            () => Assert.Equal(Path.Combine(temp.FullPath, "docs", "a.md"), selection.GetDisplayPath(files[0].RelativePath)));
    }

    /// <summary>
    ///     Test that an absolute pattern outside the root selects files beneath its own directory.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_ResolveCommandLine_AbsolutePatternOutsideRoot_SelectsBeneathItsDirectory()
    {
        // Arrange: a working root, and a separate tree that holds the files
        using var workRoot = new TempDirectory();
        using var other = new TempDirectory();
        other.CreateFile("notes/a.md");
        other.CreateFile("notes/deep/b.md");
        other.CreateFile("notes/c.txt");
        var argument = Path.Combine(other.FullPath, "notes", "**", "*.md");

        // Act: resolve against the unrelated root, then select
        var selection = CommandLinePathResolver.Resolve(workRoot.FullPath, [argument]);
        var files = FileSelector.Select(
            selection.RootDirectory,
            selection.Patterns,
            [],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the search base is the literal directory, and the files are named by absolute path
        Assert.Multiple(
            () => Assert.Equal(Path.Combine(other.FullPath, "notes"), selection.RootDirectory),
            () => Assert.Equal(["a.md", "deep/b.md"], files.Select(file => file.RelativePath)),
            () => Assert.Equal(
                [Path.Combine(other.FullPath, "notes", "a.md"), Path.Combine(other.FullPath, "notes", "deep", "b.md")],
                files.Select(file => selection.GetDisplayPath(file.RelativePath))));
    }

    /// <summary>
    ///     Test that a path with backslashes selects a file on Windows and is rejected elsewhere.
    /// </summary>
    [Fact]
    public void FileSelectionSubsystem_ResolveCommandLine_BackslashRelativePath_FollowsPlatformRules()
    {
        // Arrange: a tree with one file and the PowerShell-style path to it
        using var temp = new TempDirectory();
        temp.CreateFile("docs/guide.md");
        var argument = @".\docs\guide.md";

        // Act / Assert: Windows selects the file; other systems reject the backslash
        if (OperatingSystem.IsWindows())
        {
            var selection = CommandLinePathResolver.Resolve(temp.FullPath, [argument]);
            var files = FileSelector.Select(selection.RootDirectory, selection.Patterns, [], cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["docs/guide.md"], files.Select(file => file.RelativePath));
        }
        else
        {
            Assert.Throws<InvalidGlobPatternException>(() => CommandLinePathResolver.Resolve(temp.FullPath, [argument]));
        }
    }
}
