package TextLint {
    part def DictionaryImage {
        doc /* One binary dictionary held in memory: validates the file header, loads an embedded dictionary once into a managed array, and answers whether a prepared key is stored. Bounds-checked and immutable. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/Dictionaries/DictionaryImage.cs (the shared DictionaryFormat.cs is compiled from the DictionaryBuilder project) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Dictionaries/DictionaryImageTests.cs */
        comment designRef /* Design: docs/design/textlint/dictionaries/dictionary-image.md */
        comment verificationRef /* Verification: docs/verification/textlint/dictionaries/dictionary-image.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/dictionaries/dictionary-image.yaml */
    }

    // The image reads the file layout and the hash through the format definitions that the
    // DictionaryBuilder system owns and the library compiles, so writer and reader cannot disagree.
    dependency DictionaryImageUsesDictionaryFormat from DictionaryImage to DictionaryBuilder::DictionaryFormat;
}
