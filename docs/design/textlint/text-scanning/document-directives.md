### DocumentDirectives

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`DocumentDirectives` interprets the directives that comments in a text use to control spell checking. Users silence
deliberate exceptions with directives such as the disable directive, written next to the text that needs them, so a
project
migrated from cspell must find them working. The unit finds where checking is switched off, which words the document
adds, and which directives are recognized but not supported, so that a user does not believe a directive is in force
when it is not.

#### Data Model

`DocumentDirectives` is an `internal static partial` class with no state beyond the generated regular expressions and
the two prefix words of the search guard. Its result is the immutable `DirectiveScan` (an `internal sealed class`):

- `ExcludedRanges`: the ranges in which checking is switched off (disabled blocks, disabled lines, the next line of a
  next-line directive, and the text of the directives that add words), in the order the patterns found them, unsorted
  and possibly overlapping.
- `IgnoredWords`: the words of `ignore` directives, in order.
- `Words`: the words of `words` directives, in order.
- `UnsupportedDirectives`: the ranges of the names of recognized but unsupported directives, in text order.
- `Empty`: the shared scan of a text with no directives, so that the common case allocates nothing. Offsets are UTF-16
  offsets into the scanned text.

A directive is a **prefix**, one of `cspell`, `spell`, `spellchecker`, `spell-checker`, or `textlint` in any letter
case, a colon (or two), optional white space, and a name. The prefix must not be preceded by a letter, digit, or
underscore, so a prefix glued to the end of another word is not a directive. The directives are:

- **disable** hides the text from the directive to the next **enable** written with the same prefix spelling (letter
  case aside), or to the end of the text. Text before the directive on its line and after the `enable` keyword on its
  line stays checked.
- **disable-line** hides the whole line, including text before the directive.
- **disable-next-line** (also **disable-next**) hides the rest of its own line and the next line.
- **ignore** (also `ignoreWord`, `ignoreWords`, `ignore-word`, `ignore-words`) and **words** (also `word`) add the
  listed words, separated by commas, semicolons, or white space, for the whole document. The text of the directive
  is not itself checked.
- Directives that cspell supports and this library does not are recognized and reported: `flag` and `forbid` (and
  their word forms), `locale` (also written `local`) and `language`, `dictionary` and `dictionaries`, `ignoreRegExp`,
  `includeRegExp`, and the compound-word and case-sensitivity switches.

Text that merely resembles a directive (`disabled`, `disable-lines`, a prefix inside another word) and unknown names are
ignored. A directive name must match in full, so a name that only starts like one (`locals`, `locales`) is unknown.

#### Key Methods

**Scan**: Scans a text for directives.

- _Parameters_: the text.
- _Algorithm_: (1) rule out a text that cannot hold a directive with a cheap search for the words `spell` and
  `textlint`; (2) collect the disabled regions of the three disable forms with their patterns; (3) add the text of the
  `ignore` and `words` directives to the excluded ranges; (4) read every directive line with one pattern, classify its
  name with another, and either split the listed words into the ignored or document word list, or, for an
  unsupported directive, record the range of its name unless that position lies inside a disabled region; (5) return
  `Empty` when nothing was found.
- _Postconditions_: the whole text is scanned, including disabled regions, because a words directive adds its words to
  the document wherever it is, while an unsupported directive inside a disabled region is not reported, because text
  there is not read.

The patterns, and why each is shaped as it is:

- **Disable block**: `disable` then lazily anything up to the same prefix followed by `enable`, or the end of the text.
  The prefix is captured and repeated with a case-insensitive backreference, which makes pairing by prefix part of the
  pattern; a block opened with the `spell-checker` prefix is therefore not closed by an enable directive with the
  `spell` prefix, as in cspell.
- **Disable next line**: the directive, the rest of its line, one or two line-break characters, and the next line.
  Allowing a second line-break character lets CRLF count as one break. Because the second character is optional and
  greedy, a blank next line behaves differently for the two styles: with CRLF only the blank line is covered, and
  with LF the line after the blank line is covered too. This is exactly what cspell does (verified against cspell,
  not assumed), and the library keeps it so that migrated documents hide the same text. A directive on the last line,
  with no line break after it, matches nothing.
- **Disable line**: from the start of the line through the directive to the end of the line, so the text before the
  directive is hidden too.
- **Ignore directive**: the prefix and everything to the end of the line, to exclude the listed words from checking.
- **Directive line** and **directive kind**: the first finds every line that holds a prefix and a colon; the second
  classifies the name at the start of what follows, requiring that a name not be continued by a letter, digit,
  underscore, or hyphen, so `ignored` and `words-x` are not directives.

Design choices and their reasons:

- **Patterns ported from cspell, as regular expressions.** The directive grammar is cspell's, and users depend on its
  edge cases, so the patterns are ports that keep the cspell shape: the same JavaScript differences as in
  `DefaultPatterns` apply (ASCII-only word characters, the four line terminators, case-insensitive with the invariant
  culture) and are handled the same way. Hand-written scanning would be faster but would make it hard to prove that
  the edge cases agree.
- **The guard search is a pure optimization.** Most files hold no directive, and running six patterns over every file
  would be wasted work. Every prefix contains `spell` or `textlint`, so a text that holds neither cannot contain a
  directive, and a vectorized, case-insensitive substring search rules that out at almost no cost. The guard can
  only skip work; it never changes a result, which is why it needs no requirement of its own.
- **Unsupported directives are reported, not ignored.** An author who writes a locale directive believes it is in force.
  A silent no-op would hide a difference from cspell, so it is reported at its name, where the author can find it.
- **Shared match time limit.** The patterns use the time limit of `DefaultPatterns`, so one setting bounds every
  regular expression of the subsystem.
- **Lists returned as immutable arrays.** The result is shared freely between threads and files; the lists are built
  locally during the scan, so nothing needs a lock.

#### Error Handling

A null text is a programming error and throws `ArgumentNullException`. Hostile text that makes a pattern exceed its
time limit throws `RegexMatchTimeoutException`; the caller treats the text as not checked. Nothing else can fail:
unknown names, empty directives, and directives with no words are ignored or add no words rather than being errors.
The class is stateless and thread-safe; `DirectiveScan` is immutable, and the generated regular expressions are safe
for concurrent matching.

#### Dependencies

- **DefaultPatterns** (same subsystem) - the constant `MatchTimeoutMilliseconds`, so that the directive patterns share
  the time limit of the default patterns.
- **.NET base class library** - `System.Text.RegularExpressions` (including the source generator) and
  `System.Collections.Immutable`. No other unit, subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - scans each text once, merges the excluded ranges with those of the
  default patterns, applies the document words, and reports the unsupported directives as findings.
