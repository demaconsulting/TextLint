### MarkdownPatterns

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`MarkdownPatterns` finds the parts of a Markdown document that are not prose: the targets of links, the labels of
reference links, the definitions of references, the ids of anchors, and HTML entities. These are names and addresses,
and their fragments would be reported as unknown words in every document. cspell excludes them for Markdown files with
patterns of its Markdown dictionary package and its default settings, and the unit ports those patterns so that a
migrated project stays quiet where it was quiet. It also decides, from the name of a source, whether a text is
Markdown.

#### Data Model

<!-- cspell:ignore mdtext mdtxt mdwn Zorb -->

`MarkdownPatterns` is an `internal static partial` class. Its state is fixed for the life of the process:

- **Extensions**: the ten file name extensions that cspell reads as Markdown, each with its leading dot, compared
  ignoring letter case: `.markdn`, `.markdown`, `.md`, `.mdown`, `.mdtext`, `.mdtxt`, `.mdwn`, `.mkd`, `.workbook`,
  and `.mdx`.
- **All**: the five generated regular expressions, in the order cspell lists them, exposed read-only so that the time
  limit that bounds every one of them can be verified in one place and so that the checker runs them with the code that
  runs the other excluded-text patterns (see _PatternCatalog_). Every one is built case-sensitive with the invariant
  culture and has the match time limit of `PatternParser` (see _PatternParser_). A match is reported by the caller as a
  `TextRange` (see _WordTokenizer_). The five patterns are:
  - **Link reference**: the label of a reference link, the second pair of brackets of `[text][label]`, brackets
    included. The label may hold letters, digits, space, hyphen, underscore, period, backtick, apostrophe, double
    quote, asterisk, ampersand, semicolon, number sign, and at sign. The pattern needs a closing bracket directly
    before the opening one, so the first bracket pair stays checked.
  - **Link footer**: the definition of a reference, `[label]:`, and the address after it: an optional single space and
    everything up to white space. The label may hold the same characters.
  - **Link**: the target of a link, what follows the `](` of `[text](target)`, up to a closing parenthesis or white
    space. The link text and the parentheses are not matched.
  - **Anchor**: the id of an anchor, the value in `<a id="...">`, up to a double quote or white space.
  - **Symbol entity**: an HTML entity such as `&clubs;`: an ampersand, one or more lower-case ASCII letters, and a
    semicolon.

The case of the symbol entity is the one choice that needs a reason. The Markdown dictionary package of cspell defines
this pattern case-insensitively, but the default settings of cspell redefine it without the flag, and the later
definition is the one in force, so cspell does not exclude `&Zorb;`. The unit follows what cspell does, not what its
package declares, so the entity is case-sensitive.

The patterns follow the translation rules of `DefaultPatterns` (see _DefaultPatterns_): word characters are explicit
ASCII classes, and the case-sensitivity is that of the original. Where a pattern uses `\s` it keeps the .NET meaning,
which differs from the JavaScript set only in U+0085 and U+FEFF.

#### Key Methods

**AppliesTo**: Determines whether the Markdown patterns apply to a source.

- _Parameters_: the name or path of the source, normally the path of the file; may be null.
- _Returns_: `true` when the name ends in one of the extensions, ignoring letter case; `false` for null, for an empty
  name, for any other name, and for a name where a path separator follows the last dot, such as a directory that looks
  like a Markdown file (`notes.md/readme`).
- _Algorithm_: find the last dot, require that no `/` or `\` follows it, and look the rest of the name up in the table
  of extensions. The decision is made from the name only, so it is the same on every platform and needs no file, and
  both kinds of separator are recognized by the unit itself, because a path from another platform may use separators
  that this one does not.
- _Postconditions_: nothing is changed.

**All**: The five generated patterns, in the order cspell lists them: link reference, link footer, link, anchor, and
symbol entity.

Design choices and their reasons:

- **Decided by the name, not the content.** cspell applies these patterns by file type, and the name is all a string
  checker has. A pattern for link targets applied to source code would hide real findings, so a text with no name, or
  with another name, is checked without them.
- **Generated regular expressions, not scanners.** The same reasons as for `DefaultPatterns`: the patterns are cspell's
  own, a port that keeps their shape can be reviewed against the originals, they run once per file rather than once per
  word, and the source generator keeps the library safe for trimming and ahead-of-time compilation.
- **A match time limit on every pattern.** A file can be built to make a pattern backtrack, and a run that never
  finishes is worse than a file reported as not checked; see _PatternParser_ for the limits of such a time limit.
- **Ranges for the caller to merge.** The unit only supplies the expressions; `PatternCatalog.FindExcludedRanges` runs
  them and the caller merges the ranges with the others, in the same way as for the other sources of excluded text.

#### Error Handling

`AppliesTo` never throws, whatever the name, and treats a null name as not Markdown. The expressions are matched by the
caller; hostile text that makes one exceed its time limit throws `RegexMatchTimeoutException` there, and the text must
then be treated as not checked, which `SpellChecker` reports as a timed-out file. The class is stateless and
thread-safe; the generated regular expressions are immutable and safe for concurrent matching.

#### Dependencies

- **PatternParser** (same subsystem) - the constant `MatchTimeoutMilliseconds`, so that the Markdown patterns share the
  time limit of every other pattern.
- **.NET base class library** - `System.Text.RegularExpressions`, including the source generator. No other unit,
  subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - asks `AppliesTo` about the source name of each text and, when it is
  Markdown, runs `All` through `PatternCatalog.FindExcludedRanges` together with the default patterns and the patterns
  of the settings.
