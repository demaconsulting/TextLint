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
///     The kind of finding that a <see cref="SpellingIssue"/> reports.
/// </summary>
/// <remarks>
///     Consumers choose how to present each kind: an unknown word is usually a typo to fix or a word to
///     add to the settings, a flagged word is a word the project forbids, an unsupported directive is
///     a comment the author expects to work but that has no effect, and an invalid directive is a supported
///     directive whose argument cannot be used.
/// </remarks>
public enum SpellingIssueKind
{
    /// <summary>
    ///     The word is not in the dictionaries or in any word list of the settings or the document.
    /// </summary>
    UnknownWord,

    /// <summary>
    ///     The word is listed in <c>flagWords</c>, so it is reported even though it may be a known word.
    /// </summary>
    FlaggedWord,

    /// <summary>
    ///     A comment contains a directive that cspell supports but this engine does not, so it has no effect.
    /// </summary>
    UnsupportedDirective,

    /// <summary>
    ///     A comment contains a directive that this engine supports, but its argument cannot be used: an
    ///     <c>ignoreRegExp</c> directive whose pattern is invalid, unsupported, or names no pattern, so it has
    ///     no effect. The <see cref="SpellingIssue.Detail"/> gives the reason.
    /// </summary>
    InvalidDirective,
}
