package TextLint {
    part def DefaultPatterns {
        doc /* Finds the text that is never spell-checked because it is not prose: URLs, email addresses, certificates and keys, encoded data, hashes, hexadecimal values, Unicode references, and UUIDs, ported from the default ignore list of cspell. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/DefaultPatterns.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/DefaultPatternsTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/default-patterns.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/default-patterns.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/default-patterns.yaml */
    }
}
