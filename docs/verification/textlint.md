# TextLint

## Verification Approach

System-level verification combines five kinds of evidence. Requirement evidence comes from package
inspection, assembly inspection, end-to-end tests of the public API, and the cspell parity corpus; project
configuration checks are design-constraint guards that are not linked to a requirement:

- **Package inspection**: after packing the library, the build pipeline runs FileAssert
  (`.fileassert.yaml`, tag `packages`) against the real `DemaConsulting.TextLint.<version>.nupkg`
  and its `.snupkg`. The checks assert the package id, the `lib/net10.0` assembly and XML
  documentation, and the publisher, license, README, and icon metadata. This is the evidence for
  the NuGet package requirement, because it inspects the artifact that is actually published.
  A further check asserts that the package carries `THIRD-PARTY-NOTICES.txt` at its root with the
  notices of the embedded word lists; that is the evidence for the third-party notices requirement.
  Another asserts that the packaged assembly contains the four embedded dictionaries, which together
  with the assembly inspection below is the evidence for the self-contained lookup requirement.
  The library package id is a prefix of the tool package id, so the build asserts the library
  package after packing only the library and before packing the tool.
- **Project configuration** _(guard, not requirement evidence)_: FileAssert (tag `library-project`)
  checks that the library project file enables `IsAotCompatible` and `TreatWarningsAsErrors`,
  guarding the Trimming and Native AOT design constraint described in the system design.
- **Assembly inspection**: tests in `TextLintTests.cs` load the compiled `DemaConsulting.TextLint`
  assembly by name and inspect its identity, metadata, target framework, trim-compatibility marker,
  and referenced assemblies through reflection. A further guard, not linked to a requirement,
  confirms that no public signature exposes a type from YamlDotNet or
  Microsoft.Extensions.FileSystemGlobbing.
- **End-to-end tests**: tests in `TextLintTests.cs` and `TextLintSpellCheckTests.cs` drive the public API as a
  consuming application would. They write real settings files and directory trees into a per-test temporary
  directory, load the settings with `SettingsLoader`, select files with `FileSelector`, check text with
  `TextChecker` and files with `SpellChecker`, and assert on the loaded values, the diagnostics, the findings, and
  the selected paths. They cover the Configuration, FileSelection, Dictionaries, TextScanning, and SpellChecking
  subsystems together. The one test double is the recording directory used to prove
  that excluded directories are never enumerated; everything else, including YamlDotNet and
  Microsoft.Extensions.FileSystemGlobbing, is real. The dictionary tests use the real embedded
  dictionaries, and the data test reads the repository word lists, which the test project copies beside
  the test assembly, to check every listed word, in its original, lower, and upper case, against the
  public lookup for the language selection that includes its list. Directive prefixes in the spell-checking
  tests are assembled from pieces, so that the spell checker that checks this repository does not read them as
  directives of the test source.
- **cspell parity corpus**: the requirement to report the same words as cspell is verified against the real
  cspell, not against a description of it. The folder `test/DemaConsulting.TextLint.Tests/SpellChecking/Parity`
  holds corpora (text files built from invented words, so that the result does not depend on the dictionary of
  either tool), a settings file beside each corpus that both tools read, the output that cspell produced for each
  corpus (recorded by the script `record-cspell.ps1`, which is run by hand and not by the build, and which records
  the version of cspell it used), and a list of the differences that are deliberate, each with its reason.
  `CspellParityTests.cs` loads each settings file with `SettingsLoader`, checks the corpus with `TextChecker`, and
  requires the findings to equal the recorded ones except for exactly the listed differences. Because the list must
  match exactly, a difference that is fixed or newly introduced fails the test, so the documented differences from
  cspell cannot drift from the real ones. The corpora that exist only to show the differences are kept apart from
  the others, so that every other corpus must match without exceptions.

The trim and AOT analyzers run on every build with warnings treated as errors, so any trim- or
AOT-unsafe construct fails the build; the assembly-level test confirms the resulting assembly
carries the trim-compatibility marker. These checks verify analyzer configuration only and are not
linked to a requirement, because Native AOT support is a design constraint in this release and no
Native AOT consumer is published or run.

## Test Environment

Package and project checks run in the build job on Windows, Linux, and macOS, against the package
produced by `dotnet pack` in that job; FileAssert is the only tool required. Assembly and
end-to-end tests run under xUnit on .NET 10.0 on the same platforms. The test project references
the library project, so the library assembly is present beside the test assembly. End-to-end tests
need permission to create and delete files under the operating system's temporary directory; each
test uses its own uniquely named directory, so tests share no state and may run in parallel. The
dictionary tests need no files of their own except the word lists that the test project copies to
its output folder. The parity tests need the corpus files, the recorded cspell output, and the list of differences,
which the test project also copies to its output folder; cspell itself is needed only to record the output again,
never to run the tests. The unreadable-file test holds a file open with no sharing, which behaves the same way on
Windows, Linux, and macOS. No services or special configuration are required. Building the library
requires the DictionaryBuilder project, which the library project builds first; no other tool is needed.

## Acceptance Criteria

