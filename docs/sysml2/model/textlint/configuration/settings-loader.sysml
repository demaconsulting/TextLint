package TextLint {
    part def SettingsLoader {
        doc /* Reads a .textlint.yaml file into settings, or into positioned errors, validating glob patterns while loading. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/Configuration/SettingsLoader.cs (supporting types: SettingsLoadResult.cs, SettingsDiagnostic.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/Configuration/SettingsLoaderTests.cs */
        comment designRef /* Design: docs/design/textlint/configuration/settings-loader.md */
        comment verificationRef /* Verification: docs/verification/textlint/configuration/settings-loader.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/configuration/settings-loader.yaml */
    }

    // The loader builds TextLintSettings, parses with YamlDotNet, and validates patterns with the
    // glob pattern expander of the FileSelection subsystem (a documented subsystem dependency).
    dependency SettingsLoaderUsesTextLintSettings from SettingsLoader to TextLintSettings;
    dependency SettingsLoaderUsesYamlDotNet from SettingsLoader to YamlDotNet;
    dependency SettingsLoaderUsesGlobPatternExpander from SettingsLoader to GlobPatternExpander;
}
