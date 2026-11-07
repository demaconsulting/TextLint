### PatternCatalog

#### Verification Approach

`PatternCatalog` is verified with unit tests in `PatternCatalogTests.cs`. The unit depends on `PatternParser` and on the
.NET base class library, so the tests use the real classes and no mocks, doubles, or files: each test passes the
`ignoreRegExpList` entries and the pattern definitions to `Resolve`, passes the resolved regular expressions and a text
to `FindExcludedRanges`, and asserts on the text of the ranges it reports or on the exception it throws. The entry-level
operations that the in-document directives use, `ParseDefinitions`, `ResolveEntries`, and `TryResolveEntry`, are called
directly with parsed definitions and a short match limit, and the tests assert on the regular expression they return,
its limit, or the reason they give. The parser is not replaced because the catalog's contract includes the exceptions
that parsing raises for a definition or an expression, and its own behavior is verified by the unit tests of
`PatternParser`. The expectations for the predefined
patterns that exclude text only when listed were recorded from the pattern definitions of the cspell version that they
are ported from, run in a real JavaScript engine, so each row of the theory is a recorded cspell result: a name, a text,
and the matches cspell finds. The count and the names of the predefined patterns are tested against the explicit list of
twenty-eight names, so a pattern that is added or removed fails a test. The always-applied names are a theory with one
row per name. The five-second limit cannot be waited for in a test, so the test checks that every optional pattern
carries it, the entry-level operations take the limit as an argument so that the tests can prove that they pass a
shorter one on, and the search of a text of one million characters, by a pattern that starts a match at every position,
proves that a large text is searched completely.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Exactly twenty-eight predefined names are known, nine of them optional, and names are found in any letter case.
- Each of the nineteen names of patterns that are always applied is accepted and adds no pattern.
- Each optional predefined pattern matches the text that cspell matches, and every one has a match limit of five
  seconds.
- A pattern that is defined is applied only when the list names it, expressions and names are applied in the order of
  the list, a name listed twice is applied once, and a word between slashes is an expression.
- An unknown name, a malformed, predefined, or duplicate definition name, and an invalid pattern in the list or in a
  definition, used or not, are rejected with an exception that names the entry and gives the reason.
- Ranges are appended to the list without removing its items, empty matches are skipped, and a text of one million
  characters is searched completely.
- Empty lists resolve to nothing, null arguments are rejected, and a named pattern keeps its values and rejects null.
- A single entry resolves to a regular expression with the limit it is given, to nothing for a name that is always
  applied, or to a reason for an unsupported flag or an unknown name, without throwing; a defined name is found in any
  letter case before the predefined names.
- Parsed definitions are by name without regard to letter case and carry the limit they are given; a repeated or
  predefined name is rejected with the name as the entry.
- The entries of a list are resolved against parsed definitions with the limit, a name listed twice once, and an unknown
  name is rejected with an exception; null arguments are rejected.

#### Test Scenarios

**PatternCatalog_PredefinedNames_AreTheTwentyEightNamesOfCspell**: The names that the catalog knows are exactly the
twenty-eight of cspell, compared ignoring letter case, and nine of them have a regular expression of their own.

**PatternCatalog_PredefinedNames_AlwaysAppliedNames_AreNineteen**: The predefined names that are not optional are
exactly the nineteen names that are applied to every text.

**PatternCatalog_Resolve_AlwaysAppliedName_IsAcceptedAndAddsNothing**: Each of the nineteen always-applied names, one
row per name, resolves to no pattern, because the default patterns and the directives apply anyway.

**PatternCatalog_Resolve_PredefinedNameInOtherCase_IsFound**: A predefined name in lower case and in upper case resolves
to the same pattern.

**PatternCatalog_Resolve_OptionalPredefinedName_FindsWhatCspellFinds**: Each row, recorded from cspell, gives an optional
name, a text, and the matches cspell finds. The rows cover hexadecimal values and escapes of several lengths; public key
blocks, with a private key and a mismatched end left alone; escape characters, including an escaped backslash; Base64
data at and one below the minimum length, over several lines, and with a leading plus sign or a trailing padding sign;
`href` attributes in either case and one that spans lines; strings in three kinds of quotes, with escapes, empty, and
unclosed; line and block comments, including a URL scheme that is not a comment and an unclosed block; PHP here
documents, quoted, unclosed, and indented; and the pattern that excludes whole lines.

**PatternCatalog_OptionalRegexes_EveryPattern_HasDocumentedMatchTimeout**: Every regular expression of the optional
predefined patterns has the documented match limit of five seconds.

**PatternCatalog_Resolve_NamedPattern_IsAppliedOnlyWhenListed**: Of two defined patterns, the one that the list names in
other letter case is applied and the other is not.

