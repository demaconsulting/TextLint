package TextLint {
    private import SoftwareItemMetadata::*;

    part def PatternCatalog {
        doc /* Resolves the ignoreRegExpList and patterns settings into regular expressions: it knows the names of the predefined cspell patterns that are supported, checks the names that the settings define and refer to, and finds the ranges of a text that the expressions match. Includes the named pattern value that the settings hold. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/TextScanning/PatternCatalog.cs",
                "src/DemaConsulting.TextLint/TextScanning/NamedPattern.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/TextScanning/PatternCatalogTests.cs");
            designFiles = ("docs/design/textlint/text-scanning/pattern-catalog.md");
            verificationFiles = ("docs/verification/textlint/text-scanning/pattern-catalog.md");
            requirementFiles = ("docs/reqstream/textlint/text-scanning/pattern-catalog.yaml");
        }
    }

    dependency PatternCatalogUsesPatternParser from PatternCatalog to PatternParser;
}
