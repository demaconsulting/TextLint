package TextLint {
    private import SoftwareItemMetadata::*;

    part def FileSelector {
        doc /* Applies the ordered include patterns (a leading '!' removes files) and the ignore patterns to a root directory and returns the ordered, case-sensitively matched files, skipping excluded and hidden directories without reading them. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/FileSelection/FileSelector.cs",
                "src/DemaConsulting.TextLint/FileSelection/SelectedFile.cs",
                "src/DemaConsulting.TextLint/FileSelection/HiddenFilteringDirectory.cs",
                "src/DemaConsulting.TextLint/FileSelection/CancelableDirectory.cs",
                "src/DemaConsulting.TextLint/FileSelection/OrderedPruningDirectory.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/FileSelectorTests.cs");
            designFiles = ("docs/design/textlint/file-selection/file-selector.md");
            verificationFiles = ("docs/verification/textlint/file-selection/file-selector.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection/file-selector.yaml");
        }
    }

    // The selector takes its inputs as plain parameters, so it does not depend on the Configuration
    // subsystem; it uses the pattern expander and hidden-path policy of its own subsystem, and
    // matches with the globbing library. It keeps the include patterns in an ordered glob list so that
    // a leading '!' removes files and the walk can skip excluded subtrees.
    dependency FileSelectorUsesGlobPatternExpander from FileSelector to GlobPatternExpander;
    dependency FileSelectorUsesOrderedGlobList from FileSelector to OrderedGlobList;
    dependency FileSelectorUsesHiddenPathPolicy from FileSelector to HiddenPathPolicy;
    dependency FileSelectorUsesFileSystemGlobbing from FileSelector to FileSystemGlobbing;
}
