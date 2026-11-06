### SettingsLoader

![Configuration Structure](ConfigurationView.svg)

#### Purpose

`SettingsLoader` reads a `.textlint.yaml` file and maps it onto `TextLintSettings`. Its single
responsibility is to turn file content into either settings or a complete list of positioned
errors, so that callers handle every configuration failure through one path. It also defines the
supporting result types it returns, which have no unit of their own: `SettingsLoadResult` and
`SettingsDiagnostic`.

#### Data Model

`SettingsLoader` is a static class with no state.

**SettingsLoadResult** is the outcome of a load. `Settings` is non-null exactly when `Diagnostics`
is empty (the constructor discards the settings when there are errors, which enforces the invariant
in one place); `Succeeded` reports whether settings are present. `Diagnostics` lists the errors in
source order and is a snapshot: the constructor copies the supplied list into an immutable array, so
neither the producer's list nor a cast of the exposed list to a mutable interface can change it.
Instances are immutable.

**SettingsDiagnostic** is one error. It carries `Message`, `FilePath` (the path supplied to the
loader, or null), and `Line` and `Column` (1-based, null when the problem has no position, such
as an unreadable file). Every diagnostic is an error: there is no warning severity, because a file
with any problem produces no settings. `ToString` formats the compiler style
`file(line,column): error: message`, omitting parts that are unknown. Instances are immutable, and
only the loader creates them.

The supported keys and the rule for each are:

| Key                   | Accepted value                                                | Absent or null  |
| :-------------------- | :------------------------------------------------------------ | :-------------- |
| `version`             | Any scalar, carried as text                                   | No version      |
| `files`               | A list of text values, each a valid glob pattern              | Empty list      |
| `ignorePaths`         | A list of text values, each a valid glob pattern              | Empty list      |
| `enableGlobDot`       | Unquoted `true` or `false` (lower, title, or upper case)      | `false`         |
| `language`            | Text naming supported languages (`en`, `en-US`, `en-GB`)      | `en`            |
| `words`               | A list of text values, each a usable word-list entry          | Empty list      |
| `ignoreWords`         | A list of text values, each a usable word-list entry          | Empty list      |
| `flagWords`           | A list of usable entries; suggestion forms are also allowed   | Empty list      |
| `minWordLength`       | An unquoted whole number of at least 1                        | 4               |
| `maxFileSize`         | A number with optional unit, as described below; 0 = no limit | No limit (0)    |
| `ignoreRegExpList`    | A list of text values, each a usable pattern or known name    | Empty list      |
| `patterns`            | A list of mappings with a `name` and a `pattern`              | Empty list      |
| `ignoreRandomStrings` | Unquoted `true` or `false` (lower, title, or upper case)      | `true`          |
| `minRandomLength`     | An unquoted whole number of at least 1                        | 40              |

#### Key Methods

**LoadAsync**: Reads a file and maps it.

- _Parameters_: `string path` - the settings file; `CancellationToken cancellationToken`.
- _Returns_: `Task<SettingsLoadResult>`.
- _Preconditions_: `path` is non-null and non-empty.
- _Postconditions_: the result holds settings or diagnostics, never both.

Steps: (1) reject a null or empty path; (2) read the whole file as text asynchronously, honoring
the cancellation token and a byte-order mark; (3) convert an input/output, access, or unsupported-path
failure into a single diagnostic that names the file and has no position; (4) otherwise pass the text
and the path to `Parse`.

**Parse** (internal, no file system): Maps text and a source name to a result. It exists so the core
of loading is testable without files and so the public surface stays minimal; a public text overload
would be a further feature with its own requirement.

Steps: (1) load the text with YamlDotNet into its node model, converting a syntax error into one
diagnostic at the position the parser reports (a syntax error stops parsing, so it is the only
diagnostic); (2) no document, as for an empty or comment-only file, yields default settings; more
than one document yields an error at the start of the second; (3) a null root yields defaults and a
root that is not a mapping yields an error at the root; (4) collect the names that the `patterns` key defines (see
`CollectPatternNames` below); (5) for each key and value of the root mapping,
in document order, apply the rule in the table above.

**CollectPatternNames** (private): Collects the names that the `patterns` setting defines, without reporting any
problem. It runs before the keys are applied because `ignoreRegExpList` may come before `patterns` in the file and
refers to the names defined in it, as in cspell.

- _Algorithm_: find the top-level `patterns` key; when its value is a list, take the `name` of every item that is a
  mapping and whose `name` is a text value, whatever else is wrong with the definition.
- _Postconditions_: the result is a set of names compared ignoring letter case. A definition that is itself in error
  (a bad pattern, for example) still contributes its name, so one mistake is reported once, at the definition, and not
  again as an unknown name at every use.

