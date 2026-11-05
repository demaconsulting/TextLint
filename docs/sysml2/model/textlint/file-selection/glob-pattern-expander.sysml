package TextLint {
    part def GlobPatternExpander {
        doc /* Validates a user-written glob pattern and rewrites it into matcher patterns: brace alternatives expanded, bare names matching at any depth. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/FileSelection/GlobPatternExpander.cs (supporting type: InvalidGlobPatternException.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/GlobPatternExpanderTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection/glob-pattern-expander.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection/glob-pattern-expander.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection/glob-pattern-expander.yaml */
    }
}
