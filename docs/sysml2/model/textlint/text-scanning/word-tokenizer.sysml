package TextLint {
    private import SoftwareItemMetadata::*;

    part def WordTokenizer {
        doc /* Decides what a word is: finds the words, tokens, and case-change pieces of a text as ranges of the original text, and interprets word-list entries so that loading a setting and checking text agree. */

        metadata info : SoftwareInfo {
            description = "Decides what a word is: finds the words, tokens, and case-change pieces of a text as ranges of the original text, and interprets word-list entries so that loading a setting and checking text agree.";
            sourceFiles = (
                "src/DemaConsulting.TextLint/TextScanning/WordTokenizer.cs",
                "src/DemaConsulting.TextLint/TextScanning/ListEntry.cs",
                "src/DemaConsulting.TextLint/TextScanning/TextRange.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/WordTokenizerTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/word-tokenizer.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/word-tokenizer.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/word-tokenizer.yaml");
        }
    }
}
