## YamlDotNet

This document describes the integration and usage design for the `YamlDotNet` OTS software item.

### Purpose

YamlDotNet is a YAML parser for .NET. It is chosen to read the `.textlint.yaml` settings file
because it exposes a low-level node model (mappings, sequences, and scalars) in which every node
and every syntax error carries a line and column, which is what positioned configuration errors
need, and because that node model does not rely on reflection and so stays compatible with trimming
and Native AOT. It is a runtime dependency of the library and flows to consumers of the package.

### Features Used

- `YamlStream.Load` to parse text into documents, with `YamlDocument.RootNode` for each document
- `YamlMappingNode`, `YamlSequenceNode`, and `YamlScalarNode` (including the scalar `Value` and `Style`)
  to read settings
- `YamlNode.Start` to obtain the 1-based line and column of each node
- `YamlException` (and its derived exceptions for syntax errors and unknown aliases) and its `Start`
  position to report parse failures
- Anchors and aliases, resolved by the parser

Deliberately not used: the reflection-based serializer and deserializer and the static-generation
context. They would make the library reflection-dependent, and they hide node positions.

### Integration Pattern

YamlDotNet is consumed as a package reference of the library. `SettingsLoader` is the only unit
that uses it: each load creates a new `YamlStream`, loads it from a string reader over the settings
text, and maps the resulting nodes onto settings by hand. No state is shared between loads and
nothing needs disposal or initialization. YamlDotNet types are confined to `SettingsLoader` and appear
in no public signature, so the package can be upgraded without changing the library's public API.
