package TextLint {
    part def PatternParser {
        doc /* Turns one pattern entry of the settings, written between slashes with flags or as a bare expression in the syntax of JavaScript, into a .NET regular expression with the same meaning, rejecting what it cannot translate with the reason, and bounds the matching with a time limit. Includes the translator that does the work, the exception that reports an unusable entry, and the shared definitions of the JavaScript white space and letter case that the other patterns of the subsystem use. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/TextScanning/PatternParser.cs */
        comment sourceRef2 /* Source: src/DemaConsulting.TextLint/TextScanning/JavaScriptRegexTranslator.cs */
        comment sourceRef3 /* Source: src/DemaConsulting.TextLint/TextScanning/InvalidPatternException.cs */
        comment sourceRef4 /* Source: src/DemaConsulting.TextLint/TextScanning/JavaScriptSyntax.cs */
        comment sourceRef5 /* Source: src/DemaConsulting.TextLint/TextScanning/JavaScriptCaseFolding.cs */
        comment sourceRef6 /* Source: src/DemaConsulting.TextLint/TextScanning/JavaScriptCaseData.cs */
        comment sourceRef7 /* Source: src/DemaConsulting.TextLint/TextScanning/Data/generate-javascript-case-data.mjs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/PatternParserTests.cs */
        comment testRef2 /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/JavaScriptCaseFoldingTests.cs */
        comment testRef3 /* Test: test/DemaConsulting.TextLint.Tests/TextScanning/JavaScriptSyntaxTests.cs */
        comment designRef /* Design: docs/design/textlint/text-scanning/pattern-parser.md */
        comment verificationRef /* Verification: docs/verification/textlint/text-scanning/pattern-parser.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/text-scanning/pattern-parser.yaml */
    }

    // The match time limit is the one defined by the default patterns, so that every pattern of the library
    // is bounded by the same limit (a documented dependency of the code).
    dependency PatternParserUsesDefaultPatterns from PatternParser to DefaultPatterns;
}
