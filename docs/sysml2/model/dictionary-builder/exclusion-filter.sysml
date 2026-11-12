package DictionaryBuilder {
    private import SoftwareItemMetadata::*;

    part def ExclusionFilter {
        doc /* Removes the words of the reviewed exclusions list from every dictionary, so that an excluded word is never known, and reports the exclusions that no dictionary has. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.TextLint.DictionaryBuilder/ExclusionFilter.cs",
                "src/DemaConsulting.TextLint.DictionaryBuilder/ExclusionFilterResult.cs"
            );
            testFiles = ("test/DemaConsulting.TextLint.DictionaryBuilder.Tests/ExclusionFilterTests.cs");
            designFiles = ("docs/design/dictionary-builder/exclusion-filter.md");
            verificationFiles = ("docs/verification/dictionary-builder/exclusion-filter.md");
            requirementFiles = ("docs/reqstream/dictionary-builder/exclusion-filter.yaml");
        }
    }
}
