package TextLint {
    private import SoftwareItemMetadata::*;

    part def PatternParser {
        doc /* Turns one pattern entry of the settings, written between slashes with flags or as a bare expression in the syntax of JavaScript, into a .NET regular expression with the same meaning, rejecting what it cannot translate with the reason, and bounds the matching with a time limit. Includes the translator that does the work, the exception that reports an unusable entry, and the shared definitions of the JavaScript white space and letter case that the other patterns of the subsystem use. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/TextScanning/PatternParser.cs",
                "src/DemaConsulting.TextLint/TextScanning/JavaScriptRegexTranslator.cs",
                "src/DemaConsulting.TextLint/TextScanning/InvalidPatternException.cs",
                "src/DemaConsulting.TextLint/TextScanning/JavaScriptSyntax.cs",
                "src/DemaConsulting.TextLint/TextScanning/JavaScriptCaseFolding.cs",
                "src/DemaConsulting.TextLint/TextScanning/JavaScriptCaseData.cs",
                "tools/javascript-oracles/generate-javascript-case-data.mjs"
            );
            testFiles = (
                "test/DemaConsulting.TextLint.Tests/TextScanning/PatternParserTests.cs",
                "test/DemaConsulting.TextLint.Tests/TextScanning/JavaScriptCaseFoldingTests.cs",
                "test/DemaConsulting.TextLint.Tests/TextScanning/JavaScriptSyntaxTests.cs",
                "test/DemaConsulting.TextLint.Tests/TextScanning/JavaScriptUnicodeModeTests.cs"
            );
            designFiles = ("docs/design/textlint/text-scanning/pattern-parser.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/pattern-parser.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/pattern-parser.yaml");
        }
    }

    // The match time limit is the one defined by the default patterns, so that every pattern of the library
    // is bounded by the same limit (a documented dependency of the code).
    dependency PatternParserUsesDefaultPatterns from PatternParser to DefaultPatterns;
}
