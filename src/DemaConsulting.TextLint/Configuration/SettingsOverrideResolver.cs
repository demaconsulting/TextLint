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

using System.Collections.Concurrent;
using System.Collections.Immutable;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     Works out which overrides apply to a file and what the settings are for it.
/// </summary>
/// <remarks>
///     This is the one place that gives the <c>overrides</c> setting its meaning, so the spell checker only has
///     to ask. Every override's <c>filename</c> patterns are compiled once, when the resolver is created, as an
///     <see cref="OrderedGlobList"/> (so <c>!</c> and last-match-wins work as in <c>files</c>, and matching is
///     case-sensitive). For a file, every override that matches applies, in the order written: general rules
///     first and specific ones last. List settings (<c>words</c>, <c>ignoreWords</c>, <c>flagWords</c>,
///     <c>ignoreRegExpList</c>) add to the top-level values and to those of earlier overrides; single values
///     (<c>language</c>, <c>minWordLength</c>, <c>ignoreRandomStrings</c>, <c>minRandomLength</c>,
///     <c>enabled</c>) are replaced by the last matching override that sets them. Named patterns are merged
///     by name, so an override's definition replaces a definition of the same name that came before, and the
///     names an <c>ignoreRegExpList</c> entry uses are resolved afterwards by the checker, from the merged
///     definitions. A language given on the command line wins over every override and over the top-level
///     language. The merged result is an ordinary <see cref="TextLintSettings"/>, so the text checker needs to
///     know nothing about overrides. The resolver does no file system access. It is thread-safe once created;
///     the merged settings of each combination of overrides are cached so that a combination is only built once.
/// </remarks>
internal sealed class SettingsOverrideResolver
{
    /// <summary>
    ///     The resolution of a file that no override matches, which is only valid for settings with no override.
    /// </summary>
    private static readonly ImmutableArray<int> NoOverrides = [];

    /// <summary>
    ///     The settings the overrides refine.
    /// </summary>
    private readonly TextLintSettings _settings;

    /// <summary>
    ///     The language given on the command line, which wins over every other language; null when none was given.
    /// </summary>
    private readonly string? _languageOverride;

    /// <summary>
    ///     The compiled <c>filename</c> patterns, one list per override, in the order of the overrides.
    /// </summary>
    private readonly OrderedGlobList[] _filenames;

    /// <summary>
    ///     The merged settings of each combination of overrides, by the key of the combination.
    /// </summary>
    private readonly ConcurrentDictionary<string, TextLintSettings> _effective = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsOverrideResolver"/> class.
    /// </summary>
    /// <remarks>
    ///     Compiling here, and not when a file is resolved, means an unusable override fails before any file is
    ///     touched, with the same exceptions as an unusable top-level setting. The languages of the overrides are
    ///     checked here as well, because a command-line language hides them from the checker, and an override
    ///     with a language that does not exist must still be reported. Side effects: none.
    /// </remarks>
    /// <param name="settings">The settings and their overrides. Must not be null.</param>
    /// <param name="languageOverride">
    ///     The language from the command line, which wins over every override and over the top-level language;
    ///     null when none was given.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when an override has no <c>filename</c> pattern, when a language is not supported, or (as the
    ///     derived <see cref="InvalidGlobPatternException"/>) when a pattern is unsupported or malformed.
    /// </exception>
    internal SettingsOverrideResolver(TextLintSettings settings, string? languageOverride)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _languageOverride = languageOverride;

