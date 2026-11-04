package TextLintTool {
    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/Cli/CliSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint-tool/cli.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool/cli.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool/cli.yaml */

        part context : Context;
    }
}
