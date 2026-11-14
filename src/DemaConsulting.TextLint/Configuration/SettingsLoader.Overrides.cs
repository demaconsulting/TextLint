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
using DemaConsulting.TextLint.TextScanning;
using YamlDotNet.RepresentationModel;

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     The part of <see cref="SettingsLoader"/> that reads the <c>overrides</c> setting.
/// </summary>
/// <remarks>
///     Kept in its own file because the loader is already large and the overrides are a self-contained
///     feature: a list of mappings, each with a <c>filename</c> and a subset of the top-level settings. Every
///     value is read by the same readers as its top-level counterpart, so a word list entry, a pattern, a
///     language, a number, or a boolean is validated identically in both places; only the keys that make no
///     sense in an override are handled here, each with a message that says why. All problems of all overrides
///     are collected, and only complete, valid overrides are kept. The class is stateless and thread-safe.
/// </remarks>
public static partial class SettingsLoader
{
    /// <summary>
    ///     The key of an override that carries the glob patterns selecting its files.
    /// </summary>
    private const string FilenameKey = "filename";

    /// <summary>
    ///     The keys that select files or configure the glob matching, which an override may not change.
    /// </summary>
    private static readonly string[] FileSelectionKeys = [FilesKey, IgnorePathsKey, EnableGlobDotKey];

    /// <summary>
    ///     The settings that exist at the top level but are deliberately not available in an override, with the reason.
    /// </summary>
    private static readonly Dictionary<string, string> UnsupportedOverrideKeys = new(StringComparer.Ordinal)
    {
        ["languageId"] = "'languageId' is not supported in an override.",
        ["dictionaries"] = "'dictionaries' is not supported in an override.",
        [MaxFileSizeKey] = $"'{MaxFileSizeKey}' is not supported in an override; it applies to the whole run.",
        [OverridesKey] = "Nested 'overrides' are not supported.",
    };

