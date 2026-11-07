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

namespace DemaConsulting.TextLint.SpellChecking;

/// <summary>
///     One finding of the spell checker: an unknown word, a flagged word, an unsupported directive, or an
///     invalid directive, with its position.
/// </summary>
/// <remarks>
///     Findings are returned as data rather than thrown so that a caller can print or process every
///     one in a uniform way. The position is the position of the word as written in the checked text,
///     so an editor or CI log can jump to it. Instances are immutable and therefore thread-safe; the
///     suggestions are copied into an immutable array when the finding is created.
/// </remarks>
public sealed class SpellingIssue
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpellingIssue"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the spell checker produces findings; consumers only read them.
    /// </remarks>
    /// <param name="filePath">The source name of the checked text (normally a relative file path), or null.</param>
    /// <param name="line">The 1-based line of the finding.</param>
    /// <param name="column">The 1-based column of the finding, in UTF-16 code units.</param>
    /// <param name="text">The text of the finding. Must not be null.</param>
    /// <param name="kind">The kind of finding.</param>
    /// <param name="suggestions">The suggested replacements. Must not be the uninitialized default array.</param>
    /// <param name="detail">The reason an invalid directive cannot be used; null for other kinds.</param>
    internal SpellingIssue(
        string? filePath,
        int line,
        int column,
        string text,
        SpellingIssueKind kind,
        ImmutableArray<string> suggestions,
        string? detail = null)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
        Text = text;
        Kind = kind;
        Suggestions = suggestions;
        Detail = detail;
    }

    /// <summary>
    ///     Gets the source name of the checked text.
    /// </summary>
    /// <value>
    ///     The name given to the checker, which for files checked by the spell checker is the path relative
    ///     to the root directory with forward slashes; <see langword="null"/> when no name was given.
    /// </value>
    public string? FilePath { get; }

    /// <summary>
    ///     Gets the 1-based line of the finding.
    /// </summary>
    /// <remarks>
    ///     Lines are separated by the line feed character only, so a carriage return alone does not start a
    ///     line; this is how cspell counts.
    /// </remarks>
    /// <value>The line number, starting at 1.</value>
    public int Line { get; }

    /// <summary>
    ///     Gets the 1-based column of the finding.
    /// </summary>
    /// <remarks>
    ///     Columns count UTF-16 code units from the start of the line, so a character outside the Basic
    ///     Multilingual Plane (such as an emoji) counts as two; this is how cspell counts.
    /// </remarks>
    /// <value>The column number, starting at 1.</value>
    public int Column { get; }

    /// <summary>
    ///     Gets the text of the finding.
    /// </summary>
    /// <value>
    ///     The text of the finding. For an unknown or flagged word, the word exactly as written in the text
    ///     (the whole token for a flagged list entry that contains digits or punctuation); for an unsupported
    ///     directive, its name; for an invalid directive, the pattern as written.
    /// </value>
    public string Text { get; }

    /// <summary>
    ///     Gets the kind of finding.
    /// </summary>
    /// <value>The kind of finding.</value>
    public SpellingIssueKind Kind { get; }

    /// <summary>
    ///     Gets the suggested replacements.
    /// </summary>
    /// <remarks>
    ///     Only flagged words carry suggestions, taken from the <c>flagWords</c> entry that matched.
    /// </remarks>
    /// <value>An immutable list; empty when there are no suggestions.</value>
    public IReadOnlyList<string> Suggestions { get; }

    /// <summary>
    ///     Gets the reason that an invalid directive cannot be used.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="SpellingIssueKind.InvalidDirective"/> findings carry a reason, which is the sentence
    ///     that says what is wrong with the pattern.
    /// </remarks>
    /// <value>The reason, or <see langword="null"/> for the other kinds of finding.</value>
    public string? Detail { get; }

    /// <summary>
    ///     Formats the finding in the compiler style <c>file(line,column): error: message</c>.
    /// </summary>
    /// <remarks>
    ///     The compiler style is understood by editors, build servers, and CI log parsers, so findings
    ///     become clickable without any tool-specific integration, and it is the style used for
    ///     <see cref="Configuration.SettingsDiagnostic"/> too. Without a file name the location is just the position.
    /// </remarks>
    /// <returns>The formatted finding.</returns>
    public override string ToString() => Format(FilePath);

    /// <summary>
    ///     Formats the finding in the compiler style, naming the file as the caller chooses.
    /// </summary>
    /// <remarks>
    ///     The checker names a file by its path relative to the root directory, but a caller that was given
    ///     absolute paths (the command-line tool) must show those instead, and must not copy the message
    ///     wording to do so. This keeps the format in one place: <see cref="ToString"/> is this method
    ///     applied to <see cref="FilePath"/>. Stateless apart from reading the finding; thread-safe.
    /// </remarks>
    /// <param name="filePath">The file name to show before the position; null shows only the position.</param>
    /// <returns>The formatted finding.</returns>
    public string Format(string? filePath)
    {
        // Describe the finding by its kind, adding any suggestions
        var message = Kind switch
        {
            SpellingIssueKind.UnknownWord => $"Unknown word '{Text}'",
            SpellingIssueKind.FlaggedWord => $"Flagged word '{Text}'",
            SpellingIssueKind.InvalidDirective => $"Cannot use the pattern '{Text}' of the directive 'ignoreRegExp': {Detail}",
            _ => $"The directive '{Text}' is not supported",
        };

        if (Suggestions.Count > 0)
        {
            message += $"; suggestions: {string.Join(", ", Suggestions)}";
        }

        return $"{filePath}({Line},{Column}): error: {message}";
    }
}
