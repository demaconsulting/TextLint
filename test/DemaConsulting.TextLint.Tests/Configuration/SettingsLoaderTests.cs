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

// cspell:ignore colour kubectl nuget Urlz zorb quix

using System.Text;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Unit tests for the <see cref="SettingsLoader"/> class.
/// </summary>
/// <remarks>
///     The loader is tested with real YamlDotNet parsing (an OTS item, not a test double), real
///     temporary files, and the real glob pattern expander, spelling dictionary selection, and word
///     tokenizer it depends on for validating patterns, languages, and word-list entries.
/// </remarks>
public class SettingsLoaderTests
{
    /// <summary>
    ///     The source name used for diagnostics in text-based tests.
    /// </summary>
    private const string Source = "cfg.yaml";

    /// <summary>
    ///     The message the loader reports for an invalid <c>minWordLength</c> value.
    /// </summary>
    private const string MinWordLengthProblem = "Expected a whole number of at least 1 for 'minWordLength'.";

    /// <summary>
    ///     The message the loader reports for an invalid <c>maxFileSize</c> value.
    /// </summary>
    private const string MaxFileSizeProblem =
        "Expected a size such as 100000, 500K, or 1.5MB (units B, K, KB, M, MB, G, GB) or 0 for no limit, for 'maxFileSize'.";

    /// <summary>
    ///     The list of supported languages that language errors end with.
    /// </summary>
    private const string SupportedLanguages = "Supported languages: en, en-GB, en-US.";

    /// <summary>
    ///     The number of bytes in a kilobyte.
    /// </summary>
    private const long Kilobyte = 1024L;

    /// <summary>
    ///     The number of bytes in a megabyte.
    /// </summary>
    private const long Megabyte = Kilobyte * Kilobyte;

    /// <summary>
    ///     The number of bytes in a gigabyte.
    /// </summary>
    private const long Gigabyte = Megabyte * Kilobyte;

    /// <summary>
    ///     The column at which the value of a <c>minWordLength</c> entry that starts a line begins.
    /// </summary>
    private const int MinWordLengthValueColumn = 16;

    /// <summary>
    ///     The column at which the value of a <c>maxFileSize</c> entry that starts a line begins.
    /// </summary>
    private const int MaxFileSizeValueColumn = 14;

    /// <summary>
    ///     The column at which the value of a <c>language</c> entry that starts a line begins.
    /// </summary>
    private const int LanguageValueColumn = 11;

    /// <summary>
    ///     The column at which an item of a block list indented by two spaces begins.
    /// </summary>
    private const int ListItemColumn = 5;

    /// <summary>
    ///     Parses text that must fail with exactly one diagnostic and returns that diagnostic.
    /// </summary>
    /// <param name="yaml">The settings text that is expected to be in error.</param>
    /// <returns>The single diagnostic.</returns>
    private static SettingsDiagnostic ParseSingleError(string yaml)
    {
        // Parse and require exactly one error and no settings
        var result = SettingsLoader.Parse(yaml, Source);
        Assert.Null(result.Settings);
        return Assert.Single(result.Diagnostics);
    }

    /// <summary>
    ///     Parses a <c>maxFileSize</c> value and returns the size that was mapped.
    /// </summary>
    /// <param name="value">The text written after <c>maxFileSize:</c>.</param>
    /// <returns>The size in bytes, or null when the value was rejected.</returns>
    private static long? ParseSize(string value) => SettingsLoader.Parse($"maxFileSize: {value}\n", Source).Settings?.MaxFileSize;

