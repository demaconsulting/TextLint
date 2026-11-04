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

using System.Reflection;
using System.Runtime.Versioning;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.Tests.TestSupport;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.TextLint.Tests;

/// <summary>
///     System-level tests for the TextLint library.
/// </summary>
/// <remarks>
///     The package-level tests load the compiled assembly by name and verify the properties that
///     every release of the library must retain: its identity, its metadata, its target framework,
///     its trim compatibility, its independence from the tool, and that its public API exposes no
///     third-party types. The functional tests drive the public API end to end, combining the
///     Configuration and FileSelection subsystems against real temporary files, exactly as a
///     consuming application would.
/// </remarks>
public class TextLintTests
{
    /// <summary>
    ///     The simple name of the TextLint library assembly, which is also its NuGet package ID.
    /// </summary>
    private const string LibraryAssemblyName = "DemaConsulting.TextLint";

    /// <summary>
    ///     The namespace prefixes of the third-party packages the library depends on.
    /// </summary>
    private static readonly string[] ThirdPartyNamespaces = ["YamlDotNet", "Microsoft.Extensions.FileSystemGlobbing"];

    /// <summary>
    ///     Loads the TextLint library assembly by name.
    /// </summary>
    /// <returns>The loaded TextLint library assembly.</returns>
    private static Assembly LoadLibrary() => Assembly.Load(new AssemblyName(LibraryAssemblyName));

    /// <summary>
    ///     Writes a settings file into a temporary directory and loads it.
    /// </summary>
    /// <param name="temp">The temporary directory.</param>
    /// <param name="yaml">The text of the settings file.</param>
    /// <returns>The load result.</returns>
    private static Task<SettingsLoadResult> LoadConfigAsync(TempDirectory temp, string yaml) =>
        SettingsLoader.LoadAsync(temp.CreateFile(".textlint.yaml", yaml), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Selects files from a temporary tree and returns their relative paths.
    /// </summary>
    /// <param name="temp">The temporary tree.</param>
    /// <param name="settings">The settings to apply.</param>
    /// <returns>The relative paths of the selected files.</returns>
    private static string[] SelectPaths(TempDirectory temp, TextLintSettings settings) =>
        [.. FileSelector.Select(settings, temp.FullPath).Select(file => file.RelativePath)];

    /// <summary>
    ///     Test that the library assembly loads and carries the NuGet package name.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_HasPackageName()
    {
        // Arrange: no setup required; the library is referenced by this test project

        // Act: load the library assembly by its package name
        var assembly = LoadLibrary();

        // Assert: the loaded assembly carries the package name
        Assert.Equal(LibraryAssemblyName, assembly.GetName().Name);
    }

    /// <summary>
    ///     Test that the library assembly carries the package product and company metadata.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_HasPackageMetadata()
    {
        // Arrange: load the library assembly
        var assembly = LoadLibrary();

        // Act: read the product and company metadata attributes
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

        // Assert: the metadata identifies the package and its publisher
        Assert.Multiple(
            () => Assert.Equal(LibraryAssemblyName, product),
            () => Assert.Equal("DEMA Consulting", company));
    }

    /// <summary>
    ///     Test that the library assembly targets .NET 10.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_TargetsNet10()
    {
        // Arrange: load the library assembly
        var assembly = LoadLibrary();

        // Act: read the target framework the assembly was compiled for
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        // Assert: the assembly targets .NET 10
        Assert.Equal(".NETCoreApp,Version=v10.0", framework);
    }

    /// <summary>
    ///     Test that the library assembly is marked trimmable, as emitted when the project enables
    ///     the trim and Native AOT compatibility analyzers.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_IsMarkedTrimCompatible()
    {
        // Arrange: load the library assembly
        var assembly = LoadLibrary();

        // Act: read the assembly metadata that marks the assembly as trimmable
        var trimmable = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "IsTrimmable")?.Value;

        // Assert: the assembly declares itself trimmable
        Assert.Equal("True", trimmable);
    }