- All FileAssert package and project checks pass on every build.
- The library package contains exactly one `net10.0` assembly with its XML documentation, and its
  nuspec names the package `DemaConsulting.TextLint` with the publisher, MIT license, README, and
  icon metadata.
- A symbols package is produced alongside the library package.
- The library project enables `IsAotCompatible` and `TreatWarningsAsErrors` (design-constraint guard).
- All system-level tests pass on every supported platform with zero failures.
- The library assembly name equals the NuGet package name, targets .NET 10, carries the product and
  company metadata, declares itself trimmable, and does not reference the tool assembly.
- A settings file loads into the values it states, an empty file loads defaults, and settings can be
  built in code and select files.
- Every configuration error, including a missing file, malformed YAML, a wrongly typed value, an
  unsupported key, and an invalid glob pattern, is reported with its file and, where it has one, its
  line and column, and no settings are returned.
- Include and ignore patterns select the expected files case-sensitively, brace alternatives and bare
  names work at any depth, and excluded directories are not enumerated.
- Hidden entries are skipped by wildcards unless enabled and are selected when a pattern names them
  literally.
- Selected files are in ordinal order and use forward slashes.
- Unsupported glob patterns are rejected naming the pattern.
- The library package contains `THIRD-PARTY-NOTICES.txt` at its root with the notices of the embedded word
  lists, and its assembly contains the four embedded dictionaries.
- Known words are reported as known and unknown words as not known, whatever the letter case or the
  apostrophe, and unusual text (accents, other scripts, empty, blank, very long) is handled without error.
- American spellings are known by default and British spellings are not, the British selection reverses
  that, the combined selection knows both, and an unsupported language is rejected naming it.
- Technical terms, including a programming term in an American spelling, are known in every selection.
- The library assembly alone, loaded from its bytes with no dictionary files, configuration, or network, still looks
  words up correctly.
- The assembly embeds exactly the four dictionaries, each a dictionary file.
- Every word of every repository word list is known for the selection that includes its list.
- Lookups from many threads at once return correct answers.
- A text is checked without any file, and each unknown word is reported with its line, column, and the word;
  known words are not reported.
- Identifiers are split at case changes, digits, underscores, and punctuation and each word is checked; words
  shorter than the minimum length are not; an apostrophe stays inside a word; the possessive of a known word and a
  known word after a backslash escape are accepted.
- URLs, email addresses, hexadecimal values, UUIDs, hashes, and encoded data are not checked.
- Directives switch checking off for a block, a line, and the next line, and add words for the document, with every
  prefix and in any letter case; a directive that is not supported is reported at its position.
- The language, the word lists with their precedence and letter-case rules, the minimum word length, and the maximum
  file size act as configured, and an invalid value in a settings file is reported with its file, line, and column.
- Files are read as UTF-8 or UTF-16 and report the same positions; binary and too-large files are skipped and
  reported; an unreadable file is an error that does not stop the run.
- The results are identical and identically ordered at every degree of parallelism, an invalid degree is rejected,
  and a cancelled run throws.
- On every corpus the findings equal those that cspell recorded, except for exactly the documented differences.

## Test Scenarios

**TextLint_Package_Packed_ContainsNet10Library**: FileAssert inspects the produced library package
and finds exactly one `lib/net10.0/DemaConsulting.TextLint.dll` (and no other framework folder),
its XML documentation file, and the companion `.snupkg`. This scenario is tested by
`TextLint_Package_Packed_ContainsNet10Library`.

**TextLint_Package_Packed_HasPackageMetadata**: FileAssert inspects the nuspec inside the produced
library package and finds the id `DemaConsulting.TextLint`, the author, the MIT license expression,
the README and icon references, and the two runtime package dependencies (`YamlDotNet` and
`Microsoft.Extensions.FileSystemGlobbing`), with `README.md` and `Icon.png` present in the package. This
scenario is tested by `TextLint_Package_Packed_HasPackageMetadata`.

**TextLint_Project_Configured_EnablesTrimAotAnalyzersAsErrors**: FileAssert inspects the library
project file and finds `IsAotCompatible` and `TreatWarningsAsErrors` both set to `true`. This is a
design-constraint guard and is not linked to a requirement. This scenario is tested by
`TextLint_Project_Configured_EnablesTrimAotAnalyzersAsErrors`.

**TextLint_Assembly_Loaded_HasPackageName**: The library assembly is loaded by name and its simple
name is `DemaConsulting.TextLint`. This scenario also serves as the per-platform evidence that the
library builds and runs on Windows, Linux, and macOS. This scenario is tested by
`TextLint_Assembly_Loaded_HasPackageName`.

**TextLint_Assembly_Loaded_HasPackageMetadata**: The loaded assembly's product metadata is
`DemaConsulting.TextLint` and its company metadata is `DEMA Consulting`. This scenario is tested by
`TextLint_Assembly_Loaded_HasPackageMetadata`.

**TextLint_Assembly_Loaded_TargetsNet10**: The loaded assembly's target framework attribute names
.NET 10, proving the library supports the .NET 10 runtime. This scenario is tested by
`TextLint_Assembly_Loaded_TargetsNet10`.

