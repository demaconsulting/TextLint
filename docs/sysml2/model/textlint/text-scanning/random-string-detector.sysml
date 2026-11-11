package TextLint {
    private import SoftwareItemMetadata::*;

    part def RandomStringDetector {
        doc /* Recognizes a token that looks like random data, such as a key or a token, and the hexadecimal sequences inside a token, ported from the random-string rule of cspell, so that their fragments are not reported as unknown words. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint/TextScanning/RandomStringDetector.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/RandomStringDetectorTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/random-string-detector.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/random-string-detector.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/random-string-detector.yaml");
        }
    }

    // The match time limit is the one of the pattern parser (a documented dependency of the code).
    dependency RandomStringDetectorUsesPatternParser from RandomStringDetector to PatternParser;
}
