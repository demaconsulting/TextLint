package TextLint {
    // Runtime OTS (off-the-shelf) dependencies of the TextLint library. Both ship as package
    // dependencies, so they are modeled here; build-time and pipeline tooling is not modeled.

    part def YamlDotNet {
        doc /* YAML parsing library; its node model with source positions is used to read .textlint.yaml. */

        comment designRef /* Design: docs/design/ots/yamldotnet.md */
        comment verificationRef /* Verification: docs/verification/ots/yamldotnet.md */
        comment reqRef /* Requirements: docs/reqstream/ots/yamldotnet.yaml */
    }

    part def FileSystemGlobbing {
        doc /* Microsoft.Extensions.FileSystemGlobbing: matches include and exclude glob patterns against a directory tree. */

        comment designRef /* Design: docs/design/ots/filesystemglobbing.md */
        comment verificationRef /* Verification: docs/verification/ots/filesystemglobbing.md */
        comment reqRef /* Requirements: docs/reqstream/ots/filesystemglobbing.yaml */
    }
}