**TextLint_Assembly_Loaded_IsMarkedTrimCompatible**: The loaded assembly carries the `IsTrimmable`
assembly metadata set to `True`, guarding the Trimming and Native AOT design constraint: the marker
is emitted only when the project enables `IsAotCompatible`. This is not linked to a requirement.
This scenario is tested by `TextLint_Assembly_Loaded_IsMarkedTrimCompatible`.

**TextLint_Assembly_Loaded_DoesNotReferenceTool**: The loaded assembly's referenced-assembly list
does not include `DemaConsulting.TextLint.Tool`, proving the library is usable without the tool
and that the dependency direction (tool depends on library) is not inverted. This scenario is
tested by `TextLint_Assembly_Loaded_DoesNotReferenceTool`.

**TextLint_Assembly_Loaded_PublicApiExposesNoThirdPartyTypes**: Every public type, property, method
parameter and return type, field, base type, and interface of the library is inspected, and none
belongs to the YamlDotNet or Microsoft.Extensions.FileSystemGlobbing namespaces. This is a guard
that keeps package upgrades from breaking consumers and is not linked to a requirement. This
scenario is tested by `TextLint_Assembly_Loaded_PublicApiExposesNoThirdPartyTypes`.

**TextLint_Settings_CspellStyleConfigFile_LoadsConfiguredValues**: A settings file written in the
cspell key style, with comments, a quoted version, a brace pattern, and a list of ignore patterns, is
loaded; every value is present in the settings and there are no diagnostics. This scenario is tested
by `TextLint_Settings_CspellStyleConfigFile_LoadsConfiguredValues`.

**TextLint_Settings_EmptyConfigFile_LoadsDefaultSettings**: An empty settings file is loaded; the
result succeeds with default settings. This scenario is tested by
`TextLint_Settings_EmptyConfigFile_LoadsDefaultSettings`.

**TextLint_Settings_ConstructedInCode_SelectsFiles**: Settings are built in code, with no file, and
used to select files from a directory tree; the matching file is selected. This scenario is tested by
`TextLint_Settings_ConstructedInCode_SelectsFiles`.

**TextLint_Settings_MalformedConfigFile_ReportsFileLineAndColumn**: A settings file with an unclosed
list is loaded; one error is reported naming the file, with a line and column. This scenario is tested
by `TextLint_Settings_MalformedConfigFile_ReportsFileLineAndColumn`.

**TextLint_Settings_WrongValueType_ReportsFileLineAndColumn**: A settings file whose `files` value is
text rather than a list is loaded; the error is at the value, line 2 column 8, and names the file. This
scenario is tested by `TextLint_Settings_WrongValueType_ReportsFileLineAndColumn`.

**TextLint_Settings_MissingConfigFile_ReportsFileError**: A settings path that does not exist is
loaded; no exception is thrown and one error names the file. This scenario is tested by
`TextLint_Settings_MissingConfigFile_ReportsFileError`.

**TextLint_Settings_UnsupportedKey_ReportsErrorNamingKey**: A settings file containing the unsupported
key `caseSensitive` is loaded; loading fails with an error that names `caseSensitive` and the file, at line 2
column 1, and no settings are returned. This scenario is tested by
`TextLint_Settings_UnsupportedKey_ReportsErrorNamingKey`.

**TextLint_Settings_SpellingKeysConfigFile_LoadsConfiguredValues**: A settings file that uses the six spelling
keys (`language`, `words`, `ignoreWords`, `flagWords`, `minWordLength`, and `maxFileSize` with a unit) is loaded;
every value is present in the settings. This scenario is tested by
`TextLint_Settings_SpellingKeysConfigFile_LoadsConfiguredValues`.

**TextLint_Settings_UnsupportedLanguage_ReportsFileLineAndColumn**: A settings file whose language is `fr` is
loaded; the error is at the value (line 1 column 11), names the file and the language, and no settings are
returned. This scenario is tested by `TextLint_Settings_UnsupportedLanguage_ReportsFileLineAndColumn`.

**TextLint_Settings_UnusableWordEntry_ReportsFileLineAndColumn**: A settings file whose `words` list has an entry
with white space is loaded; the error is at the entry (line 2 column 5) and says that the entry contains white
space. This scenario is tested by `TextLint_Settings_UnusableWordEntry_ReportsFileLineAndColumn`.

**TextLint_Settings_InvalidMaxFileSize_ReportsFileLineAndColumn**: A settings file whose `maxFileSize` is text that
is not a size is loaded; the error is at the value (line 1 column 14) and names the setting. This scenario is
tested by `TextLint_Settings_InvalidMaxFileSize_ReportsFileLineAndColumn`.

**TextLint_Settings_InvalidPattern_ReportsFileLineAndColumn**: A settings file whose second include
pattern uses a backslash is loaded; the error is at that list item, line 3 column 5, names the file,
and names the pattern. This scenario is tested by
`TextLint_Settings_InvalidPattern_ReportsFileLineAndColumn`.

**TextLint_Files_IncludePatterns_SelectsMatchingFiles**: An include pattern for markdown files is
applied to a tree holding markdown and C# files; only the markdown files are selected. This scenario is
tested by `TextLint_Files_IncludePatterns_SelectsMatchingFiles`.

