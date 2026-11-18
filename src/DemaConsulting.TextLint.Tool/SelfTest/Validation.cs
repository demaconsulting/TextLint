// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Runtime.InteropServices;
// cspell:ignore wrold xyzzyq favourite zorb zorbquix quix

using DemaConsulting.TestResults.IO;
using DemaConsulting.TextLint.Tool.Cli;
using DemaConsulting.TextLint.Tool.Utilities;

namespace DemaConsulting.TextLint.Tool.SelfTest;

/// <summary>
///     Provides self-validation functionality for the TextLint Tool.
/// </summary>
internal static class Validation
{
    /// <summary>
    ///     A misspelled word that no dictionary holds, used by the spell-checking scenarios.
    /// </summary>
    private const string Misspelling = "wrold";

    /// <summary>
    ///     A second misspelled word that no dictionary holds, for scenarios that need two distinct findings.
    /// </summary>
    private const string OtherMisspelling = "xyzzyq";

    /// <summary>
    ///     A word spelled the American way, which only the American dictionary accepts.
    /// </summary>
    private const string AmericanSpelling = "favorite";

    /// <summary>
    ///     The same word spelled the British way, which only the British dictionary accepts.
    /// </summary>
    private const string BritishSpelling = "favourite";

    /// <summary>
    ///     Runs self-validation tests and optionally writes results to a file.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    ///     If any self-test fails, <c>context.WriteError</c> is called for each failure, which sets
    ///     <c>context.ExitCode</c> to 1 as a side-effect. If a results file is requested and its
    ///     extension is unsupported, <c>context.WriteError</c> is also called, resulting in a
    ///     non-zero exit code.
    /// </remarks>
    public static void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Print validation header
        PrintValidationHeader(context);

        // Create test results collection
        var testResults = new DemaConsulting.TestResults.TestResults
        {
            Name = "TextLint Tool Self-Validation"
        };

        // Run core functionality tests
        RunVersionTest(context, testResults);
        RunHelpTest(context, testResults);

        // Run the checks that prove real spell checking works in this environment, each in its own
        // temporary directory so that they share no state and leave nothing behind
        RunLintScenario(context, testResults, "TextLintTool_CheckCleanFile", VerifyCleanFile);
        RunLintScenario(context, testResults, "TextLintTool_CheckMisspelling", VerifyMisspelling);
        RunLintScenario(context, testResults, "TextLintTool_CheckSettingsFile", VerifySettingsFile);
        RunLintScenario(context, testResults, "TextLintTool_CheckSettingsError", VerifySettingsError);
        RunLintScenario(context, testResults, "TextLintTool_CheckGlobsReplaceFiles", VerifyGlobsReplaceFiles);
        RunLintScenario(context, testResults, "TextLintTool_CheckInlineDirective", VerifyInlineDirective);
        RunLintScenario(context, testResults, "TextLintTool_CheckLanguageOption", VerifyLanguageOption);
        RunLintScenario(context, testResults, "TextLintTool_CheckBinaryFileSkipped", VerifyBinaryFileSkipped);
        RunLintScenario(context, testResults, "TextLintTool_CheckNoFilesMatched", VerifyNoFilesMatched);
        RunLintScenario(context, testResults, "TextLintTool_CheckAbsolutePath", VerifyAbsolutePath);
        RunLintScenario(context, testResults, "TextLintTool_CheckOverrides", VerifyOverrides);
        RunLintScenario(context, testResults, "TextLintTool_CheckTechnicalTerms", VerifyTechnicalTerms);
        RunLintScenario(context, testResults, "TextLintTool_CheckCspellTokenization", VerifyCspellTokenization);

