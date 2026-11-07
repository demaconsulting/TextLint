### DefaultPatterns

#### Verification Approach

`DefaultPatterns` is verified with unit tests in `DefaultPatternsTests.cs`. The unit is pure logic over strings and
depends on nothing but the .NET base class library, so no mocks, doubles, or files are used: each test passes a text to
`FindExcludedRanges` and asserts on the text of the ranges it reports. Every pattern has positive samples, which must be
matched whole, and negative samples, which must produce nothing; the length limits are tested at the limit and one
past it, with the long values built at run time from a repeated character so that the boundary is explicit. Tests with
many samples of one behavior are theories whose rows are the samples; each theory is one scenario below. The
translation rules from JavaScript to .NET have scenarios of their own (hash and email next to non-ASCII letters, digits
of other scripts, and every line terminator). The time limit cannot be waited for in a test, so the test checks that
every pattern carries the documented limit, and hostile input is checked to complete or to fail only with the time-out
error.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each of the fourteen patterns matches its positive samples whole and matches none of its negative samples.
- A `www.` host without a scheme, a hash made only of the letters a to f, and ordinary prose are never matched.
- Length limits are enforced at the limit and one past it.
- Only ASCII characters are word characters and hexadecimal digits; a value next to a non-ASCII letter is found and
  one next to an ASCII word character is not.
- Every pattern has the documented time limit, hostile input does not hang, and a megabyte of prose is searched
  completely.
- Ranges are appended to the list and invalid arguments are rejected.

#### Test Scenarios

**DefaultPatterns_FindExcludedRanges_UrlWithScheme_IsMatched**: URLs with the http, https, and ftp schemes, a port, and
an upper-case scheme are matched whole.

**DefaultPatterns_FindExcludedRanges_UrlInQuotes_EndsBeforeQuote**: A URL inside double quotes ends before the closing
quote.

**DefaultPatterns_FindExcludedRanges_SeveralUrls_AreEachMatched**: Two URLs separated by a space are matched separately.

**DefaultPatterns_FindExcludedRanges_HostWithoutScheme_IsNotMatched**:
A `www.` host, a host with a path, a git URL, and a
mailto address are not matched.

**DefaultPatterns_FindExcludedRanges_EmailAddress_IsMatched**: Plain, bracketed, multi-label, and tagged addresses are
matched whole.

**DefaultPatterns_FindExcludedRanges_IncompleteEmailAddress_IsNotMatched**: An address without a dot in its domain, a
bare handle, an address without a domain, and one with a space before the at sign are not matched.

**DefaultPatterns_FindExcludedRanges_EmailLocalPartAtLimit_IsMatched**: A local part of 128 characters is matched.

**DefaultPatterns_FindExcludedRanges_EmailLocalPartOverLimit_IsNotMatched**: A local part of 129 characters is not
matched.

**DefaultPatterns_FindExcludedRanges_EmailLabelAtLimit_IsMatched**: A domain label of 63 characters is matched.

**DefaultPatterns_FindExcludedRanges_EmailLabelOverLimit_IsNotMatched**: A domain label of 64 characters is not matched.

**DefaultPatterns_FindExcludedRanges_PemBlock_IsMatchedWhole**: Certificate, private key, RSA private key, public key,
and RSA public key blocks are matched from the BEGIN line to the END line, and the text around them is not.

**DefaultPatterns_FindExcludedRanges_PemEndOfDifferentKind_IsNotMatched**: A certificate that ends with the END line of
a public key is not matched.

**DefaultPatterns_FindExcludedRanges_PemLowerCase_IsNotMatched**: A block with lower-case markers is not matched.

**DefaultPatterns_FindExcludedRanges_PemBodyWithInvalidCharacter_IsNotMatched**: A block whose body holds an
exclamation mark is not matched.

**DefaultPatterns_FindExcludedRanges_SshRsaKey_IsMatchedWithPadding**: An SSH key of the minimum length with its padding
is matched, in lower and upper case, and the comment after it is not.

**DefaultPatterns_FindExcludedRanges_SshRsaKeyTooShort_IsNotMatched**: A key one character shorter than the minimum is
not matched.

**DefaultPatterns_FindExcludedRanges_Base64SingleLine_IsMatched**: A line of mixed-case Base64 data after a label is
matched whole.

**DefaultPatterns_FindExcludedRanges_Base64SingleLineLength_IsMatchedFromMinimum**:
The sample cut to one character below
the minimum length is not matched and cut to exactly the minimum is.

**DefaultPatterns_FindExcludedRanges_LongLowerCaseRunWithoutDigit_IsNotMatched**: Sixty lower-case letters are not
matched.

**DefaultPatterns_FindExcludedRanges_Base64MultiLine_IsMatchedWhole**: Two lines of the minimum length are matched
together whether they are separated by LF, CRLF, CR, a line separator, or a paragraph separator.

**DefaultPatterns_FindExcludedRanges_Base64MultiLineWithShortLastLine_IncludesLastLine**: A short padded last line is
part of the match.

**DefaultPatterns_FindExcludedRanges_Base64MultiLineQuoted_IncludesQuotes**: Quoted lines are matched with their quotes.

**DefaultPatterns_FindExcludedRanges_Base64LongLineThenShortLine_IsNotMatched**: A long line followed by a short line of
prose is not matched.

**DefaultPatterns_FindExcludedRanges_Base64LinesJoinedByNextLineCharacter_IsNotMatched**: Two long lines joined by the
next-line character are not matched, because it is not a line terminator for the patterns.

**DefaultPatterns_FindExcludedRanges_HexadecimalRunWithDigit_IsMatchedAsHash**: Runs of seven or more hexadecimal digits
with a decimal digit, upper case, a forty-digit hash, digits only, and a `0x` prefix are matched whole.

