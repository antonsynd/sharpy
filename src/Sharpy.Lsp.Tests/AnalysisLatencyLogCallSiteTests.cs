using FluentAssertions;
using Xunit;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Conformance test: no production call site may reach an <see cref="AnalysisLatencyLog"/>
/// formatter directly — every latency line goes through <c>LogLatency</c>/<c>LogStages</c>, which
/// check <c>ILogger.IsEnabled</c> first.
/// </summary>
/// <remarks>
/// <para>
/// A formatter result passed to a logging call is an ordinary argument: it is built before the
/// logging call can decide whether anything wants it. The server attaches no logging provider at
/// all until a level is asked for (#1225), so an unguarded call built a string that was then
/// discarded — on every debounced analysis for the single-file path, and on every keystroke for the
/// project path, including the no-change skip whose entire purpose is to cost nothing.
/// </para>
/// <para>
/// Three call sites written at different times had independently drifted into the same bug, which
/// is why this is enforced as a rule rather than fixed three times: a fourth site added later
/// inherits the check, and one that does not fails here by name. Scoped by directory rather than by
/// an allowlist of file names, so a legitimate formatter test (the formatters are deliberately pure
/// and directly assertable — see <see cref="AnalysisLatencyLogTests"/>) needs no entry anywhere.
/// </para>
/// </remarks>
public class AnalysisLatencyLogCallSiteTests
{
    private static readonly string[] ForbiddenCalls =
    {
        "AnalysisLatencyLog.Format(",
        "AnalysisLatencyLog.FormatStages(",
    };

    /// <summary>The file that defines the formatters, and so is allowed to call them.</summary>
    private const string DeclaringFile = "AnalysisLatencyLog.cs";

    [Fact]
    public void No_production_call_site_reaches_a_latency_formatter_directly()
    {
        var serverDirectory = FindServerSourceDirectory();

        var offenders = new SCG.List<string>();
        foreach (var file in Directory.EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories))
        {
            // Build output can contain generated sources; they are not call sites anyone writes.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(Path.GetFileName(file), DeclaringFile, StringComparison.Ordinal))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var call in ForbiddenCalls)
                {
                    if (lines[i].Contains(call, StringComparison.Ordinal))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a latency line must be emitted through AnalysisLatencyLog.LogLatency/LogStages, which "
            + "check ILogger.IsEnabled first. Passing a formatter result to a logging call builds "
            + "the string whether or not any provider is attached — and none is until a log level "
            + "is asked for (#1225, #1140).\nOffending call sites:\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// Locates <c>src/Sharpy.Lsp</c>. Note this is a *sibling* of <c>src/Sharpy.Lsp.Tests</c>, not
    /// a parent of it, so a recursive walk from here covers production sources only.
    /// </summary>
    private static string FindServerSourceDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "src", "Sharpy.Lsp");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, DeclaringFile)))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate src/Sharpy.Lsp. This scan is only meaningful against real sources: a "
            + "walk of the wrong directory passes exactly like a clean tree.");
    }
}
