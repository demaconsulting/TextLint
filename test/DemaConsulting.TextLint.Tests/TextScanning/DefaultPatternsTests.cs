// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Text.RegularExpressions;
using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="DefaultPatterns"/> class.
/// </summary>
/// <remarks>
///     The patterns are pure logic over strings and depend on nothing but the .NET base class library, so
///     these tests use the real class with no test doubles. Long values such as keys and digests are built at
///     run time from a repeated character, so the boundaries of their length limits are explicit. Characters
///     outside ASCII are written as escape sequences.
/// </remarks>
public class DefaultPatternsTests
{
    /// <summary>
    ///     The number of default patterns that are documented.
    /// </summary>
    private const int DocumentedPatternCount = 14;

    /// <summary>
    ///     The documented time limit of every pattern, in milliseconds.
    /// </summary>
    private const int DocumentedTimeoutMilliseconds = 5000;

    /// <summary>
    ///     The longest local part of an email address that is matched.
    /// </summary>
    private const int EmailLocalPartLimit = 128;

    /// <summary>
    ///     The longest domain label of an email address that is matched.
    /// </summary>
    private const int EmailLabelLimit = 63;

    /// <summary>
    ///     The shortest Base64 key of an SSH public key that is matched.
    /// </summary>
    private const int SshKeyMinimumLength = 28;

    /// <summary>
    ///     The shortest line of Base64 data that is matched.
    /// </summary>
    private const int Base64LineMinimumLength = 40;

    /// <summary>
    ///     The shortest data after a digest or hash scheme name that is matched.
    /// </summary>
    private const int DigestMinimumLength = 25;

    /// <summary>
    ///     The size of the very large inputs, in characters (one megabyte of text, counted as 1,048,576 characters).
    /// </summary>
    private const int OneMegabyte = 1024 * 1024;

    /// <summary>
    ///     A line of Base64 data with a digit, mixed case, and runs of upper-case letters, which is long enough
    ///     to be recognized as encoded data on its own.
    /// </summary>
    private const string Base64Sample = "QmFzZTY0IGlzIGEgc2FtcGxlIHN0cmluZyB3aXRoIG1peGVkIGNhc2U";

    /// <summary>
    ///     Finds the ranges excluded from checking in a text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The ranges in the order the patterns report them.</returns>
    private static List<TextRange> Ranges(string text)
    {
        var ranges = new List<TextRange>();
        DefaultPatterns.FindExcludedRanges(text, ranges);
        return ranges;
    }

    /// <summary>
    ///     Finds the text of the ranges excluded from checking in a text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The text of each range in the order the patterns report them.</returns>
    private static string[] Matched(string text) =>
        [.. Ranges(text).Select(range => text.Substring(range.Start, range.Length))];

    /// <summary>
    ///     Determines whether some range excluded from checking is exactly the given text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="expected">The text that one range must equal.</param>
    /// <returns><see langword="true"/> when a range covers exactly <paramref name="expected"/>.</returns>
    private static bool IsMatched(string text, string expected) => Matched(text).Contains(expected);

    /// <summary>
    ///     Determines whether any range of a text is excluded from checking.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns><see langword="true"/> when at least one range is excluded.</returns>
    private static bool HasMatch(string text) => Ranges(text).Count > 0;

    /// <summary>
    ///     Builds a string of one repeated character.
    /// </summary>
    /// <param name="character">The character to repeat.</param>
    /// <param name="count">The number of times to repeat it.</param>
    /// <returns>The repeated character.</returns>
    private static string Repeat(char character, int count) => new(character, count);

    /// <summary>
    ///     Test that a URL with a scheme is matched whole.
    /// </summary>
    /// <param name="text">A text that contains a URL.</param>
    /// <param name="expected">The URL that must be matched.</param>
    [Theory]
    [InlineData("see https://example.com/path?q=alpha now", "https://example.com/path?q=alpha")]
    [InlineData("http://localhost:8080/api/items", "http://localhost:8080/api/items")]
    [InlineData("get ftp://files.example.com/pub/readme", "ftp://files.example.com/pub/readme")]
    [InlineData("HTTP://EXAMPLE.COM/Index", "HTTP://EXAMPLE.COM/Index")]
    public void DefaultPatterns_FindExcludedRanges_UrlWithScheme_IsMatched(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the whole URL is one of the matches
        Assert.Contains(expected, matched);
    }

