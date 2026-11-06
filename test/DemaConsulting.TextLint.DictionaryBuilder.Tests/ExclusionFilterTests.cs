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

// cspell:ignore curser pilar

using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests;

/// <summary>
///     Unit tests for the <see cref="ExclusionFilter"/> class.
/// </summary>
/// <remarks>
///     The rule is a pure function over sets of folded words, so these tests need no file system
///     and no test doubles. The unit's one documented dependency, <see cref="DictionaryFormat"/>, only
///     supplies the capitalization flag constants.
/// </remarks>
public class ExclusionFilterTests
{
    /// <summary>
    ///     Builds a word set in which every word has the lower-case flag.
    /// </summary>
    /// <param name="words">The folded words.</param>
    /// <returns>The word set.</returns>
    private static Dictionary<string, uint> Words(params string[] words) =>
        words.ToDictionary(word => word, _ => DictionaryFormat.CaseLower, StringComparer.Ordinal);

    /// <summary>
    ///     Test that an excluded word is removed from every dictionary that has it and from no other word.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_ExcludedWordInSeveralDictionaries_IsRemovedFromEach()
    {
        // Arrange: two dictionaries that both have the word to exclude, and one that does not
        var first = Words("apple", "curser");
        var second = Words("curser", "banana");
        var third = Words("cherry");

        // Act: exclude the word
        var result = ExclusionFilter.Apply([first, second, third], Words("curser"));

        // Assert: the word is gone from the first two, every other word remains, and the third is unchanged
        Assert.Equal(["apple"], result.Dictionaries[0].Keys);
        Assert.Equal(["banana"], result.Dictionaries[1].Keys);
        Assert.Equal(["cherry"], result.Dictionaries[2].Keys);
    }

    /// <summary>
    ///     Test that the number of words removed from each dictionary is counted.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_ExcludedWords_CountsRemovalsPerDictionary()
    {
        // Arrange: dictionaries from which two, one, and no excluded words will be removed
        var dictionaries = new[] { Words("curser", "pilar", "apple"), Words("pilar"), Words("apple") };

        // Act: exclude two words
        var result = ExclusionFilter.Apply(dictionaries, Words("curser", "pilar"));

        // Assert: the counts follow the input order
        Assert.Equal([2, 1, 0], result.Removed);
    }

    /// <summary>
    ///     Test that an exclusion that no dictionary has is reported, in ordinal order, and one that removed a word is not.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_ExclusionInNoDictionary_IsReportedInOrder()
    {
        // Arrange: a dictionary with one of three excluded words
        var dictionaries = new[] { Words("apple", "curser") };

        // Act: exclude that word and two that nothing has, given out of order
        var result = ExclusionFilter.Apply(dictionaries, Words("zebra", "curser", "aardvark"));

        // Assert: only the two unused words are reported, in ordinal order
        Assert.Equal(["aardvark", "zebra"], result.UnusedWords);
    }

    /// <summary>
    ///     Test that a dictionary is unchanged when the exclusions list has no words.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_NoExclusions_KeepsEveryWord()
    {
        // Arrange: a dictionary with capitalization flags and no exclusions
        var words = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["apple"] = DictionaryFormat.CaseLower,
            ["paris"] = DictionaryFormat.CaseCapitalized,
        };

        // Act: apply an empty exclusions list
        var result = ExclusionFilter.Apply([words], Words());

        // Assert: every word is kept with its flags, nothing is removed, and nothing is unused
        Assert.Equal(words.OrderBy(pair => pair.Key, StringComparer.Ordinal), result.Dictionaries[0].OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal([0], result.Removed);
        Assert.Empty(result.UnusedWords);
    }

    /// <summary>
    ///     Test that the capitalization flags of the words that remain are preserved.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_RemainingWords_KeepTheirFlags()
    {
        // Arrange: a dictionary where the remaining word has a capitalization form other than lower case
        var words = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["curser"] = DictionaryFormat.CaseLower,
            ["paris"] = DictionaryFormat.CaseCapitalized | DictionaryFormat.CaseUpper,
        };

        // Act: exclude the other word
        var result = ExclusionFilter.Apply([words], Words("curser"));

        // Assert: the remaining word has exactly its original flags
        Assert.Equal(DictionaryFormat.CaseCapitalized | DictionaryFormat.CaseUpper, result.Dictionaries[0]["paris"]);
    }

    /// <summary>
    ///     Test that the inputs are not modified.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Apply_Inputs_AreNotModified()
    {
        // Arrange: a dictionary and an exclusions list
        var words = Words("apple", "curser");
        var exclusions = Words("curser");

        // Act: apply the exclusions
        ExclusionFilter.Apply([words], exclusions);

        // Assert: both inputs still hold their original words
        Assert.Equal(["apple", "curser"], words.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["curser"], exclusions.Keys);
    }

    /// <summary>
    ///     Test that changing the collections a result was built from does not change the result.
    /// </summary>
    [Fact]
    public void ExclusionFilter_Result_SourceMutatedAfterConstruction_ResultUnchanged()
    {
        // Arrange: a result built from a dictionary, a count list, and an unused-word list that the caller keeps
        var words = Words("apple");
        var removed = new List<int> { 1 };
        var unused = new List<string> { "zebra" };
        var result = new ExclusionFilterResult([words], removed, unused);

        // Act: the caller changes all three collections afterwards
        words["banana"] = DictionaryFormat.CaseLower;
        removed.Add(2);
        unused.Add("yak");

        // Assert: the result still holds exactly the original contents, and even a cast cannot change the words
        Assert.Equal(["apple"], result.Dictionaries[0].Keys);
        Assert.Equal([1], result.Removed);
        Assert.Equal(["zebra"], result.UnusedWords);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, uint>)result.Dictionaries[0]).Add("cherry", DictionaryFormat.CaseLower));
    }
}
