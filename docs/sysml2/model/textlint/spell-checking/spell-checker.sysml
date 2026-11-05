package TextLint {
    part def SpellChecker {
        doc /* Selects the files that the settings name under a root directory, reads and checks each in parallel without locks, and returns one deterministic result with the findings, the file diagnostics, and the counts. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/SpellChecking/SpellChecker.cs (supporting types: SpellCheckOptions.cs, SpellCheckResult.cs, FileDiagnostic.cs, FileDiagnosticKind.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckerTests.cs */
        comment designRef /* Design: docs/design/textlint/spell-checking/spell-checker.md */
        comment verificationRef /* Verification: docs/verification/textlint/spell-checking/spell-checker.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/spell-checking/spell-checker.yaml */
    }

    // The checker takes the files, the ignore patterns, the hidden-file switch, and the spelling settings
    // from the settings value, selects the files with the file selector, and reads and checks them with
    // the other units of its own subsystem.
    dependency SpellCheckerUsesTextLintSettings from SpellChecker to TextLintSettings;
    dependency SpellCheckerUsesFileSelector from SpellChecker to FileSelector;
    dependency SpellCheckerUsesTextChecker from SpellChecker to TextChecker;
    dependency SpellCheckerUsesFileTextReader from SpellChecker to FileTextReader;
}
