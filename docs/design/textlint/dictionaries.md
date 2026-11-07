## Dictionaries

![Dictionaries Structure](DictionariesView.svg)

### Overview

The `Dictionaries` subsystem answers one question: is this word known? It holds the vocabulary the library
checks text against, which is shipped inside the library assembly as four binary dictionaries: the English
words that American and British English share, the American-only spellings, the British-only spellings,
and the technical terms (programming languages, tools, companies, file types, licenses, and font names). A caller selects
a language, one of `en`/`en-US` (American, the default), `en-GB` (British, with -ise spellings), or both, and
asks about words. A selection always includes the common English words and the technical terms.

The subsystem contains two units: `SpellingDictionary`, the public class that selects dictionaries and answers
lookups, and `DictionaryImage`, an internal class that holds one binary dictionary in memory, validates it, and
probes it. The subsystem also depends on `DictionaryFormat`, a unit of the `DictionaryBuilder` system whose source
file the library compiles; it defines the file layout, the case fold, and the hash.

The subsystem does no tokenization, no settings handling, and no console output. Deciding what a word is,
including what to do with punctuation, belongs to the tokenizer of a later change, and reading a language
from a settings file belongs to the settings schema, which gains the key with the spell-check engine. The
word lists are data owned by the `DictionaryBuilder` system, and this subsystem never reads them; it reads
only what the build produced from them.

### Interfaces

**SpellingDictionary.Default**: The default selection.

- _Type_: In-process .NET static property.
- _Role_: Provider.
- _Contract_: Returns the shared selection of American English plus the technical terms.
- _Constraints_: Never null; loads nothing until the first lookup.

**SpellingDictionary.SupportedLanguages**: The accepted language names.

- _Type_: In-process .NET static property.
- _Role_: Provider.
- _Contract_: A read-only list of the names `en`, `en-GB`, and `en-US`.
- _Constraints_: Fixed for the life of the process.

**SpellingDictionary.ForLanguages**: Selects dictionaries by language name.

- _Type_: In-process .NET static method.
- _Role_: Provider.
- _Contract_: Accepts one language name or several separated by commas, ignoring letter case and spaces around
  names, and returns the shared selection. `en` and `en-US` mean the same, repeated names change nothing, and
  equal selections return the same instance.
- _Constraints_: A null argument throws `ArgumentNullException`. An empty list, an empty name in a list, or a
  name that is not supported throws `ArgumentException` whose message names the offending value and lists the
  supported names.

**SpellingDictionary.IsKnown**: Looks a word up.

- _Type_: In-process .NET instance method taking a `ReadOnlySpan<char>`; a `string` converts implicitly.
- _Role_: Provider.
- _Contract_: Returns whether the word is known to the selection, ignoring letter case, treating the typographic
  apostrophe as the straight one, keeping accents significant, and looking other punctuation up as written.
  Empty text, text longer than 64 characters, and text with a character above Latin-1 are not known.
- _Constraints_: Does not allocate once the selection's dictionaries are loaded, and never throws for any text; the
  only exception is `InvalidDataException`
  for a missing or damaged embedded dictionary, which signals a defective build. Safe to call from any number
  of threads at once.

**Embedded dictionaries**: The data the subsystem consumes.

- _Type_: Four manifest resources of the library assembly, named `DemaConsulting.TextLint.Dictionaries.` followed
  by `en-common`, `en-US`, `en-GB`, or `technical` and the extension `.tld`.
- _Role_: Consumer.
- _Contract_: Each resource is a dictionary file in the layout defined by `DictionaryFormat` and produced by the
  `DictionaryBuilder` during the library build.
- _Constraints_: Each resource is read once, on first use, and then kept in memory.

### Design

`SpellingDictionary` owns the language rules and the composition of a selection. There are exactly three
selections (American, British, and both), each created once as a private static instance, so
`ForLanguages` is a parse that picks one of three shared objects and asking for the same selection in
any spelling returns the same instance. A selection is a list of lazily loaded `DictionaryImage` objects: the
common dictionary, the dictionaries of the selected languages, and the technical dictionary. Each image is held
in a process-wide lazy value, so the three selections share the loaded data, and the images of a selection
are loaded together on its first lookup, so that a damaged resource fails every lookup of the selection in the
same way instead of only the lookups that happen to reach it. The lazy values are thread-safe with execution and
publication guarantees, which makes the first load happen once however many threads ask at the same time, and
the outcome of a failed load is cached, so every caller sees the same error.

