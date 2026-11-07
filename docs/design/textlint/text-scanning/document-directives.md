### DocumentDirectives

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`DocumentDirectives` interprets the directives that comments in a text use to control spell checking. Users silence
deliberate exceptions with directives such as the disable directive, written next to the text that needs them, so a
project
migrated from cspell must find them working. The unit finds where checking is switched off, which words and which
patterns the document adds, and which directives are recognized but not supported, so that a user does not believe a
directive is in force when it is not. It only finds the patterns of `ignoreRegExp` directives, reading them the way
cspell does; turning a pattern into a regular expression, and reporting one that cannot be used, is the work of
`TextChecker`.

#### Data Model

<!-- cspell:ignore xcspell cspellchecker ignoreRegExpx dotless -->

`DocumentDirectives` is an `internal static partial` class with no state beyond the generated regular expressions and
the two prefix words of the search guard. Its result is the immutable `DirectiveScan` (an `internal sealed class`):

- `ExcludedRanges`: the ranges in which checking is switched off (disabled blocks, disabled lines, the next line of a
  next-line directive, and the text of the directives that add words), in the order the patterns found them, unsorted
  and possibly overlapping.
- `IgnoredWords`: the words of `ignore` directives, in order.
- `Words`: the words of `words` directives, in order.
- `UnsupportedDirectives`: the ranges of the names of recognized but unsupported directives, in text order. A
  directive that is supported but has nothing after its name on its line (an `ignoreRegExp` directive whose pattern is
  on the next line) is included, because it has no effect; so is a name that only begins like a directive.
- `PatternDirectives`: the patterns of the `ignoreRegExp` directives, in text order, as an array of
  `DirectiveScan.PatternDirective` (an `internal readonly record struct`). Each holds `Range`, the range of the pattern
  as written in the text; `Text`, the pattern as written (a literal between slashes with flags, a bare expression, or
  a name); and `IsReportable`, which is false when the directive lies inside a region where checking is switched off,
  so that an unusable pattern there is not reported. A directive inside a disabled region is still included, because
  its pattern applies to the whole document.
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
- **ignoreRegExp** (also written `ignore_Reg_Exp`, with or without separators and in any letter case) adds one
  pattern, written like an `ignoreRegExpList` entry, for the whole document, wherever the directive is: before it,
  after it, and also when the directive lies inside a disabled block. The text of the directive is not itself checked.
  How the pattern is read from the line is described under _FindPattern_ below. When nothing follows the name on its
  line, the directive has no pattern and is reported like an unsupported directive; the next line is never read.
- Directives that cspell supports and this library does not are recognized and reported: `flag` and `forbid` (and
  their word forms), `locale` (also written `local`) and `language`, `dictionary` and `dictionaries`,
  `includeRegExp`, and the compound-word and case-sensitivity switches.

A name is classified exactly once, and the classification decides both what the directive hides and whether it is
reported, so the two cannot disagree:

- A name is a directive only where cspell's own patterns end it. `disable-line`, `disable-next`, and `enable` end at
  any character other than a letter, digit, or underscore, so a hyphen may follow them: `disable-next-line` is the
  usual form, and `disable-next-foo`, `disable-next-line-foo`, `disable-line-foo`, and `enable-foo` act as the
  keyword, as in cspell, whose range patterns hide text for them. `disable`, `ignore`, and `words` must not be
  followed by a hyphen either.
- A name that begins with a directive keyword but is none of the above (`disabled`, `disable-lines`, `disable-foo`,
  `ignored`, `wordsmith`, `words-x`, `locals`, `ignoreRegExpx`) hides nothing, adds no words or patterns, and is
  reported like an unsupported directive at its name, because the author meant a directive that is not in force.
  cspell hides the rest of the line for the `ignore` and `word` forms; this library deliberately does not.
- A name that does not begin with a directive keyword (`mystery`), a prefix preceded by a word character
  (`xcspell:disable`), and a prefix without a name are ignored.
- Only the first prefix of a line is read for words, for patterns, and for unsupported directives, as in cspell; the
  text of an `ignoreRegExp` directive that follows another prefix on the line is still excluded, but its pattern is not
  read. The range
  directives (disable, enable, disable-line, disable-next) act wherever they occur. The prefixes `cspellchecker` and
  `cspell-checker` are accepted by cspell's range patterns but not by its directive reader, and are treated the same.

