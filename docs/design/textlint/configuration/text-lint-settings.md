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

The two lists are stored as immutable arrays. A getter converts the uninitialized default array
(the state before the property is assigned) to an empty array, so an unassigned list reads as empty
and the uninitialized state never leaks. Because the arrays are immutable, a `with` expression can
share them between the original and the copy without risk. Record equality compares the stored
arrays by reference, not by content, so settings are not intended to be compared for equality.

#### Key Methods

The type has no methods of its own beyond the property accessors.

**Files and IgnorePaths (init accessors)**: copy the assigned sequence into an immutable array.

- _Parameters_: the sequence of patterns to store.
- _Returns_: nothing; the getter returns the stored list.
- _Preconditions_: the sequence is not null.
- _Postconditions_: later changes to the caller's sequence, including an array changed in place, do not
  affect the settings, and the exposed list cannot be mutated, even through a cast to a mutable
  collection interface.

Patterns are not validated here. Validation needs the glob rules that belong to the `FileSelection`
subsystem and is applied where patterns are consumed (file selection) and where they are read from a
file (the settings loader).

#### Error Handling

Assigning a null list throws `ArgumentNullException` immediately, because a null list is a
programming error and failing at assignment names the cause. No other error is detected, and
nothing is logged.

#### Dependencies

- **.NET BCL** - `ImmutableArray<T>` and the collection interfaces. No other unit, subsystem, or OTS
  item is used.

#### Callers

- **SettingsLoader** - creates settings from a settings file.
- **Library consumers** - construct settings in code, derive modified copies with `with`, and pass the
  pattern lists and hidden-file switch on to `FileSelector`.
