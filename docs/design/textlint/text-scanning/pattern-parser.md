### PatternParser

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`PatternParser` turns a pattern written the way cspell writes it, between slashes with flags or as a bare expression,
into a .NET regular expression that has the meaning the pattern has in JavaScript and that is safe to run on any text.
A project migrated from cspell brings its `ignoreRegExpList` and `patterns` entries unchanged, and each must exclude the
same text as before. The unit is made of five types: `PatternParser`, which reads the entry form and the flags and
builds the regular expression; `JavaScriptRegexTranslator`, which translates the body of the pattern from the JavaScript
syntax to .NET and reports what it cannot translate; `InvalidPatternException`, the exception that reports an
unusable entry; and the two helpers `JavaScriptSyntax` and `JavaScriptCaseFolding`, which give the translator and the
other units the JavaScript meaning of white space and letter case. The five are documented together because they are
used only together.

#### Data Model

<!-- cspell:ignore dgimsuvxy gimsu foldings dbff -->

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
- `MaxTranslatedLength` (262144): the longest translation, in characters, that is accepted (see _Limits_).
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

`JavaScriptSyntax` is an `internal static` class of constants and two functions, shared by every unit that writes a
pattern by hand: `SpaceBody`, `Space`, and `NotSpace` (the JavaScript white space set as the body of a class, as a
class, and as its complement), `WordBoundary`, `LineStart`, `NotLineTerminator`, `IsSpace(char)`, and `Trim(string)`.
`JavaScriptCaseFolding` is an `internal static` class whose tables are built once, lazily and thread-safely, for each
of the two modes, from `JavaScriptCaseData`: a generated class of two constants, the groups of characters that a
pinned Node.js treats as the same letter, one group per line in hexadecimal (code units without the `u` flag, code
points with it), with the Node.js and Unicode versions they were recorded from. The tables hold, for every UTF-16 code
unit, a ring of the code units that are the same letter, and for every character outside the basic plane that has a
case, its group. Its functions are `Equivalents` (the ring of one code unit), `Close` (the closure of a set of ranges
under the equivalence), `CloseCodePoints` (the same for code points, closing the characters outside the basic plane by
the groups), `Merge` (sorts and joins ranges), `AstralForms` (the other forms of a character outside the basic plane),
and `Parse` (reads a
table, and refuses damaged data). It calls no casing function, culture, or case option of the regular expression engine.
The table is produced by `Data/generate-javascript-case-data.mjs`, a script that is kept in the repository and is not
run by the build.

`InvalidPatternException` is a `public sealed` class derived from `ArgumentException`, because an unusable pattern is an
invalid argument supplied by the caller. It has `Pattern` (public: the entry exactly as supplied) and `Reason` (internal:
why the entry is rejected, as a sentence, without the entry text, so that the settings loader can build a message that
names the setting and the position). Its constructor is internal, because only this subsystem rejects patterns. The
message is always `Invalid pattern '<entry>': <reason>`, so a bare exception text is enough for a user to find the
entry. The exception of the regular expression engine, when it caused the rejection, is the inner exception. Instances
are immutable.

**Entry forms.** An entry is, after JavaScript white space around it is removed, one of two forms:

- **A literal**, `/body/flags`: the text starts with a slash, has a last slash that is not the first character, and the
  text after the last slash is empty or made only of flag letters. The body is what lies between the first and the last
  slash and may contain unescaped slashes. Only the flags that are written are in force: `/text/` is case-sensitive
  and its line anchors match only at the start and end of the text. A flag may be repeated without error.
- **A bare expression**: anything else, including an entry that starts with a slash but does not end in flag letters,
  such as a path (`/usr/lib/x`). It is read with the flags `i` and `m`: case-insensitive, with the line anchors matching
  at every line, as cspell reads it.

