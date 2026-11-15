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
///     Unit tests for the <see cref="FileSelector"/> class.
/// </summary>
/// <remarks>
///     The selector is tested against real temporary directory trees with the real matcher (an OTS
///     item) and the real pattern expander and hidden-path policy it depends on. The patterns are
///     passed directly, because the selector does not depend on any configuration type. The only
///     test double is the recording directory, which proves that excluded directories are never
///     enumerated without relying on timing.
/// </remarks>
public class FileSelectorTests
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
    ///     Selects files from a temporary tree through a recording directory.
    /// </summary>
    /// <param name="temp">The temporary tree.</param>
    /// <param name="files">The include patterns.</param>
    /// <param name="ignorePaths">The ignore patterns, or null for none.</param>
    /// <returns>The root-relative paths of the directories that were enumerated.</returns>
    private static IReadOnlyList<string> SelectAndRecord(TempDirectory temp, string[] files, string[]? ignorePaths = null)
    {
        // Wrap the real directory so every enumeration is observed
        var recording = new RecordingDirectoryInfo(new DirectoryInfoWrapper(new DirectoryInfo(temp.FullPath)));
        _ = FileSelector.Select(recording, files, ignorePaths ?? [], enableGlobDot: false, TestContext.Current.CancellationToken);
        return recording.EnumeratedDirectories;
    }

    /// <summary>
    ///     Test that include patterns select the matching files.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IncludePatterns_ReturnsMatchingFiles()
    {
        // Arrange: a tree with markdown and C# files
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("src/Program.cs");

        // Act: select the markdown files
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: only the markdown files are selected
        Assert.Equal(["README.md", "docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that ignore patterns exclude matching files.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IgnorePatterns_ExcludesMatches()
    {
        // Arrange: a tree with two markdown files
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("CHANGELOG.md");

        // Act: select markdown files while ignoring one by name
        var paths = SelectPaths(temp, ["**/*.md"], ["CHANGELOG.md"]);

        // Assert: the ignored file is not selected
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that empty include patterns select nothing.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NoIncludePatterns_ReturnsEmpty()
    {
        // Arrange: a tree with a file
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");

        // Act: select with no include patterns
        var paths = SelectPaths(temp, []);

        // Assert: nothing is selected
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that an ignore pattern wins over an overlapping include pattern.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IgnoreAndIncludeOverlap_IgnoreWins()
    {
        // Arrange: a file matched by both an include and an ignore pattern
        using var temp = new TempDirectory();
        temp.CreateFile("docs/guide.md");

        // Act: select with overlapping patterns
        var paths = SelectPaths(temp, ["docs/**"], ["docs/**"]);

        // Assert: the ignore pattern wins
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that deeply nested files are found.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NestedFiles_AreFound()
    {
        // Arrange: a file several directories deep
        using var temp = new TempDirectory();
        temp.CreateFile("a/b/c/d/deep.md");

        // Act: select with a recursive pattern
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: the nested file is selected
        Assert.Equal(["a/b/c/d/deep.md"], paths);
    }

    /// <summary>
    ///     Test that names containing spaces are selected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NamesWithSpaces_AreSelected()
    {
        // Arrange: a directory and file whose names contain spaces
        using var temp = new TempDirectory();
        temp.CreateFile("my docs/read me.md");

        // Act: select markdown files
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: the file is selected
        Assert.Equal(["my docs/read me.md"], paths);
    }

    /// <summary>
    ///     Test that a bare directory name in the ignore list excludes the nested subtree.
    /// </summary>
    [Fact]
    public void FileSelector_Select_BareDirectoryNameIgnored_ExcludesNestedSubtree()
    {
        // Arrange: a node_modules directory nested below the root
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("packages/app/node_modules/dep/readme.md");

        // Act: select markdown files, ignoring node_modules by bare name
        var paths = SelectPaths(temp, ["**/*.md"], ["node_modules"]);

        // Assert: the nested subtree is excluded
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that a token that is already canceled is honored before any directory is read.
    /// </summary>
    [Fact]
    public void FileSelector_Select_TokenAlreadyCanceled_ThrowsOperationCanceled()
    {
        // Arrange: a valid tree and a token that is canceled before the call
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert: the call throws and returns no result
        Assert.Throws<OperationCanceledException>(
            () => FileSelector.Select(temp.FullPath, ["**/*.md"], [], false, cancellation.Token));
    }

    /// <summary>
    ///     Test that canceling while the walk is running stops it at the next entry, whether or not wildcards may match hidden names.
    /// </summary>
    /// <param name="enableGlobDot">Whether wildcards may match hidden names.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileSelector_Select_CanceledDuringWalk_StopsWalkAndThrows(bool enableGlobDot)
    {
        // Arrange: three directories with a file each, and a recording directory that cancels the token as soon as
        // the walk starts to read the first subdirectory
        using var temp = new TempDirectory();
        temp.CreateFile("a/one.md");
        temp.CreateFile("b/two.md");
        temp.CreateFile("c/three.md");
        using var cancellation = new CancellationTokenSource();
        var recording = new RecordingDirectoryInfo(
            new DirectoryInfoWrapper(new DirectoryInfo(temp.FullPath)),
            relative => { if (relative.Length > 0) { cancellation.Cancel(); } });

        // Act: select through the seam
        var exception = Record.Exception(
            () => FileSelector.Select(recording, ["**/*.md"], [], enableGlobDot, cancellation.Token));

        // Assert: the walk was canceled, and it read only the root and the first subdirectory, not the other two
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(2, recording.EnumeratedDirectories.Count);
        Assert.Equal(string.Empty, recording.EnumeratedDirectories[0]);
    }

    /// <summary>
    ///     Test that a token that is never canceled does not change the selection.
    /// </summary>
    [Fact]
    public void FileSelector_Select_TokenNotCanceled_ReturnsSelection()
    {
        // Arrange: a tree and a live token
        using var temp = new TempDirectory();
        temp.CreateFile("a/one.md");
        temp.CreateFile("b/two.md");
        using var cancellation = new CancellationTokenSource();

        // Act: select with the token
        var paths = FileSelector.Select(temp.FullPath, ["**/*.md"], [], false, cancellation.Token)
            .Select(file => file.RelativePath)
            .ToArray();

        // Assert: both files are selected
        Assert.Equal(["a/one.md", "b/two.md"], paths);
    }

    /// <summary>
    ///     Test that a missing root directory is rejected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_RootDirectoryMissing_ThrowsDirectoryNotFound()
    {
        // Arrange: a root that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: selecting from it throws
        Assert.Throws<DirectoryNotFoundException>(
            () => FileSelector.Select(missing, ["**/*.md"], [], false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a missing root is reported even when no include pattern would read it.
    /// </summary>
    [Fact]
    public void FileSelector_Select_RootDirectoryMissingNoIncludes_ThrowsDirectoryNotFound()
    {
        // Arrange: a root that does not exist and an empty include list
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: the root is still probed, so the caller error is not hidden by the empty selection
        Assert.Throws<DirectoryNotFoundException>(
            () => FileSelector.Select(missing, [], [], false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a root that names a file is reported as an input/output failure and not as a selection.
    /// </summary>
    [Fact]
    public void FileSelector_Select_RootIsFile_ThrowsIOException()
    {
        // Arrange: a root path that names a file
        using var temp = new TempDirectory();
        temp.CreateFile("file.txt");
        var file = Path.Combine(temp.FullPath, "file.txt");

        // Act / Assert: the file system's failure is reported (a directory-not-found failure is also an IOException)
        Assert.ThrowsAny<IOException>(
            () => FileSelector.Select(file, ["**/*.md"], [], false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an existing empty root selects nothing without failing.
    /// </summary>
    [Fact]
    public void FileSelector_Select_EmptyRootDirectory_ReturnsEmpty()
    {
        // Arrange: an existing directory with no entries
        using var temp = new TempDirectory();

        // Act: select from it
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: the probe of an empty directory is not a failure
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that a directory-not-found failure from the probe is reported as a missing root that names the root.
    /// </summary>
    [Fact]
    public void FileSelector_ProbeRoot_DirectoryNotFound_ThrowsDirectoryNotFoundNamingRoot()
    {
        // Arrange: a listing that fails because the directory does not exist
        var original = new DirectoryNotFoundException("gone");

        // Act: probe through the failing listing
        var exception = Assert.Throws<DirectoryNotFoundException>(
            () => FileSelector.ProbeRoot("some/root", () => throw original));

        // Assert: the message names the root and the original failure is kept
        Assert.Contains("some/root", exception.Message, StringComparison.Ordinal);
        Assert.Same(original, exception.InnerException);
    }

    /// <summary>
    ///     Test that an access failure from the probe is not reported as a missing root.
    /// </summary>
    [Fact]
    public void FileSelector_ProbeRoot_AccessDenied_PropagatesUnauthorizedAccess()
    {
        // Arrange: a listing that fails because the process may not read the directory
        var original = new UnauthorizedAccessException("denied");

        // Act / Assert: the same exception reaches the caller, not a DirectoryNotFoundException
        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => FileSelector.ProbeRoot("some/root", () => throw original));
        Assert.Same(original, exception);
    }

    /// <summary>
    ///     Test that a failure raised while stepping through the listing is reported as it is.
    /// </summary>
    [Fact]
    public void FileSelector_ProbeRoot_LazyListingFails_PropagatesIOException()
    {
        // Arrange: a listing that fails only when its first entry is requested, as file system listings do
        static IEnumerable<FileSystemInfo> FailingListing() =>
            Enumerable.Range(0, 1).Select<int, FileSystemInfo>(_ => throw new IOException("device error"));

        // Act / Assert: the input/output failure is neither swallowed nor turned into a missing root
        var exception = Assert.Throws<IOException>(() => FileSelector.ProbeRoot("some/root", FailingListing));
        Assert.IsNotType<DirectoryNotFoundException>(exception);
    }

    /// <summary>
    ///     Test that the probe rejects null arguments.
    /// </summary>
    [Fact]
    public void FileSelector_ProbeRoot_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange: a valid listing and root
        static IEnumerable<FileSystemInfo> Listing() => [];

        // Act / Assert: each null argument is rejected
        Assert.Throws<ArgumentNullException>(() => FileSelector.ProbeRoot(null!, Listing));
        Assert.Throws<ArgumentNullException>(() => FileSelector.ProbeRoot("some/root", null!));
    }

    /// <summary>
    ///     Test that an invalid pattern is reported even when the root directory is also missing.
    /// </summary>
    [Fact]
    public void FileSelector_Select_InvalidPatternAndMissingRoot_ThrowsInvalidGlobPattern()
    {
        // Arrange: a root that does not exist and an ignore pattern using the unsupported '?' wildcard
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: the pattern error wins because patterns are validated before the directory is read
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(missing, ["**/*.md"], ["file?.md"], false, TestContext.Current.CancellationToken));
        Assert.Equal("file?.md", exception.Pattern);
    }

    /// <summary>
    ///     Test that null include patterns are rejected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NullIncludePatterns_ThrowsArgumentNullException()
    {
        // Arrange: a valid root
        using var temp = new TempDirectory();

        // Act / Assert: null include patterns throw
        Assert.Throws<ArgumentNullException>(
            () => FileSelector.Select(temp.FullPath, null!, [], false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that null ignore patterns are rejected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NullIgnorePatterns_ThrowsArgumentNullException()
    {
        // Arrange: a valid root
        using var temp = new TempDirectory();

        // Act / Assert: null ignore patterns throw
        Assert.Throws<ArgumentNullException>(
            () => FileSelector.Select(temp.FullPath, ["**/*.md"], null!, false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a null root is rejected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NullRoot_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: a null root throws
        Assert.Throws<ArgumentNullException>(
            () => FileSelector.Select((string)null!, ["**/*.md"], [], false, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an empty root is rejected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_EmptyRoot_ThrowsArgumentException()
    {
        // Arrange: no setup required

        // Act / Assert: an empty root throws ArgumentException (not the derived null exception)
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => FileSelector.Select(string.Empty, ["**/*.md"], [], false, TestContext.Current.CancellationToken));
        Assert.IsNotType<ArgumentNullException>(exception);
    }

    /// <summary>
    ///     Test that a selected file exposes its full path.
    /// </summary>
    [Fact]
    public void FileSelector_Select_Result_ExposesFullPath()
    {
        // Arrange: a nested file
        using var temp = new TempDirectory();
        var created = temp.CreateFile("docs/guide.md");

        // Act: select it
        var file = Assert.Single(FileSelector.Select(temp.FullPath, ["**/*.md"], [], false, TestContext.Current.CancellationToken));

        // Assert: the full path is the absolute operating-system path of the file
        Assert.Equal(created, file.FullPath);
    }

    /// <summary>
    ///     Test that a file whose extension differs only in case is not selected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_DifferentCaseExtension_IsNotSelected()
    {
        // Arrange: a file with an upper-case extension
        using var temp = new TempDirectory();
        temp.CreateFile("README.MD");

        // Act: select with a lower-case extension pattern
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: matching is case-sensitive on every platform
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that a directory whose name differs only in case is not selected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_DifferentCaseDirectory_IsNotSelected()
    {
        // Arrange: a directory with an upper-case name
        using var temp = new TempDirectory();
        temp.CreateFile("Docs/guide.md");

        // Act: select with a lower-case directory pattern
        var paths = SelectPaths(temp, ["docs/*.md"]);

        // Assert: matching is case-sensitive on every platform
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that a hidden file is skipped by default.
    /// </summary>
    [Fact]
    public void FileSelector_Select_HiddenFile_SkippedByDefault()
    {
        // Arrange: a visible and a hidden file
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile(".hidden.md");

        // Act: select with a wildcard pattern
        var paths = SelectPaths(temp, ["*.md"]);

        // Assert: the hidden file is skipped
        Assert.Equal(["visible.md"], paths);
    }

    /// <summary>
    ///     Test that files in a hidden directory are skipped by default.
    /// </summary>
    [Fact]
    public void FileSelector_Select_HiddenDirectory_SkippedByDefault()
    {
        // Arrange: a visible file and a file inside a hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile("docs/guide.md");
        temp.CreateFile(".git/notes.md");

        // Act: select with a recursive wildcard pattern
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: the hidden directory's file is skipped
        Assert.Equal(["docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that hidden entries are selected when hidden-file matching is enabled.
    /// </summary>
    [Fact]
    public void FileSelector_Select_HiddenEntries_SelectedWhenGlobDotEnabled()
    {
        // Arrange: hidden and visible files
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile(".hidden.md");
        temp.CreateFile(".git/notes.md");

        // Act: select with hidden-file matching enabled
        var paths = SelectPaths(temp, ["**/*.md"], enableGlobDot: true);

        // Assert: wildcards now match the hidden entries
        Assert.Equal([".git/notes.md", ".hidden.md", "visible.md"], paths);
    }

    /// <summary>
    ///     Test that a literal hidden directory in a pattern selects files beneath it.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LiteralHiddenDirectoryPattern_SelectsFilesBeneath()
    {
        // Arrange: a workflow file in a hidden directory and a file in another hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile(".github/workflows/ci.yml");
        temp.CreateFile(".cache/other.yml");

        // Act: select with a pattern that names .github literally
        var paths = SelectPaths(temp, [".github/**/*.yml"]);

        // Assert: only the literally named hidden directory is searched
        Assert.Equal([".github/workflows/ci.yml"], paths);
    }

    /// <summary>
    ///     Test that a literal hidden file name in a pattern selects that file at any depth.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LiteralHiddenFilePattern_SelectsFileAtAnyDepth()
    {
        // Arrange: hidden configuration files at the root and nested
        using var temp = new TempDirectory();
        temp.CreateFile(".editorconfig");
        temp.CreateFile("src/.editorconfig");
        temp.CreateFile(".other");

        // Act: select with a bare hidden name
        var paths = SelectPaths(temp, [".editorconfig"]);

        // Assert: both named files are selected and the other hidden file is not
        Assert.Equal([".editorconfig", "src/.editorconfig"], paths);
    }

    /// <summary>
    ///     Test that a wildcard beneath a literal hidden directory skips nested hidden directories.
    /// </summary>
    [Fact]
    public void FileSelector_Select_WildcardBeneathLiteralHidden_SkipsNestedHidden()
    {
        // Arrange: a visible and a nested hidden directory beneath .github
        using var temp = new TempDirectory();
        temp.CreateFile(".github/workflows/ci.yml");
        temp.CreateFile(".github/.cache/stale.yml");

        // Act: select with a pattern that names .github literally and then uses a wildcard
        var paths = SelectPaths(temp, [".github/**/*.yml"]);

        // Assert: the nested hidden directory is not searched
        Assert.Equal([".github/workflows/ci.yml"], paths);
    }

    /// <summary>
    ///     Test that another pattern naming a hidden directory does not let a wildcard pattern reach it.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LiteralInOtherPattern_DoesNotLetWildcardReachHidden()
    {
        // Arrange: a markdown file and a yaml file in .github
        using var temp = new TempDirectory();
        temp.CreateFile(".github/notes.md");
        temp.CreateFile(".github/ci.yml");

        // Act: select markdown by wildcard and yaml by a literal .github pattern
        var paths = SelectPaths(temp, ["**/*.md", ".github/*.yml"]);

        // Assert: the yaml file is selected but the wildcard does not reach the markdown file
        Assert.Equal([".github/ci.yml"], paths);
    }

    /// <summary>
    ///     Test that an ignored directory is never enumerated.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IgnoredDirectory_IsNeverEnumerated()
    {
        // Arrange: a node_modules directory containing a nested directory
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("node_modules/pkg/readme.md");

        // Act: select while ignoring node_modules and record the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md"], ["node_modules"]);

        // Assert: neither node_modules nor anything beneath it was enumerated
        Assert.Multiple(
            () => Assert.Contains(string.Empty, enumerated),
            () => Assert.DoesNotContain("node_modules", enumerated),
            () => Assert.DoesNotContain("node_modules/pkg", enumerated));
    }

    /// <summary>
    ///     Test that a hidden directory is never enumerated by default.
    /// </summary>
    [Fact]
    public void FileSelector_Select_HiddenDirectory_IsNeverEnumeratedByDefault()
    {
        // Arrange: a hidden directory and a visible directory
        using var temp = new TempDirectory();
        temp.CreateFile(".git/objects/info.md");
        temp.CreateFile("docs/guide.md");

        // Act: select with a wildcard pattern and record the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md"]);

        // Assert: the hidden directory was skipped and the visible one was read
        Assert.Multiple(
            () => Assert.DoesNotContain(".git", enumerated),
            () => Assert.DoesNotContain(".git/objects", enumerated),
            () => Assert.Contains("docs", enumerated));
    }

    /// <summary>
    ///     Test that a nested ignored directory is never enumerated.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NestedIgnoredDirectory_IsNeverEnumerated()
    {
        // Arrange: a build directory nested two levels down
        using var temp = new TempDirectory();
        temp.CreateFile("src/lib/build/out.md");
        temp.CreateFile("src/lib/docs.md");

        // Act: select while ignoring the directory name and record the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md"], ["build/"]);

        // Assert: the nested directory was not enumerated but its parent was
        Assert.Multiple(
            () => Assert.Contains("src/lib", enumerated),
            () => Assert.DoesNotContain("src/lib/build", enumerated));
    }

    /// <summary>
    ///     Test that a hidden directory named literally is enumerated.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LiteralHiddenDirectory_IsEnumerated()
    {
        // Arrange: a hidden directory and a second hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile(".github/ci.yml");
        temp.CreateFile(".cache/x.yml");

        // Act: select with a pattern that names .github and record the enumerations
        var enumerated = SelectAndRecord(temp, [".github/*.yml"]);

        // Assert: the named hidden directory was read and the other was not
        Assert.Multiple(
            () => Assert.Contains(".github", enumerated),
            () => Assert.DoesNotContain(".cache", enumerated));
    }

    /// <summary>
    ///     Test that files created out of order are returned in ordinal order.
    /// </summary>
    [Fact]
    public void FileSelector_Select_FilesCreatedOutOfOrder_ReturnsOrdinalOrder()
    {
        // Arrange: files created in non-sorted order, including mixed case
        using var temp = new TempDirectory();
        temp.CreateFile("c.md");
        temp.CreateFile("a/z.md");
        temp.CreateFile("B.md");
        temp.CreateFile("a.md");

        // Act: select all markdown files
        var paths = SelectPaths(temp, ["**/*.md"]);

        // Assert: the order is ordinal (upper case before lower case)
        Assert.Equal(["B.md", "a.md", "a/z.md", "c.md"], paths);
    }

    /// <summary>
    ///     Test that nested files use forward slashes in their relative paths.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NestedFiles_UseForwardSlashes()
    {
        // Arrange: a nested file
        using var temp = new TempDirectory();
        temp.CreateFile("docs/sub/guide.md");

        // Act: select it
        var file = Assert.Single(FileSelector.Select(temp.FullPath, ["**/*.md"], [], false, TestContext.Current.CancellationToken));

        // Assert: the relative path uses forward slashes on every platform
        Assert.Equal("docs/sub/guide.md", file.RelativePath);
    }

    /// <summary>
    ///     Test that an invalid include pattern is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void FileSelector_Select_InvalidIncludePattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: a root and an include pattern list that starts with a negation
        using var temp = new TempDirectory();

        // Act / Assert: selection throws an exception that names the pattern
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["!**/*.md"], [], false, TestContext.Current.CancellationToken));
        Assert.Equal("!**/*.md", exception.Pattern);
    }

    /// <summary>
    ///     Test that absolute path patterns are rejected naming the pattern.
    /// </summary>
    [Fact]
    public void FileSelector_Select_AbsolutePathPattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: a root, a drive path as an include pattern, and a network path as an ignore pattern
        using var temp = new TempDirectory();

        // Act / Assert: each is rejected as an absolute path, and the exception exposes the pattern
        var include = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["C:/docs/**"], [], false, TestContext.Current.CancellationToken));
        var ignore = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["**/*.md"], ["//server/share/**"], false, TestContext.Current.CancellationToken));
        Assert.Multiple(
            () => Assert.Equal("C:/docs/**", include.Pattern),
            () => Assert.Contains("absolute paths", include.Message),
            () => Assert.Equal("//server/share/**", ignore.Pattern),
            () => Assert.Contains("absolute paths", ignore.Message));
    }

    /// <summary>
    ///     Test that a pattern with a leading slash matches only at the root directory.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LeadingSlashPattern_MatchesOnlyAtRoot()
    {
        // Arrange: a file named README.md at the root and another nested
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/README.md");

        // Act: select by a leading-slash pattern
        var paths = SelectPaths(temp, ["/README.md"]);

        // Assert: only the file in the root directory is selected
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that an invalid ignore pattern is rejected even when nothing is included.
    /// </summary>
    [Fact]
    public void FileSelector_Select_InvalidIgnorePattern_ThrowsInvalidGlobPattern()
    {
        // Arrange: a root, no include patterns, and a backslash ignore pattern
        using var temp = new TempDirectory();

        // Act / Assert: selection throws an exception that names the pattern
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, [], ["docs\\old"], false, TestContext.Current.CancellationToken));
        Assert.Equal("docs\\old", exception.Pattern);
    }

    /// <summary>
    ///     Test that a negated pattern removes the files an earlier pattern selected.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegationAfterInclude_ExcludesFiles()
    {
        // Arrange: markdown files in the root, in docs, and in src
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("src/notes.md");

        // Act: select all markdown files except those beneath docs
        var paths = SelectPaths(temp, ["**/*.md", "!docs/**"]);

        // Assert: the docs files are removed
        Assert.Equal(["README.md", "src/notes.md"], paths);
    }

    /// <summary>
    ///     Test that a later pattern includes again what a negation removed.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IncludeAfterNegation_SelectsFilesAgain()
    {
        // Arrange: markdown files beneath docs, one of them beneath docs/keep
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("docs/keep/a.md");
        temp.CreateFile("docs/other/b.md");

        // Act: remove docs and bring back docs/keep
        var paths = SelectPaths(temp, ["**/*.md", "!docs", "docs/keep/**"]);

        // Assert: only the re-included files and the others outside docs are selected
        Assert.Equal(["README.md", "docs/keep/a.md"], paths);
    }

    /// <summary>
    ///     Test that a list without negation selects exactly what the same patterns selected as plain includes.
    /// </summary>
    [Fact]
    public void FileSelector_Select_ListWithoutNegation_SelectsSameFilesAsBefore()
    {
        // Arrange: a tree with hidden and visible files, and a negation-free list with a literal hidden pattern
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile(".github/ci.md");
        temp.CreateFile(".cache/x.md");

        // Act: select with wildcards and one literal hidden directory
        var paths = SelectPaths(temp, ["**/*.md", ".github/*.md"]);

        // Assert: hidden content is reached only through the literal pattern
        Assert.Equal([".github/ci.md", "README.md", "docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that a negated directory is never enumerated.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegatedBareDirectory_IsNotEnumerated()
    {
        // Arrange: a tree with a node_modules directory at two depths
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("node_modules/pkg/a.md");
        temp.CreateFile("src/node_modules/b.md");
        temp.CreateFile("src/c.md");

        // Act: select markdown files except node_modules, recording the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md", "!node_modules"]);

        // Assert: the excluded directories were never read, and the others were
        Assert.Multiple(
            () => Assert.DoesNotContain("node_modules", enumerated),
            () => Assert.DoesNotContain("src/node_modules", enumerated),
            () => Assert.Contains("src", enumerated));
    }

    /// <summary>
    ///     Test that a negated directory is still enumerated when a later pattern can include a file beneath it.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegatedDirectoryWithLaterInclude_IsEnumerated()
    {
        // Arrange: docs with a re-included subdirectory and an excluded one
        using var temp = new TempDirectory();
        temp.CreateFile("docs/keep/a.md");
        temp.CreateFile("docs/other/b.md");

        // Act: select with a negation and a later include, recording the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md", "!docs", "docs/keep/**"]);

        // Assert: docs and the re-included directory are read, and the excluded sibling is not
        Assert.Multiple(
            () => Assert.Contains("docs", enumerated),
            () => Assert.Contains("docs/keep", enumerated),
            () => Assert.DoesNotContain("docs/other", enumerated));
    }

    /// <summary>
    ///     Test that a negation of particular files does not stop the directory being enumerated.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegatedFilePattern_StillEnumeratesDirectory()
    {
        // Arrange: markdown and text files beneath docs
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md");
        temp.CreateFile("docs/b.txt");

        // Act: select everything except markdown files beneath docs
        var enumerated = SelectAndRecord(temp, ["**/*", "!docs/**/*.md"]);
        var paths = SelectPaths(temp, ["**/*", "!docs/**/*.md"]);

        // Assert: docs is read and only the text file is selected
        Assert.Multiple(
            () => Assert.Contains("docs", enumerated),
            () => Assert.Equal(["docs/b.txt"], paths));
    }

    /// <summary>
    ///     Test that negating an anchored directory selects exactly what the same ignore pattern leaves.
    /// </summary>
    [Theory]
    [InlineData("docs/a")]
    [InlineData("docs/a/")]
    [InlineData("docs/a/**")]
    public void FileSelector_Select_NegatedAnchoredDirectory_MatchesIgnorePattern(string pattern)
    {
        // Arrange: a directory, a similarly named sibling, and a directory with a deeper level
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a/b.md");
        temp.CreateFile("docs/a/deep/c.md");
        temp.CreateFile("docs/ab/d.md");
        temp.CreateFile("docs/e.md");

        // Act: select once with the negation and once with the same pattern as an ignore pattern
        var negated = SelectPaths(temp, ["**/*.md", "!" + pattern]);
        var enumerated = SelectAndRecord(temp, ["**/*.md", "!" + pattern]);
        var ignored = SelectPaths(temp, ["**/*.md"], [pattern]);

        // Assert: both leave the same files, and the negated directory is not even read
        Assert.Multiple(
            () => Assert.Equal(["docs/ab/d.md", "docs/e.md"], negated),
            () => Assert.Equal(ignored, negated),
            () => Assert.DoesNotContain("docs/a", enumerated),
            () => Assert.Contains("docs/ab", enumerated));
    }

    /// <summary>
    ///     Test that a file beneath an anchored directory that a negation removed can be selected again by a later include.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegatedAnchoredDirectoryThenInclude_SelectsFileAgain()
    {
        // Arrange: a directory with a file to keep and a file to drop
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a/keep.md");
        temp.CreateFile("docs/a/drop.md");
        temp.CreateFile("docs/b.md");

        // Act: select with the directory negated without a recursive wildcard, then one file included again
        var paths = SelectPaths(temp, ["**/*.md", "!docs/a", "docs/a/keep.md"]);

        // Assert: only the re-included file of the directory is selected
        Assert.Equal(["docs/a/keep.md", "docs/b.md"], paths);
    }

    /// <summary>
    ///     Test that an anchored directory path as a plain include still selects no file beneath the directory.
    /// </summary>
    [Fact]
    public void FileSelector_Select_AnchoredDirectoryAsInclude_SelectsNothingBeneathIt()
    {
        // Arrange: a directory that holds a file
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a/b.md");

        // Act: select with the directory path written without a slash, and with a trailing slash
        var withoutSlash = SelectPaths(temp, ["docs/a"]);
        var withSlash = SelectPaths(temp, ["docs/a/"]);

        // Assert: an include names a file, so only the trailing slash selects the files beneath the directory
        Assert.Multiple(
            () => Assert.Empty(withoutSlash),
            () => Assert.Equal(["docs/a/b.md"], withSlash));
    }

    /// <summary>
    ///     Test that hidden directories are still skipped when the list has a negation.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegationAndHiddenDirectory_HiddenIsNotEnumerated()
    {
        // Arrange: a hidden directory and a visible one
        using var temp = new TempDirectory();
        temp.CreateFile(".git/config.md");
        temp.CreateFile("docs/a.md");
        temp.CreateFile("docs/skip/b.md");

        // Act: select with a negation, recording the enumerations
        var enumerated = SelectAndRecord(temp, ["**/*.md", "!docs/skip/"]);

        // Assert: the hidden directory and the negated one are not read
        Assert.Multiple(
            () => Assert.DoesNotContain(".git", enumerated),
            () => Assert.DoesNotContain("docs/skip", enumerated),
            () => Assert.Contains("docs", enumerated));
    }

    /// <summary>
    ///     Test that a negation works together with enabled hidden-name matching.
    /// </summary>
    [Fact]
    public void FileSelector_Select_NegationAndGlobDot_SelectsHiddenExceptNegated()
    {
        // Arrange: two hidden directories
        using var temp = new TempDirectory();
        temp.CreateFile(".github/ci.md");
        temp.CreateFile(".cache/x.md");

        // Act: select markdown files with hidden matching enabled, except one hidden directory
        var paths = SelectPaths(temp, ["**/*.md", "!.cache"], enableGlobDot: true);

        // Assert: only the hidden directory that is not negated is selected
        Assert.Equal([".github/ci.md"], paths);
    }

    /// <summary>
    ///     Test that the ignore patterns still win over a later include and still reject a negation.
    /// </summary>
    [Fact]
    public void FileSelector_Select_IgnorePatternWithOrderedInclude_IgnoreStillWins()
    {
        // Arrange: a file matched by a re-include and an ignore pattern, and a negated ignore pattern
        using var temp = new TempDirectory();
        temp.CreateFile("docs/keep/a.md");
        temp.CreateFile("docs/keep/b.md");

        // Act: select with a re-include that an ignore pattern overrules
        var paths = SelectPaths(temp, ["**/*.md", "!docs", "docs/keep/**"], ["docs/keep/b.md"]);
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(temp.FullPath, ["**/*.md"], ["!docs/**"], false, TestContext.Current.CancellationToken));

        // Assert: the ignored file is gone and a negated ignore pattern is rejected naming the pattern
        Assert.Multiple(
            () => Assert.Equal(["docs/keep/a.md"], paths),
            () => Assert.Equal("!docs/**", exception.Pattern));
    }

    /// <summary>
    ///     Test that a list starting with a negation is rejected before the file system is touched.
    /// </summary>
    [Fact]
    public void FileSelector_Select_LeadingNegationAndMissingRoot_ThrowsInvalidGlobPattern()
    {
        // Act / Assert: the pattern error is reported even though the root does not exist
        var exception = Assert.Throws<InvalidGlobPatternException>(
            () => FileSelector.Select(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), ["!docs/**"], [], false, TestContext.Current.CancellationToken));
        Assert.Contains("must follow a pattern", exception.Message);
    }

    /// <summary>
    ///     Test that the walk selects exactly the files the exact per-file rule selects, whatever is pruned.
    /// </summary>
    [Fact]
    public void FileSelector_Select_OrderedLists_MatchesExactRuleForEveryFile()
    {
        // Arrange: a tree that touches the shapes of the lists, and the lists to try
        using var temp = new TempDirectory();
        string[] all =
        [
            "a.md", "docs/a.md", "docs/a/b.md", "docs/keep/a.md", "docs/z/a.md", "src/a.md", "src/x/a.md", "x/a.md",
            "build/keep.md", "build/other.md", "src/build/a.md", ".git/c.md", "docs/.hidden/d.md",
        ];
        foreach (var file in all)
        {
            temp.CreateFile(file);
        }

        string[][] lists =
        [
            ["**/*.md", "!docs"],
            ["**/*.md", "!docs", "docs/keep/**"],
            ["**/*.md", "!docs/**", "docs/a/**", "!docs/a/b.md"],
            ["**/*", "!{docs,src}/**", "src/x/**"],
            ["docs/**", "!docs/a/**", "x/**"],
            ["**/*.md", "!**/build/**", "build/keep.md"],
            ["**/*.md", "!docs/**/*.md", "docs/a/b.md"],
            ["**/*.md", "!/docs/*/", "docs/z/a.md"],
            ["**/*.md", "!docs/a"],
            ["**/*.md", "!docs/a", "docs/a/b.md"],
        ];

        foreach (var patterns in lists)
        {
            // Act: walk the tree, and ask the exact rule about every file of the tree
            var walked = SelectPaths(temp, patterns);
            var list = OrderedGlobList.Parse(patterns, false);
            var exact = all.Where(list.IsMatch).Order(StringComparer.Ordinal).ToArray();

            // Assert: pruning lost nothing and added nothing
            Assert.Equal(exact, walked);
        }
    }
}
