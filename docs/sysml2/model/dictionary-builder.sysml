package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * DictionaryBuilder is the build-time system that converts the plain-text word lists under
     * dictionaries/ into the binary dictionary files that the TextLint library embeds. It is not
     * packaged or shipped: it runs inside the library's build, and its output is the shipped data.
     * It depends on nothing but the .NET base class library.
     */

    part def DictionaryBuilderSystem {
        doc /* The dictionary builder system as a whole: converts word-list folders into binary dictionary files. */

        metadata info : SoftwareInfo {
            description = "The dictionary builder system as a whole: converts word-list folders into binary dictionary files.";
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/DictionaryBuilderTests.cs");
            designFiles = ("docs/design/dictionary-builder.md");
            verificationFiles = ("docs/verification/dictionary-builder.md");
            requirementFiles = ("docs/reqstream/dictionary-builder.yaml");
        }

        part builderProgram : BuilderProgram;
        part wordListReader : WordListReader;
        part dictionaryWriter : DictionaryWriter;
        part dictionaryFormat : DictionaryFormat;
        part variantFilter : VariantFilter;
        part exclusionFilter : ExclusionFilter;
    }

    // BuilderProgram drives the reader, the variant filter, the exclusion filter, and the writer; the reader and the writer
    // both rely on the shared format definitions.
    dependency BuilderProgramUsesWordListReader from BuilderProgram to WordListReader;
    dependency BuilderProgramUsesVariantFilter from BuilderProgram to VariantFilter;
    dependency BuilderProgramUsesExclusionFilter from BuilderProgram to ExclusionFilter;
    dependency BuilderProgramUsesDictionaryWriter from BuilderProgram to DictionaryWriter;
    dependency WordListReaderUsesDictionaryFormat from WordListReader to DictionaryFormat;
    dependency DictionaryWriterUsesDictionaryFormat from DictionaryWriter to DictionaryFormat;
}