**Flags.** The flags `g`, `i`, `m`, `s`, and `u` are accepted in any combination. `g` changes nothing, because every
match is wanted. `i`, `m`, `s`, and `u` are passed to the translator. The .NET case-insensitive option is **never** used:
it follows the culture tables of .NET, which differ from JavaScript (for example, .NET matches the Kelvin sign U+212A with
`k` in every mode, and does not match `µ` with `μ`), so the translator writes the case behavior of `i` itself (see _Letter
case_ below) and the options are only the invariant culture. `s` makes `.` match every UTF-16 code unit, line terminators
included. The flags `x`, `y`, `d`, and `v` are
rejected by name, with the reason `the flag 'x' is not supported; use only g, i, m, s, and u.`, because they have no
safe equivalent and cspell drops some of them silently, so a pattern that depends on one would not mean what its author
wrote. The `s` flag has an exact equivalent, so it is accepted. A pattern written in an in-document directive cannot
carry `s`, because there only the flag letters that cspell reads (`g`, `i`, `m`, `u`, and `y`) are taken as flags (see
_DocumentDirectives_); this is a deliberate parity decision, and the flag works in the entries of the settings.

**Translation rules.** The translator is a strict recursive-descent reader of the JavaScript syntax. For each construct
it understands it writes the .NET form, and it rejects everything else with a reason, instead of passing text through
that .NET might read differently. Each rule is covered by a unit test.

- _Word characters, digits, and boundaries._ In JavaScript, `\w`, `\d`, and `\b` are ASCII-only, while in .NET they
  are Unicode-aware. `\d` and `\w` are written as classes of the ASCII ranges and `\D` and `\W` as their negations;
  `\b` and `\B` are written as look-around assertions on the ASCII word characters, the same text that
  `DefaultPatterns` uses. Inside a class, `\b` is the backspace character, as in JavaScript.
- _White space._ `\s` is the JavaScript set listed above (the shared `JavaScriptSyntax.SpaceBody`, which the
  hand-written patterns of the other units use too), and `\S` its negation. It is not the .NET set: U+FEFF is in the
  JavaScript set and U+0085 is not. Inside a class, the negated
  shorthands `\D`, `\W`, and `\S` have no .NET spelling with the JavaScript meaning, so they are written out as the
  complementary ranges over the UTF-16 code units.
- _Letter case._ With the `i` flag the translator expands the characters so that the .NET engine, run without its case
  option, matches what JavaScript matches. A literal letter becomes a class of its equivalents, a class is closed
  under the equivalence (a negated class is the complement of the closed set, so `[^a]` excludes `A`), the shorthands
  `\w` and `\W` are closed too, and a `\p{..}` or `\P{..}` is written as the explicit ranges of the category, closed.
  The equivalence is the one of JavaScript, taken from `JavaScriptCaseFolding`: without the `u` flag two code units are
  the same letter when their simple upper-case forms are equal, except that a code unit whose full upper case is
  several characters keeps itself and that a non-ASCII character is never the same as an ASCII one; with the `u` flag
  it is simple case folding. A literal outside the basic plane is expanded only with the `u` flag, into an alternation
  of its forms; with `\b` under `iu` the word class includes U+017F and U+212A, as in JavaScript. A back-reference
  is rejected under `i` (see below).
- _Line terminators._ `.` is the class of every character except `\n`, `\r`, U+2028, and U+2029; with the `s` flag it
  is the class `[\u0000-\uFFFF]` of every UTF-16 code unit, line terminators included, and with the `u` flag it is one
  character in the sense of the next rule. In multiline mode `^`
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
  to a name that is not defined before it is rejected. `\k` in a pattern without a named group is rejected. With the
  `i` flag every back-reference is rejected, with the reason that it cannot be used with the flag `i`: .NET could only
  compare the text with its own case rules, which follow the runtime and differ from JavaScript, and an answer that
  depends on the platform is worse than a refusal. A pattern that needs both uses a literal without the flag.
- _Classes._ The members of a bracketed class are written as `\uXXXX` escapes, ranges as a pair of escapes, and
  shorthand classes as their ranges, so no character can be read as class syntax. With the `i` or the `u` flag a class
  is built as a set of characters (with the case flag closed under letter case, and with the Unicode flag by code
  points, see above) and written from the set. An empty class `[]` matches nothing
  (`(?!)`) and `[^]` matches every code unit (every character with the `u` flag), as in JavaScript. A range that is out
  of order is rejected. Without the
  `u` flag a shorthand next to a hyphen makes the hyphen a plain character, as in JavaScript; with it, that is rejected.