**DefaultPatterns_FindExcludedRanges_LettersOnlyOrTooShort_IsNotMatchedAsHash**:
Words made only of the letters a to f and
a run of six digits are not matched.

**DefaultPatterns_FindExcludedRanges_CommitHashLink_IncludesBrackets**: A bracketed hash is matched with its brackets.

**DefaultPatterns_FindExcludedRanges_CommitHashLinkTooShort_IsNotMatched**:
A bracketed run of six digits is not matched.

**DefaultPatterns_FindExcludedRanges_CStyleHexLiteral_IsMatched**:
Literals with upper and lower case digits, underscores,
and a trailing `n`, and a literal inside a sentence, are matched whole.

**DefaultPatterns_FindExcludedRanges_IncompleteHexLiteral_IsNotMatched**: A bare prefix, a prefix with a non-hexadecimal
character, and a prefix inside a word are not matched.

**DefaultPatterns_FindExcludedRanges_CssColor_IsMatchedWithNumberSign**:
Colors of three, four, six, and eight digits are
matched with their number sign.

**DefaultPatterns_FindExcludedRanges_NotACssColor_IsNotMatched**:
Two digits, nine digits, non-hexadecimal characters, and
a heading marker are not matched.

**DefaultPatterns_FindExcludedRanges_ShaDigest_IsMatchedWhole**: Digests of three algorithms with the minimum data and
padding are matched without the quotes of the attribute that holds them.

**DefaultPatterns_FindExcludedRanges_ShaDigestTooShort_IsNotMatched**:
Data one character shorter than the minimum is not
matched.

**DefaultPatterns_FindExcludedRanges_HashString_IsMatchedWhole**: Data of the minimum length after each scheme name and
separator, and after `#code/`, is matched with the scheme.

**DefaultPatterns_FindExcludedRanges_UnknownSchemeOrShortData_IsNotMatched**: Data after an unknown name, and data one
character too short, are not matched.

**DefaultPatterns_FindExcludedRanges_HashStringContinuedInQuotedString_IsMatchedToEnd**:
Data continued in a concatenated
quoted string is matched to its end.

**DefaultPatterns_FindExcludedRanges_UnicodeReference_IsMatched**:
References of four and five digits, a range, and lower
case are matched whole.

**DefaultPatterns_FindExcludedRanges_NotAUnicodeReference_IsNotMatched**:
A reference with two digits, a bare prefix, and a
reference after a word character are not matched.

**DefaultPatterns_FindExcludedRanges_Uuid_IsMatchedWhole**: A UUID in lower case, in upper case, and with placeholder
characters is matched whole.

**DefaultPatterns_FindExcludedRanges_NotAUuid_IsNotMatchedWhole**: A UUID with a short segment, a long segment, or an
invalid character is not matched as a whole.

**DefaultPatterns_FindExcludedRanges_OrdinaryText_IsNeverMatched**:
Prose, identifiers, words made of the letters a to f,
a `www.` host, version numbers, dates, a Windows path, and accented words match nothing.

**DefaultPatterns_FindExcludedRanges_HashNextToNonAsciiLetter_IsMatched**: A hash between accented letters is matched
without them.

**DefaultPatterns_FindExcludedRanges_EmailNextToNonAsciiLetter_IsMatched**: An address directly after an accented letter
is matched without it.

**DefaultPatterns_FindExcludedRanges_HashNextToAsciiWordCharacter_IsNotMatched**:
A hash next to an underscore or an ASCII
letter is not matched.

**DefaultPatterns_FindExcludedRanges_NonAsciiDigits_AreNotHexadecimal**: Seven full-width or Arabic-Indic digits are not
matched.

**DefaultPatterns_All_EveryPattern_HasDocumentedMatchTimeout**: The time limit constant and every pattern carry the
documented five seconds.

**DefaultPatterns_All_Patterns_AreTheDocumentedNumber**: Fourteen patterns are available and none is missing.

**DefaultPatterns_FindExcludedRanges_HostileInput_CompletesOrTimesOut**: Large texts built from repeated certificate
markers, at signs, and dots finish, or fail only with the time-out error.

**DefaultPatterns_FindExcludedRanges_OneMegabyteOfProse_ReturnsNoRanges**: A line of one megabyte of prose is searched
completely and nothing is excluded.

**DefaultPatterns_FindExcludedRanges_ExistingListContent_IsKeptAndAppendedTo**:
A list that already holds a range keeps it
and the URL range follows.

**DefaultPatterns_FindExcludedRanges_TextMatchedByTwoPatterns_ReportsEachMatch**:
A hexadecimal literal that is also a hash
is reported more than once, with every range inside the text.

**DefaultPatterns_FindExcludedRanges_EmptyText_ReturnsNoRanges**: An empty text has no ranges.

**DefaultPatterns_FindExcludedRanges_NullArguments_ThrowArgumentNullException**: A null text and a null list each throw.

**DefaultPatterns_All_JavaScriptWhiteSpace_EndsAndJoinsLikeJavaScript**: Each row, recorded by running cspell's own
JavaScript expression, gives a pattern, a text with the byte order mark, the no-break space, the line separator, or the
next-line character where a pattern reads white space, and the matches; the byte order mark and the others of the
JavaScript set end or join a match, and the next-line character does not.

**DefaultPatterns_All_CaseHandling_IsExplicitAndFollowsJavaScript**: No pattern uses the .NET case option, and the
Kelvin sign is not read as `k` by the patterns that cspell applies case-insensitively, while their ASCII letters match
in both cases.
