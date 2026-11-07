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

// cspell:ignore favourite neighbour organise tokenizer kubernetes powershell textlint xyzzyq cafè tabel catalogue labelled honoured modelling curser operants programing milage writeable unannotated mockable honour colour centre analyse judgement colourize colourized decolourize glamourize glamourized ation ations organizability unmaneuverable unmanoeuvrable manoeuvre segoe consolas calibri menlo

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.SpellChecking;
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
///     Configuration and FileSelection subsystems against real temporary files, and the
///     Dictionaries subsystem against the real embedded dictionaries, exactly as a consuming
///     application would.
/// </remarks>
public class TextLintTests
{
    /// <summary>
    ///     The simple name of the TextLint library assembly, which is also its NuGet package ID.
    /// </summary>
    private const string LibraryAssemblyName = "DemaConsulting.TextLint";

    /// <summary>
    ///     The start of the logical name of every dictionary embedded in the library assembly.
    /// </summary>
    private const string DictionaryResourcePrefix = "DemaConsulting.TextLint.Dictionaries.";

    /// <summary>
    ///     The text at the start of every dictionary file.
    /// </summary>
    private const string DictionaryIdentifier = "TLD1";

    /// <summary>
    ///     The number of characters in a text that is far longer than any word.
    /// </summary>
    private const int VeryLongTextLength = 10_000;

    /// <summary>
    ///     The number of lookups the parallel test performs.
    /// </summary>
    private const int ParallelLookups = 20_000;

    /// <summary>
    ///     The most failures a data test reports, so a systematic failure stays readable.
    /// </summary>
    private const int MaxReportedFailures = 20;

    /// <summary>
    ///     The number of words below which the word lists are considered not to have been found.
    /// </summary>
    private const int MinimumCheckedWords = 100_000;

    /// <summary>
    ///     The file name of the reviewed exclusions list inside the word-list folder.
    /// </summary>
    private const string ExclusionsFileName = "exclusions.txt";

    /// <summary>
    ///     The file name of the routing script, which defines the -ize endings, inside the word-list folder.
    /// </summary>
    private const string RoutingScriptFileName = "word_forms.py";

    /// <summary>
    ///     The distance from a lower-case ASCII or Latin-1 letter to its upper-case partner.
    /// </summary>
    private const int UpperCaseOffset = 32;

    /// <summary>
    ///     The namespace prefixes of the third-party packages the library depends on.
    /// </summary>
    private static readonly string[] ThirdPartyNamespaces = ["YamlDotNet", "Microsoft.Extensions.FileSystemGlobbing"];

    /// <summary>
    ///     American spellings paired with their British -ise spellings, for words whose British spelling does not depend on the part of speech.
    /// </summary>
    private static readonly (string American, string British)[] CuratedSpellingPairs =
    [
        ("honor", "honour"), ("color", "colour"), ("center", "centre"), ("catalog", "catalogue"),
        ("labeled", "labelled"), ("organize", "organise"), ("analyze", "analyse"), ("modeling", "modelling"),
        ("judgment", "judgement"),
    ];

    /// <summary>
    ///     Matches the line of the routing script that defines the -ize endings and captures the pattern text.
    /// </summary>
    private static readonly Regex IzeFormDefinition = new("^IZE_FORM = re\\.compile\\(r\"([^\"]+)\"\\)", RegexOptions.Multiline | RegexOptions.CultureInvariant);

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
        [.. SelectFiles(temp.FullPath, settings).Select(file => file.RelativePath)];

    /// <summary>
    ///     Selects files by passing the values of a settings object to the file selector, exactly as a consuming application does.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="settings">The settings whose patterns and hidden-file switch are passed on.</param>
    /// <returns>The selected files.</returns>
    private static IReadOnlyList<SelectedFile> SelectFiles(string root, TextLintSettings settings) =>
        FileSelector.Select(root, settings.Files, settings.IgnorePaths, settings.EnableGlobDot, TestContext.Current.CancellationToken);

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
        var result = await LoadConfigAsync(temp, "version: 0.2\ncaseSensitive: true\n");

