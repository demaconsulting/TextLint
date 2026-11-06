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
five-second match limit cannot be waited for in a test, so one test checks that the regular expression carries it, and
the time-out behavior is checked on a catastrophic pattern whose regular expression is rebuilt with a limit of fifty
milliseconds, which keeps the test fast and proves that matching ends with a time-out error instead of hanging.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A pattern between slashes uses only the flags that are written; any other text is a case-insensitive expression whose
  line anchors match at every line; white space around an entry is ignored; slashes may appear in the body; and an entry
  that starts with a slash but does not end in flags is a bare expression.
- The flags g, i, m, and u are accepted in any combination, and every other flag is rejected by name.
- Every construct of the meaning theory finds exactly the matches recorded from the JavaScript engine, groups are
  numbered in order of their opening parenthesis, and an astral character is one character only with the u flag.
- A back-reference to a group that did not take part in the match does not match.
- Every construct without a safe equivalent is rejected with an exception that names the entry and gives a reason.
- A blank entry and an empty body are rejected, and a missing entry throws a null-argument error.
- A pattern of 4096 characters and groups nested 100 levels deep are accepted, one more of either is rejected, and
  hostile patterns end with a regular expression or the documented exception.
- The regular expression has the invariant culture, the case option the entry asks for, and a match limit of five
  seconds, and a catastrophic pattern ends with a time-out error.

#### Test Scenarios

**PatternParser_Parse_SlashForm_UsesOnlyTheWrittenFlags**: A literal without flags matches only the exact case, and the
same literal with the case flag matches both cases.

**PatternParser_Parse_BareForm_IsCaseInsensitiveAndMultiline**: A bare expression with a line start and one with a line
end find their text at a later line and in other letter case.

**PatternParser_Parse_WhiteSpaceAndSlashesInBody_AreHandled**: A literal with white space around it and one with a slash
inside its body find their text.

**PatternParser_Parse_LeadingSlashWithoutFlagsEnding_IsBareExpression**: A path-like entry and a lone slash are read as
literal text and find themselves.

**PatternParser_Parse_SupportedFlags_AreAccepted**: Each of the flags g, i, m, and u, all four together, and a repeated
flag are accepted.

**PatternParser_Parse_UnsupportedFlag_ThrowsNamingTheFlag**: Each of the flags x, y, s, d, and v is rejected with an
exception that holds the entry, names the flag in its reason, and shows the entry in its message.

**PatternParser_Parse_Result_HasInvariantCultureCaseOptionAndTimeLimit**: A case-sensitive entry gets the invariant
culture only, a case-insensitive entry gets the invariant culture and the case option, both get the documented five
seconds, and the published limit constant is five thousand milliseconds.

**PatternParser_Parse_Construct_KeepsItsJavaScriptMeaning**: Each row, recorded from the JavaScript engine, gives an
entry, a text, and the matches that JavaScript finds. The rows cover ASCII-only word characters, digits, and word
boundaries; the JavaScript white-space set, including the byte order mark and the no-break space and excluding the
next-line character; the dot and the line anchors with the four JavaScript line terminators; classes, ranges, escapes
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
must be escaped with the u flag, a class escape as the end of a range, and escapes that are not allowed inside a class.

**PatternParser_Parse_BlankOrEmptyBody_Throws**: An empty entry, an entry of white space, and two slashes with and
without flags are rejected with a reason that says the entry is blank or empty.

**PatternParser_Parse_NullEntry_ThrowsArgumentNullException**: A null entry throws a null-argument error.

**PatternParser_Parse_LengthLimit_IsEnforced**: A body of 4096 characters is accepted, a body one character longer is
rejected with the limit named in the reason, and the published limit constant is 4096.

**PatternParser_Parse_NestingLimit_IsEnforced**: Groups nested 100 levels deep are accepted, and one level more is
rejected with a reason that mentions nesting.

**PatternParser_Parse_HostilePatterns_AreHandledWithoutErrorOtherThanInvalidPattern**: Bodies of 4096 repeated
characters of parentheses, closing parentheses, brackets, backslashes, stars, and braces, and of repeated named-group
openings, are parsed or rejected with the documented exception, and nothing else escapes.

**PatternParser_Parse_CatastrophicPattern_TimesOutWhenMatching**: A nested quantifier that backtracks without end, built
again with a limit of fifty milliseconds, throws the time-out error of the regular expression engine on a text that
almost matches, instead of hanging.

<!-- cspell:ignore metacharacters -->
