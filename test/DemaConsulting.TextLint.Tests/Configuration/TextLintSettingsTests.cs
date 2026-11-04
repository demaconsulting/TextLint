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
}
