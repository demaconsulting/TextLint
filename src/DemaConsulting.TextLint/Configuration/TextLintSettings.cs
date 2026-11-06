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

using System.Collections.Immutable;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     Immutable TextLint settings, either loaded from a <c>.textlint.yaml</c> file or built in code.
/// </summary>
/// <remarks>
///     A single settings type serves both the file-driven and the programmatic route so that every
///     consumer (the command-line tool and any embedding application) feeds the same value to the
///     rest of the library. The type is a non-positional record with <c>init</c> properties and
///     defaults so that settings added in later releases are source-compatible additions, and so
///     that callers can derive a modified copy with a <c>with</c> expression (for example to
///     replace <see cref="Files"/> with globs supplied on a command line) without mutating the
///     original. List properties are copied on assignment and stored as immutable arrays, so a
///     settings value can be shared freely between threads. Record equality compares the stored
///     list instances rather than their contents, so settings should not be compared for equality.
///     Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed record TextLintSettings
{
    /// <summary>
    ///     The language that cspell uses when none is configured, which is American English.
    /// </summary>
    private const string DefaultLanguage = "en";

    /// <summary>
    ///     The minimum length of a word that is checked when none is configured, which is cspell's default.
    /// </summary>
    private const int DefaultMinWordLength = 4;

    /// <summary>
    ///     The length from which a token may be judged to be random when none is configured, which is cspell's default.
    /// </summary>
    private const int DefaultMinRandomLength = 40;

    /// <summary>
    ///     Immutable backing store for <see cref="Files"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _files;

    /// <summary>
    ///     Immutable backing store for <see cref="IgnorePaths"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _ignorePaths;

    /// <summary>
    ///     Immutable backing store for <see cref="Words"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _words;

    /// <summary>
    ///     Immutable backing store for <see cref="IgnoreWords"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _ignoreWords;

    /// <summary>
    ///     Immutable backing store for <see cref="FlagWords"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _flagWords;

    /// <summary>
    ///     Immutable backing store for <see cref="IgnoreRegExpList"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _ignoreRegExpList;

    /// <summary>
    ///     Immutable backing store for <see cref="Patterns"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<NamedPattern> _patterns;

    /// <summary>
    ///     Backing store for <see cref="Language"/>.
    /// </summary>
    private readonly string _language = DefaultLanguage;

    /// <summary>
    ///     Backing store for <see cref="MinWordLength"/>.
    /// </summary>
    private readonly int _minWordLength = DefaultMinWordLength;

    /// <summary>
    ///     Backing store for <see cref="MaxFileSize"/>.
    /// </summary>
    private readonly long _maxFileSize;

    /// <summary>
    ///     Backing store for <see cref="MinRandomLength"/>.
    /// </summary>
    private readonly int _minRandomLength = DefaultMinRandomLength;

    /// <summary>
    ///     Gets the settings schema version text from the <c>version</c> key.
    /// </summary>
    /// <remarks>
    ///     The value is carried verbatim and is never validated, because the schema version has no
    ///     behavior yet; it exists so that configurations migrated from cspell, which nearly always
    ///     declare a version, load without complaint.
    /// </remarks>
    /// <value>The version text (for example <c>0.2</c>), or <see langword="null"/> when absent.</value>
    public string? Version { get; init; }

    /// <summary>
    ///     Gets the include glob patterns (<c>files</c>) that select the files to check.
    /// </summary>
    /// <remarks>
    ///     An empty list selects nothing. The assigned sequence is copied, so later changes to the
    ///     caller's list do not affect the settings, and the exposed list cannot be mutated.
    /// </remarks>
    /// <value>An immutable list of glob patterns; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> Files
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_files);
        init => _files = CopyList(value);
    }

    /// <summary>
    ///     Gets the exclude glob patterns (<c>ignorePaths</c>) removed from the selection.
    /// </summary>
    /// <remarks>
    ///     The assigned sequence is copied and the exposed list cannot be mutated, exactly as for
    ///     <see cref="Files"/>.
    /// </remarks>
    /// <value>An immutable list of glob patterns; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> IgnorePaths
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_ignorePaths);
        init => _ignorePaths = CopyList(value);
    }

    /// <summary>
    ///     Gets a value indicating whether wildcards may match hidden (dot-prefixed) files and directories.
    /// </summary>
    /// <remarks>
    ///     When <see langword="false"/> (the default), only hidden names written literally in a
    ///     pattern can match a hidden file or directory.
    /// </remarks>
    /// <value><see langword="true"/> to let wildcards match hidden names; otherwise <see langword="false"/>.</value>
    public bool EnableGlobDot { get; init; }

    /// <summary>
    ///     Gets the language selection (<c>language</c>) that chooses the English dictionaries.
    /// </summary>
    /// <remarks>
    ///     A language name or a comma-separated list of names, as accepted by
    ///     <see cref="Dictionaries.SpellingDictionary.ForLanguages"/>: <c>en</c> and <c>en-US</c> (American
    ///     English), <c>en-GB</c> (British English), or both. The value is not validated here; a loaded
    ///     settings file has been validated by the loader, and the spell checker rejects an unsupported
    ///     language when it is created.
    /// </remarks>
    /// <value>The language text; <c>en</c> by default, which is American English as in cspell.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null.</exception>
    public string Language
    {
        get => _language;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _language = value;
        }
    }

    /// <summary>
    ///     Gets the words (<c>words</c>) that are accepted as correct.
    /// </summary>
    /// <remarks>
    ///     Entries are matched ignoring letter case. An entry that contains digits or punctuation, such as
    ///     <c>k8s</c> or <c>foo-bar</c>, is matched against whole tokens of the text. The assigned sequence is
    ///     copied, so later changes to the caller's list do not affect the settings, and the exposed list
    ///     cannot be mutated. Entries are validated when the spell checker is created, not here.
    /// </remarks>
    /// <value>An immutable list of entries; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> Words
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_words);
        init => _words = CopyList(value);
    }

    /// <summary>
    ///     Gets the words (<c>ignoreWords</c>) that are never reported, even if they are also flagged.
    /// </summary>
    /// <remarks>
    ///     Matched exactly like <see cref="Words"/>. An ignored word wins over a flagged word, which wins
    ///     over an accepted word. Copied and immutable, exactly as for <see cref="Words"/>.
    /// </remarks>
    /// <value>An immutable list of entries; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> IgnoreWords
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_ignoreWords);
        init => _ignoreWords = CopyList(value);
    }

    /// <summary>
    ///     Gets the words (<c>flagWords</c>) that are always reported, even if they are known words.
    /// </summary>
    /// <remarks>
    ///     An entry written entirely in lower case flags the word in any letter case; an entry with a
    ///     capital letter flags only that exact spelling. The cspell forms <c>word-&gt;one, two</c> and
    ///     <c>word: one</c> attach suggestions to the entry. Copied and immutable, exactly as for <see cref="Words"/>.
    /// </remarks>
    /// <value>An immutable list of entries; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> FlagWords
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_flagWords);
        init => _flagWords = CopyList(value);
    }

    /// <summary>
    ///     Gets the minimum length (<c>minWordLength</c>) of a word that is checked.
    /// </summary>
    /// <remarks>
    ///     Words and the pieces of a word shorter than this are not checked, which keeps abbreviations and
    ///     variable names such as <c>idx</c> quiet; flagged words are always checked. The length is counted in
    ///     UTF-16 code units, as cspell does.
    /// </remarks>
    /// <value>A length of at least 1; 4 by default, as in cspell.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is less than 1.</exception>
    public int MinWordLength
    {
        get => _minWordLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minWordLength = value;
        }
    }

    /// <summary>
    ///     Gets the largest file size in bytes (<c>maxFileSize</c>) that is checked.
    /// </summary>
    /// <remarks>
    ///     A larger file is skipped and reported by the spell checker. Zero means no limit, which is the
    ///     default, as in cspell; the spell checker still refuses a file that is too large to hold in memory.
    /// </remarks>
    /// <value>A size in bytes, never negative; 0 (no limit) by default.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is negative.</exception>
    public long MaxFileSize
    {
        get => _maxFileSize;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxFileSize = value;
        }
    }

    /// <summary>
    ///     Gets the patterns (<c>ignoreRegExpList</c>) whose matches are not spell-checked, in addition to the
    ///     default ones.
    /// </summary>
    /// <remarks>
    ///     Each entry is a pattern written out, either <c>/body/flags</c> or a bare expression, or the name
    ///     of a pattern: one that <see cref="Patterns"/> defines or one that cspell defines in advance. The default
    ///     ignore list (URLs, email addresses, hashes, and so on) always applies and is not listed here.
    ///     The assigned sequence is copied, so later changes to the caller's list do not affect the
    ///     settings, and the exposed list cannot be mutated. Entries are validated when the spell checker is
    ///     created, not here.
    /// </remarks>
    /// <value>An immutable list of entries; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> IgnoreRegExpList
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_ignoreRegExpList);
        init => _ignoreRegExpList = CopyList(value);
    }

    /// <summary>
    ///     Gets the named patterns (<c>patterns</c>) that <see cref="IgnoreRegExpList"/> can refer to.
    /// </summary>
    /// <remarks>
    ///     A pattern is applied only when <see cref="IgnoreRegExpList"/> names it. Names are compared
    ///     ignoring letter case and must not be the name of a predefined pattern. Copied and immutable,
    ///     exactly as for <see cref="IgnoreRegExpList"/>; entries are validated when the spell checker is
    ///     created, not here.
    /// </remarks>
    /// <value>An immutable list of named patterns; never <see langword="null"/>, empty by default.</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<NamedPattern> Patterns
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_patterns);
        init => _patterns = CopyList(value);
    }

    /// <summary>
    ///     Gets a value indicating whether long tokens that look like random data are not spell-checked
    ///     (<c>ignoreRandomStrings</c>).
    /// </summary>
    /// <remarks>
    ///     When <see langword="true"/> (the default, as in cspell) a token of at least
    ///     <see cref="MinRandomLength"/> characters that looks like random data, such as a key or a token,
    ///     is skipped, and so are unknown words that lie inside a hexadecimal sequence of a token.
    /// </remarks>
    /// <value><see langword="true"/> to skip such text; otherwise <see langword="false"/>. Default <see langword="true"/>.</value>
    public bool IgnoreRandomStrings { get; init; } = true;

    /// <summary>
    ///     Gets the length (<c>minRandomLength</c>) from which a token is judged to be random data or not.
    /// </summary>
    /// <remarks>
    ///     A shorter token is always checked as words. Used only when <see cref="IgnoreRandomStrings"/> is
    ///     <see langword="true"/>. Counted in UTF-16 code units, as cspell does.
    /// </remarks>
    /// <value>A length of at least 1; 40 by default, as in cspell.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is less than 1.</exception>
    public int MinRandomLength
    {
        get => _minRandomLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minRandomLength = value;
        }
    }

    /// <summary>
    ///     Converts a stored list to the value exposed by the property getters.
    /// </summary>
    /// <remarks>
    ///     An unassigned <see cref="ImmutableArray{T}"/> is the uninitialized <c>default</c> value,
    ///     which throws on use; replacing it with the empty array keeps that state from leaking.
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <typeparam name="T">The type of the list entries.</typeparam>
    /// <param name="stored">The stored array, possibly the uninitialized default.</param>
    /// <returns>The stored array, or the empty array when <paramref name="stored"/> is the default.</returns>
    private static ImmutableArray<T> NormalizeList<T>(ImmutableArray<T> stored) =>
        stored.IsDefault ? ImmutableArray<T>.Empty : stored;

    /// <summary>
    ///     Takes an immutable copy of a caller-supplied sequence of list entries (patterns or words).
    /// </summary>
    /// <remarks>
    ///     Copying isolates the settings from later changes to the caller's list, which is what makes
    ///     the record immutable. Stateless and thread-safe; reads the sequence once.
    /// </remarks>
    /// <typeparam name="T">The type of the list entries.</typeparam>
    /// <param name="value">The sequence to copy. Must not be null, and must not hold a null entry.</param>
    /// <returns>An immutable array holding the items of <paramref name="value"/> in order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null or holds a null entry.</exception>
    private static ImmutableArray<T> CopyList<T>(IEnumerable<T> value)
    {
        // A null list is a programming error; failing at assignment names the cause immediately
        ArgumentNullException.ThrowIfNull(value);

        // Snapshot the sequence so the settings never observe later changes to it
        var copy = value.ToImmutableArray();
        if (copy.Any(static entry => entry is null))
        {
            throw new ArgumentNullException(nameof(value), "The list must not hold a null entry.");
        }

        return copy;
    }
}
