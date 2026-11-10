package TextLint {
    private import SoftwareItemMetadata::*;

    part def SpellCheckingSubsystem {
        doc /* Checks the spelling of a string of text, and of the files that the settings select under a directory, in parallel, and returns every finding and every file problem in a deterministic order. */

        metadata info : SoftwareInfo {
            description = "Checks the spelling of a string of text, and of the files that the settings select under a directory, in parallel, and returns every finding and every file problem in a deterministic order.";
            testFiles = ("test/DemaConsulting.TextLint.Tests/SpellChecking/SpellCheckingSubsystemTests.cs");
            designFiles = ("docs/design/textlint/spell-checking.md");
            verificationFiles = ("docs/verification/textlint/spell-checking.md");
            requirementFiles = ("docs/reqstream/textlint/spell-checking.yaml");
        }

        part textChecker : TextChecker;
        part fileTextReader : FileTextReader;
        part spellChecker : SpellChecker;
    }
}