    /// <summary>
    ///     Reads the <c>overrides</c> list, a list of mappings that each hold a <c>filename</c> and settings.
    /// </summary>
    /// <remarks>
    ///     A null value is treated as an empty list. A value that is not a list is reported once at the value,
    ///     and an item that is not a mapping at the item. Only complete, valid overrides are kept, and every
    ///     problem is found in one pass. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="topLevelNames">The names that the top-level <c>patterns</c> setting defines. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The valid overrides found, in order; meaningful only when no diagnostics were added.</returns>
    private static List<SettingsOverride> ReadOverrides(
        YamlNode value,
        IReadOnlySet<string> topLevelNames,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        var overrides = new List<SettingsOverride>();
        if (IsNull(value))
        {
            return overrides;
        }

        if (value is not YamlSequenceNode sequence)
        {
            diagnostics.Add(At($"Expected a list of overrides, each with a '{FilenameKey}'.", value.Start, sourceName));
            return overrides;
        }

        // Read each override on its own so that a mistake in one does not hide the mistakes in the others
        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode mapping)
            {
                diagnostics.Add(At($"Expected an override with a '{FilenameKey}'.", item.Start, sourceName));
            }
            else if (TryReadOverride(mapping, topLevelNames, sourceName, diagnostics, out var settingsOverride))
            {
                overrides.Add(settingsOverride);
            }
        }

        return overrides;
    }

    /// <summary>
    ///     Reads one override and validates it.
    /// </summary>
    /// <remarks>
    ///     The keys are read in document order, so diagnostics come out in source order. The pattern names an
    ///     <c>ignoreRegExpList</c> entry may use are the top-level ones plus the override's own, collected first
    ///     because the list may come before the definitions; a name that only another override defines is
    ///     therefore reported as unknown. After the keys, the override as a whole is checked: it needs a
    ///     <c>filename</c>, it must set something, and <c>enabled: false</c> must stand alone because a disabled
    ///     override leaves no file for its other settings to apply to. Side effects: appends to
    ///     <paramref name="diagnostics"/>. Thread-safe as long as the arguments are not shared.
    /// </remarks>
    /// <param name="mapping">The mapping node of the override. Must not be null.</param>
    /// <param name="topLevelNames">The names that the top-level <c>patterns</c> setting defines. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <param name="settingsOverride">The override when the method returns true.</param>
    /// <returns><see langword="true"/> when the override is complete and valid.</returns>
    private static bool TryReadOverride(
        YamlMappingNode mapping,
        IReadOnlySet<string> topLevelNames,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics,
        out SettingsOverride settingsOverride)
    {
        // Pattern names may be used before they are defined, so gather the override's own first
        var allowedNames = new HashSet<string>(topLevelNames, StringComparer.OrdinalIgnoreCase);
        allowedNames.UnionWith(CollectPatternNames(mapping));

        var diagnosticsBefore = diagnostics.Count;
        var result = new SettingsOverride { SourceName = sourceName, Line = (int)mapping.Start.Line, Column = (int)mapping.Start.Column };
        var hasFilename = false;
        var hasSetting = false;
        var hasOtherSetting = false;
        YamlScalarNode? enabledKey = null;
        var enabled = true;

        foreach (var (key, value) in mapping.Children)
        {
            // A key that is not plain text cannot name a setting
            if (key is not YamlScalarNode { Value: { } name } keyScalar)
            {
                diagnostics.Add(At("Setting names must be text.", key.Start, sourceName));
                continue;
            }

            // Keys with a specific explanation are handled before the ones that carry a value
            if (FileSelectionKeys.Contains(name))
            {
                diagnostics.Add(At(
                    $"Overrides cannot change which files are selected; use the top-level '{name}' setting.",
                    key.Start,
                    sourceName));
                continue;
            }

            if (UnsupportedOverrideKeys.TryGetValue(name, out var reason))
            {
                diagnostics.Add(At(reason, key.Start, sourceName));
                continue;
            }

            // The two keys that are special to an override are read here; they are not ordinary settings
            if (name == FilenameKey)
            {
                hasFilename |= !IsEmptyFilename(value);
                result = result with { Filename = ReadFilename(value, sourceName, diagnostics) };
                continue;
            }

            if (name == EnabledKey)
            {
                hasSetting = true;
                enabledKey = keyScalar;
                if (!IsNull(value) && TryReadBoolean(value, out var flag))
                {
                    enabled = flag;
                    result = result with { Enabled = flag };
                }
                else if (!IsNull(value))
                {
                    diagnostics.Add(At($"Expected true or false for '{EnabledKey}'.", value.Start, sourceName));
                }

                continue;
            }

            // Every other key is a setting that has an effect when the override applies
            hasSetting = true;
            hasOtherSetting = true;
            result = ApplySetting(result, name, key, value, allowedNames, sourceName, diagnostics);
        }

        // The override as a whole: it needs files to apply to, something to change, and no contradiction
        if (!hasFilename)
        {
            diagnostics.Add(At($"An override needs a '{FilenameKey}'.", mapping.Start, sourceName));
        }
        else if (!hasSetting)
        {
            diagnostics.Add(At("This override changes nothing: add settings, or remove it.", mapping.Start, sourceName));
        }

        if (enabledKey is not null && !enabled && hasOtherSetting)
        {
            diagnostics.Add(At(
                "This override skips its files, so its other settings have no effect; " +
                $"remove them, or remove '{EnabledKey}: false' to apply them.",
                enabledKey.Start,
                sourceName));
        }

        settingsOverride = result;
        return diagnostics.Count == diagnosticsBefore;
    }

    /// <summary>
    ///     Determines whether the value of a <c>filename</c> key names no pattern at all.
    /// </summary>
    /// <remarks>
    ///     A null value and an empty list both leave the override without files, which is reported as a missing
    ///     <c>filename</c>; a value of the wrong type is reported by the reader, so it counts as present here
    ///     and the same mistake is not reported twice. Stateless and thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <returns><see langword="true"/> when the value is null or an empty list.</returns>
    private static bool IsEmptyFilename(YamlNode value) =>
        IsNull(value) || value is YamlSequenceNode { Children.Count: 0 };

    /// <summary>
    ///     Reads the <c>filename</c> of an override: one glob pattern or an ordered list of them.
    /// </summary>
    /// <remarks>
    ///     A single pattern is the same as a list of one. The list has the meaning of the <c>files</c> setting,
    ///     so <c>!</c> excludes, the last match wins, and a leading <c>!</c> is an error. A null value is an
    ///     empty list, reported by the caller as a missing <c>filename</c>. Side effects: appends to
    ///     <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The valid patterns found, in order; meaningful only when no diagnostics were added.</returns>
    private static List<string> ReadFilename(YamlNode value, string? sourceName, List<SettingsDiagnostic> diagnostics)
    {
        // A single scalar is a list of one
        if (value is YamlScalarNode scalar && !IsNull(value))
        {
            return TryValidateOrderedPattern(scalar, isFirst: true, sourceName, diagnostics) ? [scalar.Value!] : [];
        }

        // A mapping cannot hold patterns; say what is expected rather than the more general list message
        if (value is YamlMappingNode)
        {
            diagnostics.Add(At($"Expected a glob or a list of globs for '{FilenameKey}'.", value.Start, sourceName));
            return [];
        }

        return ReadOrderedPatterns(value, sourceName, diagnostics);
    }

    /// <summary>
    ///     Applies one ordinary setting key of an override to the override being built.
    /// </summary>
    /// <remarks>
    ///     Delegates to the readers of the top-level settings, so validation is identical. A null value leaves
    ///     the setting unset, as at the top level. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="result">The override so far. Must not be null.</param>
    /// <param name="name">The key text. Must not be null.</param>
    /// <param name="key">The key node, for the error position. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="allowedNames">The pattern names an <c>ignoreRegExpList</c> entry may use. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The override with the setting applied, or unchanged when the value is null or in error.</returns>
    private static SettingsOverride ApplySetting(
        SettingsOverride result,
        string name,
        YamlNode key,
        YamlNode value,
        IReadOnlySet<string> allowedNames,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        switch (name)
        {
            case WordsKey:
                return result with { Words = ReadWords(WordsKey, false, value, sourceName, diagnostics) };
            case IgnoreWordsKey:
                return result with { IgnoreWords = ReadWords(IgnoreWordsKey, false, value, sourceName, diagnostics) };
            case FlagWordsKey:
                return result with { FlagWords = ReadWords(FlagWordsKey, true, value, sourceName, diagnostics) };
            case IgnoreRegExpListKey:
                return result with { IgnoreRegExpList = ReadIgnoreRegExpList(value, allowedNames, sourceName, diagnostics) };
            case PatternsKey:
                return result with { Patterns = ReadNamedPatterns(value, sourceName, diagnostics) };
            case LanguageKey:
                return !IsNull(value) && TryReadLanguage(value, sourceName, diagnostics, out var language)
                    ? result with { Language = language }
                    : result;
            case MinWordLengthKey:
                return TryReadCount(value, MinWordLengthKey, sourceName, diagnostics, out var wordLength)
                    ? result with { MinWordLength = wordLength }
                    : result;
            case MinRandomLengthKey:
                return TryReadCount(value, MinRandomLengthKey, sourceName, diagnostics, out var randomLength)
                    ? result with { MinRandomLength = randomLength }
                    : result;
            case IgnoreRandomStringsKey:
                return TryReadSwitch(value, IgnoreRandomStringsKey, sourceName, diagnostics, out var ignoreRandom)
                    ? result with { IgnoreRandomStrings = ignoreRandom }
                    : result;
            default:
                diagnostics.Add(At($"'{name}' is not a supported setting.", key.Start, sourceName));
                return result;
        }
    }

    /// <summary>
    ///     Reads a whole number of at least 1 that is optional in an override.
    /// </summary>
    /// <remarks>
    ///     Read exactly as <c>minWordLength</c> is at the top level. Side effects: appends to
    ///     <paramref name="diagnostics"/> on a value that is not such a number. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="key">The setting name, used in the message. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any error. Must not be null.</param>
    /// <param name="number">The number when the method returns true.</param>
    /// <returns><see langword="true"/> when the value is a valid number; false for null or an error.</returns>
    private static bool TryReadCount(
        YamlNode value,
        string key,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics,
        out int number)
    {
        number = 0;
        if (IsNull(value))
        {
            return false;
        }

        if (TryReadWholeNumber(value, out number))
        {
            return true;
        }

        diagnostics.Add(At($"Expected a whole number of at least 1 for '{key}'.", value.Start, sourceName));
        return false;
    }

    /// <summary>
    ///     Reads a plain boolean that is optional in an override.
    /// </summary>
    /// <remarks>
    ///     Read exactly as <c>ignoreRandomStrings</c> is at the top level. Side effects: appends to
    ///     <paramref name="diagnostics"/> on a value that is not a boolean. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="key">The setting name, used in the message. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any error. Must not be null.</param>
    /// <param name="result">The boolean when the method returns true.</param>
    /// <returns><see langword="true"/> when the value is a valid boolean; false for null or an error.</returns>
    private static bool TryReadSwitch(
        YamlNode value,
        string key,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics,
        out bool result)
    {
        result = false;
        if (IsNull(value))
        {
            return false;
        }

        if (TryReadBoolean(value, out result))
        {
            return true;
        }

        diagnostics.Add(At($"Expected true or false for '{key}'.", value.Start, sourceName));
        return false;
    }
}
