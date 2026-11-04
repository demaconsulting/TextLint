package TextLintTool {
    doc
    /*
     * TextLintTool is the .NET command-line application (command textlint) that
     * provides fast text linting for code and documentation.
     */

    part def TextLintToolSystem {
        doc /* The TextLintTool system as a whole. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/IntegrationTests.cs */
        comment designRef /* Design: docs/design/textlint-tool.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool.yaml */

        part program : Program;
        part cli : CliSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
    }
}
