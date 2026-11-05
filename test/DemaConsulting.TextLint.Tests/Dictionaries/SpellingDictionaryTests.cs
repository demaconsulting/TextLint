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

// cspell:ignore colour neighbour organise favourite tokenizer kubernetes powershell nuget textlint cafè xyzzyq καλημέρα

using System.Collections.Concurrent;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.Tests.Dictionaries;

/// <summary>
///     Unit tests for the <see cref="SpellingDictionary"/> class.
/// </summary>
/// <remarks>
///     The class selects among fixed embedded dictionaries, so the tests use the real embedded
///     dictionaries of the library, which are the unit's documented dependency, together with the
///     image reader and the shared format definitions behind it. Words are chosen to be
///     unambiguous in the data: <c>neighbor</c> and <c>favorite</c> are American spellings that
///     appear in no technical list, and <c>neighbour</c> and <c>organise</c> are British ones.
/// </remarks>
public class SpellingDictionaryTests
{
    /// <summary>
    ///     The number of characters in a word that is longer than any dictionary word.
    /// </summary>
    private const int OverlongWordLength = 65;

    /// <summary>
    ///     The number of characters in a very long text.
    /// </summary>
    private const int VeryLongTextLength = 10_000;

    /// <summary>
    ///     The number of lookups each parallel test performs.
    /// </summary>
    private const int ParallelLookups = 20_000;

    /// <summary>
    ///     The number of lookups performed to measure allocation after warm-up.
    /// </summary>
    private const int MeasuredLookups = 1_000;

    /// <summary>
    ///     Test that the default selection is American English.
    /// </summary>
    [Fact]
    public void SpellingDictionary_Default_IsAmerican()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act: look up an American-only and a British-only spelling
        var american = dictionary.IsKnown("neighbor");
        var british = dictionary.IsKnown("neighbour");

