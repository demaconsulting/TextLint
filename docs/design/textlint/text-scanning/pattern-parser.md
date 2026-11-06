### PatternParser

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`PatternParser` turns a pattern written the way cspell writes it, between slashes with flags or as a bare expression,
into a .NET regular expression that has the meaning the pattern has in JavaScript and that is safe to run on any text.
A project migrated from cspell brings its `ignoreRegExpList` and `patterns` entries unchanged, and each must exclude the
same text as before. The unit is made of three types: `PatternParser`, which reads the entry form and the flags and
builds the regular expression; `JavaScriptRegexTranslator`, which translates the body of the pattern from the JavaScript
syntax to .NET and reports what it cannot translate; and `InvalidPatternException`, the exception that reports an
unusable entry. The three are documented together because they are used only together.

#### Data Model

<!-- cspell:ignore dgimsuvxy gimsu -->

`PatternParser` is an `internal static` class with no state. Its constants are the flag letters and the time limit:

- `BareFlags` (`im`): the flags of an entry that has no slashes. cspell reads a bare expression with the flags `g`,
  `i`, and `m`, and `g` is always in force here, so `i` and `m` remain.
- `FlagLetters` (`dgimsuvxy`): the letters that count as flags of a literal, whether or not they are supported. Text
  after the last slash is read as flags only when it is made of these letters.
- `SupportedFlags` (`gimsu`): the flags that are accepted.
- `MatchTimeoutMilliseconds` (5000): the production limit, the longest time a pattern may spend on one text. It is the
  constant of `DefaultPatterns`, so every regular expression the library runs has the same limit. An internal overload
  of `Parse` takes another limit (see _Key Methods_), which only tests use.

`JavaScriptRegexTranslator` is an `internal static` class whose state is fixed for the life of the process: the limits,
the text of the fixed translations, and the tables of character ranges.

- `MaxPatternLength` (4096): the longest pattern body, in UTF-16 code units, that is accepted.
- A private nesting limit of 100: the deepest nesting of groups that is accepted.
- The translation of the line terminators, `.`, `^`, `$`, `\b`, and `\B` (see the rules below), written once as
  constants.
- The ranges of the ASCII digits, the ASCII word characters, and the characters that JavaScript counts as white space
  (U+0009 to U+000D, U+0020, U+00A0, U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F, U+3000, and U+FEFF),
  each with its complement over the range of a UTF-16 code unit, written as the body of a class of `\uXXXX` escapes.
- The 37 names of the Unicode general categories (`L`, `Lu`, `Ll`, `Lt`, `Lm`, `Lo`, `M`, `Mn`, `Mc`, `Me`, `N`, `Nd`,
  `Nl`, `No`, `P`, `Pc`, `Pd`, `Ps`, `Pe`, `Pi`, `Pf`, `Po`, `S`, `Sm`, `Sc`, `Sk`, `So`, `Z`, `Zs`, `Zl`, `Zp`, `C`,
  `Cc`, `Cf`, `Cs`, `Co`, and `Cn`), which .NET spells as JavaScript does.

Each call to the translator creates a private `Reader`, a sealed class that holds the pattern, the three flags it needs
(multiline, Unicode, and dot-all), the translation written so far, the groups that have been closed, the names of the named
groups with their numbers, the position, the number of capturing groups opened so far and in the whole pattern, whether
the pattern has a named group, the current depth, and the first problem found. A reader is never shared, which is what
makes the translator thread-safe.

`InvalidPatternException` is a `public sealed` class derived from `ArgumentException`, because an unusable pattern is an
invalid argument supplied by the caller. It has `Pattern` (public: the entry exactly as supplied) and `Reason` (internal:
why the entry is rejected, as a sentence, without the entry text, so that the settings loader can build a message that
names the setting and the position). Its constructor is internal, because only this subsystem rejects patterns. The
message is always `Invalid pattern '<entry>': <reason>`, so a bare exception text is enough for a user to find the
entry. The exception of the regular expression engine, when it caused the rejection, is the inner exception. Instances
are immutable.

**Entry forms.** An entry is, after white space around it is removed, one of two forms:

- **A literal**, `/body/flags`: the text starts with a slash, has a last slash that is not the first character, and the
  text after the last slash is empty or made only of flag letters. The body is what lies between the first and the last
  slash and may contain unescaped slashes. Only the flags that are written are in force: `/text/` is case-sensitive
  and its line anchors match only at the start and end of the text. A flag may be repeated without error.
- **A bare expression**: anything else, including an entry that starts with a slash but does not end in flag letters,
  such as a path (`/usr/lib/x`). It is read with the flags `i` and `m`: case-insensitive, with the line anchors matching
  at every line, as cspell reads it.

