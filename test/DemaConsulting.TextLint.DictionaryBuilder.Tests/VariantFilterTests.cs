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

using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="VariantFilter"/> class.
/// </summary>
/// <remarks>
///     The rule is a pure function over sets of folded words, so these tests need no file system
///     and no test doubles. The unit's one documented dependency, <see cref="DictionaryFormat"/>, only
///     supplies the capitalization flag constants.
/// </remarks>
public class VariantFilterTests
{
    /// <summary>
    ///     Builds a word set in which every word has the lower-case flag.
    /// </summary>
    /// <param name="words">The folded words.</param>
    /// <returns>The word set.</returns>
    private static Dictionary<string, uint> Words(params string[] words) =>
        words.ToDictionary(word => word, _ => DictionaryFormat.CaseLower, StringComparer.Ordinal);

    /// <summary>
    ///     Applies the rule with the given sets.
    /// </summary>
    /// <param name="general">The general words.</param>
    /// <param name="language">The language words.</param>
    /// <param name="keep">The keep-list words.</param>
    /// <param name="variants">The variant words.</param>
    /// <returns>The filter result.</returns>
    private static VariantFilterResult Apply(
        Dictionary<string, uint> general,
        Dictionary<string, uint> language,
        Dictionary<string, uint> keep,
        params string[] variants) =>
        VariantFilter.Apply(general, language, keep, new HashSet<string>(variants, StringComparer.Ordinal));

    /// <summary>
    ///     Test that a general-list variant spelling that nothing exempts is removed.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_GeneralVariant_IsStripped()
    {
        // Arrange: a general list with a variant word and an ordinary word
        var general = Words("favorite", "kubernetes");

        // Act: apply the rule with no language or keep words
        var result = Apply(general, Words("void"), Words(), "favorite");

        // Assert: the variant is gone, the ordinary word and the language word remain, and one word was stripped
        Assert.Equal(["kubernetes", "void"], result.Words.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(1, result.Stripped);
    }

    /// <summary>
    ///     Test that changing the collections a result was built from does not change the result.
    /// </summary>
    [Fact]
    public void VariantFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged()
    {
        // Arrange: a result built from a dictionary and a list that the caller keeps
        var words = Words("kubernetes");
        var notVariants = new List<string> { "nuget" };
        var result = new VariantFilterResult(words, 1, 2, 3, notVariants);

        // Act: the caller changes both collections afterwards
        words["void"] = DictionaryFormat.CaseLower;
        notVariants.Add("npm");

        // Assert: the result still holds exactly the original contents, and even a cast cannot change the words
        Assert.Equal(["kubernetes"], result.Words.Keys);
        Assert.Equal(["nuget"], result.KeepWordsNotVariants);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, uint>)result.Words).Add("cherry", DictionaryFormat.CaseLower));
    }

    /// <summary>
    ///     Test that a non-variant general word is kept.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_NonVariantGeneralWord_IsKept()
    {
        // Arrange: a general list with only ordinary words
        var general = Words("kubernetes", "nuget");

        // Act: apply the rule with an unrelated variant
        var result = Apply(general, Words("void"), Words(), "favorite");

        // Assert: nothing was stripped
        Assert.Equal(0, result.Stripped);
        Assert.Contains("nuget", result.Words.Keys);
        Assert.Contains("kubernetes", result.Words.Keys);
    }

    /// <summary>
    ///     Test that a general variant that a language list also has is kept.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_GeneralVariantInLanguageList_IsKept()
    {
        // Arrange: the variant word color is in both a general and a language list
        var general = Words("color");
        var language = Words("color");

        // Act: apply the rule with color as a variant
        var result = Apply(general, language, Words(), "color");

        // Assert: the word is kept, counted as kept via the language list, and not stripped
        Assert.Contains("color", result.Words.Keys);
        Assert.Equal(1, result.KeptViaLanguage);
        Assert.Equal(0, result.Stripped);
    }

    /// <summary>
    ///     Test that a general variant that is in the keep list is kept.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_GeneralVariantInKeepList_IsKept()
    {
        // Arrange: the variant word tokenizer is in a general list and in the keep list
        var general = Words("tokenizer");
        var keep = Words("tokenizer");

        // Act: apply the rule with tokenizer as a variant
        var result = Apply(general, Words("void"), keep, "tokenizer");

        // Assert: the word is kept, counted as kept via the keep list, and not stripped
        Assert.Contains("tokenizer", result.Words.Keys);
        Assert.Equal(1, result.KeptViaKeepList);
        Assert.Equal(0, result.Stripped);
        Assert.Empty(result.KeepWordsNotVariants);
    }

    /// <summary>
    ///     Test that a language-list variant is kept even when no general list has it.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_LanguageVariantOnly_IsKept()
    {
        // Arrange: a language list with a British variant and an empty general list
        var language = Words("grey");

        // Act: apply the rule with grey as a variant
        var result = Apply(Words("kubernetes"), language, Words(), "grey");

        // Assert: the language word is kept and counted
        Assert.Contains("grey", result.Words.Keys);
        Assert.Equal(1, result.KeptViaLanguage);
    }

    /// <summary>
    ///     Test that a keep-list word that is not a variant is reported, in order, and still kept.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_KeepWordNotVariant_IsReported()
    {
        // Arrange: a keep list with a variant and two words that are not variants
        var keep = Words("zebra", "tokenizer", "apple");

        // Act: apply the rule with only tokenizer as a variant
        var result = Apply(Words("kubernetes"), Words("void"), keep, "tokenizer");

        // Assert: the two non-variants are reported in ordinal order and only the variant is counted
        Assert.Equal(["apple", "zebra"], result.KeepWordsNotVariants);
        Assert.Equal(1, result.KeptViaKeepList);
    }

    /// <summary>
    ///     Test that the capitalization flags of a word are merged across the lists that have it.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_WordInSeveralLists_MergesFlags()
    {
        // Arrange: one word with different capitalization flags in the general and language lists
        var general = new Dictionary<string, uint> { ["java"] = DictionaryFormat.CaseCapitalized };
        var language = new Dictionary<string, uint> { ["java"] = DictionaryFormat.CaseLower };

        // Act: apply the rule
        var result = Apply(general, language, Words());

        // Assert: the kept word carries both flags
        Assert.Equal(DictionaryFormat.CaseLower | DictionaryFormat.CaseCapitalized, result.Words["java"]);
    }

    /// <summary>
    ///     Test that the counts add up for a mixed input.
    /// </summary>
    [Fact]
    public void VariantFilter_Apply_MixedInput_CountsKeptAndStripped()
    {
        // Arrange: two language variants, one keep variant, two stripped variants, and an ordinary word
        var language = Words("color", "grey", "void");
        var general = Words("favorite", "colour", "tokenizer", "kubernetes");
        var keep = Words("tokenizer");

        // Act: apply the rule
        var result = Apply(general, language, keep, "favorite", "color", "tokenizer", "colour", "grey");

        // Assert: the counts and the remaining words are as designed
        Assert.Equal(2, result.KeptViaLanguage);
        Assert.Equal(1, result.KeptViaKeepList);
        Assert.Equal(2, result.Stripped);
        Assert.Equal(["color", "grey", "kubernetes", "tokenizer", "void"], result.Words.Keys.Order(StringComparer.Ordinal));
    }
}
