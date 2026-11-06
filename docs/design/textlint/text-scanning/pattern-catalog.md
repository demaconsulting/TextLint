### PatternCatalog

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`PatternCatalog` knows the pattern names that cspell defines in advance and turns the `ignoreRegExpList` and `patterns`
settings, and the patterns that in-document `ignoreRegExp` directives write, into the regular expressions that exclude
text from checking. An `ignoreRegExpList` entry is either a pattern
written out, which `PatternParser` reads, or the name of a pattern, and the catalog is the one place that decides which
it is, which names exist, and what a name stands for; a directive writes its pattern in the same forms and is resolved
by the same code, so a pattern has the same meaning wherever it is written. It also finds the ranges of a text that a
set of regular
expressions match, so that the patterns of the settings and the Markdown patterns are run by the same code. The unit
includes the supporting type `NamedPattern`, the pair of a name and a pattern that the `patterns` setting holds.

#### Data Model

<!-- cspell:ignore anrvtbf -->

`PatternCatalog` is an `internal static partial` class. Its state is fixed for the life of the process:

- **AlwaysAppliedNames**: the nineteen predefined names of patterns that are applied to every text anyway, so that
  listing one changes nothing. Fourteen are the patterns of `DefaultPatterns`: `Urls`, `Email`, `RsaCert`, `SshRsa`,
  `Base64MultiLine`, `Base64SingleLine`, `CommitHash`, `CommitHashLink`, `CStyleHexValue`, `CSSHexValue`, `SHA`,
  `HashStrings`, `UnicodeRef`, and `UUID`. Five belong to the directives that `DocumentDirectives` implements:
  `SpellCheckerDisable`, `SpellCheckerDisableBlock`, `SpellCheckerDisableLine`, `SpellCheckerDisableNext`, and
  `SpellCheckerIgnoreInDocSetting`.
- **Optional patterns**: the nine predefined patterns that exclude text only when listed, as generated regular
  expressions in a table compared ignoring letter case. They are ported from the cspell originals with the translation
  rules of `DefaultPatterns` (ASCII word characters and boundaries, the four line terminators, and the case-sensitivity
  of the original; see _DefaultPatterns_), and each has the match time limit of `PatternParser`:
  - **HexValues** (insensitive): a color or a hexadecimal value in a common form: `#` and three to eight hexadecimal
    digits, `0x` and hexadecimal digits, `\u` and four, or `\x{` and four and `}`.
  - **PublicKey** (sensitive): a public key block, from its BEGIN line (`PUBLIC KEY`, optionally `RSA PUBLIC KEY`) to the
    END line of the same kind.
  - **EscapeCharacters** (insensitive): a backslash and one of `anrvtbf`, or `x` or `u` and hexadecimal digits.
  - **Base64** (sensitive): Base64 data of at least 40 characters, continued over several lines when each line is that
    long, with an optional shorter last line.
  - **href** (insensitive): an HTML `href` attribute with its double-quoted value, on one line.
  - **string** (sensitive): a quoted string literal: single or double quotes on one line, or backticks over several
    lines, with a backslash escaping a quote.
  - **CStyleComment** (sensitive): a line comment to the end of the line or a block comment; a `//` directly after a
    word character and a colon, as in `http://`, is not a comment.
  - **PhpHereDoc** (sensitive): a PHP here-document, from its `<<<` marker to the line that ends with the same name
    and a semicolon.
  - **Everything** (insensitive): every line of text, without its line terminator.

  Where an optional pattern uses `\s` it keeps the .NET meaning, which differs from the JavaScript set only in U+0085 and
  U+FEFF.
- **PredefinedNames**: the set of all twenty-eight names, compared ignoring letter case, exposed so that the tests and
  the documentation can state the complete list in one place. **OptionalRegexes** exposes the nine generated regular
  expressions, so the time limit that bounds every one of them can be verified in one place.
- The constants for the shared text of the translation (the ASCII word boundary, the line start, and the character that
  is not a line terminator), written once, and the two sets of regular expression options (case-insensitive and
  case-sensitive, both with the invariant culture).

`NamedPattern` is a `public sealed record` with `Name` (the name that `ignoreRegExpList` uses; compared ignoring letter
case) and `Pattern` (the pattern, `/body/flags` or a bare expression), both kept exactly as given. Only a null is
rejected by its constructor, with `ArgumentNullException`, because the rules for a valid name and a valid pattern belong
to the catalog and are reported with the setting they came from. It is a public type because the `Patterns` property of
`TextLintSettings` is a list of them. Instances are immutable.

**Name shape.** An entry has the shape of a name when, after white space around it is removed, it is not empty, starts
with an ASCII letter, and holds only ASCII letters, digits, underscores, and hyphens. An entry of that shape is always
read as a name and never as a literal word. Anything else, including every entry with a slash, a dot, or other
punctuation, is read as a pattern.

