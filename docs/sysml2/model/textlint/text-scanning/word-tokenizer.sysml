package TextLint {
    part def WordTokenizer {
        doc /* Decides what a word is: finds the words, tokens, and case-change pieces of a text as ranges of the original text, and interprets word-list entries so that loading a setting and checking text agree. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/WordTokenizer.cs (supporting types: ListEntry.cs, TextRange.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/WordTokenizerTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/word-tokenizer.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/word-tokenizer.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/word-tokenizer.yaml */
    }
}