**Flags.** The flags `g`, `i`, `m`, `s`, and `u` are accepted in any combination. `g` changes nothing, because every
match is wanted. `i` selects case-insensitive matching with the invariant culture. `m`, `s`, and `u` are passed to the
translator; `s` makes `.` match every UTF-16 code unit, line terminators included. The flags `x`, `y`, `d`, and `v` are
rejected by name, with the reason `the flag 'x' is not supported; use only g, i, m, s, and u.`, because they have no
safe equivalent and cspell drops some of them silently, so a pattern that depends on one would not mean what its author
wrote. The `s` flag has an exact equivalent, so it is accepted. A pattern written in an in-document directive cannot
carry `s`, because there only the flag letters that cspell reads (`g`, `i`, `m`, `u`, and `y`) are taken as flags (see
_DocumentDirectives_); the flag works in the entries of the settings.

**Translation rules.** The translator is a strict recursive-descent reader of the JavaScript syntax. For each construct
it understands it writes the .NET form, and it rejects everything else with a reason, instead of passing text through
that .NET might read differently. Each rule is covered by a unit test.

- _Word characters, digits, and boundaries._ In JavaScript, `\w`, `\d`, and `\b` are ASCII-only, while in .NET they
  are Unicode-aware. `\d` and `\w` are written as classes of the ASCII ranges and `\D` and `\W` as their negations;
  `\b` and `\B` are written as look-around assertions on the ASCII word characters, the same text that
  `DefaultPatterns` uses. Inside a class, `\b` is the backspace character, as in JavaScript.
- _White space._ `\s` is the JavaScript set listed above, and `\S` its negation. Inside a class, the negated
  shorthands `\D`, `\W`, and `\S` have no .NET spelling with the JavaScript meaning, so they are written out as the
  complementary ranges over the UTF-16 code units.