**What the lists mean.** `ignoreRegExpList` is a list read in order; `patterns` is a list of definitions that apply
only when the list names them. A name in the list stands for the user's definition when there is one, and otherwise
for a predefined pattern. The twenty-eight predefined names are known whatever the settings say, and a definition may
not use one of them, so a name never means two things. Names are compared ignoring letter case, as cspell does.

The difference from cspell that this design makes on purpose: cspell reads an entry that is not a defined name as a
regular expression, so a misspelled name silently matches the misspelled text and nothing else. Here an entry in the
shape of a name that is neither defined nor predefined is an error that advises writing a literal word between slashes,
such as `/text/`, so a setting the user believes is in force can never silently do nothing. The decision applies to
the patterns of in-document directives too: an unknown plain name there is an error that is reported at the pattern
(cspell would read it as a literal expression), stricter than cspell by design. The sentence of the error is built in
one place (`UnknownNameReason`), so the settings file, the settings built in code, and the directives give the same
advice.

#### Key Methods

**IsNameShaped**: Determines whether an entry has the shape of a name.

- _Parameters_: the entry. Not null.
- _Returns_: `true` for the shape above.

**IsPredefined**: Determines whether a name is one of the twenty-eight predefined names, ignoring letter case and
surrounding white space.

**CheckDefinitionName**: Checks that a name may be given to a pattern of the `patterns` setting.

- _Returns_: the reason as a sentence, or null when the name can be used. The reasons are: the name does not start
  with a letter or holds a character other than letters, digits, `_` and `-` (or has white space around it), and the
  name is predefined.
- _Postconditions_: nothing is changed; duplicates are not detected here, because that needs the whole list.

**CheckReference**: Checks that a name written in `ignoreRegExpList` refers to a pattern.

- _Parameters_: the name, and the set of names that `patterns` defines, compared ignoring letter case.
- _Returns_: null when the name is predefined or defined; otherwise the sentence `'<name>' is not a defined or
  predefined pattern name; define it in 'patterns', or write a regular expression between slashes, such as /text/, to
  match literal text.`

**Resolve**: Builds the regular expressions that the two settings describe, with the production match time limit.

- _Parameters_: the entries of `ignoreRegExpList` and the definitions of `patterns`.
- _Returns_: an immutable array of regular expressions, one per pattern that excludes text, in the order of the list;
  empty when both lists are empty.
- _Preconditions_: neither list is null and no entry is null.
- _Algorithm_: the 5-second limit (`MatchTimeoutMilliseconds`) is passed to `ParseDefinitions`, and the result to
  `ResolveEntries`; the two methods below do the work.
- _Postconditions_: a definition that the list does not name adds nothing to the result, and an always-applied name adds
  nothing either, because those patterns run anyway.

**ParseDefinitions**: Parses the definitions of the `patterns` setting.

- _Parameters_: the definitions of `patterns`, and the match time limit of each expression (an internal test seam,
  passed through to `PatternParser`; it must be positive).
- _Returns_: an immutable dictionary of the regular expression of each definition by name, compared ignoring letter
  case.
- _Preconditions_: the list is not null.
- _Algorithm_: for every definition, in order, check the name with `CheckDefinitionName`, reject a name already defined
  (compared ignoring letter case), and parse the pattern with `PatternParser` and the given limit, whether or not
  anything uses the definition, so a mistake is found where it is made.
- _Postconditions_: throws `InvalidPatternException` for the first bad, duplicate, or predefined name or unusable
  pattern.

**ResolveEntries**: Builds the regular expressions of the `ignoreRegExpList` entries from parsed definitions.

- _Parameters_: the entries of `ignoreRegExpList`, the parsed definitions, and the match time limit.
- _Returns_: an immutable array of regular expressions, one per entry that excludes text, in the order of the list.
- _Preconditions_: no argument and no entry is null.
- _Algorithm_: for every entry, in order: an entry that has the shape of a name and was already used (compared ignoring
  letter case) is skipped, so a name listed twice counts once; every other entry is resolved with `TryResolveEntry`,
  a failure is thrown as `InvalidPatternException` with the entry and the reason, and a result that is not null is
  added.
- _Postconditions_: the first entry that cannot be used is thrown; always-applied names add nothing.

**TryResolveEntry**: Resolves one entry, written in `ignoreRegExpList` or in an in-document directive, to a regular
expression, with the failure returned as data.

- _Parameters_: the entry, the parsed definitions, the match time limit, and the out parameters for the regular
  expression and for the problem.
- _Returns_: `true` when the entry can be used, with the regular expression (null for a predefined name that is always
  applied, which excludes nothing by itself); `false` with the reason as a sentence otherwise. Nothing is thrown for an
  unusable entry, so the caller can report the reason where the entry was written.
- _Preconditions_: the entry and the definitions are not null.
- _Algorithm_: (1) an entry that does not have the shape of a name is parsed with `PatternParser` and the given limit,
  and the reason of an `InvalidPatternException` becomes the problem (a literal keeps the flags it is written with; a
  bare expression is read with `i` and `m`); (2) otherwise the trimmed entry is looked up among the definitions
  (ignoring letter case) first, then among the optional predefined patterns, then among the always-applied names, which
  succeed with no regular expression; (3) a name that is none of these fails with the reason built by
  `UnknownNameReason`, the sentence of `CheckReference`.

