package TextLint {
    private import SoftwareItemMetadata::*;

    part def HiddenPathPolicy {
        doc /* Decides which hidden (dot-prefixed) paths the include patterns may reach: wildcards never match a hidden name, a literal leading dot does. */

        metadata info : SoftwareInfo {
            description = "Decides which hidden (dot-prefixed) paths the include patterns may reach: wildcards never match a hidden name, a literal leading dot does.";
            sourceFiles = ("src/DemaConsulting.TextLint/FileSelection/HiddenPathPolicy.cs");
            testFiles = ("test/DemaConsulting.TextLint.Tests/FileSelection/HiddenPathPolicyTests.cs");
            designFiles = ("docs/design/textlint/file-selection/hidden-path-policy.md");
            verificationFiles = ("docs/verification/textlint/file-selection/hidden-path-policy.md");
            requirementFiles = ("docs/reqstream/textlint/file-selection/hidden-path-policy.yaml");
        }
    }
}
