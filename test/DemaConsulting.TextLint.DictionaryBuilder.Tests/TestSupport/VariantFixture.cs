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

// cspell:ignore colour

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

/// <summary>
///     Builds the small word-list tree that the spelling-variant tests share.
/// </summary>
/// <remarks>
///     One tree exercises every branch of the rule: the American and British lists define the
///     variants; the technical dictionary has a language list, a general list, and a keep list.
///     It is test support, not a shipped component.
/// </remarks>
internal static class VariantFixture
{
    /// <summary>
    ///     The command-line arguments that declare the fixture's dictionaries and the variant rule.
    /// </summary>
    /// <param name="temp">The temporary directory that holds the fixture's input and output folders.</param>
    /// <returns>The complete argument list for the builder.</returns>
    public static string[] Arguments(TempDirectory temp) =>
    [
        "--input", temp.PathOf("input"),
        "--output", temp.PathOf("out"),
        "en-US", "en-GB", "technical",
        "--technical", "technical",
        "--variants", "en-US",
        "--variants", "en-GB"
    ];

    /// <summary>
    ///     Creates the fixture's word lists.
    /// </summary>
    /// <remarks>
    ///     American variants: favorite, color, tokenizer. British variants: colour, grey. The
    ///     language list has color and grey (variants) and void (not a variant). The general list has
    ///     favorite, colour, and tokenizer (variants) and kubernetes (not a variant). The keep list
    ///     has tokenizer. The expected technical words are therefore color, grey, void, tokenizer, and
    ///     kubernetes; favorite and colour are stripped.
    /// </remarks>
    /// <param name="temp">The temporary directory to create the input folder in.</param>
    public static void Create(TempDirectory temp)
    {
        temp.CreateFile("input/en-US/us.txt", "favorite\ncolor\ntokenizer\n");
        temp.CreateFile("input/en-GB/gb.txt", "colour\ngrey\n");
        temp.CreateFile("input/technical/language/lang.txt", "color\ngrey\nvoid\n");
        temp.CreateFile("input/technical/general/general.txt", "favorite\ncolour\ntokenizer\nkubernetes\n");
        temp.CreateFile("input/technical/variant-keep.txt", "# reviewed\ntokenizer\n");
    }
}
