package TextLint {
    part def TextScanningSubsystem {
        doc /* Decides what the checkable parts of a text are: the words, tokens, and case-change pieces; the text that is never spell-checked because it is not prose, by default and by the patterns of the settings; the long tokens that look like random data; the in-document directives that switch checking off or add words; and the interpretation of word-list entries. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/TextScanningSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning.yaml */

        part wordTokenizer : WordTokenizer;
        part defaultPatterns : DefaultPatterns;
        part documentDirectives : DocumentDirectives;
        part patternParser : PatternParser;
        part patternCatalog : PatternCatalog;
        part markdownPatterns : MarkdownPatterns;
        part randomStringDetector : RandomStringDetector;
    }
}