        // Assert: loading fails and the error names the key, file, and position
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Null(result.Settings),
            () => Assert.Contains("'caseSensitive' is not a supported setting", diagnostic.Message),
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
        var files = FileSelector.Select(recording, settings.Files, settings.IgnorePaths, settings.EnableGlobDot, TestContext.Current.CancellationToken);

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
    ///     Test that an absolute path pattern is rejected naming the pattern.
    /// </summary>
    [Fact]
    public void TextLint_Files_AbsolutePathPattern_ThrowsNamingPattern()
    {
        // Arrange: settings built in code with a drive path
        using var temp = new TempDirectory();
        var settings = new TextLintSettings { Files = ["C:/docs/**"] };

        // Act / Assert: selection throws an exception that names the pattern and says it is absolute
        var exception = Assert.Throws<InvalidGlobPatternException>(() => SelectFiles(temp.FullPath, settings));
        Assert.Multiple(
            () => Assert.Contains("'C:/docs/**'", exception.Message),
            () => Assert.Contains("absolute paths", exception.Message));
    }

    /// <summary>
    ///     Test that an absolute path argument is checked and its findings are named by absolute path.
    /// </summary>
    [Fact]
    public async Task TextLint_CommandLinePaths_AbsolutePath_ChecksFileAndNamesItAbsolute()
    {
        // Arrange: a file with a misspelling, named on the command line by its absolute path
        using var temp = new TempDirectory();
        var file = temp.CreateFile("docs/a.md", "Hello xyzzyq\n");

        // Act: resolve the argument, check what it selects, and name the finding as a command-line front-end would
        var selection = CommandLinePathResolver.Resolve(temp.FullPath, [file]);
        var settings = new TextLintSettings { Files = selection.Patterns };
        var result = await SpellChecker.CheckAsync(settings, selection.RootDirectory, cancellationToken: TestContext.Current.CancellationToken);
        var issue = Assert.Single(result.Issues);

        // Assert: the finding is named by the absolute path the user typed
        Assert.Equal($"{file}(1,7): error: Unknown word 'xyzzyq'", issue.Format(selection.GetDisplayPath(issue.FilePath!)));
    }

    /// <summary>
    ///     Test that a relative path argument is checked and its findings are named by relative path.
    /// </summary>
    [Fact]
    public async Task TextLint_CommandLinePaths_RelativePath_ChecksFileAndNamesItRelative()
    {
        // Arrange: a file with a misspelling, named on the command line by a relative path
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", "Hello xyzzyq\n");

        // Act: resolve the argument, check what it selects, and name the finding as a command-line front-end would
        var selection = CommandLinePathResolver.Resolve(temp.FullPath, ["docs/*.md"]);
        var settings = new TextLintSettings { Files = selection.Patterns };
        var result = await SpellChecker.CheckAsync(settings, selection.RootDirectory, cancellationToken: TestContext.Current.CancellationToken);
        var issue = Assert.Single(result.Issues);

        // Assert: the finding is named by the relative path with forward slashes
        Assert.Equal("docs/a.md(1,7): error: Unknown word 'xyzzyq'", issue.Format(selection.GetDisplayPath(issue.FilePath!)));
    }

    /// <summary>
    ///     Test that a pattern with a leading slash is anchored at the root directory.
    /// </summary>
    [Fact]
    public void TextLint_Files_LeadingSlashPattern_AnchorsAtRoot()
    {
        // Arrange: a file named README.md at the root and another nested
        using var temp = new TempDirectory();
        temp.CreateFile("README.md");
        temp.CreateFile("docs/README.md");

        // Act: select with a leading-slash pattern
        var paths = SelectPaths(temp, new TextLintSettings { Files = ["/README.md"] });

        // Assert: only the file in the root directory is selected
        Assert.Equal(["README.md"], paths);
    }

    /// <summary>
    ///     Test that an absolute path pattern in a settings file is reported with the file, line, and column.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_AbsolutePathPattern_ReportsFileLineAndColumn()
    {
        // Arrange: a settings file whose second pattern is a drive path
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "files:\n  - \"**/*.md\"\n  - \"C:/docs/**\"\n");