**TextLint_Files_BraceAndBareNamePatterns_SelectAtAnyDepth**: A brace pattern and a bare file name are
applied to a tree with matching files at several depths; all matches at every depth are selected. This
scenario is tested by `TextLint_Files_BraceAndBareNamePatterns_SelectAtAnyDepth`.

**TextLint_Files_DifferentCase_IsNotSelected**: A file whose extension is upper case is not selected by
a lower-case pattern on any platform. This scenario is tested by `TextLint_Files_DifferentCase_IsNotSelected`.

**TextLint_Files_IgnorePatterns_ExcludesIgnoredFiles**: A bare directory name in the ignore patterns
excludes the files beneath it. This scenario is tested by
`TextLint_Files_IgnorePatterns_ExcludesIgnoredFiles`.

**TextLint_Files_IgnoredDirectory_IsNotEnumerated**: Files are selected through a recording directory
while a directory is ignored; the selection is correct and no directory beneath the ignored one is
ever enumerated. This scenario is tested by `TextLint_Files_IgnoredDirectory_IsNotEnumerated`.

**TextLint_Files_HiddenEntries_SkippedByDefault**: A wildcard pattern is applied to a tree with hidden
files and a hidden directory; none of them are selected. This scenario is tested by
`TextLint_Files_HiddenEntries_SkippedByDefault`.

**TextLint_Files_LiteralHiddenPattern_SelectsHiddenEntries**: A pattern that names `.github`
literally selects the workflow file beneath it, while a file in another hidden directory is not
selected. This scenario is tested by `TextLint_Files_LiteralHiddenPattern_SelectsHiddenEntries`.

**TextLint_Files_HiddenEntries_SelectedWhenEnabled**: With hidden-file matching enabled, the same
wildcard pattern selects the hidden files as well. This scenario is tested by
`TextLint_Files_HiddenEntries_SelectedWhenEnabled`.

**TextLint_Files_SelectedFiles_AreOrderedWithForwardSlashes**: Nested files created out of order are
returned in ordinal order with forward-slash relative paths. This scenario is tested by
`TextLint_Files_SelectedFiles_AreOrderedWithForwardSlashes`.

**TextLint_Files_NegatedPattern_ThrowsNamingPattern**: Selecting with a negated pattern throws an
exception whose message names the pattern. This scenario is tested by
`TextLint_Files_NegatedPattern_ThrowsNamingPattern`.

**TextLint_Files_BackslashPattern_ThrowsNamingPattern**: Selecting with a pattern that uses a backslash
throws an exception whose message names the pattern. This scenario is tested by
`TextLint_Files_BackslashPattern_ThrowsNamingPattern`.

**TextLint_Files_AbsolutePathPattern_ThrowsNamingPattern**: Selecting with the drive path `C:/docs/**`
throws an exception whose message names the pattern and says absolute paths are not supported. This scenario is
tested by `TextLint_Files_AbsolutePathPattern_ThrowsNamingPattern`.

**TextLint_Files_LeadingSlashPattern_AnchorsAtRoot**: A pattern `/README.md` selects the README in the root
directory and not the one in a subdirectory, showing that a leading slash means the root directory and not an
absolute path. This scenario is tested by `TextLint_Files_LeadingSlashPattern_AnchorsAtRoot`.

**TextLint_Settings_AbsolutePathPattern_ReportsFileLineAndColumn**: A settings file whose second include
pattern is the drive path `C:/docs/**` is loaded; the error is at line 3 column 5, names the file, and says
absolute paths are not supported. This scenario is tested by
`TextLint_Settings_AbsolutePathPattern_ReportsFileLineAndColumn`.

**TextLint_Settings_QuestionMarkPattern_ReportsErrorSuggestingStar**: A settings file whose pattern is
`docs/file?.md` is loaded; the error is at the item, names the pattern, and suggests using `*`. This scenario is
tested by `TextLint_Settings_QuestionMarkPattern_ReportsErrorSuggestingStar`.

**TextLint_CommandLinePaths_AbsolutePath_ChecksFileAndNamesItAbsolute**: A file with a misspelling is named on a
command line by its absolute path; the path is resolved, the selected file is checked, and the finding is
formatted under the absolute path. This scenario is tested by
`TextLint_CommandLinePaths_AbsolutePath_ChecksFileAndNamesItAbsolute`.

**TextLint_CommandLinePaths_RelativePath_ChecksFileAndNamesItRelative**: The same file is named by a relative
pattern; the finding is formatted under the relative path with forward slashes. This scenario is tested by
`TextLint_CommandLinePaths_RelativePath_ChecksFileAndNamesItRelative`.

**TextLint_Package_Packed_ContainsThirdPartyNotices**: FileAssert inspects the produced library package and finds
exactly one `THIRD-PARTY-NOTICES.txt` at its root whose text names the author of the English word lists, carries
the permission sentence of their license verbatim, names the publisher of the technical word lists, and names the
author of the cspell-lib rules from which the default ignore patterns are ported. This
scenario is tested by `TextLint_Package_Packed_ContainsThirdPartyNotices`.

**TextLint_Package_Packed_ContainsEmbeddedDictionaries**: FileAssert inspects the packaged library assembly and
finds the name of each of the four embedded dictionary resources in it, proving that the package, which is packed
from the build output without building again, carries the data. This scenario is tested by
`TextLint_Package_Packed_ContainsEmbeddedDictionaries`.

