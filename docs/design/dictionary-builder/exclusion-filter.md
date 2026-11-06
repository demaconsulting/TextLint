## ExclusionFilter

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`ExclusionFilter` removes the words of the reviewed exclusions list from every dictionary, so that an excluded word
is never known, whichever list or dictionary an imported source put it in. The imported English lists are large and
faithful to their upstream sources, and some of their entries hide real typos: a misspelling that the source
contains, a fragment left by an expansion, or a rare real word that is almost always a mistyped common one. Editing
an imported list would be undone by the next refresh, so the removal is a rule applied when the dictionaries are
combined, driven by one reviewed list. The unit exists so that this rule is one pure, separately verifiable
function, and so that the reviewed list is checked for stale entries: an excluded word that no dictionary has is
reported, which keeps the list honest and short.

### Data Model

`ExclusionFilter` is an internal static class with no state. Its inputs are the words of each dictionary, in a
fixed order (each word folded, mapped to its capitalization-form flags), and the folded words of the exclusions
list.

The result type, `ExclusionFilterResult`, is documented here because it has no behavior of its own. It holds, in the
order of the input dictionaries, the words that remain in each dictionary (each with its flags), the number of
words removed from each dictionary, and the excluded words that no dictionary had, in ordinal order. The words are
copied into immutable frozen dictionaries and the lists into immutable arrays when the result is created, so later
changes to the collections the filter built the result from cannot alter it, and it cannot be modified through a
cast either.

### Key Methods

**Apply**: applies the exclusions.

- _Parameters_: the words of each dictionary and the excluded words.
- _Returns_: an `ExclusionFilterResult`.
- _Preconditions_: neither parameter is null; all words are in folded form.
- _Postconditions_: the inputs are not changed; each dictionary of the result holds exactly the words of the
  matching input dictionary that are not excluded, with their flags unchanged; the removal counts and the unused
  words are consistent with that.

Each dictionary is copied without its excluded words, and the excluded words that were found are remembered. Because
words are folded, every letter case of an excluded word is removed together. An excluded word that was found in at
least one dictionary has done its work; the others are collected and sorted in ordinal order as the unused words.

### Error Handling

None. The unit is a total, pure function: an unusual input produces a result, and reporting a problem is the
caller's decision. In particular an excluded word that no dictionary has is returned in the result rather than
thrown, so that `BuilderProgram` can report it as a problem with its file. Null arguments are programming errors
and are not defended against.

### Dependencies

- **.NET BCL** - collections and sorting. The unit uses no other unit of the system.

### Callers

- **BuilderProgram** - applies the exclusions after the dictionaries have been read and combined.
