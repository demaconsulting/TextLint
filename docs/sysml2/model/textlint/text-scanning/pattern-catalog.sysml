package TextLint {
    part def PatternCatalog {
        doc /* Resolves the ignoreRegExpList and patterns settings into regular expressions: it knows the names of the predefined cspell patterns that are supported, checks the names that the settings define and refer to, and finds the ranges of a text that the expressions match. Includes the named pattern value that the settings hold. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/PatternCatalog.cs */
        comment sourceRef2 /* Source: src/DemaConsulting.TextLint/TextScanning/NamedPattern.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/PatternCatalogTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/pattern-catalog.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/pattern-catalog.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/pattern-catalog.yaml */
    }

    dependency PatternCatalogUsesPatternParser from PatternCatalog to PatternParser;
}
