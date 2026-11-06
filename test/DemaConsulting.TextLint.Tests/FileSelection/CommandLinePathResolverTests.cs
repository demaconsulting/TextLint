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
///     Unit tests for the <see cref="CommandLinePathResolver"/> class and the <see cref="CommandLineSelection"/> it returns.
/// </summary>
/// <remarks>
///     The resolver is pure string logic, so most tests call the internal overload that takes the path
///     rules as a parameter. That lets every test prove both the Windows and the Unix rules on whatever
///     operating system runs the tests, with no file system and no test doubles. The few tests of the
///     public method use the rules of the operating system they run on and so assert per platform.
/// </remarks>
public class CommandLinePathResolverTests
{
    /// <summary>
    ///     The root directory used by the Windows-rules tests, as Windows writes it.
    /// </summary>
    private const string WindowsRoot = @"C:\work\proj";

    /// <summary>
    ///     The root directory used by the Unix-rules tests.
    /// </summary>
    private const string UnixRoot = "/home/x";

    /// <summary>
    ///     Resolves arguments under the Windows rules with the standard Windows root.
    /// </summary>
    /// <param name="arguments">The arguments as typed.</param>
    /// <returns>The resolved selection.</returns>
    private static CommandLineSelection ResolveWindows(params string[] arguments) =>
        CommandLinePathResolver.ResolveWithStyle(WindowsRoot, arguments, windowsPaths: true);

    /// <summary>
    ///     Resolves arguments under the Unix rules with the standard Unix root.
    /// </summary>
    /// <param name="arguments">The arguments as typed.</param>
    /// <returns>The resolved selection.</returns>
    private static CommandLineSelection ResolveUnix(params string[] arguments) =>
        CommandLinePathResolver.ResolveWithStyle(UnixRoot, arguments, windowsPaths: false);

    /// <summary>
    ///     Test that on Windows a relative path written with backslashes becomes a portable pattern.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsBackslashRelative_ConvertsSeparators()
    {
        // Arrange: the form that PowerShell tab completion produces

        // Act: resolve under the Windows rules
        var selection = ResolveWindows(@".\docs\guide.md");

        // Assert: the pattern is portable, relative paths are reported, and the root is unchanged
        Assert.Multiple(
            () => Assert.Equal(["./docs/guide.md"], selection.Patterns),
            () => Assert.False(selection.ReportsAbsolutePaths),
            () => Assert.Equal("C:/work/proj", selection.RootDirectory));
    }

    /// <summary>
    ///     Test that outside Windows a backslash is not a separator and the strict pattern rules reject it.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_UnixBackslashRelative_IsRejected()
    {
        // Arrange: the same text as the Windows case, where it is an ordinary character instead

        // Act: resolve under the Unix rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveUnix(@"docs\guide.md"));

        // Assert: the error names the argument and says to use a slash
        Assert.Multiple(
            () => Assert.Equal(@"docs\guide.md", exception.Pattern),
            () => Assert.Contains("use '/' as the path separator", exception.Message));
    }

    /// <summary>
    ///     Test that a Windows absolute file inside the root becomes an anchored root-relative pattern.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsAbsoluteFileInsideRoot_AnchorsBeneathRoot()
    {
        // Arrange: a full path to a file beneath the root, written with backslashes

        // Act: resolve under the Windows rules
        var selection = ResolveWindows(@"C:\work\proj\docs\guide.md");

        // Assert: the root is kept, the pattern is anchored, and absolute paths are reported
        Assert.Multiple(
            () => Assert.Equal("C:/work/proj", selection.RootDirectory),
            () => Assert.Equal(["/docs/guide.md"], selection.Patterns),
            () => Assert.True(selection.ReportsAbsolutePaths));
    }

    /// <summary>
    ///     Test that a file directly in the root is anchored there and not matched at any depth.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_AbsoluteFileDirectlyInRoot_IsAnchored()
    {
        // Arrange: a full path to a file that sits in the root directory itself

        // Act: resolve under the Unix rules
        var selection = ResolveUnix("/home/x/README.md");

        // Assert: the leading slash stops a bare file name from matching at every depth
        Assert.Equal(["/README.md"], selection.Patterns);
    }

