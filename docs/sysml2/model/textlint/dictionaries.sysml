package TextLint {
    part def DictionariesSubsystem {
        doc /* Answers whether a word is known to the selected English language and the technical vocabulary, from dictionaries embedded in the library assembly. Safe code only: each dictionary is read once into a managed array. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Dictionaries/DictionariesSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint/dictionaries.md */
        comment verificationRef /* Verification: docs/verification/textlint/dictionaries.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/dictionaries.yaml */

        part spellingDictionary : SpellingDictionary;
        part dictionaryImage : DictionaryImage;
    }
}
