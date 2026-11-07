### PatternParser

#### Verification Approach

`PatternParser` is verified with unit tests in `PatternParserTests.cs`. The unit, together with the translator of
JavaScript regular expression syntax that it relies on, is pure logic over strings and depends on nothing but the .NET
base class library, so no mocks, doubles, or files are used: each test passes a pattern entry to `Parse` and either
searches a text with the regular expression it returns or asserts on the exception it throws. The meaning of the
translated constructs is verified against expectations that were recorded from a real JavaScript engine, the one that
cspell runs its patterns in, and not derived from the .NET behavior: each row of the meaning theory holds an entry, a
text, and the matches that JavaScript finds in it, so a translation that changes the meaning of a construct fails
there. The one construct whose meaning cannot be kept, a back-reference to a group that did not take part in the match,
has a test of its own that states the documented difference. Rejected constructs are a theory whose rows hold an entry
and a text that the reason must contain; each exception must name the entry. Length and nesting limits are tested at the
limit and one past it, with the values built at run time from repeated characters so that the boundary is explicit, and
hostile patterns built from repeated metacharacters must be parsed or rejected with the documented exception only. The
production match limit is five seconds, which a test cannot wait for, so the unit has an internal overload of `Parse`
that takes the limit. One test checks that the default is five seconds and that a limit that is not positive is
rejected, and the time-out behavior is checked on a catastrophic pattern parsed with a limit of fifty milliseconds
through the overload, which keeps the test fast and proves that the regular expression carries the requested limit and
that matching ends with a time-out error instead of hanging.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A pattern between slashes uses only the flags that are written; any other text is a case-insensitive expression whose
  line anchors match at every line; white space around an entry is ignored; slashes may appear in the body; and an entry
  that starts with a slash but does not end in flags is a bare expression.
- The flags g, i, m, s, and u are accepted in any combination, and every other flag is rejected by name.
- The s flag makes the dot match the four JavaScript line terminators, and without it the dot does not match them.
- Every construct of the meaning theory finds exactly the matches recorded from the JavaScript engine, groups are
  numbered in order of their opening parenthesis, and an astral character is one character only with the u flag.
- A back-reference to a group that did not take part in the match does not match.
- Every construct without a safe equivalent is rejected with an exception that names the entry and gives a reason.
- A blank entry and an empty body are rejected, and a missing entry throws a null-argument error.
- A pattern of 4096 characters and groups nested 100 levels deep are accepted, one more of either is rejected, a pattern
  whose translation would be longer than 262,144 characters is rejected in milliseconds with the limit named while
  ordinary patterns (including long ones) are accepted, the shared caches give the same result on many threads, and
  hostile patterns end with a regular expression or the documented exception.
- A braced code point accepts any number of leading zeros and is rejected only above 10FFFF, when empty, or when it holds
  anything but hexadecimal digits, as in Node.js.
- The regular expression has the invariant culture, no .NET case option (the case behavior of the i flag is in the
  translation and follows JavaScript), and a match limit of five seconds by default, and a limit that is not positive
  is rejected.
- With the i flag the letters that JavaScript treats as one (the micro sign and the Greek mu, the sigma forms) match
  each other, the Kelvin sign and the long s match `k` and `s` only with the u flag, and classes, negated classes, word
  shorthands, properties, and boundaries follow the same rules; letters whose case relation differs between .NET
  runtimes (U+A7CE to U+A7D5) are not related, and relations that only simple case folding has (U+0390 with U+1FD3)
  are related with the u flag; a back-reference is rejected when the flag is set.
- Every code unit and every code point that the library treats as the same letter is the same letter in the recorded
  JavaScript groups, and the recorded groups have no pair that the library misses: the table the library embeds is the
  recorded file line for line, on every platform.
- No source of the library that reads or writes patterns uses a casing function of the runtime, and no regular
  expression of the library uses the .NET case option.
- The white space set of the library, its two patterns, and the translation of `\s` are the JavaScript set on every
  code unit, and the trim removes that set and nothing else (the next-line character stays, the byte order mark goes).
- A catastrophic pattern parsed with a limit of fifty milliseconds carries that limit and ends with a time-out error.

#### Test Scenarios

**PatternParser_Parse_SlashForm_UsesOnlyTheWrittenFlags**: A literal without flags matches only the exact case, and the
same literal with the case flag matches both cases.

**PatternParser_Parse_BareForm_IsCaseInsensitiveAndMultiline**: A bare expression with a line start and one with a line
end find their text at a later line and in other letter case.

