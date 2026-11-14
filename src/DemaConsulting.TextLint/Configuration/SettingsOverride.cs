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
///     One entry of the <c>overrides</c> setting: settings that apply only to the files its <see cref="Filename"/>
///     patterns select.
/// </summary>
/// <remarks>
///     An override is its own type, and not a <see cref="TextLintSettings"/>, because it must be able to say
///     "not set": the settings type fills every unset value with its default, so a settings value cannot tell
///     whether an override left the language alone or set it to the default. Single values are therefore
///     nullable here (null means the override does not set it), and list values are empty when the override adds
///     nothing. The settings that select files (<c>files</c>, <c>ignorePaths</c>, <c>enableGlobDot</c>) and the
///     ones that apply to the whole run (<c>maxFileSize</c>, <c>version</c>) are deliberately not available. The
///     type is a non-positional record with <c>init</c> properties and defaults, so that further settings can be
///     added compatibly. List properties are copied on assignment and stored as immutable arrays, so a value can
///     be shared freely between threads. Record equality compares the stored list instances rather than their
///     contents, so overrides should not be compared for equality. The position of the entry in its settings
///     file is carried along so that a message about the override, produced long after loading, can point at it.
///     Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed record SettingsOverride
{
    /// <summary>
    ///     Immutable backing store for <see cref="Filename"/>; <c>default</c> until first assigned.
    /// </summary>
    private readonly ImmutableArray<string> _filename;

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
    ///     Backing store for <see cref="MinWordLength"/>.
    /// </summary>
    private readonly int? _minWordLength;

    /// <summary>
    ///     Backing store for <see cref="MinRandomLength"/>.
    /// </summary>
    private readonly int? _minRandomLength;

    /// <summary>
    ///     Gets the glob patterns (<c>filename</c>) that select the files the override applies to.
    /// </summary>
    /// <remarks>
    ///     An ordered list with the meaning of the <c>files</c> setting: a pattern with a leading <c>!</c> removes
    ///     files that an earlier pattern selected and the last matching pattern decides. The patterns are
    ///     relative to the settings root, the directory that the <c>files</c> patterns are relative to by default.
    ///     The assigned sequence is copied, so later changes to the caller's list do not affect the override, and
    ///     the exposed list cannot be mutated. The patterns are validated when the spell checker starts, not here.
    /// </remarks>
    /// <value>An immutable list of glob patterns; never <see langword="null"/>, empty by default (an override needs at least one).</value>
    /// <exception cref="ArgumentNullException">Thrown by the setter when the assigned value is null or holds a null entry.</exception>
    public IReadOnlyList<string> Filename
    {
        // Normalize default(ImmutableArray) so an unassigned property reads as an empty list
        get => NormalizeList(_filename);
        init => _filename = CopyList(value);
    }

    /// <summary>
    ///     Gets the words (<c>words</c>) the override accepts as correct, in addition to those of the settings.
    /// </summary>
    /// <remarks>
    ///     List settings of an override add to the top-level list and to those of earlier overrides that match
    ///     the same file. Copied and immutable, exactly as for <see cref="Filename"/>; entries are validated when
    ///     the spell checker starts.
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
    ///     Gets the words (<c>ignoreWords</c>) the override never reports, in addition to those of the settings.
    /// </summary>
    /// <remarks>
    ///     Added to the top-level list like <see cref="Words"/>. Copied and immutable, exactly as for <see cref="Filename"/>.
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
    ///     Gets the words (<c>flagWords</c>) the override always reports, in addition to those of the settings.
    /// </summary>
    /// <remarks>
    ///     Added to the top-level list like <see cref="Words"/>. Copied and immutable, exactly as for <see cref="Filename"/>.
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
    ///     Gets the patterns (<c>ignoreRegExpList</c>) the override adds to those of the settings.
    /// </summary>
    /// <remarks>
    ///     An entry is a pattern written out or the name of a pattern. A name may refer to a predefined pattern,
    ///     to a pattern of the top-level <c>patterns</c>, or to one of this override's own <see cref="Patterns"/>.
    ///     Added to the top-level list like <see cref="Words"/>. Copied and immutable, exactly as for <see cref="Filename"/>.
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
    ///     Gets the named patterns (<c>patterns</c>) the override defines.
    /// </summary>
    /// <remarks>
    ///     A definition replaces a definition of the same name (compared ignoring letter case) from the
    ///     top-level settings or an earlier override, for the files this override applies to; it is applied only
    ///     where an <see cref="IgnoreRegExpList"/> entry names it. Copied and immutable, exactly as for
    ///     <see cref="Filename"/>.
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
    ///     Gets the language selection (<c>language</c>) the override sets, or null when it leaves the language alone.
    /// </summary>
    /// <remarks>
    ///     Accepts what <see cref="TextLintSettings.Language"/> accepts. A command-line language wins over
    ///     it. The value is not validated here; the spell checker rejects an unsupported language when it starts.
    /// </remarks>
    /// <value>The language text, or <see langword="null"/> when not set (the default).</value>
    public string? Language { get; init; }

    /// <summary>
    ///     Gets the minimum word length (<c>minWordLength</c>) the override sets, or null when it leaves it alone.
    /// </summary>
    /// <remarks>
    ///     A single value, so it replaces the top-level value for the files the override applies to.
    /// </remarks>
    /// <value>A length of at least 1, or <see langword="null"/> when not set (the default).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is less than 1.</exception>
    public int? MinWordLength
    {
        get => _minWordLength;
        init
        {
            if (value is { } length)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
            }

            _minWordLength = value;
        }
    }

    /// <summary>
    ///     Gets whether the override switches the skipping of random strings (<c>ignoreRandomStrings</c>) on or
    ///     off, or null when it leaves the setting alone.
    /// </summary>
    /// <remarks>
    ///     A single value, so it replaces the top-level value for the files the override applies to.
    /// </remarks>
    /// <value><see langword="true"/> or <see langword="false"/>, or <see langword="null"/> when not set (the default).</value>
    public bool? IgnoreRandomStrings { get; init; }

    /// <summary>
    ///     Gets the length (<c>minRandomLength</c>) the override sets from which a token may be random data, or null.
    /// </summary>
    /// <remarks>
    ///     A single value, so it replaces the top-level value for the files the override applies to.
    /// </remarks>
    /// <value>A length of at least 1, or <see langword="null"/> when not set (the default).</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is less than 1.</exception>
    public int? MinRandomLength
    {
        get => _minRandomLength;
        init
        {
            if (value is { } length)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
            }

            _minRandomLength = value;
        }
    }

    /// <summary>
    ///     Gets whether the override switches spell checking of its files on or off (<c>enabled</c>), or null when it
    ///     leaves that alone.
    /// </summary>
    /// <remarks>
    ///     A single value, so the last matching override that sets it wins, as in cspell. A file for which the
    ///     result is <see langword="false"/> is not read, reported, or counted.
    /// </remarks>
    /// <value><see langword="true"/> or <see langword="false"/>, or <see langword="null"/> when not set (the default).</value>
    public bool? Enabled { get; init; }

    /// <summary>
    ///     Gets the name of the settings file the override was loaded from, for messages about it.
    /// </summary>
    /// <value>The name as the loader was given it; <see langword="null"/> for an override built in code.</value>
    public string? SourceName { get; init; }

    /// <summary>
    ///     Gets the 1-based line at which the override starts in its settings file.
    /// </summary>
    /// <value>The line; <see langword="null"/> for an override built in code.</value>
    public int? Line { get; init; }

    /// <summary>
    ///     Gets the 1-based column at which the override starts in its settings file.
    /// </summary>
    /// <value>The column; <see langword="null"/> for an override built in code.</value>
    public int? Column { get; init; }

    /// <summary>
    ///     Converts a stored list to the value exposed by the property getters.
    /// </summary>
    /// <remarks>
    ///     An unassigned <see cref="ImmutableArray{T}"/> is the uninitialized <c>default</c> value, which throws on
    ///     use; replacing it with the empty array keeps that state from leaking. Stateless and thread-safe.
    /// </remarks>
    /// <typeparam name="T">The type of the list entries.</typeparam>
    /// <param name="stored">The stored array, possibly the uninitialized default.</param>
    /// <returns>The stored array, or the empty array when <paramref name="stored"/> is the default.</returns>
    private static ImmutableArray<T> NormalizeList<T>(ImmutableArray<T> stored) =>
        stored.IsDefault ? ImmutableArray<T>.Empty : stored;

    /// <summary>
    ///     Takes an immutable copy of a caller-supplied sequence of list entries.
    /// </summary>
    /// <remarks>
    ///     Copying isolates the override from later changes to the caller's list, which is what makes the record
    ///     immutable. Stateless and thread-safe; reads the sequence once.
    /// </remarks>
    /// <typeparam name="T">The type of the list entries.</typeparam>
    /// <param name="value">The sequence to copy. Must not be null, and must not hold a null entry.</param>
    /// <returns>An immutable array holding the items of <paramref name="value"/> in order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null or holds a null entry.</exception>
    private static ImmutableArray<T> CopyList<T>(IEnumerable<T> value)
    {
        // A null list is a programming error; failing at assignment names the cause immediately
        ArgumentNullException.ThrowIfNull(value);

        // Snapshot the sequence so the override never observes later changes to it
        var copy = value.ToImmutableArray();
        if (copy.Any(static entry => entry is null))
        {
            throw new ArgumentNullException(nameof(value), "The list must not hold a null entry.");
        }

        return copy;
    }
}
