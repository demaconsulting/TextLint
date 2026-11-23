package TextLint_SettingsOverrideResolver {
    private import SoftwareItemMetadata::*;

    part def SettingsOverrideResolver {
        doc /* Compiles the filename lists of the settings overrides, resolves which overrides apply to a file by its path relative to the settings root, and merges the top-level settings with the matching overrides, general first and specific last, into one effective settings value and one enabled decision per file. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/Configuration/SettingsOverrideResolver.cs",
                "src/DemaConsulting.TextLint/Configuration/FileResolution.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/Configuration/SettingsOverrideResolverTests.cs");
            designFiles = ("docs/design/textlint/configuration/settings-override-resolver.md");
            verificationFiles = ("docs/verification/textlint/configuration/settings-override-resolver.md");
            requirementFiles = ("docs/reqstream/textlint/configuration/settings-override-resolver.yaml");
        }
    }

    // The resolver reads the settings value, matches each override's filename list with the ordered
    // glob list of the FileSelection subsystem, and checks override languages with the language
    // selection of the Dictionaries subsystem (documented subsystem dependencies).
    dependency SettingsOverrideResolverUsesTextLintSettings from SettingsOverrideResolver to TextLint::TextLintSettings;
    dependency SettingsOverrideResolverUsesOrderedGlobList from SettingsOverrideResolver to TextLint::OrderedGlobList;
    dependency SettingsOverrideResolverUsesSpellingDictionary from SettingsOverrideResolver to TextLint::SpellingDictionary;
}
