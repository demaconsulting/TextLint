package TextLint {
    part def MarkdownPatterns {
        doc /* Finds the text of a Markdown file that is never spell-checked because it is a name or an address, the targets of links, the labels of reference links, the definitions of references, the ids of anchors, and HTML entities, ported from cspell, and decides from the name of a source whether it is a Markdown file. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/MarkdownPatterns.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/MarkdownPatternsTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/markdown-patterns.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/markdown-patterns.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/markdown-patterns.yaml */
    }

    // The match time limit is the one of the pattern parser (a documented dependency of the code).
    dependency MarkdownPatternsUsesPatternParser from MarkdownPatterns to PatternParser;
}
