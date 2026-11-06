package TextLint {
    part def TextChecker {
        doc /* Checks the spelling of a string of text against the dictionary of the configured language and the word lists, skipping the text that the patterns of the settings, the Markdown rules, and the random-string rule exclude, and returns each unknown word, flagged word, and unsupported directive with its line and column in text order. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/SpellChecking/TextChecker.cs (supporting types: SpellingIssue.cs, SpellingIssueKind.cs, WordRules.cs, WordSet.cs, FlagSet.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/SpellChecking/TextCheckerTests.cs */
        comment designRef /* Design: docs/design/textlint/spell-checking/text-checker.md */
        comment verificationRef /* Verification: docs/verification/textlint/spell-checking/text-checker.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/spell-checking/text-checker.yaml */
    }

    // The checker takes its language, word lists, and minimum length from the settings value, looks words
    // up in the spelling dictionary, and uses the units of the text scanning subsystem to split the text
    // into words, to find the text that is never checked, and to read the directives.
    dependency TextCheckerUsesTextLintSettings from TextChecker to TextLintSettings;
    dependency TextCheckerUsesSpellingDictionary from TextChecker to SpellingDictionary;
    dependency TextCheckerUsesWordTokenizer from TextChecker to WordTokenizer;
    dependency TextCheckerUsesDefaultPatterns from TextChecker to DefaultPatterns;
    dependency TextCheckerUsesDocumentDirectives from TextChecker to DocumentDirectives;
    dependency TextCheckerUsesPatternCatalog from TextChecker to PatternCatalog;
    dependency TextCheckerUsesMarkdownPatterns from TextChecker to MarkdownPatterns;
    dependency TextCheckerUsesRandomStringDetector from TextChecker to RandomStringDetector;
}
