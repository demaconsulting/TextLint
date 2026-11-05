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

using System.Globalization;
using DemaConsulting.TextLint.Dictionaries;
using DemaConsulting.TextLint.FileSelection;
using DemaConsulting.TextLint.TextScanning;
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
///     YAML, a value of the wrong type, an unsupported key, an invalid glob pattern, an unsupported
///     language, an unusable word-list entry, an invalid number or size) are returned as
///     diagnostics, not thrown, so a caller can print all of them uniformly; every error found is
///     collected rather than stopping at the first, except that a YAML syntax error stops parsing at
///     the point of failure. Invalid glob patterns are detected at load time by applying the same
///     rules as <see cref="FileSelector"/>, an unsupported language by asking
///     <see cref="SpellingDictionary"/>, and word-list entries by interpreting them as the spell
///     checker does (see <see cref="WordTokenizer"/>), so a bad value is reported with the line and
///     column of the offending node. Only the keys <c>version</c>, <c>files</c>, <c>ignorePaths</c>,
///     <c>enableGlobDot</c>, <c>language</c>, <c>words</c>, <c>ignoreWords</c>, <c>flagWords</c>,
///     <c>minWordLength</c>, and <c>maxFileSize</c> are supported; any other key is an error until the
///     feature that needs it exists. The class is stateless and thread-safe.
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
    ///     The key that carries the language selection.
    /// </summary>
    private const string LanguageKey = "language";

    /// <summary>
    ///     The key that carries the accepted words.
    /// </summary>
    private const string WordsKey = "words";

    /// <summary>
    ///     The key that carries the ignored words.
    /// </summary>
    private const string IgnoreWordsKey = "ignoreWords";

    /// <summary>
    ///     The key that carries the flagged words.
    /// </summary>
    private const string FlagWordsKey = "flagWords";

    /// <summary>
    ///     The key that carries the minimum word length.
    /// </summary>
    private const string MinWordLengthKey = "minWordLength";

    /// <summary>
    ///     The key that carries the maximum file size.
    /// </summary>
    private const string MaxFileSizeKey = "maxFileSize";

    /// <summary>
    ///     The number of bytes in a kilobyte, as cspell counts them (binary multiples).
    /// </summary>
    private const double BytesPerKilobyte = 1024.0;

    /// <summary>
    ///     The text that cuts the parameter name that <see cref="ArgumentException"/> appends to its message.
    /// </summary>
    private const string ParameterSuffixMarker = " (Parameter '";

    /// <summary>
    ///     The size units of the <c>maxFileSize</c> setting and the number of bytes each stands for; the
    ///     comparison ignores letter case.
    /// </summary>
    private static readonly Dictionary<string, double> SizeUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        [string.Empty] = 1.0,
        ["b"] = 1.0,
        ["k"] = BytesPerKilobyte,
        ["kb"] = BytesPerKilobyte,
        ["m"] = BytesPerKilobyte * BytesPerKilobyte,
        ["mb"] = BytesPerKilobyte * BytesPerKilobyte,
        ["g"] = BytesPerKilobyte * BytesPerKilobyte * BytesPerKilobyte,
        ["gb"] = BytesPerKilobyte * BytesPerKilobyte * BytesPerKilobyte,
    };

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
            LanguageKey => MapLanguage(settings, value, sourceName, diagnostics),
            WordsKey => settings with { Words = ReadWords(WordsKey, false, value, sourceName, diagnostics) },
            IgnoreWordsKey => settings with { IgnoreWords = ReadWords(IgnoreWordsKey, false, value, sourceName, diagnostics) },
            FlagWordsKey => settings with { FlagWords = ReadWords(FlagWordsKey, true, value, sourceName, diagnostics) },
            MinWordLengthKey => MapMinWordLength(settings, value, sourceName, diagnostics),
            MaxFileSizeKey => MapMaxFileSize(settings, value, sourceName, diagnostics),
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
    ///     Applies the <c>language</c> value, which must name supported languages.
    /// </summary>
    /// <remarks>
    ///     The value is validated by asking <see cref="SpellingDictionary.ForLanguages"/>, so the loader
    ///     and the spell checker can never disagree about which languages exist, and the message names the
    ///     offending value and lists the supported ones. The text is stored as written. Side effects:
    ///     appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the language applied, or unchanged when the value is null or in error.</returns>
    private static TextLintSettings MapLanguage(
        TextLintSettings settings,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(value))
        {
            return settings;
        }

        if (value is not YamlScalarNode { Value: { } text })
        {
            return Reject(settings, $"Expected a text value for '{LanguageKey}'.", value, sourceName, diagnostics);
        }

        try
        {
            _ = SpellingDictionary.ForLanguages(text);
            return settings with { Language = text };
        }
        catch (ArgumentException exception)
        {
            // The framework appends the parameter name to the message, which means nothing in a settings file
            var message = exception.Message;
            var cut = message.IndexOf(ParameterSuffixMarker, StringComparison.Ordinal);
            return Reject(settings, cut >= 0 ? message[..cut] : message, value, sourceName, diagnostics);
        }
    }

    /// <summary>
    ///     Reads a list of words, validating each entry.
    /// </summary>
    /// <remarks>
    ///     A null value is treated as an empty list so a commented-out list does not break the file.
    ///     Every entry is interpreted exactly as the spell checker will interpret it, so an entry that
    ///     could never match (blank, containing white space or a character that cannot be part of a word,
    ///     without a letter or digit, or the unsupported forbidden-word marker) is reported at load time
    ///     with the position of its list item. The entries are kept as written. Side effects: appends to
    ///     <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="key">The name of the setting, used in messages. Must not be null.</param>
    /// <param name="allowSuggestions"><see langword="true"/> to accept the suggestion forms of flagged words.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The valid entries found, in order; meaningful only when no diagnostics were added.</returns>
    private static List<string> ReadWords(
        string key,
        bool allowSuggestions,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics) =>
        ReadList(value, sourceName, diagnostics, entry => TryValidateWord(key, allowSuggestions, entry, sourceName, diagnostics));

    /// <summary>
    ///     Checks that a word-list entry can match something.
    /// </summary>
    /// <remarks>
    ///     Reuses <see cref="WordTokenizer.TryParseListEntry"/>, so load-time and check-time validation can
    ///     never disagree. Side effects: appends to <paramref name="diagnostics"/> on failure. Thread-safe.
    /// </remarks>
    /// <param name="key">The name of the setting, used in the message. Must not be null.</param>
    /// <param name="allowSuggestions"><see langword="true"/> to accept the suggestion forms of flagged words.</param>
    /// <param name="entry">The scalar node holding the entry. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives the error. Must not be null.</param>
    /// <returns><see langword="true"/> when the entry is usable.</returns>
    private static bool TryValidateWord(
        string key,
        bool allowSuggestions,
        YamlScalarNode entry,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (WordTokenizer.TryParseListEntry(entry.Value, allowSuggestions, out _, out var problem))
        {
            return true;
        }

        diagnostics.Add(At($"Cannot use the '{key}' entry '{entry.Value}': {problem}", entry.Start, sourceName));
        return false;
    }

    /// <summary>
    ///     Applies the <c>minWordLength</c> value, which must be a plain whole number of at least 1.
    /// </summary>
    /// <remarks>
    ///     Quoted values, signs, and fractions are rejected rather than guessed at, as for
    ///     <c>enableGlobDot</c>. Side effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the length applied, or unchanged when the value is null or in error.</returns>
    private static TextLintSettings MapMinWordLength(
        TextLintSettings settings,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(value))
        {
            return settings;
        }

        // Accept only an unquoted run of digits that is at least 1 and fits an integer
        if (value is YamlScalarNode { Style: ScalarStyle.Plain, Value: { } text } &&
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var length) &&
            length >= 1)
        {
            return settings with { MinWordLength = length };
        }

        return Reject(settings, $"Expected a whole number of at least 1 for '{MinWordLengthKey}'.", value, sourceName, diagnostics);
    }

    /// <summary>
    ///     Applies the <c>maxFileSize</c> value, which must be a size.
    /// </summary>
    /// <remarks>
    ///     The size is a number optionally followed by a unit (<c>K</c>, <c>KB</c>, <c>M</c>, <c>MB</c>,
    ///     <c>G</c>, <c>GB</c>, or <c>B</c>, in any letter case), counted in binary multiples as cspell does;
    ///     the number may have a fraction (<c>0.5M</c>). Zero means no limit. A fractional number of bytes
    ///     is rounded down, which selects the same files because a file has a whole number of bytes. Side
    ///     effects: appends to <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="settings">The settings so far. Must not be null.</param>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The settings with the size applied, or unchanged when the value is null or in error.</returns>
    private static TextLintSettings MapMaxFileSize(
        TextLintSettings settings,
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics)
    {
        if (IsNull(value))
        {
            return settings;
        }

        if (value is YamlScalarNode { Value: { } text } && TryParseSize(text, out var bytes))
        {
            return settings with { MaxFileSize = bytes };
        }

        return Reject(
            settings,
            $"Expected a size such as 100000, 500K, or 1.5MB (units B, K, KB, M, MB, G, GB) or 0 for no limit, for '{MaxFileSizeKey}'.",
            value,
            sourceName,
            diagnostics);
    }

    /// <summary>
    ///     Parses a size text such as <c>500K</c> or <c>1.5MB</c> into a number of bytes.
    /// </summary>
    /// <remarks>
    ///     Follows cspell's grammar: digits with an optional fraction (a lone fraction such as <c>.5</c> is
    ///     allowed), then an optional unit made of letters. A positive size below one byte is rejected,
    ///     because it would silently mean no limit. Stateless and thread-safe.
    /// </remarks>
    /// <param name="text">The size text. Must not be null.</param>
    /// <param name="bytes">Receives the size in bytes when the method returns true; otherwise 0.</param>
    /// <returns><see langword="true"/> when the text is a valid size that fits in a 64-bit integer.</returns>
    private static bool TryParseSize(string text, out long bytes)
    {
        bytes = 0;

        // Split the number from the unit: the number is the leading run of digits and dots
        var numberLength = 0;
        while (numberLength < text.Length && (char.IsAsciiDigit(text[numberLength]) || text[numberLength] == '.'))
        {
            numberLength++;
        }

        // The number must parse as a plain decimal, and the unit must be a known one
        if (!double.TryParse(text.AsSpan(0, numberLength), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ||
            !SizeUnits.TryGetValue(text[numberLength..], out var multiplier))
        {
            return false;
        }

        // Round down to whole bytes, and refuse a size that is not representable or that would mean no limit by accident
        var size = Math.Floor(number * multiplier);
        if (size >= long.MaxValue || (number > 0 && size < 1))
        {
            return false;
        }

        bytes = (long)size;
        return true;
    }

    /// <summary>
    ///     Reads a list of glob patterns, validating each pattern.
    /// </summary>
    /// <remarks>
    ///     Every item is validated with the same rules the file selector applies, so an invalid pattern
    ///     is reported at load time with the position of its list item. See <see cref="ReadList"/> for the
    ///     handling of a null value and of items that are not text. Side effects: appends to
    ///     <paramref name="diagnostics"/>. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <returns>The valid patterns found, in order; meaningful only when no diagnostics were added.</returns>
    private static List<string> ReadPatterns(YamlNode value, string? sourceName, List<SettingsDiagnostic> diagnostics) =>
        ReadList(value, sourceName, diagnostics, pattern => TryValidatePattern(pattern, sourceName, diagnostics));

    /// <summary>
    ///     Reads a list of text values, keeping each item that the validator accepts.
    /// </summary>
    /// <remarks>
    ///     A null value is treated as an empty list so a commented-out list does not break the
    ///     file. A value that is not a list is reported once at the value, and every item that is not
    ///     text is reported at the item, rather than stopping at the first problem. Side effects:
    ///     appends to <paramref name="diagnostics"/>, and the validator may too. Thread-safe.
    /// </remarks>
    /// <param name="value">The value node. Must not be null.</param>
    /// <param name="sourceName">The name to report in diagnostics; may be null.</param>
    /// <param name="diagnostics">The list that receives any errors. Must not be null.</param>
    /// <param name="accept">
    ///     Validates one text item, reporting its own diagnostic when it is not acceptable. Must not be null.
    /// </param>
    /// <returns>The accepted items in order; meaningful only when no diagnostics were added.</returns>
    private static List<string> ReadList(
        YamlNode value,
        string? sourceName,
        List<SettingsDiagnostic> diagnostics,
        Func<YamlScalarNode, bool> accept)
    {
        var items = new List<string>();
        if (IsNull(value))
        {
            return items;
        }

        // The value must be a sequence; a scalar or mapping is reported once at the value
        if (value is not YamlSequenceNode sequence)
        {
            diagnostics.Add(At("Expected a list of text values.", value.Start, sourceName));
            return items;
        }

        // Check each item, reporting every bad item rather than stopping at the first
        foreach (var item in sequence.Children)
        {
            if (item is not YamlScalarNode scalar || IsNull(item))
            {
                diagnostics.Add(At("Expected a text value in the list.", item.Start, sourceName));
            }
            else if (accept(scalar))
            {
                items.Add(scalar.Value!);
            }
        }

        return items;
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