#### Key Methods

**Scan**: Scans a text for directives.

- _Parameters_: the text.
- _Algorithm_: (1) rule out a text that cannot hold a directive with a cheap search for the words `spell` and
  `textlint`; (2) find every prefix and classify the name after it with one pattern, which yields the kind of each
  directive; (3) derive the disabled regions from the kinds: a block from each disable to the next enable with the
  same prefix, the covered lines of each next-line directive, and the line of each disable-line directive;
  (4) add the text of the `ignore`, `words`, and `ignoreRegExp` directives to the excluded ranges; (5) read the first
  readable prefix of each line, splitting the listed words into the ignored or document word list, finding the pattern
  of an `ignoreRegExp` directive with _FindPattern_ (recording it with its range and whether it is reportable, or, when
  the line holds nothing after the name, recording the range of the name as an unsupported directive unless that
  position lies inside a disabled region), and, for an unsupported directive or a name that begins like a directive,
  recording the range of its name unless that position lies inside a disabled region; (6) return `Empty` when nothing
  was found.
- _Postconditions_: the whole text is scanned, including disabled regions, because a words directive adds its words to
  the document wherever it is, and an `ignoreRegExp` directive adds its pattern to the whole document wherever it is,
  while an unsupported directive inside a disabled region is not reported, because text there is not read; the pattern
  of an `ignoreRegExp` directive there is returned with `IsReportable` false.

**FindPattern**: Reads the pattern of an `ignoreRegExp` directive from its line, exactly the way cspell does.

