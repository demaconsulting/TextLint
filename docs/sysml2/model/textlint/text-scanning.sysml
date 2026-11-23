package TextLint_TextScanningSubsystem {
    public import TextLint_DefaultPatterns::*;
    public import TextLint_DocumentDirectives::*;
    public import TextLint_MarkdownPatterns::*;
    public import TextLint_PatternCatalog::*;
    public import TextLint_PatternParser::*;
    public import TextLint_RandomStringDetector::*;
    public import TextLint_WordTokenizer::*;
    private import SoftwareItemMetadata::*;

    part def TextScanningSubsystem {
        doc /* Decides what the checkable parts of a text are: the words, tokens, and case-change pieces; the text that is never spell-checked because it is not prose, by default and by the patterns of the settings; the long tokens that look like random data; the in-document directives that switch checking off or add words; and the interpretation of word-list entries. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/TextScanningSubsystemTests.cs");
            designFiles = ("docs/design/textlint/text-scanning.md");
            verificationFiles = ("docs/verification/textlint/text-scanning.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning.yaml");
        }

        part wordTokenizer : WordTokenizer;
        part defaultPatterns : DefaultPatterns;
        part documentDirectives : DocumentDirectives;
        part patternParser : PatternParser;
        part patternCatalog : PatternCatalog;
        part markdownPatterns : MarkdownPatterns;
        part randomStringDetector : RandomStringDetector;
    }
}
