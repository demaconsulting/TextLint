package TextLint {
    part def CommandLinePathResolver {
        doc /* Turns the path and pattern arguments typed on a command line (backslash paths on Windows, absolute paths) into a search directory and validated portable patterns, and defines the result that names the files found. */

        comment sourceRef /* Source: src/DemaConsulting.TextLint/FileSelection/CommandLinePathResolver.cs (supporting type: CommandLineSelection.cs) */
        comment testRef /* Test: test/DemaConsulting.TextLint.Tests/FileSelection/CommandLinePathResolverTests.cs */
        comment designRef /* Design: docs/design/textlint/file-selection/command-line-path-resolver.md */
        comment verificationRef /* Verification: docs/verification/textlint/file-selection/command-line-path-resolver.md */
        comment reqRef /* Requirements: docs/reqstream/textlint/file-selection/command-line-path-resolver.yaml */
    }
}
