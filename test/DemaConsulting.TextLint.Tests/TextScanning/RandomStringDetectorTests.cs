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

// cspell:ignore zorb quix flib deadbeef fedacb cabdab abcdef abcdefab abcdefa zdeadbeef zorbquix czorb zorbquixflibzorbquixflibzorbquixflibzorbquixflib

using DemaConsulting.TextLint.TextScanning;

namespace DemaConsulting.TextLint.Tests.TextScanning;

/// <summary>
///     Unit tests for the <see cref="RandomStringDetector"/> class.
/// </summary>
/// <remarks>
///     The detector is pure logic over strings and depends on nothing but the .NET base class library, so
///     these tests use the real class with no test doubles. The expected scores and sequences were computed
///     with the cspell source that the detector is ported from, so each case is a recorded cspell result.
///     Characters outside ASCII are written as escape sequences.
/// </remarks>
public class RandomStringDetectorTests
{
    /// <summary>
    ///     The documented shortest hexadecimal sequence that is taken as data.
    /// </summary>
    private const int DocumentedMinimumHexSequence = 8;

    /// <summary>
    ///     The size of the very large inputs, in characters.
    /// </summary>
    private const int VeryLargeToken = 1_000_000;

    /// <summary>
    ///     Finds the hexadecimal sequences of a whole text treated as one token.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text of each sequence, in order.</returns>
    private static string[] HexSequences(string text)
    {
        var sequences = new List<TextRange>();
        RandomStringDetector.FindHexSequences(text, new TextRange(0, text.Length), sequences);
        return [.. sequences.Select(range => text.Substring(range.Start, range.Length))];
    }

    /// <summary>
    ///     Test that a token with many separators, digits, and capitals is judged to be random.
    /// </summary>
    /// <param name="token">A token whose categorized form is at least half its length.</param>
    [Theory]
    [InlineData("zorb-1-quix-2-flib-3-zorb-4-quix-5-flib-6-zorb")]
    [InlineData("Q9x-Zorb_5kLm")]
    [InlineData("aB3-xK9_pQ2.mZ7-vL4_nR8")]
    [InlineData("a1b2")]
    [InlineData("x1y1z1")]
    [InlineData("a.b.c.d")]
    [InlineData("+a+b")]
    public void RandomStringDetector_IsRandom_MixedTokens_AreRandom(string token)
    {
        // Arrange: the token is passed in by the theory data
        // Act: score it
        var isRandom = RandomStringDetector.IsRandom(token);

        // Assert: it looks random
        Assert.True(isRandom);
    }

    /// <summary>
    ///     Test that identifiers, long words, and digit runs are not judged to be random.
    /// </summary>
    /// <param name="token">A token whose categorized form is less than half its length.</param>
    [Theory]
    [InlineData("zorbquixflibzorbquixflibzorbquixflibzorbquixflib")]
    [InlineData("ZorbQuixFlibZorbQuixFlibZorbQuixFlibZorbQuixFlib")]
    [InlineData("1234567890123456789012345678901234567890123")]
    [InlineData("abcd")]
    [InlineData("ABCD")]
    [InlineData("Xyz")]
    [InlineData("zorb12quix")]
    [InlineData("rock'n'roll")]
    [InlineData("-_-")]
    public void RandomStringDetector_IsRandom_IdentifiersAndLongRuns_AreNotRandom(string token)
    {
        // Arrange: the token is passed in by the theory data
        // Act: score it
        var isRandom = RandomStringDetector.IsRandom(token);

        // Assert: it does not look random
        Assert.False(isRandom);
    }

    /// <summary>
    ///     Test that a categorized form of exactly half the length of the token counts as random.
    /// </summary>
    /// <param name="token">A token whose categorized form is exactly half its length.</param>
    [Theory]
    [InlineData("ab")]
    [InlineData("AbCd")]
    [InlineData("ab12")]
    [InlineData("AB12cd")]
    public void RandomStringDetector_IsRandom_ExactlyHalf_IsRandom(string token)
    {
        // Arrange: the token is passed in by the theory data
        // Act: score it and the same token with one more plain letter appended, which lowers the ratio below one half
        var isRandom = RandomStringDetector.IsRandom(token);
        var lowered = RandomStringDetector.IsRandom(token + new string('x', token.Length * 3));

        // Assert: the boundary is inclusive, as in cspell, and a longer token of the same structure is not random
        Assert.Multiple(
            () => Assert.True(isRandom),
            () => Assert.False(lowered));
    }

