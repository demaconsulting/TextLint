package TextLint {
    private import SoftwareItemMetadata::*;

    part def MarkdownPatterns {
        doc /* Finds the text of a Markdown file that is never spell-checked because it is a name or an address, the targets of links, the labels of reference links, the definitions of references, the ids of anchors, and HTML entities, ported from cspell, and decides from the name of a source whether it is a Markdown file. */

        metadata info : SoftwareInfo {
            description = "Finds the text of a Markdown file that is never spell-checked because it is a name or an address, the targets of links, the labels of reference links, the definitions of references, the ids of anchors, and HTML entities, ported from cspell, and decides from the name of a source whether it is a Markdown file.";
            sourceFiles = ("src/DemaConsulting.TextLint/TextScanning/MarkdownPatterns.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/MarkdownPatternsTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/markdown-patterns.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/markdown-patterns.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/markdown-patterns.yaml");
        }
    }

    // The match time limit is the one of the pattern parser (a documented dependency of the code).
    dependency MarkdownPatternsUsesPatternParser from MarkdownPatterns to PatternParser;
}
