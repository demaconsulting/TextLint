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

// cspell:ignore colour neighbour organise favourite kubernetes xyzzyq cafè tabel καλημέρα catalogue labelled honoured modelling ageing curser operants programing milage writeable unannotated mockable honour analyse colourize decolourize colourized glamourize glamourized unmanoeuvrable

using System.Collections.Concurrent;
using DemaConsulting.TextLint.Dictionaries;

namespace DemaConsulting.TextLint.Tests.Dictionaries;

/// <summary>
///     Subsystem tests for the Dictionaries subsystem.
/// </summary>
/// <remarks>
///     These tests exercise <see cref="SpellingDictionary"/> and the dictionary image reader
///     together, through the embedded dictionaries of the library assembly. The only dependency
///     outside the subsystem is the shared format definition that the dictionary builder owns,
///     which both units compile; it is used for real.
/// </remarks>
public class DictionariesSubsystemTests
{
    /// <summary>
    ///     The number of characters in a word that is longer than any dictionary word.
    /// </summary>
    private const int OverlongWordLength = 65;

    /// <summary>
    ///     The number of lookups each parallel test performs.
    /// </summary>
    private const int ParallelLookups = 20_000;

    /// <summary>
    ///     Test that listed and unlisted words are reported correctly.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_ListedAndUnlistedWords_ReportsCorrectly()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: listed words are known and unlisted ones are not
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("table")),
            () => Assert.True(dictionary.IsKnown("neighbor")),
            () => Assert.False(dictionary.IsKnown("xyzzyq")),
            () => Assert.False(dictionary.IsKnown("tabel")));
    }

    /// <summary>
    ///     Test that empty, blank, and overlong text is unknown and never throws.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_EmptyWhitespaceAndOverlongWords_AreUnknown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: none of these is a word
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown(string.Empty)),
            () => Assert.False(dictionary.IsKnown("   ")),
            () => Assert.False(dictionary.IsKnown(new string('a', OverlongWordLength))));
    }

    /// <summary>
    ///     Test that letter case does not change whether a word is known.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_DifferentCase_IsKnown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: every capitalization is known
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("table")),
            () => Assert.True(dictionary.IsKnown("Table")),
            () => Assert.True(dictionary.IsKnown("TABLE")),
            () => Assert.True(dictionary.IsKnown("Paris")),
            () => Assert.True(dictionary.IsKnown("PARIS")));
    }

    /// <summary>
    ///     Test that the typographic apostrophe matches the straight apostrophe.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_TypographicApostrophe_IsKnown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: the contraction is known with both apostrophes
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("don't")),
            () => Assert.True(dictionary.IsKnown("don\u2019t")));
    }

    /// <summary>
    ///     Test that both the accented and the unaccented spelling of a source word are known while a wrong accent is not.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_AccentedAndUnaccentedSpelling_BothKnown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: accents are significant
        Assert.Multiple(
            () => Assert.True(dictionary.IsKnown("café")),
            () => Assert.True(dictionary.IsKnown("cafe")),
            () => Assert.False(dictionary.IsKnown("cafè")));
    }

    /// <summary>
    ///     Test that text in another script is unknown.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_OtherScript_IsUnknown()
    {
        // Arrange: the default selection
        var dictionary = SpellingDictionary.Default;

        // Act / Assert: nothing outside Latin-1 is known
        Assert.Multiple(
            () => Assert.False(dictionary.IsKnown("日本語")),
            () => Assert.False(dictionary.IsKnown("καλημέρα")));
    }

    /// <summary>
    ///     Test that the American, British, and combined selections each know their own spellings.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_AmericanBritishAndBoth_SelectExpectedSpellings()
    {
        // Arrange: the three selections
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");
        var both = SpellingDictionary.ForLanguages("en-US,en-GB");

        // Act / Assert: each knows its own spellings, and the combination knows both
        Assert.Multiple(
            () => Assert.True(american.IsKnown("neighbor")),
            () => Assert.False(american.IsKnown("neighbour")),
            () => Assert.False(british.IsKnown("neighbor")),
            () => Assert.True(british.IsKnown("neighbour")),
            () => Assert.True(both.IsKnown("neighbor")),
            () => Assert.True(both.IsKnown("neighbour")));
    }

    /// <summary>
    ///     Test that an unsupported language is rejected with an error that names it.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_UnsupportedLanguage_ThrowsNamingLanguage()
    {
        // Arrange: no setup required

        // Act: ask for a language the library does not have
        var exception = Assert.Throws<ArgumentException>(() => SpellingDictionary.ForLanguages("en-AU"));

        // Assert: the error names the language
        Assert.Contains("en-AU", exception.Message);
    }

    /// <summary>
    ///     Test that technical terms are known in every language selection.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_TechnicalTermInEveryLanguage_IsKnown()
    {
        // Arrange: the three selections
        var selections = new[] { "en-US", "en-GB", "en-US,en-GB" }.Select(SpellingDictionary.ForLanguages).ToArray();

        // Act / Assert: the technical term is known everywhere
        Assert.All(selections, dictionary => Assert.True(dictionary.IsKnown("kubernetes")));
    }

    /// <summary>
    ///     Test that equal selections share one instance.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_SameSelection_ReturnsSameInstance() =>
        // Arrange / Act / Assert: two requests for the same selection return the same object
        Assert.Same(SpellingDictionary.ForLanguages("en-GB"), SpellingDictionary.ForLanguages(" en-gb "));

    /// <summary>
    ///     Test that callers on many threads get consistent answers from the shared dictionaries.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_ParallelCallers_ReturnConsistentAnswers()
    {
        // Arrange: the three selections, with the answers expected for a spelling that differs between them
        var selections = new[]
        {
            (Dictionary: SpellingDictionary.ForLanguages("en-US"), Neighbor: true, Neighbour: false),
            (Dictionary: SpellingDictionary.ForLanguages("en-GB"), Neighbor: false, Neighbour: true),
            (Dictionary: SpellingDictionary.ForLanguages("en-US,en-GB"), Neighbor: true, Neighbour: true),
        };
        var failures = new ConcurrentBag<string>();

        // Act: query every selection from many threads at once
        Parallel.For(0, ParallelLookups, i =>
        {
            var (dictionary, neighbor, neighbour) = selections[i % selections.Length];
            if (dictionary.IsKnown("neighbor") != neighbor || dictionary.IsKnown("neighbour") != neighbour)
            {
                failures.Add($"selection {i % selections.Length}");
            }
        });

        // Assert: every thread saw the right answers
        Assert.Empty(failures);
    }

    /// <summary>
    ///     Test that each embedded dictionary contributes words that no other dictionary supplies.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_ShippedVocabulary_ContainsSampleWordsOfEachPart()
    {
        // Arrange: words that come from exactly one of the four dictionaries, and the selections
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act / Assert: the common words are in both selections, each regional word is in its own
        // selection only, and the technical terms are in both
        Assert.Multiple(
            () => Assert.True(american.IsKnown("table") && british.IsKnown("table")),
            () => Assert.True(american.IsKnown("favorite") && !british.IsKnown("favorite")),
            () => Assert.True(british.IsKnown("organise") && !american.IsKnown("organise")),
            () => Assert.True(american.IsKnown("kubernetes") && british.IsKnown("kubernetes")));
    }

    /// <summary>
    ///     Test that derived forms of English words, which the base word list alone does not have, are known in every selection.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_DerivedForms_AreKnownInEveryLanguage()
    {
        // Arrange: derived forms that are common in technical writing, and the three selections
        var words = new[] { "testability", "reviewable", "precomputed", "unannotated", "untestable", "lookups", "mockable" };
        var selections = new[] { "en-US", "en-GB", "en-US,en-GB" }.Select(SpellingDictionary.ForLanguages).ToArray();

        // Act: look up every word in every selection
        var unknown = words.Where(word => selections.Any(selection => !selection.IsKnown(word))).ToArray();

        // Assert: all of them are known everywhere
        Assert.Empty(unknown);
    }

    /// <summary>
    ///     Test that British-only spellings are known for British English only, so that the language selection still decides them.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_BritishOnlySpellings_AreKnownOnlyForBritish()
    {
        // Arrange: British spellings that other English word lists also accept for American English
        var words = new[] { "catalogue", "labelled", "honoured", "modelling", "colour", "ageing" };
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act: look each word up in both selections
        var knownToAmerican = words.Where(word => american.IsKnown(word)).ToArray();
        var unknownToBritish = words.Where(word => !british.IsKnown(word)).ToArray();

        // Assert: no American selection knows them and the British selection knows all of them
        Assert.Empty(knownToAmerican);
        Assert.Empty(unknownToBritish);
    }

    /// <summary>
    ///     Test that a capitalized form of a regional spelling and an -ize form do not carry a word into the wrong selection.
    /// </summary>
    /// <remarks>
    ///     Lookups fold letter case and British English is -ise only. Spelling checks do not know the part of speech,
    ///     so words such as license and practice, whose British form differs by part of speech, are accepted in
    ///     both forms by British English; they are not part of this test.
    /// </remarks>
    [Fact]
    public void DictionariesSubsystem_ForLanguages_CapitalizedAndIzeForms_DoNotCrossRegions()
    {
        // Arrange: capitalized American and British spellings, and -ize forms with a British -ise counterpart
        var americanOnly = new[] { "Honor", "HONOR", "Judgment", "Recognize", "Unmaneuverable" };
        var britishOnly = new[] { "Honour", "HONOUR", "Organise", "Analyse", "Unmanoeuvrable" };
        var izeForms = new[] { "colourize", "Glamourize", "decolourize", "colourized", "recognize" };
        var american = SpellingDictionary.ForLanguages("en-US");
        var british = SpellingDictionary.ForLanguages("en-GB");

        // Act: look the words up in the selection of the other region
        var americanKnownToBritish = americanOnly.Where(word => british.IsKnown(word)).ToArray();
        var britishKnownToAmerican = britishOnly.Where(word => american.IsKnown(word)).ToArray();
        var izeKnownToBritish = izeForms.Where(word => british.IsKnown(word)).ToArray();

        // Assert: no spelling crossed, and British English knows no -ize form
        Assert.Multiple(
            () => Assert.Empty(americanKnownToBritish),
            () => Assert.Empty(britishKnownToAmerican),
            () => Assert.Empty(izeKnownToBritish));
    }

    /// <summary>
    ///     Test that words of the reviewed exclusions list are unknown in every selection, while accepted variants stay known.
    /// </summary>
    [Fact]
    public void DictionariesSubsystem_IsKnown_ExcludedWords_AreUnknownInEveryLanguage()
    {
        // Arrange: words that imported lists contain but that are almost always a typo of another word, and accepted variants that are not excluded
        var excluded = new[] { "curser", "operants", "programing", "Curser" };
        var variants = new[] { "milage", "writeable" };
        var selections = new[] { "en-US", "en-GB", "en-US,en-GB" }.Select(SpellingDictionary.ForLanguages).ToArray();

        // Act: look the words up in every selection
        var knownExcluded = excluded.Where(word => selections.Any(selection => selection.IsKnown(word))).ToArray();
        var unknownVariants = variants.Where(word => selections.Any(selection => !selection.IsKnown(word))).ToArray();

        // Assert: no excluded word is known and every accepted variant is
        Assert.Empty(knownExcluded);
        Assert.Empty(unknownVariants);
    }
}
