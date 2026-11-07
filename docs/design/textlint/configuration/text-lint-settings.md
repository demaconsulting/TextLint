### TextLintSettings

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`TextLintSettings` is the single immutable value that carries TextLint's settings, whether they
were loaded from a `.textlint.yaml` file or built in code. Its responsibility is to hold the
values with safe defaults and to guarantee that a settings value never changes after it is
created, so it can be shared freely across threads.

#### Data Model

`TextLintSettings` is a sealed, non-positional record with `init` properties and defaults, so keys
added in later releases are source-compatible additions.

| Property              | Type                          | Default | Meaning                                          |
| :-------------------- | :---------------------------- | :------ | :----------------------------------------------- |
| `Version`             | `string?`                     | `null`  | Schema version text; never validated.            |
| `Files`               | `IReadOnlyList<string>`       | empty   | Include glob patterns; empty selects nothing.    |
| `IgnorePaths`         | `IReadOnlyList<string>`       | empty   | Exclude glob patterns.                           |
| `EnableGlobDot`       | `bool`                        | `false` | Whether wildcards may match hidden names.        |
| `Language`            | `string`                      | `en`    | Language name or comma-separated names.          |
| `Words`               | `IReadOnlyList<string>`       | empty   | Words accepted as correct.                       |
| `IgnoreWords`         | `IReadOnlyList<string>`       | empty   | Words never reported, even when flagged.         |
| `FlagWords`           | `IReadOnlyList<string>`       | empty   | Words always reported, even when known.          |
| `MinWordLength`       | `int`                         | 4       | Shortest word that is checked; at least 1.       |
| `MaxFileSize`         | `long`                        | 0       | Largest file checked, in bytes; 0 is no limit.   |
| `IgnoreRegExpList`    | `IReadOnlyList<string>`       | empty   | Patterns or pattern names that are not checked.  |
| `Patterns`            | `IReadOnlyList<NamedPattern>` | empty   | Named patterns that IgnoreRegExpList can use.    |
| `IgnoreRandomStrings` | `bool`                        | `true`  | Whether random-looking tokens are skipped.       |
| `MinRandomLength`     | `int`                         | 40      | Length from which a token is judged; at least 1. |

Further notes on the meaning: `Version` is the text of the `version` key, `Files` selects nothing when it is empty, and
`Language` `en` is American English.

The defaults for the spelling settings are cspell's, so that a project that configures none of them
behaves as a cspell user expects. `IgnoreRegExpList` holds the patterns that are added to the default ignore list, which
always applies and is not listed; an entry is a pattern, `/body/flags` or a bare expression, or the name of a pattern.
`Patterns` holds `NamedPattern` values (a name and a pattern, see _PatternCatalog_), and a pattern applies only when
`IgnoreRegExpList` names it. `IgnoreRandomStrings` and `MinRandomLength` control whether a token of at least that length
that looks like random data, and the unknown words inside a hexadecimal sequence of a token, are skipped. The seven list
properties are stored as immutable arrays (the five lists of strings, and `IgnoreRegExpList` and `Patterns`). A getter
converts the uninitialized default array (the state before the property is assigned) to an empty array,
so an unassigned list reads as empty and the uninitialized state never leaks. Because the arrays are
immutable, a `with` expression can share them between the original and the copy without risk. The
scalar properties have plain backing fields initialized to the defaults (the switch `IgnoreRandomStrings` is an
auto-property that defaults to `true`). Record equality compares the
stored arrays by reference, not by content, so settings are not intended to be compared for equality.

Invariants: no list is null and no list entry is null; `Language` is not null; `MinWordLength` and `MinRandomLength` are
at least 1; `MaxFileSize` is not negative. The setters enforce them, so no instance can violate them.

#### Key Methods

The type has no methods of its own beyond the property accessors.

**Files, IgnorePaths, Words, IgnoreWords, FlagWords, IgnoreRegExpList, and Patterns (init accessors)**: copy the
assigned sequence into an immutable array.

- _Parameters_: the sequence of patterns, words, or named patterns to store.
- _Returns_: nothing; the getter returns the stored list.
- _Preconditions_: the sequence is not null and holds no null entry.
- _Postconditions_: later changes to the caller's sequence, including an array changed in place, do not
  affect the settings, and the exposed list cannot be mutated, even through a cast to a mutable
  collection interface.

**Language (init accessor)**: stores the language text.

- _Parameters_: the language name or comma-separated names.
- _Preconditions_: the text is not null.
- _Postconditions_: the text is stored as given.

**MinWordLength (init accessor)**: stores the minimum word length.

- _Parameters_: the length, counted in UTF-16 code units as cspell counts it.
- _Preconditions_: the length is at least 1.
- _Postconditions_: the length is stored.

**MaxFileSize (init accessor)**: stores the maximum file size.

- _Parameters_: the size in bytes.
- _Preconditions_: the size is not negative; 0 means no limit.
- _Postconditions_: the size is stored.

**MinRandomLength (init accessor)**: stores the length from which a token is judged to be random.

- _Parameters_: the length, counted in UTF-16 code units as cspell counts it.
- _Preconditions_: the length is at least 1.
- _Postconditions_: the length is stored.

**IgnoreRandomStrings (init accessor)**: stores the switch; every value is meaningful, so nothing is checked.

Patterns, words, and the language are not validated here beyond the checks above. Validation needs
rules that belong to other subsystems (the glob rules of `FileSelection`, the language names of
`Dictionaries`, and the entry and pattern rules of `TextScanning`), and keeping this unit free of validation
dependencies keeps the settings value trivially shareable. Its one dependency on `TextScanning` is the `NamedPattern`
type that the `Patterns` property holds, which only carries a name and a pattern. The values are validated where they
are consumed (file selection and the spell checker's `TextChecker`) and where they are read from a file (the settings
loader), which use the same rules, so settings built in code cannot carry an unusable value into effect silently.

#### Error Handling

Assigning a null list, a list with a null entry, or a null language throws `ArgumentNullException`, a minimum word
length or minimum random length below 1 throws `ArgumentOutOfRangeException`, and a negative maximum file size throws
`ArgumentOutOfRangeException`, in
each case immediately, because these are programming errors and failing at assignment names the cause.
No other error is detected, and nothing is logged. The type is immutable and therefore thread-safe.

#### Dependencies

- **NamedPattern** (TextScanning subsystem) - the element type of the `Patterns` list. No behavior of the type is used.
- **.NET BCL** - `ImmutableArray<T>` and the collection interfaces. No other unit, subsystem, or OTS
  item is used.

#### Callers

- **SettingsLoader** - creates settings from a settings file.
- **TextChecker** (SpellChecking subsystem) - reads the spelling settings, including the patterns and the random-string
  settings, and validates the language, the word-list entries, and the patterns again when it is created, because
  settings built in code bypass the loader.
- **SpellChecker** (SpellChecking subsystem) - reads the spelling settings, including the maximum file size.
- **Library consumers** - construct settings in code, derive modified copies with `with`, and pass the
  pattern lists and hidden-file switch on to `FileSelector`.