    /// <summary>
    ///     Test that loading a valid file returns its settings.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_ValidFile_ReturnsSettings()
    {
        // Arrange: write a valid settings file
        using var temp = new TempDirectory();
        var path = temp.CreateFile(".textlint.yaml", "files:\n  - \"**/*.md\"\n");

        // Act: load the file
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: the load succeeded with the file's include pattern
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal(["**/*.md"], result.Settings!.Files));
    }

    /// <summary>
    ///     Test that a missing file is reported as an error naming the file.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_MissingFile_ReturnsErrorNamingFile()
    {
        // Arrange: a path that does not exist
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, "missing.yaml");

        // Act: load the missing file
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: no settings, and one error that names the file
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Equal(path, diagnostic.FilePath),
            () => Assert.Contains("Cannot read settings file", diagnostic.Message));
    }

    /// <summary>
    ///     Test that an already cancelled token cancels the load.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange: a valid file and a token that is already cancelled
        using var temp = new TempDirectory();
        var path = temp.CreateFile(".textlint.yaml", "version: 0.2\n");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the load observes the cancellation
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SettingsLoader.LoadAsync(path, source.Token));
    }

    /// <summary>
    ///     Test that a null path is rejected.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_NullPath_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: a null path throws
        await Assert.ThrowsAsync<ArgumentNullException>(() => SettingsLoader.LoadAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that an empty path is rejected.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_EmptyPath_ThrowsArgumentException()
    {
        // Arrange: no setup required

        // Act / Assert: an empty path throws ArgumentException (not the derived null exception)
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SettingsLoader.LoadAsync(string.Empty, TestContext.Current.CancellationToken));
        Assert.IsNotType<ArgumentNullException>(exception);
    }

    /// <summary>
    ///     Test that a file starting with a UTF-8 byte-order mark loads normally.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_Utf8BomFile_ReturnsSettings()
    {
        // Arrange: write a settings file that begins with a UTF-8 byte-order mark
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, ".textlint.yaml");
        await File.WriteAllTextAsync(path, "version: 0.2\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), TestContext.Current.CancellationToken);

        // Act: load the file
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: the BOM did not disturb parsing
        Assert.Equal("0.2", result.Settings!.Version);
    }

    /// <summary>
    ///     Test that a missing file's diagnostic carries no line or column.
    /// </summary>
    [Fact]
    public async Task SettingsLoader_LoadAsync_MissingFile_DiagnosticHasNoPosition()
    {
        // Arrange: a path that does not exist
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, "missing.yaml");

        // Act: load the missing file
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: the diagnostic has no position, and its text omits the position
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(diagnostic.Line),
            () => Assert.Null(diagnostic.Column),
            () => Assert.StartsWith(path + ": error: ", diagnostic.ToString()));
    }

    /// <summary>
    ///     Test that every supported key maps to its setting.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue()
    {
        // Arrange: text that sets every supported key
        const string yaml =
            "version: \"0.2\"\nfiles:\n  - \"**/*.md\"\n  - src/**/*.cs\nignorePaths:\n  - node_modules\nenableGlobDot: true\n" +
            "language: en-GB\nwords:\n  - kubectl\nignoreWords:\n  - nuget\nflagWords:\n  - colour\nminWordLength: 3\nmaxFileSize: 2K\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: every value is mapped
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("0.2", settings.Version),
            () => Assert.Equal(["**/*.md", "src/**/*.cs"], settings.Files),
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
    ///     Test that an empty document yields default settings.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_EmptyDocument_ReturnsDefaultSettings()
    {
        // Arrange: no setup required

        // Act: parse empty text
        var result = SettingsLoader.Parse(string.Empty, Source);

        // Assert: success with default settings
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Settings!.Files),
            () => Assert.False(result.Settings!.EnableGlobDot));
    }

    /// <summary>
    ///     Test that a document holding only comments yields default settings.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_CommentOnlyDocument_ReturnsDefaultSettings()
    {
        // Arrange: text that is only a comment
        const string yaml = "# nothing configured yet\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: success with default settings
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Null(result.Settings!.Version));
    }

    /// <summary>
    ///     Test that a list key with no value is treated as an empty list.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_NullListValue_TreatedAsAbsent()
    {
        // Arrange: a files key whose list is empty or commented out
        const string yaml = "files:\n# - \"**/*.md\"\nignorePaths: ~\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: success with empty lists
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Settings!.Files),
            () => Assert.Empty(result.Settings!.IgnorePaths));
    }

    /// <summary>
    ///     Test that an unquoted numeric version is carried as text.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_NumericVersion_MapsToText()
    {
        // Arrange: an unquoted version that looks like a number
        const string yaml = "version: 0.2\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the version keeps its original text
        Assert.Equal("0.2", result.Settings!.Version);
    }

    /// <summary>
    ///     Test that flow-style and block-style lists both map.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FlowAndBlockLists_BothMap()
    {
        // Arrange: one list in flow style and one in block style
        const string yaml = "files: [\"**/*.md\", \"**/*.cs\"]\nignorePaths:\n  - bin\n  - obj\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: both lists are mapped in order
        Assert.Multiple(
            () => Assert.Equal(["**/*.md", "**/*.cs"], result.Settings!.Files),
            () => Assert.Equal(["bin", "obj"], result.Settings!.IgnorePaths));
    }

    /// <summary>
    ///     Test that anchors and aliases are resolved.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_AnchorsAndAliases_AreResolved()
    {
        // Arrange: an anchored pattern reused through an alias
        const string yaml = "files:\n  - &docs \"docs/**\"\nignorePaths:\n  - *docs\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the alias resolves to the anchored value
        Assert.Equal(["docs/**"], result.Settings!.IgnorePaths);
    }

    /// <summary>
    ///     Test that a list key holding a scalar is reported at the value.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FilesNotAList_ReportsErrorAtValue()
    {
        // Arrange: files given as a single text value instead of a list
        const string yaml = "version: 0.2\nfiles: \"**/*.md\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the value on line 2
        Assert.Multiple(
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(8, diagnostic.Column),
            () => Assert.Contains("list", diagnostic.Message));
    }

    /// <summary>
    ///     Test that a list item that is not text is reported at the item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FilesItemNotText_ReportsErrorAtItem()
    {
        // Arrange: a list whose second item is a nested mapping
        const string yaml = "files:\n  - \"**/*.md\"\n  - key: value\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the offending item on line 3
        Assert.Multiple(
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a non-boolean enableGlobDot is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_EnableGlobDotNotBoolean_ReportsError()
    {
        // Arrange: enableGlobDot set to ordinary text
        const string yaml = "enableGlobDot: maybe\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error names the key and is at the value
        Assert.Multiple(
            () => Assert.Contains("enableGlobDot", diagnostic.Message),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(16, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a YAML 1.1 boolean spelling is not accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_EnableGlobDotYes_ReportsError()
    {
        // Arrange: enableGlobDot set to the YAML 1.1 spelling yes
        const string yaml = "enableGlobDot: yes\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the value is rejected rather than guessed at
        Assert.Contains("true or false", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a version that is not text is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_VersionNotText_ReportsError()
    {
        // Arrange: version given as a list
        const string yaml = "version:\n  - 0.2\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error names the key
        Assert.Contains("version", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a document whose root is not a mapping is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_RootNotMapping_ReportsError()
    {
        // Arrange: a document holding a list at the top level
        const string yaml = "- a\n- b\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the start of the document
        Assert.Multiple(
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(1, diagnostic.Column),
            () => Assert.Contains("mapping", diagnostic.Message));
    }

    /// <summary>
    ///     Test that a key that is not plain text is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_NonScalarKey_ReportsError()
    {
        // Arrange: a mapping whose key is itself a list
        const string yaml = "? [a, b]\n: value\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the key
        Assert.Equal(1, diagnostic.Line);
    }

    /// <summary>
    ///     Test that more than one YAML document is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MultipleDocuments_ReportsError()
    {
        // Arrange: two documents separated by a document marker
        const string yaml = "version: 0.2\n---\nfiles: []\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the second document and says so
        Assert.Multiple(
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Contains("Multiple YAML documents", diagnostic.Message));
    }

    /// <summary>
    ///     Test that every type error is reported, not just the first.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MultipleTypeErrors_ReportsEveryError()
    {
        // Arrange: three separate type errors
        const string yaml = "files: oops\nignorePaths: oops\nenableGlobDot: oops\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: three diagnostics, in source order
        Assert.Equal([1, 2, 3], result.Diagnostics.Select(diagnostic => diagnostic.Line));
    }

    /// <summary>
    ///     Test that any error discards the settings.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_AnyError_ReturnsNoSettings()
    {
        // Arrange: valid values alongside one error
        const string yaml = "version: 0.2\nenableGlobDot: oops\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: no settings are returned
        Assert.Multiple(
            () => Assert.False(result.Succeeded),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that malformed YAML is reported with its line and column.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MalformedYaml_ReportsLineAndColumn()
    {
        // Arrange: a flow list that is never closed
        const string yaml = "version: 0.2\nfiles: [\"a\", \n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error has a position inside the text
        Assert.Multiple(
            () => Assert.NotNull(diagnostic.Line),
            () => Assert.True(diagnostic.Line >= 2),
            () => Assert.NotNull(diagnostic.Column));
    }

    /// <summary>
    ///     Test that a duplicate key is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_DuplicateKey_ReportsErrorAtDuplicate()
    {
        // Arrange: the files key given twice
        const string yaml = "files: []\nfiles: []\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the second occurrence
        Assert.Equal(2, diagnostic.Line);
    }

    /// <summary>
    ///     Test that tab indentation is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_TabIndentation_ReportsError()
    {
        // Arrange: a list indented with a tab character
        const string yaml = "files:\n\t- a\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error has a position
        Assert.NotNull(diagnostic.Line);
    }

    /// <summary>
    ///     Test that an alias with no matching anchor is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_UnknownAlias_ReportsError()
    {
        // Arrange: an alias that refers to an anchor that does not exist
        const string yaml = "files:\n  - *missing\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error has a position
        Assert.NotNull(diagnostic.Line);
    }

    /// <summary>
    ///     Test that an unsupported key is an error that names the key and its position.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_UnsupportedKey_ReportsErrorNamingKey()
    {
        // Arrange: a key that TextLint does not support, after a supported one
        const string yaml = "version: 0.2\ncaseSensitive: true\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error names the key and is at the key
        Assert.Multiple(
            () => Assert.Equal("'caseSensitive' is not a supported setting.", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(1, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an unsupported key prevents settings from being returned.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_UnsupportedKey_ReturnsNoSettings()
    {
        // Arrange: an otherwise valid file with one unsupported key
        const string yaml = "files:\n  - \"**/*.md\"\nallowCompoundWords: true\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading fails and no settings are returned
        Assert.Multiple(
            () => Assert.False(result.Succeeded),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that each of several unsupported keys is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_SeveralUnsupportedKeys_ReportsEach()
    {
        // Arrange: three unsupported keys
        const string yaml = "caseSensitive: true\nimport: []\nallowCompoundWords: true\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: one error per key, in source order
        Assert.Equal(
            ["'caseSensitive' is not a supported setting.", "'import' is not a supported setting.", "'allowCompoundWords' is not a supported setting."],
            result.Diagnostics.Select(diagnostic => diagnostic.Message));
    }

    /// <summary>
    ///     Test that an unsupported key with a nested value is reported once.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_UnsupportedKeyWithNestedValue_ReportsSingleError()
    {
        // Arrange: an unsupported key whose value is a nested mapping
        const string yaml = "overrides:\n  - filename: a\n    words: [b]\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: only the key is reported, not its nested content
        Assert.Contains("'overrides'", diagnostic.Message);
    }

    /// <summary>
    ///     Test that key matching is case-sensitive.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_KeyDifferentCase_TreatedAsUnsupported()
    {
        // Arrange: a supported key written with different case
        const string yaml = "Files:\n  - a\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the key is not recognized
        Assert.Equal("'Files' is not a supported setting.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that an invalid include pattern is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_InvalidFilesPattern_ReportsErrorAtItem()
    {
        // Arrange: an include list whose second pattern uses a backslash
        const string yaml = "files:\n  - \"**/*.md\"\n  - 'docs\\*.md'\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the second item
        Assert.Multiple(
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an invalid ignore pattern is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_InvalidIgnorePathsPattern_ReportsErrorAtItem()
    {
        // Arrange: an ignore list holding a parent-escaping pattern
        const string yaml = "ignorePaths:\n  - ../outside/**\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the item
        Assert.Equal(2, diagnostic.Line);
    }

    /// <summary>
    ///     Test that an absolute path pattern is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_AbsolutePathPattern_ReportsErrorAtItem()
    {
        // Arrange: an include list whose second item is a drive path and whose ignore list holds a network path
        const string yaml = "files:\n  - \"**/*.md\"\n  - \"C:/docs/**\"\nignorePaths:\n  - \"//server/share/**\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: both items are reported as absolute paths at their own positions
        Assert.Multiple(
            () => Assert.Equal([3, 5], result.Diagnostics.Select(diagnostic => diagnostic.Line)),
            () => Assert.All(result.Diagnostics, diagnostic => Assert.Contains("absolute paths", diagnostic.Message)),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that a question-mark pattern is reported with a message that suggests the star.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_QuestionMarkPattern_ReportsErrorSuggestingStar()
    {
        // Arrange: an include list using the unsupported ? wildcard
        const string yaml = "files:\n  - \"docs/file?.md\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the item, names the pattern, and suggests the supported wildcard
        Assert.Multiple(
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Contains("'docs/file?.md'", diagnostic.Message),
            () => Assert.Contains("use '*'", diagnostic.Message));
    }

    /// <summary>
    ///     Test that a pattern with a single leading slash, which anchors at the root, is accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_LeadingSlashPattern_IsAccepted()
    {
        // Arrange: patterns written with a leading slash
        const string yaml = "files:\n  - /README.md\nignorePaths:\n  - /build/\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading succeeds and the patterns are kept as written
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["/README.md"], result.Settings!.Files),
            () => Assert.Equal(["/build/"], result.Settings!.IgnorePaths));
    }

    /// <summary>
    ///     Test that an invalid pattern's error names the pattern and the reason.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_InvalidPattern_NamesPatternAndReason()
    {
        // Arrange: a negated pattern
        const string yaml = "files:\n  - \"!**/*.md\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the pattern and why it is rejected
        Assert.Multiple(
            () => Assert.Contains("!**/*.md", diagnostic.Message),
            () => Assert.Contains("negated", diagnostic.Message));
    }

    /// <summary>
    ///     Test that brace and bare-name patterns are accepted at load time.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_ValidBraceAndBareNamePatterns_AreAccepted()
    {
        // Arrange: patterns using braces and a bare name
        const string yaml = "files:\n  - \"**/*.{md,cs}\"\nignorePaths:\n  - node_modules\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading succeeds and the patterns are kept as written
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["**/*.{md,cs}"], result.Settings!.Files),
            () => Assert.Equal(["node_modules"], result.Settings!.IgnorePaths));
    }

    /// <summary>
    ///     Test that a diagnostic carries the source name, line, and column.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_ErrorDiagnostic_CarriesSourceNameLineAndColumn()
    {
        // Arrange: text with one error on line 2
        const string yaml = "version: 0.2\nenableGlobDot: oops\n";

        // Act: parse the text with a source name
        var diagnostic = ParseSingleError(yaml);

        // Assert: the diagnostic names the source and has a 1-based position
        Assert.Multiple(
            () => Assert.Equal(Source, diagnostic.FilePath),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(16, diagnostic.Column));
    }

    /// <summary>
    ///     Test that the diagnostics of a result cannot be changed through their collection interface.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Result_DiagnosticsCannotBeMutatedThroughInterface()
    {
        // Arrange: a result that holds one error
        var result = SettingsLoader.Parse("enableGlobDot: oops\n", Source);
        var exposed = (IList<SettingsDiagnostic>)result.Diagnostics;

        // Act / Assert: adding or clearing through the mutable interface is refused and the error remains
        Assert.Multiple(
            () => Assert.Throws<NotSupportedException>(() => exposed.Clear()),
            () => Assert.Throws<NotSupportedException>(() => exposed.Add(exposed[0])),
            () => Assert.Single(result.Diagnostics),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that changing the caller's list after a result is created does not break the result.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Result_CallerListMutatedAfterCreation_DoesNotAffectResult()
    {
        // Arrange: a successful result created from an empty diagnostics list
        var diagnostics = new List<SettingsDiagnostic>();
        var result = new SettingsLoadResult(new TextLintSettings(), diagnostics);

        // Act: the caller adds a diagnostic to its own list afterwards
        diagnostics.Add(new SettingsDiagnostic("late", null, null, null));

        // Assert: the result is unchanged, so settings are still present with no diagnostics
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.NotNull(result.Settings),
            () => Assert.Empty(result.Diagnostics));
    }

    /// <summary>
    ///     Test that a diagnostic is formatted in the compiler style.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Diagnostic_ToString_FormatsFileLineColumn()
    {
        // Arrange: text with one error on line 2, column 16
        const string yaml = "version: 0.2\nenableGlobDot: oops\n";
        var diagnostic = ParseSingleError(yaml);

        // Act: format the diagnostic
        var text = diagnostic.ToString();

        // Assert: the text is file(line,column): error: message
        Assert.Equal("cfg.yaml(2,16): error: Expected true or false for 'enableGlobDot'.", text);
    }

    /// <summary>
    ///     Test that a diagnostic with no source name omits the file name.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Diagnostic_ToStringWithoutSource_OmitsFileName()
    {
        // Arrange: parse with no source name
        var result = SettingsLoader.Parse("- a\n", null);
        var diagnostic = Assert.Single(result.Diagnostics);

        // Act: format the diagnostic
        var text = diagnostic.ToString();

        // Assert: only the position, the severity word, and the message appear
        Assert.StartsWith("(1,1): error: ", text);
    }

    /// <summary>
    ///     Test that a diagnostic with neither source name nor position shows only the message.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Diagnostic_ToStringWithNothingKnown_ShowsOnlyMessage()
    {
        // Arrange: a diagnostic that has no file and no position
        var diagnostic = new SettingsDiagnostic("something failed", null, null, null);

        // Act: format the diagnostic
        var text = diagnostic.ToString();

        // Assert: only the severity word and the message appear
        Assert.Equal("error: something failed", text);
    }

    /// <summary>
    ///     Test that every spelling key maps to its setting.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_SpellingKeys_MapsEveryValue()
    {
        // Arrange: text that sets the six spelling keys, with more than one entry in each list
        const string yaml =
            "language: en-GB\nwords: [kubectl, k8s]\nignoreWords: [nuget, apple]\nflagWords: [colour, banana]\n" +
            "minWordLength: 7\nmaxFileSize: 1MB\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: every value is mapped, in order
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("en-GB", settings.Language),
            () => Assert.Equal(["kubectl", "k8s"], settings.Words),
            () => Assert.Equal(["nuget", "apple"], settings.IgnoreWords),
            () => Assert.Equal(["colour", "banana"], settings.FlagWords),
            () => Assert.Equal(7, settings.MinWordLength),
            () => Assert.Equal(Megabyte, settings.MaxFileSize));
    }

    /// <summary>
    ///     Test that absent spelling keys leave the cspell defaults in force.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_SpellingKeysAbsent_UsesCspellDefaults()
    {
        // Arrange: text that sets no spelling key
        const string yaml = "version: 0.2\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: American English, a minimum length of 4, empty lists, and no size limit
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
    ///     Test that a spelling key with a null value is treated as absent.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_NullSpellingValues_TreatedAsAbsent()
    {
        // Arrange: every spelling key given an empty, tilde, or null value
        const string yaml = "language: ~\nwords:\nignoreWords: ~\nflagWords: null\nminWordLength: ~\nmaxFileSize:\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading succeeds and the defaults are in force
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal("en", settings.Language),
            () => Assert.Equal(4, settings.MinWordLength),
            () => Assert.Equal(0L, settings.MaxFileSize),
            () => Assert.Empty(settings.Words),
            () => Assert.Empty(settings.IgnoreWords),
            () => Assert.Empty(settings.FlagWords));
    }

    /// <summary>
    ///     Test that word lists written in flow, block, and quoted styles all map.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordListFlowBlockAndQuotedForms_AllMap()
    {
        // Arrange: a flow list with plain, double-quoted, and single-quoted entries, and a block list
        const string yaml = "words: [apple, \"banana\", 'cherry']\nignoreWords:\n  - grape\n  - \"melon\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the entries are mapped in order without their quotes
        Assert.Multiple(
            () => Assert.Equal(["apple", "banana", "cherry"], result.Settings!.Words),
            () => Assert.Equal(["grape", "melon"], result.Settings!.IgnoreWords));
    }

    /// <summary>
    ///     Test that word entries with digits, punctuation, and capitals are kept as written.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordEntriesWithDigitsAndPunctuation_AreKeptAsWritten()
    {
        // Arrange: entries holding a digit, a hyphen and a capital, and an apostrophe
        const string yaml = "words: [\"k8s\", \"Foo-Bar\", \"don't\"]\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the entries are stored exactly as written
        Assert.Equal(["k8s", "Foo-Bar", "don't"], result.Settings!.Words);
    }

    /// <summary>
    ///     Test that the suggestion forms of a flagged word are accepted and kept as written.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FlagWordsSuggestionForms_AreAccepted()
    {
        // Arrange: a flagged word with the arrow form and one with the colon form
        const string yaml = "flagWords:\n  - \"utilize->use, employ\"\n  - \"leverage: use\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading succeeds and both entries are kept as written
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["utilize->use, employ", "leverage: use"], result.Settings!.FlagWords));
    }

    /// <summary>
    ///     Test that a language is accepted in any letter case and as a list, and is kept as written.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_LanguageCaseAndListForms_AreAccepted()
    {
        // Arrange: a language list with odd letter case and spaces around the names
        const string yaml = "language: \"EN-gb , en-US\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: loading succeeds and the text is stored as written
        Assert.Equal("EN-gb , en-US", result.Settings!.Language);
    }

    /// <summary>
    ///     Test that a plain whole number of bytes is mapped.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeWholeNumber_MapsToBytes()
    {
        // Arrange: a size without a unit
        const long bytes = 100000L;

        // Act: parse the text
        var size = ParseSize(bytes.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Assert: the number is the number of bytes
        Assert.Equal(bytes, size);
    }

    /// <summary>
    ///     Test that every size unit maps to its binary multiple, in any letter case.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeUnits_MapToBinaryMultiples()
    {
        // Arrange: the expected size for each spelling of each unit
        var expected = new Dictionary<string, long?>
        {
            ["7B"] = 7L,
            ["7b"] = 7L,
            ["3K"] = 3 * Kilobyte,
            ["3k"] = 3 * Kilobyte,
            ["3KB"] = 3 * Kilobyte,
            ["3kb"] = 3 * Kilobyte,
            ["2M"] = 2 * Megabyte,
            ["2MB"] = 2 * Megabyte,
            ["2mB"] = 2 * Megabyte,
            ["1G"] = Gigabyte,
            ["1GB"] = Gigabyte,
            ["1gb"] = Gigabyte,
        };

        // Act: parse a size for every spelling
        var actual = expected.Keys.ToDictionary(text => text, ParseSize);

        // Assert: each size is the number times the binary multiple of its unit
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Test that a size with a fraction is mapped.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeFractions_MapToBytes()
    {
        // Arrange: the expected size for fractions with and without a leading digit
        var expected = new Dictionary<string, long?>
        {
            ["1.5MB"] = Megabyte + (Megabyte / 2),
            [".5K"] = Kilobyte / 2,
            ["0.5M"] = Megabyte / 2,
            ["2.50K"] = (2 * Kilobyte) + (Kilobyte / 2),
        };

        // Act: parse a size for every spelling
        var actual = expected.Keys.ToDictionary(text => text, ParseSize);

        // Assert: the fraction of the unit is converted exactly
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Test that a fractional number of bytes is rounded down.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeFractionalBytes_RoundsDown()
    {
        // Arrange: the expected size for sizes that are not a whole number of bytes
        var expected = new Dictionary<string, long?>
        {
            ["1.9"] = 1L,
            ["2.7B"] = 2L,
            ["1.9999K"] = (2 * Kilobyte) - 1,
        };

        // Act: parse a size for every spelling
        var actual = expected.Keys.ToDictionary(text => text, ParseSize);

        // Assert: each size is the number of whole bytes
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Test that zero, with or without a unit or fraction, means no limit.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeZero_MeansNoLimit()
    {
        // Arrange: the expected size for several spellings of zero
        var expected = new Dictionary<string, long?>
        {
            ["0"] = 0L,
            ["0K"] = 0L,
            ["0.0MB"] = 0L,
        };

        // Act: parse a size for every spelling
        var actual = expected.Keys.ToDictionary(text => text, ParseSize);

        // Assert: each is the no-limit value
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Test that a quoted size is accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeQuotedText_IsAccepted()
    {
        // Arrange: sizes written in double and single quotes
        var expected = new Dictionary<string, long?>
        {
            ["\"500K\""] = 500 * Kilobyte,
            ["'1MB'"] = Megabyte,
        };

        // Act: parse a size for every spelling
        var actual = expected.Keys.ToDictionary(text => text, ParseSize);

        // Assert: the quotes do not matter
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Test that the largest representable size is accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeLargestRepresentable_IsAccepted()
    {
        // Arrange: the largest whole number of gigabytes whose size fits in a 64-bit integer
        const long wholeGigabytes = 8589934591L;

        // Act: parse the size
        var size = ParseSize($"{wholeGigabytes}GB");

        // Assert: the size is mapped exactly
        Assert.Equal(wholeGigabytes * Gigabyte, size);
    }

    /// <summary>
    ///     Test that the smallest valid minimum word length is accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthOne_IsAccepted()
    {
        // Arrange: the boundary length of 1
        const string yaml = "minWordLength: 1\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the length is mapped
        Assert.Equal(1, result.Settings!.MinWordLength);
    }

    /// <summary>
    ///     Test that an unsupported language is reported at its value, naming it and the supported languages.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_UnsupportedLanguage_ReportsErrorAtValue()
    {
        // Arrange: a language that has no dictionaries
        const string yaml = "language: fr\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the language and the supported ones, without a parameter name, at the value
        Assert.Multiple(
            () => Assert.Equal($"The language 'fr' is not supported. {SupportedLanguages}", diagnostic.Message),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(LanguageValueColumn, diagnostic.Column),
            () => Assert.Equal(Source, diagnostic.FilePath));
    }

    /// <summary>
    ///     Test that an unsupported name in a list of languages is the one that is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_LanguageListWithUnsupportedName_NamesTheUnsupportedName()
    {
        // Arrange: a language list whose second name is unsupported
        const string yaml = "language: \"en-US, en-AU\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: only the unsupported name is reported
        Assert.Equal($"The language 'en-AU' is not supported. {SupportedLanguages}", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a language list with an empty name is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_LanguageListWithEmptyName_ReportsError()
    {
        // Arrange: a language list that ends with a separator
        const string yaml = "language: \"en-US,\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the list and the supported languages
        Assert.Equal($"The language list 'en-US,' contains an empty language name. {SupportedLanguages}", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a quoted empty language is reported rather than treated as absent.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_EmptyQuotedLanguage_ReportsError()
    {
        // Arrange: a language that is an empty quoted string, which is text and not null
        const string yaml = "language: \"\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message says the list has an empty name
        Assert.Equal($"The language list '' contains an empty language name. {SupportedLanguages}", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a language that is not text is reported at its value.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_LanguageNotText_ReportsErrorAtValue()
    {
        // Arrange: a language given as a list
        const string yaml = "language:\n  - en\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error names the key and is at the list
        Assert.Multiple(
            () => Assert.Equal("Expected a text value for 'language'.", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(3, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a blank word entry is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsBlankEntry_ReportsErrorAtItem()
    {
        // Arrange: a list holding an entry of white space only
        const string yaml = "words:\n  - \"  \"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the key, the entry, and the problem, at the item
        Assert.Multiple(
            () => Assert.Equal("Cannot use the 'words' entry '  ': it is blank.", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column),
            () => Assert.Equal(Source, diagnostic.FilePath));
    }

    /// <summary>
    ///     Test that a word entry containing white space is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsEntryWithWhiteSpace_ReportsError()
    {
        // Arrange: an entry holding two words
        const string yaml = "words:\n  - \"foo bar\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message says the entry can never match and to list each word separately
        Assert.Equal(
            "Cannot use the 'words' entry 'foo bar': it contains white space, so it can never match a word; list each word separately.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that a word entry containing a character that cannot be part of a word is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsEntryWithDisallowedCharacter_ReportsError()
    {
        // Arrange: an entry holding an at sign
        const string yaml = "words:\n  - \"foo@bar\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the character
        Assert.Equal(
            "Cannot use the 'words' entry 'foo@bar': it contains the character '@', which cannot be part of a word, so it can never match.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that the forbidden-word marker is reported with a pointer to the flagged-word list.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsForbiddenMarkerEntry_ReportsError()
    {
        // Arrange: an entry written in cspell's forbidden-word form
        const string yaml = "words:\n  - \"!word\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message explains that the marker is unsupported and what to use instead
        Assert.Equal(
            "Cannot use the 'words' entry '!word': the forbidden-word marker '!' is not supported; list the word in flagWords instead.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that a word entry without a letter or digit is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsEntryWithoutLetterOrDigit_ReportsError()
    {
        // Arrange: an entry made only of dots
        const string yaml = "words:\n  - \"...\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message says the entry has no letter or digit
        Assert.Equal(
            "Cannot use the 'words' entry '...': it has no letter or digit, so it can never match.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that the suggestion form is not accepted in the accepted-word list.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsSuggestionForm_ReportsError()
    {
        // Arrange: an entry in the colon suggestion form, which only flagged words allow
        const string yaml = "words:\n  - \"utilize: use\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the colon is reported as a character that cannot be part of a word
        Assert.Equal(
            "Cannot use the 'words' entry 'utilize: use': it contains the character ':', which cannot be part of a word, so it can never match.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that a bad ignored-word entry is reported under its own key name.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreWordsBadEntry_NamesIgnoreWordsKey()
    {
        // Arrange: an ignored-word entry holding two words
        const string yaml = "ignoreWords:\n  - \"foo bar\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the ignoreWords key
        Assert.StartsWith("Cannot use the 'ignoreWords' entry 'foo bar': ", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a bad flagged-word entry is reported under its own key name.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FlagWordsBadEntry_NamesFlagWordsKey()
    {
        // Arrange: a flagged-word entry holding two words before its suggestion
        const string yaml = "flagWords:\n  - \"foo bar: baz\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the flagWords key
        Assert.StartsWith("Cannot use the 'flagWords' entry 'foo bar: baz': ", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a flagged-word entry that has suggestions but no word is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_FlagWordsEntryWithoutWord_ReportsError()
    {
        // Arrange: an entry that starts with the arrow
        const string yaml = "flagWords:\n  - \"->color\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message says there is no word before the suggestions
        Assert.Equal(
            "Cannot use the 'flagWords' entry '->color': there is no word before its suggestions.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that only the unusable entries of a word list are reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordListWithGoodAndBadEntries_ReportsOnlyTheBadEntry()
    {
        // Arrange: a list whose middle entry is unusable
        const string yaml = "words:\n  - apple\n  - \"foo bar\"\n  - banana\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the one error is at the middle item
        Assert.Multiple(
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a word list that is not a list is reported at its value.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsNotAList_ReportsErrorAtValue()
    {
        // Arrange: words given as a single text value
        const string yaml = "words: apple\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the value
        Assert.Multiple(
            () => Assert.Equal("Expected a list of text values.", diagnostic.Message),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(8, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a word list item that is not text is reported at the item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsItemNotText_ReportsErrorAtItem()
    {
        // Arrange: a flagged-word list whose item is a nested mapping
        const string yaml = "flagWords:\n  - key: value\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the item
        Assert.Multiple(
            () => Assert.Equal("Expected a text value in the list.", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a null word list item is reported at the item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_WordsNullItem_ReportsErrorAtItem()
    {
        // Arrange: a list holding a null entry
        const string yaml = "words:\n  - ~\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error is at the item
        Assert.Multiple(
            () => Assert.Equal("Expected a text value in the list.", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line));
    }

    /// <summary>
    ///     Test that a minimum word length of zero is reported at its value.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthZero_ReportsErrorAtValue()
    {
        // Arrange: a length below the minimum of 1
        const string yaml = "minWordLength: 0\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message, file, line, and column are all reported, and they format as a compiler diagnostic
        Assert.Multiple(
            () => Assert.Equal(MinWordLengthProblem, diagnostic.Message),
            () => Assert.Equal(Source, diagnostic.FilePath),
            () => Assert.Equal(1, diagnostic.Line),
            () => Assert.Equal(MinWordLengthValueColumn, diagnostic.Column),
            () => Assert.Equal($"cfg.yaml(1,16): error: {MinWordLengthProblem}", diagnostic.ToString()));
    }

    /// <summary>
    ///     Test that a negative minimum word length is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthNegative_ReportsError()
    {
        // Arrange: a negative length
        const string yaml = "minWordLength: -1\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the length is rejected
        Assert.Equal(MinWordLengthProblem, diagnostic.Message);
    }

    /// <summary>
    ///     Test that a minimum word length with a fraction is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthFraction_ReportsError()
    {
        // Arrange: a length with a fraction
        const string yaml = "minWordLength: 2.5\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the length is rejected
        Assert.Equal(MinWordLengthProblem, diagnostic.Message);
    }

    /// <summary>
    ///     Test that a quoted minimum word length is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthQuoted_ReportsError()
    {
        // Arrange: a number written in quotes
        const string yaml = "minWordLength: \"4\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the length is rejected rather than guessed at
        Assert.Equal(MinWordLengthProblem, diagnostic.Message);
    }

    /// <summary>
    ///     Test that a minimum word length too large for an integer is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthOverflow_ReportsError()
    {
        // Arrange: a number larger than any 32-bit integer
        const string yaml = "minWordLength: 99999999999\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the length is rejected
        Assert.Equal(MinWordLengthProblem, diagnostic.Message);
    }

    /// <summary>
    ///     Test that a minimum word length that is not a number is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MinWordLengthNotANumber_ReportsError()
    {
        // Arrange: ordinary text
        const string yaml = "minWordLength: long\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the length is rejected
        Assert.Equal(MinWordLengthProblem, diagnostic.Message);
    }

    /// <summary>
    ///     Test that malformed sizes are each reported at their value with the expected forms.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeInvalidForms_ReportEachAtValue()
    {
        // Arrange: a negative size, an unknown unit, a unit without a number, text, an exponent, two dots,
        // a space before the unit, and an empty quoted value
        string[] invalid = ["-5", "5TB", "KB", "big", "1e3", "1.2.3", "5 K", "\"\""];

        // Act: parse each size
        var results = invalid.Select(value => SettingsLoader.Parse($"maxFileSize: {value}\n", Source)).ToList();

        // Assert: each is rejected with the size message at the value
        Assert.Multiple(
            () => Assert.All(results, result => Assert.Null(result.Settings)),
            () => Assert.All(results, result => Assert.Equal([MaxFileSizeProblem], result.Diagnostics.Select(d => d.Message))),
            () => Assert.All(results, result => Assert.Equal(MaxFileSizeValueColumn, Assert.Single(result.Diagnostics).Column)),
            () => Assert.All(results, result => Assert.Equal(1, Assert.Single(result.Diagnostics).Line)));
    }

    /// <summary>
    ///     Test that a size below one byte is reported, because it would silently mean no limit.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeBelowOneByte_ReportsError()
    {
        // Arrange: a positive size that rounds down to zero bytes, with and without a unit
        string[] tooSmall = ["0.5", "0.0004K"];

        // Act: parse each size
        var results = tooSmall.Select(value => SettingsLoader.Parse($"maxFileSize: {value}\n", Source)).ToList();

        // Assert: each is rejected with the size message
        Assert.All(results, result => Assert.Equal([MaxFileSizeProblem], result.Diagnostics.Select(d => d.Message)));
    }

    /// <summary>
    ///     Test that sizes that do not fit in a 64-bit integer are reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeOverflow_ReportsError()
    {
        // Arrange: a huge number, the largest 64-bit integer, and the first size above the largest gigabyte count
        string[] tooLarge = ["99999999999999999999", "9223372036854775807", "8589934592GB"];

        // Act: parse each size
        var results = tooLarge.Select(value => SettingsLoader.Parse($"maxFileSize: {value}\n", Source)).ToList();

        // Assert: each is rejected with the size message
        Assert.All(results, result => Assert.Equal([MaxFileSizeProblem], result.Diagnostics.Select(d => d.Message)));
    }

    /// <summary>
    ///     Test that a size given as a list is reported at its value.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MaxFileSizeNotText_ReportsErrorAtValue()
    {
        // Arrange: a size given as a list
        const string yaml = "maxFileSize:\n  - 1\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the size message is reported at the list
        Assert.Multiple(
            () => Assert.Equal(MaxFileSizeProblem, diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(3, diagnostic.Column));
    }

    /// <summary>
    ///     Test that every invalid spelling value is reported, each at its own position.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_MultipleSpellingErrors_ReportsEveryError()
    {
        // Arrange: an unsupported language, an unusable word, a bad length, and a bad size
        const string yaml = "language: fr\nwords:\n  - \"a b\"\nminWordLength: 0\nmaxFileSize: -1\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: four errors in source order and no settings
        Assert.Multiple(
            () => Assert.Equal([1, 3, 4, 5], result.Diagnostics.Select(diagnostic => diagnostic.Line)),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that the entries of <c>ignoreRegExpList</c> are mapped in order, whatever their form.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpList_MapsEntriesInOrder()
    {
        // Arrange: a slash expression, a bare expression, a predefined name, and a name defined below
        const string yaml =
            "ignoreRegExpList:\n  - \"/zorb\\\\d+/i\"\n  - \"zorb\\\\w*\"\n  - Urls\n  - ZorbName\n" +
            "patterns:\n  - name: ZorbName\n    pattern: \"/quix/\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the entries are kept as written, in order
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["/zorb\\d+/i", "zorb\\w*", "Urls", "ZorbName"], result.Settings!.IgnoreRegExpList));
    }

    /// <summary>
    ///     Test that the named patterns are mapped with their names and expressions, and that a description is accepted.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_Patterns_MapsNamesAndExpressions()
    {
        // Arrange: two named patterns, one with a description
        const string yaml =
            "patterns:\n  - name: First\n    pattern: \"/zorb/\"\n  - pattern: \"quix\"\n    name: Second\n    description: Documentation only\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: both definitions are kept in order, whatever the order of their keys
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Equal(["First", "Second"], result.Settings!.Patterns.Select(pattern => pattern.Name)),
            () => Assert.Equal(["/zorb/", "quix"], result.Settings!.Patterns.Select(pattern => pattern.Pattern)));
    }

    /// <summary>
    ///     Test that an <c>ignoreRegExpList</c> entry with an unsupported flag is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpListUnsupportedFlag_ReportsErrorAtItem()
    {
        // Arrange: an expression with the extended flag, which has no safe equivalent and which cspell silently drops
        const string yaml = "ignoreRegExpList:\n  - \"/zorb/x\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message names the setting, the entry, and the flag, at the item
        Assert.Multiple(
            () => Assert.Contains("Cannot use the 'ignoreRegExpList' entry '/zorb/x'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("the flag 'x' is not supported", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an invalid regular expression in <c>ignoreRegExpList</c> is reported at its list item, and the rest is kept.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpListInvalidExpression_ReportsEveryBadItem()
    {
        // Arrange: a good entry between two bad ones
        const string yaml = "ignoreRegExpList:\n  - \"/(/\"\n  - \"/zorb/\"\n  - \"/\\\\z/\"\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the two bad items are reported at their own lines, and there are no settings
        Assert.Multiple(
            () => Assert.Equal([2, 4], result.Diagnostics.Select(diagnostic => diagnostic.Line)),
            () => Assert.Contains("a '(' is not closed", result.Diagnostics[0].Message, StringComparison.Ordinal),
            () => Assert.Contains("the escape '\\z' is not supported", result.Diagnostics[1].Message, StringComparison.Ordinal),
            () => Assert.Null(result.Settings));
    }

    /// <summary>
    ///     Test that a name that nothing defines is reported at its list item.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpListUnknownName_ReportsErrorAtItem()
    {
        // Arrange: a misspelled predefined name
        const string yaml = "ignoreRegExpList:\n  - Urlz\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message says the name is not defined and how to write literal text, at the item
        Assert.Multiple(
            () => Assert.Contains("Cannot use the 'ignoreRegExpList' entry 'Urlz'", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Contains("not a defined or predefined pattern name", diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(ListItemColumn, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a blank entry of <c>ignoreRegExpList</c> is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpListBlankEntry_ReportsError()
    {
        // Arrange: an entry of two quoted spaces
        const string yaml = "ignoreRegExpList:\n  - \"  \"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the entry is reported as blank
        Assert.Contains("the pattern is blank", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value of <c>ignoreRegExpList</c> that is not a list of text is reported.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IgnoreRegExpListNotAListOfText_ReportsErrors()
    {
        // Arrange: a text instead of a list, and a list with a mapping in it
        const string scalarYaml = "ignoreRegExpList: Urls\n";
        const string mappingYaml = "ignoreRegExpList:\n  - a: b\n";

        // Act: parse both
        var scalar = ParseSingleError(scalarYaml);
        var mapping = ParseSingleError(mappingYaml);

        // Assert: each is reported by the shared list rules
        Assert.Multiple(
            () => Assert.Equal("Expected a list of text values.", scalar.Message),
            () => Assert.Equal("Expected a text value in the list.", mapping.Message));
    }

    /// <summary>
    ///     Test that an invalid definition of <c>patterns</c> is reported at the node that holds the problem.
    /// </summary>
    /// <param name="yaml">A settings text with one invalid definition.</param>
    /// <param name="line">The line of the expected diagnostic.</param>
    /// <param name="column">The column of the expected diagnostic.</param>
    /// <param name="message">Text that the message must contain.</param>
    [Theory]
    [InlineData("patterns:\n  - name: Urls\n    pattern: \"/zorb/\"\n", 2, 11, "predefined")]
    [InlineData("patterns:\n  - name: 1zorb\n    pattern: \"/zorb/\"\n", 2, 11, "must start with a letter")]
    [InlineData("patterns:\n  - name: \"two words\"\n    pattern: \"/zorb/\"\n", 2, 11, "must start with a letter")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: \"/zorb/\"\n  - name: ZORB\n    pattern: \"/quix/\"\n", 4, 11, "already defined")]
    [InlineData("patterns:\n  - pattern: \"/zorb/\"\n", 2, 5, "A pattern needs a 'name'")]
    [InlineData("patterns:\n  - name: Zorb\n", 2, 5, "A pattern needs a 'pattern'")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: \"/(/\"\n", 3, 14, "Cannot use the pattern 'Zorb' ('/(/')")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: \"/zorb/x\"\n", 3, 14, "the flag 'x' is not supported")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern:\n      - Urls\n      - Email\n", 4, 7, "A list of patterns is not supported")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: [a, b]\n", 3, 14, "A list of patterns is not supported")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: {a: b}\n", 3, 14, "Expected a text value for 'pattern'")]
    [InlineData("patterns:\n  - name: [a]\n    pattern: \"/zorb/\"\n", 2, 11, "Expected a text value for 'name'")]
    [InlineData("patterns:\n  - name: Zorb\n    pattern: \"/zorb/\"\n    flags: i\n", 4, 5, "'flags' is not a supported key of a pattern")]
    [InlineData("patterns:\n  - Zorb\n", 2, 5, "Expected a pattern with a 'name' and a 'pattern'")]
    [InlineData("patterns: Zorb\n", 1, 11, "Expected a list of patterns")]
    [InlineData("patterns:\n  - name:\n    pattern: \"/zorb/\"\n", 2, 10, "Expected a text value for 'name'")]
    public void SettingsLoader_Parse_InvalidPatternDefinition_ReportsErrorAtTheProblem(string yaml, int line, int column, string message)
    {
        // Arrange: the settings text and the expected position and wording are passed in by the theory data
        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the problem is reported at its node, with the expected wording
        Assert.Multiple(
            () => Assert.Contains(message, diagnostic.Message, StringComparison.Ordinal),
            () => Assert.Equal(line, diagnostic.Line),
            () => Assert.Equal(column, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a definition with a problem still defines its name, so a reference to it is not reported again.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_DefinitionWithBadPattern_StillDefinesItsName()
    {
        // Arrange: a definition whose pattern is invalid, and a list that refers to it
        const string yaml = "ignoreRegExpList:\n  - Zorb\npatterns:\n  - name: Zorb\n    pattern: \"/(/\"\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: only the pattern is reported, not the reference to the name
        Assert.Contains("Cannot use the pattern 'Zorb'", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that errors in several settings are reported in source order.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_PatternErrorsAmongOtherKeys_AreReportedInSourceOrder()
    {
        // Arrange: a bad list entry, a bad definition, and a bad length, in that order
        const string yaml =
            "ignoreRegExpList:\n  - Urlz\npatterns:\n  - name: Zorb\n    pattern: \"/(/\"\nminRandomLength: 0\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: three errors, in source order
        Assert.Equal([2, 5, 6], result.Diagnostics.Select(diagnostic => diagnostic.Line));
    }

    /// <summary>
    ///     Test that null values of the new keys leave the defaults, like the other keys.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_NullPatternValues_LeaveDefaults()
    {
        // Arrange: every new key written with no value
        const string yaml = "ignoreRegExpList:\npatterns: ~\nignoreRandomStrings:\nminRandomLength: null\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: the defaults are kept
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Settings!.IgnoreRegExpList),
            () => Assert.Empty(result.Settings!.Patterns),
            () => Assert.True(result.Settings!.IgnoreRandomStrings),
            () => Assert.Equal(40, result.Settings!.MinRandomLength));
    }

    /// <summary>
    ///     Test that the random-string keys are mapped.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_RandomStringKeys_AreMapped()
    {
        // Arrange: the switch turned off and a length
        const string yaml = "ignoreRandomStrings: false\nminRandomLength: 25\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: both values are mapped
        Assert.Multiple(
            () => Assert.False(result.Settings!.IgnoreRandomStrings),
            () => Assert.Equal(25, result.Settings!.MinRandomLength));
    }

    /// <summary>
    ///     Test that invalid values of the random-string keys are reported at their value.
    /// </summary>
    /// <param name="yaml">A settings text with one invalid value.</param>
    /// <param name="message">The expected message.</param>
    [Theory]
    [InlineData("ignoreRandomStrings: yes\n", "Expected true or false for 'ignoreRandomStrings'.")]
    [InlineData("ignoreRandomStrings: \"true\"\n", "Expected true or false for 'ignoreRandomStrings'.")]
    [InlineData("ignoreRandomStrings: 1\n", "Expected true or false for 'ignoreRandomStrings'.")]
    [InlineData("minRandomLength: 0\n", "Expected a whole number of at least 1 for 'minRandomLength'.")]
    [InlineData("minRandomLength: -3\n", "Expected a whole number of at least 1 for 'minRandomLength'.")]
    [InlineData("minRandomLength: 4.5\n", "Expected a whole number of at least 1 for 'minRandomLength'.")]
    [InlineData("minRandomLength: \"40\"\n", "Expected a whole number of at least 1 for 'minRandomLength'.")]
    [InlineData("minRandomLength: 99999999999\n", "Expected a whole number of at least 1 for 'minRandomLength'.")]
    public void SettingsLoader_Parse_InvalidRandomStringValue_ReportsErrorAtValue(string yaml, string message)
    {
        // Arrange: the settings text and the expected message are passed in by the theory data
        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the message is reported at the value, which follows the key and a space
        Assert.Multiple(
            () => Assert.Equal(message, diagnostic.Message),
            () => Assert.Equal(1, diagnostic.Line));
    }

    /// <summary>
    ///     Test that a keyword of cspell that is still unsupported stays an error.
    /// </summary>
    [Fact]
    public void SettingsLoader_Parse_IncludeRegExpList_RemainsUnsupported()
    {
        // Arrange: the include counterpart of the supported key
        const string yaml = "includeRegExpList:\n  - Urls\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the key is reported as unsupported
        Assert.Equal("'includeRegExpList' is not a supported setting.", diagnostic.Message);
    }
}
