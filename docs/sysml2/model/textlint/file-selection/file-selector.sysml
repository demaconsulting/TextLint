package TextLint {
    part def FileSelector {
        doc /* Applies settings to a root directory and returns the ordered, case-sensitively matched files, skipping excluded and hidden directories without reading them. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/FileSelection/FileSelector.cs (supporting types: SelectedFile.cs, HiddenFilteringDirectory.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/FileSelectorTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection/file-selector.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection/file-selector.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection/file-selector.yaml */
    }

    // The selector takes TextLintSettings as input (a documented subsystem dependency), uses the
    // pattern expander and hidden-path policy of its own subsystem, and matches with the globbing library.
    dependency FileSelectorUsesTextLintSettings from FileSelector to TextLintSettings;
    dependency FileSelectorUsesGlobPatternExpander from FileSelector to GlobPatternExpander;
    dependency FileSelectorUsesHiddenPathPolicy from FileSelector to HiddenPathPolicy;
    dependency FileSelectorUsesFileSystemGlobbing from FileSelector to FileSystemGlobbing;
}
