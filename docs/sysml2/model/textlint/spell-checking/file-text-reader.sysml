package TextLint {
    private import SoftwareItemMetadata::*;

    part def FileTextReader {
        doc /* Reads a file into text for checking: chooses the encoding, removes the byte order mark, and recognizes files that are binary, too large, or unreadable, returning the reason as data. */

        metadata info : SoftwareInfo {
            description = "Reads a file into text for checking: chooses the encoding, removes the byte order mark, and recognizes files that are binary, too large, or unreadable, returning the reason as data.";
            sourceFiles = (
                "src/DemaConsulting.TextLint/SpellChecking/FileTextReader.cs",
                "src/DemaConsulting.TextLint/SpellChecking/FileTextContent.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.Tests/SpellChecking/FileTextReaderTests.cs");
            designFiles = ("docs/design/textlint/spell-checking/file-text-reader.md");
            verificationFiles = ("docs/verification/textlint/spell-checking/file-text-reader.md");
            requirementFiles = ("docs/reqstream/textlint/spell-checking/file-text-reader.yaml");
        }
    }
}
