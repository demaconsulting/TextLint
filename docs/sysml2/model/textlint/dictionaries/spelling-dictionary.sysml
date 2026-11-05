package TextLint {
    part def SpellingDictionary {
        doc /* The public word lookup: selects the embedded dictionaries for a language and reports whether a word is known, ignoring letter case. Immutable, shared per selection, and thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/Dictionaries/SpellingDictionary.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Dictionaries/SpellingDictionaryTests.cs */
        comment designRef /* Design: docs/design/textlint/dictionaries/spelling-dictionary.md */
        comment verificationRef /* Verification: docs/verification/textlint/dictionaries/spelling-dictionary.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/dictionaries/spelling-dictionary.yaml */
    }

    // The lookup asks the dictionary images of its selection and uses the shared format definitions,
    // owned by the DictionaryBuilder system, to turn a word into its key.
    dependency SpellingDictionaryUsesDictionaryImage from SpellingDictionary to DictionaryImage;
    dependency SpellingDictionaryUsesDictionaryFormat from SpellingDictionary to DictionaryBuilder::DictionaryFormat;
}
