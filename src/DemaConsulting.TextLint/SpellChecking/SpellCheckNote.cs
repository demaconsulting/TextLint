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

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     A message about how the settings were applied during a run, such as an override that matched no
///     file or a language that replaced an override's language.
/// </summary>
/// <remarks>
///     Notes are returned as data so that every front-end shows them in one uniform format, and they never
///     count as a problem: a run with only notes succeeds. Informational notes are formatted as
///     <c>info: message</c>; a warning is formatted in the compiler style
///     <c>file(line,column): warning: message</c> with the position of the override it is about, so editors
///     and CI log parsers make it clickable. Instances are immutable and therefore thread-safe.
/// </remarks>
public sealed class SpellCheckNote
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpellCheckNote"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the spell checker produces notes; consumers only read them. The severity
    ///     follows from the kind: only an unmatched override is a warning.
    /// </remarks>
    /// <param name="kind">The kind of note.</param>
    /// <param name="message">The sentence that describes the note, without location or severity. Must not be null.</param>
    /// <param name="source">The settings file the note is about, or null when it is not about one.</param>
    /// <param name="line">The 1-based line in <paramref name="source"/>, or null when unknown.</param>
    /// <param name="column">The 1-based column in <paramref name="source"/>, or null when unknown.</param>
    internal SpellCheckNote(SpellCheckNoteKind kind, string message, string? source, int? line, int? column)
    {
        Kind = kind;
        Severity = kind == SpellCheckNoteKind.OverrideUnmatched ? SpellCheckNoteSeverity.Warning : SpellCheckNoteSeverity.Info;
        Message = message;
        Source = source;
        Line = line;
        Column = column;
    }

    /// <summary>
    ///     Gets the kind of note.
    /// </summary>
    /// <value>The kind.</value>
    public SpellCheckNoteKind Kind { get; }

    /// <summary>
    ///     Gets how serious the note is.
    /// </summary>
    /// <value><see cref="SpellCheckNoteSeverity.Warning"/> for an unmatched override; otherwise <see cref="SpellCheckNoteSeverity.Info"/>.</value>
    public SpellCheckNoteSeverity Severity { get; }

    /// <summary>
    ///     Gets the sentence that describes the note.
    /// </summary>
    /// <value>A human-readable sentence without location or severity word.</value>
    public string Message { get; }

    /// <summary>
    ///     Gets the settings file the note is about.
    /// </summary>
    /// <value>The name the settings were loaded under; <see langword="null"/> when the note is not about a settings file.</value>
    public string? Source { get; }

    /// <summary>
    ///     Gets the 1-based line the note is about within <see cref="Source"/>.
    /// </summary>
    /// <value>The line, or <see langword="null"/> when unknown.</value>
    public int? Line { get; }

    /// <summary>
    ///     Gets the 1-based column the note is about within <see cref="Source"/>.
    /// </summary>
    /// <value>The column, or <see langword="null"/> when unknown.</value>
    public int? Column { get; }

    /// <summary>
    ///     Formats the note as one line of output.
    /// </summary>
    /// <remarks>
    ///     An informational note is <c>info: message</c>. A warning is <c>file(line,column): warning: message</c>;
    ///     parts of the location that are unknown are omitted, as for a settings diagnostic, and with no
    ///     location at all it is <c>warning: message</c>. Thread-safe.
    /// </remarks>
    /// <returns>The formatted note.</returns>
    public string Format()
    {
        // An informational note is about the run as a whole and carries no location
        if (Severity == SpellCheckNoteSeverity.Info)
        {
            return $"info: {Message}";
        }

        // A warning is positioned at the setting it is about, from whichever parts are known
        var location = Source ?? string.Empty;
        if (Line is not null && Column is not null)
        {
            location += $"({Line},{Column})";
        }

        return location.Length == 0 ? $"warning: {Message}" : $"{location}: warning: {Message}";
    }

    /// <summary>
    ///     Formats the note as one line of output.
    /// </summary>
    /// <remarks>
    ///     The same text as <see cref="Format"/>.
    /// </remarks>
    /// <returns>The formatted note.</returns>
    public override string ToString() => Format();
}
