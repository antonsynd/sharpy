using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// The log-level configuration as a real editor sees it: a spawned server process, driven over
/// stdio, with the flag and the environment variable that a launcher would actually use (#1225).
/// </summary>
/// <remarks>
/// <see cref="AnalysisStageAttributionTests"/> pins the attribution's content and its honest-empty
/// semantics in process; these prove the configuration reaches a server that was started as a
/// separate process, and that the destination it writes to is one an editor can read.
/// </remarks>
public class ServerLoggingTests
{
    private readonly ITestOutputHelper _output;

    public ServerLoggingTests(ITestOutputHelper output) => _output = output;

    private const string Uri = "file:///logging_probe.spy";

    /// <summary>A body <c>AstFingerprint</c> can prove equal, so a comment-only edit to it
    /// classifies as no structural change and is served without a compiler call.</summary>
    private const string Source = "def main() -> int:\n    return 1\n";

    [Fact]
    public async Task Default_startup_attaches_no_log_destination()
    {
        await using var client = LspTestClient.Start(_output);
        await client.InitializeAsync();
        await AnalyseAsync(client);

        // The strong assertion is about the *Information*-level latency line, not the Debug stage
        // line: the analysis that just ran logs one, so if any provider were attached — say a
        // refactor that registered one unconditionally — it would appear here. Its absence is what
        // proves an unconfigured server still routes nothing, which is what keeps the per-keystroke
        // instrumentation free when unobserved (#1140).
        client.StandardErrorLines.Should().NotContain(l => l.Contains(AnalysisLatencyLog.Marker),
            "an unconfigured server attaches no log destination");
        client.LogMessages.Should().NotContain(l => l.Contains(AnalysisLatencyLog.Marker));

        client.StandardErrorLines.Should().NotContain(l => l.Contains(AnalysisLatencyLog.StagesMarker));
        client.LogMessages.Should().NotContain(l => l.Contains(AnalysisLatencyLog.StagesMarker));

        client.StandardErrorLines.Should().BeEmpty(
            "a server nobody configured writes nothing at all");

        await ShutdownAsync(client);
    }

    [Fact]
    public async Task Log_level_debug_makes_the_per_stage_attribution_reachable()
    {
        await using var client = LspTestClient.Start(
            _output, serverArgs: new[] { "--log-level", "Debug" });
        await client.InitializeAsync();
        await AnalyseAsync(client);

        await WaitForAsync(() => client.StandardErrorLines.Any(
            l => l.Contains(AnalysisLatencyLog.StagesMarker)));

        client.StandardErrorLines.Should().Contain(
            l => l.Contains(AnalysisLatencyLog.StagesMarker),
            "the per-stage attribution (#1140) exists to be read on a running server, which is "
            + "the configuration it diagnoses (#1225)");

        // The contrast with Default_startup_attaches_no_log_destination: with a destination
        // attached, the Information-level latency line reaches standard error too.
        client.StandardErrorLines.Should().Contain(l => l.Contains(AnalysisLatencyLog.Marker));

        await ShutdownAsync(client);
    }

    [Fact]
    public async Task Environment_variable_configures_a_server_that_takes_no_arguments()
    {
        await using var client = LspTestClient.Start(
            _output,
            environment: new[]
            {
                new KeyValuePair<string, string>(ServerCommandLine.EnvironmentVariable, "Debug"),
            });
        await client.InitializeAsync();
        await AnalyseAsync(client);

        await WaitForAsync(() => client.StandardErrorLines.Any(
            l => l.Contains(AnalysisLatencyLog.StagesMarker)));

        client.StandardErrorLines.Should().Contain(
            l => l.Contains(AnalysisLatencyLog.StagesMarker),
            "editors whose LSP client configuration cannot pass server arguments have only this");

        await ShutdownAsync(client);
    }

    [Fact]
    public async Task Fast_path_edits_get_a_latency_line_and_no_invented_stages()
    {
        await using var client = LspTestClient.Start(
            _output, serverArgs: new[] { "--log-level", "Debug" });
        await client.InitializeAsync();
        await AnalyseAsync(client);
        await WaitForAsync(() => client.StandardErrorLines.Any(
            l => l.Contains(AnalysisLatencyLog.StagesMarker)));

        var stageLinesBefore = Count(client, AnalysisLatencyLog.StagesMarker);
        var latencyLinesBefore = Count(client, AnalysisLatencyLog.Marker);

        // A comment-only edit: the same AST, so an incremental fast path serves it without calling
        // the compiler. It still publishes, so it still logs a latency line.
        await client.DidChangeAsync(Uri, Source + "# a comment\n", 2);
        await client.WaitForNotificationAsync("textDocument/publishDiagnostics", TimeSpan.FromSeconds(30));
        await WaitForAsync(() => Count(client, AnalysisLatencyLog.Marker) > latencyLinesBefore);

        Count(client, AnalysisLatencyLog.StagesMarker).Should().Be(stageLinesBefore,
            "an edit served without a compiler call has no stages to attribute; an empty breakdown "
            + "is the signal that the fast path worked, and inventing one would erase the "
            + "served-from-cache vs analysed distinction");

        await ShutdownAsync(client);
    }

    [Fact]
    public async Task An_unusable_log_level_warns_but_still_serves()
    {
        await using var client = LspTestClient.Start(
            _output, serverArgs: new[] { "--log-level", "chatty" });

        var result = await client.InitializeAsync();

        result.Should().NotBeNull("a broken editor configuration must not stop the server from starting");
        result!["capabilities"].Should().NotBeNull();

        await WaitForAsync(() => client.StandardErrorLines.Any(l => l.Contains("chatty")));
        client.StandardErrorLines.Should().Contain(l => l.Contains("chatty"),
            "the rejected value is reported once so the misconfiguration is discoverable");

        await ShutdownAsync(client);
    }

    private static int Count(LspTestClient client, string marker)
        => client.StandardErrorLines.Count(l => l.Contains(marker, StringComparison.Ordinal));

    /// <summary>Opens the probe document and waits for its diagnostics — one real analysis.</summary>
    private static async Task AnalyseAsync(LspTestClient client)
    {
        await client.DidOpenAsync(Uri, Source);
        await client.WaitForNotificationAsync("textDocument/publishDiagnostics", TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Polls for a condition, and throws if it never holds. Log lines are written after the
    /// diagnostics that prove the analysis ran, and reach this process through a pipe, so they can
    /// lag the notification slightly.
    /// </summary>
    /// <remarks>
    /// Expiring quietly would make <see cref="Fast_path_edits_get_a_latency_line_and_no_invented_stages"/>
    /// pass for free: if the edit were never processed, the stage count would be trivially
    /// unchanged and the test would report that a thing which never happened emitted no stages. A
    /// wait that expires is a broken arrangement, not the absence the caller was about to assert.
    /// </remarks>
    private static async Task WaitForAsync(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Waited {timeout.TotalSeconds:F0}s and this never became true: {description}. "
            + "The server did not produce what the test then asserts about.");
    }

    private static async Task ShutdownAsync(LspTestClient client)
    {
        try
        {
            await client.ShutdownAsync();
        }
        catch
        {
            // Server may have already exited.
        }
    }
}
