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

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Subsystem tests for the Configuration subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="SettingsLoader"/> and <see cref="TextLintSettings"/> together
///     through real settings files. The subsystem's one documented dependency outside itself, the
///     glob pattern expander used to validate patterns at load time, is used for real.
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
        const string yaml = "version: \"0.2\"\nfiles: [\"**/*.md\"]\nignorePaths: [node_modules]\nenableGlobDot: true\n";

        // Act: load it
        var result = await LoadAsync(temp, yaml);

        // Assert: every value is present in the settings
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Equal("0.2", settings.Version),
            () => Assert.Equal(["**/*.md"], settings.Files),
            () => Assert.Equal(["node_modules"], settings.IgnorePaths),
            () => Assert.True(settings.EnableGlobDot));
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

        // Assert: the settings are the defaults
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Null(result.Settings!.Version),
            () => Assert.Empty(result.Settings!.Files),
            () => Assert.False(result.Settings!.EnableGlobDot));
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
        var result = await LoadAsync(temp, "version: 0.2\nwords: [foo]\n");

        // Assert: loading fails with an error that names the key and its position
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("'words' is not a supported setting", diagnostic.Message),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(1, diagnostic.Column));
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
}