    /// <summary>
    ///     Test that underscores take no part in the score, while runs of the other separators count once.
    /// </summary>
    [Fact]
    public void RandomStringDetector_IsRandom_UnderscoresAndSeparatorRuns_FollowTheCategorization()
    {
        // Arrange: an underscore between two letters, a run of separators, and underscores alone
        // Act: score them
        var underscoreBetweenLetters = RandomStringDetector.IsRandom("a_b");
        var separatorRun = RandomStringDetector.IsRandom("a..b");
        var underscoresAlone = RandomStringDetector.IsRandom("__");

        // Assert: "a_b" scores 2 of 3, "a..b" scores 3 of 4, and underscores vanish, scoring nothing
        Assert.Multiple(
            () => Assert.True(underscoreBetweenLetters),
            () => Assert.True(separatorRun),
            () => Assert.False(underscoresAlone));
    }

    /// <summary>
    ///     Test that combining marks stay with their letter and that a mark alone counts as a symbol.
    /// </summary>
    [Fact]
    public void RandomStringDetector_IsRandom_CombiningMarks_AreCategorizedWithTheirLetter()
    {
        // Arrange: a decomposed accented word, a word with several marks, and a mark with no letter before it
        const string decomposedWord = "e\u0301cole";
        const string severalMarks = "abc\u0301\u0302d";
        const string markWithoutLetter = "\u0301abc";

        // Act: score them
        var decomposed = RandomStringDetector.IsRandom(decomposedWord);
        var several = RandomStringDetector.IsRandom(severalMarks);
        var lonely = RandomStringDetector.IsRandom(markWithoutLetter);

        // Assert: the first two are one word and not random; the lonely mark adds a symbol and reaches one half
        Assert.Multiple(
            () => Assert.False(decomposed),
            () => Assert.False(several),
            () => Assert.True(lonely));
    }

    /// <summary>
    ///     Test that a letter outside the basic plane is measured in UTF-16 code units, as the token is.
    /// </summary>
    [Fact]
    public void RandomStringDetector_IsRandom_LetterOutsideBasicPlane_CountsTwoCodeUnits()
    {
        // Arrange: a mathematical capital after a word, and before a word
        const string capitalAfter = "zorb\ud835\udc9c";
        const string capitalBefore = "\ud835\udc9czorb";

        // Act: score them
        var after = RandomStringDetector.IsRandom(capitalAfter);
        var before = RandomStringDetector.IsRandom(capitalBefore);

        // Assert: two symbols of six units, and one of six, are both below one half
        Assert.Multiple(
            () => Assert.False(after),
            () => Assert.False(before));
    }

    /// <summary>
    ///     Test that an empty token is not random and that a very large token is scored in one pass.
    /// </summary>
    [Fact]
    public void RandomStringDetector_IsRandom_EmptyAndVeryLargeTokens_AreHandled()
    {
        // Arrange: an empty token and a token of one million alternating digits and capitals
        var large = string.Concat(Enumerable.Repeat("1A", VeryLargeToken / 2));

        // Act: score them
        var empty = RandomStringDetector.IsRandom(string.Empty);
        var veryLarge = RandomStringDetector.IsRandom(large);

        // Assert: the empty token is not random, and the large one is scored without error
        Assert.Multiple(
            () => Assert.False(empty),
            () => Assert.True(veryLarge));
    }

