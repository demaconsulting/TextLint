package TextLint {
    private import SoftwareItemMetadata::*;

    part def ConfigurationSubsystem {
        doc /* Turns a .textlint.yaml file, or values built in code, into one immutable settings value, including the spelling settings, and reports configuration errors with their positions. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.TextLint.Tests/Configuration/ConfigurationSubsystemTests.cs");
            designFiles = ("docs/design/textlint/configuration.md");
            verificationFiles = ("docs/verification/textlint/configuration.md");
            requirementFiles = ("docs/reqstream/textlint/configuration.yaml");
        }

        part textLintSettings : TextLintSettings;
        part settingsLoader : SettingsLoader;
    }
}