    /// <summary>
    ///     Test that Windows drive letters and directory names are compared ignoring case.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsDifferentCase_IsInsideRoot()
    {
        // Arrange: a path that names the root with different letter case

        // Act: resolve under the Windows rules
        var selection = ResolveWindows("c:/WORK/Proj/README.md");

        // Assert: it is treated as inside the root, so the root stays the search base
        Assert.Multiple(
            () => Assert.Equal("C:/work/proj", selection.RootDirectory),
            () => Assert.Equal(["/README.md"], selection.Patterns));
    }

    /// <summary>
    ///     Test that outside Windows directory names are compared case-sensitively.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_UnixDifferentCase_IsOutsideRoot()
    {
        // Arrange: a path that differs from the root only in letter case

        // Act: resolve under the Unix rules
        var selection = ResolveUnix("/Home/X/a.md");

        // Assert: it is a different directory, so its own directory becomes the search base
        Assert.Multiple(
            () => Assert.Equal("/Home/X", selection.RootDirectory),
            () => Assert.Equal(["/a.md"], selection.Patterns));
    }

    /// <summary>
    ///     Test that the wildcard part of an absolute argument is kept as the pattern.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_AbsoluteWildcardInsideRoot_KeepsWildcardPart()
    {
        // Arrange: a full path whose directory is literal and whose rest is a recursive pattern

        // Act: resolve under the Windows rules, with forward slashes as PowerShell also accepts them
        var selection = ResolveWindows("C:/work/proj/docs/**/*.{md,txt}");

        // Assert: the literal directory moves into the anchored pattern and the wildcard part is unchanged
        Assert.Equal(["/docs/**/*.{md,txt}"], selection.Patterns);
    }

    /// <summary>
    ///     Test that the literal leading part of an absolute argument outside the root becomes the search base.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_AbsoluteOutsideRoot_UsesLiteralPartAsBase()
    {
        // Arrange: a pattern on another drive

        // Act: resolve under the Windows rules
        var selection = ResolveWindows(@"D:\other\notes\*.md");

        // Assert: the literal directory is the search base and the pattern is relative to it
        Assert.Multiple(
            () => Assert.Equal("D:/other/notes", selection.RootDirectory),
            () => Assert.Equal(["/*.md"], selection.Patterns),
            () => Assert.True(selection.ReportsAbsolutePaths));
    }

