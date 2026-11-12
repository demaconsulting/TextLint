package TextLint {
    private import SoftwareItemMetadata::*;

    part def SpellingDictionary {
        doc /* The public word lookup: selects the embedded dictionaries for a language and reports whether a word is known, ignoring letter case. Immutable, shared per selection, and thread-safe. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint/Dictionaries/SpellingDictionary.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/Dictionaries/SpellingDictionaryTests.cs");
            designFiles = ("docs/design/textlint/dictionaries/spelling-dictionary.md");
            verificationFiles = ("docs/verification/textlint/dictionaries/spelling-dictionary.md");
            requirementFiles = ("docs/reqstream/textlint/dictionaries/spelling-dictionary.yaml");
        }
    }

    // The lookup asks the dictionary images of its selection and uses the shared format definitions,
    // owned by the DictionaryBuilder system, to turn a word into its key.
    dependency SpellingDictionaryUsesDictionaryImage from SpellingDictionary to DictionaryImage;
    dependency SpellingDictionaryUsesDictionaryFormat from SpellingDictionary to DictionaryBuilder::DictionaryFormat;
}
