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

// cspell:ignore cafè

using System.Collections.ObjectModel;

namespace DemaConsulting.TextLint.Dictionaries;

/// <summary>
///     Answers whether a word is a known word of the selected English language or of the technical
///     vocabulary, using dictionaries embedded in the library.
/// </summary>
/// <remarks>
///     This is the library's word lookup: the question a spell checker asks of every word it
///     meets. The dictionaries ship inside the assembly, so no data files are needed at run time.
///     A selection always includes the words common to American and British English and the
///     technical terms (programming languages, tools, companies, file types, licenses); it adds the
///     American-only spellings, the British-only spellings, or both, according to the language
///     names given to <see cref="ForLanguages"/>. Only the three possible selections exist, and
///     each is a single shared instance, so asking for the same selection twice returns the same
///     object. A selection loads its dictionaries lazily, the first time a word is looked up, and
///     the loaded data is shared by every selection that uses it. A failed load (a missing or damaged
///     embedded dictionary, which means a defective build) is cached for the life of the process and is
///     not retried, so every lookup of the affected selection throws the same
///     <see cref="InvalidDataException"/>. Instances are immutable and
///     safe to use from any number of threads at the same time.
/// </remarks>
public sealed class SpellingDictionary
{
    /// <summary>
    ///     The language name that selects American English, the default.
    /// </summary>
    private const string AmericanLanguage = "en-US";

    /// <summary>
    ///     The language name that selects British English with -ise spellings.
    /// </summary>
    private const string BritishLanguage = "en-GB";

    /// <summary>
    ///     The language name that means American English, as cspell treats it.
    /// </summary>
    private const string EnglishLanguage = "en";

    /// <summary>
    ///     The character that separates language names in a selection.
    /// </summary>
    private const char LanguageSeparator = ',';

    /// <summary>
    ///     The start of the logical name of every embedded dictionary resource; it must match the
    ///     logical names that the library project gives the converted dictionary files.
    /// </summary>
    private const string ResourcePrefix = "DemaConsulting.TextLint.Dictionaries.";

    /// <summary>
    ///     The end of the logical name of every embedded dictionary resource, which is the extension
    ///     of the converted dictionary files.
    /// </summary>
    private const string ResourceSuffix = ".tld";

    /// <summary>
    ///     The name of the dictionary of words spelled the same in American and British English.
    /// </summary>
    private const string CommonDictionary = "en-common";

    /// <summary>
    ///     The name of the dictionary of American-only spellings.
    /// </summary>
    private const string AmericanDictionary = "en-US";

    /// <summary>
    ///     The name of the dictionary of British-only spellings.
    /// </summary>
    private const string BritishDictionary = "en-GB";

    /// <summary>
    ///     The name of the dictionary of technical terms.
    /// </summary>
    private const string TechnicalDictionary = "technical";

    /// <summary>
    ///     The language names that <see cref="ForLanguages"/> accepts, in alphabetical order.
    /// </summary>
    private static readonly ReadOnlyCollection<string> Languages =
        Array.AsReadOnly([EnglishLanguage, BritishLanguage, AmericanLanguage]);

    /// <summary>
    ///     The lazily loaded dictionary of words common to both dialects, shared by every selection.
    /// </summary>
    private static readonly Lazy<DictionaryImage> CommonImage = LoadLazily(CommonDictionary);

    /// <summary>
    ///     The lazily loaded dictionary of American-only spellings, shared by every selection that uses it.
    /// </summary>
    private static readonly Lazy<DictionaryImage> AmericanImage = LoadLazily(AmericanDictionary);

    /// <summary>
    ///     The lazily loaded dictionary of British-only spellings, shared by every selection that uses it.
    /// </summary>
    private static readonly Lazy<DictionaryImage> BritishImage = LoadLazily(BritishDictionary);

