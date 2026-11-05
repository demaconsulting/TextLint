package TextLint {
    part def FileSelectionSubsystem {
        doc /* Applies include and ignore glob patterns to a root directory and returns the ordered list of files to check, following the glob pattern and hidden-file rules. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/FileSelectionSubsystemTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection.yaml */

        part globPatternExpander : GlobPatternExpander;
        part hiddenPathPolicy : HiddenPathPolicy;
        part fileSelector : FileSelector;
    }
}
