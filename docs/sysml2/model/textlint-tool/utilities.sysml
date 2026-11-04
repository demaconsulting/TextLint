package TextLintTool {
    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/Utilities/UtilitiesSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint-tool/utilities.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool/utilities.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool/utilities.yaml */

        part pathHelpers : PathHelpers;
    }
}
