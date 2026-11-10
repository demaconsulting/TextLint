package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def LintRunner {
        doc /* Runs one check: resolves the root, finds and loads the settings, applies the command-line patterns and overrides through the library, calls the spell checker, reports the result, and reports every failure a user can cause. */

        metadata info : SoftwareInfo {
            description = "Runs one check: resolves the root, finds and loads the settings, applies the command-line patterns and overrides through the library, calls the spell checker, reports the result, and reports every failure a user can cause.";
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Linting/LintRunner.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Linting/LintRunnerTests.cs");
            designFiles = ("docs/design/textlint-tool/linting/lint-runner.md");
            verificationFiles = ("docs/verification/textlint-tool/linting/lint-runner.md");
            requirementFiles = ("docs/reqstream/textlint-tool/linting/lint-runner.yaml");
        }
    }
}
