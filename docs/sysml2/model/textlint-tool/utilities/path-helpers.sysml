package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint.Tool/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/textlint-tool/utilities/path-helpers.md");
            verificationFiles = ("docs/verification/textlint-tool/utilities/path-helpers.md");
            requirementFiles = ("docs/reqstream/textlint-tool/utilities/path-helpers.yaml");
        }
    }
}
