## HelpCards Verification

This document provides the verification evidence for the DemaConsulting.HelpCards OTS software item.
Requirements for this OTS item are defined in the HelpCards OTS Software Requirements document.

### Required Functionality

HelpCards (`TextLint-OTS-HelpCards-Cli` and `TextLint-OTS-HelpCards-Testing`) shall answer the
help command of the `textlint` tool from the cards embedded in the tool assembly, and report card
defects and example results to the tests of the tool.

### Verification Approach

HelpCards is a runtime library of the tool with no self-validation mode, so it is verified by
transitive evidence from the tests of the tool, which use the real package and the cards embedded
in the tool assembly. The package is not mocked anywhere in the tool tests.

### Test Scenarios

#### HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero

**Scenario**: The help verb is answered in-process from the embedded cards.

**Expected**: Exit code 0, and the index lists the command of every topic.

**Requirement coverage**: `TextLint-OTS-HelpCards-Cli`.

#### TextLintTool_HelpTopics_HelpVerb_ListsTopics

**Scenario**: The built tool is run with the help verb.

**Expected**: Exit code 0, and the output lists the command of every topic.

**Requirement coverage**: `TextLint-OTS-HelpCards-Cli`.

#### TextLintTool_HelpTopics_SearchAndExample_AnswerFromShippedCards

**Scenario**: The built tool is run with `help --search install`, and with
`help getting-started --example ci-steps`.

**Expected**: Both exit with code 0; the search lists the getting-started topic, and the example
holds the `actions/setup-dotnet@v4` step and the run step of the tool.

**Requirement coverage**: `TextLint-OTS-HelpCards-Cli`.

#### HelpCommand_CreateOptions_ShippedCatalog_PassesValidation

**Scenario**: The validator checks the shipped catalog with warnings treated as errors.

**Expected**: No defect is reported.

**Requirement coverage**: `TextLint-OTS-HelpCards-Testing`.

#### TextLintTool_HelpExamples_AllCommandExamples_GiveStatedResults

**Scenario**: The example runner runs every command example of the cards against the built tool.

**Expected**: Every example gives the exit code and output that its card states.

**Requirement coverage**: `TextLint-OTS-HelpCards-Testing`.
