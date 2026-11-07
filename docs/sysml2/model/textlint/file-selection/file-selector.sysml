package TextLint {
    part def FileSelector {
        doc /* Applies include and ignore glob patterns to a root directory and returns the ordered, case-sensitively matched files, skipping excluded and hidden directories without reading them. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/FileSelection/FileSelector.cs (supporting types: SelectedFile.cs, HiddenFilteringDirectory.cs, CancelableDirectory.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/FileSelectorTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection/file-selector.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection/file-selector.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection/file-selector.yaml */
    }

    // The selector takes its inputs as plain parameters, so it does not depend on the Configuration
    // subsystem; it uses the pattern expander and hidden-path policy of its own subsystem, and
    // matches with the globbing library.
    dependency FileSelectorUsesGlobPatternExpander from FileSelector to GlobPatternExpander;
    dependency FileSelectorUsesHiddenPathPolicy from FileSelector to HiddenPathPolicy;
    dependency FileSelectorUsesFileSystemGlobbing from FileSelector to FileSystemGlobbing;
}
