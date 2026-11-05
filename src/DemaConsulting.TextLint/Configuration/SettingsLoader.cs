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
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     Loads <see cref="TextLintSettings"/> from a <c>.textlint.yaml</c> file.
/// </summary>
/// <remarks>
///     Settings are read through YamlDotNet's node model and mapped by hand rather than
///     deserialized by reflection, which keeps the library trim and Native AOT safe and gives every
///     node a source position for error messages. Content problems (an unreadable file, malformed
///     YAML, a value of the wrong type, an unsupported key, an invalid glob pattern) are returned as
///     diagnostics, not thrown, so a caller can print all of them uniformly; every error found is
///     collected rather than stopping at the first, except that a YAML syntax error stops parsing at
///     the point of failure. Invalid glob patterns are detected at load time by applying the same
///     rules as <see cref="FileSelector"/>, so a bad pattern is reported with the line and column
///     of the offending list item. Only the keys <c>version</c>, <c>files</c>, <c>ignorePaths</c>,
///     and <c>enableGlobDot</c> are supported; any other key is an error until the feature that
///     needs it exists. The class is stateless and thread-safe.
/// </remarks>
public static class SettingsLoader
{
    /// <summary>
    ///     The key that carries the schema version text.
    /// </summary>
    private const string VersionKey = "version";

    /// <summary>
    ///     The key that carries the include glob patterns.
    /// </summary>
    private const string FilesKey = "files";

    /// <summary>
    ///     The key that carries the exclude glob patterns.
    /// </summary>
    private const string IgnorePathsKey = "ignorePaths";

    /// <summary>
    ///     The key that carries the hidden-file matching switch.
    /// </summary>
    private const string EnableGlobDotKey = "enableGlobDot";

    /// <summary>
    ///     The plain-scalar spellings of YAML null.
    /// </summary>
    private static readonly string[] NullSpellings = ["", "~", "null", "Null", "NULL"];

    /// <summary>
    ///     The plain-scalar spellings of YAML true.
    /// </summary>
    private static readonly string[] TrueSpellings = ["true", "True", "TRUE"];

    /// <summary>
    ///     The plain-scalar spellings of YAML false.
    /// </summary>
    private static readonly string[] FalseSpellings = ["false", "False", "FALSE"];

    /// <summary>
    ///     Reads a settings file and maps it to settings, or to the errors that prevent it.
    /// </summary>
    /// <remarks>
    ///     A missing or unreadable file is reported as a diagnostic that names the file (with no
    ///     line or column) rather than thrown, so callers handle every configuration failure the
    ///     same way. Only programming errors (a null or empty path) and cancellation are thrown.
    ///     Side effects: reads the file; nothing is written. Thread-safe.
    /// </remarks>
    /// <param name="path">The path of the settings file. Must not be null or empty.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>A result holding either the settings or the diagnostics; never null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static async Task<SettingsLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // Read the whole file; a byte-order mark is honored by the reader
        string yaml;
        try
        {
            yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // An unreadable file has no position, so the diagnostic only names the file
            var diagnostic = new SettingsDiagnostic($"Cannot read settings file: {exception.Message}", path, null, null);
            return new SettingsLoadResult(new TextLintSettings(), [diagnostic]);
        }