    /// <summary>
    ///     Test that a URL ends at white space or a double quote.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_UrlInQuotes_EndsBeforeQuote()
    {
        // Arrange: a URL inside double quotes and followed by more text
        const string text = "visit \"https://example.com/page\" today";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the URL excludes the quotes and the words around it
        Assert.Equal(["https://example.com/page"], matched);
    }

    /// <summary>
    ///     Test that each URL in a text is matched separately.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_SeveralUrls_AreEachMatched()
    {
        // Arrange: two URLs separated by a space
        const string text = "https://example.com/a https://example.com/b";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: each URL is matched on its own
        Assert.Equal(["https://example.com/a", "https://example.com/b"], matched);
    }

    /// <summary>
    ///     Test that a host with no scheme and a scheme that is not listed are not matched as URLs.
    /// </summary>
    /// <param name="text">A text that looks like an address but has no web or file-transfer scheme.</param>
    [Theory]
    [InlineData("www.example.com")]
    [InlineData("example.com/path")]
    [InlineData("git://example.com/repo")]
    [InlineData("mailto:alice")]
    public void DefaultPatterns_FindExcludedRanges_HostWithoutScheme_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that an email address is matched whole, with or without angle brackets.
    /// </summary>
    /// <param name="text">A text that contains an email address.</param>
    /// <param name="expected">The address that must be matched.</param>
    [Theory]
    [InlineData("write to alice@example.com today", "alice@example.com")]
    [InlineData("mail <alice@example.com> now", "<alice@example.com>")]
    [InlineData("first.last+tag@mail.example.co.uk", "first.last+tag@mail.example.co.uk")]
    [InlineData("under_score-name@example.org", "under_score-name@example.org")]
    public void DefaultPatterns_FindExcludedRanges_EmailAddress_IsMatched(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: the whole address is one of the matches
        Assert.Contains(expected, Matched(text));
    }