- _Unicode properties._ `\p{..}` and `\P{..}` are accepted only with the `u` flag, which is when JavaScript gives them
  their meaning, and only for the general categories. They are written out as the set of the code points of the
  category, so they read characters outside the basic plane. Scripts and binary properties have no .NET equivalent and are
  rejected, as is the bare `\p` of a pattern without the flag, which JavaScript would read as the letter `p`.
- _Character escapes._ `\t`, `\n`, `\r`, `\f`, `\v`, `\0` (not followed by a digit), `\cX`, `\xHH`, `\uHHHH`, and, with
  the `u` flag, `\u{H..}` are translated. The braced form takes any number of hexadecimal digits, leading zeros
  included (`\u{00000041}` is `A`, as in Node.js v22.18.0), and is rejected only when the value is above 10FFFF, when it
  is empty, or when it holds anything but hexadecimal digits (no sign, no space). An escaped punctuation character
  stands for itself. An escaped letter or digit
  that is not one of the escapes above is rejected, because JavaScript reads it as the plain letter but .NET gives many
  of them a meaning, such as `\z`, `\A`, and `\e`. Octal escapes are rejected.
- _Literal characters._ ASCII letters, digits, and a short list of harmless punctuation are written as they are. Every
  other character is written as a `\uXXXX` escape, so .NET can read it only as itself.
- _Characters outside the basic plane, and the Unicode flag._ Without the `u` flag JavaScript reads the text by UTF-16
  code units, as .NET does, so a literal surrogate pair is two code units and the quantifier applies to the second
  unit only; `.`, a negated class, and the negated shorthands match one code unit, and a class may hold the two units of
  a pair as two members. With the `u` flag JavaScript reads the text by characters: a surrogate pair is one character,
  a surrogate that is not half of a pair is a character of its own, and a match never starts inside a pair. .NET does
  not, so with the flag everything that reads one character is written to read one character:
  - a set of characters (a class, a negated class, a negated shorthand, a Unicode property, and the dot) is written by
    `CodePointSetText` as the alternation of up to four parts: the code units outside the surrogate range as one class,
    the high surrogates followed by no low surrogate, the low surrogates preceded by no high surrogate (the look-around
    is what makes a half of a pair unable to match alone), and the characters outside the basic plane as pairs of a
    lead and a trail class, where adjacent leads with the same trails are joined, so a range of a million code
    points is one pair of classes; a set that has only code units outside the surrogate range stays a plain class, and
    anything else is wrapped in a group so a quantifier covers the whole character;
  - the complement of a set is taken over all code points up to 10FFFF, so `[^a]`, `\W`, `\P{L}`, and `[^]` match a
    pair whole, and a lone surrogate too;
  - a class may hold a character outside the basic plane, as a literal, as `\u{...}`, or as two `\u` escapes of a pair,
    alone or as the end of a range, and a lone surrogate as a member (`[\uD800-\uDBFF]` matches only a high surrogate
    that is not half of a pair);
  - a literal surrogate that is not half of a pair, or an escape of one (`\uD83D`, `\u{D83D}`), matches only where it
    is alone, and two four-digit escapes of a high and a low surrogate in a row are one character (the braced forms are
    never joined), so `\uD83D\uDE00{2}` repeats the pair;
  - a Unicode property is the set of the code points of the category (all planes, from the .NET character data), and
    with the `i` flag the set is closed under letter case (`CloseCodePoints`, which closes the characters outside the
    basic plane by the groups of the table).
  Look-behind needs nothing more: .NET reads the same alternation backwards, and the look-around of the lone surrogate
  forms look at the characters on the right and left of the unit, which is what they must do in either direction.