        return Parse(yaml, path);
    }

    /// <summary>
    ///     Maps settings text to settings, or to the errors that prevent it.
    /// </summary>
    /// <remarks>
    ///     Internal and file-system free: it is the testable core of loading and keeps the public
    ///     surface minimal, because a public text overload would be a further feature needing its own
    ///     requirement. Stateless and thread-safe.
    /// </remarks>
    /// <param name="yaml">The settings text. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics, normally the file path; may be null.</param>
    /// <returns>A result holding either the settings or the diagnostics; never null.</returns>
    internal static SettingsLoadResult Parse(string yaml, string? sourceName)
    {
        // Collect diagnostics while mapping so every problem is reported together
        var diagnostics = new List<SettingsDiagnostic>();
        var settings = ParseSettings(yaml, sourceName, diagnostics);
        return new SettingsLoadResult(settings, diagnostics);
    }

    /// <summary>
    ///     Parses the YAML text and maps its single document to settings.
    /// </summary>
    /// <remarks>
    ///     An empty or comment-only file has no document and yields default settings, which lets a
    ///     new project start with an empty file. A syntax error is reported at the position where
    ///     the parser failed. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="yaml">The settings text. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The mapped settings; meaningful only when no diagnostics were added.</returns>
    private static TextLintSettings ParseSettings(string yaml, string? sourceName, List<SettingsDiagnostic> diagnostics)
    {
        // Parse the text into YamlDotNet's node model, converting a syntax error into a diagnostic
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            diagnostics.Add(At(exception.Message, exception.Start, sourceName));
            return new TextLintSettings();
        }

        // No document means defaults; more than one is ambiguous and rejected
        if (stream.Documents.Count == 0)
        {
            return new TextLintSettings();
        }

        if (stream.Documents.Count > 1)
        {
            var second = stream.Documents[1].RootNode;
            diagnostics.Add(At("Multiple YAML documents are not supported; use a single document.", second.Start, sourceName));
            return new TextLintSettings();
        }

        return MapRoot(stream.Documents[0].RootNode, sourceName, diagnostics);
    }

    /// <summary>
    ///     Maps the root node of the document to settings.
    /// </summary>
    /// <remarks>
    ///     A null root (a document holding only <c>~</c> or nothing) is treated like an empty file.
    ///     Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="root">The document root node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The mapped settings; meaningful only when no diagnostics were added.</returns>
    private static TextLintSettings MapRoot(YamlNode root, string? sourceName, List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(root))
        {
            return new TextLintSettings();
        }

        // The settings must be a mapping of keys to values
        if (root is not YamlMappingNode mapping)
        {
            diagnostics.Add(At("Expected a mapping of settings at the top level.", root.Start, sourceName));
            return new TextLintSettings();
        }

        // Apply each key in document order so diagnostics come out in source order
        var settings = new TextLintSettings();
        foreach (var (key, value) in mapping.Children)
        {
            settings = MapEntry(settings, key, value, sourceName, diagnostics);
        }

        return settings;
    }

    /// <summary>
    ///     Applies one key and value from the top-level mapping to the settings.
    /// </summary>
    /// <remarks>
    ///     Keys are matched with exact (ordinal) comparison, so they are case-sensitive; <c>Files</c> is not <c>files</c>.
    ///     A key that is not supported is an error naming the key, because silently ignoring it
    ///     would let a setting the user believes is in force have no effect. Side effects: appends
    ///     to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="key">The key node. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the entry applied, or unchanged when the entry is in error or null.</returns>
    private static TextLintSettings MapEntry(
        TextLintSettings settings,
        YamlNode key,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        // A key that is not plain text cannot name a setting
        if (key is not YamlScalarNode keyScalar)
        {
            diagnostics.Add(At("Setting names must be text.", key.Start, sourceName));
            return settings;
        }

        // Dispatch on the key; an unsupported key is reported at the key itself
        return keyScalar.Value switch
        {
            VersionKey => MapVersion(settings, value, sourceName, diagnostics),
            FilesKey => settings with { Files = ReadPatterns(value, sourceName, diagnostics) },
            IgnorePathsKey => settings with { IgnorePaths = ReadPatterns(value, sourceName, diagnostics) },
            EnableGlobDotKey => MapEnableGlobDot(settings, value, sourceName, diagnostics),
            _ => Reject(settings, $"'{keyScalar.Value}' is not a supported setting.", key, sourceName, diagnostics),
        };
    }

    /// <summary>
    ///     Applies the <c>version</c> value, which is carried as text and not validated.
    /// </summary>
    /// <remarks>
    ///     Any scalar style is accepted, so an unquoted <c>0.2</c> and a quoted <c>"0.2"</c> both
    ///     map to the text <c>0.2</c>. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the version applied, or unchanged when the value is null or in error.</returns>
    private static TextLintSettings MapVersion(
        TextLintSettings settings,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(value))
        {
            return settings;
        }

        return value is YamlScalarNode scalar
            ? settings with { Version = scalar.Value }
            : Reject(settings, $"Expected a text value for '{VersionKey}'.", value, sourceName, diagnostics);
    }

    /// <summary>
    ///     Applies the <c>enableGlobDot</c> value, which must be a plain boolean.
    /// </summary>
    /// <remarks>
    ///     Only the YAML core spellings <c>true</c> and <c>false</c> (in lower, title, or upper
    ///     case) are accepted. Quoted values and YAML 1.1 spellings such as <c>yes</c> are rejected
    ///     rather than guessed at. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the switch applied, or unchanged when the value is null or in error.</returns>
    private static TextLintSettings MapEnableGlobDot(
        TextLintSettings settings,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(value))
        {
            return settings;
        }

        // Accept only unquoted true or false
        if (value is YamlScalarNode { Style: ScalarStyle.Plain, Value: { } text })
        {
            if (TrueSpellings.Contains(text))
            {
                return settings with { EnableGlobDot = true };
            }

            if (FalseSpellings.Contains(text))
            {
                return settings with { EnableGlobDot = false };
            }
        }

        return Reject(settings, $"Expected true or false for '{EnableGlobDotKey}'.", value, sourceName, diagnostics);
    }

    /// <summary>
    ///     Reads a list of glob patterns, validating each pattern.
    /// </summary>
    /// <remarks>
    ///     A null value is treated as an empty list so a commented-out list does not break the
    ///     file. Every item is validated with the same rules the file selector applies, so an
    ///     invalid pattern is reported at load time with the position of its list item. Side
    ///     effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The valid patterns found, in order; meaningful only when no diagnostics were added.</returns>
    private static List<string> ReadPatterns(YamlNode value, string? sourceName, List<SettingsDiagnostic> diagnostics)
    {
        var patterns = new List<string>();
        if (IsNull(value))
        {
            return patterns;
        }

        // The value must be a sequence; a scalar or mapping is reported once at the value
        if (value is not YamlSequenceNode sequence)
        {
            diagnostics.Add(At("Expected a list of text values.", value.Start, sourceName));
            return patterns;
        }

        // Check each item, reporting every bad item rather than stopping at the first
        foreach (var item in sequence.Children)
        {
            if (item is not YamlScalarNode scalar || IsNull(item))
            {
                diagnostics.Add(At("Expected a text value in the list.", item.Start, sourceName));
            }
            else if (TryValidatePattern(scalar, sourceName, diagnostics))
            {
                patterns.Add(scalar.Value!);
            }
        }

        return patterns;
    }

    /// <summary>
    ///     Checks that a pattern is acceptable to the file selector.
    /// </summary>
    /// <remarks>
    ///     Reuses <see cref="GlobPatternExpander"/> so load-time and selection-time validation can
    ///     never disagree. Side effects: appends to <paramref name="diagnostics"/> on failure. Thread-safe.
    /// </remarks>
    /// <param name="pattern">The scalar node holding the pattern. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives the error. Must not be null.</param>
    /// <returns><see langword="true"/> when the pattern is valid.</returns>
    private static bool TryValidatePattern(YamlScalarNode pattern, string? sourceName, List<SettingsDiagnostic> diagnostics)
    {
        try
        {
            _ = GlobPatternExpander.Expand(pattern.Value!);
            return true;
        }
        catch (InvalidGlobPatternException exception)
        {
            diagnostics.Add(At(exception.Message, pattern.Start, sourceName));
            return false;
        }
    }

    /// <summary>
    ///     Records an error at a node and returns the settings unchanged.
    /// </summary>
    /// <remarks>
    ///     Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings to return. Must not be null.</param>
    /// <param name="message">The error message. Must not be null.</param>
    /// <param name="node">The node the error is reported at. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives the error. Must not be null.</param>
    /// <returns>The unchanged <paramref name="settings"/>.</returns>
    private static TextLintSettings Reject(
        TextLintSettings settings,
        string message,
        YamlNode node,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        diagnostics.Add(At(message, node.Start, sourceName));
        return settings;
    }

    /// <summary>
    ///     Determines whether a node is YAML null.
    /// </summary>
    /// <remarks>
    ///     Only an unquoted scalar can be null, so a quoted <c>"null"</c> is ordinary text.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="node">The node to test. Must not be null.</param>
    /// <returns><see langword="true"/> when the node is an unquoted empty value, <c>~</c>, or <c>null</c>.</returns>
    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar && (scalar.Value is null || NullSpellings.Contains(scalar.Value));

    /// <summary>
    ///     Creates a diagnostic positioned at a YAML mark.
    /// </summary>
    /// <remarks>
    ///     YamlDotNet marks are 1-based, which is what the diagnostic documents. Stateless and thread-safe.
    /// </remarks>
    /// <param name="message">The error message. Must not be null.</param>
    /// <param name="start">The position of the problem.</param>
    /// <param name="sourceName">The name to report in the diagnostic; may be null.</param>
    /// <returns>The new diagnostic.</returns>
    private static SettingsDiagnostic At(string message, Mark start, string? sourceName) =>
        new(message, sourceName, (int)start.Line, (int)start.Column);
}
