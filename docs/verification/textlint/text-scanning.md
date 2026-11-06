## TextScanning

### Verification Approach

The `TextScanning` subsystem is verified by integration tests in `TextScanningSubsystemTests.cs`. They drive
`WordTokenizer`, `DefaultPatterns`, `DocumentDirectives`, `PatternCatalog` (with `PatternParser` and `NamedPattern`),
`MarkdownPatterns`, and `RandomStringDetector` together through the subsystem boundary the way a spell checker does:
the excluded ranges of the patterns and of the directives are combined, and the words, tokens, and list entries of the
tokenizer are then filtered and compared with them. All units are real; the subsystem has no dependencies on other
subsystems and no external service, so there is nothing to replace with a double. The tests use text written in the
test, so they need no files. The exhaustive behavior of each unit is verified by the unit tests of that unit; the
subsystem tests prove that the units compose, for example that the words inside a URL, a disabled block, or an ignore
directive are not left among the words to check, that the patterns of the settings and the names they use find their
ranges, that an unusable pattern is rejected with its entry and reason, that the Markdown patterns apply to a Markdown
name only, and that a random-looking token and the hexadecimal sequence of a token are recognized.

### Test Environment

N/A - standard test environment. The tests use no files, no network, and no configuration; they run on every platform
the test project supports.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Identifiers and prose are split into the expected pieces, positions count UTF-16 code units, and malformed text and
  a very long line are processed without error.
- A word-list entry with digits matches the trimmed token of the text, an entry written as a word matches the word in
  any letter case, an entry that could never match is rejected with a reason, and a flagged entry carries its
  suggestions.
- URLs, email addresses, hexadecimal values, UUIDs, and encoded data are excluded from the words to check, and ordinary
  prose is not.
- A disabled block, an ignore directive, and every directive prefix switch checking off as documented, an ignore
  directive adds its words, an ignoreRegExp directive adds its pattern as written and is not reported as unsupported,
  and an unsupported directive is reported at its name.
- Patterns written as expressions between slashes, as bare expressions, and as defined and predefined names find the
  ranges they match with the case rules of their form, and a pattern that cannot be used is rejected with the entry
  and the reason.
- The Markdown patterns apply to a Markdown source name and not to another, and find link targets and entities.
- A token that looks like random data is recognized and an identifier made of words is not, and the hexadecimal
  sequence inside a token is found.

### Test Scenarios

**TextScanningSubsystem_ExtractWords_IdentifiersAndProse_YieldsCheckedPieces**: A text with camel case joined by an
underscore and a hyphen, an apostrophe word, an acronym, an accented word, a Han word, and a word with a digit is
reduced to the expected pieces; the Han word is left out and the digit ends a word.

**TextScanningSubsystem_ExtractWords_AfterEmoji_PositionsAreUtf16**: A word after an emoji is positioned after the two
code units of the emoji.

**TextScanningSubsystem_ExtractWords_MalformedText_CompletesWithoutError**: Lone surrogates around words and a line of
one megabyte pass through every unit without error and the words are all found.

**TextScanningSubsystem_MatchListEntry_TokenEntry_MatchesTrimmedToken**: The entry `k8s` is a token entry, and the
trimmed token of `(k8s).` in a sentence equals it.

**TextScanningSubsystem_MatchListEntry_WordEntry_MatchesWordInAnyCase**: The entry `Alpha` is a single word and
matches the word `alpha` of a text ignoring letter case.

**TextScanningSubsystem_MatchListEntry_EntryThatCannotMatch_IsRejectedWithReason**: An entry with white space is
rejected with a reason, and the text that holds it consists of two separate words.

**TextScanningSubsystem_MatchListEntry_FlaggedEntryWithSuggestions_CarriesSuggestions**: An entry in the arrow form
gives a word that matches the text and both suggestions.

**TextScanningSubsystem_FindExcludedRanges_UrlAndEmail_AreExcludedFromWords**: Prose with a URL and an email address
leaves only the prose words.

**TextScanningSubsystem_FindExcludedRanges_HexUuidAndEncodedData_AreExcludedFromWords**: A hash, a hexadecimal literal,
a color, a UUID, and encoded data are excluded and the prose around them is kept.

**TextScanningSubsystem_FindExcludedRanges_OrdinaryProse_ExcludesNothing**: Every word of an ordinary sentence is kept.

**TextScanningSubsystem_Scan_DisabledBlock_ExcludesWordsInside**: The words of a disabled block and of its directives
are not checked, and the words around the block are.

**TextScanningSubsystem_Scan_IgnoreDirective_AddsWordsAndExcludesDirectiveText**: An ignore directive adds its words as
ignored words, and the words of the directive are not among the words to check.

**TextScanningSubsystem_Scan_IgnoreRegExpDirective_AddsItsPattern**: An ignoreRegExp directive after some text adds its
pattern as written (a slash pattern with the i flag) at its position, nothing is reported as unsupported, and the words
of the directive are not among the words to check.

**TextScanningSubsystem_Scan_UnsupportedDirective_IsReportedAtItsName**: An unsupported directive is reported at the
range of its name, and the text before it is still checked.

**TextScanningSubsystem_Scan_EveryPrefix_SwitchesOffCheckingAlike**: A disable-line directive written with each of the
accepted prefixes hides the words of its line.

**TextScanningSubsystem_ScanDocument_DisabledBlockUrlAndIgnoreDirective_LeavesOnlyCheckedWords**: A document with a
disabled block that holds a URL, a visible URL and email address, and an ignore directive reports the ignored word and
leaves only the visible prose to check.

**TextScanningSubsystem_ResolvePatterns_ExpressionsAndNames_FindMatchedRanges**: A case-sensitive expression between
slashes, a bare expression, a defined name, and the predefined `string` name are resolved together and find their
matches in a text; the bare expression and the defined pattern ignore letter case, the case-sensitive one does not,
and text that none of them matches is left alone.

**TextScanningSubsystem_ResolvePatterns_UnusableEntry_ThrowsWithEntryAndReason**: An expression with an unsupported
flag (the x flag), a name that nothing defines, and an invalid expression are each rejected with an exception that
holds the entry
and a reason about the flag, the pattern name, or the unclosed group.

**TextScanningSubsystem_MarkdownPatterns_LinkTarget_IsExcludedOnlyForMarkdownNames**: The patterns apply to a `.md`
name and not to a `.txt` name, and on a line with a link and an entity they find the link target and the entity.

**TextScanningSubsystem_RandomStrings_TokenAndHexSequence_AreRecognized**: A token of digits, words, and hyphens looks
random, a long camel-case identifier does not, and the hexadecimal part of a token that starts with a word is found.
