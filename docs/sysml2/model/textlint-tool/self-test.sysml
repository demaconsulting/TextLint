package TextLintTool {
    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/SelfTest/SelfTestSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint-tool/self-test.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool/self-test.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool/self-test.yaml */

        part validation : Validation;
    }
}
