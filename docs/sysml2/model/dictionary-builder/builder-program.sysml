package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def BuilderProgram {
        doc /* Entry point of the builder: parses the command line, drives the reader, the variant filter, the exclusion filter, and the writer, writes the files all-or-nothing, and reports problems in the compiler's error format. */

        metadata info : SoftwareInfo {
            description = "Entry point of the builder: parses the command line, drives the reader, the variant filter, the exclusion filter, and the writer, writes the files all-or-nothing, and reports problems in the compiler's error format.";
            sourceFiles = (
                "src/DemaConsulting.TextLint.DictionaryBuilder/BuilderProgram.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/FileOperations.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/BuilderProgramTests.cs");
            designFiles = ("docs/design/dictionary-builder/builder-program.md");
            verificationFiles = ("docs/verification/dictionary-builder/builder-program.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/builder-program.yaml");
        }
    }
}
