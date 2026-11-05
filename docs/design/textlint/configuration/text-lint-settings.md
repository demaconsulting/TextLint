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

| Property        | Type                    | Default | Meaning                                                      |
| :-------------- | :---------------------- | :------ | :----------------------------------------------------------- |
| `Version`       | `string?`               | `null`  | Schema version text from the `version` key; never validated. |
| `Files`         | `IReadOnlyList<string>` | empty   | Include glob patterns; an empty list selects nothing.        |
| `IgnorePaths`   | `IReadOnlyList<string>` | empty   | Exclude glob patterns.                                       |
| `EnableGlobDot` | `bool`                  | `false` | Whether wildcards may match hidden (dot-prefixed) names.     |
| `Language`      | `string`                | `en`    | Language name or comma-separated names; `en` is American.    |
| `Words`         | `IReadOnlyList<string>` | empty   | Words accepted as correct.                                   |
| `IgnoreWords`   | `IReadOnlyList<string>` | empty   | Words never reported, even when also flagged.                |
| `FlagWords`     | `IReadOnlyList<string>` | empty   | Words always reported, even when they are known words.       |
| `MinWordLength` | `int`                   | 4       | Shortest word that is checked; at least 1.                   |
| `MaxFileSize`   | `long`                  | 0       | Largest file size in bytes that is checked; 0 is no limit.   |

The defaults for the spelling settings are cspell's, so that a project that configures none of them
behaves as a cspell user expects. The five list properties are stored as immutable arrays. A getter
converts the uninitialized default array (the state before the property is assigned) to an empty array,
so an unassigned list reads as empty and the uninitialized state never leaks. Because the arrays are
immutable, a `with` expression can share them between the original and the copy without risk. The
scalar properties have plain backing fields initialized to the defaults. Record equality compares the
stored arrays by reference, not by content, so settings are not intended to be compared for equality.

Invariants: no list is null; `Language` is not null; `MinWordLength` is at least 1; `MaxFileSize` is not
negative. The setters enforce them, so no instance can violate them.

#### Key Methods

The type has no methods of its own beyond the property accessors.

**Files, IgnorePaths, Words, IgnoreWords, and FlagWords (init accessors)**: copy the assigned sequence
into an immutable array.

- _Parameters_: the sequence of patterns or words to store.
- _Returns_: nothing; the getter returns the stored list.
- _Preconditions_: the sequence is not null.
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

Patterns, words, and the language are not validated here beyond the checks above. Validation needs
rules that belong to other subsystems (the glob rules of `FileSelection`, the language names of
`Dictionaries`, and the entry rules of `TextScanning`), and keeping this unit free of dependencies keeps
the settings value trivially shareable. The values are validated where they are consumed (file selection
and the spell checker's `TextChecker`) and where they are read from a file (the settings loader), which
use the same rules, so settings built in code cannot carry an unusable value into effect silently.

#### Error Handling

Assigning a null list or language throws `ArgumentNullException`, a minimum word length below 1 throws
`ArgumentOutOfRangeException`, and a negative maximum file size throws `ArgumentOutOfRangeException`, in
each case immediately, because these are programming errors and failing at assignment names the cause.
No other error is detected, and nothing is logged. The type is immutable and therefore thread-safe.

#### Dependencies

- **.NET BCL** - `ImmutableArray<T>` and the collection interfaces. No other unit, subsystem, or OTS
  item is used.

#### Callers

- **SettingsLoader** - creates settings from a settings file.
- **TextChecker** (SpellChecking subsystem) - reads the spelling settings and validates the language and
  the word-list entries again when it is created, because settings built in code bypass the loader.
- **SpellChecker** (SpellChecking subsystem) - reads the spelling settings, including the maximum file size.
- **Library consumers** - construct settings in code, derive modified copies with `with`, and pass the
  pattern lists and hidden-file switch on to `FileSelector`.
