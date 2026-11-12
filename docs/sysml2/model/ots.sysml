package TextLint {
    private import SoftwareItemMetadata::*;

    // Runtime OTS (off-the-shelf) dependencies of the TextLint library. Both ship as package
    // dependencies, so they are modeled here; build-time and pipeline tooling is not modeled.

    part def YamlDotNet {
        doc /* YAML parsing library; its node model with source positions is used to read .textlint.yaml. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/yamldotnet.md");
            verificationFiles = ("docs/verification/ots/yamldotnet.md");
            requirementFiles = ("docs/reqstream/ots/yamldotnet.yaml");
        }
    }

    part def FileSystemGlobbing {
        doc /* Microsoft.Extensions.FileSystemGlobbing: matches include and exclude glob patterns against a directory tree. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/filesystemglobbing.md");
            verificationFiles = ("docs/verification/ots/filesystemglobbing.md");
            requirementFiles = ("docs/reqstream/ots/filesystemglobbing.yaml");
        }
    }
}
