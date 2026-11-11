package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def WordListReader {
        doc /* Reads and validates the plain-text word lists of one dictionary and merges them into one set of distinct, case-folded words. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint.DictionaryBuilder/WordListReader.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/WordListReadResult.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/WordListDiagnostic.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/WordListReaderTests.cs");
            designFiles = ("docs/design/dictionary-builder/word-list-reader.md");
            verificationFiles = ("docs/verification/dictionary-builder/word-list-reader.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/word-list-reader.yaml");
        }
    }
}
