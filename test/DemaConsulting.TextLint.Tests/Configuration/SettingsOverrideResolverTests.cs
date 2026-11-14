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

// cspell:ignore zorb quix wrold

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Unit tests for the <see cref="SettingsOverrideResolver"/> class.
/// </summary>
/// <remarks>
///     The resolver is pure, so it is tested without a file system, on settings built in code and on path
///     strings. The real ordered glob list and spelling dictionary selection it uses are exercised, because
///     they are pure too.
/// </remarks>
public class SettingsOverrideResolverTests
{
    /// <summary>
    ///     Creates a resolver for settings that hold the given overrides.
    /// </summary>
    /// <param name="overrides">The overrides.</param>
    /// <returns>The resolver, with no command-line language.</returns>
    private static SettingsOverrideResolver For(params SettingsOverride[] overrides) =>
        new(new TextLintSettings { Overrides = overrides }, null);

    /// <summary>
    ///     Test that a file that no override matches gets the top-level enabled value and no overrides.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_Resolve_NoOverrideMatches_IsEnabledWithNoMatches()
    {
        // Arrange: one override that matches only docs
        var resolver = For(new SettingsOverride { Filename = ["docs/**"], Words = ["zorb"] });

        // Act: resolve a file elsewhere
        var resolution = resolver.Resolve("src/a.md");

        // Assert: it is enabled and no override applies
        Assert.Multiple(
            () => Assert.True(resolution.Enabled),
            () => Assert.Empty(resolution.MatchedOverrides),
            () => Assert.Equal(string.Empty, resolution.SettingsKey));
    }

    /// <summary>
    ///     Test that every matching override is reported, in the order written.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_Resolve_SeveralOverridesMatch_ReportsAllInOrder()
    {
        // Arrange: three overrides, the first and the third match
        var resolver = For(
            new SettingsOverride { Filename = ["**/*.md"], Words = ["zorb"] },
            new SettingsOverride { Filename = ["src/**"], Words = ["quix"] },
            new SettingsOverride { Filename = ["docs/**"], Enabled = true });

        // Act: resolve a documentation file
        var resolution = resolver.Resolve("docs/a.md");

        // Assert: the first and third match, in order, and only the first changes the checker
        Assert.Multiple(
            () => Assert.Equal([0, 2], resolution.MatchedOverrides),
            () => Assert.Equal([0], resolution.SettingsOverrides),
            () => Assert.Equal("0", resolution.SettingsKey));
    }

    /// <summary>
    ///     Test that an ordered filename list with a negation excludes and re-includes.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_Resolve_OrderedFilenameWithNegation_LastMatchWins()
    {
        // Arrange: an override for markdown except docs, but docs/keep again
        var resolver = For(new SettingsOverride { Filename = ["**/*.md", "!docs/**", "docs/keep/**"], Words = ["zorb"] });

        // Act / Assert: the last matching pattern decides
        Assert.Multiple(
            () => Assert.Single(resolver.Resolve("a.md").MatchedOverrides),
            () => Assert.Empty(resolver.Resolve("docs/a.md").MatchedOverrides),
            () => Assert.Single(resolver.Resolve("docs/keep/a.md").MatchedOverrides));
    }

    /// <summary>
    ///     Test that matching is case-sensitive.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_Resolve_DifferentCase_DoesNotMatch()
    {
        // Arrange: an override for a lower-case directory
        var resolver = For(new SettingsOverride { Filename = ["docs/**"], Words = ["zorb"] });

        // Act / Assert: a different case does not match
        Assert.Empty(resolver.Resolve("Docs/a.md").MatchedOverrides);
    }