Mapping rules in detail:

- Keys are matched with exact (ordinal) comparison, so they are case-sensitive; `Files` is not `files`.
  A key that is not
  plain text is an error at the key.
- A key outside the supported set is an error at the key, `'caseSensitive' is not a supported setting.`,
  naming the key; its value, including any nested content, is not inspected.
- A value is null when it is an unquoted empty value, `~`, or `null`; null is treated as absent for
  every key, which keeps a commented-out list from breaking a file.
- A list value that is not a sequence is one error at the value. Each item that is not text is an
  error at the item. Each text item is checked with `GlobPatternExpander`; if the expander rejects it,
  the error carries the expander's message, which names the pattern and the reason, at the item's
  position. Every bad item is reported, not just the first.
- `enableGlobDot` accepts only the unquoted spellings `true`/`True`/`TRUE` and `false`/`False`/`FALSE`;
  quoted values and YAML 1.1 spellings such as `yes` are errors rather than guesses.
- `language` must be a scalar (any other shape is the error `Expected a text value for 'language'.`
  at the value). The text is passed to `SpellingDictionary.ForLanguages`, which accepts one name or a
  comma-separated list, in any letter case and with spaces around the names, and rejects an unsupported
  name or an empty name. Its `ArgumentException` becomes the diagnostic with the framework's appended
  parameter name removed, so the message is, for example,
  `The language 'fr' is not supported. Supported languages: en, en-GB, en-US.` The text is stored as
  written. A quoted empty string is text, not null, and is therefore reported as an empty language name.
- `words`, `ignoreWords`, and `flagWords` are lists handled like the glob lists, except that each text
  item is passed to `WordTokenizer.TryParseListEntry`, with suggestion forms allowed only for `flagWords`.
  A rejected entry is the error `Cannot use the '<key>' entry '<entry>': <problem>` at the item, where
  the problem is the tokenizer's reason: the entry is blank, has no word before its suggestions, uses the
  forbidden-word marker `!`, contains white space, contains a character that cannot be part of a word
  (such as `@`, or `:` in a list that does not take suggestions), or has no letter or digit. The entries
  are stored as written, so the spell checker interprets them again with the same rules.
- `minWordLength` must be an unquoted run of digits that fits a 32-bit integer and is at least 1; a sign,
  a fraction, quotes, or any other text is the error
  `Expected a whole number of at least 1 for 'minWordLength'.` at the value.
- `maxFileSize` is parsed by the size grammar below; text that does not match, a value that is not a
  scalar, a size that does not fit a 64-bit integer, or a positive size below one byte is the error
  `Expected a size such as 100000, 500K, or 1.5MB (units B, K, KB, M, MB, G, GB) or 0 for no limit,
  for 'maxFileSize'.` at the value.
- `ignoreRegExpList` is a list handled like the glob lists, except that each text item is checked as an entry of the
  pattern settings: an item that has the shape of a name (see _PatternCatalog_) must be a name that `patterns` defines,
  wherever in the file, or a predefined name, compared ignoring letter case; any other item is parsed by
  `PatternParser`, exactly as the spell checker will parse it. A rejected item is the error
  `Cannot use the 'ignoreRegExpList' entry '<entry>': <problem>` at the item, where the problem is the reason of the
  parser (a blank entry, an unsupported flag or construct, an invalid regular expression) or the sentence of
  `PatternCatalog.CheckReference` for a name that is not defined, which advises writing a literal word between slashes.
  The entries are stored as written.
- `patterns` is a list of mappings, each with a `name` and a `pattern`. A null value is an empty list. Every problem is
  reported at the node that holds it, so one pass finds them all: a value that is not a list is
  `Expected a list of patterns, each with a 'name' and a 'pattern'.` at the value, and an item that is not a mapping is
  `Expected a pattern with a 'name' and a 'pattern'.` at the item. In a definition, `description` is accepted and not
  kept; any other key is `'<key>' is not a supported key of a pattern; use 'name' and 'pattern'.` at the key; a missing
  key is `A pattern needs a 'name'.` or `A pattern needs a 'pattern'.` at the mapping. A `name` that is not text is
  `Expected a text value for 'name'.`; a name that does not have the shape of a name, is predefined, or repeats an
  earlier one (ignoring letter case) is `Cannot use the pattern name '<name>': <problem>`, using the sentences of
  `PatternCatalog.CheckDefinitionName` and `a pattern with this name is already defined.`, at the name. A `pattern` that
  is a list is rejected with advice to give each pattern its own name and list the names in `ignoreRegExpList`, because
  cspell allows a list and this design does not; one that is not text is `Expected a text value for 'pattern'.`; one
  that `PatternParser` rejects is `Cannot use the pattern '<name>' ('<expression>'): <problem>` at the expression. Only
  complete, valid definitions are kept, in the order written, with the name and the expression as written.
