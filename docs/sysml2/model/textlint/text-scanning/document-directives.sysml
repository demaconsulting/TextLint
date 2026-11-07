package TextLint {
    part def DocumentDirectives {
        doc /* Interprets the directives that comments in a text use to control spell checking: disable and enable blocks, disable-line and disable-next-line, ignore and words, the patterns of ignoreRegExp, and the cspell directives that are recognized but not supported. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/DocumentDirectives.cs (supporting type: DirectiveScan.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/DocumentDirectivesTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/document-directives.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/document-directives.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/document-directives.yaml */
    }

    // The directive patterns share the match time limit of the default patterns (a documented dependency
    // within the subsystem).
    dependency DocumentDirectivesUsesDefaultPatterns from DocumentDirectives to DefaultPatterns;
}