    /// <summary>
    ///     Test that an argument that ends with a separator selects everything beneath the directory.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_AbsoluteDirectory_SelectsEverythingBeneath()
    {
        // Arrange: a directory written with a trailing backslash

        // Act: resolve under the Windows rules
        var selection = ResolveWindows(@"C:\work\proj\docs\");

        // Assert: the whole subtree is selected, anchored at the directory
        Assert.Equal(["/docs/**"], selection.Patterns);
    }

    /// <summary>
    ///     Test that the root of a drive or of the file system is itself a valid directory argument.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_FileSystemRootDirectory_SelectsEverything()
    {
        // Arrange: only the root of the file system, outside the working root

        // Act: resolve under the Unix rules
        var selection = ResolveUnix("/");

        // Assert: the search base is the file system root and everything beneath it is selected
        Assert.Multiple(
            () => Assert.Equal("/", selection.RootDirectory),
            () => Assert.Equal(["/**"], selection.Patterns));
    }

    /// <summary>
    ///     Test that absolute paths outside the root share their deepest common directory as the search base.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_TwoAbsoluteOutsideRoot_UsesCommonAncestor()
    {
        // Arrange: two files in sibling directories, neither inside the root

        // Act: resolve under the Unix rules
        var selection = ResolveUnix("/data/a/x.md", "/data/b/y.md");

        // Assert: the common directory is the base and each pattern is anchored beneath it
        Assert.Multiple(
            () => Assert.Equal("/data", selection.RootDirectory),
            () => Assert.Equal(["/a/x.md", "/b/y.md"], selection.Patterns));
    }

    /// <summary>
    ///     Test that absolute paths on different drives cannot be combined.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_DifferentDrives_ThrowsArgumentException()
    {
        // Arrange: two files on two drives, both outside the root

        // Act / Assert: no directory contains both, so the combination is rejected
        var exception = Assert.Throws<ArgumentException>(() => ResolveWindows(@"D:\a\x.md", @"E:\b\y.md"));
        Assert.Contains("different drives", exception.Message);
    }

    /// <summary>
    ///     Test that a relative argument cannot be combined with an absolute one outside the root.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_RelativeAndOutsideAbsolute_ThrowsArgumentException()
    {
        // Arrange: a relative file name and an absolute path outside the root

        // Act / Assert: the error names both arguments
        var exception = Assert.Throws<ArgumentException>(() => ResolveUnix("README.md", "/data/x.md"));
        Assert.Multiple(
            () => Assert.Contains("'README.md'", exception.Message),
            () => Assert.Contains("'/data/x.md'", exception.Message));
    }

    /// <summary>
    ///     Test that relative arguments and absolute arguments inside the root combine in one selection.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_RelativeAndInsideAbsolute_AreCombined()
    {
        // Arrange: a relative bare name and an absolute path inside the root

        // Act: resolve under the Unix rules
        var selection = ResolveUnix("README.md", "/home/x/docs/a.md");

        // Assert: both patterns are kept and, because one was absolute, absolute paths are reported
        Assert.Multiple(
            () => Assert.Equal(["README.md", "/docs/a.md"], selection.Patterns),
            () => Assert.True(selection.ReportsAbsolutePaths));
    }

    /// <summary>
    ///     Test that a network path uses its server and share as the root.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPath_UsesServerAndShareAsRoot()
    {
        // Arrange: a UNC path with a wildcard

        // Act: resolve under the Windows rules
        var selection = ResolveWindows(@"\\server\share\docs\*.md");

        // Assert: the network path is outside the root, so its literal directory is the base
        Assert.Multiple(
            () => Assert.Equal("//server/share/docs", selection.RootDirectory),
            () => Assert.Equal(["/*.md"], selection.Patterns));
    }

    /// <summary>
    ///     Test that a network path without a share is rejected, naming the argument as typed.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsNetworkPathWithoutShare_IsRejected()
    {
        // Arrange: a server name only

        // Act: resolve under the Windows rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveWindows(@"\\server"));

        // Assert: the error names the argument and what is missing
        Assert.Multiple(
            () => Assert.Equal(@"\\server", exception.Pattern),
            () => Assert.Contains("server and share", exception.Message));
    }

    /// <summary>
    ///     Test that a Windows drive-relative path is rejected.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsDriveRelative_IsRejected()
    {
        // Arrange: a path that names a drive but not a directory on it

        // Act: resolve under the Windows rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveWindows(@"C:docs\a.md"));

        // Assert: the error names the argument as typed and says why
        Assert.Multiple(
            () => Assert.Equal(@"C:docs\a.md", exception.Pattern),
            () => Assert.Contains("drive-relative", exception.Message));
    }

    /// <summary>
    ///     Test that a Windows path that starts with a separator but names no drive is rejected.
    /// </summary>
    [Theory]
    [InlineData(@"\docs\a.md")]
    [InlineData("/docs/a.md")]
    public void CommandLinePathResolver_ResolveWithStyle_WindowsRootedWithoutDrive_IsRejected(string argument)
    {
        // Arrange: the argument is a parameter, written with either separator

        // Act: resolve under the Windows rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveWindows(argument));

        // Assert: the error names the argument as typed and says it is ambiguous
        Assert.Multiple(
            () => Assert.Equal(argument, exception.Pattern),
            () => Assert.Contains("ambiguous on Windows", exception.Message));
    }

    /// <summary>
    ///     Test that outside Windows a drive path is not absolute and the strict pattern rules reject it.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_UnixDriveLetterPath_IsRejected()
    {
        // Arrange: a Windows-style absolute path given on a system where it names nothing

        // Act: resolve under the Unix rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveUnix("C:/work/a.md"));

        // Assert: the strict rules say that absolute paths are not supported
        Assert.Contains("absolute paths", exception.Message);
    }

    /// <summary>
    ///     Test that a tilde is never expanded, on either set of rules.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CommandLinePathResolver_ResolveWithStyle_Tilde_IsNotExpanded(bool windowsPaths)
    {
        // Arrange: a home-directory shorthand that only a shell may expand
        var root = windowsPaths ? WindowsRoot : UnixRoot;

        // Act: resolve under either rule set
        var selection = CommandLinePathResolver.ResolveWithStyle(root, ["~/docs/*.md"], windowsPaths);

        // Assert: the text stays a relative pattern
        Assert.Multiple(
            () => Assert.Equal(["~/docs/*.md"], selection.Patterns),
            () => Assert.False(selection.ReportsAbsolutePaths));
    }

    /// <summary>
    ///     Test that parent and current-directory segments before the first wildcard are resolved lexically.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_ParentSegmentsInLiteralPart_AreNormalized()
    {
        // Arrange: an absolute path that goes into docs and back out again
        var argument = "/home/x/docs/../src/./a.md";

        // Act: resolve under the Unix rules
        var selection = ResolveUnix(argument);

        // Assert: the redundant segments are gone
        Assert.Equal(["/src/a.md"], selection.Patterns);
    }

    /// <summary>
    ///     Test that a parent segment that would climb above the file system root is rejected.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_ParentAboveFileSystemRoot_IsRejected()
    {
        // Arrange: a path with more parent segments than directories

        // Act: resolve under the Unix rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveUnix("/../a.md"));

        // Assert: the error names the argument
        Assert.Multiple(
            () => Assert.Equal("/../a.md", exception.Pattern),
            () => Assert.Contains("climbs above the root", exception.Message));
    }

    /// <summary>
    ///     Test that a rejection by the pattern rules names the argument as typed, not the rewritten form.
    /// </summary>
    [Theory]
    [InlineData(@"C:\work\proj\a?.md", "'?' wildcard")]
    [InlineData(@"C:\work\proj\*\..\a.md", "'..'")]
    [InlineData(@"C:\work\proj\docs\{a,b", "braces are not balanced")]
    public void CommandLinePathResolver_ResolveWithStyle_PatternRuleViolation_NamesTypedArgument(string argument, string reason)
    {
        // Arrange: an absolute argument that breaks a pattern rule after the wildcard part is split off

        // Act: resolve under the Windows rules
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveWindows(argument));

        // Assert: the error names the text the user typed and gives the reason
        Assert.Multiple(
            () => Assert.Equal(argument, exception.Pattern),
            () => Assert.Contains(reason, exception.Message));
    }

