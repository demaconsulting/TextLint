package DictionaryBuilder {
    part def DictionaryFormat {
        doc /* The shared definition of the binary dictionary format: layout constants, case fold, key hash, and capitalization classification. Safe code only; its source file is also compiled into the library, which reads each dictionary into a managed array. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint.DictionaryBuilder/DictionaryFormat.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.DictionaryBuilder.Tests/DictionaryFormatTests.cs */
        comment designRef /* Design: docs/design/dictionary-builder/dictionary-format.md */
        comment verificationRef /* Verification: docs/verification/dictionary-builder/dictionary-format.md */
        comment reqRef /* Requirements: docs/reqstream/dictionary-builder/dictionary-format.yaml */
    }
}
