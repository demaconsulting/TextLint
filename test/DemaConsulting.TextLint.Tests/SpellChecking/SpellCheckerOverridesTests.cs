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

// cspell:ignore wrold speling colour

using DemaConsulting.TextLint.Configuration;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.SpellChecking;
using DemaConsulting.TextLint.TextScanning;
using DemaConsulting.TextLint.Tests.TestSupport;

namespace DemaConsulting.TextLint.Tests.SpellChecking;

/// <summary>
///     Unit tests for the way <see cref="SpellChecker"/> applies <c>overrides</c> and <c>enabled</c>, and for the
///     <see cref="SpellCheckNote"/> type.
/// </summary>
/// <remarks>
///     The checker is tested with real temporary directories and its documented dependencies real: the settings
///     value, the override resolver, the file selector, the file reader, and the text checker. Words are chosen
///     to be unambiguous: <c>wrold</c> and <c>speling</c> are misspellings that no dictionary holds, and
///     <c>colour</c> is a word only the British dictionary holds.
/// </remarks>
public class SpellCheckerOverridesTests
{
    /// <summary>
    ///     A misspelled word that is unknown to every dictionary.
    /// </summary>
    private const string Unknown = "wrold";

    /// <summary>
    ///     A second misspelled word that is unknown to every dictionary.
    /// </summary>
    private const string OtherUnknown = "speling";

    /// <summary>
    ///     A word that only the British dictionary accepts.
    /// </summary>
    private const string British = "colour";

    /// <summary>
    ///     The path that the tests use for a settings file in notes.
    /// </summary>
    private const string SettingsFile = ".textlint.yaml";

    /// <summary>
    ///     The line that the tests give an override.
    /// </summary>
    private const int OverrideLine = 14;

    /// <summary>
    ///     The column that the tests give an override.
    /// </summary>
    private const int OverrideColumn = 5;

    /// <summary>
    ///     Creates settings that select markdown files and hold the given overrides.
    /// </summary>
    /// <param name="overrides">The overrides.</param>
    /// <returns>The settings.</returns>
    private static TextLintSettings WithOverrides(params SettingsOverride[] overrides) =>
        new() { Files = ["**/*.md"], Overrides = overrides };

