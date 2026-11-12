package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Program.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/ProgramTests.cs");
            designFiles = ("docs/design/textlint-tool/program.md");
            verificationFiles = ("docs/verification/textlint-tool/program.md");
            requirementFiles = ("docs/reqstream/textlint-tool/program.yaml");
        }
    }
}
