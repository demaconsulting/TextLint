package TextLint {
    private import SoftwareItemMetadata::*;

    part def GlobPatternExpander {
        doc /* Validates a user-written glob pattern and rewrites it into matcher patterns: brace alternatives expanded, bare names matching at any depth. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/FileSelection/GlobPatternExpander.cs",
                "src/DemaConsulting.TextLint/FileSelection/InvalidGlobPatternException.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/GlobPatternExpanderTests.cs");
            designFiles = ("docs/design/textlint/file-selection/glob-pattern-expander.md");
            verificationFiles = ("docs/verification/textlint/file-selection/glob-pattern-expander.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection/glob-pattern-expander.yaml");
        }
    }
}