    /// <summary>
    ///     Test that the library assembly does not reference the TextLint tool.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_DoesNotReferenceTool()
    {
        // Arrange: load the library assembly
        var assembly = LoadLibrary();

        // Act: list the names of every assembly the library references
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        // Assert: the library never depends on the tool, keeping the tool a thin front-end
        Assert.DoesNotContain("DemaConsulting.TextLint.Tool", references);
    }

    /// <summary>
    ///     Test that no public member of the library exposes a type from a third-party package.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_PublicApiExposesNoThirdPartyTypes()
    {
        // Arrange: load the library and collect every type used in its public signatures
        var assembly = LoadLibrary();
        var used = new List<Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            used.Add(type.BaseType!);
            used.AddRange(type.GetInterfaces());
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Non-public members are not part of the API, so only visible signatures count
                switch (member)
                {
                    case PropertyInfo property when property.GetMethod?.IsPublic == true:
                        used.Add(property.PropertyType);
                        break;
                    case MethodBase { IsPublic: true } method:
                        used.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                        if (method is MethodInfo info)
                        {
                            used.Add(info.ReturnType);
                        }

                        break;
                    case FieldInfo { IsPublic: true } field:
                        used.Add(field.FieldType);
                        break;
                }
            }
        }

        // Act: find any used type whose namespace belongs to a third-party package
        var leaked = used
            .Where(type => ThirdPartyNamespaces.Any(prefix => type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .Select(type => type.FullName)
            .ToList();

        // Assert: none are exposed, so package upgrades cannot break consumers
        Assert.Empty(leaked);
    }

    /// <summary>
    ///     Test that a cspell-style settings file loads its configured values.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_CspellStyleConfigFile_LoadsConfiguredValues()
    {
        // Arrange: a configuration in the shape a cspell user already has, using cspell's key names
        using var temp = new TempDirectory();
        const string yaml = """
            # Spell-checking configuration
            version: "0.2"
            files:
              - "**/*.{md,cs}"
            ignorePaths:
              - "**/node_modules/**"
              - dist
            enableGlobDot: false
            """;

        // Act: load the settings file
        var result = await LoadConfigAsync(temp, yaml);

        // Assert: every value is loaded and there are no diagnostics
        var settings = result.Settings!;
        Assert.Multiple(
            () => Assert.Empty(result.Diagnostics),
            () => Assert.Equal("0.2", settings.Version),
            () => Assert.Equal(["**/*.{md,cs}"], settings.Files),
            () => Assert.Equal(["**/node_modules/**", "dist"], settings.IgnorePaths),
            () => Assert.False(settings.EnableGlobDot));
    }

    /// <summary>
    ///     Test that an empty settings file loads default settings.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_EmptyConfigFile_LoadsDefaultSettings()
    {
        // Arrange: an empty settings file
        using var temp = new TempDirectory();

        // Act: load the settings file
        var result = await LoadConfigAsync(temp, string.Empty);

        // Assert: default settings are loaded
        Assert.Multiple(
            () => Assert.True(result.Succeeded),
            () => Assert.Empty(result.Settings!.Files),
            () => Assert.Empty(result.Settings!.IgnorePaths),
            () => Assert.False(result.Settings!.EnableGlobDot));
    }

    /// <summary>
    ///     Test that settings built in code, without any file, select files.
    /// </summary>
    [Fact]
    public void TextLint_Settings_ConstructedInCode_SelectsFiles()
    {
        // Arrange: a tree and settings built in code
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("src/Program.cs");
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: select files with the in-code settings
        var paths = SelectPaths(temp, settings);

        // Assert: the matching file is selected
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that malformed YAML is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_MalformedConfigFile_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file with an unclosed list
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "version: 0.2\nfiles: [\"a\", \n");

        // Assert: one error that names the file and has a line and column
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.True(diagnostic.Line >= 2),
            () => Assert.NotNull(diagnostic.Column));
    }

    /// <summary>
    ///     Test that a value of the wrong type is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_WrongValueType_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file whose files value is not a list
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "version: 0.2\nfiles: \"**/*.md\"\n");

        // Assert: the error is at the value on line 2
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(8, diagnostic.Column));
    }

    /// <summary>
    ///     Test that a missing settings file is reported as an error naming the file.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_MissingConfigFile_ReportsFileError()
    {
        // Arrange: a settings path that does not exist
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.FullPath, ".textlint.yaml");

        // Act: load it
        var result = await SettingsLoader.LoadAsync(path, TestContext.Current.CancellationToken);

        // Assert: loading fails with an error that names the file
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Equal(path, Assert.Single(result.Diagnostics).FilePath));
    }

    /// <summary>
    ///     Test that an unsupported key is an error that names the key and its position.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_UnsupportedKey_ReportsErrorNamingKey()
    {
        // Arrange: a settings file with a key TextLint does not support
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "version: 0.2\nwords:\n  - foo\n");

        // Assert: loading fails and the error names the key, file, and position
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("'words' is not a supported setting", diagnostic.Message),
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Equal(1, diagnostic.Column));
    }

    /// <summary>
    ///     Test that an invalid glob pattern is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_InvalidPattern_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file whose second pattern uses a backslash
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "files:\n  - \"**/*.md\"\n  - 'docs\\*.md'\n");

        // Assert: the error is at the second item and names the pattern
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column),
            () => Assert.Contains("docs\\*.md", diagnostic.Message));
    }

    /// <summary>
    ///     Test that include patterns select the matching files.
    /// </summary>
    [Fact]
    public void TextLint_Files_IncludePatterns_SelectsMatchingFiles()
    {
        // Arrange: a tree with markdown and C# files
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/guide.md");
        temp.CreateFile("src/Program.cs");

        // Act: select the markdown files
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"] });

        // Assert: only the markdown files are selected
        Assert.Equal(["README.md", "docs/guide.md"], paths);
    }

    /// <summary>
    ///     Test that brace alternatives and bare names select files at any depth.
    /// </summary>
    [Fact]
    public void TextLint_Files_BraceAndBareNamePatterns_SelectAtAnyDepth()
    {
        // Arrange: files of several kinds at several depths
        using var temp = new TempDirectory();
        temp.CreateFile("a.md");
        temp.CreateFile("x/b.cs");
        temp.CreateFile("x/y/package-lock.json");
        temp.CreateFile("x/c.txt");

        // Act: select with a brace pattern and a bare name
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.{md,cs}", "package-lock.json"] });

        // Assert: all matches at every depth are selected
        Assert.Equal(["a.md", "x/b.cs", "x/y/package-lock.json"], paths);
    }

    /// <summary>
    ///     Test that a file whose name differs only in case is not selected.
    /// </summary>
    [Fact]
    public void TextLint_Files_DifferentCase_IsNotSelected()
    {
        // Arrange: a file whose extension is upper case
        using var temp = new TempDirectory();
        temp.CreateFile("README.MD");

        // Act: select with a lower-case pattern
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"] });

        // Assert: matching is case-sensitive on every platform
        Assert.Empty(paths);
    }

    /// <summary>
    ///     Test that ignore patterns exclude files from the selection.
    /// </summary>
    [Fact]
    public void TextLint_Files_IgnorePatterns_ExcludesIgnoredFiles()
    {
        // Arrange: a tree with a generated directory
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("generated/api.md");

        // Act: select markdown files, ignoring the generated directory
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"], IgnorePaths = ["generated"] });

        // Assert: the ignored files are not selected
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that an ignored directory is not enumerated.
    /// </summary>
    [Fact]
    public void TextLint_Files_IgnoredDirectory_IsNotEnumerated()
    {
        // Arrange: a tree with a large ignored directory, observed through a recording directory
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("node_modules/a/b/readme.md");
        var recording = new RecordingDirectoryInfo(new DirectoryInfoWrapper(new DirectoryInfo(temp.FullPath)));
        var settings = new TextLintSettings { Files = ["**/*.md"], IgnorePaths = ["node_modules"] };

        // Act: select files
        var files = FileSelector.Select(settings, recording);

        // Assert: the ignored directory was never read, and the visible file was still found
        Assert.Multiple(
            () => Assert.Equal(["README.md"], files.Select(file => file.RelativePath)),
            () => Assert.DoesNotContain(recording.EnumeratedDirectories, path => path.StartsWith("node_modules", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that hidden files and directories are skipped by default.
    /// </summary>
    [Fact]
    public void TextLint_Files_HiddenEntries_SkippedByDefault()
    {
        // Arrange: visible, hidden, and hidden-directory files
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile(".hidden.md");
        temp.CreateFile(".git/notes.md");

        // Act: select with wildcards and default settings
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"] });

        // Assert: hidden entries are skipped
        Assert.Equal(["visible.md"], paths);
    }

    /// <summary>
    ///     Test that hidden names written literally in a pattern are selected.
    /// </summary>
    [Fact]
    public void TextLint_Files_LiteralHiddenPattern_SelectsHiddenEntries()
    {
        // Arrange: a workflow file in a hidden directory and a file in another hidden directory
        using var temp = new TempDirectory();
        temp.CreateFile(".github/workflows/ci.yml");
        temp.CreateFile(".cache/other.yml");

        // Act: select with a pattern that names .github literally
        var paths = SelectPaths(temp, new TextLintSettings { Files = [".github/**/*.yml"] });

        // Assert: the literally named hidden directory is searched; the other is not
        Assert.Equal([".github/workflows/ci.yml"], paths);
    }

    /// <summary>
    ///     Test that hidden entries are selected when hidden-file matching is enabled.
    /// </summary>
    [Fact]
    public void TextLint_Files_HiddenEntries_SelectedWhenEnabled()
    {
        // Arrange: visible, hidden, and hidden-directory files
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile(".hidden.md");
        temp.CreateFile(".git/notes.md");

        // Act: select with hidden-file matching enabled
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"], EnableGlobDot = true });

        // Assert: wildcards match the hidden entries as well
        Assert.Equal([".git/notes.md", ".hidden.md", "visible.md"], paths);
    }

    /// <summary>
    ///     Test that selected files are ordered by ordinal relative path with forward slashes.
    /// </summary>
    [Fact]
    public void TextLint_Files_SelectedFiles_AreOrderedWithForwardSlashes()
    {
        // Arrange: nested files created out of order
        using var temp = new TempDirectory();
        temp.CreateFile("z/deep/last.md");
        temp.CreateFile("a/first.md");
        temp.CreateFile("B.md");

        // Act: select all markdown files
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["**/*.md"] });

        // Assert: ordinal order, with forward slashes at every depth
        Assert.Equal(["B.md", "a/first.md", "z/deep/last.md"], paths);
    }

    /// <summary>
    ///     Test that a negated pattern is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void TextLint_Files_NegatedPattern_ThrowsNamingPattern()
    {
        // Arrange: settings built in code with a negated pattern
        using var temp = new TempDirectory();
        var settings = new TextLintSettings { Files = ["!docs/**"] };

        // Act / Assert: selection throws an exception that names the pattern
        var exception = Assert.Throws<InvalidGlobPatternException>(() => FileSelector.Select(settings, temp.FullPath));
        Assert.Contains("'!docs/**'", exception.Message);
    }

    /// <summary>
    ///     Test that a pattern with a backslash is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void TextLint_Files_BackslashPattern_ThrowsNamingPattern()
    {
        // Arrange: settings built in code with a backslash pattern
        using var temp = new TempDirectory();
        var settings = new TextLintSettings { Files = ["docs\\*.md"] };

        // Act / Assert: selection throws an exception that names the pattern
        var exception = Assert.Throws<InvalidGlobPatternException>(() => FileSelector.Select(settings, temp.FullPath));
        Assert.Contains("'docs\\*.md'", exception.Message);
    }
}
