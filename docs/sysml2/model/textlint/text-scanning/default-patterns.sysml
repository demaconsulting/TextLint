package TextLint {
    private import SoftwareItemMetadata::*;

    part def DefaultPatterns {
        doc /* Finds the text that is never spell-checked because it is not prose: URLs, email addresses, certificates and keys, encoded data, hashes, hexadecimal values, Unicode references, and UUIDs, ported from the default ignore list of cspell. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint/TextScanning/DefaultPatterns.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/DefaultPatternsTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/default-patterns.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/default-patterns.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/default-patterns.yaml");
        }
    }
}