    /// <summary>
    ///     Test that text that only resembles an email address is not matched.
    /// </summary>
    /// <param name="text">A text with an at sign but no complete address.</param>
    [Theory]
    [InlineData("user@localhost")]
    [InlineData("@handle")]
    [InlineData("alice@")]
    [InlineData("alice @example.com")]
    public void DefaultPatterns_FindExcludedRanges_IncompleteEmailAddress_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: nothing is excluded
        Assert.Empty(Ranges(text));
    }

    /// <summary>
    ///     Test that an email address with the longest local part is matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmailLocalPartAtLimit_IsMatched()
    {
        // Arrange: an address whose local part has the maximum length
        var address = Repeat('x', EmailLocalPartLimit) + "@example.com";

        // Act: find the excluded ranges
        var matched = Matched(address);

        // Assert: the whole address is matched
        Assert.Equal([address], matched);
    }

    /// <summary>
    ///     Test that an email address whose local part is one character too long is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmailLocalPartOverLimit_IsNotMatched()
    {
        // Arrange: an address whose local part is one character over the maximum
        var address = Repeat('x', EmailLocalPartLimit + 1) + "@example.com";

        // Act: find the excluded ranges
        var ranges = Ranges(address);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that an email address with the longest domain label is matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmailLabelAtLimit_IsMatched()
    {
        // Arrange: an address whose first domain label has the maximum length
        var address = "a@" + Repeat('y', EmailLabelLimit) + ".com";

        // Act: find the excluded ranges
        var matched = Matched(address);

        // Assert: the whole address is matched
        Assert.Equal([address], matched);
    }

    /// <summary>
    ///     Test that an email address whose domain label is one character too long is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmailLabelOverLimit_IsNotMatched()
    {
        // Arrange: an address whose first domain label is one character over the maximum
        var address = "a@" + Repeat('y', EmailLabelLimit + 1) + ".com";

        // Act: find the excluded ranges
        var ranges = Ranges(address);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that a certificate or key block is matched from its BEGIN line to its END line.
    /// </summary>
    /// <param name="kind">The kind of block named on the BEGIN and END lines.</param>
    [Theory]
    [InlineData("CERTIFICATE")]
    [InlineData("PRIVATE KEY")]
    [InlineData("RSA PRIVATE KEY")]
    [InlineData("PUBLIC KEY")]
    [InlineData("RSA PUBLIC KEY")]
    public void DefaultPatterns_FindExcludedRanges_PemBlock_IsMatchedWhole(string kind)
    {
        // Arrange: a block of the given kind between two lines of ordinary text
        var block = $"-----BEGIN {kind}-----\nalpha/beta+gamma=\ndelta epsilon\n-----END {kind}-----";
        var text = "before\n" + block + "\nafter";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the block, and only the block, is matched
        Assert.Equal([block], matched);
    }

    /// <summary>
    ///     Test that a block whose END line names a different kind is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_PemEndOfDifferentKind_IsNotMatched()
    {
        // Arrange: a certificate that ends with the END line of a public key
        const string text = "-----BEGIN CERTIFICATE-----\nalpha beta\n-----END PUBLIC KEY-----";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that the markers of a block are case-sensitive.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_PemLowerCase_IsNotMatched()
    {
        // Arrange: a block written with lower-case markers
        const string text = "-----begin certificate-----\nalpha beta\n-----end certificate-----";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that a block with a character that cannot occur in encoded data is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_PemBodyWithInvalidCharacter_IsNotMatched()
    {
        // Arrange: a certificate whose body contains an exclamation mark
        const string text = "-----BEGIN CERTIFICATE-----\nalpha ! beta\n-----END CERTIFICATE-----";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that an SSH public key is matched from the key type to the end of the key, padding included.
    /// </summary>
    /// <param name="keyType">The key type as written, in any letter case.</param>
    [Theory]
    [InlineData("ssh-rsa")]
    [InlineData("SSH-RSA")]
    public void DefaultPatterns_FindExcludedRanges_SshRsaKey_IsMatchedWithPadding(string keyType)
    {
        // Arrange: a key of the minimum length with padding, followed by a comment
        var key = keyType + " " + Repeat('Q', SshKeyMinimumLength) + "==";
        var text = key + " comment";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the key and its padding are matched and the comment is not
        Assert.Equal([key], matched);
    }

    /// <summary>
    ///     Test that a key shorter than the minimum length is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_SshRsaKeyTooShort_IsNotMatched()
    {
        // Arrange: a key one character shorter than the minimum
        var text = "ssh-rsa " + Repeat('Q', SshKeyMinimumLength - 1);

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that one line of mixed-case Base64 data is matched whole.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64SingleLine_IsMatched()
    {
        // Arrange: a line of encoded data after a label
        const string text = "token: " + Base64Sample + " end";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the encoded data is matched
        Assert.Equal([Base64Sample], matched);
    }

    /// <summary>
    ///     Test that encoded data is matched from the minimum length and not below it.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64SingleLineLength_IsMatchedFromMinimum()
    {
        // Arrange: the encoded sample cut to one character below and exactly at the minimum length
        var tooShort = Base64Sample[..(Base64LineMinimumLength - 1)];
        var longEnough = Base64Sample[..Base64LineMinimumLength];

        // Act: find the excluded ranges of each
        var shortRanges = Ranges(tooShort);
        var longMatched = Matched(longEnough);

        // Assert: only the long enough line is matched
        Assert.Empty(shortRanges);
        Assert.Equal([longEnough], longMatched);
    }

    /// <summary>
    ///     Test that a long run of lower-case letters without a digit is not taken as encoded data.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_LongLowerCaseRunWithoutDigit_IsNotMatched()
    {
        // Arrange: sixty lower-case letters
        var text = Repeat('x', Base64LineMinimumLength + 20);

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that Base64 data over several lines is matched whole, whatever the line separator.
    /// </summary>
    /// <param name="separator">The line separator between the two lines.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void DefaultPatterns_FindExcludedRanges_Base64MultiLine_IsMatchedWhole(string separator)
    {
        // Arrange: two lines of the minimum length joined by the separator
        var text = Repeat('Q', Base64LineMinimumLength) + separator + Repeat('R', Base64LineMinimumLength);

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: both lines are matched together
        Assert.Equal([text], matched);
    }

    /// <summary>
    ///     Test that a shorter final line of Base64 data, with its padding, is part of the match.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64MultiLineWithShortLastLine_IncludesLastLine()
    {
        // Arrange: two long lines followed by a short padded line
        var text = Repeat('Q', Base64LineMinimumLength) + "\r\n" + Repeat('R', Base64LineMinimumLength) + "\r\nSSS==";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: all three lines are matched together
        Assert.Equal([text], matched);
    }

    /// <summary>
    ///     Test that quoted lines of Base64 data are matched with their quotes.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64MultiLineQuoted_IncludesQuotes()
    {
        // Arrange: two quoted lines of the minimum length
        var text = "\"" + Repeat('Q', Base64LineMinimumLength) + "\"\n\"" + Repeat('R', Base64LineMinimumLength) + "\"";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: both quoted lines are matched together
        Assert.Equal([text], matched);
    }

    /// <summary>
    ///     Test that one long line followed by a short line is not Base64 data over several lines.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64LongLineThenShortLine_IsNotMatched()
    {
        // Arrange: one line of the minimum length and a short line of prose
        var text = Repeat('Q', Base64LineMinimumLength) + "\nshort line";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that the next-line character does not start a new line of Base64 data.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_Base64LinesJoinedByNextLineCharacter_IsNotMatched()
    {
        // Arrange: two lines joined by U+0085, which is not a line terminator for the patterns
        var text = Repeat('Q', Base64LineMinimumLength) + "\u0085" + Repeat('R', Base64LineMinimumLength);

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that a run of hexadecimal digits that includes a digit is matched as a commit hash.
    /// </summary>
    /// <param name="text">A text that contains a hash or a hexadecimal number.</param>
    /// <param name="expected">The text that must be matched.</param>
    [Theory]
    [InlineData("commit a1b2c3d fixed", "a1b2c3d")]
    [InlineData("A1B2C3D4", "A1B2C3D4")]
    [InlineData("da39a3ee5e6b4b0d3255bfef95601890afd80709", "da39a3ee5e6b4b0d3255bfef95601890afd80709")]
    [InlineData("1234567", "1234567")]
    [InlineData("0xdeadbeef", "0xdeadbeef")]
    public void DefaultPatterns_FindExcludedRanges_HexadecimalRunWithDigit_IsMatchedAsHash(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: ask whether the whole run is one of the matches
        var isMatched = IsMatched(text, expected);

        // Assert: it is
        Assert.True(isMatched);
    }

    /// <summary>
    ///     Test that words made only of the letters a to f, and runs that are too short, are not hashes.
    /// </summary>
    /// <param name="text">A word or short run that is not a hash.</param>
    [Theory]
    [InlineData("deadbeef")]
    [InlineData("defaced")]
    [InlineData("effaced")]
    [InlineData("facade")]
    [InlineData("a1b2c3")]
    public void DefaultPatterns_FindExcludedRanges_LettersOnlyOrTooShort_IsNotMatchedAsHash(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: no text is matched
        Assert.Empty(Matched(text));
    }

    /// <summary>
    ///     Test that a commit hash in square brackets is matched with its brackets.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_CommitHashLink_IncludesBrackets()
    {
        // Arrange: a bracketed hash after a label
        const string text = "fixed in [a1b2c3d4]";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the brackets are part of a match
        Assert.Contains("[a1b2c3d4]", matched);
    }

    /// <summary>
    ///     Test that a bracketed run that is too short is not a commit hash link.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_CommitHashLinkTooShort_IsNotMatched()
    {
        // Arrange: a bracketed run of six hexadecimal digits
        const string text = "[a1b2c3]";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that a C-style hexadecimal literal is matched whole.
    /// </summary>
    /// <param name="text">A text that contains a hexadecimal literal.</param>
    /// <param name="expected">The literal that must be matched.</param>
    [Theory]
    [InlineData("0xFF", "0xFF")]
    [InlineData("0x1_000", "0x1_000")]
    [InlineData("0xDEADn", "0xDEADn")]
    [InlineData("mask 0x7fffffff done", "0x7fffffff")]
    public void DefaultPatterns_FindExcludedRanges_CStyleHexLiteral_IsMatched(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the whole literal is matched
        Assert.Contains(matched, item => item == expected);
    }

    /// <summary>
    ///     Test that a prefix with no digits, a non-hexadecimal digit, or a word before it is not a literal.
    /// </summary>
    /// <param name="text">A text that resembles a hexadecimal literal but is not one.</param>
    [Theory]
    [InlineData("0x")]
    [InlineData("0xg")]
    [InlineData("10x20")]
    public void DefaultPatterns_FindExcludedRanges_IncompleteHexLiteral_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: ask whether anything is excluded
        var hasMatch = HasMatch(text);

        // Assert: nothing is
        Assert.False(hasMatch);
    }

    /// <summary>
    ///     Test that a CSS color of three to eight hexadecimal digits is matched with its number sign.
    /// </summary>
    /// <param name="text">A text that contains a color.</param>
    /// <param name="expected">The color that must be matched.</param>
    [Theory]
    [InlineData("color: #fff;", "#fff")]
    [InlineData("#FFAA00", "#FFAA00")]
    [InlineData("#abcd", "#abcd")]
    [InlineData("#ff00ff80", "#ff00ff80")]
    public void DefaultPatterns_FindExcludedRanges_CssColor_IsMatchedWithNumberSign(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: some range is exactly the color, number sign included
        Assert.Contains(ranges, range => text.Substring(range.Start, range.Length) == expected);
    }

    /// <summary>
    ///     Test that a number sign followed by too few, too many, or non-hexadecimal characters is not a color.
    /// </summary>
    /// <param name="text">A text that resembles a color but is not one.</param>
    [Theory]
    [InlineData("#ff")]
    [InlineData("#fffffffff")]
    [InlineData("#ghi")]
    [InlineData("# heading")]
    public void DefaultPatterns_FindExcludedRanges_NotACssColor_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded text
        var matched = Matched(text);

        // Assert: nothing is excluded
        Assert.Empty(matched);
    }

    /// <summary>
    ///     Test that a digest with its algorithm name is matched whole, including in an attribute value.
    /// </summary>
    /// <param name="algorithm">The digest algorithm name.</param>
    [Theory]
    [InlineData("sha256")]
    [InlineData("sha384")]
    [InlineData("sha512")]
    public void DefaultPatterns_FindExcludedRanges_ShaDigest_IsMatchedWhole(string algorithm)
    {
        // Arrange: a digest of the minimum data length inside a quoted attribute
        var digest = algorithm + "-" + Repeat('Q', DigestMinimumLength) + "=";
        var text = "integrity=\"" + digest + "\"";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the digest is matched without the quotes
        Assert.Contains(digest, matched);
        Assert.DoesNotContain(matched, item => item.Contains('"'));
    }

    /// <summary>
    ///     Test that a digest whose data is one character too short is not matched.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_ShaDigestTooShort_IsNotMatched()
    {
        // Arrange: a digest with one character less data than the minimum
        var text = "sha512-" + Repeat('Q', DigestMinimumLength - 1);

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that data introduced by a hash scheme name and a separator is matched whole.
    /// </summary>
    /// <param name="prefix">The scheme name and separator that introduce the data.</param>
    [Theory]
    [InlineData("md5:")]
    [InlineData("sha256:")]
    [InlineData("bcrypt$")]
    [InlineData("scrypt-")]
    [InlineData("crypt=")]
    [InlineData("base64,")]
    [InlineData("assertion=")]
    [InlineData("security-token:")]
    [InlineData("#code/")]
    public void DefaultPatterns_FindExcludedRanges_HashString_IsMatchedWhole(string prefix)
    {
        // Arrange: data of the minimum length after the scheme name
        var hash = prefix + Repeat('Q', DigestMinimumLength);
        var text = "value " + hash + " end";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the scheme name and the data are matched together
        Assert.Contains(hash, matched);
    }

    /// <summary>
    ///     Test that data after an unknown scheme name, and data that is too short, is not matched.
    /// </summary>
    /// <param name="prefix">The scheme name and separator before the data.</param>
    /// <param name="dataLength">The number of data characters.</param>
    [Theory]
    [InlineData("foo:", DigestMinimumLength + 5)]
    [InlineData("md5:", DigestMinimumLength - 1)]
    public void DefaultPatterns_FindExcludedRanges_UnknownSchemeOrShortData_IsNotMatched(string prefix, int dataLength)
    {
        // Arrange: the scheme name and data length are passed in by the theory data
        var text = prefix + Repeat('Q', dataLength);

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that hash data that continues in a concatenated quoted string is matched to its end.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_HashStringContinuedInQuotedString_IsMatchedToEnd()
    {
        // Arrange: hash data followed by a quote, a plus, and the rest of the data in a second string
        var text = "md5:" + Repeat('Q', DigestMinimumLength) + "' + 'RRR";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the whole concatenation is matched
        Assert.Equal([text], matched);
    }

    /// <summary>
    ///     Test that a Unicode code point reference, or a range of them, is matched whole.
    /// </summary>
    /// <param name="text">A text that contains a code point reference.</param>
    /// <param name="expected">The reference that must be matched.</param>
    [Theory]
    [InlineData("U+1F600", "U+1F600")]
    [InlineData("see U+2603 here", "U+2603")]
    [InlineData("U+0041-005A", "U+0041-005A")]
    [InlineData("u+00e9", "u+00e9")]
    public void DefaultPatterns_FindExcludedRanges_UnicodeReference_IsMatched(string text, string expected)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: the whole reference is the only match
        Assert.Single(Matched(text), expected);
    }

    /// <summary>
    ///     Test that a plus sign after a letter, with too few digits or a word before it, is not a reference.
    /// </summary>
    /// <param name="text">A text that resembles a code point reference but is not one.</param>
    [Theory]
    [InlineData("U+41")]
    [InlineData("U+")]
    [InlineData("XU+0041")]
    public void DefaultPatterns_FindExcludedRanges_NotAUnicodeReference_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: no range has any length
        Assert.Empty(Ranges(text).Select(range => range.Length));
    }

    /// <summary>
    ///     Test that a UUID, including one with placeholder characters, is matched whole.
    /// </summary>
    /// <param name="uuid">A UUID written in the 8-4-4-4-12 form.</param>
    [Theory]
    [InlineData("123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("123E4567-E89B-12D3-A456-426614174000")]
    [InlineData("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")]
    public void DefaultPatterns_FindExcludedRanges_Uuid_IsMatchedWhole(string uuid)
    {
        // Arrange: the UUID is passed in by the theory data
        var text = "id: " + uuid + ".";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the whole UUID is matched
        Assert.Contains(uuid, matched);
    }

    /// <summary>
    ///     Test that a text with a segment of the wrong length or an invalid character is not matched whole.
    /// </summary>
    /// <param name="text">A text that resembles a UUID but is not one.</param>
    [Theory]
    [InlineData("123e4567-e89b-12d3-a456-42661417400")]
    [InlineData("123e4567-e89b-12d3-a456-4266141740000")]
    [InlineData("123e4567-e89b-12d3-a456-42661417400g")]
    public void DefaultPatterns_FindExcludedRanges_NotAUuid_IsNotMatchedWhole(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: no match covers the whole text
        Assert.DoesNotContain(text, matched);
    }

    /// <summary>
    ///     Test that ordinary prose and identifiers are never matched.
    /// </summary>
    /// <param name="text">A text that is not encoded, hexadecimal, or an address.</param>
    [Theory]
    [InlineData("The quick brown fox jumps over the lazy dog.")]
    [InlineData("Contact the maintainers through the project website.")]
    [InlineData("A decade ago a defaced facade was effaced by the weather.")]
    [InlineData("getHttpResponseCode and snake_case_identifier are names")]
    [InlineData("Visit www.example.com for details")]
    [InlineData("Use the dash-separated option --long-flag, e.g. here")]
    [InlineData("Versions v1.2.3 and 2024-01-15 are dates")]
    [InlineData("Email me at user at example dot com")]
    [InlineData("C:\\Program Files\\App\\bin")]
    [InlineData("The accented caf\u00E9 and na\u00EFve words")]
    public void DefaultPatterns_FindExcludedRanges_OrdinaryText_IsNeverMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: no range covers any text
        Assert.Empty(Ranges(text).Select(range => text.Substring(range.Start, range.Length)));
    }

    /// <summary>
    ///     Test that a hash next to non-ASCII letters is matched, because only ASCII characters are word characters.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_HashNextToNonAsciiLetter_IsMatched()
    {
        // Arrange: a hash with an accented letter on each side
        const string hash = "a1b2c3d";
        var text = "\u00E9" + hash + "\u00E9";

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the hash is matched and the accented letters are not
        Assert.Equal([hash], matched);
    }

    /// <summary>
    ///     Test that an email address next to a non-ASCII letter is matched without that letter.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmailNextToNonAsciiLetter_IsMatched()
    {
        // Arrange: an address directly after an accented letter
        const string address = "alice@example.com";
        var text = "\u00E9" + address;

        // Act: find the excluded ranges
        var matched = Matched(text);

        // Assert: the address is matched and the accented letter is not
        Assert.Equal([address], matched);
    }

    /// <summary>
    ///     Test that a hash next to an ASCII letter, digit, or underscore is not matched.
    /// </summary>
    /// <param name="text">A hash with an ASCII word character beside it.</param>
    [Theory]
    [InlineData("_a1b2c3d")]
    [InlineData("a1b2c3d_")]
    [InlineData("xa1b2c3d")]
    [InlineData("a1b2c3dx")]
    public void DefaultPatterns_FindExcludedRanges_HashNextToAsciiWordCharacter_IsNotMatched(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act / Assert: no range is excluded
        Assert.DoesNotContain(Ranges(text), range => range.Length > 0);
    }

    /// <summary>
    ///     Test that digits of other scripts are not hexadecimal digits.
    /// </summary>
    /// <param name="text">Seven digits written in a script other than ASCII.</param>
    [Theory]
    [InlineData("\uFF11\uFF12\uFF13\uFF14\uFF15\uFF16\uFF17")]
    [InlineData("\u0661\u0662\u0663\u0664\u0665\u0666\u0667")]
    public void DefaultPatterns_FindExcludedRanges_NonAsciiDigits_AreNotHexadecimal(string text)
    {
        // Arrange: the text is passed in by the theory data

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: no range is excluded
        Assert.DoesNotContain(ranges, range => !range.IsEmpty);
    }

    /// <summary>
    ///     Test that every pattern is bounded by the documented time limit.
    /// </summary>
    [Fact]
    public void DefaultPatterns_All_EveryPattern_HasDocumentedMatchTimeout()
    {
        // Arrange: the documented time limit
        var expected = TimeSpan.FromMilliseconds(DocumentedTimeoutMilliseconds);

        // Act / Assert: the constant and every pattern carry the documented limit
        Assert.Equal(DocumentedTimeoutMilliseconds, DefaultPatterns.MatchTimeoutMilliseconds);
        Assert.All(DefaultPatterns.All, pattern => Assert.Equal(expected, pattern.MatchTimeout));
    }

    /// <summary>
    ///     Test that the documented number of patterns is available.
    /// </summary>
    [Fact]
    public void DefaultPatterns_All_Patterns_AreTheDocumentedNumber()
    {
        // Arrange: the patterns

        // Act: count them
        var count = DefaultPatterns.All.Count;

        // Assert: the documented number of patterns is there and none is missing
        Assert.Equal(DocumentedPatternCount, count);
        Assert.All(DefaultPatterns.All, Assert.NotNull);
    }

    /// <summary>
    ///     Test that hostile input that is built to provoke slow matching completes, or fails with a timeout error.
    /// </summary>
    /// <param name="unit">The text that is repeated to build the hostile input.</param>
    /// <param name="repeats">The number of times the unit is repeated.</param>
    [Theory]
    [InlineData("-----BEGIN CERTIFICATE-----", 2000)]
    [InlineData("a@", 50000)]
    [InlineData("a.", 100000)]
    public void DefaultPatterns_FindExcludedRanges_HostileInput_CompletesOrTimesOut(string unit, int repeats)
    {
        // Arrange: a large text built from a repeated fragment that nearly matches several patterns
        var text = string.Concat(Enumerable.Repeat(unit, repeats));

        // Act: find the excluded ranges
        var exception = Record.Exception(() => Ranges(text));

        // Assert: the call either finished or reported the time limit, and never failed in another way
        Assert.True(exception is null || exception is RegexMatchTimeoutException);
    }

    /// <summary>
    ///     Test that a text of one megabyte of prose is searched completely and nothing is excluded.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_OneMegabyteOfProse_ReturnsNoRanges()
    {
        // Arrange: a single line of one megabyte of ordinary prose
        const string sentence = "The quick brown fox jumps over the lazy dog. ";
        var text = string.Concat(Enumerable.Repeat(sentence, OneMegabyte / sentence.Length));

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: nothing is excluded
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that ranges are appended to a list that already holds items.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_ExistingListContent_IsKeptAndAppendedTo()
    {
        // Arrange: a list that already holds a range
        const string url = "https://example.com/a";
        var existing = new TextRange(0, 0);
        var ranges = new List<TextRange> { existing };

        // Act: find the excluded ranges of a text with a URL
        DefaultPatterns.FindExcludedRanges(url, ranges);

        // Assert: the earlier range is still first and the URL follows it
        Assert.Equal([existing, new TextRange(0, url.Length)], ranges);
    }

    /// <summary>
    ///     Test that a text matched by several patterns is reported once for each pattern, without merging.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_TextMatchedByTwoPatterns_ReportsEachMatch()
    {
        // Arrange: a hexadecimal literal that is both a C-style literal and a hash
        const string text = "0xdeadbeef";

        // Act: find the excluded ranges
        var ranges = Ranges(text);

        // Assert: more than one range is reported and each lies inside the text
        Assert.True(ranges.Count > 1);
        Assert.All(ranges, range => Assert.InRange(range.End, 1, text.Length));
    }

    /// <summary>
    ///     Test that an empty text has no excluded ranges.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_EmptyText_ReturnsNoRanges()
    {
        // Arrange: an empty text

        // Act: find the excluded ranges
        var ranges = Ranges(string.Empty);

        // Assert: there are none
        Assert.Empty(ranges);
    }

    /// <summary>
    ///     Test that a missing text or a missing list is rejected.
    /// </summary>
    [Fact]
    public void DefaultPatterns_FindExcludedRanges_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a valid list and text to pair with each null argument

        // Act / Assert: a null text and a null list each throw
        Assert.Throws<ArgumentNullException>(() => DefaultPatterns.FindExcludedRanges(null!, []));
        Assert.Throws<ArgumentNullException>(() => DefaultPatterns.FindExcludedRanges("alpha", null!));
    }
}
