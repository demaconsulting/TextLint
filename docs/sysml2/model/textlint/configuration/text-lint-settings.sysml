package TextLint {
    private import SoftwareItemMetadata::*;

    part def TextLintSettings {
        doc /* The immutable settings value (version, include and ignore patterns, hidden-file switch, language, word lists, minimum word length, maximum file size, ignore patterns and named patterns, and the random-string switch and length) shared by the file and code routes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint/Configuration/TextLintSettings.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/Configuration/TextLintSettingsTests.cs");
            designFiles = ("docs/design/textlint/configuration/text-lint-settings.md");
            verificationFiles = ("docs/verification/textlint/configuration/text-lint-settings.md");
            requirementFiles = ("docs/reqstream/textlint/configuration/text-lint-settings.yaml");
        }
    }

    // The settings hold the named patterns, a value type that the pattern catalog defines (a documented
    // dependency of the code).
    dependency TextLintSettingsUsesPatternCatalog from TextLintSettings to PatternCatalog;
}
