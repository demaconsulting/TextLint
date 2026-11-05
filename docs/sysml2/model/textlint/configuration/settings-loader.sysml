package TextLint {
    part def SettingsLoader {
        doc /* Reads a .textlint.yaml file into settings, or into positioned errors, validating glob patterns, languages, word-list entries, and numbers while loading. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/Configuration/SettingsLoader.cs (supporting types: SettingsLoadResult.cs, SettingsDiagnostic.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Configuration/SettingsLoaderTests.cs */
        comment designRef /* Design: docs/design/textlint/configuration/settings-loader.md */
        comment verificationRef /* Verification: docs/verification/textlint/configuration/settings-loader.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/configuration/settings-loader.yaml */
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
}
