package DictionaryBuilder {
    part def ExclusionFilter {
        doc /* Removes the words of the reviewed exclusions list from every dictionary, so that an excluded word is never known, and reports the exclusions that no dictionary has. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint.DictionaryBuilder/ExclusionFilter.cs (with the supporting result type ExclusionFilterResult.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.DictionaryBuilder.Tests/ExclusionFilterTests.cs */
        comment designRef /* Design: docs/design/dictionary-builder/exclusion-filter.md */
        comment verificationRef /* Verification: docs/verification/dictionary-builder/exclusion-filter.md */
        comment reqRef /* Requirements: docs/reqstream/dictionary-builder/exclusion-filter.yaml */
    }
}
