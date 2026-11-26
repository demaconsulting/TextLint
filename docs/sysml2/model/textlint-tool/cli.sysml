package TextLintTool_CliSubsystem {
    public import TextLintTool_Context::*;
    public import TextLintTool_HelpCommand::*;
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, exit-code tracking, and the built-in help command. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/textlint-tool/cli.md");
            verificationFiles = ("docs/verification/textlint-tool/cli.md");
            requirementFiles = ("docs/reqstream/textlint-tool/cli.yaml");
        }

        part context : Context;
        part helpCommand : HelpCommand;
    }
}
