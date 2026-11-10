package TextLint {
    private import SoftwareItemMetadata::*;

    part def DictionariesSubsystem {
        doc /* Answers whether a word is known to the selected English language and the technical vocabulary, from dictionaries embedded in the library assembly. Safe code only: each dictionary is read once into a managed array. */

        metadata info : SoftwareInfo {
            description = "Answers whether a word is known to the selected English language and the technical vocabulary, from dictionaries embedded in the library assembly. Safe code only: each dictionary is read once into a managed array.";
            testFiles = ("test/DemaConsulting.TextLint.Tests/Dictionaries/DictionariesSubsystemTests.cs");
            designFiles = ("docs/design/textlint/dictionaries.md");
            verificationFiles = ("docs/verification/textlint/dictionaries.md");
            requirementFiles = ("docs/reqstream/textlint/dictionaries.yaml");
        }

        part spellingDictionary : SpellingDictionary;
        part dictionaryImage : DictionaryImage;
    }
}