    /// <summary>
    ///     Test that qualifying hexadecimal sequences are found with their offsets.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="expected">The sequences that must be found, separated by a comma.</param>
    [Theory]
    [InlineData("zorbquix-0123456789abcdef", "0123456789abcdef")]
    [InlineData("cabdab-0123-fedacb", "cabdab-0123-fedacb")]
    [InlineData("abcd-abcd", "abcd-abcd")]
    [InlineData("ABCD-ABCD", "ABCD-ABCD")]
    [InlineData("0123456789", "0123456789")]
    [InlineData("deadbeef12", "deadbeef12")]
    [InlineData("x-deadbeef-12-", "deadbeef-12")]
    [InlineData("1234-5678", "1234-5678")]
    [InlineData("AB-CD-EF-12", "AB-CD-EF-12")]
    [InlineData("ab--cd12", "ab--cd12")]
    public void RandomStringDetector_FindHexSequences_QualifyingSequences_AreFound(string token, string expected)
    {
        // Arrange: the token is passed in by the theory data
        // Act: find the sequences
        var found = HexSequences(token);

        // Assert: exactly the expected sequences were found
        Assert.Equal(expected.Split(','), found);
    }

    /// <summary>
    ///     Test that sequences that are too short, mixed in case, or next to a letter are not found.
    /// </summary>
    /// <param name="token">The token.</param>
    [Theory]
    [InlineData("Abcd-abcd")]
    [InlineData("abcdefab")]
    [InlineData("abcdefa")]
    [InlineData("-deadbeef")]
    [InlineData("zdeadbeef12")]
    [InlineData("deadbeef12z")]
    [InlineData("ab-12")]
    [InlineData("ABCDEF12-abcdef12")]
    [InlineData("abcdef12\u00e9")]
    [InlineData("\u00e9abcdef12")]
    [InlineData("abcdef12\ud835\udc9c")]
    [InlineData("abc_def12_ab")]
    public void RandomStringDetector_FindHexSequences_NonQualifyingSequences_AreNotFound(string token)
    {
        // Arrange: the token is passed in by the theory data
        // Act: find the sequences
        var found = HexSequences(token);

        // Assert: nothing qualifies
        Assert.Empty(found);
    }

    /// <summary>
    ///     Test that offsets are relative to the whole text and the list is appended to.
    /// </summary>
    [Fact]
    public void RandomStringDetector_FindHexSequences_TokenInsideText_ReportsTextOffsetsAndKeepsList()
    {
        // Arrange: a token that starts at offset 5 of a longer text, and a list that already holds a range
        const string text = "zorb quix-0123456789abcdef flib";
        var token = new TextRange(5, "quix-0123456789abcdef".Length);
        var sequences = new List<TextRange> { new(0, 1) };

        // Act: find the sequences
        RandomStringDetector.FindHexSequences(text, token, sequences);

        // Assert: the earlier item is kept and the new range is an offset into the text
        Assert.Equal([new TextRange(0, 1), new TextRange(10, "0123456789abcdef".Length)], sequences);
    }

    /// <summary>
    ///     Test that the shortest sequence is the documented one.
    /// </summary>
    [Fact]
    public void RandomStringDetector_FindHexSequences_MinimumLength_IsTheDocumentedOne()
    {
        // Arrange: a sequence one character short of the minimum, and one of exactly the minimum
        var tooShort = new string('1', DocumentedMinimumHexSequence - 1);
        var exact = new string('1', DocumentedMinimumHexSequence);

        // Act: find the sequences
        var shortFound = HexSequences(tooShort);
        var exactFound = HexSequences(exact);

        // Assert: the constant matches the documented value, and only the longer sequence is found
        Assert.Multiple(
            () => Assert.Equal(DocumentedMinimumHexSequence, RandomStringDetector.MinimumHexSequenceLength),
            () => Assert.Empty(shortFound),
            () => Assert.Single(exactFound));
    }

    /// <summary>
    ///     Test that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void RandomStringDetector_FindHexSequences_InvalidArguments_Throw()
    {
        // Arrange: a text and a list
        const string text = "zorb";
        var sequences = new List<TextRange>();

        // Act / Assert: nulls and a token outside the text are rejected
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => RandomStringDetector.FindHexSequences(null!, new TextRange(0, 0), sequences)),
            () => Assert.Throws<ArgumentNullException>(() => RandomStringDetector.FindHexSequences(text, new TextRange(0, 0), null!)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => RandomStringDetector.FindHexSequences(text, new TextRange(2, text.Length), sequences)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => RandomStringDetector.FindHexSequences(text, new TextRange(-1, 1), sequences)));
    }
}
