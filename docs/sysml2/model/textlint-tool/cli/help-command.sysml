package TextLintTool_HelpCommand {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    part def HelpCommand {
        doc /* Built-in help command: answers the help verb from the help cards embedded in the assembly. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Cli/HelpCommand.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/textlint-tool/cli/help-command.md");
            verificationFiles = ("docs/verification/textlint-tool/cli/help-command.md");
            requirementFiles = ("docs/reqstream/textlint-tool/cli/help-command.yaml");
        }
    }

    // Serves the help command from the cards embedded in the assembly
    dependency HelpCommand to HelpCards;
}
