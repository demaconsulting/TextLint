package TextLint {
    private import SoftwareItemMetadata::*;

    part def SpellChecker {
        doc /* Selects the files that the settings name under a root directory, resolves the overrides and the enabled switch for each file, reads and checks each enabled file in parallel without locks, and returns one deterministic result with the findings, the file diagnostics, the notes, and the counts. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/SpellChecking/SpellChecker.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckOptions.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckResult.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FileDiagnostic.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FileDiagnosticKind.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckNote.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckNoteKind.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckNoteSeverity.cs"
            );
            testFiles = (
                "test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckerTests.cs",
                "test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckerOverridesTests.cs"
            );
            designFiles = ("docs/design/textlint/spell-checking/spell-checker.md");
            verificationFiles = ("docs/verification/textlint/spell-checking/spell-checker.md");
            requirementFiles = ("docs/reqstream/textlint/spell-checking/spell-checker.yaml");
        }
    }

    // The checker takes the files, the ignore patterns, the hidden-file switch, and the spelling settings
    // from the settings value, selects the files with the file selector, and reads and checks them with
    // the other units of its own subsystem. It resolves the per-file overrides with the override resolver
    // of the Configuration subsystem and compares the dictionaries of two languages when it reports that
    // the command-line language replaced an override language (documented subsystem dependencies).
    dependency SpellCheckerUsesTextLintSettings from SpellChecker to TextLintSettings;
    dependency SpellCheckerUsesSettingsOverrideResolver from SpellChecker to SettingsOverrideResolver;
    dependency SpellCheckerUsesSpellingDictionary from SpellChecker to SpellingDictionary;
    dependency SpellCheckerUsesFileSelector from SpellChecker to FileSelector;
    dependency SpellCheckerUsesTextChecker from SpellChecker to TextChecker;
    dependency SpellCheckerUsesFileTextReader from SpellChecker to FileTextReader;
}