        // Assert: the error is at the second item, names the file, and says the path is absolute
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.EndsWith(".textlint.yaml", diagnostic.FilePath),
            () => Assert.Equal(3, diagnostic.Line),
            () => Assert.Equal(5, diagnostic.Column),
            () => Assert.Contains("absolute paths", diagnostic.Message));
    }

    /// <summary>
    ///     Test that a question-mark pattern in a settings file is reported with a suggestion to use the star.
    /// </summary>
    [Fact]
    public async Task TextLint_Settings_QuestionMarkPattern_ReportsErrorSuggestingStar()
    {
        // Arrange: a settings file whose pattern uses the unsupported ? wildcard
        using var temp = new TempDirectory();

        // Act: load it
        var result = await LoadConfigAsync(temp, "files:\n  - \"docs/file?.md\"\n");

        // Assert: the error is at the item, names the pattern, and suggests the supported wildcard
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Multiple(
            () => Assert.Equal(2, diagnostic.Line),
            () => Assert.Contains("'docs/file?.md'", diagnostic.Message),
            () => Assert.Contains("use '*'", diagnostic.Message));
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
        var exception = Assert.Throws<InvalidGlobPatternException>(() => SelectFiles(temp.FullPath, settings));
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
        var exception = Assert.Throws<InvalidGlobPatternException>(() => SelectFiles(temp.FullPath, settings));
        Assert.Contains("'docs\\*.md'", exception.Message);
    }

    /// <summary>
    ///     Test that a word in the dictionaries is reported as known.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_KnownWord_IsKnown()
    {
        // Arrange: the default dictionary selection
        var dictionary = SpellingDictionary.Default;

        // Act: ask about an ordinary English word
        var known = dictionary.IsKnown("table");

        // Assert: the word is known
        Assert.True(known);
    }

    /// <summary>
    ///     Test that a word that is in no dictionary is reported as not known.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_UnknownWord_IsNotKnown()
    {
        // Arrange: the default dictionary selection
        var dictionary = SpellingDictionary.Default;

        // Act: ask about a misspelling and a nonsense word
        var misspelling = dictionary.IsKnown("tabel");
        var nonsense = dictionary.IsKnown("xyzzyq");

        // Assert: neither is known
        Assert.Multiple(
            () => Assert.False(misspelling),
            () => Assert.False(nonsense));
    }

    /// <summary>
    ///     Test that letter case does not change whether a word is known.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_DifferentCase_IsKnown()
    {
        // Arrange: the default dictionary selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: lower, capitalized, upper, and mixed case are all known, including a name
        // that the word lists write with a capital
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("table")),
            () => Assert.True(dictionary.IsKnown("Table")),
            () => Assert.True(dictionary.IsKnown("TABLE")),
            () => Assert.True(dictionary.IsKnown("tABLe")),
            () => Assert.True(dictionary.IsKnown("Paris")),
            () => Assert.True(dictionary.IsKnown("paris")),
            () => Assert.True(dictionary.IsKnown("PARIS")));
    }

    /// <summary>
    ///     Test that the typographic apostrophe, contractions, and possessives are known.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_TypographicApostrophe_IsKnown()
    {
        // Arrange: the default dictionary selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: contractions and possessives are known with the straight and the curly apostrophe
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("don't")),
            () => Assert.True(dictionary.IsKnown("don\u2019t")),
            () => Assert.True(dictionary.IsKnown("dog's")),
            () => Assert.True(dictionary.IsKnown("dog\u2019s")));
    }

    /// <summary>
    ///     Test that accented words, text in other scripts, empty text, and very long text are handled without error.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_UnusualText_IsHandledWithoutError()
    {
        // Arrange: the default dictionary selection and unusual text
        var dictionary = SpellingDictionary.Default;
        var veryLong = new string('a', VeryLongTextLength);

        // Act / Assert: accented words are known in either case and with either spelling, while a wrong
        // accent, another script, empty text, blank text, and very long text are not known and never throw
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("café")),
            () => Assert.True(dictionary.IsKnown("CAFÉ")),
            () => Assert.True(dictionary.IsKnown("cafe")),
            () => Assert.False(dictionary.IsKnown("cafè")),
            () => Assert.False(dictionary.IsKnown("日本語")),
            () => Assert.False(dictionary.IsKnown(string.Empty)),
            () => Assert.False(dictionary.IsKnown("   ")),
            () => Assert.False(dictionary.IsKnown(veryLong)));
    }

    /// <summary>
    ///     Test that the default language is American English and that <c>en</c> means the same.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_DefaultLanguage_AcceptsAmericanNotBritishSpelling()
    {
        // Arrange: the default selection and the one named en
        var dictionary = SpellingDictionary.Default;
        var english = SpellingDictionary.ForLanguages("en");

        // Act / Assert: American spellings are known and British ones are not, whichever way it is chosen
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("favorite")),
            () => Assert.True(dictionary.IsKnown("neighbor")),
            () => Assert.False(dictionary.IsKnown("favourite")),
            () => Assert.False(dictionary.IsKnown("neighbour")),
            () => Assert.False(dictionary.IsKnown("organise")),
            () => Assert.Same(dictionary, english));
    }

    /// <summary>
    ///     Test that British English accepts British spellings and not American ones.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_BritishLanguage_AcceptsBritishNotAmericanSpelling()
    {
        // Arrange: the British selection
        var dictionary = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: British spellings are known and American ones are not
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("favourite")),
            () => Assert.True(dictionary.IsKnown("neighbour")),
            () => Assert.True(dictionary.IsKnown("organise")),
            () => Assert.False(dictionary.IsKnown("favorite")),
            () => Assert.False(dictionary.IsKnown("neighbor")));
    }

    /// <summary>
    ///     Test that selecting both languages accepts both spellings.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_BothLanguages_AcceptBothSpellings()
    {
        // Arrange: the selection of both languages, written with spaces
        var dictionary = SpellingDictionary.ForLanguages("en-US, en-GB");

        // Act / Assert: American and British spellings are both known
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("favorite")),
            () => Assert.True(dictionary.IsKnown("favourite")),
            () => Assert.True(dictionary.IsKnown("neighbor")),
            () => Assert.True(dictionary.IsKnown("neighbour")));
    }

    /// <summary>
    ///     Test that an unsupported language is rejected with an error that names it.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_UnsupportedLanguage_ThrowsNamingLanguage()
    {
        // Arrange: no setup required

        // Act: ask for a language the library does not provide
        var exception = Assert.Throws<ArgumentException>(() => SpellingDictionary.ForLanguages("en-AU"));

        // Assert: the error names the language and lists the supported ones
        Assert.Multiple(
            () => Assert.Contains("en-AU", exception.Message),
            () => Assert.Contains("en-GB", exception.Message),
            () => Assert.Contains("en-US", exception.Message));
    }

    /// <summary>
    ///     Test that technical terms are known in every language selection.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_TechnicalTerm_IsKnownInEveryLanguage()
    {
        // Arrange: every language selection and technical terms of several kinds, including font names and a
        // programming term in an American spelling that the word lists keep for every language
        string[] terms =
        [
            "kubernetes", "initialize", "github", "powershell", "microsoft", "textlint", "tokenizer",
            "segoe", "consolas", "calibri", "menlo", "roboto"
        ];
        var selections = new[] { "en-US", "en-GB", "en-US,en-GB" }.Select(SpellingDictionary.ForLanguages).ToArray();

        // Act: find the terms each selection does not know
        var unknown = selections.SelectMany(dictionary => terms.Where(term => !dictionary.IsKnown(term))).ToArray();

        // Assert: every selection knows every term
        Assert.Empty(unknown);
    }

    /// <summary>
    ///     The shape of <see cref="SpellingDictionary.IsKnown"/>, so the method of a copy of the library loaded
    ///     into another load context can be called without a reference to that copy's types.
    /// </summary>
    /// <param name="word">The word to look up.</param>
    /// <returns>Whether the word is known.</returns>
    private delegate bool IsKnownDelegate(ReadOnlySpan<char> word);

    /// <summary>
    ///     Test that a copy of the library assembly with nothing beside it still looks words up.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_LibraryAssemblyAlone_LooksUpWords()
    {
        // Arrange: load the bytes of the library assembly, and nothing else, into a separate load context, so no
        // dictionary file, settings file, or other assembly of the library is available to it
        var bytes = File.ReadAllBytes(LoadLibrary().Location);
        var context = new AssemblyLoadContext("library-alone");
        using var stream = new MemoryStream(bytes);
        var type = context.LoadFromStream(stream).GetType("DemaConsulting.TextLint.Dictionaries.SpellingDictionary")!;
        var dictionary = type.GetMethod("ForLanguages")!.Invoke(null, ["en-GB"])!;
        var isKnown = type.GetMethod("IsKnown")!.CreateDelegate<IsKnownDelegate>(dictionary);

        // Act: look up an English word, a British spelling, an American spelling, a technical term, and a nonsense word
        var answers = new[] { "table", "neighbour", "neighbor", "kubernetes", "xyzzyq" }.Select(word => isKnown(word)).ToArray();

        // Assert: the answers are those of the British selection
        Assert.Equal([true, true, false, true, false], answers);
    }

    /// <summary>
    ///     Test that the library assembly carries the four dictionaries as embedded resources.
    /// </summary>
    [Fact]
    public void TextLint_Assembly_Loaded_EmbedsDictionaries()
    {
        // Arrange: load the library assembly and the names the dictionaries must have
        var assembly = LoadLibrary();
        string[] expected =
        [
            $"{DictionaryResourcePrefix}en-GB.tld",
            $"{DictionaryResourcePrefix}en-US.tld",
            $"{DictionaryResourcePrefix}en-common.tld",
            $"{DictionaryResourcePrefix}technical.tld",
        ];

        // Act: list the dictionary resources and read the start of each
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(DictionaryResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var identifiers = names.Select(name =>
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var start = new byte[DictionaryIdentifier.Length];
            stream.ReadExactly(start);
            return Encoding.ASCII.GetString(start);
        }).ToArray();

        // Assert: exactly the four dictionaries are embedded and each is a dictionary file
        Assert.Multiple(
            () => Assert.Equal(expected, names),
            () => Assert.All(identifiers, identifier => Assert.Equal(DictionaryIdentifier, identifier)));
    }

    /// <summary>
    ///     Test that every word of every word list is known for the language selection that includes its list, except the excluded words.
    /// </summary>
    /// <remarks>
    ///     The reviewed exclusions list names words that the builder removes from every dictionary, so
    ///     they are skipped here explicitly; <see cref="TextLint_Dictionary_EveryExcludedWord_IsUnknown"/>
    ///     checks that they are unknown instead. The exclusions list is not a word list of any
    ///     dictionary and is not itself checked as one.
    /// </remarks>
    [Fact]
    public void TextLint_Dictionary_EveryListedWord_IsKnown()
    {
        // Arrange: every word list under the word-list folder copied beside the tests, and the words the exclusions list removes
        var root = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var excluded = ReadExcludedWords(root);
        var files = Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories)
            .Where(file => !string.Equals(Path.GetFileName(file), ExclusionsFileName, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).ToArray();
        var failures = new List<string>();
        var checkedWords = 0;

        // Act: look up each entry as written, in lower case, and in upper case, in each selection that includes its list
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var dictionaries = SelectionsFor(relative).Select(SpellingDictionary.ForLanguages).ToArray();
            foreach (var entry in File.ReadLines(file).Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] != '#'))
            {
                if (excluded.Contains(entry.ToLowerInvariant()))
                {
                    continue;
                }

                foreach (var form in new[] { entry, entry.ToLowerInvariant(), UpperCaseLatin1(entry) })
                {
                    checkedWords++;
                    if (!dictionaries.All(dictionary => dictionary.IsKnown(form)))
                    {
                        failures.Add($"{relative}: {form}");
                    }
                }
            }
        }

        // Assert: the lists held a substantial vocabulary and every word was known
        Assert.Multiple(
            () => Assert.Empty(failures.Take(MaxReportedFailures)),
            () => Assert.True(checkedWords > MinimumCheckedWords, $"Only {checkedWords} words were checked."));
    }

    /// <summary>
    ///     Test that derived forms are known everywhere and regional spellings only for their own language selection.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_DerivedFormsAndRegionalSpellings_FollowTheLanguageSelection()
    {
        // Arrange: derived forms the base word list lacks, British-only spellings, and an American-only spelling
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");
        var derived = new[] { "testability", "reviewable", "precomputed", "unannotated", "untestable" };
        var britishOnly = new[] { "catalogue", "labelled", "honoured", "modelling" };

        // Act: look the words up through the public lookup of each selection
        var derivedUnknown = derived.Where(word => !american.IsKnown(word) || !british.IsKnown(word)).ToArray();
        var britishKnownToAmerican = britishOnly.Where(word => american.IsKnown(word)).ToArray();
        var britishUnknownToBritish = britishOnly.Where(word => !british.IsKnown(word)).ToArray();

        // Assert: derived forms are known in both, and British spellings only in the British selection
        Assert.Multiple(
            () => Assert.Empty(derivedUnknown),
            () => Assert.Empty(britishKnownToAmerican),
            () => Assert.Empty(britishUnknownToBritish),
            () => Assert.True(american.IsKnown("favorite") && !british.IsKnown("favorite")));
    }

    /// <summary>
    ///     Test that curated American and British spellings of the same word stay in their own language selection, in every letter case.
    /// </summary>
    /// <remarks>
    ///     The dictionaries fold letter case, so a capitalized form of a word (a name such as Honor) must not carry
    ///     the word into the wrong region. Spelling checks do not know the part of speech, so only pairs whose British
    ///     form differs in spelling regardless of the part of speech are listed (license and practice are not).
    ///     The technical dictionary is shared by every language and deliberately keeps the American spelling of a
    ///     word that is also a programming term (color, center, catalog, labeled, modeling, organize, analyze), so
    ///     British English knows those American spellings; the committed word lists, which do not hold them for
    ///     British English, are checked for all nine pairs by <see cref="TextLint_Dictionary_CommittedWordLists_HaveNoCrossRegionLeaks"/>.
    ///     Here every British spelling must be American-unknown, and the American spellings that are not
    ///     technical terms must be British-unknown.
    /// </remarks>
    [Fact]
    public void TextLint_Dictionary_CuratedSpellingPairs_StayInTheirOwnRegion()
    {
        // Arrange: the British spellings of the curated pairs, the American spellings that are not technical terms, and the three language selections
        var britishSpellings = CuratedSpellingPairs.Select(pair => pair.British).ToArray();
        var americanSpellings = new[] { "honor", "judgment", "favorite", "defense", "recognize", "emphasize" };
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");
        var both = SpellingDictionary.ForLanguages("en-US,en-GB");
        var failures = new List<string>();

        // Act: look up each spelling as written, capitalized, and in upper case in each selection
        foreach (var form in new Func<string, string>[] { word => word, Capitalize, UpperCaseLatin1 })
        {
            foreach (var spelling in americanSpellings.Select(form))
            {
                if (!american.IsKnown(spelling) || british.IsKnown(spelling) || !both.IsKnown(spelling))
                {
                    failures.Add($"American spelling {spelling} is not American-only");
                }
            }

            foreach (var spelling in britishSpellings.Select(form))
            {
                if (american.IsKnown(spelling) || !british.IsKnown(spelling) || !both.IsKnown(spelling))
                {
                    failures.Add($"British spelling {spelling} is not British-only");
                }
            }
        }

        // Assert: no spelling crossed into the other region
        Assert.Empty(failures);
    }

    /// <summary>
    ///     Test that -ize forms with a British -ise counterpart are not known to British English.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_IzeFormsWithAnIseCounterpart_AreUnknownToBritishEnglish()
    {
        // Arrange: -ize forms that have a known -ise counterpart in British English and are not technical terms, and the British selection
        var izeForms = new[]
        {
            "colourize", "colourized", "decolourize", "glamourize", "glamourized", "recognize", "realize", "emphasize",
            "summarizing", "criticized", "organizational", "organizationally",
        };
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act: collect the forms that British English knows
        var known = izeForms.Where(word => british.IsKnown(word)).ToArray();

        // Assert: British English is -ise only, so none of them is known
        Assert.Empty(known);
    }

    /// <summary>
    ///     Test that the committed word-list folders hold no spelling of a curated pair that belongs to the other region.
    /// </summary>
    /// <remarks>
    ///     The dictionaries fold letter case, so the lists are compared on lower-case words, the way the builder
    ///     combines them: the common and American lists must not hold the British spelling of any pair, the common
    ///     and British lists must not hold the American spelling, and the British lists hold no -ize form.
    /// </remarks>
    [Fact]
    public void TextLint_Dictionary_CommittedWordLists_HaveNoCrossRegionLeaks()
    {
        // Arrange: the words of the common, American, and British folders (lower-cased) and the curated pairs
        var root = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var izeForm = ReadProductionIzeForm(root);
        var common = ReadFolderWords(root, "en-common");
        var american = ReadFolderWords(root, "en-US");
        var british = ReadFolderWords(root, "en-GB");
        var pairs = CuratedSpellingPairs;
        var failures = new List<string>();

        // Act: check each pair and the British folder for -ize forms with a counterpart
        foreach (var (americanSpelling, britishSpelling) in pairs)
        {
            if (!american.Contains(americanSpelling) || common.Contains(americanSpelling) || british.Contains(americanSpelling))
            {
                failures.Add($"{americanSpelling} must be in the American folder only");
            }

            if (!british.Contains(britishSpelling) || common.Contains(britishSpelling) || american.Contains(britishSpelling))
            {
                failures.Add($"{britishSpelling} must be in the British folder only");
            }
        }

        foreach (var word in british.Where(word => izeForm.IsMatch(word)))
        {
            var counterpart = izeForm.Replace(word, "is$1");
            if (british.Contains(counterpart) || british.Contains(counterpart.Replace("our", "or", StringComparison.Ordinal)))
            {
                failures.Add($"{word} is an -ize form in the British folder");
            }
        }

        // Assert: no leaks, and the pattern read from the routing script recognizes the derived endings (organizational, organizationally) as well as the verb endings
        Assert.Multiple(
            () => Assert.Empty(failures.Take(MaxReportedFailures)),
            () => Assert.All(
                new[] { "organize", "organizes", "organized", "organizing", "organizer", "organizers", "organization", "organizations", "organizable", "organizability", "organizational", "organizationally" },
                word => Assert.Matches(izeForm, word)),
            () => Assert.DoesNotMatch(izeForm, "organise"));
    }

    /// <summary>
    ///     Reads the -ize ending pattern from the routing script, so the tests use exactly the definition that the word lists were routed with.
    /// </summary>
    /// <param name="root">The word-list folder copied beside the tests.</param>
    /// <returns>The pattern that matches an -ize ending and captures what follows the -iz.</returns>
    private static Regex ReadProductionIzeForm(string root)
    {
        var script = File.ReadAllText(Path.Combine(root, RoutingScriptFileName));
        var match = IzeFormDefinition.Match(script);
        Assert.True(match.Success, "The routing script no longer defines IZE_FORM in the expected form.");
        return new Regex(match.Groups[1].Value, RegexOptions.CultureInvariant);
    }

    /// <summary>
    ///     Test that words built on a regional spelling by affixes stay in the region of that spelling, in the lists and in lookups.
    /// </summary>
    /// <remarks>
    ///     The word unmaneuverable comes from an imported list that SCOWL does not cover, and the spelling
    ///     patterns cannot reach it from manoeuvre, so the routing must recognize it as the American word
    ///     maneuverable with the prefix un. It must be known to American English and unknown to British
    ///     English, and the reverse for unmanoeuvrable. The words are checked in the common, American, and
    ///     British folders, and through the public lookup.
    /// </remarks>
    [Fact]
    public void TextLint_Dictionary_AffixedRegionalWords_StayInTheirOwnRegion()
    {
        // Arrange: the lower-cased words of the three folders, the two selections, and the American word with its British counterpart
        const string americanWord = "unmaneuverable";
        const string britishWord = "unmanoeuvrable";
        var root = Path.Combine(AppContext.BaseDirectory, "dictionaries");
        var common = ReadFolderWords(root, "en-common");
        var american = ReadFolderWords(root, "en-US");
        var british = ReadFolderWords(root, "en-GB");
        var americanSelection = SpellingDictionary.ForLanguages("en-US");
        var britishSelection = SpellingDictionary.ForLanguages("en-GB");
        var failures = new List<string>();

        // Act: check the lists and the lookups, in three letter cases
        if (common.Contains(americanWord) || british.Contains(americanWord) || !american.Contains(americanWord))
        {
            failures.Add($"{americanWord} must be in the American folder only");
        }

        if (common.Contains(britishWord) || american.Contains(britishWord) || !british.Contains(britishWord))
        {
            failures.Add($"{britishWord} must be in the British folder only");
        }

        foreach (var form in new Func<string, string>[] { word => word, Capitalize, UpperCaseLatin1 })
        {
            if (!americanSelection.IsKnown(form(americanWord)) || britishSelection.IsKnown(form(americanWord)))
            {
                failures.Add($"{form(americanWord)} is not American-only");
            }

            if (americanSelection.IsKnown(form(britishWord)) || !britishSelection.IsKnown(form(britishWord)))
            {
                failures.Add($"{form(britishWord)} is not British-only");
            }
        }

        // Assert: neither word crossed into the other region
        Assert.Empty(failures);
    }

    /// <summary>
    ///     Reads the lower-cased words of every list in one word-list folder.
    /// </summary>
    /// <param name="root">The word-list folder copied beside the tests.</param>
    /// <param name="folder">The name of the folder below the root.</param>
    /// <returns>The distinct lower-case words.</returns>
    private static HashSet<string> ReadFolderWords(string root, string folder) =>
        Directory.GetFiles(Path.Combine(root, folder), "*.txt")
            .SelectMany(File.ReadLines)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    ///     Capitalizes the first letter of a word.
    /// </summary>
    /// <param name="word">The non-empty word.</param>
    /// <returns>The word with an upper-case first letter.</returns>
    private static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>
    ///     Test that every word of the reviewed exclusions list is unknown in every language selection, in every letter case.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_EveryExcludedWord_IsUnknown()
    {
        // Arrange: the excluded words and each language selection
        var excluded = ReadExcludedWords(Path.Combine(AppContext.BaseDirectory, "dictionaries"));
        var dictionaries = new[] { "en-US", "en-GB", "en-US,en-GB" }.Select(SpellingDictionary.ForLanguages).ToArray();
        var failures = new List<string>();

        // Act: look up each excluded word as written, capitalized, and in upper case
        foreach (var word in excluded)
        {
            foreach (var form in new[] { word, char.ToUpperInvariant(word[0]) + word[1..], UpperCaseLatin1(word) })
            {
                if (dictionaries.Any(dictionary => dictionary.IsKnown(form)))
                {
                    failures.Add(form);
                }
            }
        }

        // Assert: the exclusions list is not empty and every excluded word is unknown
        Assert.Multiple(
            () => Assert.NotEmpty(excluded),
            () => Assert.Empty(failures.Take(MaxReportedFailures)));
    }

    /// <summary>
    ///     Reads the words of the reviewed exclusions list.
    /// </summary>
    /// <param name="root">The word-list folder copied beside the tests.</param>
    /// <returns>The distinct lower-case excluded words.</returns>
    private static HashSet<string> ReadExcludedWords(string root) =>
        File.ReadLines(Path.Combine(root, ExclusionsFileName))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    ///     Test that lookups from many threads at once all return the correct answers.
    /// </summary>
    [Fact]
    public void TextLint_Dictionary_ParallelLookups_ReturnCorrectAnswers()
    {
        // Arrange: the three selections and the answers each must give for a few words
        var selections = new (SpellingDictionary Dictionary, bool Favorite, bool Favourite)[]
        {
            (SpellingDictionary.ForLanguages("en-US"), true, false),
            (SpellingDictionary.ForLanguages("en-GB"), false, true),
            (SpellingDictionary.ForLanguages("en-US,en-GB"), true, true),
        };
        var failures = new ConcurrentBag<string>();

        // Act: query all the selections from many threads at once
        Parallel.For(0, ParallelLookups, i =>
        {
            var (dictionary, favorite, favourite) = selections[i % selections.Length];
            if (dictionary.IsKnown("favorite") != favorite ||
                dictionary.IsKnown("favourite") != favourite ||
                !dictionary.IsKnown("table") ||
                !dictionary.IsKnown("kubernetes") ||
                dictionary.IsKnown("xyzzyq"))
            {
                failures.Add($"selection {i % selections.Length}");
            }
        });

        // Assert: every answer was right
        Assert.Empty(failures);
    }

    /// <summary>
    ///     Gets the language selections that must know every word of a word list.
    /// </summary>
    /// <param name="relativePath">The path of the word list relative to the word-list folder, with forward slashes.</param>
    /// <returns>
    ///     The selections: the common words must be known in each single-language selection, a regional
    ///     list only in its own language, the programming-language lists and the reviewed list in each
    ///     language, and the general technical lists in the combined selection (a spelling variant the
    ///     builder removes from them is still known where its language is selected).
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown for a list in a folder this test does not know, so a new list cannot be left unchecked.</exception>
    private static string[] SelectionsFor(string relativePath) => relativePath switch
    {
        _ when relativePath.StartsWith("en-common/", StringComparison.Ordinal) => ["en-US", "en-GB"],
        _ when relativePath.StartsWith("en-US/", StringComparison.Ordinal) => ["en-US"],
        _ when relativePath.StartsWith("en-GB/", StringComparison.Ordinal) => ["en-GB"],
        _ when relativePath.StartsWith("technical/language/", StringComparison.Ordinal) => ["en-US", "en-GB"],
        "technical/variant-keep.txt" => ["en-US", "en-GB"],
        _ when relativePath.StartsWith("technical/general/", StringComparison.Ordinal) => ["en-US,en-GB"],
        _ => throw new InvalidOperationException($"The word list '{relativePath}' is not covered by this test."),
    };

    /// <summary>
    ///     Converts the ASCII and Latin-1 letters of a word to upper case without changing any other character.
    /// </summary>
    /// <remarks>
    ///     Culture-aware upper-casing would turn the sharp s into two letters and the letter y with
    ///     diaeresis into a character outside Latin-1, which no dictionary word contains, so the
    ///     conversion is done letter by letter for the letters that have an upper-case partner in Latin-1.
    /// </remarks>
    /// <param name="word">The word to convert.</param>
    /// <returns>The word in upper case.</returns>
    private static string UpperCaseLatin1(string word) =>
        string.Create(word.Length, word, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var character = source[i];
                var lowerLatin1 = character is >= '\u00E0' and <= '\u00FE' && character != '\u00F7';
                span[i] = character is >= 'a' and <= 'z' || lowerLatin1 ? (char)(character - UpperCaseOffset) : character;
            }
        });
}
