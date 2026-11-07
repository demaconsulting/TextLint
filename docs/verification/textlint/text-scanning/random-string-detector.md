### RandomStringDetector

#### Verification Approach

`RandomStringDetector` is verified with unit tests in `RandomStringDetectorTests.cs`. The unit is pure logic over
strings and depends on nothing but the .NET base class library, so no mocks, doubles, or files are used: each test
passes a token to `IsRandom` and asserts on the verdict, or passes a text and the range of a token to
`FindHexSequences` and asserts on the text of the ranges it adds. The scores and the sequences were computed with the
cspell source that the detector is ported from, so each row of a theory is a recorded cspell result, and the theories
are split by the verdict they record. A token of exactly half the length of its categorized form is a boundary of its
own, tested together with the same token lengthened so that it falls below one half. Characters outside ASCII are
written as escape sequences in the tests, so that combining marks and characters outside the basic plane are exercised
without depending on the encoding of the source file. The very large token is built at run time from a repeated pair of
characters, and the shortest hexadecimal sequence is tested at one below the documented length and at the length itself.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A token of mixed digits, capitals, separators, and short words is judged random, and identifiers, long words, and long
  digit runs are not.
- A categorized form of exactly half the length of the token counts as random.
- Underscores take no part in the score, a run of separators counts once, a combining mark stays with its letter, and a
  letter outside the basic plane is measured in two code units.
- An empty token is not random and a token of one million characters is judged without error.
- Hexadecimal sequences of at least eight characters that are delimited, written in one letter case, contain a digit or
  a hyphen, and do not touch a letter are found with offsets relative to the whole text, and all others are not.
- A list is appended to, never cleared, and invalid arguments are rejected.

#### Test Scenarios

**RandomStringDetector_IsRandom_MixedTokens_AreRandom**: Tokens made of words, digits, and separators, mixed
capitals and underscores, letters alternating with digits, letters separated by dots, and a plus sign between letters are
judged random.

**RandomStringDetector_IsRandom_IdentifiersAndLongRuns_AreNotRandom**: Long runs of lower-case words, a long camel-case
identifier, a long run of digits, short words, an apostrophe between words, and a token of separators alone are not
judged random.

**RandomStringDetector_IsRandom_ExactlyHalf_IsRandom**: Tokens whose categorized form is exactly half their length are
judged random, and the same tokens with letters appended, which lower the ratio below one half, are not.

**RandomStringDetector_IsRandom_UnderscoresAndSeparatorRuns_FollowTheCategorization**: An underscore between two letters
leaves a score of two of three, a run of two dots scores three of four, and underscores alone score nothing.

**RandomStringDetector_IsRandom_CombiningMarks_AreCategorizedWithTheirLetter**: A decomposed accented word and a word with
several marks are one word and not random, and a mark with no letter before it adds a symbol and reaches one half.

**RandomStringDetector_IsRandom_LetterOutsideBasicPlane_CountsTwoCodeUnits**: A mathematical capital after a word and
before a word is measured as two code units, and both tokens stay below one half.

**RandomStringDetector_IsRandom_EmptyAndVeryLargeTokens_AreHandled**: An empty token is not random, and a token of one
million alternating digits and capitals is judged random without error.

**RandomStringDetector_FindHexSequences_QualifyingSequences_AreFound**: Hexadecimal digits and hyphens after a word, with
several groups, in lower case or upper case, digits only, with a digit after letters, with a trailing hyphen left out,
with a double hyphen, and with capitals in several groups are found whole.

**RandomStringDetector_FindHexSequences_NonQualifyingSequences_AreNotFound**: Sequences of mixed case, of seven or eight
letters with no digit or hyphen, that start with a hyphen, that touch a letter before or after, that are too short, that
mix cases across a hyphen, that touch an accented letter or a letter outside the basic plane, and that are split by
underscores are not found.

**RandomStringDetector_FindHexSequences_TokenInsideText_ReportsTextOffsetsAndKeepsList**: A token that starts partway
into a text yields a range that is an offset into the text, and a range already in the list is kept.

**RandomStringDetector_FindHexSequences_MinimumLength_IsTheDocumentedOne**: The published minimum is eight, a sequence of
seven digits is not found, and a sequence of eight is.

**RandomStringDetector_FindHexSequences_InvalidArguments_Throw**: A null text and a null list throw a null-argument
error, and a token that starts before the text or extends past it throws an out-of-range error.