    /// <summary>
    ///     Test that a wildcard filename matches hidden names only when glob-dot is enabled.
    /// </summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void SettingsOverrideResolver_Resolve_HiddenFile_FollowsEnableGlobDot(bool enableGlobDot, int expectedMatches)
    {
        // Arrange: settings with an override for all markdown files
        var settings = new TextLintSettings
        {
            EnableGlobDot = enableGlobDot,
            Overrides = [new SettingsOverride { Filename = ["**/*.md"], Words = ["zorb"] }],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: resolve a file in a hidden directory
        var resolution = resolver.Resolve(".github/a.md");

        // Assert: it matches only with hidden names enabled
        Assert.Equal(expectedMatches, resolution.MatchedOverrides.Length);
    }

    /// <summary>
    ///     Test the rule for the enabled setting: the last matching override that sets it wins.
    /// </summary>
    [Theory]
    [InlineData(true, new[] { "-" }, true)]
    [InlineData(false, new[] { "-" }, false)]
    [InlineData(false, new[] { "true" }, true)]
    [InlineData(true, new[] { "false" }, false)]
    [InlineData(true, new[] { "false", "true" }, true)]
    [InlineData(true, new[] { "true", "false" }, false)]
    [InlineData(false, new[] { "true", "-" }, true)]
    [InlineData(false, new[] { "-", "-" }, false)]
    public void SettingsOverrideResolver_Resolve_EnabledCombinations_LastSettingWins(bool topLevel, string[] values, bool expected)
    {
        // Arrange: overrides that all match the same file, each setting enabled or leaving it ("-")
        var overrides = values.Select(value => new SettingsOverride
        {
            Filename = ["**/*.md"],
            Enabled = value == "-" ? null : bool.Parse(value),
            Words = value == "-" ? ["zorb"] : [],
        }).ToArray();
        var resolver = new SettingsOverrideResolver(new TextLintSettings { Enabled = topLevel, Overrides = overrides }, null);

        // Act: resolve a markdown file
        var resolution = resolver.Resolve("a.md");

        // Assert: the result follows the last override that sets the value
        Assert.Equal(expected, resolution.Enabled);
    }

    /// <summary>
    ///     Test that a file outside the settings root gets the top-level enabled value and no overrides.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_ResolveUnmatched_UsesTopLevelEnabled()
    {
        // Arrange: disabled settings with an override that would enable everything
        var settings = new TextLintSettings
        {
            Enabled = false,
            Overrides = [new SettingsOverride { Filename = ["**"], Enabled = true }],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: resolve a file that has no path relative to the settings root
        var resolution = resolver.ResolveUnmatched();

        // Assert: it is disabled and no override applies
        Assert.Multiple(
            () => Assert.False(resolution.Enabled),
            () => Assert.Empty(resolution.MatchedOverrides));
    }

    /// <summary>
    ///     Test that list settings add to the top-level values in the order of the overrides.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_Lists_AreConcatenatedInOrder()
    {
        // Arrange: top-level lists and two matching overrides that add to every list
        var settings = new TextLintSettings
        {
            Words = ["top1"],
            IgnoreWords = ["ignoretop"],
            FlagWords = ["flagtop"],
            IgnoreRegExpList = ["Urls"],
            Overrides =
            [
                new SettingsOverride { Filename = ["**"], Words = ["first1"], IgnoreWords = ["ignorefirst"], FlagWords = ["flagfirst"], IgnoreRegExpList = ["/first/"] },
                new SettingsOverride { Filename = ["**"], Words = ["second1", "second2"] },
            ],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: merge for a file both match
        var effective = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));

        // Assert: the top-level values come first, then each override's, in order
        Assert.Multiple(
            () => Assert.Equal(["top1", "first1", "second1", "second2"], effective.Words),
            () => Assert.Equal(["ignoretop", "ignorefirst"], effective.IgnoreWords),
            () => Assert.Equal(["flagtop", "flagfirst"], effective.FlagWords),
            () => Assert.Equal(["Urls", "/first/"], effective.IgnoreRegExpList),
            () => Assert.Empty(effective.Overrides));
    }

    /// <summary>
    ///     Test that single values are replaced by the last matching override that sets them.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_SingleValues_LastSettingWins()
    {
        // Arrange: top-level values, an override that sets all, and a later one that sets only the word length
        var settings = new TextLintSettings
        {
            Language = "en",
            MinWordLength = 4,
            IgnoreRandomStrings = true,
            MinRandomLength = 40,
            Overrides =
            [
                new SettingsOverride { Filename = ["**"], Language = "en-GB", MinWordLength = 5, IgnoreRandomStrings = false, MinRandomLength = 20 },
                new SettingsOverride { Filename = ["**"], MinWordLength = 6 },
            ],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: merge for a file both match
        var effective = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));

        // Assert: each value comes from the last override that sets it
        Assert.Multiple(
            () => Assert.Equal("en-GB", effective.Language),
            () => Assert.Equal(6, effective.MinWordLength),
            () => Assert.False(effective.IgnoreRandomStrings),
            () => Assert.Equal(20, effective.MinRandomLength));
    }

    /// <summary>
    ///     Test that the settings that are not overridden are copied unchanged.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_OtherSettings_AreCopied()
    {
        // Arrange: settings with values that no override can change
        var settings = new TextLintSettings
        {
            Version = "0.2",
            Files = ["**/*.md"],
            IgnorePaths = ["build"],
            EnableGlobDot = true,
            MaxFileSize = 2048,
            Overrides = [new SettingsOverride { Filename = ["**"], Words = ["zorb"] }],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: merge for a matching file
        var effective = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));