- `ignoreRandomStrings` is read exactly as `enableGlobDot` is; anything else is the error
  `Expected true or false for 'ignoreRandomStrings'.` at the value. `minRandomLength` is read exactly as `minWordLength`
  is; anything else is `Expected a whole number of at least 1 for 'minRandomLength'.` at the value.
- Errors are collected across the whole mapping. If any exist, no settings are returned.

**Size grammar** (`maxFileSize`): the leading run of digits and dots is the number, which must parse as a
plain decimal (a lone fraction such as `.5` is allowed; signs, exponents, and a second dot are not); the
rest of the text, with no white space allowed before it, is the unit, compared ignoring letter case:
none or `B` (1), `K` or `KB` (1024), `M` or `MB` (1024 squared), and `G` or `GB` (1024 cubed). The size
is the number times the unit, rounded down to whole bytes. Rounding down selects the same files as the
fractional size would, because a file has a whole number of bytes. A positive size that rounds down to 0
is rejected, because it would silently mean no limit, and `0` itself is the explicit no-limit value. A
size of `long.MaxValue` or more bytes is rejected as not representable; the largest accepted whole number
of gigabytes is 8589934591. Quoted values are accepted, because a size is text with a unit and quoting
carries no other meaning. Binary multiples are used because cspell uses them.

Settings are built by applying each key to the previous settings value with a `with` expression, so
the mapping is a sequence of small pure steps rather than mutation of a builder.

#### Error Handling

Content problems are converted to diagnostics and never thrown: YamlDotNet exceptions for syntax
errors (including duplicate keys, bad indentation, and unknown aliases), input/output and access
failures when reading the file, wrong types or shapes, unsupported keys, invalid glob patterns, an
unsupported language, an unusable word-list entry, an invalid minimum word length or file size, an unusable
`ignoreRegExpList` entry or `patterns` definition, and an invalid `ignoreRandomStrings` or `minRandomLength`.
The `ArgumentException` that `SpellingDictionary.ForLanguages` throws is caught and converted locally,
because it is the dictionary's way of describing a bad value, not a programming error of the loader. The
`InvalidPatternException` that `PatternParser.Parse` throws is caught and converted in the same way, using its
`Reason`, so the diagnostic can name the setting and the position instead of repeating the entry in the exception
message.
Only programming errors propagate: a null path throws `ArgumentNullException`, an empty path throws
`ArgumentException`, and a cancelled token throws `OperationCanceledException`. No logging is
performed; the caller decides how to present diagnostics. The loader is stateless and thread-safe.

#### Dependencies

- **YamlDotNet** - parses the text into a node model with source positions. Only the node model is used,
  never the reflection-based serializer, which keeps the library trim and AOT safe. No YamlDotNet type
  appears in any public signature. See _YamlDotNet Integration Design_.
- **TextLintSettings** - the value that is built.
- **GlobPatternExpander** (FileSelection subsystem) - validates glob patterns at load time. This is a
  documented dependency of the Configuration subsystem on the FileSelection subsystem.
- **SpellingDictionary** (Dictionaries subsystem) - validates the `language` value through its
  `ForLanguages` method and supplies the list of supported languages for the message. This is a
  documented dependency of the Configuration subsystem on the Dictionaries subsystem. No dictionary data
  is loaded by the call.
- **WordTokenizer** (TextScanning subsystem) - interprets word-list entries through its
  `TryParseListEntry` method, so the loader and the spell checker agree on which entries are usable. This
  is a documented dependency of the Configuration subsystem on the TextScanning subsystem.
- **PatternParser** and **PatternCatalog** (TextScanning subsystem) - validate the entries of `ignoreRegExpList` and the
  definitions of `patterns` at load time: `PatternParser.Parse` for each pattern, with the `Reason` of its
  `InvalidPatternException`, and `PatternCatalog.IsNameShaped`, `CheckReference`, and `CheckDefinitionName` for
  names. The loader never
  builds the regular expression of the settings for use, so the work is done again, with the same rules, when a checker
  is created. This is a further documented dependency of the Configuration subsystem on the TextScanning subsystem.
- **.NET BCL** - file reading, number parsing, and text handling.

The alternative of duplicating the language names, the entry rules, the pattern rules, and the glob rules in the loader
was rejected because the copies would drift, and a value accepted at load time could then be rejected by the spell
checker or the file selector.

#### Callers

- **Library consumers** - load a settings file and print any diagnostics.
- **The textlint tool** - will load the settings file and print diagnostics in a later change.
