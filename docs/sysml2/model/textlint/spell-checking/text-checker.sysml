package TextLint {
    private import SoftwareItemMetadata::*;

    part def TextChecker {
        doc /* Checks the spelling of a string of text against the dictionary of the configured language and the word lists, splitting an unknown word at its optional case breaks and accepting adjacent pieces that form an accepted word, skipping the text that the patterns of the settings, the patterns of the ignoreRegExp directives, the Markdown rules, and the random-string rule exclude, and returns each unknown word, flagged word, unsupported directive, and unusable directive pattern with its line and column in text order. */

        metadata info : SoftwareInfo {
            description = "Checks the spelling of a string of text against the dictionary of the configured language and the word lists, splitting an unknown word at its optional case breaks and accepting adjacent pieces that form an accepted word, skipping the text that the patterns of the settings, the patterns of the ignoreRegExp directives, the Markdown rules, and the random-string rule exclude, and returns each unknown word, flagged word, unsupported directive, and unusable directive pattern with its line and column in text order.";
            sourceFiles = (
                "src/DemaConsulting.TextLint/SpellChecking/TextChecker.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellingIssue.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellingIssueKind.cs",
                "src/DemaConsulting.TextLint/SpellChecking/WordRules.cs",
                "src/DemaConsulting.TextLint/SpellChecking/WordSet.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FlagSet.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/SpellChecking/TextCheckerTests.cs");
            designFiles = ("docs/design/textlint/spell-checking/text-checker.md");
            verificationFiles = ("docs/verification/textlint/spell-checking/text-checker.md");
            requirementFiles = ("docs/reqstream/textlint/spell-checking/text-checker.yaml");
        }
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
