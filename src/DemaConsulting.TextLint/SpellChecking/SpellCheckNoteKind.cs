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
///     The kinds of note a run can attach to its result.
/// </summary>
/// <remarks>
///     A note is information about how the settings were applied, not a finding about a file, so it never
///     makes a run fail. The kind lets a consumer decide what to show; the wording is the library's.
/// </remarks>
public enum SpellCheckNoteKind
{
    /// <summary>
    ///     A language given with the options replaced the language of an override that matched a checked file.
    /// </summary>
    LanguageReplaced,

    /// <summary>
    ///     Files were selected, but the settings disable every one of them.
    /// </summary>
    AllFilesDisabled,

    /// <summary>
    ///     An override matched none of the selected files.
    /// </summary>
    OverrideUnmatched,
}