**PatternCatalog_Resolve_ExpressionsAndNames_AreAppliedInListOrder**: An expression, a defined name, and a predefined
name, listed in that order, report their matches in that order.

**PatternCatalog_Resolve_NameListedTwice_IsAppliedOnce**: A defined name and a predefined name, each listed twice in
different letter case, resolve to two patterns, and the text is matched once.

**PatternCatalog_Resolve_LiteralWordBetweenSlashes_IsAnExpression**: A word that is not a name, written between slashes,
matches as literal text.

**PatternCatalog_Resolve_UnknownName_ThrowsAndAdvisesSlashes**: A misspelled predefined name is rejected with an
exception that holds the entry, says that it is not a defined or predefined name, and advises writing literal text
between slashes.

**PatternCatalog_Resolve_InvalidDefinitionName_ThrowsWithReason**: A definition named like a predefined pattern in any
letter case, and definitions whose names start with a digit, hold a space or a dot, are padded, or are empty, are
rejected with the name as the entry and the matching reason.

**PatternCatalog_Resolve_DuplicateDefinitionName_Throws**: Two definitions whose names differ only in letter case are
rejected, with the second named and a reason that says it is already defined.

**PatternCatalog_Resolve_UnusedInvalidDefinition_Throws**: A definition with an invalid pattern that the list does not
name is still rejected, with the pattern as the entry.

**PatternCatalog_Resolve_InvalidExpressions_ThrowForListAndDefinitions**: An expression with an unsupported flag (the
x flag) in the list, and an invalid pattern in a definition, are each rejected with their own entry.

**PatternCatalog_IsNameShaped_Entries_AreClassified**: Words with letters, digits, hyphens, underscores, and padding are
classified as names, and slashed expressions, expressions with metacharacters, entries that start with a digit or a
hyphen, entries with a space, and the empty entry are not.

**PatternCatalog_CheckDefinitionNameAndReference_GiveReasonOrNull**: A good definition name and a reference to a defined
or predefined name give no reason, a definition that reuses a predefined name and a reference to an unknown name give
one, and the predefined test ignores padding and does not accept a user name.

**PatternCatalog_FindExcludedRanges_ExistingListAndEmptyMatches_AreKeptAndSkipped**: A pattern that can match the empty
string adds only its runs of characters to a list that already holds a range, which is kept.

**PatternCatalog_FindExcludedRanges_OneMegabyteOfText_IsSearchedCompletely**: A text of one million characters is
searched by a pattern that starts a match at every position; the search completes and finds nothing.

**PatternCatalog_Resolve_EmptyListsAndNulls_AreHandled**: Empty lists resolve to no pattern, and a null list and a null
definition list each throw a null-argument error.

**PatternCatalog_FindExcludedRanges_NullArguments_ThrowArgumentNullException**: A null pattern list, a null text, and a
null range list each throw a null-argument error.

**NamedPattern_Constructor_KeepsValuesAndRejectsNull**: A named pattern keeps its name and pattern, equal values are
equal, and a null name or pattern throws.

**PatternCatalog_TryResolveEntry_Forms_AreResolvedOrExplained**: With parsed definitions of one defined pattern and a
limit of 250 milliseconds, an expression resolves to a regular expression that has the limit, a defined name in another
letter case resolves to the parsed pattern itself, an optional predefined name resolves to a pattern, and a name that
is always applied resolves to nothing, each with no reason; an expression with the x flag and a name that nothing
defines resolve to nothing with a reason that names the flag and a reason that says the name is not a defined or
predefined pattern name.

**PatternCatalog_ParseDefinitions_Names_AreCaseInsensitiveAndChecked**: Parsing one definition with a limit of 300
milliseconds gives a pattern that is found by its name in other letter case and has the limit; a repeat that differs
only by letter case and a name of a predefined pattern are each rejected with the name as the entry, and a null list
throws a null-argument error.

**PatternCatalog_ResolveEntries_Entries_AreResolvedOnceWithTheLimit**: A list that names a defined pattern twice in
different letter case, an expression, and a name that is always applied resolves to the parsed pattern once and the
expression with the limit of 400 milliseconds; an unknown name is rejected with an exception, and a null list, null
definitions, a null entry, and null definitions for a single entry each throw a null-argument error.

**PatternCatalog_Resolve_JavaScriptWhiteSpace_EndsAndJoinsLikeJavaScript**: Each row, recorded from cspell's JavaScript
expressions, puts the byte order mark, the no-break space, the line separator, or the next-line character where an
optional pattern reads white space, and finds the matches of JavaScript.

<!-- cspell:ignore metacharacters -->
