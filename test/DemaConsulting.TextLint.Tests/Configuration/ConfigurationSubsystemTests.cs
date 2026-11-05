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

// cspell:ignore colour kubectl nuget

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Subsystem tests for the Configuration subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="SettingsLoader"/> and <see cref="TextLintSettings"/> together
///     through real settings files. The subsystem's documented dependencies outside itself, the
///     glob pattern expander, the spelling dictionary selection, and the word tokenizer used to
///     validate values at load time, are used for real.
/// </remarks>
public class ConfigurationSubsystemTests
{
    /// <summary>
    ///     Writes a settings file into a temporary directory and loads it.
    /// </summary>
    /// <param name="temp">The temporary directory.</param>
    /// <param name="yaml">The text of the settings file.</param>
    /// <returns>The load result.</returns>
    private static Task<SettingsLoadResult> LoadAsync(TempDirectory temp, string yaml)
    {
        var path = temp.CreateFile(".textlint.yaml", yaml);
        return SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Test that loading a valid file returns settings.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_ValidFile_ReturnsSettings()
    {
        // Arrange: a valid settings file
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files:\n  - \"**/*.md\"\n");

        // Assert: the result holds settings and no diagnostics
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Diagnostics));
    }

    /// <summary>
    ///     Test that a missing file returns an error diagnostic instead of throwing.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_MissingFile_ReturnsErrorDiagnostic()
    {
        // Arrange: a path that does not exist
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, ".textlint.yaml");

        // Act: load it
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: no exception, no settings, and a diagnostic that names the file
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Equal(path, Assert.Single(result.Diagnostics).FilePath));
    }

    /// <summary>
    ///     Test that every supported key populates the settings.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_AllSupportedKeys_PopulatesSettings()
    {
        // Arrange: a file that sets every supported key
        using var temp = new TempDirectory();
        const string yaml =
            "version: \"0.2\"\nfiles: [\"**/*.md\"]\nignorePaths: [node_modules]\nenableGlobDot: true\n" +
            "language: en-GB\nwords: [kubectl]\nignoreWords: [nuget]\nflagWords: [colour]\nminWordLength: 3\nmaxFileSize: 2K\n";

        // Act: load it
        var result = await LoadAsync(temp, yaml);

        // Assert: every value is present in the settings
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("0.2", settings.Version),
            () => Assert.Equal(["**/*.md"], settings.Files),
            () => Assert.Equal(["node_modules"], settings.IgnorePaths),
            () => Assert.True(settings.EnableGlobDot),
            () => Assert.Equal("en-GB", settings.Language),
            () => Assert.Equal(["kubectl"], settings.Words),
            () => Assert.Equal(["nuget"], settings.IgnoreWords),
            () => Assert.Equal(["colour"], settings.FlagWords),
            () => Assert.Equal(3, settings.MinWordLength),
            () => Assert.Equal(2048L, settings.MaxFileSize));
    }

    /// <summary>
    ///     Test that an empty file loads default settings.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_EmptyFile_ReturnsDefaults()
    {
        // Arrange: an empty settings file
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, string.Empty);

        // Assert: the settings are the defaults, including the cspell defaults of the spelling settings
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Null(result.Settings!.Version),
            () => Assert.Empty(result.Settings!.Files),
            () => Assert.False(result.Settings!.EnableGlobDot),
            () => Assert.Equal("en", result.Settings!.Language),
            () => Assert.Equal(4, result.Settings!.MinWordLength),
            () => Assert.Equal(0L, result.Settings!.MaxFileSize));
    }

    /// <summary>
    ///     Test that settings built in code are immutable.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Settings_ConstructedInCode_IsImmutable()
    {
        // Arrange: a mutable list used to build settings in code
        var files = new List<string> { "**/*.md" };
        var settings = new TextLintSettings { Files = files, EnableGlobDot = true };

        // Act: change the caller's list afterwards
        files.Clear();

        // Assert: the settings keep their values
        Assert.Multiple(
            () => Assert.Equal(["**/*.md"], settings.Files),
            () => Assert.True(settings.EnableGlobDot));
    }

    /// <summary>
    ///     Test that copying loaded settings leaves the loaded settings unchanged.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_Settings_LoadedThenCopied_LeavesLoadedUnchanged()
    {
        // Arrange: loaded settings
        using var temp = new TempDirectory();
        var loaded = (await LoadAsync(temp, "files: [\"**/*.md\"]\n")).Settings!;

        // Act: derive a copy that replaces the include patterns
        var copy = loaded with { Files = ["**/*.cs"] };

        // Assert: the loaded settings are unchanged
        Assert.Multiple(
            () => Assert.Equal(["**/*.md"], loaded.Files),
            () => Assert.Equal(["**/*.cs"], copy.Files));
    }

    /// <summary>
    ///     Test that malformed YAML is reported with its file and position.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_MalformedYaml_ReportsPosition()
    {
        // Arrange: a file with an unclosed flow list
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files: [\"a\", \n");

        // Assert: one error carrying the file, line, and column
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.NotNull(diagnostic.FilePath),
            () => Assert.NotNull(diagnostic.Line),
            () => Assert.NotNull(diagnostic.Column));
    }

    /// <summary>
    ///     Test that several errors in one file are all reported.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_MultipleTypeErrors_ReportsAll()
    {
        // Arrange: a file with two wrongly typed values
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files: oops\nenableGlobDot: oops\n");

        // Assert: both errors are reported, each with a position
        Assert.Equal([1, 2], result.Diagnostics.Select(diagnostic => diagnostic.Line));
    }

    /// <summary>
    ///     Test that an unsupported key is an error naming the key and its position.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_UnsupportedKey_ReportsErrorWithKeyAndPosition()
    {
        // Arrange: a file with an unsupported key on line 2
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "version: 0.2\ncaseSensitive: true\n");

        // Assert: loading fails with an error that names the key and its position
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("'caseSensitive' is not a supported setting", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(1, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an absolute path pattern is reported with its file, line, and column.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_AbsolutePathPattern_ReportsErrorWithPosition()
    {
        // Arrange: a file whose include pattern is a network path
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files:\n  - \"//server/share/**\"\n");

        // Assert: loading fails with an error at the item that names the file and says the path is absolute
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column),
            () => Assert.Contains("absolute paths", diagnostic.Message));
    }

    /// <summary>
    ///     Test that an invalid glob pattern is reported with its file and position.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_InvalidPattern_ReportsErrorWithPosition()
    {
        // Arrange: a file whose include pattern is negated
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files:\n  - \"!**/*.md\"\n");

        // Assert: loading fails with an error at the pattern that names it
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Contains("!**/*.md", diagnostic.Message));
    }

    /// <summary>
    ///     Test that every spelling key in a file populates the settings.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_SpellingKeys_PopulatesSettings()
    {
        // Arrange: a file that sets the six spelling keys, with a size that has a unit
        using var temp = new TempDirectory();
        const string yaml =
            "language: en-GB\nwords: [kubectl, k8s]\nignoreWords: [nuget]\nflagWords: [\"colour->color\"]\n" +
            "minWordLength: 7\nmaxFileSize: 1.5MB\n";

        // Act: load it
        var result = await LoadAsync(temp, yaml);

        // Assert: every value is in the settings, with the size converted to bytes
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("en-GB", settings.Language),
            () => Assert.Equal(["kubectl", "k8s"], settings.Words),
            () => Assert.Equal(["nuget"], settings.IgnoreWords),
            () => Assert.Equal(["colour->color"], settings.FlagWords),
            () => Assert.Equal(7, settings.MinWordLength),
            () => Assert.Equal(1572864L, settings.MaxFileSize));
    }

    /// <summary>
    ///     Test that spelling keys that are absent leave the cspell defaults in force.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_SpellingKeysAbsent_UsesCspellDefaults()
    {
        // Arrange: a file that sets no spelling key
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "files: [\"**/*.md\"]\n");

        // Assert: American English, a minimum word length of 4, empty lists, and no size limit
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("en", settings.Language),
            () => Assert.Equal(4, settings.MinWordLength),
            () => Assert.Equal(0L, settings.MaxFileSize),
            () => Assert.Empty(settings.Words),
            () => Assert.Empty(settings.IgnoreWords),
            () => Assert.Empty(settings.FlagWords));
    }

    /// <summary>
    ///     Test that word lists supplied in code are copied and cannot be changed afterwards.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Settings_WordListsConstructedInCode_AreImmutable()
    {
        // Arrange: mutable lists used to build settings in code
        var words = new List<string> { "apple" };
        var ignoreWords = new List<string> { "banana" };
        var flagWords = new List<string> { "cherry" };
        var settings = new TextLintSettings { Words = words, IgnoreWords = ignoreWords, FlagWords = flagWords };

        // Act: change the callers' lists, and try to change an exposed list through its mutable interface
        words.Clear();
        ignoreWords.Add("grape");
        flagWords[0] = "melon";
        var exposed = (IList<string>)settings.Words;

        // Assert: the settings keep their values and refuse the change
        Assert.Multiple(
            () => Assert.Equal(["apple"], settings.Words),
            () => Assert.Equal(["banana"], settings.IgnoreWords),
            () => Assert.Equal(["cherry"], settings.FlagWords),
            () => Assert.Throws<NotSupportedException>(() => exposed.Add("lemon")));
    }

    /// <summary>
    ///     Test that spelling values that are out of range are rejected when settings are built in code.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Settings_SpellingValuesOutOfRange_AreRejected()
    {
        // Arrange: no setup required

        // Act / Assert: a zero minimum word length, a negative size, and a missing language are each refused
        Assert.Multiple(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new TextLintSettings { MinWordLength = 0 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new TextLintSettings { MaxFileSize = -1 }),
            () => Assert.Throws<ArgumentNullException>(() => new TextLintSettings { Language = null! }));
    }

    /// <summary>
    ///     Test that an unsupported language is reported with its file, line, and column.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_UnsupportedLanguage_ReportsErrorWithPosition()
    {
        // Arrange: a file whose language has no dictionaries
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "version: 0.2\nlanguage: fr\n");

        // Assert: loading fails with an error at the value that names the language
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(11, diagnostic.Column),
            () => Assert.Contains("'fr' is not supported", diagnostic.Message));
    }

    /// <summary>
    ///     Test that an unusable word-list entry is reported with its file, line, and column.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_UnusableWordEntry_ReportsErrorWithPosition()
    {
        // Arrange: a file whose word list holds an entry with white space
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "words:\n  - apple\n  - \"foo bar\"\n");

        // Assert: loading fails with an error at the entry that names the list, the entry, and the problem
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column),
            () => Assert.StartsWith("Cannot use the 'words' entry 'foo bar': it contains white space", diagnostic.Message));
    }

    /// <summary>
    ///     Test that an invalid minimum word length is reported with its file, line, and column.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_InvalidMinWordLength_ReportsErrorWithPosition()
    {
        // Arrange: a file whose minimum word length is zero
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "minWordLength: 0\n");

        // Assert: loading fails with an error at the value that names the key
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(16, diagnostic.Column),
            () => Assert.Contains("'minWordLength'", diagnostic.Message));
    }

    /// <summary>
    ///     Test that an invalid maximum file size is reported with its file, line, and column.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_InvalidMaxFileSize_ReportsErrorWithPosition()
    {
        // Arrange: a file whose maximum file size has an unknown unit
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "maxFileSize: 5TB\n");

        // Assert: loading fails with an error at the value that names the key
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(14, diagnostic.Column),
            () => Assert.Contains("'maxFileSize'", diagnostic.Message));
    }

    /// <summary>
    ///     Test that several invalid spelling values in one file are all reported.
    /// </summary>
    [Fact]
    public async Task ConfigurationSubsystem_LoadAsync_MultipleSpellingErrors_ReportsAll()
    {
        // Arrange: a file with an unsupported language, an unusable word, and two bad numbers
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadAsync(temp, "language: fr\nflagWords:\n  - \"a b\"\nminWordLength: 0\nmaxFileSize: -1\n");

        // Assert: every error is reported at its own line and no settings are returned
        Assert.Multiple(
            () => Assert.Equal([1, 3, 4, 5], result.Diagnostics.Select(diagnostic => diagnostic.Line)),
            () => Assert.Null(result.Settings));
    }
}
