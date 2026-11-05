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

using DemaConsulting.TextLint.Configuration;

namespace DemaConsulting.TextLint.Tests.Configuration;

/// <summary>
///     Unit tests for the <see cref="TextLintSettings"/> class.
/// </summary>
public class TextLintSettingsTests
{
    /// <summary>
    ///     The minimum word length of the original settings in the copy test.
    /// </summary>
    private const int OriginalMinWordLength = 6;

    /// <summary>
    ///     The minimum word length of the copied settings in the copy test.
    /// </summary>
    private const int CopyMinWordLength = 9;

    /// <summary>
    ///     The maximum file size of the original settings in the copy test.
    /// </summary>
    private const long OriginalMaxFileSize = 2048L;

    /// <summary>
    ///     The maximum file size of the copied settings in the copy test.
    /// </summary>
    private const long CopyMaxFileSize = 4096L;

    /// <summary>
    ///     Test that new settings have the documented defaults.
    /// </summary>
    [Fact]
    public void TextLintSettings_New_NoValues_HasDocumentedDefaults()
    {
        // Arrange: no setup required

        // Act: create settings without assigning anything
        var settings = new TextLintSettings();

        // Assert: no version, empty lists (never null), and hidden-file matching disabled
        Assert.Multiple(
            () => Assert.Null(settings.Version),
            () => Assert.Empty(settings.Files),
            () => Assert.Empty(settings.IgnorePaths),
            () => Assert.False(settings.EnableGlobDot));
    }

    /// <summary>
    ///     Test that a copy made with a changed list leaves the original unchanged.
    /// </summary>
    [Fact]
    public void TextLintSettings_With_ChangedFiles_LeavesOriginalUnchanged()
    {
        // Arrange: create settings holding one include pattern
        var original = new TextLintSettings { Files = ["**/*.md"] };

        // Act: derive a copy with different include patterns
        var copy = original with { Files = ["**/*.cs"] };

        // Assert: the original keeps its patterns and the copy has the new ones
        Assert.Multiple(
            () => Assert.Equal(["**/*.md"], original.Files),
            () => Assert.Equal(["**/*.cs"], copy.Files));
    }

    /// <summary>
    ///     Test that changing a list after assigning it does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_ListMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: a mutable list assigned to the settings
        var files = new List<string> { "**/*.md" };
        var settings = new TextLintSettings { Files = files };

        // Act: change the caller's list after assignment
        files.Add("**/*.cs");