        // Calculate totals
        var totalTests = testResults.Results.Count;
        var passedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Passed);
        var failedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Failed);

        // Print summary
        context.WriteLine("");
        context.WriteLine($"Total Tests: {totalTests}");
        context.WriteLine($"Passed: {passedTests}");
        if (failedTests > 0)
        {
            context.WriteError($"Failed: {failedTests}");
        }
        else
        {
            context.WriteLine($"Failed: {failedTests}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>
    ///     Prints the validation header with system information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} DEMA Consulting TextLint Tool");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs a test for version display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunVersionTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("TextLintTool_VersionDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "version-test.log");

            // Build command line arguments
            var args = new List<string>
            {
                "--silent",
                "--log", logFile,
                "--version"
            };

            // Run the program
            int exitCode;
            using (var testContext = Context.Create([.. args]))
            {
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify version string is in log (version contains dots like 0.0.0)
                var versionPattern = new System.Text.RegularExpressions.Regex(
                    @"\b\d+\.\d+\.\d+",
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                if (!string.IsNullOrWhiteSpace(logContent) &&
                    versionPattern.IsMatch(logContent))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine($"✓ TextLintTool_VersionDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Version string not found in log";
                    context.WriteError($"✗ TextLintTool_VersionDisplay - Failed: Version string not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ TextLintTool_VersionDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "TextLintTool_VersionDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test for help display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunHelpTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("TextLintTool_HelpDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "help-test.log");

            // Build command line arguments
            var args = new List<string>
            {
                "--silent",
                "--log", logFile,
                "--help"
            };

            // Run the program
            int exitCode;
            using (var testContext = Context.Create([.. args]))
            {
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify help text is in log
                if (logContent.Contains("Usage:") && logContent.Contains("Options:"))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine($"✓ TextLintTool_HelpDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Help text not found in log";
                    context.WriteError($"✗ TextLintTool_HelpDisplay - Failed: Help text not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ TextLintTool_HelpDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "TextLintTool_HelpDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs one spell-checking scenario and records its outcome.
    /// </summary>
    /// <remarks>
    ///     Every scenario runs the real tool logic in-process against files it creates in a private temporary
    ///     directory, so passing proves that settings are read, files are selected, text is checked, and
    ///     results are reported on this operating system and runtime, which the version and help checks cannot.
    ///     A scenario returns <see langword="null"/> when it passed, or a description of what was wrong. Any
    ///     exception is recorded as a failure of that scenario, so one broken scenario cannot hide the others.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    /// <param name="testName">The name of the scenario, which requirements and test reports refer to.</param>
    /// <param name="scenario">Runs the scenario in a sandbox and returns null on success or the failure description.</param>
    private static void RunLintScenario(
        Context context,
        DemaConsulting.TestResults.TestResults testResults,
        string testName,
        Func<LintSandbox, string?> scenario)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult(testName);

        try
        {
            using var sandbox = new LintSandbox();
            var failure = scenario(sandbox);
            if (failure is null)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ {testName} - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = failure;
                context.WriteError($"✗ {testName} - Failed: {failure}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, testName, ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Verifies that a file without spelling mistakes is accepted.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyCleanFile(LintSandbox sandbox)
    {
        // A correctly spelled document must give exit code 0, no finding, and the exact clean summary
        sandbox.WriteFile("clean.md", "# Release Notes\n\nThe quick brown fox jumps over the lazy dog.\nEvery change is reviewed before it is merged.\n");
        var outcome = sandbox.RunTool("clean.md");

        return outcome.ExitCode == 0 && outcome.Reports([], Summary(1, 0, 0))
            ? null
            : $"Expected exit code 0, no findings, and a clean summary, but got exit code {outcome.ExitCode}: {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that a misspelling is reported at its line and column and fails the run.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyMisspelling(LintSandbox sandbox)
    {
        // Two misspellings among correct words: each must be reported at its own line and column, and nothing else
        sandbox.WriteFile("a.md", $"Hello {Misspelling}\nThe second line has {OtherMisspelling} in it.\n");
        var outcome = sandbox.RunTool("a.md");

        string[] expected =
        [
            $"a.md(1,7): error: Unknown word '{Misspelling}'",
            $"a.md(2,21): error: Unknown word '{OtherMisspelling}'"
        ];
        return outcome.ExitCode == 1 && outcome.Reports(expected, Summary(1, 2, 1))
            ? null
            : $"Expected exit code 1 and exactly the findings at a.md(1,7) and a.md(2,21), but got exit code {outcome.ExitCode}: {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that the settings file in the root directory is found and applied.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifySettingsFile(LintSandbox sandbox)
    {
        // The settings select markdown files, ignore one of them, accept one word, and flag another
        sandbox.WriteFile(
            ".textlint.yaml",
            "files:\n  - \"*.md\"\nignorePaths:\n  - ignored.md\nwords:\n  - " + Misspelling + "\nflagWords:\n  - \"utilize->use\"\n");
        sandbox.WriteFile("a.md", $"Hello {Misspelling} utilize\n");
        sandbox.WriteFile("ignored.md", $"Hello {OtherMisspelling}\n");
        var outcome = sandbox.RunTool();

        // The accepted word and the ignored file are silent; the flagged word is the only finding, with its suggestion
        var applied = outcome.ExitCode == 1 &&
                      outcome.Reports(["a.md(1,13): error: Flagged word 'utilize'; suggestions: use"], Summary(1, 1, 1));
        return applied ? null : $"The settings file was not applied (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that an invalid settings file is reported with its position and nothing is checked.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifySettingsError(LintSandbox sandbox)
    {
        // A key the tool does not support is an error, so the file must not be partly applied
        sandbox.WriteFile(".textlint.yaml", "bogusKey: true\n");
        sandbox.WriteFile("a.md", $"Hello {Misspelling}\n");
        var outcome = sandbox.RunTool("a.md");

        // The settings error is the only error line, and no summary is written because nothing is checked
        var settingsPath = PathHelpers.SafePathCombine(sandbox.WorkDirectory, ".textlint.yaml");
        var reported = outcome.ExitCode == 1 &&
                       outcome.Findings.SequenceEqual(
                           [$"{settingsPath}(1,1): error: 'bogusKey' is not a supported setting."],
                           StringComparer.Ordinal) &&
                       !outcome.HasSummary;
        return reported ? null : $"Expected a positioned settings error and no check (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that patterns on the command line replace the <c>files</c> setting.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyGlobsReplaceFiles(LintSandbox sandbox)
    {
        // The settings name b.md, but the command line names a.md, so only a.md may be checked
        sandbox.WriteFile(".textlint.yaml", "files:\n  - b.md\n");
        sandbox.WriteFile("a.md", $"Hello {Misspelling}\n");
        sandbox.WriteFile("b.md", $"Hello {OtherMisspelling}\n");
        var outcome = sandbox.RunTool("a.md");

        var replaced = outcome.ExitCode == 1 &&
                       outcome.Reports([$"a.md(1,7): error: Unknown word '{Misspelling}'"], Summary(1, 1, 1));
        return replaced ? null : $"Expected only a.md to be checked (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that in-document directives of both supported prefixes suppress findings.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyInlineDirective(LintSandbox sandbox)
    {
        // Each file would be reported without its directive, so a clean run proves both prefixes work
        sandbox.WriteFile("cspell-prefix.md", $"<!-- cspell:ignore {Misspelling} -->\nHello {Misspelling}\n");
        sandbox.WriteFile("textlint-prefix.md", $"<!-- textlint:ignore {OtherMisspelling} -->\nHello {OtherMisspelling}\n");
        var outcome = sandbox.RunTool("*.md");

        return outcome.ExitCode == 0 && outcome.Reports([], Summary(2, 0, 0))
            ? null
            : $"Expected both files to be checked and clean (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that the language option chooses between the American and the British dictionary.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyLanguageOption(LintSandbox sandbox)
    {
        // One American and one British spelling of the same word: each language must accept only its own
        sandbox.WriteFile("a.md", $"{AmericanSpelling} {BritishSpelling}\n");
        var american = sandbox.RunTool("--language", "en-US", "a.md");
        var british = sandbox.RunTool("--language", "en-GB", "a.md");

        var americanOk = american.ExitCode == 1 &&
                         american.Reports([$"a.md(1,10): error: Unknown word '{BritishSpelling}'"], Summary(1, 1, 1));
        var britishOk = british.ExitCode == 1 &&
                        british.Reports([$"a.md(1,1): error: Unknown word '{AmericanSpelling}'"], Summary(1, 1, 1));
        return americanOk && britishOk
            ? null
            : $"The language option did not select the dictionary. en-US: {american.Output} en-GB: {british.Output}";
    }

    /// <summary>
    ///     Verifies that a binary file is skipped and reported as information without failing the run.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyBinaryFileSkipped(LintSandbox sandbox)
    {
        // A NUL byte marks the file as binary; the text file beside it is still checked
        sandbox.WriteFile("clean.md", "Hello world.\n");
        sandbox.WriteBytes("data.bin", [0x00, 0x01, 0x02]);
        var outcome = sandbox.RunTool("*");

        var skipped = outcome.ExitCode == 0 &&
                      outcome.Reports([], Summary(1, 0, 0, skipped: 1)) &&
                      outcome.HasLine(
                          "data.bin: info: The file looks like binary data: it contains a NUL character in its first 1,024 characters.");
        return skipped ? null : $"Expected the binary file to be skipped without failing (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that a selection that matches no files fails unless an empty selection is allowed.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyNoFilesMatched(LintSandbox sandbox)
    {
        // The pattern matches nothing: a failure by default, and a success when explicitly allowed
        sandbox.WriteFile("a.md", "Hello world.\n");
        var failing = sandbox.RunTool("*.nomatch");
        var allowed = sandbox.RunTool("--allow-empty", "*.nomatch");

        var handled = failing.ExitCode == 1 &&
                      failing.Findings.Count == 0 &&
                      failing.HasLine(
                          "No files matched the file selection, so nothing was checked. Use --allow-empty to accept an empty selection.") &&
                      !failing.HasSummary &&
                      allowed.ExitCode == 0 &&
                      allowed.Reports([], Summary(0, 0, 0));
        return handled
            ? null
            : $"Expected exit code 1 then 0 with --allow-empty, but got {failing.ExitCode} then {allowed.ExitCode}: {failing.Output} {allowed.Output}";
    }

    /// <summary>
    ///     Verifies that a file named by an absolute path is checked and reported by that path.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyAbsolutePath(LintSandbox sandbox)
    {
        // The absolute path is what the user typed, so it is what the report must show
        var path = sandbox.WriteFile("a.md", $"Hello {Misspelling}\n");
        var outcome = sandbox.RunTool(path);

        return outcome.ExitCode == 1 &&
               outcome.Reports([$"{path}(1,7): error: Unknown word '{Misspelling}'"], Summary(1, 1, 1))
            ? null
            : $"Expected the issue to be reported by absolute path (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that an ordered files list, an override with words, and an override that disables files all apply.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyOverrides(LintSandbox sandbox)
    {
        // The files list removes the skipped directory with a negation, one override accepts the word for docs, and
        // another disables the legacy files; every file holds the same misspelling, so only a.md may be reported
        sandbox.WriteFile(
            ".textlint.yaml",
            "files:\n  - \"**/*.md\"\n  - \"!skipped/**\"\noverrides:\n  - filename: docs/**\n    words:\n      - " + Misspelling +
            "\n  - filename: legacy/**\n    enabled: false\n");
        foreach (var path in new[] { "a.md", "docs/b.md", "skipped/c.md", "legacy/d.md" })
        {
            sandbox.WriteFile(path, $"Hello {Misspelling}\n");
        }

        var outcome = sandbox.RunTool();

        // Only the file that no setting touches is reported, and the others are silent
        // a.md and docs/b.md are checked (the disabled legacy file is not counted), and a.md holds the only finding
        var applied = outcome.ExitCode == 1 &&
                      outcome.Reports([$"a.md(1,7): error: Unknown word '{Misspelling}'"], Summary(2, 1, 1));
        return applied ? null : $"Expected only a.md to be reported (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Verifies that the bundled technical-term dictionary is loaded and applies in every language.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    private static string? VerifyTechnicalTerms(LintSandbox sandbox)
    {
        // Software terms that no English word list holds sit beside one real misspelling, so the run proves that
        // the technical terms are known and that the file was really checked; this holds for both languages
        sandbox.WriteFile("a.md", $"Deploy with kubectl to kubernetes from github using powershell and nuget {Misspelling}\n");
        string[] expected = [$"a.md(1,74): error: Unknown word '{Misspelling}'"];

        foreach (var language in new[] { "en-US", "en-GB" })
        {
            var outcome = sandbox.RunTool("--language", language, "a.md");
            if (outcome.ExitCode != 1 || !outcome.Reports(expected, Summary(1, 1, 1)))
            {
                return $"Expected only '{Misspelling}' at a.md(1,74) with {language} (exit code {outcome.ExitCode}): {outcome.Output}";
            }
        }

        return null;
    }

    /// <summary>
    ///     Verifies the cspell-compatible handling of identifiers, addresses, and hexadecimal numbers.
    /// </summary>
    /// <param name="sandbox">The sandbox to run in.</param>
    /// <returns>Null when the scenario passed; otherwise what was wrong.</returns>
    /// <remarks>
    ///     The expected findings are those that cspell 10 records for the same constructs in the parity corpus: a
    ///     camel-case identifier is split into its words, each reported at its own column, while a URL, an
    ///     e-mail address, and a hexadecimal number are ignored by the default patterns.
    /// </remarks>
    private static string? VerifyCspellTokenization(LintSandbox sandbox)
    {
        sandbox.WriteFile(
            "a.md",
            "Call zorbQuix at https://zorb.example/quix or mail user@zorbquix.org with 0xDEADBEEF\n");
        var outcome = sandbox.RunTool("a.md");

        string[] expected =
        [
            "a.md(1,6): error: Unknown word 'zorb'",
            "a.md(1,10): error: Unknown word 'Quix'"
        ];
        return outcome.ExitCode == 1 && outcome.Reports(expected, Summary(1, 2, 1))
            ? null
            : $"Expected exactly 'zorb' at a.md(1,6) and 'Quix' at a.md(1,10) (exit code {outcome.ExitCode}): {outcome.Output}";
    }

    /// <summary>
    ///     Writes test results to a file in TRX or JUnit format.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results to write.</param>
    private static void WriteResultsFile(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            string content;

            if (extension == ".trx")
            {
                content = TrxSerializer.Serialize(testResults);
            }
            else if (extension == ".xml")
            {
                // Assume JUnit format for .xml extension
                content = JUnitSerializer.Serialize(testResults);
            }
            else
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified here as a top-level handler to log file write errors
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a new test result object with common properties.
    /// </summary>
    /// <param name="testName">The name of the test.</param>
    /// <returns>A new test result object.</returns>
    private static DemaConsulting.TestResults.TestResult CreateTestResult(string testName)
    {
        return new DemaConsulting.TestResults.TestResult
        {
            Name = testName,
            ClassName = "Validation",
            CodeBase = "TextLintTool"
        };
    }

    /// <summary>
    ///     Finalizes a test result by setting its duration and adding it to the collection.
    /// </summary>
    /// <param name="test">The test result to finalize.</param>
    /// <param name="startTime">The start time of the test.</param>
    /// <param name="testResults">The test results collection to add to.</param>
    private static void FinalizeTestResult(
        DemaConsulting.TestResults.TestResult test,
        DateTime startTime,
        DemaConsulting.TestResults.TestResults testResults)
    {
        test.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(test);
    }

    /// <summary>
    ///     Handles test exceptions by setting failure information and logging the error.
    /// </summary>
    /// <param name="test">The test result to update.</param>
    /// <param name="context">The context for output.</param>
    /// <param name="testName">The name of the test for error messages.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void HandleTestException(
        DemaConsulting.TestResults.TestResult test,
        Context context,
        string testName,
        Exception ex)
    {
        test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
        test.ErrorMessage = $"Exception: {ex.Message}";
        context.WriteError($"✗ {testName} - FAILED: {ex.Message}");
    }

    /// <summary>
    ///     The outcome of one run of the tool inside a scenario.
    /// </summary>
    /// <param name="ExitCode">The exit code the run would have returned to the operating system.</param>
    /// <param name="Output">Everything the run wrote, as it was written to the log.</param>
    private sealed record LintRunOutcome(int ExitCode, string Output)
    {
        /// <summary>
        ///     Gets the finding lines of the run, in the order they were written.
        /// </summary>
        /// <remarks>
        ///     A finding is a compiler-style line <c>file(line,column): error: message</c>; comparing the whole
        ///     list lets a scenario prove that exactly the expected findings were reported and nothing else.
        /// </remarks>
        public List<string> Findings { get; } = Output
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Contains("): error: ", StringComparison.Ordinal))
            .ToList();

        /// <summary>
        ///     Gets whether the run wrote a line that is exactly the given text, ignoring surrounding white space.
        /// </summary>
        /// <param name="expected">The complete line, such as the summary line.</param>
        /// <returns><see langword="true"/> when a complete line of the output equals <paramref name="expected"/>.</returns>
        public bool HasLine(string expected) => Output
            .Split('\n')
            .Any(line => string.Equals(line.Trim(), expected, StringComparison.Ordinal));

        /// <summary>
        ///     Gets whether the run wrote a summary line, which it does only when files were checked.
        /// </summary>
        public bool HasSummary => Output
            .Split('\n')
            .Any(static line => line.Trim().StartsWith("Checked ", StringComparison.Ordinal));

        /// <summary>
        ///     Gets whether the findings are exactly the expected ones, in order, and the run wrote the expected
        ///     summary line.
        /// </summary>
        /// <param name="expectedFindings">Every finding line the run must write, and no other.</param>
        /// <param name="expectedSummary">The complete summary line.</param>
        /// <returns><see langword="true"/> when both match exactly.</returns>
        public bool Reports(string[] expectedFindings, string expectedSummary) =>
            Findings.SequenceEqual(expectedFindings, StringComparer.Ordinal) && HasLine(expectedSummary);
    }

    /// <summary>
    ///     Formats the summary line the tool writes at the end of a check.
    /// </summary>
    /// <param name="files">The number of files checked.</param>
    /// <param name="issues">The number of findings.</param>
    /// <param name="filesWithIssues">The number of files the findings are in.</param>
    /// <param name="skipped">The number of files skipped on purpose.</param>
    /// <returns>The complete summary line.</returns>
    private static string Summary(int files, int issues, int filesWithIssues, int skipped = 0) =>
        $"Checked {files} file(s): {issues} issue(s) in {filesWithIssues} file(s), {skipped} skipped, 0 could not be checked.";

    /// <summary>
    ///     A private working area for one spell-checking scenario: a directory of sample files and a way to run
    ///     the tool on it.
    /// </summary>
    /// <remarks>
    ///     The tool is run in-process through <see cref="Program.Run(Context)"/> with the
    ///     root option instead of changing the current directory, because the current directory is shared by
    ///     the whole process. The log file lives beside the sample directory, not in it, so it can never be
    ///     selected as a file to check. Disposing removes everything.
    /// </remarks>
    private sealed class LintSandbox : IDisposable
    {
        /// <summary>
        ///     The temporary directory that holds the sample directory and the log file.
        /// </summary>
        private readonly TemporaryDirectory _temporary = new();

        /// <summary>
        ///     The number of runs so far, so that each run writes its own log file.
        /// </summary>
        private int _runCount;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LintSandbox"/> class and creates the sample directory.
        /// </summary>
        public LintSandbox()
        {
            WorkDirectory = PathHelpers.SafePathCombine(_temporary.DirectoryPath, "work");
            Directory.CreateDirectory(WorkDirectory);
        }

        /// <summary>
        ///     Gets the absolute path of the directory that holds the sample files and is the root of each run.
        /// </summary>
        public string WorkDirectory { get; }

        /// <summary>
        ///     Writes a sample text file, creating any missing directories.
        /// </summary>
        /// <param name="relativePath">The path relative to the sample directory.</param>
        /// <param name="content">The text of the file.</param>
        /// <returns>The absolute path of the file.</returns>
        public string WriteFile(string relativePath, string content)
        {
            var path = PathHelpers.SafePathCombine(WorkDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>
        ///     Writes a sample binary file.
        /// </summary>
        /// <param name="relativePath">The path relative to the sample directory.</param>
        /// <param name="content">The bytes of the file.</param>
        public void WriteBytes(string relativePath, byte[] content)
        {
            File.WriteAllBytes(PathHelpers.SafePathCombine(WorkDirectory, relativePath), content);
        }

        /// <summary>
        ///     Runs the tool on the sample directory with the given arguments.
        /// </summary>
        /// <param name="arguments">The arguments after the options the sandbox adds itself.</param>
        /// <returns>The exit code and everything the run wrote.</returns>
        public LintRunOutcome RunTool(params string[] arguments)
        {
            // Silent keeps the validation output clean; the log file captures everything the run writes
            var logFile = PathHelpers.SafePathCombine(_temporary.DirectoryPath, $"run-{++_runCount}.log");
            string[] args = ["--silent", "--log", logFile, "--root", WorkDirectory, .. arguments];

            // The context must be disposed before the log is read, so that the log is complete
            int exitCode;
            using (var runContext = Context.Create(args))
            {
                Program.Run(runContext);
                exitCode = runContext.ExitCode;
            }

            return new LintRunOutcome(exitCode, File.ReadAllText(logFile));
        }

        /// <summary>
        ///     Deletes the sandbox and everything in it.
        /// </summary>
        public void Dispose()
        {
            _temporary.Dispose();
        }
    }

    /// <summary>
    ///     Represents a temporary directory that is automatically deleted when disposed.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        ///     Gets the path to the temporary directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
        /// </summary>
        public TemporaryDirectory()
        {
            DirectoryPath = PathHelpers.SafePathCombine(Path.GetTempPath(), $"textlint_validation_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Deletes the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup errors during disposal
            }
        }
    }
}