- _Quantifiers._ `*`, `+`, `?`, `{n}`, `{n,}`, and `{n,m}`, each with an optional lazy `?`. A brace that does not form a
  quantifier is a literal character without the `u` flag (a lone `{`, `}`, or `]` is rejected with it). A quantifier with
  nothing before it, one that follows another, one on an assertion that cannot be repeated, one whose numbers are out of
  order, and one with a number that does not fit a 32-bit integer are rejected.

**Rejected constructs.** Besides the cases above, the translator rejects, each with a reason: group syntax other than
the supported groups (inline options, atomic groups, conditionals, and every other `(?...)` form), a back-reference to
a group that is not complete, a back-reference when the `i` flag is set, a pattern that ends with a backslash, an
unclosed group or class, a closing parenthesis
with no opening one, and a malformed group name, `\c`, `\x`, `\u`, `\k`, or `\p`. The reason is always one sentence
naming the construct.

**Limits.** A pattern body is rejected when it is empty or longer than 4096 characters, when its groups are nested
more than 100 levels deep, or when its translation would be longer than 262,144 characters. The length limit bounds the
work of reading a pattern, but it does not bound the size of the translation: with the `i` flag or the `u` flag a
Unicode property, a negated class, or a shorthand is written out as the explicit set of characters it matches, about
8,600 characters for one `\p{L}` (all planes), so 400 of them would give a pattern of 3.4 million characters, and the
work to build it, before any match could be tried (and before the match time limit applies). The translation limit
stops that: it is more than four times the longest translation of an ordinary pattern of the maximum length (4,096
letters with the `i` flag give 57,344 characters), allows about thirty Unicode properties, and is checked after every
term and after every member of a class (a class counts the ranges it has collected, as the text it will become), so a
hostile pattern is refused after the first few expansions, in milliseconds, with a reason that names the limit and the
flags. Without both flags a class stays its members, so the limit is never reached by a pattern within the length limit.
The nesting limit exists because the reader recurses once per level, so a hostile pattern cannot exhaust
the stack. Repetition counts that fit a 32-bit integer are accepted, because .NET does not unroll them and the
translation does not grow with them, so the time limit, not the count, bounds the work of matching.

**The cost of every construct** was checked against the source length. Literals, groups, quantifiers, anchors,
escapes, and look-around assertions give a constant amount of output per source character (at most 29 characters for
a letter outside the basic plane with the `i` and `u` flags, and about 40 for a lone surrogate with the `u` flag). The
constructs that expand with the `i` or `u` flag (a property, a shorthand, a negated class, a dot with the `u` flag, a
class that collects sets) are the ones that the translation limit bounds, and their work is bounded too: the set of a
category (a pass over the 1,114,112 code points) and the set of a shorthand are computed once per process and kept in
caches of fixed size (37 names, with and without negation and with and without the case flag, and six shorthands in
three modes), and the closure of a set under letter case (`Close`, `CloseCodePoints`) works on the ranges and on the few
thousand letters that have a case, not on all code points, so 1,300 classes of three characters cost milliseconds. The
caches hold immutable values and are filled with a thread-safe dictionary; two threads that fill an entry at once compute
the same value.

**Known differences from JavaScript** that the translation cannot remove, and that are stated rather than hidden:

- A back-reference to a group that did not take part in the match matches the empty string in JavaScript and fails to
  match in .NET. The pattern is accepted, and the match differs only on a text that reaches that case.
- A pattern is matched on UTF-16 code units, and the columns that the checker reports count UTF-16 code units, as the
  text of cspell does. With the `u` flag the translation reads characters outside the basic plane whole (the dot,
  negated classes, negated shorthands, properties, classes with such characters, and surrogates that are not halves of a
  pair), as JavaScript does; this was recorded from Node.js v22.18.0 on 211 cases. A back-reference with the `u` flag
  compares code units, which is the same as comparing the characters.
- Case folding follows JavaScript exactly as far as the table goes: the table is what Node.js 22 (Unicode 16) does, so a
  character that a later Unicode version gives a case (and a newer JavaScript engine therefore folds) is not folded
  until the table is regenerated, and a character that .NET treats as cased is not folded unless the table says so.
  Nothing depends on the platform or the .NET version.
