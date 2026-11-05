### SpellingDictionary

![Dictionaries Structure](DictionariesView.svg)

#### Purpose

`SpellingDictionary` is the public word lookup of the library. Its single responsibility is to turn a language
choice into a selection of dictionaries and to answer whether a word is known to that selection, so that a
spell checker, or any other consumer, never deals with dictionary files, case folding, or loading.

#### Data Model

`SpellingDictionary` is a sealed class. A selection holds one field, the lazily loaded list of the
`DictionaryImage` objects it consults, in the order common, regional (American, British, or both), and technical.
The class has three private static instances, one for each possible selection (American, British, and both), so
no instance is ever created by a caller, and four private static lazy values, one for each embedded
dictionary, that every selection shares. The instances are immutable: they hold no state that changes after the
images are loaded, and the loaded images are immutable.

The supported language names are `en` (the same as `en-US`), `en-GB`, and `en-US`, matched ignoring letter case.
They are kept in a fixed read-only list that `SupportedLanguages` exposes in alphabetical order. The names of
the four embedded resources are built from one prefix, the dictionary name, and the extension `.tld`; the prefix
and the dictionary names are the contract with the library project, which gives the converted files those logical
names.

#### Key Methods

**ForLanguages**: Selects the shared instance for a list of language names.

- _Parameters_: `string languages` - one name or several separated by commas.
- _Returns_: the shared selection.
- _Preconditions_: `languages` is not null.
- _Postconditions_: equal selections return the same instance; nothing is loaded.

Steps: (1) split the text at commas; (2) trim each part, and reject an empty part (which also rejects an empty
or blank text, because it splits to one empty part); (3) compare the part with the supported names ignoring
letter case, setting the American or British flag, and reject any other name; (4) return the American, British, or
combined instance for the flags. An American selection is the result when only `en` or `en-US` was named, and at
least one flag is always set when no error was raised.

**IsKnown**: Answers whether a word is known.

- _Parameters_: `ReadOnlySpan<char> word`.
- _Returns_: `true` when any dictionary of the selection holds the folded word.
- _Preconditions_: none; any text is allowed.
- _Postconditions_: nothing is allocated once the dictionaries are loaded, and no state changes.

Steps: (1) fold the word into a stack buffer with `DictionaryFormat.TryFold`, which also lowers the capitals
of ASCII and Latin-1, straightens the typographic apostrophe, and refuses text that cannot be a key; (2) hash
the key once; (3) ask each image of the selection with the key and hash and return `true` at the first hit;
(4) return `false`. The first call of a selection triggers its loading.

**LoadLazily** (private): Creates the thread-safe lazy loader of one embedded dictionary from its name, so that
the load happens once for the process and its outcome, including a failure, is cached.

#### Error Handling

Callers' mistakes are exceptions with the offending value in the message: a null language list throws
`ArgumentNullException`, and an empty list, an empty name, or an unsupported name throws `ArgumentException`
that names the value and lists the supported names. A word is never a mistake: any text, however odd, is
answered `true` or `false`. A missing or damaged embedded dictionary surfaces as the `InvalidDataException` that
`DictionaryImage` throws, which names the resource; it is not caught or translated, because it means the library
itself is defective, and because the load outcome is cached, every lookup of the selection reports the same error.

#### Dependencies

- **DictionaryImage** - holds and probes each dictionary and loads the embedded resources.
- **DictionaryFormat** (a unit of the DictionaryBuilder system, compiled into the library) - the case fold and the
  hash. This is a documented dependency on the format definitions that the writer and the reader share.
- **.NET BCL** - lazy initialization, collections, string comparison, and stack allocation of the key buffer.

#### Callers

- **Library consumers** - applications that look words up directly.
- **The spell-check engine** - will use a selection to check the words of the files it reads, in a later change.
- **The textlint tool** - does not call it in this release; the tool is unchanged.
