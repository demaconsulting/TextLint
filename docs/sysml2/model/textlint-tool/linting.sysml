package TextLintTool {
    part def LintingSubsystem {
        doc /* Runs one spell check as the command line asked for it: finds and loads the settings, applies the command-line overrides, calls the TextLint library, reports the result, and decides the exit status. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/Linting/LintingSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint-tool/linting.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool/linting.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool/linting.yaml */

        part lintRunner : LintRunner;
        part lintReporter : LintReporter;
    }
}