        // Assert: the settings still hold only the original pattern
        Assert.Equal(["**/*.md"], settings.Files);
    }

    /// <summary>
    ///     Test that changing the ignore list after assigning it does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_IgnorePathsMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: a mutable list assigned to IgnorePaths
        var ignored = new List<string> { "node_modules" };
        var settings = new TextLintSettings { IgnorePaths = ignored };

        // Act: change the caller's list after assignment
        ignored.Add("bin");

        // Assert: the settings still hold only the original pattern
        Assert.Equal(["node_modules"], settings.IgnorePaths);
    }

    /// <summary>
    ///     Test that changing an assigned array in place does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_ArrayMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: an array assigned to Files (arrays are mutable in place)
        var files = new[] { "**/*.md" };
        var settings = new TextLintSettings { Files = files };

        // Act: overwrite the array element after assignment
        files[0] = "**/*.cs";

        // Assert: the settings still hold the original pattern
        Assert.Equal(["**/*.md"], settings.Files);
    }

    /// <summary>
    ///     Test that assigning a null include list is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullFiles_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to Files throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { Files = null! });
    }

    /// <summary>
    ///     Test that assigning a null ignore list is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullIgnorePaths_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to IgnorePaths throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { IgnorePaths = null! });
    }

    /// <summary>
    ///     Test that the exposed list cannot be changed through its collection interface.
    /// </summary>
    [Fact]
    public void TextLintSettings_Files_ExposedList_CannotBeMutatedThroughInterface()
    {
        // Arrange: settings holding one include pattern
        var settings = new TextLintSettings { Files = ["**/*.md"] };

        // Act: try to add to the exposed list through the mutable collection interface
        var exposed = (IList<string>)settings.Files;

        // Assert: the list is read-only and unchanged
        Assert.Multiple(
            () => Assert.Throws<NotSupportedException>(() => exposed.Add("**/*.cs")),
            () => Assert.Equal(["**/*.md"], settings.Files));
    }

    /// <summary>
    ///     Test that new settings have the documented spelling defaults.
    /// </summary>
    [Fact]
    public void TextLintSettings_New_NoValues_HasSpellingDefaults()
    {
        // Arrange: no setup required

        // Act: create settings without assigning anything
        var settings = new TextLintSettings();

        // Assert: American English, a minimum word length of 4, empty word lists, and no size limit
        Assert.Multiple(
            () => Assert.Equal("en", settings.Language),
            () => Assert.Equal(4, settings.MinWordLength),
            () => Assert.Equal(0L, settings.MaxFileSize),
            () => Assert.Empty(settings.Words),
            () => Assert.Empty(settings.IgnoreWords),
            () => Assert.Empty(settings.FlagWords));
    }

    /// <summary>
    ///     Test that a copy made with changed spelling values leaves the original unchanged.
    /// </summary>
    [Fact]
    public void TextLintSettings_With_ChangedSpellingValues_LeavesOriginalUnchanged()
    {
        // Arrange: settings holding one value of each spelling setting
        var original = new TextLintSettings
        {
            Language = "en-GB",
            Words = ["apple"],
            IgnoreWords = ["banana"],
            FlagWords = ["cherry"],
            MinWordLength = OriginalMinWordLength,
            MaxFileSize = OriginalMaxFileSize,
        };

        // Act: derive a copy that changes every spelling setting
        var copy = original with
        {
            Language = "en-US",
            Words = ["grape"],
            IgnoreWords = ["melon"],
            FlagWords = ["lemon"],
            MinWordLength = CopyMinWordLength,
            MaxFileSize = CopyMaxFileSize,
        };

        // Assert: the original keeps its values and the copy has the new ones
        Assert.Multiple(
            () => Assert.Equal("en-GB", original.Language),
            () => Assert.Equal(["apple"], original.Words),
            () => Assert.Equal(["banana"], original.IgnoreWords),
            () => Assert.Equal(["cherry"], original.FlagWords),
            () => Assert.Equal(OriginalMinWordLength, original.MinWordLength),
            () => Assert.Equal(OriginalMaxFileSize, original.MaxFileSize),
            () => Assert.Equal("en-US", copy.Language),
            () => Assert.Equal(["grape"], copy.Words),
            () => Assert.Equal(["melon"], copy.IgnoreWords),
            () => Assert.Equal(["lemon"], copy.FlagWords),
            () => Assert.Equal(CopyMinWordLength, copy.MinWordLength),
            () => Assert.Equal(CopyMaxFileSize, copy.MaxFileSize));
    }

    /// <summary>
    ///     Test that a language that is not supported is still accepted, because validation is not done here.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_UnsupportedLanguage_IsAcceptedWithoutValidation()
    {
        // Arrange: no setup required

        // Act: assign a language that has no dictionaries
        var settings = new TextLintSettings { Language = "fr" };

        // Assert: the text is stored as given; the spell checker rejects it when it is created
        Assert.Equal("fr", settings.Language);
    }

    /// <summary>
    ///     Test that changing a words list after assigning it does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_WordsMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: a mutable list assigned to Words
        var words = new List<string> { "apple" };
        var settings = new TextLintSettings { Words = words };

        // Act: change the caller's list after assignment
        words.Add("banana");

        // Assert: the settings still hold only the original word
        Assert.Equal(["apple"], settings.Words);
    }

    /// <summary>
    ///     Test that changing an ignored-words list after assigning it does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_IgnoreWordsMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: a mutable list assigned to IgnoreWords
        var words = new List<string> { "apple" };
        var settings = new TextLintSettings { IgnoreWords = words };

        // Act: change the caller's list after assignment
        words.Clear();

        // Assert: the settings still hold the original word
        Assert.Equal(["apple"], settings.IgnoreWords);
    }

    /// <summary>
    ///     Test that changing an array assigned as flagged words in place does not change the settings.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_FlagWordsArrayMutatedAfterAssignment_DoesNotAffectSettings()
    {
        // Arrange: an array assigned to FlagWords (arrays are mutable in place)
        var words = new[] { "apple" };
        var settings = new TextLintSettings { FlagWords = words };

        // Act: overwrite the array element after assignment
        words[0] = "banana";

        // Assert: the settings still hold the original word
        Assert.Equal(["apple"], settings.FlagWords);
    }

    /// <summary>
    ///     Test that assigning a null words list is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullWords_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to Words throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { Words = null! });
    }

    /// <summary>
    ///     Test that assigning a null ignored-words list is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullIgnoreWords_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to IgnoreWords throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { IgnoreWords = null! });
    }

    /// <summary>
    ///     Test that assigning a null flagged-words list is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullFlagWords_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to FlagWords throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { FlagWords = null! });
    }

    /// <summary>
    ///     Test that assigning a null language is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_NullLanguage_ThrowsArgumentNullException()
    {
        // Arrange: no setup required

        // Act / Assert: assigning null to Language throws
        Assert.Throws<ArgumentNullException>(() => new TextLintSettings { Language = null! });
    }

    /// <summary>
    ///     Test that the exposed word lists cannot be changed through their collection interface.
    /// </summary>
    [Fact]
    public void TextLintSettings_WordLists_ExposedLists_CannotBeMutatedThroughInterface()
    {
        // Arrange: settings holding one entry in each word list
        var settings = new TextLintSettings { Words = ["apple"], IgnoreWords = ["banana"], FlagWords = ["cherry"] };

        // Act: cast each exposed list to the mutable collection interface
        var words = (IList<string>)settings.Words;
        var ignoreWords = (IList<string>)settings.IgnoreWords;
        var flagWords = (IList<string>)settings.FlagWords;

        // Assert: adding to each is refused and the lists are unchanged
        Assert.Multiple(
            () => Assert.Throws<NotSupportedException>(() => words.Add("grape")),
            () => Assert.Throws<NotSupportedException>(() => ignoreWords.Add("grape")),
            () => Assert.Throws<NotSupportedException>(() => flagWords.Add("grape")),
            () => Assert.Equal(["apple"], settings.Words),
            () => Assert.Equal(["banana"], settings.IgnoreWords),
            () => Assert.Equal(["cherry"], settings.FlagWords));
    }

    /// <summary>
    ///     Test that a minimum word length of zero is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_MinWordLengthZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: a length below the minimum of 1 throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextLintSettings { MinWordLength = 0 });
    }

    /// <summary>
    ///     Test that a negative minimum word length is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_MinWordLengthNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: a negative length throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextLintSettings { MinWordLength = -1 });
    }

    /// <summary>
    ///     Test that the smallest valid minimum word length is accepted.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_MinWordLengthOne_IsAccepted()
    {
        // Arrange: no setup required

        // Act: assign the boundary length of 1
        var settings = new TextLintSettings { MinWordLength = 1 };

        // Assert: the length is stored
        Assert.Equal(1, settings.MinWordLength);
    }

    /// <summary>
    ///     Test that a negative maximum file size is rejected.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_MaxFileSizeNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: no setup required

        // Act / Assert: a negative size throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextLintSettings { MaxFileSize = -1 });
    }

    /// <summary>
    ///     Test that zero, meaning no limit, and the largest size are accepted as a maximum file size.
    /// </summary>
    [Fact]
    public void TextLintSettings_Init_MaxFileSizeZeroAndLargest_AreAccepted()
    {
        // Arrange: no setup required

        // Act: assign the boundary values
        var unlimited = new TextLintSettings { MaxFileSize = 0 };
        var largest = new TextLintSettings { MaxFileSize = long.MaxValue };

        // Assert: both are stored
        Assert.Multiple(
            () => Assert.Equal(0L, unlimited.MaxFileSize),
            () => Assert.Equal(long.MaxValue, largest.MaxFileSize));
    }
}