**TextLint_Dictionary_KnownWord_IsKnown**: The default selection is asked about an ordinary English word; the
word is known. This scenario is tested by `TextLint_Dictionary_KnownWord_IsKnown`.

**TextLint_Dictionary_UnknownWord_IsNotKnown**: The default selection is asked about a misspelling and a nonsense
word; neither is known. This scenario is tested by `TextLint_Dictionary_UnknownWord_IsNotKnown`.

**TextLint_Dictionary_DifferentCase_IsKnown**: A word is looked up in lower, capitalized, upper, and mixed case,
and a name that the word lists write with a capital is looked up in lower and upper case; all are known. This
scenario is tested by `TextLint_Dictionary_DifferentCase_IsKnown`.

**TextLint_Dictionary_TypographicApostrophe_IsKnown**: Contractions and possessives are looked up with the
straight and with the curly apostrophe; all are known. This scenario is tested by
`TextLint_Dictionary_TypographicApostrophe_IsKnown`.

**TextLint_Dictionary_UnusualText_IsHandledWithoutError**: An accented word is known in either case and in its
unaccented spelling, while a word with a wrong accent, text in another script, empty text, blank text, and a text of
ten thousand letters are not known, and no lookup throws. This scenario is tested by
`TextLint_Dictionary_UnusualText_IsHandledWithoutError`.

**TextLint_Dictionary_DefaultLanguage_AcceptsAmericanNotBritishSpelling**: The default selection, and the one
named `en`, know American spellings and not British ones, and are the same shared instance. This scenario is tested
by `TextLint_Dictionary_DefaultLanguage_AcceptsAmericanNotBritishSpelling`.

**TextLint_Dictionary_BritishLanguage_AcceptsBritishNotAmericanSpelling**: The `en-GB` selection knows British
spellings and not American ones. This scenario is tested by
`TextLint_Dictionary_BritishLanguage_AcceptsBritishNotAmericanSpelling`.

**TextLint_Dictionary_BothLanguages_AcceptBothSpellings**: The selection `en-US, en-GB`, written with a space,
knows both spellings. This scenario is tested by `TextLint_Dictionary_BothLanguages_AcceptBothSpellings`.

**TextLint_Dictionary_UnsupportedLanguage_ThrowsNamingLanguage**: Asking for `en-AU` throws an error that names
`en-AU` and lists the supported languages. This scenario is tested by
`TextLint_Dictionary_UnsupportedLanguage_ThrowsNamingLanguage`.

**TextLint_Dictionary_TechnicalTerm_IsKnownInEveryLanguage**: Technical terms of several kinds, including a
programming term in an American spelling that the word lists keep for every language, are known in the American,
British, and combined selections. This scenario is tested by
`TextLint_Dictionary_TechnicalTerm_IsKnownInEveryLanguage`.

**TextLint_Dictionary_LibraryAssemblyAlone_LooksUpWords**: The bytes of the library assembly, and nothing else, are
loaded into a separate load context, so no dictionary file, settings file, or other part of the library is available to
it; a British selection is requested and a known word, a British spelling, an American spelling, a technical term, and
an unknown word are looked up with the correct answers. This scenario is tested by
`TextLint_Dictionary_LibraryAssemblyAlone_LooksUpWords`.

**TextLint_Assembly_Loaded_EmbedsDictionaries**: The loaded library assembly's resources include exactly the four
dictionary resources under their stable names, and each starts with the dictionary file identifier. This scenario
is tested by `TextLint_Assembly_Loaded_EmbedsDictionaries`.

**TextLint_Dictionary_EveryListedWord_IsKnown**: Every entry of every word list in the repository, except the words
of the reviewed exclusions list (which are skipped explicitly and checked by the next scenario), is looked up as
written, in lower case, and in upper case, in each language selection that must know the list: the common words
in the American and the British selection, the regional lists in their own selection, the programming-language
lists and the reviewed variant list in both single-language selections, and the general technical lists in the
combined selection. Every lookup is known, and the test fails for a word list in a folder it does not know, so a
new list cannot go unchecked; the exclusions list itself is not a word list of any dictionary and is not looked
up. This scenario is tested by `TextLint_Dictionary_EveryListedWord_IsKnown`.

**TextLint_Dictionary_EveryExcludedWord_IsUnknown**: Every word of the reviewed exclusions list is looked up as
written, capitalized, and in upper case in the American, the British, and the combined selection; none is known,
and the list is not empty. This scenario is tested by `TextLint_Dictionary_EveryExcludedWord_IsUnknown`.

**TextLint_Dictionary_DerivedFormsAndRegionalSpellings_FollowTheLanguageSelection**: Derived forms that a base word
list lacks (`testability`, `reviewable`, `precomputed`, `unannotated`, `untestable`) are known in the American and
the British selection; British-only spellings (`catalogue`, `labelled`, `honoured`, `modelling`) are known only in
the British selection, and `favorite` only in the American one. This scenario is tested by
`TextLint_Dictionary_DerivedFormsAndRegionalSpellings_FollowTheLanguageSelection`.

