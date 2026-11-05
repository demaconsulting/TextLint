package TextLint {
    part def FileTextReader {
        doc /* Reads a file into text for checking: chooses the encoding, removes the byte order mark, and recognizes files that are binary, too large, or unreadable, returning the reason as data. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/SpellChecking/FileTextReader.cs (supporting type: FileTextContent.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/SpellChecking/FileTextReaderTests.cs */
        comment designRef /* Design: docs/design/textlint/spell-checking/file-text-reader.md */
        comment verificationRef /* Verification: docs/verification/textlint/spell-checking/file-text-reader.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/spell-checking/file-text-reader.yaml */
    }
}