**FindExcludedRanges**: Appends to a list the ranges of a text that any of the given regular expressions match.

- _Parameters_: the regular expressions (from `Resolve`, `ResolveEntries`, `TryResolveEntry`, or `MarkdownPatterns`),
  the text, and the list that
  receives the ranges.
- _Algorithm_: run each expression over the whole text with the allocation-free match enumeration, and append the index
  and length of each match that has at least one character. A match with no characters is skipped, because it excludes
  nothing: a pattern that ends in a star also matches the empty string at every position.
- _Postconditions_: the list holds its earlier items followed by the ranges, expression by expression. They are neither
  sorted nor free of overlaps; the caller merges them, because only the caller knows what else is excluded. Each
  expression is applied to the whole text, so a match can span lines and can cut a word in two, and the text around the
  match is still checked.

The nine generated patterns are private members of the class that are reached only through the table of optional
patterns.

Design choices and their reasons:

- **Validate every definition, used or not.** A definition that nothing names is still a mistake the user made and will
  want to know about, and validating only what is used would let it hide until the day it is used.
- **A definition cannot reuse a predefined name.** The alternative, letting the user's pattern win, would make the same
  name mean different things in different projects, and a migrated configuration would silently change meaning.
- **The always-applied names are accepted and do nothing.** cspell lists them as predefined, a configuration written for
  cspell may mention them, and since the patterns are in force whether or not they are listed, accepting them keeps the
  configuration loadable and its meaning unchanged.
- **The optional patterns are generated, the user's patterns are constructed.** The nine patterns are known when the
  library is built, so they use the source generator, which keeps the library trim and ahead-of-time safe and avoids the
  cost of building them at start-up; the patterns of a settings file are known only at run time and go through
  `PatternParser`.
- **Failure as data for a single entry.** A directive is written by whoever wrote the text, and its unusable pattern must
  be reported where it is written, with the rest of the text still checked, so `TryResolveEntry` returns the reason
  instead of throwing. The settings path (`Resolve`, `ResolveEntries`) keeps its exception, because an unusable setting
  stops the run. Both use the same method, so a pattern means the same in the settings and in a directive.
- **A timeout parameter, only for tests.** `ParseDefinitions`, `ResolveEntries`, and `TryResolveEntry` take the match
  time limit, so that a test can prove the time-out behavior in milliseconds; `Resolve` and the public API always pass
  the production limit of 5 seconds. The nine generated patterns keep their own fixed limit.
- **Ranges appended to a list.** The same reasons as for the other units of the subsystem: no allocation per call, and
  the caller combines several sources into one list.

#### Error Handling

A null argument, or a null entry in the list, is a programming error and throws `ArgumentNullException`. An entry or a
definition that cannot be used throws `InvalidPatternException` from `Resolve`, `ParseDefinitions`, and
`ResolveEntries`, naming the entry and the reason: the
first problem found is thrown, definitions before entries. `TryResolveEntry` does not throw for an unusable entry; it
returns `false` and the reason. The reasons are those of `PatternParser`, an invalid or
predefined or repeated definition name, and a name that is not defined. Hostile text that makes a pattern exceed its
time limit throws `RegexMatchTimeoutException` from `FindExcludedRanges`; the ranges found so far remain in the list,
but the caller must treat the text as not checked, and `SpellChecker` reports the file as timed out. The class is
stateless and thread-safe; the regular expressions are immutable and safe for concurrent matching, and only the list
passed in is changed, which must not be shared between threads.

#### Dependencies

- **PatternParser** (same subsystem) - parses every pattern, and supplies the match time limit of the generated
  patterns.
- **InvalidPatternException** (same unit as `PatternParser`) - reports every rejected entry.
- **TextRange** (see _WordTokenizer_) - the type of the reported ranges.
- **DefaultPatterns** and **DocumentDirectives** (same subsystem) - not called, but the nineteen always-applied names
  are the names of their patterns.
- **.NET base class library** - `System.Text.RegularExpressions`, including the source generator, and
  `System.Collections.Immutable`. No other unit, subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem) - calls `ParseDefinitions` and `ResolveEntries` once, when the checker is
  created, with the lists of the settings (and keeps the parsed definitions), `TryResolveEntry` for each different
  pattern of the `ignoreRegExp` directives of a text, and `FindExcludedRanges` once per text with the resulting
  expressions and, for a Markdown source, with those of `MarkdownPatterns`. The public constructor uses the 5-second
  limit.
- **SettingsLoader** (`Configuration` subsystem) - uses `IsNameShaped`, `CheckReference`, and `CheckDefinitionName` to
  validate the entries and definitions of a settings file at load time, so the loader and the checker apply the same
  rules.
- **TextLintSettings** (`Configuration` subsystem) - holds `NamedPattern` values in its `Patterns` property.