    /// <summary>
    ///     Test that an empty or white-space argument is rejected by the pattern rules.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandLinePathResolver_ResolveWithStyle_BlankArgument_IsRejected(string argument)
    {
        // Arrange: the blank argument is a parameter

        // Act: resolve it
        var exception = Assert.Throws<InvalidGlobPatternException>(() => ResolveUnix(argument));

        // Assert: the error says the pattern is empty
        Assert.Contains("the pattern is empty", exception.Message);
    }

    /// <summary>
    ///     Test that an absolute path that names an existing directory selects everything below it, on both rule sets.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CommandLinePathResolver_ResolveWithStyle_ExistingDirectory_SelectsEverythingBeneath(bool windowsPaths)
    {
        // Arrange: a directory inside the root and one outside it, which the probe reports as existing
        var root = windowsPaths ? WindowsRoot : UnixRoot;
        var inside = windowsPaths ? @"C:\work\proj\docs" : "/home/x/docs";
        var outside = windowsPaths ? @"D:\other\notes" : "/data/notes";

        // Act: resolve each, with a probe that says every path is a directory
        var insideSelection = CommandLinePathResolver.ResolveWithStyle(root, [inside], windowsPaths, _ => true);
        var outsideSelection = CommandLinePathResolver.ResolveWithStyle(root, [outside], windowsPaths, _ => true);

        // Assert: the directory is searched whole, and the one outside the root becomes the search base
        Assert.Multiple(
            () => Assert.Equal(["/docs/**"], insideSelection.Patterns),
            () => Assert.Equal(windowsPaths ? "C:/work/proj" : "/home/x", insideSelection.RootDirectory),
            () => Assert.Equal(["/**"], outsideSelection.Patterns),
            () => Assert.Equal(windowsPaths ? "D:/other/notes" : "/data/notes", outsideSelection.RootDirectory));
    }

