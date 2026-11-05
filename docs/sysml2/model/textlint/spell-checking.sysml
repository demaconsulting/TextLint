package TextLint {
    part def SpellCheckingSubsystem {
        doc /* Checks the spelling of a string of text, and of the files that the settings select under a directory, in parallel, and returns every finding and every file problem in a deterministic order. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckingSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint/spell-checking.md */
        comment verificationRef /* Verification: docs/verification/textlint/spell-checking.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/spell-checking.yaml */

        part textChecker : TextChecker;
        part fileTextReader : FileTextReader;
        part spellChecker : SpellChecker;
    }
}
