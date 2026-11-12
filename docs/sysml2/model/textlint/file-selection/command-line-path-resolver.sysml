package TextLint {
    private import SoftwareItemMetadata::*;

    part def CommandLinePathResolver {
        doc /* Turns the path and pattern arguments typed on a command line (backslash paths on Windows, absolute paths) into a search directory and validated portable patterns, and defines the result that names the files found. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint/FileSelection/CommandLinePathResolver.cs",
                "src/DemaConsulting.TextLint/FileSelection/CommandLineSelection.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/CommandLinePathResolverTests.cs");
            designFiles = ("docs/design/textlint/file-selection/command-line-path-resolver.md");
            verificationFiles = ("docs/verification/textlint/file-selection/command-line-path-resolver.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection/command-line-path-resolver.yaml");
        }
    }
}
