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

namespace DemaConsulting.TextLint.Configuration;

/// <summary>
///     What the overrides say about one file: whether it is checked, which overrides match it, and which of them
///     change the settings of the checker.
/// </summary>
/// <remarks>
///     An immutable value produced by <see cref="SettingsOverrideResolver"/>. The matching overrides are kept
///     separately from the ones that carry checker settings, because every matching override counts as used
///     (so it is not reported as matching no file) but only the latter need a differently configured checker.
/// </remarks>
/// <param name="Enabled"><see langword="true"/> when the file is checked.</param>
/// <param name="MatchedOverrides">The zero-based indices of every override that matches the file, ascending.</param>
/// <param name="SettingsOverrides">The zero-based indices of the matching overrides that set checker settings, ascending.</param>
internal sealed record FileResolution(bool Enabled, ImmutableArray<int> MatchedOverrides, ImmutableArray<int> SettingsOverrides)
{
    /// <summary>
    ///     Gets the key of the combination of overrides that configure the checker.
    /// </summary>
    /// <value>The indices joined with commas; empty when the top-level settings apply as they are.</value>
    internal string SettingsKey => string.Join(',', SettingsOverrides);
}
