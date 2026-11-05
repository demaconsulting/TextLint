## BuilderProgram

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`BuilderProgram` is the entry point and orchestrator of the dictionary builder. Its single responsibility is
to turn a command line into a complete, all-or-nothing build: validate the arguments, have each declared
dictionary's word lists read, report every problem found, and only if there is none, serialize and write the
dictionary files. It owns the contract with the build (arguments, exit codes, and the error format) and none
of the knowledge of the word-list or dictionary-file formats.

### Data Model

`BuilderProgram` is an internal static class with no state. It defines four exit codes: 0 (success),
1 (invalid word lists or declarations, or files that cannot be written), 2 (invalid command line), and 3
(a write failed and the file system also prevented restoring the previous files, so the output may be
inconsistent), the extension of produced files, `.tld`, and the names that give the technical dictionary's
folder its structure:
`general`, `language`, and `variant-keep.txt`. The parsed command line is held in a private record with the
absolute input folder, the absolute output folder, the list of dictionary names in command-line order, the name
of the technical dictionary (or none), the names of the variant dictionaries, and the file operations to use. A
dictionary that has been read is held in a private record of its name, its words, and an optional detail text
for the summary line.

A private nested class, `StagedFile`, keeps the three paths of one file replacement together (the target, a
staged file beside it, and a backup of the original) and remembers how far the installation got, so that a
rollback can undo exactly what was done, or say precisely what it could not undo.

`FileOperations` (its own source file, part of this unit) is a small class with virtual methods for the file
system calls that tests cannot portably make fail: enumerating the files of a folder, enumerating the
sub-folders of a folder, moving a file, and deleting a file. Production code uses its default instance, which
calls the file system directly; tests pass a subclass that throws on chosen paths. `BuilderProgram.Run` and
`WordListReader` accept it as an optional parameter.

### Key Methods

**Main**: the process entry point. It calls `Run` with the console streams and returns its result.

**Run**: performs a complete build.

- _Parameters_: the argument array, a writer for standard output, and a writer for standard error. Taking
  the writers as parameters lets tests run the builder in-process without redirecting the console.
- _Returns_: the exit code.
- _Preconditions_: none of the parameters is null.
- _Postconditions_: with exit code 0 one file exists in the output folder per declared name and every
  file holds the current content of its lists; with any other exit code no dictionary file was created or
  changed and no temporary file remains.

`Run` first parses the arguments. It then reads every declared dictionary, collecting every problem into one
list; it reports each folder under the input folder that holds word lists but was not declared; and when the
list of problems is still empty it serializes every dictionary through `DictionaryWriter` in memory. Only after
all of that does it either print the problems and return 1, or write the files.

**ParseArguments**: parses the command line into the private options record. The options `--input`, `--output`,
`--technical`, and `--variants` each need a value that is present, not empty or blank, and does not itself start
with a hyphen (an empty output folder, for example, would otherwise silently resolve to the working folder); the
first three may
appear once and `--variants` may repeat with different values. Every other argument is a dictionary name, which
must consist of ASCII letters, digits, hyphens, and underscores and must not repeat, so that a name can never
contain a path separator and climb out of the input or output folder. The technical declaration is checked as a
whole: `--technical` and `--variants` must be given together, the technical dictionary must be a declared name,
and every variant dictionary must be a declared name other than the technical dictionary. The input and output
paths are made absolute here, once.

**ReadDictionaries**: reads the word lists of each declared dictionary. A missing dictionary folder is a
problem, and so is a dictionary whose lists were readable but contain no words. The ordinary dictionaries are
read first, in declaration order, because the variant rule needs the words of the variant dictionaries; the
technical dictionary is then combined, and the results are returned in declaration order.

**ReadTechnicalDictionary**: combines the technical dictionary. It reports every word list in the technical
folder that is not under `general`, not under `language`, and not `variant-keep.txt`; has `WordListReader` read
the general lists, the language lists, and the keep list (an absent keep list is allowed, an empty general or
language set is a problem); and hands the three sets and the variant words to `VariantFilter`. Each keep-list word
that the filter reports as not being a spelling variant becomes a problem naming the keep list and the word. The
summary text for the technical dictionary states how many variants were kept through language lists, kept
through the keep list, and stripped.

**ReportUndeclaredFolders**: looks at every direct sub-folder of the input folder, in ordinal order, and
reports one that contains word-list files but is not declared, which catches a mistyped or forgotten name
that would otherwise silently drop a whole dictionary from the product.

**WriteFiles**: writes the files all-or-nothing. It creates the output folder; refuses, before writing anything,
a target path that is occupied by a folder; writes each changed file to a staged file beside its target (a file
whose content is already identical is left alone, so its timestamp is preserved for incremental builds);
installs all staged files; and prints one summary line per file.

**Install**: moves each existing target aside to a backup and the staged file into its place. If any step
throws, the files touched so far are put back in reverse order (the new file deleted, the backup restored) and the
failure is returned for reporting. Restoring a file can itself fail: the new file may not be deletable, or the
backup may not be movable back. Each such file is described in the compiler's error format, with the backup that
still holds its previous content, and the backup is never deleted. When any file could not be restored, `WriteFiles`
reports the fatal condition and returns exit code 3. On success the backups are deleted on a best-effort basis. The
staged files are always deleted afterwards, so no temporary file is left behind whatever happened.

### Error Handling

Problems with the word lists or declarations are data, not exceptions: they are collected as diagnostics and
printed to standard error in the compiler's format, `path(line): error: message` (without the line part when a
problem has no line), after which the run returns 1. A folder that cannot be searched or listed (access denied or
an I/O error, including part-way through) is such a problem and is never mistaken for an empty folder. A
command-line problem prints `DictionaryBuilder: error:` followed by the reason and the usage text and returns 2. A
dictionary that is larger than the format allows (a defensive case that no realistic list reaches) is reported as
a problem for that dictionary. File system failures while creating the output folder, writing a staged file, or
installing the files are caught, the previous state is restored, and the failure is reported as an error naming the
output folder with exit code 1; a failure to read an input file is reported by `WordListReader` as a problem of that
file. When the previous state cannot be restored, the run reports a fatal condition instead: the error stream names
the output folder, every file left inconsistent, and the backup that holds each previous version, and the exit code
is 3. Anything else is a defect and propagates as an unhandled exception, which also fails the build.

### Dependencies

- **WordListReader** - reads and validates the word lists of one dictionary or of an explicit list of files,
  and finds word-list files.
- **VariantFilter** - applies the spelling-variant rule to the technical lists.
- **DictionaryWriter** - serializes the words of one dictionary.
- **.NET BCL** - file system access, text writers, and collections.

### Callers

N/A - entry point, called by the build of the library (through the `dotnet` host) and by tests.
