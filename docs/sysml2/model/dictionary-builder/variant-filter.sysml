package DictionaryBuilder {
    part def VariantFilter {
        doc /* Applies the spelling-variant rule when the technical word lists are combined: a US-only or GB-only variant stays known only when a programming-language list or the reviewed list has it. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint.DictionaryBuilder/VariantFilter.cs (with the supporting result type VariantFilterResult.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.DictionaryBuilder.Tests/VariantFilterTests.cs */
        comment designRef /* Design: docs/design/dictionary-builder/variant-filter.md */
        comment verificationRef /* Verification: docs/verification/dictionary-builder/variant-filter.md */
        comment reqRef /* Requirements: docs/reqstream/dictionary-builder/variant-filter.yaml */
    }
}
