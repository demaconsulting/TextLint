# TextLint

## Verification Approach

System-level verification combines four kinds of evidence. Requirement evidence comes from package
inspection, assembly inspection, and end-to-end tests of the public API; project configuration
checks are design-constraint guards that are not linked to a requirement:

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
- **End-to-end tests**: tests in `TextLintTests.cs` drive the public API as a consuming
  application would. They write real settings files and directory trees into a per-test temporary
  directory, load the settings with `SettingsLoader`, select files with `FileSelector`, and assert
  on the loaded values, the diagnostics, and the selected paths. They cover both the Configuration
  and FileSelection subsystems together. The one test double is the recording directory used to prove
  that excluded directories are never enumerated; everything else, including YamlDotNet and
  Microsoft.Extensions.FileSystemGlobbing, is real. The dictionary tests use the real embedded
  dictionaries, and the data test reads the repository word lists, which the test project copies beside
  the test assembly, to check every listed word, in its original, lower, and upper case, against the
  public lookup for the language selection that includes its list.

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
its output folder. No services or special configuration are required. Building the library
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
key `words` is loaded; loading fails with an error that names `words` and the file, at line 2 column 1,
and no settings are returned. This scenario is tested by
`TextLint_Settings_UnsupportedKey_ReportsErrorNamingKey`.

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

**TextLint_Package_Packed_ContainsThirdPartyNotices**: FileAssert inspects the produced library package and finds
exactly one `THIRD-PARTY-NOTICES.txt` at its root whose text names the author of the English word lists, carries
the permission sentence of their license verbatim, and names the publisher of the technical word lists. This
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

**TextLint_Dictionary_EveryListedWord_IsKnown**: Every entry of every word list in the repository is looked up as
written, in lower case, and in upper case, in each language selection that must know the list: the common words
in the American and the British selection, the regional lists in their own selection, the programming-language
lists and the reviewed variant list in both single-language selections, and the general technical lists in the
combined selection. Every lookup is known, and the test fails for a word list in a folder it does not know, so a
new list cannot go unchecked. This scenario is tested by `TextLint_Dictionary_EveryListedWord_IsKnown`.

**TextLint_Dictionary_ParallelLookups_ReturnCorrectAnswers**: Twenty thousand lookups spread over the three
selections are made from many threads at once; every answer is correct. This scenario is tested by
`TextLint_Dictionary_ParallelLookups_ReturnCorrectAnswers`.