<!-- cspell:ignore colourize glamourize colour organise analyse decolourize unmanoeuvrable manoeuvre -->

**TextLint_Dictionary_CuratedSpellingPairs_StayInTheirOwnRegion**: The British spellings of nine curated pairs
(`honour`, `colour`, `centre`, `catalogue`, `labelled`, `organise`, `analyse`, `modelling`, `judgement`) are looked up
as written, capitalized, and in upper case; each is known in the British and the combined selection but not in the
American one. The American spellings that are not programming terms (`honor`, `judgment`, `favorite`, `defense`,
`recognize`, `emphasize`) are known in the American and the combined selection but not in the British one. The
American spellings of the programming terms among the pairs (`color`, `center`, `catalog`, `labeled`, `modeling`,
`organize`, `analyze`) are deliberately known in every language through the shared technical dictionary, so for them
the absence from the British word lists is checked by the committed-word-lists scenario below instead. This scenario
is tested by
`TextLint_Dictionary_CuratedSpellingPairs_StayInTheirOwnRegion`.

**TextLint_Dictionary_IzeFormsWithAnIseCounterpart_AreUnknownToBritishEnglish**: -ize forms that have a British -ise
counterpart and are not programming terms (`colourize`, `glamourize`, `decolourize`, `recognize`, `emphasize`,
`organizational`, `organizationally`, and others) are unknown in the British selection, because British English here
is -ise only. This scenario is tested by
`TextLint_Dictionary_IzeFormsWithAnIseCounterpart_AreUnknownToBritishEnglish`.

**TextLint_Dictionary_AffixedRegionalWords_StayInTheirOwnRegion**: The word `unmaneuverable` (an American word with a
prefix, imported from a list that SCOWL does not cover) is in the American folder and in neither of the others, and
`unmanoeuvrable` is in the British folder only; both are looked up as written, capitalized, and in upper case, and
the American word is known in the American selection and not in the British one, the British word the reverse. This
scenario is tested by `TextLint_Dictionary_AffixedRegionalWords_StayInTheirOwnRegion`.

**TextLint_Dictionary_CommittedWordLists_HaveNoCrossRegionLeaks**: The words of every list in the common, American,
and British folders are compared in lower case, as the builder combines them: for each curated pair the American
spelling is in the American folder and in neither of the others, the British spelling is in the British folder and
in neither of the others, and the British folder holds no -ize form whose -ise counterpart is also there. The
definition of an -ize form is read from the routing script (`IZE_FORM` in `word_forms.py`, copied beside the test
assembly), so the test cannot drift from the definition the lists were routed with; the scenario also asserts that
the pattern matches the verb endings and the derived endings (`organizational`, `organizationally`). This
scenario is tested by `TextLint_Dictionary_CommittedWordLists_HaveNoCrossRegionLeaks`.

**TextLint_Dictionary_ParallelLookups_ReturnCorrectAnswers**: Twenty thousand lookups spread over the three
selections are made from many threads at once; every answer is correct. This scenario is tested by
`TextLint_Dictionary_ParallelLookups_ReturnCorrectAnswers`.

**TextLint_CheckText_UnknownWord_ReportsLineColumnAndWord**: A text with one misspelling on its second line is
checked; the one finding is an unknown word with the line, the column, and the word. This scenario is tested by
`TextLint_CheckText_UnknownWord_ReportsLineColumnAndWord`.

**TextLint_CheckText_KnownWords_ReportsNothing**: A sentence of ordinary English words is checked; nothing is
reported. This scenario is tested by `TextLint_CheckText_KnownWords_ReportsNothing`.

**TextLint_CheckText_CamelAndSnakeCase_ReportsEachWordSeparately**: An identifier made of a short word, a
capitalized unknown word, an underscore, and a second unknown word is checked; each unknown word is reported at its
own position. This scenario is tested by `TextLint_CheckText_CamelAndSnakeCase_ReportsEachWordSeparately`.

**TextLint_CheckText_ShortWords_AreNotChecked**: Two short unknown words and one unknown word of exactly four
letters are checked; only the word of four letters is reported. This scenario is tested by
`TextLint_CheckText_ShortWords_AreNotChecked`.

**TextLint_CheckText_ApostropheWords_StayWhole**: A contraction of known words and the possessive of an unknown
word are checked; the contraction passes and the possessive is reported as one word. This scenario is tested by
`TextLint_CheckText_ApostropheWords_StayWhole`.

**TextLint_CheckText_PossessiveOfKnownWord_IsAccepted**: Possessives of known words, with the straight and the
typographic apostrophe, are checked; nothing is reported. This scenario is tested by
`TextLint_CheckText_PossessiveOfKnownWord_IsAccepted`.

**TextLint_CheckText_WordAfterBackslashEscape_IsAccepted**: A string literal with a line-feed escape in front of a
known word is checked; nothing is reported. This scenario is tested by
`TextLint_CheckText_WordAfterBackslashEscape_IsAccepted`.

**TextLint_CheckText_UrlsAndEmails_AreNotChecked**: A URL and an email address made of unknown words are checked;
nothing is reported. This scenario is tested by `TextLint_CheckText_UrlsAndEmails_AreNotChecked`.

