package TextLintTool {
    doc
    /*
     * TextLintTool is the .NET command-line application (command textlint) that
     * provides fast text linting for code and documentation. It is a thin front-end
     * only: argument parsing, console and log output, exit codes, self-validation, and
     * calling the TextLint library. All linting logic lives in the library.
     */

    part def TextLintToolSystem {
        doc /* The TextLintTool system as a whole: the thin command-line front-end over the TextLint library. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tool.Tests/IntegrationTests.cs */
        comment designRef /* Design: docs/design/textlint-tool.md */
        comment verificationRef /* Verification: docs/verification/textlint-tool.md */
        comment reqRef /* Requirements: docs/reqstream/textlint-tool.yaml */

        part program : Program;
        part cli : CliSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
    }

    // The tool is a thin command-line front-end: every linting capability lives in the
    // DemaConsulting.TextLint library, which the tool calls but does not contain.
    dependency TextLintToolDependsOnTextLint from TextLintToolSystem to TextLint::TextLintSystem;
}
