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

using System.Text;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Unit tests for the <see cref="SettingsLoader"/> class.
/// </summary>
/// <remarks>
///     The loader is tested with real YamlDotNet parsing (an OTS item, not a test double), real
///     temporary files, and the real glob pattern expander it depends on for pattern validation.
/// </remarks>
public class SettingsLoaderTests
{
    /// <summary>
    ///     The source name used for diagnostics in text-based tests.
    /// </summary>
    private const string Source = "cfg.yaml";

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
        const string yaml = "version: \"0.2\"\nfiles:\n  - \"**/*.md\"\n  - src/**/*.cs\nignorePaths:\n  - node_modules\nenableGlobDot: true\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: every value is mapped
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("0.2", settings.Version),
            () => Assert.Equal(["**/*.md", "src/**/*.cs"], settings.Files),
            () => Assert.Equal(["node_modules"], settings.IgnorePaths),
            () => Assert.True(settings.EnableGlobDot));
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
        const string yaml = "version: 0.2\nwords:\n  - foo\n";

        // Act: parse the text
        var diagnostic = ParseSingleError(yaml);

        // Assert: the error names the key and is at the key
        Assert.Multiple(
            () => Assert.Equal("'words' is not a supported setting.", diagnostic.Message),
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
        const string yaml = "files:\n  - \"**/*.md\"\nlanguage: en\n";

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
        const string yaml = "words: []\nlanguage: en\nminWordLength: 4\n";

        // Act: parse the text
        var result = SettingsLoader.Parse(yaml, Source);

        // Assert: one error per key, in source order
        Assert.Equal(
            ["'words' is not a supported setting.", "'language' is not a supported setting.", "'minWordLength' is not a supported setting."],
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
}
