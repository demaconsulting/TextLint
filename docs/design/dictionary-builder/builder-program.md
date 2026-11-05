## BuilderProgram

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`BuilderProgram` is the entry point and orchestrator of the dictionary builder. Its single responsibility is
to turn a command line into a complete, all-or-nothing build: validate the arguments, have each declared
dictionary's word lists read, report every problem found, and only if there is none, serialize and write the
dictionary files. It owns the contract with the build (arguments, exit codes, and the error format) and none
of the knowledge of the word-list or dictionary-file formats.

### Data Model

`BuilderProgram` is an internal static class with no state beyond one comparer. It defines four exit codes: 0
(success), 1 (invalid word lists or declarations, or files that cannot be written), 2 (invalid command line), and
3 (the output folder could not be left clean and consistent: a write failed and the file system also prevented
restoring the previous files, or a temporary file or backup could not be deleted), the extension of produced
files, `.tld`, and the names that give the technical dictionary's
folder its structure:
`general`, `language`, and `variant-keep.txt`. The one comparer, ordinal and ignoring case, is used for every
name the builder compares (dictionary names, the references to the technical and variant dictionaries, folder
names, the technical folder's sub-folders and keep list, and output file paths), because on a case-insensitive
file system `alpha` and `ALPHA` are one folder and one file. The parsed command line is held in a private record
with the
absolute input folder, the absolute output folder, the list of dictionary names in command-line order, the name
of the technical dictionary (or none), the names of the variant dictionaries, and the file operations to use. The
names and the technical and variant references are stored in immutable collections, in the spelling in which the
dictionaries were declared. A
dictionary that has been read is held in a private record of its name, the on-disk folder it was read from, its words,
and an optional detail text for the summary line. The listing of the input folder, made once per run, is held in a
private record of the folders and whether the listing failed.

A private nested class, `StagedFile`, keeps the paths of one file replacement together (the declared target, the
existing original as spelled on disk, a staged file beside the target, and a backup of the original) and remembers
how far the installation got, so that a
rollback can undo exactly what was done, or say precisely what it could not undo.

`FileOperations` (its own source file, part of this unit) is a small class with virtual methods for the file
system calls that tests cannot portably make fail: enumerating the files of a folder, enumerating the
sub-folders of a folder, enumerating the entries of the output folder, reading a file, moving a file, and deleting a
file. Production code uses its default
instance, which calls the file system directly; tests pass a subclass that throws on chosen paths.
`BuilderProgram.Run` and `WordListReader` accept it as an optional parameter.

### Key Methods

**Main**: the process entry point. It calls `Run` with the console streams and returns its result.

**Run**: performs a complete build.

- _Parameters_: the argument array, a writer for standard output, and a writer for standard error. Taking
  the writers as parameters lets tests run the builder in-process without redirecting the console.
- _Returns_: the exit code.
- _Preconditions_: none of the parameters is null.
- _Postconditions_: with exit code 0 one file exists in the output folder per declared name and every
  file holds the current content of its lists; with exit code 1 or 2 no dictionary file was created or
  changed and no temporary file remains; with exit code 3 the error stream names every file that may be
  inconsistent or was left behind.

`Run` first parses the arguments. It then lists the input folder once, reads every declared dictionary (resolving
each name against that listing), collecting every problem into one
list; it reports each folder in the same listing that holds word lists but was not declared; and when the
list of problems is still empty it serializes every dictionary through `DictionaryWriter` in memory. Only after
all of that does it either print the problems and return 1, or write the files.

**ParseArguments**: parses the command line into the private options record. The options `--input`, `--output`,
`--technical`, and `--variants` each need a value that is present, not empty or blank, and does not itself start
with a hyphen (an empty output folder, for example, would otherwise silently resolve to the working folder); the
first three may
appear once and `--variants` may repeat with different values. Every other argument is a dictionary name, which
must consist of ASCII letters, digits, hyphens, and underscores and must not repeat ignoring letter case, so that a
name can never contain a path separator and climb out of the input or output folder and two names can never be
one folder or file on a case-insensitive file system. The technical declaration is checked as a
whole: `--technical` and `--variants` must be given together, the technical dictionary must be a declared name
(matched ignoring case), and every variant dictionary must be a declared name other than the technical
dictionary, with no variant repeated. A reference that matched in another case is resolved to the declared
spelling, which is then the folder and file name used. The input and output
paths are made absolute here, once.

**ReadDictionaries**: reads the word lists of each declared dictionary. The folder of each declared name is found
by `ResolveFolder` (below), never by joining the name onto the input path. A dictionary folder that does not exist
is a problem ("does not exist"), an ambiguous one is a different problem, a folder that exists but cannot be
searched is another, and so is a dictionary whose lists were readable but contain no words. The ordinary
dictionaries are
read first, in declaration order, because the variant rule needs the words of the variant dictionaries; the
technical dictionary is then combined, and the results are returned in declaration order.

**ListInputFolders**: lists the folders directly beneath the input folder, once per run, in ordinal order. Only a
genuine "directory not found" gives an empty listing (every declared dictionary then reports its own missing
folder); any other failure is reported once, as "the input folder cannot be listed", and marks the listing as
failed so that no declared name is wrongly called missing.

**ResolveFolder**: resolves a declared name against that listing, ignoring letter case, and returns the folder's
real on-disk path, so a dictionary declared as `alpha` finds `ALPHA` on a case-sensitive file system just as it does
on a case-insensitive one. No match is a "does not exist" problem. More than one match (possible on a
case-sensitive file system, for example `alpha` and `Alpha`) is an ambiguity problem that names every candidate;
none is chosen, because reading the wrong folder would be a silent error.

**ReadTechnicalDictionary**: combines the technical dictionary. It searches the technical folder once and sorts
every list that was found into exactly one place by its first path segment, compared ignoring case: under
`general`, under `language`, or `variant-keep.txt` at the top. A list anywhere else is reported, so a list that
was found is always either read or reported and cannot be dropped. The on-disk spelling of each place is taken from
the listing, and when one place has more than one spelling (`general` and `General`) the technical dictionary is
reported as ambiguous, naming both, instead of merging them; diagnostics name the paths as spelled on disk. It has
`WordListReader` read the general lists,
the language lists, and the keep list; an empty general or language set is a problem. The keep list is optional
only in the sense that it may be absent from the listing: whether it exists is decided by that same listing and
never by a separate existence check, so a keep list that exists but cannot be read is a problem and can never
silently strip approved words. The three sets and the variant words go to `VariantFilter`. Each keep-list word
that the filter reports as not being a spelling variant becomes a problem naming the keep list and the word. The
summary text for the technical dictionary states how many variants were kept through language lists, kept
through the keep list, and stripped.

**ReportUndeclaredFolders**: looks at every folder of the same listing, in ordinal order, and reports one that
contains word-list files but is not declared (matched ignoring case), or that cannot be searched to find out, which
catches a mistyped or forgotten name that would otherwise silently drop a whole dictionary from the product.
Because it uses the same listing and the same comparison as `ResolveFolder`, a folder is undeclared exactly when no
declared name resolves to it, so the two can never disagree.

**WriteFiles**: writes the files all-or-nothing and reports every file it cannot clean up. It runs
**StageAndInstall**, then, whatever the outcome, deletes every staged file; a staged file that cannot be deleted is
not swallowed but reported by path in a fatal report, and the run returns exit code 3 whatever the earlier result
was. **StageAndInstall** lists the output folder once and resolves each output file against that listing, ignoring
letter case, exactly as input folders are resolved: one match is the existing output (under its on-disk spelling),
no match means a new file, and more than one match (possible on a case-sensitive file system, for example
`ALPHA.tld` and `alpha.tld`) is an ambiguity problem that names the candidates, reported with exit code 1 before
anything is written or even created. It then creates the output folder; refuses, before writing anything, an
existing path that is a folder (only to give a clear message; every other failure is reported by the operation that
meets it); learns whether the existing file has identical content by reading it, never by a separate
existence check, so a file that exists but cannot be read is a reported failure rather than an absent file; writes
each changed file to a staged file beside its target (a file whose content is already identical and that already has
the declared spelling is left alone, so its timestamp is preserved for incremental builds, while an identical file
under another spelling is replaced so that only the declared spelling remains); installs all staged files; and prints
one summary line per file.

**Install**: moves each existing original (the file found by the listing, whatever its spelling) aside to a backup
and the staged file into the declared target path; whether an original exists is learned from the move itself (a
missing source is the "no original" case). If any step
throws, the files touched so far are put back in reverse order (the new file deleted, the backup restored under the
original's own spelling) and the
failure is returned for reporting. Restoring a file can itself fail: the new file may not be deletable, or the
backup may not be movable back. Each such file is described in the compiler's error format, with the backup that
still holds its previous content, and the backup is never deleted. When any file could not be restored, `WriteFiles`
reports the fatal condition and returns exit code 3. On success the backups are deleted; a backup that cannot be
deleted is described and reported by `WriteFiles` in the same fatal report as a leftover staged file, because a file
left in the output folder must never be hidden behind the run's other result.

### Error Handling

Problems with the word lists or declarations are data, not exceptions: they are collected as diagnostics and
printed to standard error in the compiler's format, `path(line): error: message` (without the line part when a
problem has no line), after which the run returns 1. A folder that cannot be searched or listed, or a list that
cannot be read (access denied or an I/O error, including part-way through), is such a problem and is never mistaken
for an empty or absent one; only a genuine "directory not found" or "file not found" means absent. A
command-line problem prints `DictionaryBuilder: error:` followed by the reason and the usage text and returns 2. A
dictionary that is larger than the format allows (a defensive case that no realistic list reaches) is reported as
a problem for that dictionary. File system failures while creating the output folder, reading an existing output
file, writing a staged file, or
installing the files are caught, the previous state is restored, and the failure is reported as an error naming the
output folder with exit code 1; a failure to read an input file is reported by `WordListReader` as a problem of that
file. When the previous state cannot be restored, or a temporary file or backup cannot be deleted, the run reports
a fatal condition instead: the error stream names the output folder, every file left inconsistent or behind, and
the backup that holds each previous version, and the exit code is 3. No exception handler in the builder discards
an error: each one either converts the failure into a reported problem or describes what it could not undo.
Anything else is a defect and propagates as an unhandled exception, which also fails the build.

### Dependencies

- **WordListReader** - reads and validates the word lists of one dictionary or of an explicit list of files,
  and finds word-list files.
- **VariantFilter** - applies the spelling-variant rule to the technical lists.
- **DictionaryWriter** - serializes the words of one dictionary.
- **.NET BCL** - file system access, text writers, and collections.

### Callers

N/A - entry point, called by the build of the library (through the `dotnet` host) and by tests.
