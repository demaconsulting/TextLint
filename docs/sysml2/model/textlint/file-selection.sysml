package TextLint {
    private import SoftwareItemMetadata::*;

    part def FileSelectionSubsystem {
        doc /* Applies include and ignore glob patterns to a root directory and returns the ordered list of files to check, following the glob pattern and hidden-file rules. */

        metadata info : SoftwareInfo {
            description = "Applies include and ignore glob patterns to a root directory and returns the ordered list of files to check, following the glob pattern and hidden-file rules.";
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/FileSelectionSubsystemTests.cs");
            designFiles = ("docs/design/textlint/file-selection.md");
            verificationFiles = ("docs/verification/textlint/file-selection.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection.yaml");
        }

        part globPatternExpander : GlobPatternExpander;
        part hiddenPathPolicy : HiddenPathPolicy;
        part fileSelector : FileSelector;
        part commandLinePathResolver : CommandLinePathResolver;
    }
}