        // Assert: the other settings are as they were
        Assert.Multiple(
            () => Assert.Equal("0.2", effective.Version),
            () => Assert.Equal(["**/*.md"], effective.Files),
            () => Assert.Equal(["build"], effective.IgnorePaths),
            () => Assert.True(effective.EnableGlobDot),
            () => Assert.Equal(2048, effective.MaxFileSize));
    }

    /// <summary>
    ///     Test that an override's pattern definition replaces a definition of the same name.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_PatternWithSameName_IsReplaced()
    {
        // Arrange: a top-level pattern, an override that redefines it, a later override that redefines it again,
        // and an override with a new name
        var settings = new TextLintSettings
        {
            Patterns = [new NamedPattern("Mine", "/top/"), new NamedPattern("Kept", "/kept/")],
            Overrides =
            [
                new SettingsOverride { Filename = ["**"], Patterns = [new NamedPattern("mine", "/first/")] },
                new SettingsOverride { Filename = ["**"], Patterns = [new NamedPattern("MINE", "/second/"), new NamedPattern("Extra", "/extra/")] },
            ],
        };
        var resolver = new SettingsOverrideResolver(settings, null);

        // Act: merge for a file both match, and for a file only the first matches by resolving the first alone
        var both = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));
        var first = resolver.GetEffectiveSettingsForOverride(0);

        // Assert: the name has one definition, the latest, compared ignoring case; other names stay
        Assert.Multiple(
            () => Assert.Equal(["Kept", "MINE", "Extra"], both.Patterns.Select(pattern => pattern.Name)),
            () => Assert.Equal("/second/", both.Patterns.Single(pattern => pattern.Name == "MINE").Pattern),
            () => Assert.Equal(["Kept", "mine"], first.Patterns.Select(pattern => pattern.Name)));
    }

    /// <summary>
    ///     Test that the language of the command line wins over overrides and the top level.
    /// </summary>
    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-GB", "en-GB")]
    [InlineData(null, "en-GB")]
    public void SettingsOverrideResolver_GetEffectiveSettings_LanguagePrecedence_CommandLineWins(string? commandLine, string expected)
    {
        // Arrange: a top-level language, and an override that sets British English
        var settings = new TextLintSettings
        {
            Language = "en",
            Overrides = [new SettingsOverride { Filename = ["**"], Language = "en-GB" }],
        };
        var resolver = new SettingsOverrideResolver(settings, commandLine);

        // Act: merge for a matching file
        var effective = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));

        // Assert: the command-line language, when given, wins
        Assert.Equal(expected, effective.Language);
    }

    /// <summary>
    ///     Test that the command-line language applies to files that no override matches too.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_NoOverrideMatches_AppliesCommandLineLanguage()
    {
        // Arrange: a resolver with a command-line language and an override that matches nothing here
        var settings = new TextLintSettings { Overrides = [new SettingsOverride { Filename = ["docs/**"], Words = ["zorb"] }] };
        var resolver = new SettingsOverrideResolver(settings, "en-GB");

        // Act: merge for a file that matches no override
        var effective = resolver.GetEffectiveSettings(resolver.Resolve("src/a.md"));

        // Assert: the language is the command-line one
        Assert.Equal("en-GB", effective.Language);
    }

    /// <summary>
    ///     Test that the merged settings of one combination are built once and shared.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetEffectiveSettings_SameCombination_ReturnsSameInstance()
    {
        // Arrange: an override that matches two files
        var resolver = For(new SettingsOverride { Filename = ["**/*.md"], Words = ["zorb"] });

        // Act: merge for two files with the same combination, and for a file with none
        var first = resolver.GetEffectiveSettings(resolver.Resolve("a.md"));
        var second = resolver.GetEffectiveSettings(resolver.Resolve("b/c.md"));
        var none = resolver.GetEffectiveSettings(resolver.Resolve("a.txt"));

        // Assert: the combination is shared, and differs from the top-level one
        Assert.Multiple(
            () => Assert.Same(first, second),
            () => Assert.NotSame(first, none),
            () => Assert.Empty(none.Words));
    }

    /// <summary>
    ///     Test which overrides count as having settings for the checker.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_HasCheckerSettings_ReportsWhetherCheckerIsAffected()
    {
        // Arrange: overrides that only toggle enabled, and that set each kind of checker setting
        var resolver = For(
            new SettingsOverride { Filename = ["a"], Enabled = false },
            new SettingsOverride { Filename = ["a"], Words = ["zorb"] },
            new SettingsOverride { Filename = ["a"], IgnoreWords = ["zorb"] },
            new SettingsOverride { Filename = ["a"], FlagWords = ["zorb"] },
            new SettingsOverride { Filename = ["a"], IgnoreRegExpList = ["Urls"] },
            new SettingsOverride { Filename = ["a"], Patterns = [new NamedPattern("Mine", "/x/")] },
            new SettingsOverride { Filename = ["a"], Language = "en" },
            new SettingsOverride { Filename = ["a"], MinWordLength = 3 },
            new SettingsOverride { Filename = ["a"], IgnoreRandomStrings = false },
            new SettingsOverride { Filename = ["a"], MinRandomLength = 3 });

        // Act / Assert: only the first has nothing for the checker
        Assert.Multiple(
            () => Assert.False(resolver.HasCheckerSettings(0)),
            () => Assert.All(Enumerable.Range(1, 9), index => Assert.True(resolver.HasCheckerSettings(index))));
    }

    /// <summary>
    ///     Test that the overrides are exposed by index and counted.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_GetOverride_Index_ReturnsOverride()
    {
        // Arrange: two overrides
        var first = new SettingsOverride { Filename = ["a"], Words = ["zorb"] };
        var second = new SettingsOverride { Filename = ["b"], Words = ["quix"] };
        var resolver = For(first, second);

        // Act / Assert: they are counted and returned as given
        Assert.Multiple(
            () => Assert.Equal(2, resolver.OverrideCount),
            () => Assert.Same(second, resolver.GetOverride(1)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => resolver.GetOverride(2)));
    }

    /// <summary>
    ///     Test that an override without a filename pattern is rejected.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_New_EmptyFilename_ThrowsArgumentException()
    {
        // Arrange: an override that has no filename
        var settings = new TextLintSettings { Overrides = [new SettingsOverride { Words = ["zorb"] }] };

        // Act / Assert: the resolver refuses it and says which override
        var exception = Assert.Throws<ArgumentException>(() => new SettingsOverrideResolver(settings, null));
        Assert.Contains("Override 1", exception.Message);
    }

    /// <summary>
    ///     Test that unusable filename patterns are rejected naming the pattern.
    /// </summary>
    [Theory]
    [InlineData("a?")]
    [InlineData("!docs/**")]
    [InlineData("../x")]
    public void SettingsOverrideResolver_New_UnusableFilenamePattern_ThrowsInvalidGlobPattern(string pattern)
    {
        // Arrange: an override whose first filename pattern is unusable
        var settings = new TextLintSettings { Overrides = [new SettingsOverride { Filename = [pattern], Words = ["zorb"] }] };

        // Act / Assert: the pattern is named
        var exception = Assert.Throws<InvalidGlobPatternException>(() => new SettingsOverrideResolver(settings, null));
        Assert.Equal(pattern, exception.Pattern);
    }

    /// <summary>
    ///     Test that an unsupported override language is rejected even when a command-line language hides it.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_New_UnsupportedOverrideLanguage_ThrowsArgumentException()
    {
        // Arrange: an override with a language that does not exist, and a valid command-line language
        var settings = new TextLintSettings { Overrides = [new SettingsOverride { Filename = ["**"], Language = "klingon" }] };

        // Act / Assert: the language is reported
        var exception = Assert.Throws<ArgumentException>(() => new SettingsOverrideResolver(settings, "en-GB"));
        Assert.Contains("klingon", exception.Message);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a resolver
        var resolver = For(new SettingsOverride { Filename = ["a"], Words = ["zorb"] });

        // Act / Assert: each null argument is a programming error
        Assert.Throws<ArgumentNullException>(() => new SettingsOverrideResolver(null!, null));
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        Assert.Throws<ArgumentNullException>(() => resolver.GetEffectiveSettings(null!));
    }

    /// <summary>
    ///     Test that a resolver can be used from several threads at once.
    /// </summary>
    [Fact]
    public void SettingsOverrideResolver_ConcurrentUse_GivesTheSameAnswers()
    {
        // Arrange: a resolver with overlapping overrides, and the answer for each path on one thread
        var resolver = For(
            new SettingsOverride { Filename = ["**/*.md"], Words = ["zorb"] },
            new SettingsOverride { Filename = ["docs/**"], Words = ["quix"] });
        string[] paths = ["a.md", "docs/a.md", "docs/b.txt", "src/c.cs"];
        var expected = paths.Select(path => resolver.Resolve(path).SettingsKey).ToArray();

        // Act: resolve and merge from many threads
        var results = new string[8][];
        Parallel.For(0, results.Length, index =>
            results[index] = [.. paths.Select(path => { var resolution = resolver.Resolve(path); _ = resolver.GetEffectiveSettings(resolution); return resolution.SettingsKey; })]);

        // Assert: every thread got the same answers
        Assert.All(results, result => Assert.Equal(expected, result));
    }
}
