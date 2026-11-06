package TextLintTool {
    part def LintRunner {
        doc /* Runs one check: resolves the root, finds and loads the settings, applies the command-line patterns and overrides through the library, calls the spell checker, reports the result, and reports every failure a user can cause. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint.Tool/Linting/LintRunner.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/Linting/LintRunnerTests.cs */
        comment designRef /* Design: docs/design/textlint-tool/linting/lint-runner.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool/linting/lint-runner.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool/linting/lint-runner.yaml */
    }
}
