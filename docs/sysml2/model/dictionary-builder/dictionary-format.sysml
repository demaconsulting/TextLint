package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def DictionaryFormat {
        doc /* The shared definition of the binary dictionary format: layout constants, case fold, key hash, and capitalization classification. Safe code only; its source file is also compiled into the library, which reads each dictionary into a managed array. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint.DictionaryBuilder/DictionaryFormat.cs");
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/DictionaryFormatTests.cs");
            designFiles = ("docs/design/dictionary-builder/dictionary-format.md");
            verificationFiles = ("docs/verification/dictionary-builder/dictionary-format.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/dictionary-format.yaml");
        }
    }
}