        // Assert: only the American spelling is known, and the default is the shared en-US selection
        Assert.Multiple(
            () => Assert.True(american),
            () => Assert.False(british),
            () => Assert.Same(SpellingDictionary.ForLanguages("en-US"), dictionary));
    }

    /// <summary>
    ///     Test that the language name <c>en</c> selects the same dictionaries as <c>en-US</c>.
    /// </summary>
    [Fact]
    public void SpellingDictionary_ForLanguages_En_SameAsEnUs() =>
        // Arrange / Act / Assert: both names return the same shared instance
        Assert.Same(SpellingDictionary.ForLanguages("en-US"), SpellingDictionary.ForLanguages("en"));

    /// <summary>
    ///     Test that language names ignore letter case and surrounding spaces.
    /// </summary>
    [Fact]
    public void SpellingDictionary_ForLanguages_LowerCaseAndSpaces_Accepted()
    {
        // Arrange: spellings of the same selections with odd case and spacing
        var american = SpellingDictionary.ForLanguages("EN-us");
        var both = SpellingDictionary.ForLanguages(" en-gb , EN-US ");

        // Act / Assert: they are the shared selections
        Assert.Multiple(
            () => Assert.Same(SpellingDictionary.Default, american),
            () => Assert.Same(SpellingDictionary.ForLanguages("en-US,en-GB"), both));
    }

    /// <summary>
    ///     Test that an unsupported language is rejected with an error that names it and lists the supported names.
    /// </summary>
    /// <param name="languages">The unsupported language selection.</param>
    /// <param name="offending">The part of it that the message must name.</param>
    [Theory]
    [InlineData("en-AU", "en-AU")]
    [InlineData("fr", "fr")]
    [InlineData("en-US, de", "de")]
    [InlineData("en;en-GB", "en;en-GB")]
    public void SpellingDictionary_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage(string languages, string offending)
    {
        // Arrange: no setup required

        // Act: ask for the unsupported selection
        var exception = Assert.Throws<ArgumentException>(() => SpellingDictionary.ForLanguages(languages));

        // Assert: the message names the offending value and lists the supported languages
        Assert.Multiple(
            () => Assert.Contains($"'{offending}'", exception.Message),
            () => Assert.Contains("en-GB", exception.Message),
            () => Assert.Equal("languages", exception.ParamName));
    }

    /// <summary>
    ///     Test that an empty or blank language list is rejected.
    /// </summary>
    /// <param name="languages">The empty or blank language list.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SpellingDictionary_ForLanguages_Empty_Throws(string languages)
    {
        // Arrange: no setup required

        // Act / Assert: an empty list names no language and is rejected
        Assert.Throws<ArgumentException>(() => SpellingDictionary.ForLanguages(languages));
    }

    /// <summary>
    ///     Test that an empty name inside a list is rejected.
    /// </summary>
    /// <param name="languages">A list with an empty name.</param>
    [Theory]
    [InlineData("en-US,,en-GB")]
    [InlineData("en-US,")]
    public void SpellingDictionary_ForLanguages_EmptyNameInList_Throws(string languages)
    {
        // Arrange: no setup required

        // Act / Assert: the empty name is rejected rather than ignored
        Assert.Throws<ArgumentException>(() => SpellingDictionary.ForLanguages(languages));
    }

    /// <summary>
    ///     Test that a null language list is rejected.
    /// </summary>
    [Fact]
    public void SpellingDictionary_ForLanguages_Null_Throws() =>
        // Arrange / Act / Assert: a null list is rejected
        Assert.Throws<ArgumentNullException>(() => SpellingDictionary.ForLanguages(null!));

    /// <summary>
    ///     Test that the supported languages are listed and cannot be changed.
    /// </summary>
    [Fact]
    public void SpellingDictionary_SupportedLanguages_ListsNames_CannotBeModified()
    {
        // Arrange: the supported languages
        var languages = SpellingDictionary.SupportedLanguages;

        // Act: try to change them through the list interface
        var list = Assert.IsAssignableFrom<IList<string>>(languages);

        // Assert: the names are listed and the list rejects changes
        Assert.Multiple(
            () => Assert.Equal(["en", "en-GB", "en-US"], languages),
            () => Assert.Throws<NotSupportedException>(() => list.Add("fr")));
    }

    /// <summary>
    ///     Test that each regional spelling is known only to the selection that includes its dictionary.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_WordOnlyInOneDictionary_IsKnownWhenSelected()
    {
        // Arrange: the three selections
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");
        var both = SpellingDictionary.ForLanguages("en-US,en-GB");

        // Act / Assert: each spelling is known exactly where its dictionary is selected
        Assert.Multiple(
            () => Assert.True(american.IsKnown("neighbor")),
            () => Assert.False(british.IsKnown("neighbor")),
            () => Assert.True(both.IsKnown("neighbor")),
            () => Assert.False(american.IsKnown("neighbour")),
            () => Assert.True(british.IsKnown("neighbour")),
            () => Assert.True(both.IsKnown("neighbour")));
    }

    /// <summary>
    ///     Test that a British-only word is unknown when only American English is selected.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_GbOnlyWordWithAmerican_IsUnknown() =>
        // Arrange / Act / Assert: the -ise spelling is not American
        Assert.False(SpellingDictionary.ForLanguages("en").IsKnown("organise"));

    /// <summary>
    ///     Test that words common to both dialects are known in every selection.
    /// </summary>
    /// <param name="languages">The language selection.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("en-US,en-GB")]
    public void SpellingDictionary_IsKnown_CommonWord_KnownInEverySelection(string languages)
    {
        // Arrange: a selection
        var dictionary = SpellingDictionary.ForLanguages(languages);

        // Act / Assert: ordinary English words are known
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("table")),
            () => Assert.True(dictionary.IsKnown("the")));
    }

    /// <summary>
    ///     Test that a word that is in no dictionary is unknown.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_UnknownWord_IsFalse() =>
        // Arrange / Act / Assert: a nonsense word is not known
        Assert.False(SpellingDictionary.Default.IsKnown("xyzzyq"));

    /// <summary>
    ///     Test that technical terms are known in every selection.
    /// </summary>
    /// <param name="languages">The language selection.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("en-US,en-GB")]
    public void SpellingDictionary_IsKnown_TechnicalWord_KnownForEveryLanguage(string languages)
    {
        // Arrange: a selection
        var dictionary = SpellingDictionary.ForLanguages(languages);

        // Act / Assert: terms from the language, general, and project lists are known
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("kubernetes")),
            () => Assert.True(dictionary.IsKnown("powershell")),
            () => Assert.True(dictionary.IsKnown("nuget")),
            () => Assert.True(dictionary.IsKnown("textlint")));
    }

    /// <summary>
    ///     Test that a programming term in a variant spelling that is on the reviewed list is known in every selection.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_ProgrammingTermInVariantSpelling_KnownForEveryLanguage()
    {
        // Arrange: both single-dialect selections
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: "tokenizer" is an American spelling that is a programming term, so both know it
        Assert.Multiple(
            () => Assert.True(american.IsKnown("tokenizer")),
            () => Assert.True(british.IsKnown("tokenizer")));
    }

    /// <summary>
    ///     Test that a general technical word in a variant spelling follows the selected language.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_VariantSpellingInGeneralList_FollowsLanguage()
    {
        // Arrange: "favorite" appears in a general technical list but is an American-only spelling
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: only the American selection knows it
        Assert.Multiple(
            () => Assert.True(american.IsKnown("favorite")),
            () => Assert.False(british.IsKnown("favorite")),
            () => Assert.True(british.IsKnown("favourite")));
    }

    /// <summary>
    ///     Test that a word is known in upper case, lower case, and mixed case.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_UpperCase_IsKnown()
    {
        // Arrange: the British selection
        var dictionary = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: every capitalization of a known word is known
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("colour")),
            () => Assert.True(dictionary.IsKnown("Colour")),
            () => Assert.True(dictionary.IsKnown("COLOUR")),
            () => Assert.True(dictionary.IsKnown("cOlOuR")));
    }

    /// <summary>
    ///     Test that a word listed only with a capital is known in every case.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_WordListedWithCapital_KnownInAnyCase()
    {
        // Arrange: the default selection, whose dictionaries list "Paris" with a capital
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: the word is known in any case
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("Paris")),
            () => Assert.True(dictionary.IsKnown("paris")),
            () => Assert.True(dictionary.IsKnown("PARIS")));
    }

    /// <summary>
    ///     Test that the typographic apostrophe is treated as the straight apostrophe.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_TypographicApostrophe_IsKnown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: contractions and possessives are known with either apostrophe
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("don't")),
            () => Assert.True(dictionary.IsKnown("don\u2019t")),
            () => Assert.True(dictionary.IsKnown("dog's")),
            () => Assert.True(dictionary.IsKnown("dog\u2019s")));
    }

    /// <summary>
    ///     Test that other punctuation is not stripped, so a leading apostrophe makes a word unknown.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_PunctuationAroundWord_IsLookedUpAsWritten()
    {
        // Arrange: the British selection
        var dictionary = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: the word with surrounding punctuation is not the word
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown("'colour")),
            () => Assert.False(dictionary.IsKnown("colour.")),
            () => Assert.False(dictionary.IsKnown("'")));
    }

    /// <summary>
    ///     Test that accents are significant while both the accented and unaccented spelling of a source word are known.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_Accented_IsKnown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: the accented word and its unaccented spelling are known, in any case, but a wrong accent is not
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("café")),
            () => Assert.True(dictionary.IsKnown("CAFÉ")),
            () => Assert.True(dictionary.IsKnown("cafe")),
            () => Assert.False(dictionary.IsKnown("cafè")));
    }

    /// <summary>
    ///     Test that text in another script, or with a lone surrogate, is unknown and does not throw.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_OtherScript_IsUnknown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: nothing outside Latin-1 can be known
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown("日本語")),
            () => Assert.False(dictionary.IsKnown("καλημέρα")),
            () => Assert.False(dictionary.IsKnown("\ud800")),
            () => Assert.False(dictionary.IsKnown("caf\u0301e")));
    }

    /// <summary>
    ///     Test that text longer than any word is unknown.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_VeryLongWord_IsUnknown()
    {
        // Arrange: texts of letters that are longer than any word the dictionaries can hold
        var dictionary = SpellingDictionary.Default;
        var overlong = new string('a', OverlongWordLength);
        var veryLong = new string('a', VeryLongTextLength);

        // Act / Assert: they are unknown and nothing throws
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown(overlong)),
            () => Assert.False(dictionary.IsKnown(veryLong)));
    }

    /// <summary>
    ///     Test that digits, spaces, and empty text are unknown.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_DigitsSpacesAndEmpty_AreUnknown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: none of these is a word
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown(string.Empty)),
            () => Assert.False(dictionary.IsKnown("   ")),
            () => Assert.False(dictionary.IsKnown("color1")),
            () => Assert.False(dictionary.IsKnown("123")),
            () => Assert.False(dictionary.IsKnown("col or")));
    }

    /// <summary>
    ///     Test that only the span passed in is looked up, so a word can be checked inside larger text without copying.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_SpanOfLargerText_ChecksOnlyTheSpan()
    {
        // Arrange: text in which "table" is a slice
        const string text = "the table xyzzyq";
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: the slices are judged on their own
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown(text.AsSpan(4, 5))),
            () => Assert.False(dictionary.IsKnown(text.AsSpan(10))));
    }

    /// <summary>
    ///     Test that a lookup does not allocate once the dictionaries are loaded.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_AfterWarmUp_DoesNotAllocate()
    {
        // Arrange: load the dictionaries and run each lookup path once so that nothing is left to initialize
        var dictionary = SpellingDictionary.ForLanguages("en-US,en-GB");
        _ = dictionary.IsKnown("table");
        _ = dictionary.IsKnown("xyzzyq");

        // Act: measure the bytes allocated by this thread across many hits and misses
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < MeasuredLookups; i++)
        {
            _ = dictionary.IsKnown("table");
            _ = dictionary.IsKnown("xyzzyq");
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: nothing was allocated
        Assert.Equal(0, allocated);
    }

    /// <summary>
    ///     Test that equivalent selections return the same instance whatever their spelling.
    /// </summary>
    [Fact]
    public void SpellingDictionary_ForLanguages_EquivalentSelections_ReturnSameInstance()
    {
        // Arrange: several spellings of each of the three selections
        var american = SpellingDictionary.ForLanguages("en");
        var british = SpellingDictionary.ForLanguages("en-GB");
        var both = SpellingDictionary.ForLanguages("en-US,en-GB");

        // Act / Assert: equivalent requests share one instance, and the selections differ from each other
        Assert.Multiple(
            () => Assert.Same(american, SpellingDictionary.ForLanguages(" EN-us ")),
            () => Assert.Same(american, SpellingDictionary.ForLanguages("en,en-US")),
            () => Assert.Same(british, SpellingDictionary.ForLanguages("en-gb,EN-GB")),
            () => Assert.Same(both, SpellingDictionary.ForLanguages("en-GB, en")),
            () => Assert.Same(both, SpellingDictionary.ForLanguages("en-US,en-GB,en")),
            () => Assert.NotSame(american, british),
            () => Assert.NotSame(american, both),
            () => Assert.NotSame(british, both));
    }

    /// <summary>
    ///     Test that lookups from many threads at once all return the correct answers.
    /// </summary>
    [Fact]
    public void SpellingDictionary_IsKnown_ParallelLookups_AllCorrect()
    {
        // Arrange: words with the answers expected from the British selection
        var dictionary = SpellingDictionary.ForLanguages("en-GB");
        (string Word, bool Expected)[] cases =
        [
            ("colour", true), ("neighbour", true), ("neighbor", false), ("kubernetes", true),
            ("xyzzyq", false), ("COLOUR", true), ("don\u2019t", true), ("日本語", false),
        ];
        var failures = new ConcurrentBag<string>();

        // Act: look the words up from many threads at the same time
        Parallel.For(0, ParallelLookups, i =>
        {
            var (word, expected) = cases[i % cases.Length];
            if (dictionary.IsKnown(word) != expected)
            {
                failures.Add(word);
            }
        });

        // Assert: every answer was right
        Assert.Empty(failures);
    }
}
