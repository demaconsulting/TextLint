package TextLint_SettingsLoader {
    private import SoftwareItemMetadata::*;

    part def SettingsLoader {
        doc /* Reads a .textlint.yaml file into settings, or into positioned errors, validating ordered glob patterns, overrides, languages, word-list entries, regular-expression patterns, and numbers while loading. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/Configuration/SettingsLoader.cs",
                "src/DemaConsulting.TextLint/Configuration/SettingsLoader.Overrides.cs",
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
    // subsystem dependencies). It checks the ordered file lists with the ordered glob list.
    dependency SettingsLoaderUsesTextLintSettings from SettingsLoader to TextLint::TextLintSettings;
    dependency SettingsLoaderUsesYamlDotNet from SettingsLoader to OtsDependencies::YamlDotNet;
    dependency SettingsLoaderUsesGlobPatternExpander from SettingsLoader to TextLint::GlobPatternExpander;
    dependency SettingsLoaderUsesOrderedGlobList from SettingsLoader to TextLint::OrderedGlobList;
    dependency SettingsLoaderUsesSpellingDictionary from SettingsLoader to TextLint::SpellingDictionary;
    dependency SettingsLoaderUsesWordTokenizer from SettingsLoader to TextLint::WordTokenizer;
    dependency SettingsLoaderUsesPatternCatalog from SettingsLoader to TextLint::PatternCatalog;
    dependency SettingsLoaderUsesPatternParser from SettingsLoader to TextLint::PatternParser;
}
