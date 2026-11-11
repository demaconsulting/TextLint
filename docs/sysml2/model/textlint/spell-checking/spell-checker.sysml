package TextLint {
    private import SoftwareItemMetadata::*;

    part def SpellChecker {
        doc /* Selects the files that the settings name under a root directory, reads and checks each in parallel without locks, and returns one deterministic result with the findings, the file diagnostics, and the counts. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/SpellChecking/SpellChecker.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckOptions.cs",
                "src/DemaConsulting.TextLint/SpellChecking/SpellCheckResult.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FileDiagnostic.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FileDiagnosticKind.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckerTests.cs");
            designFiles = ("docs/design/textlint/spell-checking/spell-checker.md");
            verificationFiles = ("docs/verification/textlint/spell-checking/spell-checker.md");
            requirementFiles = ("docs/reqstream/textlint/spell-checking/spell-checker.yaml");
        }
    }

    // The checker takes the files, the ignore patterns, the hidden-file switch, and the spelling settings
    // from the settings value, selects the files with the file selector, and reads and checks them with
    // the other units of its own subsystem.
    dependency SpellCheckerUsesTextLintSettings from SpellChecker to TextLintSettings;
    dependency SpellCheckerUsesFileSelector from SpellChecker to FileSelector;
    dependency SpellCheckerUsesTextChecker from SpellChecker to TextChecker;
    dependency SpellCheckerUsesFileTextReader from SpellChecker to FileTextReader;
}
