package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/textlint-tool/self-test.md");
            verificationFiles = ("docs/verification/textlint-tool/self-test.md");
            requirementFiles = ("docs/reqstream/textlint-tool/self-test.yaml");
        }

        part validation : Validation;
    }
}
