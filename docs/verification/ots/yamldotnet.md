## YamlDotNet Verification

This document provides the verification evidence for the YamlDotNet OTS software item. Requirements
for this OTS item are defined in the YamlDotNet OTS Software Requirements document.

### Required Functionality

YamlDotNet parses the `.textlint.yaml` settings file into a node model of mappings, sequences, and
scalars, resolves anchors and aliases, and reports the line and column of every node and of every
syntax error. The settings loader depends on all of this to map settings and to report positioned
configuration errors.

### Verification Approach

YamlDotNet is a runtime dependency on the critical path of every settings load, so it is verified by
transitive evidence from the library test suite rather than by a dedicated OTS test project: the
settings loader unit tests parse real YAML text with the real parser, and each scenario below names a
specific test whose outcome can only be correct if the parser behaves as required. A passing run of those
tests on every supported platform is the evidence that both requirements are satisfied. YamlDotNet is not
mocked anywhere in the library tests.

### Test Scenarios

#### SettingsLoader_Parse_AllSupportedKeys_MapsEveryValue

**Scenario**: YAML text using plain and quoted scalars, block lists, and a boolean is parsed and mapped.

**Expected**: Every value reaches the settings, proving the node model presents mappings, sequences, and
scalars correctly.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-ParseYaml`.

#### SettingsLoader_Parse_FlowAndBlockLists_BothMap

**Scenario**: One list is written in flow style and one in block style.

**Expected**: Both parse into sequences with their items in order.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-ParseYaml`.

#### SettingsLoader_Parse_AnchorsAndAliases_AreResolved

**Scenario**: A value is anchored and reused through an alias.

**Expected**: The alias resolves to the anchored value.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-ParseYaml`.

#### SettingsLoader_Parse_MalformedYaml_ReportsLineAndColumn

**Scenario**: A flow list that is never closed is parsed.

**Expected**: The parser fails with a position inside the text, which the loader reports as a line and column.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-SourcePositions`.

#### SettingsLoader_Parse_FilesNotAList_ReportsErrorAtValue

**Scenario**: A value of the wrong type is parsed on a known line.

**Expected**: The node carries the exact line and column of the value, 2 and 8.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-SourcePositions`.

#### SettingsLoader_Parse_DuplicateKey_ReportsErrorAtDuplicate

**Scenario**: A key is given twice.

**Expected**: The parser fails at the position of the second occurrence.

**Requirement coverage**: `TextLint-OTS-YamlDotNet-SourcePositions`.
