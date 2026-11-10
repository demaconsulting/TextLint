package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite and reports pass/fail results. */

        metadata info : SoftwareInfo {
            description = "Orchestrates the self-validation test suite and reports pass/fail results.";
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/textlint-tool/self-test/validation.md");
            verificationFiles = ("docs/verification/textlint-tool/self-test/validation.md");
            requirementFiles = ("docs/reqstream/textlint-tool/self-test/validation.yaml");
        }
    }
}
