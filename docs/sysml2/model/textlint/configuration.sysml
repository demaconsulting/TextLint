package TextLint {
    part def ConfigurationSubsystem {
        doc /* Turns a .textlint.yaml file, or values built in code, into one immutable settings value, including the spelling settings, and reports configuration errors with their positions. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Configuration/ConfigurationSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint/configuration.md */
        comment verificationRef /* Verification: docs/verification/textlint/configuration.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/configuration.yaml */

        part textLintSettings : TextLintSettings;
        part settingsLoader : SettingsLoader;
    }
}
