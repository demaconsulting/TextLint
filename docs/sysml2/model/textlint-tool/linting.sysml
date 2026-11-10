package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def LintingSubsystem {
        doc /* Runs one spell check as the command line asked for it: finds and loads the settings, applies the command-line overrides, calls the TextLint library, reports the result, and decides the exit status. */

        metadata info : SoftwareInfo {
            description = "Runs one spell check as the command line asked for it: finds and loads the settings, applies the command-line overrides, calls the TextLint library, reports the result, and decides the exit status.";
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Linting/LintingSubsystemTests.cs");
            designFiles = ("docs/design/textlint-tool/linting.md");
            verificationFiles = ("docs/verification/textlint-tool/linting.md");
            requirementFiles = ("docs/reqstream/textlint-tool/linting.yaml");
        }

        part lintRunner : LintRunner;
        part lintReporter : LintReporter;
    }
}