    /// <summary>
    ///     The lazily loaded dictionary of technical terms, shared by every selection.
    /// </summary>
    private static readonly Lazy<DictionaryImage> TechnicalImage = LoadLazily(TechnicalDictionary);

    /// <summary>
    ///     The one selection of American English plus the technical terms.
    /// </summary>
    private static readonly SpellingDictionary American = new([CommonImage, AmericanImage, TechnicalImage]);

    /// <summary>
    ///     The one selection of British English plus the technical terms.
    /// </summary>
    private static readonly SpellingDictionary British = new([CommonImage, BritishImage, TechnicalImage]);

    /// <summary>
    ///     The one selection of both American and British English plus the technical terms.
    /// </summary>
    private static readonly SpellingDictionary Both = new([CommonImage, AmericanImage, BritishImage, TechnicalImage]);

    /// <summary>
    ///     The dictionaries of this selection, loaded together the first time they are needed.
    /// </summary>
    private readonly Lazy<DictionaryImage[]> _images;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpellingDictionary"/> class for a set of dictionaries.
    /// </summary>
    /// <remarks>
    ///     The constructor is private so that only the three shared selections exist. It does no
    ///     loading: it only records how to obtain the images. The images are loaded together, on
    ///     first use, so that a damaged resource fails every lookup of the selection the same way
    ///     instead of failing only for words that happen to reach it.
    /// </remarks>
    /// <param name="sources">The lazily loaded dictionaries that make up the selection. Must not be null.</param>
    private SpellingDictionary(Lazy<DictionaryImage>[] sources) =>
        _images = new Lazy<DictionaryImage[]>(
            () => Array.ConvertAll(sources, source => source.Value),
            LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets the default selection: American English (<c>en-US</c>) plus the technical terms.
    /// </summary>
    /// <remarks>
    ///     American English is the default because it is cspell's default language, which TextLint
    ///     keeps so that migrated configurations behave the same. The returned object is shared and
    ///     thread-safe; nothing is loaded until the first lookup.
    /// </remarks>
    /// <value>The shared selection that <see cref="ForLanguages"/> returns for <c>en</c> and <c>en-US</c>.</value>
    public static SpellingDictionary Default => American;

    /// <summary>
    ///     Gets the language names that <see cref="ForLanguages"/> accepts.
    /// </summary>
    /// <remarks>
    ///     The names are <c>en</c> (the same as <c>en-US</c>), <c>en-GB</c>, and <c>en-US</c>. They
    ///     are matched ignoring letter case. The list is fixed for the life of the process and is
    ///     read-only.
    /// </remarks>
    /// <value>The supported language names, in alphabetical order.</value>
    public static IReadOnlyList<string> SupportedLanguages => Languages;

    /// <summary>
    ///     Gets the selection for a language name or a comma-separated list of language names.
    /// </summary>
    /// <remarks>
    ///     Names are compared ignoring letter case, and spaces around a name are ignored, so
    ///     <c>EN-us</c> and <c>en-US , en-GB</c> are accepted. <c>en</c> and <c>en-US</c> both select
    ///     American English, <c>en-GB</c> selects British English, and listing both dialects selects
    ///     both; repeating a name changes nothing. Technical terms are always included. Equal
    ///     selections return the same instance, whatever the spelling of the request. Nothing is
    ///     loaded by this method. Thread-safe.
    /// </remarks>
    /// <param name="languages">One language name or several separated by commas. Must not be null.</param>
    /// <returns>The shared selection for the named languages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="languages"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the list is empty or contains an empty name, or when a name is not supported
    ///     (for example <c>fr</c> or <c>en-AU</c>). The message names the offending value and lists the
    ///     supported names, so the caller can correct the configuration.
    /// </exception>
    public static SpellingDictionary ForLanguages(string languages)
    {
        ArgumentNullException.ThrowIfNull(languages);

        // Work out which dialects the names ask for; at least one name is required, and every name
        // must be one that is supported, so that a typo is never silently ignored
        var american = false;
        var british = false;
        foreach (var name in languages.Split(LanguageSeparator).Select(static part => part.Trim()))
        {
            if (name.Length == 0)
            {
                throw new ArgumentException(
                    $"The language list '{languages}' contains an empty language name. " +
                    $"Supported languages: {string.Join(", ", Languages)}.",
                    nameof(languages));
            }

            if (string.Equals(name, BritishLanguage, StringComparison.OrdinalIgnoreCase))
            {
                british = true;
            }
            else if (string.Equals(name, AmericanLanguage, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, EnglishLanguage, StringComparison.OrdinalIgnoreCase))
            {
                american = true;
            }
            else
            {
                throw new ArgumentException(
                    $"The language '{name}' is not supported. Supported languages: {string.Join(", ", Languages)}.",
                    nameof(languages));
            }
        }

        // Return the one shared instance of the selection
        return (american, british) switch
        {
            (true, true) => Both,
            (false, true) => British,
            _ => American,
        };
    }

    /// <summary>
    ///     Determines whether a word is known to the dictionaries of this selection.
    /// </summary>
    /// <remarks>
    ///     This is the question a spell checker asks of every word, so it neither allocates nor
    ///     throws for any text. Letter case is ignored (<c>Color</c>, <c>COLOR</c>, and
    ///     <c>color</c> are the same word, and a word listed only with a capital is known in every
    ///     case), the typographic apostrophe (U+2019) is treated as the straight apostrophe, and
    ///     accents are significant (<c>café</c> is known, <c>cafè</c> is not). Anything that cannot be
    ///     a word of the dictionaries, namely the empty string, text longer than 64 characters, and
    ///     text with a character above Latin-1 (such as other scripts), is reported as unknown. Other
    ///     punctuation is not stripped: the text is looked up as written, because deciding what a word
    ///     is belongs to tokenization. Unicode normalization is not applied, so a decomposed accented
    ///     letter is not recognized. The first call loads the selection's dictionaries and may take a
    ///     few milliseconds. Thread-safe.
    /// </remarks>
    /// <param name="word">The word to look up; a <see cref="string"/> converts implicitly.</param>
    /// <returns><see langword="true"/> when the word is known to the selection; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown, naming the resource, when an embedded dictionary is missing or damaged. This
    ///     indicates a defective build of the library, not a problem with the word; every lookup of
    ///     the selection fails the same way.
    /// </exception>
    public bool IsKnown(ReadOnlySpan<char> word)
    {
        // Turn the word into its lookup key on the stack; a word that cannot be stored is not known
        Span<byte> buffer = stackalloc byte[DictionaryFormat.MaxKeyLength];
        if (!DictionaryFormat.TryFold(word, buffer, out var keyLength))
        {
            return false;
        }

        // Hash the key once and ask each dictionary of the selection in turn
        var key = buffer[..keyLength];
        var hash = DictionaryFormat.Hash(key);
        foreach (var image in _images.Value)
        {
            if (image.Contains(key, hash))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Creates the lazy loader of one embedded dictionary.
    /// </summary>
    /// <remarks>
    ///     Loading on first use keeps start-up free of work for programs that never look a word up.
    ///     The loader runs at most once and its outcome is cached, a failure included, so every
    ///     caller sees the same result; the loaded image is shared by every selection that uses it.
    ///     Thread-safe.
    /// </remarks>
    /// <param name="dictionary">The name of the dictionary, which is the last part of its resource name.</param>
    /// <returns>A lazy value that loads and validates the embedded dictionary when first read.</returns>
    private static Lazy<DictionaryImage> LoadLazily(string dictionary) =>
        new(
            () => DictionaryImage.LoadEmbedded(typeof(SpellingDictionary).Assembly, ResourcePrefix + dictionary + ResourceSuffix),
            LazyThreadSafetyMode.ExecutionAndPublication);
}