**PatternParser_Parse_WhiteSpaceAndSlashesInBody_AreHandled**: A literal with white space around it and one with a slash
inside its body find their text.

**PatternParser_Parse_EntryEdges_TrimsOnlyJavaScriptWhiteSpace**: A literal with the byte order mark and the no-break
space around it is read as a literal, and a bare expression with the next-line character at both ends keeps them.

**PatternParser_Parse_LeadingSlashWithoutFlagsEnding_IsBareExpression**: A path-like entry and a lone slash are read as
literal text and find themselves.

**PatternParser_Parse_SupportedFlags_AreAccepted**: Each of the flags g, i, m, s, and u, all five together, and a
repeated flag are accepted.

**PatternParser_Parse_UnsupportedFlag_ThrowsNamingTheFlag**: Each of the flags x, y, d, and v is rejected with an
exception that holds the entry, names the flag in its reason, and shows the entry in its message.

**PatternParser_Parse_Result_HasInvariantCultureCaseOptionAndTimeLimit**: Every entry, case-sensitive or not, gets the
invariant culture option and no .NET case option, because the case behavior is written into the translation; both get
the documented five seconds, and the published limit constant is five thousand milliseconds.

**PatternParser_Parse_CaseFlag_KeepsTheJavaScriptLetterEquivalence**: Each row, recorded from the JavaScript engine
(Node.js 22), gives an entry with the i flag, with and without u, a text, and the matches that JavaScript finds. The
rows cover the Kelvin sign and the long s with and without the u flag, the micro sign and the sigma forms, the sharp s
and its capital, the Turkish dotted and dotless i, classes and their negations, the word shorthands, Unicode
properties, a word boundary, a letter outside the basic plane, the letters U+A7CE to U+A7D5 that a runtime may treat
as cased and JavaScript does not, and the simple case foldings that only the u flag has (U+0390 with U+1FD3, U+03B0
with U+1FE3, U+FB05 with U+FB06) with the pairs that every mode has (U+A7DA with U+A7DB, U+1C89 with U+1C8A, U+019B
with U+A7DC).

**PatternParser_Parse_BareExpression_UsesTheJavaScriptCaseRules**: A bare expression (read with the i flag) matches
the micro sign and the sigma forms as JavaScript does and does not match the Kelvin sign.

**PatternParser_Parse_Construct_KeepsItsJavaScriptMeaning**: Each row, recorded from the JavaScript engine, gives an
entry, a text, and the matches that JavaScript finds. The rows cover ASCII-only word characters, digits, and word
boundaries; the JavaScript white-space set, including the byte order mark and the no-break space and excluding the
next-line character; the dot and the line anchors with the four JavaScript line terminators; the dot with the s flag,
which also matches the line terminators, alone and combined with the other flags, with a greedy repeat, with a line
anchor, with a negated class and an escaped dot that it does not change, with a back-reference, and with a Unicode
property; classes, ranges, escapes
inside classes, and empty classes; numbered, named, and mixed back-references; look-ahead and look-behind; counted,
lazy, and literal braces; hexadecimal, Unicode, code point, control, and null escapes; Unicode general categories with
the u flag; case-insensitive matching of non-ASCII letters; and astral characters with and without the u flag.

**PatternParser_Parse_BackReferenceToUnmatchedGroup_DoesNotMatch**: A pattern whose second alternative refers to a group
of the first finds nothing in `b`, where JavaScript matches the letter; this is the documented difference.

**PatternParser_Parse_NamedAndPlainGroups_AreNumberedInOrder**: A named group followed by a plain group, referred to by
number, matches the text that repeats the plain group and then the named group, and not the other text.

**PatternParser_Parse_AstralLiteralWithQuantifier_FollowsTheUnicodeFlag**: A doubled astral character with a quantifier
matches both characters with the u flag and nothing without it, where the quantifier repeats only the last code unit.

**PatternParser_Parse_UnsupportedConstruct_ThrowsWithReason**: Each row, an entry and the wording of its reason, is
rejected with an exception that holds the entry, whose reason contains the wording, and whose message shows the entry.
The rows cover inline options, atomic groups, conditionals, quoted and comment groups, a repeated group name, a bad
group name, an unclosed name, escapes that are plain letters in JavaScript, references to groups that do not exist or
are not complete, a named reference without a named group, octal escapes, misplaced and repeated quantifiers, repeated
assertions, counts that are out of order or too large, unclosed groups and classes, a stray closing parenthesis, ranges
that are out of order, a trailing backslash, malformed escapes, a code point out of range, an astral character in a
class without the u flag, Unicode properties without the u flag or that are not general categories, characters that
must be escaped with the u flag, a class escape as the end of a range, escapes that are not allowed inside a class, and
back-references when the i flag is set (numbered, named, with the u flag, and in a bare expression).