A lookup folds the word to its key once, with `DictionaryFormat`, on the stack, and computes its hash once.
It then asks each image of the selection in turn with the prepared key and hash. A word that cannot be a key
(empty, longer than 64 characters, or containing a character above Latin-1) is answered as not known before any
image is touched, which is how the subsystem satisfies the requirement that no text can make a lookup fail. The
fold is the one defined by `DictionaryFormat` and shared with the builder, so a dictionary built from a list and
a lookup of the same word can never disagree about case or the apostrophe.

`DictionaryImage` owns the file. Its constructor validates the file in one linear pass over bytes that were just read:
the identifier, the option flags and hash identifier the reader supports, the reserved bytes, the slot count being a
power of two, the load factor, the exact total length the header implies, the word block ending with a terminator, the
header's word count agreeing with the number of terminators in the block and with the number of occupied slots, and
every occupied slot pointing at the start of a word, with each word referenced by exactly one slot. Any violation is an
`InvalidDataException` that names the
dictionary. A lookup is then a hash-selected slot and a forward probe through a table of 32-bit slots, each holding the
offset of a word in the word block; every read goes through a bounds-checked span and `BinaryPrimitives`, and the probe
is capped at one cycle of the table, so a damaged file can only produce an exception at load and never an
out-of-range read or an endless loop.

<!-- cspell:ignore colourize licence practise unmaneuverable -->
<!-- textlint:ignore glamourize -->

**Regional spellings and the part of speech.** The data decides which dictionary a spelling belongs to, and the
lookup never looks at anything but the folded word, so four consequences follow and are deliberate. First, a
capitalized form cannot carry a word into the other region: the lists are routed by the lower-cased word, so a
name such as `Honor` sits in the same region as `honor`, and the shipped lists are checked for that. Second, a word
that SCOWL does not list but that is a regional SCOWL word with prefixes or suffixes (`unmaneuverable` is `un` plus
`maneuverable`) belongs to that word's region, so an imported list cannot let an American spelling into British
English through an affixed form. Third, British
English is -ise only: an -ize form whose -ise counterpart is a known British spelling (`colourize`, `glamourize`) is
in no British list, and a common -ize word that has such a counterpart is American. Fourth, a spelling check is
blind to the part of speech, so a word whose British form differs only by part of speech (the verb `license` and the
noun `licence`, `practice` and `practise`) is accepted in both forms by British English, because one of the two forms
is common to both dialects; that is inherent in a spelling check and is not a leak.

**Why a precomputed binary format.** The vocabulary is about two hundred thousand words, and a spell checker
looks up millions of words in a large repository. The alternatives were to load the words into a general-purpose set
at run time and to generate source code that initializes one. Building a set from text on every run costs
allocation and start-up time proportional to the size of the vocabulary: in the prior research (174 thousand
words) a precomputed index loaded in about 3 ms, against about 43 ms for a hash set and about 67 ms for a frozen
set, and allocated 3 to 4 MB against 14 to 32 MB. On a projected 3,000-file, 36 MB repository with 3.5 million
lookups on 16 cores that was about 19 ms of wall time against 55 ms and 78 ms. The implementation was re-measured
with the safe loading described below: first use of the combined selection, including the consistency pass over the
data, takes about 9 ms cold and allocates
about 3 MB (the one managed copy of the data), against about 31 ms and 23 MB to build a frozen set of the same
words, and a lookup takes about 160 ns without allocating, which is slower than a hash set's lookup but costs no
per-word memory. Generated source was rejected because compiling a source file of that size is slow, and
runtime parsing of the lists was rejected for the start-up cost. The accepted costs are a build step, about
3 MB of data in the assembly, and one copy of that data in memory. There is deliberately no requirement on speed;
the format is a design choice justified here, and no requirement depends on it.

**Why a managed array and no unsafe code.** A faster scheme would read the embedded resource in place through a
pointer into the loaded assembly image, which saves the one copy. That needs unsafe code (an unsafe-code compiler
option and pointer access to the resource stream), and the owner decided that safety wins over speed: the library
contains no unsafe code, no unsafe compiler option, and no analyzer suppression for it. Each resource is therefore
read once, completely, into a managed byte array, and all access goes through spans and `BinaryPrimitives` over
that array. The cost is about 3 MB of memory and a few milliseconds at first use, which no requirement is
sensitive to. As a side effect the design is independent of how the host maps assembly resources, so it needs no
special handling for single-file or Native AOT hosts, and an unloaded assembly can never leave a dangling pointer.

**Why a build step.** The binary dictionaries are produced from the plain-text word lists by the
`DictionaryBuilder` as part of the library build, not committed. The library project builds the builder first,
runs it incrementally (it runs again only when a word list or the builder changes), embeds the four files under the
stable resource names above, and compiles the builder's `DictionaryFormat` source file into the library. A
builder failure fails the library build with the builder's diagnostics. The mechanism is described in the
_TextLint_ design.
