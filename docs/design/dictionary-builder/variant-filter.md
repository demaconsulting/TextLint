## VariantFilter

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`VariantFilter` applies the spelling-variant rule when the technical word lists are combined. The language
setting (American or British English) must decide ordinary spelling variants such as `favorite` or `flavors`,
but the general technical lists imported from upstream contain many of them and would make them known in every
language. Programming keywords and API names, such as the CSS color names, are different: they are fixed by the
programming language and must stay known however the user spells English. The unit exists so that this rule is
one pure, separately verifiable function, applied at build time, instead of an edit of the imported lists, which
therefore stay faithful to their source.

### Data Model

`VariantFilter` is an internal static class with no state. Its inputs are four sets of folded words: the words of
the general technical lists, the words of the programming-language lists, the words of the reviewed keep list
(each mapped to its capitalization-form flags), and the set of variant words, which is the union of the words of
the American-only and the British-only dictionaries.

The result type, `VariantFilterResult`, is documented here because it has no behavior of its own. It holds the
technical words that remain (each with its flags), the number of variants kept because a language list has them,
the number kept only because the keep list has them, the number stripped, and the keep-list words that are not
spelling variants, in ordinal order. The words are copied into an immutable frozen dictionary and the list into an
immutable array when the result is created, so later changes to the collections the filter built the result from
cannot alter it, and it cannot be modified through a cast either.

### Key Methods

**Apply**: applies the rule.

- _Parameters_: the general words, the language words, the keep-list words, and the variant words.
- _Returns_: a `VariantFilterResult`.
- _Preconditions_: none of the parameters is null; all words are in folded form.
- _Postconditions_: the inputs are not changed; the remaining words contain every language word, every keep-list
  word, and every general word except those stripped.

Every language word is kept, whether or not it is a variant. Every keep-list word is kept; one that is not a
variant is also recorded for reporting, so that the keep list cannot silently collect entries that do nothing,
and one that is a variant and in no language list is counted as kept through the keep list. A general word is
stripped when it is a variant and neither a language list nor the keep list has it; every other general word
is kept. The capitalization-form flags of a kept word are the union of its flags in every list that has it,
so that no knowledge of how a word was written is lost. The variants kept through language lists are counted
as the language words that are variants.

### Error Handling

None. The unit is a total, pure function: an unusual input produces a result, and reporting a problem is the
caller's decision. In particular a keep-list word that is not a variant is returned in the result rather than
thrown, so that `BuilderProgram` can report it as a problem with its file. Null arguments are programming
errors and are not defended against.

### Dependencies

- **.NET BCL** - collections and sorting. The unit uses no other unit of the system.

### Callers

- **BuilderProgram** - applies the rule when it combines the technical dictionary.