    /// <summary>
    ///     Test that a path that does not exist, or is not a directory, stays a file name.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CommandLinePathResolver_ResolveWithStyle_NotADirectory_IsFileName(bool windowsPaths)
    {
        // Arrange: a path that the probe says is not a directory (missing, or a file)
        var root = windowsPaths ? WindowsRoot : UnixRoot;
        var argument = windowsPaths ? @"C:\work\proj\docs\README" : "/home/x/docs/README";

        // Act: resolve it with a probe that says no, and without a probe at all
        var withProbe = CommandLinePathResolver.ResolveWithStyle(root, [argument], windowsPaths, _ => false);
        var withoutProbe = CommandLinePathResolver.ResolveWithStyle(root, [argument], windowsPaths);

        // Assert: the last segment is a file name anchored in its directory
        Assert.Multiple(
            () => Assert.Equal(["/docs/README"], withProbe.Patterns),
            () => Assert.Equal(["/docs/README"], withoutProbe.Patterns));
    }

    /// <summary>
    ///     Test that the directory probe is asked only about literal absolute paths, with the portable form of the path.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_DirectoryProbe_AskedOnlyAboutLiteralAbsolutePaths()
    {
        // Arrange: a probe that records what it is asked
        var asked = new List<string>();

        // Act: resolve a literal absolute path, one with a trailing separator, one with a wildcard, and a relative one
        CommandLinePathResolver.ResolveWithStyle(
            WindowsRoot,
            [@"C:\work\proj\docs", @"C:\work\proj\guides\", @"C:\work\proj\*.md", "a.md"],
            windowsPaths: true,
            isDirectory: path =>
            {
                asked.Add(path);
                return false;
            });

        // Assert: only the first was looked up, in portable form
        Assert.Equal(["C:/work/proj/docs"], asked);
    }

    /// <summary>
    ///     Test that a failure of the directory probe is not mistaken for a missing path.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_ResolveWithStyle_DirectoryProbeFails_Propagates()
    {
        // Arrange: a probe that cannot examine the path

        // Act / Assert: the failure reaches the caller instead of the path being guessed to be a file name
        Assert.Throws<UnauthorizedAccessException>(() => CommandLinePathResolver.ResolveWithStyle(
            UnixRoot,
            ["/home/x/secret"],
            windowsPaths: false,
            isDirectory: _ => throw new UnauthorizedAccessException("denied")));
    }

    /// <summary>
    ///     Test that the public method looks the path up in the real file system.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_RealPaths_DirectoryIsSearchedAndFileOrMissingIsName()
    {
        // Arrange: a root with a directory and a file, and the path of something that does not exist
        var root = Path.Combine(Path.GetTempPath(), $"textlint-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", "a.md"), "x");
        try
        {
            // Act: resolve the directory, the file, and the missing path
            var directory = CommandLinePathResolver.Resolve(root, [Path.Combine(root, "docs")]);
            var file = CommandLinePathResolver.Resolve(root, [Path.Combine(root, "docs", "a.md")]);
            var missing = CommandLinePathResolver.Resolve(root, [Path.Combine(root, "docs", "nowhere")]);

            // Assert: only the directory selects everything below it
            Assert.Multiple(
                () => Assert.Equal(["/docs/**"], directory.Patterns),
                () => Assert.Equal(["/docs/a.md"], file.Patterns),
                () => Assert.Equal(["/docs/nowhere"], missing.Patterns));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that missing or empty input is rejected before anything is resolved.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_InvalidInput_Throws()
    {
        // Arrange: a valid root and argument list to vary one value at a time
        var arguments = new[] { "a.md" };

        // Act / Assert: each kind of invalid input throws the documented exception
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => CommandLinePathResolver.Resolve(null!, arguments)),
            () => Assert.Throws<ArgumentException>(() => CommandLinePathResolver.Resolve(" ", arguments)),
            () => Assert.Throws<ArgumentNullException>(() => CommandLinePathResolver.Resolve(".", null!)),
            () => Assert.Throws<ArgumentException>(() => CommandLinePathResolver.Resolve(".", [])),
            () => Assert.Throws<ArgumentException>(() => CommandLinePathResolver.Resolve(".", [null!])),
            () => Assert.Throws<ArgumentException>(() => CommandLinePathResolver.ResolveWithStyle("relative/root", arguments, false)));
    }

