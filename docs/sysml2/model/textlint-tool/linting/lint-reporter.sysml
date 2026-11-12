package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def LintReporter {
        doc /* Writes the result of one check: each finding, each file that was skipped or could not be checked, and a one-line summary. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Linting/LintReporter.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Linting/LintReporterTests.cs");
            designFiles = ("docs/design/textlint-tool/linting/lint-reporter.md");
            verificationFiles = ("docs/verification/textlint-tool/linting/lint-reporter.md");
            requirementFiles = ("docs/reqstream/textlint-tool/linting/lint-reporter.yaml");
        }
    }
}
