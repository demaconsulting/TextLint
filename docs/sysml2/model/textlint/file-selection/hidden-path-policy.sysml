package TextLint {
    part def HiddenPathPolicy {
        doc /* Decides which hidden (dot-prefixed) paths the include patterns may reach: wildcards never match a hidden name, a literal leading dot does. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/FileSelection/HiddenPathPolicy.cs */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/HiddenPathPolicyTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection/hidden-path-policy.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection/hidden-path-policy.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection/hidden-path-policy.yaml */
    }
}