**TextLint_CheckText_HexUuidAndHashValues_AreNotChecked**: A C hexadecimal literal, a CSS color, a UUID, a commit
hash, and a digest are checked; nothing is reported. This scenario is tested by
`TextLint_CheckText_HexUuidAndHashValues_AreNotChecked`.

**TextLint_CheckText_EncodedData_IsNotChecked**: A line of Base64 data is checked; nothing is reported. This
scenario is tested by `TextLint_CheckText_EncodedData_IsNotChecked`.

**TextLint_CheckText_DisableEnableBlock_SkipsRegion**: A misspelling before, inside, and after a block that a
directive disables and enables is checked; only the words outside the block are reported. This scenario is tested
by `TextLint_CheckText_DisableEnableBlock_SkipsRegion`.

**TextLint_CheckText_DisableLine_SkipsLine**: A misspelling before a disable-line directive on its line, and one on
the next line, are checked; only the next line is reported. This scenario is tested by
`TextLint_CheckText_DisableLine_SkipsLine`.

**TextLint_CheckText_DisableNextLine_SkipsNextLine**: A disable-next-line directive followed by two misspelled lines
is checked; only the second line is reported. This scenario is tested by
`TextLint_CheckText_DisableNextLine_SkipsNextLine`.

**TextLint_CheckText_IgnoreAndWordsDirectives_AddDocumentWords**: An ignore directive and a words directive each add
one word, and a third word is not added; only the third word is reported. This scenario is tested by
`TextLint_CheckText_IgnoreAndWordsDirectives_AddDocumentWords`.

**TextLint_CheckText_UnterminatedDisable_RunsToEndOfText**: A visible misspelling is followed by a disable directive
that is never closed; only the visible one is reported. This scenario is tested by
`TextLint_CheckText_UnterminatedDisable_RunsToEndOfText`.

**TextLint_CheckText_AllDirectivePrefixes_AreAccepted**: For each of the five prefixes, and for upper-case spellings
of two of them, a text whose first line is hidden by a disable-line directive and whose second line is not is
checked; only the second line is reported. This scenario is tested by
`TextLint_CheckText_AllDirectivePrefixes_AreAccepted`.

**TextLint_CheckText_UnsupportedDirective_ReportsPosition**: A locale directive is checked; the finding is an
unsupported directive named `locale` at the position of its name. This scenario is tested by
`TextLint_CheckText_UnsupportedDirective_ReportsPosition`.

**TextLint_SpellCheck_BritishLanguage_AcceptsBritishNotAmericanSpelling**: A file that uses an American and a British
spelling of one word is checked with the default language and with British English; each language reports the other's
spelling only. This scenario is tested by `TextLint_SpellCheck_BritishLanguage_AcceptsBritishNotAmericanSpelling`.

**TextLint_CheckText_WordsSetting_AcceptsAnyCase**: An unknown word written in lower, upper, and capitalized form is
checked with and without the word in the `words` setting; with it nothing is reported and without it all three are.
This scenario is tested by `TextLint_CheckText_WordsSetting_AcceptsAnyCase`.

**TextLint_CheckText_FlagWords_ReportsFlaggedKind**: A known word that a `flagWords` entry flags, with two
suggestions, is checked; the finding is a flagged word with the suggestions. This scenario is tested by
`TextLint_CheckText_FlagWords_ReportsFlaggedKind`.

**TextLint_CheckText_FlagWordCapitalEntry_MatchesExactCase**: A `flagWords` entry written with a capital letter is
checked against three letter cases of the word; only the exact spelling is flagged. This scenario is tested by
`TextLint_CheckText_FlagWordCapitalEntry_MatchesExactCase`.

**TextLint_CheckText_IgnoreWordsOverrideFlagWords_ReportsNothing**: A word that is both ignored and flagged is
checked; nothing is reported. This scenario is tested by
`TextLint_CheckText_IgnoreWordsOverrideFlagWords_ReportsNothing`.

**TextLint_CheckText_WordWithDigitsOrPunctuation_MatchesWholeToken**: An entry with a digit and a hyphen is used as
a whole token in two letter cases and its last part is used alone; the whole tokens are accepted and the part alone is
reported. This scenario is tested by `TextLint_CheckText_WordWithDigitsOrPunctuation_MatchesWholeToken`.

**TextLint_CheckText_MinWordLengthRaised_SkipsShorterWords**: An unknown word of five letters and one of seven are
checked with a minimum of six; only the longer word is reported. This scenario is tested by
`TextLint_CheckText_MinWordLengthRaised_SkipsShorterWords`.

**TextLint_CheckText_MinWordLengthLowered_ChecksShorterWords**: An unknown word of two letters is checked with a
minimum of two and with the default; it is reported only with the lowered minimum. This scenario is tested by
`TextLint_CheckText_MinWordLengthLowered_ChecksShorterWords`.

**TextLint_SpellCheck_FilesWithUnknownWords_ReportsFileLineColumnAndWord**: Two text files, one in a subdirectory, hold
misspellings and a file that the patterns do not select holds another; the findings name the two files in path order,
with line, column, and word, and the third file is not checked. This scenario is tested by
`TextLint_SpellCheck_FilesWithUnknownWords_ReportsFileLineColumnAndWord`.

