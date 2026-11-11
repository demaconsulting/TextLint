package TextLint {
    private import SoftwareItemMetadata::*;

    part def DocumentDirectives {
        doc /* Interprets the directives that comments in a text use to control spell checking: disable and enable blocks, disable-line and disable-next-line, ignore and words, the patterns of ignoreRegExp, and the cspell directives that are recognized but not supported. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/TextScanning/DocumentDirectives.cs",
                "src/DemaConsulting.TextLint/TextScanning/DirectiveScan.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/DocumentDirectivesTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/document-directives.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/document-directives.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/document-directives.yaml");
        }
    }

    // The directive patterns share the match time limit of the default patterns (a documented dependency
    // within the subsystem).
    dependency DocumentDirectivesUsesDefaultPatterns from DocumentDirectives to DefaultPatterns;
}
