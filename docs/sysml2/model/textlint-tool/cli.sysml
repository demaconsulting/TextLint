package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/textlint-tool/cli.md");
            verificationFiles = ("docs/verification/textlint-tool/cli.md");
            requirementFiles = ("docs/reqstream/textlint-tool/cli.yaml");
        }

        part context : Context;
    }
}