    /// <summary>
    ///     Test that a relative root directory is made absolute against the current directory.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_RelativeRoot_BecomesAbsolute()
    {
        // Arrange: a root written relative to the current directory

        // Act: resolve a relative argument against it
        var selection = CommandLinePathResolver.Resolve("some/dir", ["a.md"]);

        // Assert: the search base is the absolute form of the root, in this platform's form
        Assert.Equal(Path.GetFullPath("some/dir"), selection.RootDirectory);
    }

    /// <summary>
    ///     Test that an absolute path of the running operating system is recognized by the public method.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_PlatformAbsolutePath_IsAnchoredInsideRoot()
    {
        // Arrange: a root and a file beneath it, both written in this platform's own form
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "textlint-resolver-root"));
        var file = Path.Combine(root, "docs", "guide.md");

        // Act: resolve the platform path
        var selection = CommandLinePathResolver.Resolve(root, [file]);

        // Assert: it is inside the root, anchored, and reported as an absolute path
        Assert.Multiple(
            () => Assert.Equal(root, selection.RootDirectory),
            () => Assert.Equal(["/docs/guide.md"], selection.Patterns),
            () => Assert.True(selection.ReportsAbsolutePaths));
    }

    /// <summary>
    ///     Test that a backslash path is a separator only on Windows, through the public method.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_BackslashRelativePath_FollowsPlatformRules()
    {
        // Arrange: the PowerShell-style relative path
        var argument = @".\docs\guide.md";
        var root = Path.GetFullPath(Path.GetTempPath());

        // Act / Assert: Windows converts it; every other system rejects the backslash
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(["./docs/guide.md"], CommandLinePathResolver.Resolve(root, [argument]).Patterns);
        }
        else
        {
            Assert.Throws<InvalidGlobPatternException>(() => CommandLinePathResolver.Resolve(root, [argument]));
        }
    }

    /// <summary>
    ///     Test that the display path is the relative path unless absolute paths are reported.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_GetDisplayPath_RelativeAndAbsoluteReporting_FollowsSelection()
    {
        // Arrange: one selection that reports relative paths and one that reports absolute paths
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "textlint-resolver-display"));
        var relative = CommandLinePathResolver.Resolve(root, ["docs/*.md"]);
        var absolute = CommandLinePathResolver.Resolve(root, [Path.Combine(root, "docs", "*.md")]);

        // Act: name the same selected file under each
        var relativeName = relative.GetDisplayPath("docs/a.md");
        var absoluteName = absolute.GetDisplayPath("docs/a.md");

        // Assert: the relative selection leaves the path alone; the absolute one gives the platform path
        Assert.Multiple(
            () => Assert.Equal("docs/a.md", relativeName),
            () => Assert.Equal(Path.Combine(root, "docs", "a.md"), absoluteName),
            () => Assert.Throws<ArgumentNullException>(() => absolute.GetDisplayPath(null!)));
    }

    /// <summary>
    ///     Test that the patterns of a selection are copied and cannot be changed afterwards.
    /// </summary>
    [Fact]
    public void CommandLinePathResolver_Resolve_Patterns_AreImmutableCopy()
    {
        // Arrange: an argument list that the caller could change after the call
        var arguments = new List<string> { "a.md" };
        var selection = CommandLinePathResolver.Resolve(".", arguments);

        // Act: change the caller's list and try to change the selection through a mutable interface
        arguments[0] = "changed.md";
        var patterns = Assert.IsAssignableFrom<IList<string>>(selection.Patterns);

        // Assert: the selection kept its own copy and refuses changes
        Assert.Multiple(
            () => Assert.Equal(["a.md"], selection.Patterns),
            () => Assert.Throws<NotSupportedException>(() => patterns.Add("other")));
    }
}