- _Parameters_: the text, the offset just past the directive name, and the offset of the end of the line.
- _Algorithm_: (1) trim white space from both ends of the rest of the line after the name; (2) when nothing is left,
  return an empty range; (3) take the longest stretch that starts at the first `/` that has another `/` after it and
  ends at the last `/` of the rest of the line, together with the flag letters from `g`, `i`, `m`, `u`, and `y` that
  directly follow that last slash (so two literals on one line are read as one stretch, and `/x/s` is read as `/x/`,
  because `s` is not one of cspell's flag letters, which is why a pattern that needs the `s` flag can be written in the
  settings but not in a directive); (4) otherwise take the first word, which ends at white space, so an escaped space
  does not help: `a\ b` is read as `a\`. Anything after the pattern is ignored.
- _Postconditions_: the returned range lies inside the line and holds the pattern text as written; it is empty only
  when the line has nothing after the name.

The patterns, and why each is shaped as it is:

- **Prefix**: the prefix with its colon or colons and the white space after it, not preceded by a letter, digit, or
  underscore. It finds every prefix, including one inside another directive's line, because cspell's range patterns act
  on those too. A second, narrower pattern says whether a prefix is one that cspell's directive reader accepts.
- **Name**: anchored where the name starts, it holds one alternative per kind and is the only place that decides what
  a directive is. Its alternatives are tried in a fixed order, and the last one matches a name that only begins with a
  directive keyword.
- **Disable block**: the block runs from a disable directive to the first later enable directive written with the same
  prefix (compared without regard to letter case), or to the end of the text; a disable directive inside a block does
  not start another. A block opened with the `spell-checker` prefix is therefore not closed by an enable directive
  with the `spell` prefix, as in cspell. Unlike cspell, an enable keyword that directly follows a letter is not a
  directive and does not close a block.
- **Disable next line**: matched after the directive's name: the rest of its line, one or two line-break characters,
  and the next line.
  Allowing a second line-break character lets CRLF count as one break. Because the second character is optional and
  greedy, a blank next line behaves differently for the two styles: with CRLF only the blank line is covered, and
  with LF the line after the blank line is covered too. This is exactly what cspell does (verified against cspell,
  not assumed), and the library keeps it so that migrated documents hide the same text. A directive on the last line,
  with no line break after it, matches nothing.
- **Disable line**: from the start of the line through the directive to the end of the line, so the text before the
  directive is hidden too; two such directives on one line hide it once.
- **Ignore, words, and ignoreRegExp text**: from the prefix to the end of the line, to exclude the listed words and
  the pattern from checking.

Design choices and their reasons:

- **Patterns ported from cspell, as regular expressions.** The directive grammar is cspell's, and users depend on its
  edge cases, so the patterns are ports that keep the cspell shape: the same JavaScript differences as in
  `DefaultPatterns` apply (ASCII-only word characters, the four line terminators, the JavaScript white space set) and
  are handled the same way. The keywords are spelled with both cases of every letter (`[Dd][Ii][Ss][Aa][Bb][Ll][Ee]`)
  and the .NET case option is not used: cspell reads them with the JavaScript `i` flag, under which an ASCII letter
  matches only itself and its other case, while the .NET option also lets the long s (U+017F) match `s` and the Kelvin
  sign (U+212A) match `k`. So a prefix with a long s, and a keyword with a dotless i or a dotted capital I, are text,
  as in
  cspell, and nothing is replaced in the
  text. The words of a directive are split, and the
  directive names are trimmed, with `JavaScriptSyntax.IsSpace`, not the .NET white space test. Hand-written scanning
  would be faster but would make it hard to prove that the edge cases agree.
- **The guard search is a pure optimization.** Most files hold no directive, and running six patterns over every file
  would be wasted work. Every prefix contains `spell` or `textlint`, so a text that holds neither cannot contain a
  directive, and a vectorized, case-insensitive substring search rules that out at almost no cost. The comparison that
  ignores case is the ordinal one, which equates an ASCII letter only with its other case, on every platform. The guard
  can only skip work; it never changes a result, which is why it needs no requirement of its own.
- **Unsupported directives are reported, not ignored.** An author who writes a locale directive believes it is in force.
  A silent no-op would hide a difference from cspell, so it is reported at its name, where the author can find it.
  The same holds for a name that only begins like a directive: it neither hides text nor goes unnoticed.
- **The pattern is read the way cspell reads it, and only found here.** cspell applies the pattern to the whole
  document and reads it from the line with its own rule, which has odd edge cases (two literals on a line become one,
  a flag outside `gimuy` is not read); migrated documents must hide the same text, so the rule is ported exactly and
  verified against cspell. The unit does not build the regular expression: that needs the pattern names of the
  settings and the match time limit, which belong to `TextChecker`, so the scan stays free of settings and cheap.
- **A pattern the checker cannot use is reportable at its position.** cspell silently drops an invalid pattern, which
  leaves the author believing it is in force, so the scan returns the range of each pattern and whether it lies in a
  disabled region (where nothing is reported), and the checker reports an unusable pattern where it is written. A
  directive with nothing after its name is reported like an unsupported directive, because it has no effect.
- **One classification drives everything.** With one pattern per effect and a separate one to decide what to report,
  a name such as `disable-next-foo` could hide text while being reported as unknown, or the other way round. The
  effects (ranges, words, reports) are therefore derived from the kind of each classified directive, so they cannot
  disagree.
- **Shared match time limit.** The patterns use the time limit of `DefaultPatterns`, so one setting bounds every
  regular expression of the subsystem.
- **Lists returned as immutable arrays.** The result is shared freely between threads and files; the lists are built
  locally during the scan, so nothing needs a lock.

#### Error Handling

A null text is a programming error and throws `ArgumentNullException`. Hostile text that makes a pattern exceed its
time limit throws `RegexMatchTimeoutException`; the caller treats the text as not checked. Nothing else can fail:
names that do not begin like a directive, empty directives, and directives with no words are ignored or add no words
rather than being errors, and a pattern that is unusable is not an error of this unit, which only finds it.
The class is stateless and thread-safe; `DirectiveScan` is immutable, and the generated regular expressions are safe
for concurrent matching.

#### Dependencies

- **DefaultPatterns** (same subsystem) - the constant `MatchTimeoutMilliseconds`, so that the directive patterns share
  the time limit of the default patterns.
- **.NET base class library** - `System.Text.RegularExpressions` (including the source generator) and
  `System.Collections.Immutable`. No other unit, subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - scans each text once, merges the excluded ranges with those of the
  default patterns, applies the document words, resolves and applies the patterns of the `ignoreRegExp` directives, and
  reports the unsupported directives and the unusable patterns as findings.
