package TextLint {
    private import SoftwareItemMetadata::*;

    part def DictionaryImage {
        doc /* One binary dictionary held in memory: validates the file header, loads an embedded dictionary once into a managed array, and answers whether a prepared key is stored. Bounds-checked and immutable. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/Dictionaries/DictionaryImage.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/DictionaryFormat.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/Dictionaries/DictionaryImageTests.cs");
            designFiles = ("docs/design/textlint/dictionaries/dictionary-image.md");
            verificationFiles = ("docs/verification/textlint/dictionaries/dictionary-image.md");
            requirementFiles = ("docs/reqstream/textlint/dictionaries/dictionary-image.yaml");
        }
    }

    // The image reads the file layout and the hash through the format definitions that the
    // DictionaryBuilder system owns and the library compiles, so writer and reader cannot disagree.
    dependency DictionaryImageUsesDictionaryFormat from DictionaryImage to DictionaryBuilder::DictionaryFormat;
}