- Characters outside the basic plane have a case only with the `u` flag, as in JavaScript. `\p{..}` and `\P{..}` follow
  the Unicode general categories of the .NET runtime, which are those of the Unicode version of that runtime and not
  those of Node.js, so a character assigned in a recent Unicode version can be in a category in one and not in the
  other; the categories are the same on every operating system for one runtime version.

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
- _Postconditions_: the regular expression is built with the invariant culture, no case option (the case behavior is in
  the translated pattern), and the match time limit. The multiline and dot-all behavior is in the translated pattern,
  not in the options.
- _Algorithm_: (1) trim the entry of JavaScript white space (`JavaScriptSyntax.Trim`, not the .NET trim, which treats
  U+0085 and U+FEFF the other way round); (2) when it starts with a slash, find the last slash, and when that slash is
  not the first character and what follows consists only of flag letters, take the text between the slashes as the body
  and the letters as the flags, otherwise take the whole text as a bare expression with `im`; (3) reject a blank entry;
  (4) reject the first flag that is not `g`, `i`, `m`, `s`, or `u`; (5) translate the body with the multiline, Unicode,
  dot-all, and ignore-case flags; (6) construct the `Regex` with the limit and turn any `ArgumentException` of the
  engine into the exception below.

**JavaScriptRegexTranslator.TryTranslate**: Translates a body to a .NET pattern.

- _Parameters_: the body, whether the multiline flag is set, whether the Unicode flag is set, whether the dot-all flag
  (`s`) is set, whether the ignore-case flag (`i`) is set, and the out parameters for the translation and for the
  problem.
- _Returns_: `true` and the .NET pattern, or `false` and the reason as a sentence. Failure is data: nothing is thrown
  for a bad pattern.
- _Preconditions_: the body is not null; a null body throws `ArgumentNullException`.
- _Algorithm_: (1) reject an empty body and one longer than the limit before reading any of it; (2) create a reader;
  (3) count the capturing groups of the whole pattern, because the meaning of a decimal escape depends on how many
  exist; (4) read alternatives separated by `|`, each a sequence of terms, each term an assertion or an atom with its
  quantifier, writing the .NET form as it goes and checking after every term, and after every member of a class, that
  the translation is within its limit; (5) require that nothing is left over. Every method of the reader returns
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
- **Case behavior in the translation, not in an option, and from data, not from the runtime.** The .NET
  case-insensitive option is culture-table driven and differs from JavaScript on about 80 pairs of characters (the
  Kelvin sign, the long s, the micro sign, the sigma forms, and others), in both directions. Writing the equivalent
  characters out gives the JavaScript behavior whatever the culture of the computer, at the price of longer patterns for
  letters and classes. The equivalences themselves are not computed from the casing functions of .NET either, because
  those follow the Unicode data of the runtime and of the platform's ICU library, and a CI run on Linux and macOS proved
  that a table derived from them differs from one derived on another runtime (the letters U+A7CE to U+A7D5, for example).
  `JavaScriptCaseData` is a table recorded from the pinned Node.js, so a pattern means the same on every operating
  system and every .NET version, and only a deliberate regeneration changes it. The recording script checks the groups
  against the JavaScript engine for every pair of characters that can have a case relation (which found the simple case
  foldings with no `toLowerCase` or `toUpperCase` relation, such as U+0390 with U+1FD3), and the unit tests compare
  every code unit and code point with the recorded file, so a drift or a damaged table is detected.
- **One shared definition of the JavaScript syntax.** `JavaScriptSyntax` holds the white space set, the word boundary,
  the line start, and the dot, as text constants, and `IsSpace` and `Trim`. The translator, `DefaultPatterns`,
  `MarkdownPatterns`, `PatternCatalog`, and `DocumentDirectives` use them, so no unit relies on the .NET meaning of
  `\s`, `\w`, `\d`, `\b`, `.`, `^`, or `$`.
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
- **JavaScriptSyntax** and **JavaScriptCaseFolding** (same subsystem, described in this unit) - the shared white space
  set and trim, and the JavaScript letter equivalence.
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
