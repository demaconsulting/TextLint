package TextLintTool {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * TextLintTool is the .NET command-line application (command textlint) that
     * provides fast text linting for code and documentation. It is a thin front-end
     * only: argument parsing, console and log output, exit codes, self-validation, and
     * calling the TextLint library. All linting logic lives in the library.
     */

    part def TextLintToolSystem {
        doc /* The TextLintTool system as a whole: the thin command-line front-end over the TextLint library. */

        metadata info : SoftwareInfo {
            description = "The TextLintTool system as a whole: the thin command-line front-end over the TextLint library.";
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/textlint-tool.md");
            verificationFiles = ("docs/verification/textlint-tool.md");
            requirementFiles = ("docs/reqstream/textlint-tool.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part linting : LintingSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
    }

    // The tool is a thin command-line front-end: every linting capability lives in the
    // DemaConsulting.TextLint library, which the tool calls but does not contain.
    dependency TextLintToolDependsOnTextLint from TextLintToolSystem to TextLint::TextLintSystem;
}
