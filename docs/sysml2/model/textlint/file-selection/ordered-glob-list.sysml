package TextLint_OrderedGlobList {
    private import SoftwareItemMetadata::*;

    part def OrderedGlobList {
        doc /* Compiles an ordered list of glob patterns in which a leading '!' removes files that an earlier pattern selected, decides whether a path is selected (the last matching pattern wins), and tells the file walk which subtrees it may skip. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.TextLint/FileSelection/OrderedGlobList.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/OrderedGlobListTests.cs");
            designFiles = ("docs/design/textlint/file-selection/ordered-glob-list.md");
            verificationFiles = ("docs/verification/textlint/file-selection/ordered-glob-list.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection/ordered-glob-list.yaml");
        }
    }

    // The list expands each pattern with the pattern expander, applies the hidden-name rule of the
    // hidden-path policy to each pattern, and matches with the globbing library.
    dependency OrderedGlobListUsesGlobPatternExpander from OrderedGlobList to TextLint::GlobPatternExpander;
    dependency OrderedGlobListUsesHiddenPathPolicy from OrderedGlobList to TextLint::HiddenPathPolicy;
    dependency OrderedGlobListUsesFileSystemGlobbing from OrderedGlobList to OtsDependencies::FileSystemGlobbing;
}
