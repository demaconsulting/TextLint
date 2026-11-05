### WordTokenizer

![TextScanning Structure](TextScanningView.svg)

#### Purpose

`WordTokenizer` is the one place that decides what a word is, so that the spell checker, the settings loader, and the
document directives all agree. It finds the tokens, words, and case-change pieces of a text as ranges of the original
text, and it interprets the entries of word lists. The rules follow what cspell does, so a project migrated from cspell
gets the same words: a word is a run of letters, an apostrophe may sit between two letters, digits and most
punctuation end a word, Han, Hiragana, Katakana, and Hangul are never words, and a word is split for checking at
case changes.

#### Data Model

`WordTokenizer` is an `internal static` class with no state. Its results are two immutable value types.

- **TextRange** (`readonly record struct`): `Start`, the zero-based offset of the first UTF-16 code unit, and `Length`,
  the number of code units. `End` is `Start + Length` and `IsEmpty` is `Length == 0`. Every finding of the subsystem
  is a `TextRange` into the original string, never a copy of text, so findings from different units can be combined
  without allocation and converted to a line and column in one place.
- **ListEntry** (`readonly record struct`): `Word`, the entry without edge punctuation and without its suggestions;
  `IsSingleWord`, `true` when the entry is exactly one word and is therefore matched against words, `false` when it
  contains digits or punctuation and is therefore matched against whole tokens; and `Suggestions`, an immutable array,
  empty except for a flagged-word entry.

Terms used by the rules:

- A **token** is a maximal run of letters, ASCII digits, underscores, apostrophes (straight and typographic),
  backticks, dots, plus signs, and hyphens. A backslash directly
  before a straight apostrophe belongs to the token. Tokens are what list entries with digits or punctuation, such as
  `k8s` or `foo-bar`, are matched against.
- A **word** is a run of letters, each optionally followed by one combining mark, in which a straight or typographic
  apostrophe, optionally preceded by a backslash, is allowed between two letters. Digits, underscores, hyphens, dots,
  and everything else end a word; an apostrophe at the start or the end of a run is not part of it.
- A **piece** is part of a word split at a case change: before a capital that follows a lower-case letter (`fooBar`),
  and before the last capital of a run of capitals that is followed by a lower-case letter (`XMLParser`), except that a
  capital that starts one of seven common English suffixes, such as the plural s, when no lower-case letter follows
  the suffix does not split (`APIs` stays whole). The suffix list is an ordinal, case-sensitive table in the order
  cspell lists it.
- Letters are the Unicode letter categories (upper, lower, title, modifier, other), combining marks are the three mark
  categories, and the scripts that are never words are a fixed table of coarse code point blocks.

#### Key Methods

**FindTokens**: Appends the tokens found in a part of a text.

- _Parameters_: the text, the offsets `start` and `end` of the part, and the list that receives the ranges.
- _Algorithm_: skip characters that cannot start a token; extend a token over every continuation character, taking
  `\'` as one unit; append the range. The scan never reads outside the part, so a caller can scan the pieces of a
  text that remain after excluded ranges are removed.
- _Postconditions_: the list holds its earlier items followed by the tokens in order.

**FindWords**: Appends the words found inside a range.

- _Algorithm_: skip characters that are not word letters; start a word with a letter and an optional combining mark;
  keep extending while the next letter follows directly or after one apostrophe (with an optional backslash before
  it). An apostrophe joins the word only when a letter follows it, which is how `don't` is one word and the closing
  quote of `'alpha'` is not part of one. The reported range is the original text, so a typographic apostrophe stays
  typographic.

**SplitCase**: Appends the pieces of one word.

- _Algorithm_: test every boundary between two characters. The character after the boundary must be a capital. The
  boundary splits when a lower-case letter precedes it, or when a capital precedes it and a lower-case letter follows
  the capital, unless the English-suffix exception applies. A combining mark is looked through on both sides, so an
  accented capital written as a letter and a mark splits like a plain capital.
- _Postconditions_: the pieces cover the word completely and in order; a word with no case change yields one piece.

**TrimToken**: Removes leading and trailing characters that are neither letters nor digits from a token, so that
`(k8s)` or `v1.2.` in a sentence match the entries `k8s` and `v1.2`. A token with no letter or digit becomes an empty
range.

