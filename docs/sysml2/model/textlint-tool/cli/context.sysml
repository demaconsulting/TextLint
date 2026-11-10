package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation. */

        metadata info : SoftwareInfo {
            description = "Command-line argument parser and I/O owner for one tool invocation.";
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/textlint-tool/cli/context.md");
            verificationFiles = ("docs/verification/textlint-tool/cli/context.md");
            requirementFiles = ("docs/reqstream/textlint-tool/cli/context.yaml");
        }
    }
}
