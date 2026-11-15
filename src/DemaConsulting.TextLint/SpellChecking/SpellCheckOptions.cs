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
///     Options that control how <see cref="SpellChecker"/> runs, beyond what the settings say.
/// </summary>
/// <remarks>
///     What to check comes from the settings; these options are about the execution and about the context of
///     the run: how many files are checked at once, which directory the <c>filename</c> patterns of the
///     settings' overrides are relative to, and a language that the caller wants to win over the settings.
///     They are a separate type so that further options can be added without changing method signatures. The
///     type is an immutable record, so an options value can be shared freely and a modified copy derived with a
///     <c>with</c> expression.
/// </remarks>
public sealed record SpellCheckOptions
{
    /// <summary>
    ///     Backing store for <see cref="MaxDegreeOfParallelism"/>.
    /// </summary>
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount;

    /// <summary>
    ///     Gets the directory that the <c>filename</c> patterns of the overrides are relative to.
    /// </summary>
    /// <remarks>
    ///     This is the directory of the settings, which is not always the directory the files are searched in:
    ///     a caller that checks files named by an absolute path searches in another directory, and must still
    ///     have its overrides applied to those files as if it had searched from the settings directory. A file
    ///     outside this directory matches no override. When null, the directory passed to
    ///     <see cref="SpellChecker.CheckAsync"/> is used. Not validated here; a relative path is taken relative to the
    ///     current directory.
    /// </remarks>
    /// <value>A directory path, or <see langword="null"/> (the default) to use the search directory.</value>
    public string? SettingsRootDirectory { get; init; }

    /// <summary>
    ///     Gets a language selection that wins over the language of the settings and of every override.
    /// </summary>
    /// <remarks>
    ///     This is how a command-line language option is applied: it is the more specific request, so it replaces
    ///     the top-level <see cref="Configuration.TextLintSettings.Language"/> and the language of each override. It accepts what
    ///     the settings accept (<c>en</c>, <c>en-US</c>, <c>en-GB</c>, or a comma-separated list). A language
    ///     that is not supported fails the run before any file is touched, as an unsupported language in the
    ///     settings does. When it replaces the language of an override that matched a checked file, and the two
    ///     select different dictionaries, the result carries a note saying so.
    /// </remarks>
    /// <value>The language text, or <see langword="null"/> (the default) to leave the languages as the settings say.</value>
    public string? Language { get; init; }

    /// <summary>
    ///     Gets the largest number of files that are checked at the same time.
    /// </summary>
    /// <remarks>
    ///     The default is the number of logical processors, which keeps every core busy. The results do not
    ///     depend on this number: they are identical, and in the same order, whatever it is; lower it to
    ///     leave processors free for other work or to reduce memory use, because each file being checked
    ///     is held in memory.
    /// </remarks>
    /// <value>A number of at least 1; the number of logical processors by default.</value>
    /// <exception cref="ArgumentOutOfRangeException">Thrown by the setter when the assigned value is less than 1.</exception>
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxDegreeOfParallelism = value;
        }
    }
}