        // Compile the patterns of every override now, so a bad one is reported before any file is read
        _filenames = new OrderedGlobList[settings.Overrides.Count];
        for (var index = 0; index < _filenames.Length; index++)
        {
            var settingsOverride = settings.Overrides[index];
            if (settingsOverride.Filename.Count == 0)
            {
                throw new ArgumentException($"Override {index + 1} has no 'filename' pattern, so it can apply to no file.", nameof(settings));
            }

            // A language that does not exist is a defect of the override even when the command line hides it
            if (settingsOverride.Language is { } language)
            {
                _ = SpellingDictionary.ForLanguages(language);
            }

            _filenames[index] = OrderedGlobList.Parse(settingsOverride.Filename, settings.EnableGlobDot);
        }
    }

    /// <summary>
    ///     Gets the number of overrides.
    /// </summary>
    /// <value>The count; zero when the settings have no overrides.</value>
    internal int OverrideCount => _filenames.Length;

    /// <summary>
    ///     Gets the override at an index.
    /// </summary>
    /// <remarks>
    ///     Thread-safe.
    /// </remarks>
    /// <param name="index">The zero-based index, from 0 to <see cref="OverrideCount"/> minus one.</param>
    /// <returns>The override as it appears in the settings.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    internal SettingsOverride GetOverride(int index)
    {
        // An out-of-range index is a programming error; the array would otherwise report it as an unrelated exception type
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _filenames.Length);
        return _settings.Overrides[index];
    }

    /// <summary>
    ///     Finds the overrides that match a file, and whether the file is checked at all.
    /// </summary>
    /// <remarks>
    ///     Matching uses the path relative to the settings root, so the same file is matched the same way
    ///     whatever directory the file selection started from. The decision whether the file is enabled follows
    ///     the setting's rule: the last matching override that sets <c>enabled</c> wins, otherwise the
    ///     top-level value. Pure and thread-safe.
    /// </remarks>
    /// <param name="settingsRelativePath">The path relative to the settings root, using forward slashes. Must not be null.</param>
    /// <returns>The resolution; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settingsRelativePath"/> is null.</exception>
    internal FileResolution Resolve(string settingsRelativePath)
    {
        ArgumentNullException.ThrowIfNull(settingsRelativePath);

        // Collect the matching overrides in the order written, and the enabled flag the last one that sets it gives
        var enabled = _settings.Enabled;
        var matched = ImmutableArray.CreateBuilder<int>();
        for (var index = 0; index < _filenames.Length; index++)
        {
            if (!_filenames[index].IsMatch(settingsRelativePath))
            {
                continue;
            }

            matched.Add(index);
            enabled = _settings.Overrides[index].Enabled ?? enabled;
        }

        return CreateResolution(enabled, matched.ToImmutable());
    }

    /// <summary>
    ///     Creates the resolution of a file that is outside the settings root, to which no override applies.
    /// </summary>
    /// <remarks>
    ///     Only the top-level <c>enabled</c> setting still decides. Pure and thread-safe.
    /// </remarks>
    /// <returns>The resolution; never null.</returns>
    internal FileResolution ResolveUnmatched() => CreateResolution(_settings.Enabled, NoOverrides);

    /// <summary>
    ///     Gets the merged settings for a file's resolution.
    /// </summary>
    /// <remarks>
    ///     Only overrides that carry settings for the checker take part, so a file matched only by overrides that
    ///     merely enable or disable it shares the checker of the top-level settings. The merged settings of
    ///     each combination are built once. Thread-safe.
    /// </remarks>
    /// <param name="resolution">The resolution of the file. Must not be null.</param>
    /// <returns>The effective settings; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolution"/> is null.</exception>
    internal TextLintSettings GetEffectiveSettings(FileResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return _effective.GetOrAdd(resolution.SettingsKey, _ => Merge(resolution.SettingsOverrides));
    }

    /// <summary>
    ///     Gets the effective settings for a file that only one override matches.
    /// </summary>
    /// <remarks>
    ///     Used to build a checker for every override before any file is read, which validates the override's
    ///     words, patterns, and language even when no file matches it. Thread-safe.
    /// </remarks>
    /// <param name="index">The zero-based index of the override.</param>
    /// <returns>The effective settings; never null.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    internal TextLintSettings GetEffectiveSettingsForOverride(int index)
    {
        _ = GetOverride(index);
        return GetEffectiveSettings(CreateResolution(_settings.Enabled, [index]));
    }

    /// <summary>
    ///     Determines whether an override has anything for the text checker.
    /// </summary>
    /// <remarks>
    ///     An override that only sets <c>enabled</c> changes no checker, so it needs none. Thread-safe.
    /// </remarks>
    /// <param name="index">The zero-based index of the override.</param>
    /// <returns><see langword="true"/> when the override sets a word list, a pattern, a language, or a number or switch.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    internal bool HasCheckerSettings(int index) => HasCheckerSettings(GetOverride(index));

    /// <summary>
    ///     Creates a resolution, working out the key of the checker settings it needs.
    /// </summary>
    /// <remarks>
    ///     Stateless apart from reading the overrides; thread-safe.
    /// </remarks>
    /// <param name="enabled">Whether the file is checked.</param>
    /// <param name="matched">The indices of the matching overrides, ascending. Must not be default.</param>
    /// <returns>The resolution.</returns>
    private FileResolution CreateResolution(bool enabled, ImmutableArray<int> matched)
    {
        var withSettings = matched.Where(index => HasCheckerSettings(_settings.Overrides[index])).ToImmutableArray();
        return new FileResolution(enabled, matched, withSettings);
    }

    /// <summary>
    ///     Determines whether an override sets anything the text checker uses.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="settingsOverride">The override to test. Must not be null.</param>
    /// <returns><see langword="true"/> when some checker setting is present.</returns>
    private static bool HasCheckerSettings(SettingsOverride settingsOverride) =>
        settingsOverride.Words.Count > 0 ||
        settingsOverride.IgnoreWords.Count > 0 ||
        settingsOverride.FlagWords.Count > 0 ||
        settingsOverride.IgnoreRegExpList.Count > 0 ||
        settingsOverride.Patterns.Count > 0 ||
        settingsOverride.Language is not null ||
        settingsOverride.MinWordLength is not null ||
        settingsOverride.IgnoreRandomStrings is not null ||
        settingsOverride.MinRandomLength is not null;

    /// <summary>
    ///     Merges the top-level settings with the given overrides, which apply in the order given.
    /// </summary>
    /// <remarks>
    ///     Lists are concatenated, named patterns are merged by name with a later definition replacing an earlier
    ///     one, and each single value is taken from the last override that sets it. The command-line language is
    ///     applied last and wins. The result carries no overrides, because they have been applied; the settings
    ///     that select files and apply to the whole run are copied unchanged. Stateless apart from reading the
    ///     settings; thread-safe.
    /// </remarks>
    /// <param name="overrideIndexes">The indices of the overrides to apply, ascending. Must not be default.</param>
    /// <returns>The merged settings.</returns>
    private TextLintSettings Merge(ImmutableArray<int> overrideIndexes)
    {
        var applied = overrideIndexes.Select(index => _settings.Overrides[index]).ToList();

        // Lists add up: the top-level values first, then each override's, so specific rules come last
        var patterns = new List<NamedPattern>(_settings.Patterns);
        foreach (var definitions in applied.Select(static item => item.Patterns))
        {
            // A definition replaces any earlier one of the same name, so a name has exactly one definition
            patterns.RemoveAll(existing => definitions.Any(pattern => string.Equals(existing.Name, pattern.Name, StringComparison.OrdinalIgnoreCase)));
            patterns.AddRange(definitions);
        }

        // Single values come from the last override that sets them; the command line wins for the language
        var language = _languageOverride ?? applied.Select(static item => item.Language).LastOrDefault(static value => value is not null);
        return _settings with
        {
            Words = [.. _settings.Words, .. applied.SelectMany(static item => item.Words)],
            IgnoreWords = [.. _settings.IgnoreWords, .. applied.SelectMany(static item => item.IgnoreWords)],
            FlagWords = [.. _settings.FlagWords, .. applied.SelectMany(static item => item.FlagWords)],
            IgnoreRegExpList = [.. _settings.IgnoreRegExpList, .. applied.SelectMany(static item => item.IgnoreRegExpList)],
            Patterns = patterns,
            Language = language ?? _settings.Language,
            MinWordLength = applied.Select(static item => item.MinWordLength).LastOrDefault(static value => value is not null) ?? _settings.MinWordLength,
            IgnoreRandomStrings = applied.Select(static item => item.IgnoreRandomStrings).LastOrDefault(static value => value is not null) ?? _settings.IgnoreRandomStrings,
            MinRandomLength = applied.Select(static item => item.MinRandomLength).LastOrDefault(static value => value is not null) ?? _settings.MinRandomLength,
            Overrides = [],
        };
    }
}