**PatternParser_Parse_BlankOrEmptyBody_Throws**: An empty entry, an entry of white space, and two slashes with and
without flags are rejected with a reason that says the entry is blank or empty.

**PatternParser_Parse_NullEntry_ThrowsArgumentNullException**: A null entry throws a null-argument error.

**PatternParser_Parse_LengthLimit_IsEnforced**: A body of 4096 characters is accepted, a body one character longer is
rejected with the limit named in the reason, and the published limit constant is 4096.

**PatternParser_Parse_NestingLimit_IsEnforced**: Groups nested 100 levels deep are accepted, and one level more is
rejected with a reason that mentions nesting.

**PatternParser_Parse_CaseInsensitiveExpansionOverTheLimit_IsRejectedQuicklyWithReason**: A pattern of the longest
allowed length that is made of one piece, repeated, with the `i` and `u` flags is refused, in under two seconds (it
takes milliseconds), with the limit of 262,144 characters named in the reason. The pieces are `\p{L}`, `\P{L}`,
`\p{Lo}`, a class of one property, and a negated class of one property; each would build a translation of several
megabytes, and the published limit constant is 262,144.

**PatternParser_Parse_TranslationWithinTheLimit_IsAccepted**: The longest ordinary pattern (4,096 letters with the `i`
flag), fifty case-insensitive properties, as many properties as fit in the source without the `i` flag, 2,048
case-insensitive shorthands, and fifty classes that collect properties whose union is nearly every character, are all
accepted, so ordinary patterns are not affected by the limit.

**PatternParser_Parse_ConcurrentTranslations_GiveTheSameResult**: Two patterns that use the cached sets of a property, a
negated property, and shorthands, in both modes, are translated by two hundred parallel calls, and every result equals
the sequential one, so the shared caches do not change a result.

**PatternParser_Parse_BracedCodePointWithLeadingZeros_IsAccepted**: Each row, recorded from Node.js v22.18.0, gives a
braced code point written with and without leading zeros (up to twenty digits), and with the largest value, 10FFFF; it
matches the character it names and translates to the same pattern as the form without the zeros.

**PatternParser_Parse_BracedCodePointOutOfRange_IsRejected**: A braced value above 10FFFF (with and without leading
zeros, and one too large for an integer), an empty one, and ones with a space or a sign are rejected, with a reason
that names the range of a code point, as Node.js does.

**PatternParser_Parse_HostilePatterns_AreHandledWithoutErrorOtherThanInvalidPattern**: Bodies of 4096 repeated
characters of parentheses, closing parentheses, brackets, backslashes, stars, and braces, and of repeated named-group
openings, are parsed or rejected with the documented exception, and nothing else escapes.

**PatternParser_Parse_CatastrophicPattern_TimesOutWhenMatching**: A nested quantifier that backtracks without end, parsed
with a limit of fifty milliseconds through the internal overload, carries that limit and throws the time-out error of
the regular expression engine on a text that almost matches, instead of hanging.

**PatternParser_Parse_MatchTimeout_DefaultsToFiveSecondsAndMustBePositive**: A pattern parsed without a limit has the
production limit of five seconds, and a limit of zero, a negative limit, and an infinite limit are each rejected with an
out-of-range error.

The helpers have their own test files, `JavaScriptCaseFoldingTests.cs` and `JavaScriptSyntaxTests.cs`. The reference
for case folding is `Data/javascript-case-groups.txt`, the letter groups recorded from Node.js 22 (Unicode 16) by
`generate-javascript-case-data.mjs` (beside the table, in `src/.../TextScanning/Data`), which also writes the table
`JavaScriptCaseData.cs` that the library embeds; both are checked in and the script is not run by the build. The case
tests do not use a casing function of the runtime, so they give the same result on every platform; they were run on
Windows and on Linux.

**JavaScriptCaseFolding_Equivalents_EveryCodeUnit_ReproducesTheRecordedGroupsExactly**: For each mode (`i` and `iu`),
every code unit is compared with the recorded groups: the library invents no pair and misses none.

**JavaScriptCaseFolding_AstralForms_EveryCodePoint_ReproducesTheRecordedGroupsExactly**: Every code point outside the
basic plane is compared with the recorded groups of the u mode: the forms of each are exactly its group, itself first,
without duplicates, and a character with no group lists only itself.

