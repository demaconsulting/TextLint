package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def DictionaryWriter {
        doc /* Serializes a set of folded words into the binary dictionary format: header, sorted words, and a hash slot table. */

        metadata info : SoftwareInfo {
            description = "Serializes a set of folded words into the binary dictionary format: header, sorted words, and a hash slot table.";
            sourceFiles = ("src/DemaConsulting.TextLint.DictionaryBuilder/DictionaryWriter.cs");
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/DictionaryWriterTests.cs");
            designFiles = ("docs/design/dictionary-builder/dictionary-writer.md");
            verificationFiles = ("docs/verification/dictionary-builder/dictionary-writer.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/dictionary-writer.yaml");
        }
    }
}