**TryParseListEntry**: Interprets one entry of a word list (`words`, `ignoreWords`, `flagWords`, or a directive).

- _Parameters_: the entry (null allowed), whether suggestions are allowed, and two out parameters for the result or
  the reason.
- _Algorithm_: reject a blank entry; when suggestions are allowed, split `word->one, two` at the arrow, or
  `word: one` at the colon, trying the arrow first so a colon inside a suggestion list is not misread; trim the word;
  reject it if it could never match; otherwise normalize it like a token and classify it by finding the words in it.
- _Reasons_: the exact phrases, each a reason that completes the sentence "The entry is rejected because ...", are:
  - `it is blank.`
  - `there is no word before its suggestions.`
  - `the forbidden-word marker '!' is not supported; list the word in flagWords instead.`
  - `it contains white space, so it can never match a word; list each word separately.`
  - `it contains the character 'c', which cannot be part of a word, so it can never match.`, where `c` is the first
    offending character
  - `it has no letter or digit, so it can never match.`

**IsWordLetter**: Classifies a code point as a letter of a script that is spell-checked: ASCII letters by a direct test,
other code points by Unicode category, minus Han, Hiragana, Katakana, and Hangul.

**CountCharacters**: Counts Unicode scalar values, a surrogate pair once and a lone surrogate as one, where cspell
measures a length in characters rather than in UTF-16 code units, as for the minimum word length.

The private helpers decode one code point forwards or backwards (a well-formed pair is one code point, anything else
is the replacement character, which is a symbol and never a letter), look through one combining mark, and validate the
requested part once at each entry point so that the loops can rely on the bounds.

Design choices and their reasons:

- **A hand-written scanner, not regular expressions.** Word finding runs for every character of every file, so it
  must neither allocate nor backtrack. The scanner walks the string once and reports offsets; a regular-expression
  version would allocate a match per word, would need per-character-class tables written as Unicode property
  classes, and would bring the risk of catastrophic backtracking and a time limit into the hottest loop. Hand-written
  code also gives exact control of the decisions that matter: surrogate pairs, combining marks, and what a lone
  surrogate is.
- **Coarse blocks for the scripts that are not checked.** .NET has no script property lookup, so Han, Hiragana,
  Katakana, and Hangul are recognized by whole code point blocks (including compatibility, supplementary, and
  half-width forms). A block holds no Latin-script letters, and a non-letter inside a block is already excluded by the
  letter test, so coarse ranges give the same answer as exact script data without a table that must be kept current.
- **List entries are interpreted here.** Whether an entry is a single word or a token entry depends on exactly the
  rules that find words, so the decision is made by the same code. Loading a setting and checking text can therefore
  never disagree, and an entry that could never match is found when settings are read instead of silently having
  no effect.
- **Out-buffers instead of returned lists.** The methods append to a list the caller owns and never clear it, so a
  caller can reuse one list for many scans and can collect the findings of several scans in one list.

#### Error Handling

A null text or list is a programming error and throws `ArgumentNullException`; a part that is not inside the text
throws `ArgumentOutOfRangeException`, naming the offsets. The check is done once per call, before any scanning.
Text content never causes an error: an empty text has no findings, a lone surrogate, or a pair cut by the end of the
requested part, decodes as a character that is not a letter and is skipped, and every loop advances at least one code
unit, so no input can make a method loop or throw. A list entry that cannot be used is not an exception: it is
reported through the return value and the reason, because rejecting entries is an expected outcome that the settings
loader turns into a positioned error message. All methods are stateless and thread-safe; the methods that fill a list
change only that list, which must not be shared between threads.

#### Dependencies

- **.NET base class library** - `string` indexing, `char` classification, `CharUnicodeInfo` for the Unicode
  category, and `ImmutableArray` for the suggestions of a list entry. No other unit, subsystem, or OTS item is used.

#### Callers

- **TextChecker** (`SpellChecking` subsystem, with its supporting word-rule type) - finds the tokens of the text it
  checks, trims them, finds the words within each token, splits the words at case changes, parses list entries, and
  measures word lengths with `CountCharacters`.
- **SettingsLoader** (`Configuration` subsystem) - calls `TryParseListEntry` to validate the word-list settings and to
  turn a failure into a positioned error.
