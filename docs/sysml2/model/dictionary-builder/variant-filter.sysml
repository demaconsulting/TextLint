package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def VariantFilter {
        doc /* Applies the spelling-variant rule when the technical word lists are combined: a US-only or GB-only variant stays known only when a programming-language list or the reviewed list has it. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint.DictionaryBuilder/VariantFilter.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/VariantFilterResult.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/VariantFilterTests.cs");
            designFiles = ("docs/design/dictionary-builder/variant-filter.md");
            verificationFiles = ("docs/verification/dictionary-builder/variant-filter.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/variant-filter.yaml");
        }
    }
}
