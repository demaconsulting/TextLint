### MarkdownPatterns

#### Verification Approach

`MarkdownPatterns` is verified with unit tests in `MarkdownPatternsTests.cs`. The unit is pure logic over strings and
depends on nothing but the .NET base class library, so no mocks, doubles, or files are used. The patterns are tested
by passing a text to one named pattern of the list and asserting on the text of the matches. The expectations were
recorded from the pattern definitions of the cspell version that the patterns are ported from, run in a real
JavaScript engine, so each row of the theory is a recorded cspell result: a pattern name, a text, and the matches
cspell finds, including the text around a link that must not be matched. The decision of which names are Markdown is a
function of the name alone, so it is tested with plain strings, with no file on disk: one theory lists the names that
apply, covering every extension cspell reads as Markdown, upper case, nested paths, and a directory that looks like a
Markdown file followed by a Markdown file, and another lists the names that do not, including none, an empty name, a
longer extension, and a directory that looks like a Markdown file with a file that is not. The five-second limit cannot
be waited for in a test, so the test checks that every pattern carries it.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Each pattern finds the target of a link, the label of a reference link, the definition of a reference, the id of an
  anchor, and a lower-case entity as cspell does, and finds nothing in the text around them.
- The patterns apply to every extension that cspell reads as Markdown, in any letter case, and to no other name, no
  name at all, or a directory that looks like a Markdown file.
- There are five patterns and every one has a match limit of five seconds.

#### Test Scenarios

**MarkdownPatterns_All_Pattern_FindsWhatCspellFinds**: Each row, recorded from cspell, gives a pattern name, a text, and
the matches cspell finds. The reference-label pattern finds labels with hyphens, dots, underscores, and spaces, and not
a label with a slash, an empty label, a bracketed word with no label after it, or brackets split over lines. The
reference-definition pattern finds the label and the address, with and without the space after the colon, with an empty
address, and in the middle of a line, and leaves the title out. The link pattern finds the targets of links and images,
up to the first space, in angle brackets, and leaves out a target that starts with a space and parentheses that follow
no link. The anchor pattern finds the id of lower-case anchors, up to a space, and not a name attribute or an upper-case
tag. The entity pattern finds lower-case entities, including an unknown name, and not capitalized names, names with a
digit, unterminated entities, or an empty one.

**MarkdownPatterns_All_EveryPattern_HasDocumentedMatchTimeout**: There are five patterns, the test lists five names, and
every pattern carries the documented five-second limit.

**MarkdownPatterns_AppliesTo_MarkdownExtension_IsTrue**: Each of the extensions `.md`, `.markdown`, `.markdn`,
`.mdown`, `.mdtext`, `.mdtxt`, `.mdwn`, `.mkd`, `.workbook`, and `.mdx`, upper-case and mixed-case names, a Windows path,
a nested path, a directory named like a Markdown file that holds a Markdown file, and a name with several dots apply.

**MarkdownPatterns_AppliesTo_OtherNames_IsFalse**: No name, an empty name, a text file, an extension followed by
another extension, the bare word `md`, a leading dot with a backup extension, a name without an extension, a directory
that looks like a Markdown file holding a file without an extension, with either separator, and a similar but different
extension do not apply.

**MarkdownPatterns_All_JavaScriptWhiteSpace_EndsAndJoinsLikeJavaScript**: Each row, recorded from cspell's JavaScript
expressions, puts the byte order mark, the no-break space, the line separator, or the next-line character where a link,
a footer, or an anchor reads white space; the first three end the match as JavaScript white space does, and the
next-line character does not.

<!-- cspell:ignore mdtext mdtxt mdwn -->
