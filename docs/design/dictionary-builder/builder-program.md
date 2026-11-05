## BuilderProgram

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`BuilderProgram` is the entry point and orchestrator of the dictionary builder. Its single responsibility is
to turn a command line into a complete, all-or-nothing build: validate the arguments, have each declared
dictionary's word lists read, report every problem found, and only if there is none, serialize and write the
dictionary files. It owns the contract with the build (arguments, exit codes, and the error format) and none
of the knowledge of the word-list or dictionary-file formats.

### Data Model

`BuilderProgram` is an internal static class with no state. It defines three exit codes: 0 (success),
1 (invalid word lists or declarations), and 2 (invalid command line), and the extension of produced files,
`.tld`. The parsed command line is held in a private record with the absolute input folder, the absolute
output folder, and the list of dictionary names in command-line order.

### Key Methods

**Main**: the process entry point. It calls `Run` with the console streams and returns its result.

**Run**: performs a complete build.

- _Parameters_: the argument array, a writer for standard output, and a writer for standard error. Taking
  the writers as parameters lets tests run the builder in-process without redirecting the console.
- _Returns_: the exit code.
- _Preconditions_: none of the parameters is null.
- _Postconditions_: with exit code 0 one file exists in the output folder per declared name and every
  file holds the current content of its lists; with any other exit code no file was created or changed.

`Run` first parses the arguments. It then reads every declared dictionary through `WordListReader`,
collecting every problem into one list; it reports each folder under the input folder that holds word
lists but was not declared; and when the list of problems is still empty it serializes every dictionary
through `DictionaryWriter` in memory. Only after all of that does it either print the problems and return 1,
or write the files. A file is written only when no file of the same content exists, so the timestamp of an
unchanged dictionary is preserved for incremental builds.

**ParseArguments**: parses the command line into the private options record. The options `--input` and
`--output` each need a value that does not itself start with a hyphen and may appear once. Every other
argument is a dictionary name, which must consist of ASCII letters, digits, hyphens, and underscores and
must not repeat, so that a name can never contain a path separator and climb out of the input or output
folder. The input and output paths are made absolute here, once.

**ReadDictionaries**: reads the word lists of each declared dictionary. A missing dictionary folder is a
problem, and so is a dictionary whose lists were readable but contain no words.

**ReportUndeclaredFolders**: looks at every direct sub-folder of the input folder, in ordinal order, and
reports one that contains word-list files but is not declared, which catches a mistyped or forgotten name
that would otherwise silently drop a whole dictionary from the product.

**WriteFiles**: creates the output folder, then writes each file unless identical content is already
there, and prints one summary line per file.

### Error Handling

Problems with the word lists or declarations are data, not exceptions: they are collected as diagnostics and
printed to standard error in the compiler's format, `path(line): error: message` (without the line part when a
problem has no line), after which the run returns 1. A command-line problem prints `DictionaryBuilder: error:`
followed by the reason and the usage text and returns 2. A dictionary that is larger than the format allows
(a defensive case that no realistic list reaches) is reported as a problem for that dictionary. File system
failures while creating the output folder or writing a file are caught, reported as an error naming the output
folder, and return 1; a failure to read an input file is reported by `WordListReader` as a problem of that
file. Anything else is a defect and propagates as an unhandled exception, which also fails the build.

### Dependencies

- **WordListReader** - reads and validates the word lists of one dictionary, and finds word-list files.
- **DictionaryWriter** - serializes the words of one dictionary.
- **.NET BCL** - file system access, text writers, and collections.

### Callers

N/A - entry point, called by the build of the library (through the `dotnet` host) and by tests.
