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

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     The outcome of loading a settings file: either settings or the errors that prevented them.
/// </summary>
/// <remarks>
///     Returning a result instead of throwing lets a caller report every configuration problem at
///     once. The invariant is that <see cref="Settings"/> is non-null exactly when
///     <see cref="Diagnostics"/> is empty, so a caller never acts on settings that were only
///     partially understood. Instances are immutable and thread-safe.
/// </remarks>
public sealed class SettingsLoadResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsLoadResult"/> class.
    /// </summary>
    /// <remarks>
    ///     Internal because only the loader creates results. The settings are discarded when there
    ///     are diagnostics, which enforces the class invariant in one place.
    /// </remarks>
    /// <param name="settings">The mapped settings. Must not be null when there are no diagnostics.</param>
    /// <param name="diagnostics">The errors found, in source order. Must not be null.</param>
    internal SettingsLoadResult(TextLintSettings settings, IReadOnlyList<SettingsDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
        Settings = diagnostics.Count == 0 ? settings : null;
    }

    /// <summary>
    ///     Gets the loaded settings.
    /// </summary>
    /// <value>The settings, or <see langword="null"/> when <see cref="Diagnostics"/> is not empty.</value>
    public TextLintSettings? Settings { get; }

    /// <summary>
    ///     Gets the errors found while loading, in source order.
    /// </summary>
    /// <value>An empty list when loading succeeded.</value>
    public IReadOnlyList<SettingsDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets a value indicating whether loading produced settings.
    /// </summary>
    /// <value><see langword="true"/> when there are no diagnostics; otherwise <see langword="false"/>.</value>
    public bool Succeeded => Settings is not null;
}