**JavaScriptCaseData_Groups_TableText_IsTheRecordedFileLineForLine**: The embedded table of each mode is the recorded
file line for line, so the two were generated by one recording.

**JavaScriptCaseData_Versions_AreStated**: The table names its Node.js and Unicode versions, and the recorded file
header repeats them.

**JavaScriptCaseFolding_Equivalents_KnownDivergentLetters_FollowJavaScript**: The Kelvin sign, the long s, the micro
sign, the sigma forms, the sharp s, the Turkish i, the letters U+A7CE to U+A7D5 on which .NET runtimes differ, and the
simple foldings that only the u mode has, in each mode, are or are not the same letter as JavaScript says.

**JavaScriptCaseFolding_Equivalents_CharacterWithoutCase_ReturnsOnlyItself**: A digit and a character of a script
without case are only equal to themselves in both modes.

**JavaScriptCaseFolding_Close_LowerCaseLetter_AddsTheJavaScriptForms**: Closing the set of `k` adds `K`, and the Kelvin
sign only in the u mode.

**JavaScriptCaseFolding_Close_ContiguousRange_ReturnsAscendingNonOverlappingRanges**: Closing `a` to `z` gives `A` to
`Z` too, in ascending ranges that do not touch.

**JavaScriptCaseFolding_Close_AnySet_EqualsTheCodeUnitByCodeUnitDefinition**: For sets that are unsorted, overlapping,
touching, the whole range, and scattered, in each mode, a code unit is in the closed set exactly when it or a letter
equal to it is a member, tested for all 65,536 code units, and the ranges neither overlap nor touch.

**JavaScriptCaseFolding_Close_EmptySet_ReturnsEmpty**: An empty set stays empty.

**JavaScriptCaseFolding_AstralForms_LetterOutsideBasicPlane_ListsTheOtherCase**: A Deseret letter lists itself first
and its other case, without duplicates.

**JavaScriptCaseFolding_AstralForms_CharacterWithoutCase_ListsOnlyItself**: An emoji lists only itself.

**JavaScriptCaseFolding_EntryPoints_NullArgument_ThrowArgumentNullException**: Each function that takes a reference
argument rejects null.

**JavaScriptCaseFolding_Parse_AnyLineEnding_GivesTheSameTables**: A table written with line feeds, or with carriage
returns and line feeds and a trailing break, gives the same tables.

**JavaScriptCaseFolding_Parse_DamagedGroup_ThrowsInvalidDataException**: A group with one member, one that repeats a
character, one that shares a character with another group, and one that mixes the basic plane with astral planes are
refused with a message that names the group.

**JavaScriptCaseFolding_Parse_MemberNotHexadecimal_ThrowsFormatException**: A member that is not a hexadecimal number
is refused.

**JavaScriptCaseFolding_PatternSources_DoNotUseTheCasingOfTheRuntime**: No code line of the sources that read or write
patterns uses the case option, a casing function, a text-information or compare-information type, the current
culture, or normalization. The test is skipped when the source files are not above the test assembly.

**JavaScriptCaseFolding_Library_NoRegularExpressionUsesTheCaseOption**: No code line of any source file of the library
uses the case option of the regular expression engine or an inline case group.

**JavaScriptSyntax_IsSpace_EveryCodeUnit_AgreesWithJavaScript**: Over all 65,536 code units the function accepts
exactly the recorded JavaScript set.

**JavaScriptSyntax_SpaceAndNotSpace_EveryCodeUnit_ArePartitionedLikeIsSpace**: Over all code units the two patterns
accept the code units of the function and its complement.

**JavaScriptSyntax_Space_TranslatedShorthand_MatchesTheSameCodeUnits**: The translation of `\s` matches exactly the
code units of the function.

**JavaScriptSyntax_IsSpace_DivergentCharacters_FollowJavaScript**: The next-line character, the Mongolian vowel
separator, and the zero-width space are not white space; the byte order mark, the no-break space, the line and
paragraph separators, the narrow no-break space, and the ideographic space are.

**JavaScriptSyntax_Trim_Text_RemovesOnlyJavaScriptWhiteSpace**: The trim removes the byte order mark, the no-break
space, and the line separator at the ends and leaves the next-line character and inner spaces.

**JavaScriptSyntax_Trim_NullText_ThrowsArgumentNullException**: A null text throws a null-argument error.

**JavaScriptSyntax_LineConstants_LineTerminators_FollowJavaScript**: The line start follows the four JavaScript line
terminators and not the next-line character, and the dot is the reverse.

<!-- cspell:ignore metacharacters Deseret dotless foldings -->