    /// <summary>
    ///     Runs a check in a temporary directory.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="root">The directory to check.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The result.</returns>
    private static Task<SpellCheckResult> CheckAsync(TextLintSettings settings, string root, SpellCheckOptions? options = null) =>
        SpellChecker.CheckAsync(settings, root, options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Gets the files that have findings.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The distinct file paths of the findings, in result order.</returns>
    private static string[] FilesWithIssues(SpellCheckResult result) =>
        [.. result.Issues.Select(issue => issue.FilePath!).Distinct(StringComparer.Ordinal)];

    /// <summary>
    ///     Test that an override applies only to the files it matches.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_OverrideWords_ApplyOnlyToMatchingFiles()
    {
        // Arrange: two files with the same misspelling, and an override that accepts it for docs
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", Unknown);
        temp.CreateFile("src/a.md", Unknown);
        var settings = WithOverrides(new SettingsOverride { Filename = ["docs/**"], Words = [Unknown] });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: only the file outside docs has a finding
        Assert.Equal(["src/a.md"], FilesWithIssues(result));
    }

    /// <summary>
    ///     Test that list settings of an override add to the top-level lists.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_OverrideList_AddsToTopLevelList()
    {
        // Arrange: a top-level word, an override word for docs, and a file using both
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", $"{Unknown} {OtherUnknown}");
        temp.CreateFile("src/a.md", $"{Unknown} {OtherUnknown}");
        var settings = WithOverrides(new SettingsOverride { Filename = ["docs/**"], Words = [OtherUnknown] }) with { Words = [Unknown] };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: docs accepts both words, and the other file only the top-level one
        Assert.Multiple(
            () => Assert.Equal(["src/a.md"], FilesWithIssues(result)),
            () => Assert.Equal([OtherUnknown], result.Issues.Select(issue => issue.Text)));
    }

    /// <summary>
    ///     Test that overrides apply in the order written, so a later one can undo the effect of an earlier one.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_SeveralOverrides_ApplyInOrderWrittenWithLastSingleValueWinning()
    {
        // Arrange: a general override that raises the minimum word length so nothing is checked, and a specific
        // one for docs that lowers it again
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", Unknown);
        temp.CreateFile("src/a.md", Unknown);
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["**/*.md"], MinWordLength = 20 },
            new SettingsOverride { Filename = ["docs/**"], MinWordLength = 4 });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: the later, specific override wins for docs, and the general one applies elsewhere
        Assert.Equal(["docs/a.md"], FilesWithIssues(result));
    }

    /// <summary>
    ///     Test that an override can use a predefined pattern, a top-level pattern, and its own pattern.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_OverridePatterns_ResolveTopLevelOwnAndPredefinedNames()
    {
        // Arrange: a top-level pattern, an override that uses it, a predefined name, and its own pattern
        using var temp = new TempDirectory();
        temp.CreateFile("a/x.md", $"{Unknown} {OtherUnknown} http://wrold.example.com/speling");
        temp.CreateFile("b/x.md", $"{Unknown} {OtherUnknown}");
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["a/**"], IgnoreRegExpList = ["Top", "Urls", "Own"], Patterns = [new NamedPattern("Own", "/speling/")] })
            with { Patterns = [new NamedPattern("Top", "/wrold/")] };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: everything is ignored in a, and both words are reported in b
        Assert.Multiple(
            () => Assert.Equal(["b/x.md"], FilesWithIssues(result)),
            () => Assert.Equal(2, result.Issues.Count));
    }

    /// <summary>
    ///     Test that an override's pattern replaces a top-level pattern of the same name for its files only.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_OverridePatternWithTopLevelName_ReplacesItForMatchingFiles()
    {
        // Arrange: a top-level pattern named Mine that ignores one word, used by the top-level list, and an
        // override for docs that defines Mine to ignore the other word
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", $"{Unknown} {OtherUnknown}");
        temp.CreateFile("src/a.md", $"{Unknown} {OtherUnknown}");
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["docs/**"], Patterns = [new NamedPattern("Mine", "/speling/")], Enabled = null })
            with { Patterns = [new NamedPattern("Mine", "/wrold/")], IgnoreRegExpList = ["Mine"] };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: docs ignores the override's word and reports the other, and the other file the reverse
        Assert.Multiple(
            () => Assert.Equal(Unknown, result.Issues.Single(issue => issue.FilePath == "docs/a.md").Text),
            () => Assert.Equal(OtherUnknown, result.Issues.Single(issue => issue.FilePath == "src/a.md").Text));
    }

    /// <summary>
    ///     Test that a disabled file is not read, reported, or counted.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_DisabledOverride_FileIsNotReadReportedOrCounted()
    {
        // Arrange: a file with a misspelling, a binary file that would be reported as skipped if it were read,
        // both disabled by an override, and an enabled file with a misspelling
        using var temp = new TempDirectory();
        temp.CreateFile("legacy/a.md", Unknown);
        await File.WriteAllBytesAsync(Path.Combine(temp.FullPath, "legacy", "b.md"), [0x00, 0x01, 0x02], TestContext.Current.CancellationToken);
        temp.CreateFile("src/a.md", Unknown);
        var settings = WithOverrides(new SettingsOverride { Filename = ["legacy/**"], Enabled = false });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: only the enabled file is checked, and the disabled ones are counted as disabled
        Assert.Multiple(
            () => Assert.Equal(["src/a.md"], FilesWithIssues(result)),
            () => Assert.Equal(1, result.FilesChecked),
            () => Assert.Equal(0, result.FilesSkipped),
            () => Assert.Empty(result.FileDiagnostics),
            () => Assert.Equal(2, result.FilesDisabled),
            () => Assert.DoesNotContain(result.Notes, note => note.Kind == SpellCheckNoteKind.AllFilesDisabled));
    }

    /// <summary>
    ///     Test that the last override that sets enabled wins, so a later override can enable files again.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_DisabledThenEnabledOverride_LastWins()
    {
        // Arrange: a directory is disabled and one of its subdirectories enabled again
        using var temp = new TempDirectory();
        temp.CreateFile("legacy/a.md", Unknown);
        temp.CreateFile("legacy/new/a.md", Unknown);
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["legacy/**"], Enabled = false },
            new SettingsOverride { Filename = ["legacy/new/**"], Enabled = true });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: only the re-enabled file is checked
        Assert.Multiple(
            () => Assert.Equal(["legacy/new/a.md"], FilesWithIssues(result)),
            () => Assert.Equal(1, result.FilesDisabled));
    }

    /// <summary>
    ///     Test that files are disabled at the top level, and that an override can enable some of them.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_TopLevelDisabledWithEnablingOverride_ChecksOnlyEnabledFiles()
    {
        // Arrange: everything disabled, but docs enabled again by an override
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", Unknown);
        temp.CreateFile("src/a.md", Unknown);
        var settings = WithOverrides(new SettingsOverride { Filename = ["docs/**"], Enabled = true }) with { Enabled = false };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: only docs is checked
        Assert.Multiple(
            () => Assert.Equal(["docs/a.md"], FilesWithIssues(result)),
            () => Assert.Equal(1, result.FilesDisabled));
    }

    /// <summary>
    ///     Test that a run whose selected files are all disabled succeeds and says so.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_AllFilesDisabled_ReportsNoteAndNoProblem()
    {
        // Arrange: spell checking is switched off at the top level
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("b.md", Unknown);
        var settings = new TextLintSettings { Files = ["**/*.md"], Enabled = false };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: nothing is checked, nothing is a problem, and the note says how many files were disabled
        var note = Assert.Single(result.Notes);
        Assert.Multiple(
            () => Assert.Equal(2, result.FilesDisabled),
            () => Assert.Equal(0, result.FilesChecked),
            () => Assert.False(result.HasProblems),
            () => Assert.Equal(SpellCheckNoteKind.AllFilesDisabled, note.Kind),
            () => Assert.Equal(SpellCheckNoteSeverity.Info, note.Severity),
            () => Assert.Equal("info: Spell checking is disabled by the settings for all 2 selected file(s).", note.Format()));
    }

    /// <summary>
    ///     Test that a run that selects nothing has no files disabled and no notes.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NothingSelected_HasNoDisabledFilesOrNotes()
    {
        // Arrange: settings that select nothing in a directory, with an override that could never match
        using var temp = new TempDirectory();
        temp.CreateFile("a.txt", Unknown);
        var settings = WithOverrides(new SettingsOverride { Filename = ["docs/**"], Words = [Unknown] }) with { Enabled = false };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: no file was selected, so none is disabled, and the unmatched override is not reported
        Assert.Multiple(
            () => Assert.Equal(0, result.FilesDisabled),
            () => Assert.Empty(result.Notes));
    }

    /// <summary>
    ///     Test that a language from the options wins over an override and over the top level, and that the
    ///     replacement is noted.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_LanguageOption_WinsOverOverrideAndIsNoted()
    {
        // Arrange: an override that selects British English for docs, and a file with a British word there
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", British);
        var settings = WithOverrides(new SettingsOverride
        {
            Filename = ["docs/**"],
            Language = "en-GB",
            SourceName = SettingsFile,
            Line = OverrideLine,
            Column = OverrideColumn,
        });

        // Act: check without and with the language option
        var without = await CheckAsync(settings, temp.FullPath);
        var with = await CheckAsync(settings, temp.FullPath, new SpellCheckOptions { Language = "en-US" });

        // Assert: the override makes the British word acceptable, the option wins and flags it, and a note says so
        var note = Assert.Single(with.Notes);
        Assert.Multiple(
            () => Assert.Empty(without.Issues),
            () => Assert.Empty(without.Notes),
            () => Assert.Single(with.Issues),
            () => Assert.Equal(SpellCheckNoteKind.LanguageReplaced, note.Kind),
            () => Assert.Equal(
                "info: --language en-US replaces en-GB for the override at .textlint.yaml(14,5).",
                note.Format()));
    }

    /// <summary>
    ///     Test that a language from the options wins over the top-level language for files no override matches.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_LanguageOption_WinsOverTopLevelLanguage()
    {
        // Arrange: a top-level British language and a British word
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", British);
        var settings = new TextLintSettings { Files = ["**/*.md"], Language = "en-GB" };

        // Act: check with American English
        var result = await CheckAsync(settings, temp.FullPath, new SpellCheckOptions { Language = "en-US" });

        // Assert: the word is flagged, and there is nothing to note because no override was replaced
        Assert.Multiple(
            () => Assert.Single(result.Issues),
            () => Assert.Empty(result.Notes));
    }

    /// <summary>
    ///     Test that no language note is made when the dictionaries are the same, or the override matched no checked file.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_LanguageOptionSameDictionaryOrUnusedOverride_HasNoLanguageNote()
    {
        // Arrange: an override whose language names the same dictionary as the option (en and en-US), an
        // override with a different language that matches only a disabled file, and one that matches nothing
        using var temp = new TempDirectory();
        temp.CreateFile("same/a.md", "Hello");
        temp.CreateFile("off/a.md", "Hello");
        temp.CreateFile("other/a.md", "Hello");
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["same/**"], Language = "en" },
            new SettingsOverride { Filename = ["off/**"], Language = "en-GB" },
            new SettingsOverride { Filename = ["off/**"], Enabled = false },
            new SettingsOverride { Filename = ["nothing/**"], Language = "en-GB" });

        // Act: check with American English
        var result = await CheckAsync(settings, temp.FullPath, new SpellCheckOptions { Language = "en-US" });

        // Assert: no note about a language, only the warning about the override that matched nothing
        Assert.Multiple(
            () => Assert.DoesNotContain(result.Notes, note => note.Kind == SpellCheckNoteKind.LanguageReplaced),
            () => Assert.Equal([3], result.Notes.Where(note => note.Kind == SpellCheckNoteKind.OverrideUnmatched).Select(note => note.Line ?? 3)));
    }

    /// <summary>
    ///     Test that an unsupported language in the options is reported before any file is touched.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_UnsupportedLanguageOption_ThrowsBeforeTouchingFiles()
    {
        // Arrange: a root that does not exist, and a language that is not supported
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: the language is reported, not the missing directory
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => CheckAsync(WithOverrides(), missing, new SpellCheckOptions { Language = "klingon" }));
        Assert.Contains("klingon", exception.Message);
    }

    /// <summary>
    ///     Test that overrides are matched against the settings root and not against the search directory.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_SettingsRootDifferentFromSearchRoot_MatchesRelativeToSettingsRoot()
    {
        // Arrange: files under docs, searched from the docs directory, with an override for docs/** that is
        // relative to the settings root above it
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", Unknown);
        var settings = WithOverrides(new SettingsOverride { Filename = ["docs/**"], Words = [Unknown] });
        var docs = Path.Combine(temp.FullPath, "docs");

        // Act: check from docs with and without naming the settings root
        var withRoot = await CheckAsync(settings, docs, new SpellCheckOptions { SettingsRootDirectory = temp.FullPath });
        var withoutRoot = await CheckAsync(settings, docs);

        // Assert: the override applies only when the settings root is given
        Assert.Multiple(
            () => Assert.Empty(withRoot.Issues),
            () => Assert.Single(withoutRoot.Issues));
    }

    /// <summary>
    ///     Test that a file outside the settings root matches no override but follows the top-level settings.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_FileOutsideSettingsRoot_MatchesNoOverride()
    {
        // Arrange: a file in one directory, and a settings root in a sibling directory, with an override for everything
        using var temp = new TempDirectory();
        temp.CreateFile("files/a.md", Unknown);
        temp.CreateFile("settings/placeholder.txt");
        var settings = WithOverrides(new SettingsOverride { Filename = ["**"], Words = [Unknown] });

        // Act: check the files directory with the settings root elsewhere
        var result = await CheckAsync(
            settings,
            Path.Combine(temp.FullPath, "files"),
            new SpellCheckOptions { SettingsRootDirectory = Path.Combine(temp.FullPath, "settings") });

        // Assert: the override did not apply, so the word is reported, and the override is reported as unmatched
        Assert.Multiple(
            () => Assert.Single(result.Issues),
            () => Assert.Single(result.Notes, note => note.Kind == SpellCheckNoteKind.OverrideUnmatched));
    }

    /// <summary>
    ///     Test that an override that matches none of the selected files is a warning with its position, and that
    ///     an override that only disables files counts as matched.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_UnmatchedOverride_IsWarnedAboutWithPosition()
    {
        // Arrange: one override that matches nothing, and one that matches a file only to disable it
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        temp.CreateFile("legacy/b.md", Unknown);
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["missing/**"], Words = [Unknown], SourceName = SettingsFile, Line = OverrideLine, Column = OverrideColumn },
            new SettingsOverride { Filename = ["legacy/**"], Enabled = false, SourceName = SettingsFile, Line = 20, Column = OverrideColumn });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: one warning, for the first override, and the run still has its finding but no extra problem
        var note = Assert.Single(result.Notes);
        Assert.Multiple(
            () => Assert.Equal(SpellCheckNoteKind.OverrideUnmatched, note.Kind),
            () => Assert.Equal(SpellCheckNoteSeverity.Warning, note.Severity),
            () => Assert.Equal(".textlint.yaml(14,5): warning: The override matches none of the selected files.", note.Format()),
            () => Assert.Equal(SettingsFile, note.Source),
            () => Assert.Equal(OverrideLine, note.Line),
            () => Assert.Equal(OverrideColumn, note.Column),
            () => Assert.Single(result.Issues));
    }

    /// <summary>
    ///     Test that the notes come in a fixed order: information first, then warnings.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_SeveralNotes_AreOrderedInformationThenWarnings()
    {
        // Arrange: a language replacement, all files disabled, and two unmatched overrides
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello");
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["none1/**"], Words = [Unknown] },
            new SettingsOverride { Filename = ["a.md"], Language = "en-GB", SourceName = SettingsFile, Line = 5, Column = OverrideColumn },
            new SettingsOverride { Filename = ["none2/**"], Words = [Unknown] }) with { Enabled = false };

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath, new SpellCheckOptions { Language = "en-US" });

        // Assert: the disabled file means no language note (the override matched no checked file); the info note
        // comes before the warnings, which are in the order of the overrides
        Assert.Equal(
            [SpellCheckNoteKind.AllFilesDisabled, SpellCheckNoteKind.OverrideUnmatched, SpellCheckNoteKind.OverrideUnmatched],
            result.Notes.Select(note => note.Kind));
    }

    /// <summary>
    ///     Test that a language note names the override by number when it has no position.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_LanguageNoteForOverrideBuiltInCode_NamesOverrideByNumber()
    {
        // Arrange: an override with no position
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", "Hello");
        var settings = WithOverrides(new SettingsOverride { Filename = ["**"], Language = "en-GB" });

        // Act: check with American English
        var result = await CheckAsync(settings, temp.FullPath, new SpellCheckOptions { Language = "en-US" });

        // Assert: the note says which override by its number
        Assert.Equal("--language en-US replaces en-GB for the override at number 1.", Assert.Single(result.Notes).Message);
    }

    /// <summary>
    ///     Test that unusable overrides are rejected before any file or directory is touched, even when unmatched.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_UnusableOverride_ThrowsBeforeTouchingFiles()
    {
        // Arrange: a root that does not exist
        using var temp = new TempDirectory();
        var missing = Path.Combine(temp.FullPath, "missing");

        // Act / Assert: a bad word entry, a bad pattern name, a bad language, a bad glob, and a missing filename are
        // each reported, not the missing directory
        var word = await Assert.ThrowsAsync<ArgumentException>(
            () => CheckAsync(WithOverrides(new SettingsOverride { Filename = ["x/**"], IgnoreWords = [" "] }), missing));
        var name = await Assert.ThrowsAsync<InvalidPatternException>(
            () => CheckAsync(WithOverrides(new SettingsOverride { Filename = ["x/**"], IgnoreRegExpList = ["NoSuchPattern"] }), missing));
        var language = await Assert.ThrowsAsync<ArgumentException>(
            () => CheckAsync(WithOverrides(new SettingsOverride { Filename = ["x/**"], Language = "klingon" }), missing));
        var glob = await Assert.ThrowsAsync<InvalidGlobPatternException>(
            () => CheckAsync(WithOverrides(new SettingsOverride { Filename = ["x?"], Words = [Unknown] }), missing));
        var filename = await Assert.ThrowsAsync<ArgumentException>(
            () => CheckAsync(WithOverrides(new SettingsOverride { Words = [Unknown] }), missing));
        Assert.Multiple(
            () => Assert.Contains("'ignoreWords'", word.Message),
            () => Assert.Contains("NoSuchPattern", name.Message),
            () => Assert.Contains("klingon", language.Message),
            () => Assert.Equal("x?", glob.Pattern),
            () => Assert.Contains("Override 1", filename.Message));
    }

    /// <summary>
    ///     Test that a pattern name defined only by another override is unknown to an override.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_PatternNameOfAnotherOverride_IsUnknown()
    {
        // Arrange: the first override defines Mine, the second uses it
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["x/**"], Patterns = [new NamedPattern("Mine", "/x/")] },
            new SettingsOverride { Filename = ["**"], IgnoreRegExpList = ["Mine"] });

        // Act / Assert: the second override cannot see the definition
        var exception = await Assert.ThrowsAsync<InvalidPatternException>(() => CheckAsync(settings, temp.FullPath));
        Assert.Contains("Mine", exception.Message);
    }

    /// <summary>
    ///     Test that two overrides that matched the same file combine their settings.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_TwoOverridesMatchSameFile_CombineTheirLists()
    {
        // Arrange: each override accepts one of the words, and one file uses both
        using var temp = new TempDirectory();
        temp.CreateFile("docs/a.md", $"{Unknown} {OtherUnknown}");
        var settings = WithOverrides(
            new SettingsOverride { Filename = ["**/*.md"], Words = [Unknown] },
            new SettingsOverride { Filename = ["docs/**"], Words = [OtherUnknown] });

        // Act: check
        var result = await CheckAsync(settings, temp.FullPath);

        // Assert: both words are accepted
        Assert.Empty(result.Issues);
    }

    /// <summary>
    ///     Test that the result matches a run without overrides when the settings have none.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_NoOverrides_HasNoDisabledFilesOrNotes()
    {
        // Arrange: a file with a finding and settings without overrides
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);

        // Act: check
        var result = await CheckAsync(WithOverrides(), temp.FullPath);

        // Assert: the finding is reported, and nothing else is added
        Assert.Multiple(
            () => Assert.Single(result.Issues),
            () => Assert.Equal(0, result.FilesDisabled),
            () => Assert.Empty(result.Notes));
    }

    /// <summary>
    ///     Test that the notes of a result cannot be changed by casting them to a mutable interface.
    /// </summary>
    [Fact]
    public async Task SpellChecker_CheckAsync_Notes_AreImmutable()
    {
        // Arrange: a result with a note
        using var temp = new TempDirectory();
        temp.CreateFile("a.md", Unknown);
        var result = await CheckAsync(new TextLintSettings { Files = ["**/*.md"], Enabled = false }, temp.FullPath);

        // Act / Assert: the list is not a mutable collection
        var list = Assert.IsType<IList<SpellCheckNote>>(result.Notes, exactMatch: false);
        Assert.Throws<NotSupportedException>(() => list.Clear());
    }

    /// <summary>
    ///     Test that a result copies the notes it is given.
    /// </summary>
    [Fact]
    public void SpellChecker_Result_CallerNotes_AreCopied()
    {
        // Arrange: a result created with a list of notes
        var notes = new List<SpellCheckNote> { new(SpellCheckNoteKind.AllFilesDisabled, "All disabled.", null, null, null) };
        var result = new SpellCheckResult([], [], 0, 0, 1, notes);

        // Act: change the caller's list, and derive a result with run information
        notes.Clear();
        var derived = result.WithRunInfo(3, [new SpellCheckNote(SpellCheckNoteKind.AllFilesDisabled, "Again.", null, null, null)]);

        // Assert: the original is unchanged, and the derived one has the new information
        Assert.Multiple(
            () => Assert.Single(result.Notes),
            () => Assert.Equal(1, result.FilesDisabled),
            () => Assert.Equal(3, derived.FilesDisabled),
            () => Assert.Equal("Again.", Assert.Single(derived.Notes).Message));
    }

    /// <summary>
    ///     Test how a note is formatted for each severity and for locations that are partly unknown.
    /// </summary>
    [Fact]
    public void SpellCheckNote_Format_SeveritiesAndLocations_FollowCompilerStyle()
    {
        // Arrange: an information note, and warnings with a full, a file-only, and no location
        var info = new SpellCheckNote(SpellCheckNoteKind.LanguageReplaced, "Replaced.", SettingsFile, 1, 2);
        var full = new SpellCheckNote(SpellCheckNoteKind.OverrideUnmatched, "Unmatched.", SettingsFile, 3, 4);
        var fileOnly = new SpellCheckNote(SpellCheckNoteKind.OverrideUnmatched, "Unmatched.", SettingsFile, null, null);
        var none = new SpellCheckNote(SpellCheckNoteKind.OverrideUnmatched, "Unmatched.", null, null, null);

        // Act / Assert: information has no location, and warnings show what is known
        Assert.Multiple(
            () => Assert.Equal("info: Replaced.", info.Format()),
            () => Assert.Equal(SpellCheckNoteSeverity.Info, info.Severity),
            () => Assert.Equal(".textlint.yaml(3,4): warning: Unmatched.", full.Format()),
            () => Assert.Equal(".textlint.yaml: warning: Unmatched.", fileOnly.Format()),
            () => Assert.Equal("warning: Unmatched.", none.Format()),
            () => Assert.Equal(full.Format(), full.ToString()));
    }
}