**TextLint_SpellCheck_CleanFiles_ReportsNothing**: Files with only ordinary words are checked; there are no
findings, no file diagnostics, and the run has no problems. This scenario is tested by
`TextLint_SpellCheck_CleanFiles_ReportsNothing`.

**TextLint_SpellCheck_Utf8Utf16AndBom_ReportSamePositions**: The same text is written as UTF-8, UTF-8 with a byte
order mark, and UTF-16 in both byte orders with a mark; every file reports the same word at the same position and none
is skipped. This scenario is tested by `TextLint_SpellCheck_Utf8Utf16AndBom_ReportSamePositions`.

**TextLint_SpellCheck_BinaryFile_IsSkippedAndReported**: A file with a NUL character and a text file with a
misspelling are checked; the binary file is reported as skipped (not an error) and the text file is checked. This
scenario is tested by `TextLint_SpellCheck_BinaryFile_IsSkippedAndReported`.

**TextLint_SpellCheck_UnreadableFile_IsReportedAndRunContinues**: A file that another stream holds with no sharing
and a readable file with a misspelling are checked; the locked file is an error diagnostic, the other file is checked,
and the run has problems. This scenario is tested by `TextLint_SpellCheck_UnreadableFile_IsReportedAndRunContinues`.

**TextLint_SpellCheck_FileOverMaxSize_IsSkippedAndReported**: A file larger than the configured size limit and a
small file are checked; the large file is reported as too large and the small file is checked. This scenario is tested
by `TextLint_SpellCheck_FileOverMaxSize_IsSkippedAndReported`.

**TextLint_SpellCheck_DifferentConcurrency_ReturnsIdenticalOrderedResults**: Forty files spread over directories are
checked with one worker and with eight; the findings are identical and ordered by file path. This scenario is tested
by `TextLint_SpellCheck_DifferentConcurrency_ReturnsIdenticalOrderedResults`.

**TextLint_SpellCheck_InvalidConcurrency_Throws**: A degree of parallelism of zero is rejected when the options are
created. This scenario is tested by `TextLint_SpellCheck_InvalidConcurrency_Throws`.

**TextLint_SpellCheck_CancelledToken_ThrowsOperationCanceled**: A run is started with a token that is already
cancelled; it throws an operation-cancelled exception. This scenario is tested by
`TextLint_SpellCheck_CancelledToken_ThrowsOperationCanceled`.

**TextLint_CspellParity_TokenizationCorpus_MatchesRecordedCspellOutput**: The corpus of word splitting, ignored text,
escapes, scripts, and positions is checked with the settings stored beside it; the findings equal the output that
the real cspell produced for the same files, with no listed differences. This scenario is tested by
`TextLint_CspellParity_TokenizationCorpus_MatchesRecordedCspellOutput`.

**TextLint_CspellParity_DirectiveCorpora_MatchRecordedCspellOutput**: The corpus of every supported directive and
the corpus of a disable that is never enabled are checked; the findings equal cspell's recorded output. This scenario
is tested by `TextLint_CspellParity_DirectiveCorpora_MatchRecordedCspellOutput`.

**TextLint_CspellParity_CrlfDirectiveCorpus_MatchesRecordedCspellOutput**: The two directive corpora are checked with
every line ending turned into a carriage return and a line feed; the findings equal the output that cspell produced
for those line endings, which differs from the output for line feeds in the scope of a next-line directive. This
scenario is tested by `TextLint_CspellParity_CrlfDirectiveCorpus_MatchesRecordedCspellOutput`.

**TextLint_CspellParity_WordListCorpus_MatchesRecordedCspellOutput**: The corpus of accepted, ignored, and flagged
words, letter cases, and entries with digits and punctuation is checked with the word lists stored beside it; the
findings equal cspell's recorded output. This scenario is tested by
`TextLint_CspellParity_WordListCorpus_MatchesRecordedCspellOutput`.

**TextLint_CspellParity_MinimumLengthCorpora_MatchRecordedCspellOutput**: The corpora with a minimum word length of
six and of two are checked; the findings equal cspell's recorded output. This scenario is tested by
`TextLint_CspellParity_MinimumLengthCorpora_MatchRecordedCspellOutput`.

**TextLint_CspellParity_PossessiveCorpus_MatchesRecordedCspellOutput**: The corpus of possessives, contractions, and
apostrophes at the edges of words is checked; the findings equal cspell's recorded output. This scenario is tested by
`TextLint_CspellParity_PossessiveCorpus_MatchesRecordedCspellOutput`.

**TextLint_CspellParity_KnownDivergences_AreExactlyTheDocumentedOnes**: The corpora that exist only to show the
deliberate differences (the heuristic splitter and dictionaries, the unsupported directives, and the directive names
that cspell's range patterns and its directive reader end differently) are checked; the findings that only TextLint
reports and the findings that only cspell reports are exactly the ones listed with their reasons in the divergences
file, every listed difference belongs to a corpus, and every corpus has a settings file and recorded output. This
scenario is tested by `TextLint_CspellParity_KnownDivergences_AreExactlyTheDocumentedOnes`.
