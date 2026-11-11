package TextLint {
    private import SoftwareItemMetadata::*;

    part def SettingsLoader {
        doc /* Reads a .textlint.yaml file into settings, or into positioned errors, validating glob patterns, languages, word-list entries, regular-expression patterns, and numbers while loading. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/Configuration/SettingsLoader.cs",
                "src/DemaConsulting.TextLint/Configuration/SettingsLoadResult.cs",
                "src/DemaConsulting.TextLint/Configuration/SettingsDiagnostic.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/Configuration/SettingsLoaderTests.cs");
            designFiles = ("docs/design/textlint/configuration/settings-loader.md");
            verificationFiles = ("docs/verification/textlint/configuration/settings-loader.md");
            requirementFiles = ("docs/reqstream/textlint/configuration/settings-loader.yaml");
        }
    }

    // The loader builds TextLintSettings, parses with YamlDotNet, and validates values with the
    // glob pattern expander of the FileSelection subsystem, the language selection of the
    // Dictionaries subsystem, and the word tokenizer of the TextScanning subsystem (documented
    // subsystem dependencies).
    dependency SettingsLoaderUsesTextLintSettings from SettingsLoader to TextLintSettings;
    dependency SettingsLoaderUsesYamlDotNet from SettingsLoader to YamlDotNet;
    dependency SettingsLoaderUsesGlobPatternExpander from SettingsLoader to GlobPatternExpander;
    dependency SettingsLoaderUsesSpellingDictionary from SettingsLoader to SpellingDictionary;
    dependency SettingsLoaderUsesWordTokenizer from SettingsLoader to WordTokenizer;
    dependency SettingsLoaderUsesPatternCatalog from SettingsLoader to PatternCatalog;
    dependency SettingsLoaderUsesPatternParser from SettingsLoader to PatternParser;
}
