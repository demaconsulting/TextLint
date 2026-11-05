package TextLint {
    doc
    /*
     * TextLint is the DemaConsulting.TextLint .NET library. It owns ALL linting logic -
     * settings and configuration loading, file discovery and globbing, dictionaries,
     * tokenization, spell checking, and results - and is distributed as a NuGet package.
     * It is consumed by the textlint command-line tool (a thin front-end) and by any other
     * .NET application, and never depends on the tool.
     */

    part def TextLintSystem {
        doc /* The DemaConsulting.TextLint library system as a whole: the home of all linting logic. */

        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/TextLintTests.cs */
        comment designRef /* Design: docs/design/textlint.md */
        comment verificationRef /* Verification: docs/verification/textlint.md */
        comment reqRef /* Requirements: docs/reqstream/textlint.yaml */

        part configuration : ConfigurationSubsystem;
        part fileSelection : FileSelectionSubsystem;
        part dictionaries : DictionariesSubsystem;
        part textScanning : TextScanningSubsystem;
        part spellChecking : SpellCheckingSubsystem;

        // Runtime OTS dependencies: both ship as package dependencies of the library.
        part yamlDotNet : YamlDotNet;
        part fileSystemGlobbing : FileSystemGlobbing;
    }

    // The library's build runs the DictionaryBuilder to turn the word lists into the dictionaries
    // that the library embeds, and compiles one of its source files (the shared format definitions).
    // The builder is not shipped, and it depends on nothing in the library.
    dependency TextLintBuildsWithDictionaryBuilder from TextLintSystem to DictionaryBuilder::DictionaryBuilderSystem;
}
