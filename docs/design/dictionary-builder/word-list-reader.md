## WordListReader

![DictionaryBuilder Structure](DictionaryBuilderView.svg)

### Purpose

`WordListReader` reads the plain-text word lists of one dictionary, validates every entry, and merges the
entries into one set of distinct, case-folded words. It exists because word lists are edited by hand and
imported from third-party sources, so the builder cannot trust them: one strict rule applied in one place,
with every violation reported by file and line, is what stands between a stray character and a wrong
dictionary in the product. It also defines which files belong to a dictionary.

### Data Model

`WordListReader` is an internal static class with no state. Its results are carried by two supporting types
that are documented here because they have no behavior of their own:

- **WordListReadResult** holds the merged words (each distinct folded word mapped to the capitalization-form
  flags OR-ed over every spelling it had in the lists), the diagnostics found, the total number of problems, and
  the number of files read. The diagnostics are copied into an immutable array and the words into an immutable
  frozen dictionary when the result is created, so later changes to the collections the reader built the result
  from cannot alter it, and the result cannot be modified through a cast either.
- **WordListDiagnostic** holds the path, the 1-based line (zero for a problem about a whole file or folder),
  and the message of one problem, and formats itself as `path(line): error: message` or
  `path: error: message`. The builder's other units reuse it for folder-level problems.

The entry rule is: 1 to 64 characters; only ASCII letters, Latin-1 letters (U+00C0 to U+00FF except the
multiplication and division signs), and the straight apostrophe; the first and last characters are letters; no
two apostrophes in a row. The rule is applied after trimming spaces, tabs, and carriage returns, and after
skipping blank lines and lines whose first non-blank character is `#`.

### Key Methods

**FindWordListFiles**: lists the files of a dictionary folder.

- _Parameters_: the dictionary folder, an output for the problem found while searching, an output that says
  whether the folder is missing, and optionally the file operations to use.
- _Returns_: the full paths of the files beneath the folder (at any depth) whose extension is `.txt` in any letter
  case, sorted in ordinal order; empty when the folder does not exist, holds none, or could not be searched.
- _Postconditions_: the extension test ignores case, like every other file-name comparison of the builder, so a
  list named `LIST.TXT` is never silently skipped on a case-insensitive file system and the same files are found
  on every operating system; the sort is ordinal, so they are found in the same order everywhere. There is
  deliberately no existence check before the search, because such a check cannot tell a missing folder from one
  that is unreadable. Only a genuine "directory not found" condition sets the missing output and yields no
  problem. Any other failure (access denied or an I/O error, possibly part-way through the lazy enumeration)
  yields no files and sets the problem output, so a failed search can never be mistaken for an empty or absent
  folder; the callers add that problem to their diagnostics.

**Read**: reads and merges the files of a dictionary folder.

- _Parameters_: the dictionary folder and optionally the file operations to use.
- _Returns_: a `WordListReadResult`.
- _Postconditions_: every problem in every file has been counted; the first 100 are kept in detail, followed by
  one summary diagnostic that states how many more were not shown, so a hopelessly broken list cannot flood the
  build log. A folder that could not be searched is the first problem of the result; a folder that does not exist
  gives an empty result without a problem.

**ReadFiles**: the same merge over an explicit list of files, used when the files of one dictionary come from
several places (the technical dictionary reads its general lists, its language lists, and its keep list
separately). `Read` is `FindWordListFiles` followed by `ReadFiles`, so the caps and the diagnostics behave
identically. A file that is listed but cannot be read is a problem naming the file, never an absent file; the file
reads go through the file operations too, so tests can make them fail.

Each file is decoded strictly as UTF-8, so a file saved in another encoding is reported as one file-level
problem instead of being misread; a byte order mark at the start is skipped. Lines are separated at the line
feed and counted from one so that the reported line matches what an editor shows. A valid entry is folded
character by character with `DictionaryFormat.Fold` into a small array, and is merged into the result
under its folded key; the capitalization form of the spelling as written is classified by
`DictionaryFormat.ClassifyCase` and OR-ed into the key's flags. Merging by folded key is what makes the
dictionary case-insensitive and free of duplicates across case, across lines, and across files. Because the
result is a set and the writer imposes the final order, the order in which files are read never changes the
output.

### Error Handling

Invalid entries, files that are not valid UTF-8, files that cannot be read, and folders that cannot be searched
(each an I/O or access error) are collected as diagnostics and never thrown, so the caller can report all of them
at once. A missing folder is
not a problem for the reader: it simply has no files, and the caller decides that this is an error. Null
arguments are programming errors and are not defended against.

### Dependencies

- **DictionaryFormat** - the alphabet test, the fold, the maximum word length, and the capitalization
  classification.
- **.NET BCL** - file system access, strict UTF-8 decoding, and collections. The folder search goes through
  `FileOperations` (part of the _BuilderProgram_ unit), so tests can make the search and the file reads fail.

### Callers

- **BuilderProgram** - reads each declared dictionary, reads the three sources of the technical dictionary, and
  uses `FindWordListFiles` to detect folders of word lists that were not declared and misplaced technical lists.
