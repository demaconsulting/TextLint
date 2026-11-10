package TextLintTool {
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            description = "Shared, independently testable helper functions used by other subsystems.";
            testFiles = ("test/DemaConsulting.TextLint.Tool.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/textlint-tool/utilities.md");
            verificationFiles = ("docs/verification/textlint-tool/utilities.md");
            requirementFiles = ("docs/reqstream/textlint-tool/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
