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

using System.Diagnostics;

namespace DemaConsulting.TextLint.DictionaryBuilder.Tests.TestSupport;

/// <summary>
///     Runs the compiled dictionary builder as a separate process, exactly as the library's build does.
/// </summary>
/// <remarks>
///     The system tests treat the builder as a black box: they start the real assembly with
///     <c>dotnet</c>, so argument handling, exit codes, and the two output streams are observed
///     the way MSBuild observes them. It is test support, not a shipped component.
/// </remarks>
internal static class BuilderRunner
{
    /// <summary>
    ///     The name of the builder assembly, which the test project copies beside itself.
    /// </summary>
    private const string BuilderAssemblyFileName = "DemaConsulting.TextLint.DictionaryBuilder.dll";

    /// <summary>
    ///     Runs the builder and captures its exit code and output streams.
    /// </summary>
    /// <param name="arguments">The command-line arguments to pass to the builder.</param>
    /// <returns>The exit code and the captured standard output and standard error.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started.</exception>
    public static BuilderRunResult Run(params string[] arguments)
    {
        // Start "dotnet <builder dll> <arguments>" with both streams redirected
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, BuilderAssemblyFileName));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the builder process.");

        // Read both streams concurrently so a full buffer can never stall the process
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new BuilderRunResult(process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }

    /// <summary>
    ///     The outcome of one builder process.
    /// </summary>
    /// <param name="ExitCode">The process exit code.</param>
    /// <param name="Output">Everything the process wrote to standard output.</param>
    /// <param name="Error">Everything the process wrote to standard error.</param>
    internal sealed record BuilderRunResult(int ExitCode, string Output, string Error);
}
