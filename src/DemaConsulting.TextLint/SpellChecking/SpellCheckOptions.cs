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
///     Options that control how <see cref="SpellChecker"/> runs, as opposed to what it checks.
/// </summary>
/// <remarks>
///     What to check comes from the settings; these options are about the execution. They are a separate
///     type so that further options can be added without changing method signatures. The type is an
///     immutable record, so an options value can be shared freely and a modified copy derived with a
///     <c>with</c> expression.
/// </remarks>
public sealed record SpellCheckOptions
{
    /// <summary>
    ///     Backing store for <see cref="MaxDegreeOfParallelism"/>.
    /// </summary>
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount;

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