- _Line terminators._ `.` is the class of every character except `\n`, `\r`, U+2028, and U+2029; with the `s` flag it
  is the class `[\u0000-\uFFFF]` of every UTF-16 code unit, line terminators included. In multiline mode `^`
  is the look-behind that is true at the start of the text or after one of those four characters, and `$` the
  look-ahead that is true at the end of the text or before one. Without the multiline flag, `^` is `\A` and `$` is
  `\z`, so `$` does not match before a final line feed, as in JavaScript (where .NET's `$` would). The translated
  pattern is therefore always built without the .NET multiline option. An anchor or a word boundary cannot be repeated.
- _Groups._ Every capturing group, named or not, is written as a plain group, so .NET numbers the groups by the order of
  their opening parentheses, exactly as JavaScript does; .NET would otherwise number named groups after the plain ones.
  Non-capturing groups and the four look-around assertions are written as they are. A look-ahead may be repeated only
  without the `u` flag, and a look-behind never. A group name may hold only ASCII letters, digits, `_`, and `$`, may
  not start with a digit, and may not be used twice.
- _Back-references._ `\N` and `\k<name>` are written as `(?:\k<N>)` with the number of the group, in a group so that a
  digit that follows cannot become part of the number. The referenced group must be complete at that point; a reference
  to a group that does not exist (where JavaScript would read an octal escape), to a group that has not been closed, or
  to a name that is not defined before it is rejected. `\k` in a pattern without a named group is rejected.
- _Classes._ The members of a bracketed class are written as `\uXXXX` escapes, ranges as a pair of escapes, and
  shorthand classes as their ranges, so no character can be read as class syntax. An empty class `[]` matches nothing
  (`(?!)`) and `[^]` matches every code unit, as in JavaScript. A range that is out of order is rejected. Without the
  `u` flag a shorthand next to a hyphen makes the hyphen a plain character, as in JavaScript; with it, that is rejected.
- _Unicode properties._ `\p{..}` and `\P{..}` are accepted only with the `u` flag, which is when JavaScript gives them
  their meaning, and only for the general categories. Scripts and binary properties have no .NET equivalent and are
  rejected, as is the bare `\p` of a pattern without the flag, which JavaScript would read as the letter `p`.
- _Character escapes._ `\t`, `\n`, `\r`, `\f`, `\v`, `\0` (not followed by a digit), `\cX`, `\xHH`, `\uHHHH`, and, with
  the `u` flag, `\u{H..}` are translated. An escaped punctuation character stands for itself. An escaped letter or digit
  that is not one of the escapes above is rejected, because JavaScript reads it as the plain letter but .NET gives many
  of them a meaning, such as `\z`, `\A`, and `\e`. Octal escapes are rejected.
- _Literal characters._ ASCII letters, digits, and a short list of harmless punctuation are written as they are. Every
  other character is written as a `\uXXXX` escape, so .NET can read it only as itself.
- _Characters outside the basic plane._ A literal surrogate pair, or `\u{...}` above U+FFFF, is two code units. With
  the `u` flag it is written as a group of the two escapes, so a quantifier covers both; without the flag the
  quantifier applies to the second unit only, as in JavaScript. Inside a class such a character is rejected.
- _Quantifiers._ `*`, `+`, `?`, `{n}`, `{n,}`, and `{n,m}`, each with an optional lazy `?`. A brace that does not form a
  quantifier is a literal character without the `u` flag (a lone `{`, `}`, or `]` is rejected with it). A quantifier with
  nothing before it, one that follows another, one on an assertion that cannot be repeated, one whose numbers are out of
  order, and one with a number that does not fit a 32-bit integer are rejected.

**Rejected constructs.** Besides the cases above, the translator rejects, each with a reason: group syntax other than
the supported groups (inline options, atomic groups, conditionals, and every other `(?...)` form), a back-reference to
a group that is not complete, a pattern that ends with a backslash, an unclosed group or class, a closing parenthesis
with no opening one, and a malformed group name, `\c`, `\x`, `\u`, `\k`, or `\p`. The reason is always one sentence
naming the construct.

**Limits.** A pattern body is rejected when it is empty or longer than 4096 characters, or when its groups are nested
more than 100 levels deep. The length limit bounds the work of reading a pattern and the size of the regular expression
built from it. The nesting limit exists because the reader recurses once per level, so a hostile pattern cannot exhaust
the stack. Repetition counts that fit a 32-bit integer are accepted, so the time limit, not the count, bounds the work
of matching.

**Known differences from JavaScript** that the translation cannot remove, and that are stated rather than hidden:

- A back-reference to a group that did not take part in the match matches the empty string in JavaScript and fails to
  match in .NET. The pattern is accepted, and the match differs only on a text that reaches that case.
- `.` and negated classes match one UTF-16 code unit, so with the `u` flag (and with `s`) they match half of a
  character outside the basic plane where JavaScript matches the whole character. The columns that the checker reports
  count UTF-16 code units for the same reason.

#### Key Methods

**PatternParser.Parse**: Parses an entry into a regular expression. There are two forms: `Parse(entry)`, used in
production, builds the expression with the limit of 5 seconds (`MatchTimeoutMilliseconds`) and calls
`Parse(entry, matchTimeout)`, the internal test seam, which builds it with the limit it is given so that a test can
prove the time-out behavior in milliseconds. Generated (source-generated) regular expressions of other units keep
their own fixed limit of 5 seconds; the seam reaches only the expressions that `PatternParser` builds.

- _Parameters_: `string entry`, the entry as written; and, in the second form, `TimeSpan matchTimeout`, the longest
  time one match operation may take.
- _Returns_: a `Regex`, never null.
- _Preconditions_: the entry is not null; the limit is positive and not infinite, otherwise
  `ArgumentOutOfRangeException` is thrown.
- _Postconditions_: the regular expression is built with the invariant culture, the case option of the `i` flag, and
  the match time limit. The multiline and dot-all behavior is in the translated pattern, not in the options.
- _Algorithm_: (1) trim the entry; (2) when it starts with a slash, find the last slash, and when that slash is not the
  first character and what follows consists only of flag letters, take the text between the slashes as the body and the
  letters as the flags, otherwise take the whole text as a bare expression with `im`; (3) reject a blank entry;
  (4) reject the first flag that is not `g`, `i`, `m`, `s`, or `u`; (5) translate the body with the multiline, Unicode,
  and dot-all flags; (6) construct the `Regex` with the limit and turn any `ArgumentException` of the engine into the
  exception below.

**JavaScriptRegexTranslator.TryTranslate**: Translates a body to a .NET pattern.

- _Parameters_: the body, whether the multiline flag is set, whether the Unicode flag is set, whether the dot-all flag
  (`s`) is set, and the out parameters for the translation and for the problem.
- _Returns_: `true` and the .NET pattern, or `false` and the reason as a sentence. Failure is data: nothing is thrown
  for a bad pattern.
- _Preconditions_: the body is not null; a null body throws `ArgumentNullException`.
- _Algorithm_: (1) reject an empty body and one longer than the limit before reading any of it; (2) create a reader;
  (3) count the capturing groups of the whole pattern, because the meaning of a decimal escape depends on how many
  exist; (4) read alternatives separated by `|`, each a sequence of terms, each term an assertion or an atom with its
  quantifier, writing the .NET form as it goes; (5) require that nothing is left over. Every method of the reader returns
  whether it succeeded, the first failure records its reason, and every caller stops, so the reason is always the first
  problem in the pattern.

**InvalidPatternException constructor (internal)**: Creates the exception from an entry, a reason, and an optional inner
exception.

The design choices and their reasons:

- **A translator, not a pass-through.** Handing a JavaScript pattern to the .NET engine would be simpler, and would
  silently give `\w`, `\d`, `\b`, `.`, `$`, the numbering of named groups, and many escapes a different meaning, so
  a pattern would exclude different text than in cspell with no sign. Each construct is therefore either written in a
  form with the JavaScript meaning or rejected, and a rejected pattern tells its author what to rewrite.
- **Everything that cannot be translated is rejected.** An unsupported flag or construct is an error at load time,
  where cspell either ignores it or lets the JavaScript engine decide. The cost is that a few exotic patterns must be
  rewritten; the benefit is that no pattern that loads can mean something other than what its author wrote.
- **Failure as data inside, an exception outside.** The translator returns a reason, so the caller can attach a
  position, and `Parse` turns the reason into `InvalidPatternException`, which is the usual way a caller passing bad
  settings in code learns about them. The settings loader catches it and reports the reason at the node of the entry.
- **Every literal is an escape.** Writing every character that is not a letter, a digit, or a harmless punctuation mark
  as `\uXXXX` makes it impossible for .NET to read a character as syntax, at the price of a longer pattern that no one
  reads.
- **Runtime-constructed, interpreted expressions.** The patterns are known only at run time, so the source generator
  that the default patterns use cannot be applied. The regular expressions are constructed without the compiled option,
  so no code is generated at run time.
- **A time limit on every expression, with its limits stated.** A pattern that works on every file of a project can
  still backtrack catastrophically on a file built to make it, and cspell, which has no limit, would hang. The limit
  of five seconds turns that into a `RegexMatchTimeoutException` that `SpellChecker` reports as a timed-out file.
  The limit is a safety net, not a guarantee of speed. It stops one match operation of one pattern on one text after
  five seconds, but it does not prevent the processor from being busy until then. A text can therefore cost up to five
  seconds for each pattern before it is reported, and a run can lose that time for each file that is built to trigger
  it. The first time-out aborts the check of that file, so the worst case is bounded by the number of patterns that
  apply to the file: the patterns of the settings, plus at most 32 different patterns that the in-document
  `ignoreRegExp` directives of the file add (see _TextChecker_), that is at most (settings patterns + 32) x 5 seconds
  for one file, and only for a file built to trigger it. The time limit does not apply to reading a pattern; the length
  and nesting limits bound that work, and a pattern of the settings file is read when the settings are loaded and again
  when a checker is created, not per file.
- **An injectable limit for tests only.** Proving that a catastrophic pattern ends in a time-out would take five
  seconds per test with the production limit. The internal overload of `Parse` that takes the limit (and the internal
  constructor of `TextChecker` that passes it on) lets a test use a few milliseconds. Production code never passes a
  limit, so the limit in force is always the 5 seconds; the seam is not a setting and is not exposed to users.

#### Error Handling

A null entry or body throws `ArgumentNullException`, and a limit that is not positive throws
`ArgumentOutOfRangeException`. An entry that cannot be used (blank, an unsupported flag, a body
that is empty, too long, nested too deep, untranslatable, or not accepted by the regular expression engine) throws
`InvalidPatternException` from `Parse`, with the entry and the reason; the translator itself never throws for bad input
and returns the reason. The translation is built to be valid .NET, so the engine rejects it only for a construct that
only the engine can judge, and that exception is kept as the inner exception. Matching can throw
`RegexMatchTimeoutException` after the time limit; this is not handled here but by the caller of the match, which treats
the text as not checked (see _PatternCatalog_ and _TextChecker_). The classes are stateless and thread-safe; the
regular expressions they create are immutable and safe for concurrent matching.

#### Dependencies

- **DefaultPatterns** (same subsystem) - the constant `MatchTimeoutMilliseconds`, so that every pattern of the library
  shares one time limit.
- **.NET base class library** - `System.Text.RegularExpressions`, number parsing, and text handling. No other unit,
  subsystem, or OTS item is used.

#### Callers

- **PatternCatalog** (same subsystem) - parses every entry of `ignoreRegExpList` and every in-document directive
  pattern that is not a name, and every definition of `patterns`, with the limit it is given, and reads
  `MatchTimeoutMilliseconds` for the patterns it generates.
- **MarkdownPatterns** and **RandomStringDetector** (same subsystem) - read `MatchTimeoutMilliseconds` for their
  generated patterns.
- **SettingsLoader** (`Configuration` subsystem) - parses each pattern at load time, to report an unusable one at its
  position, and reads the `Reason` of the exception.
- **Library consumers** - receive `InvalidPatternException`, the one public type of the unit, from the `TextChecker`
  constructor when settings built in code hold an unusable pattern.
