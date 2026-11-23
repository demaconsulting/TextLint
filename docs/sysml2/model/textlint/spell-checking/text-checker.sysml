package TextLint_TextChecker {
    private import SoftwareItemMetadata::*;

    part def TextChecker {
        doc /* Checks the spelling of a string of text against the dictionary of the configured language and the word lists, splitting an unknown word at its optional case breaks and accepting adjacent pieces that form an accepted word, skipping the text that the patterns of the settings, the patterns of the ignoreRegExp directives, the Markdown rules, and the random-string rule exclude, and returns each unknown word, flagged word, unsupported directive, and unusable directive pattern with its line and column in text order. */

        metadata info : SoftwareInfo {
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
    dependency TextCheckerUsesTextLintSettings from TextChecker to TextLint::TextLintSettings;
    dependency TextCheckerUsesSpellingDictionary from TextChecker to TextLint::SpellingDictionary;
    dependency TextCheckerUsesWordTokenizer from TextChecker to TextLint::WordTokenizer;
    dependency TextCheckerUsesDefaultPatterns from TextChecker to TextLint::DefaultPatterns;
    dependency TextCheckerUsesDocumentDirectives from TextChecker to TextLint::DocumentDirectives;
    dependency TextCheckerUsesPatternCatalog from TextChecker to TextLint::PatternCatalog;
    dependency TextCheckerUsesMarkdownPatterns from TextChecker to TextLint::MarkdownPatterns;
    dependency TextCheckerUsesRandomStringDetector from TextChecker to TextLint::RandomStringDetector;
}
