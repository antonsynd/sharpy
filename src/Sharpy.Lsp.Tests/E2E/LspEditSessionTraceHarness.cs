using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// On-demand trace of a real edit session against the real server process, as the confirmation
/// half of the #1140 LSP latency measurement. Excluded from the normal run via
/// <c>Category=Benchmark</c>; run explicitly:
/// <code>
/// .claude/scripts/dotnet-serialized test \
///   --filter "FullyQualifiedName~LspEditSessionTraceHarness" \
///   --logger "console;verbosity=detailed"
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a scripted LSP client, not a human in an editor.</b> It drives the same stdio
/// protocol VS Code drives, against the same <c>Sharpy.Lsp</c> process with the same default
/// references — so it does exercise what the in-process harness structurally cannot: the debounce
/// timer, the buffer overlay, JSON-RPC serialization, and diagnostic publication. What it cannot
/// observe is anything that depends on a real client's behavior: VS Code's own request pattern
/// (hover, completion and semantic-token requests interleaved with edits, all contending for the
/// same analysis), its incremental change ranges as produced by real keystrokes, and the human
/// timing distribution of those keystrokes.
/// </para>
/// <para>
/// Like <see cref="LspAnalysisLatencyBaselineHarness"/>, this asserts nothing about timings — only
/// that enough samples were collected to be worth reading.
/// </para>
/// </remarks>
public sealed class LspEditSessionTraceHarness : IAsyncLifetime
{
    private const int EditCount = 35;

    /// <summary>Mirrors <c>SharpyWorkspace.DebounceDelay</c>, which is private to the server.</summary>
    private const double DebounceMs = 300;

    private readonly ITestOutputHelper _output;
    private LspTestClient _client = null!;

    public LspEditSessionTraceHarness(ITestOutputHelper output) => _output = output;

    public Task InitializeAsync()
    {
        _client = LspTestClient.Start(_output);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _client.ShutdownAsync();
        }
        catch
        {
            // Server may have already exited.
        }
        await _client.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Trace_real_edit_session_change_to_publish()
    {
        await _client.InitializeAsync();

        const string uri = "file:///trace_session.spy";
        var version = 1;
        await _client.DidOpenAsync(uri, EditedSource(0));

        // The open triggers its own publish; consume it so the first timed sample belongs to an
        // edit rather than to document open. It also carries the server's verdict on the input, so
        // it doubles as this trace's proof that what follows times a complete analysis (#1224).
        var openPublish = await _client.WaitForNotificationAsync("textDocument/publishDiagnostics",
            TimeSpan.FromSeconds(30));
        AssertNoErrorsPublished(openPublish);

        var samples = new SCG.List<double>();
        for (var i = 1; i <= EditCount; i++)
        {
            await DrainAsync("textDocument/publishDiagnostics");

            // A renamed top-level declaration: a structural edit, so the incremental fast paths
            // cannot serve it and the server runs the full analysis this trace exists to time.
            // Fixed-width so the document length never changes across the run.
            // A 1ms poll: the default 50ms cannot resolve an analysis of a few tens of ms, which is
            // exactly the quantity this trace exists to corroborate.
            var sw = Stopwatch.StartNew();
            await _client.DidChangeAsync(uri, EditedSource(i), ++version);
            await _client.WaitForNotificationAsync("textDocument/publishDiagnostics",
                TimeSpan.FromSeconds(30), pollInterval: TimeSpan.FromMilliseconds(1));
            sw.Stop();

            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        Assert.True(samples.Count >= 30,
            $"Wanted at least 30 change->publish samples, collected {samples.Count}.");

        ReportSamples("change->publish (client-observed, includes 300ms debounce)", samples);

        // The debounce is a fixed constant the server owns (SharpyWorkspace.DebounceDelay), so
        // netting it out leaves analysis plus publication — the quantity the in-process harness
        // rows measure directly, and the only part of this number that any pipeline work can move.
        var netted = new SCG.List<double>(samples.Count);
        foreach (var sample in samples)
            netted.Add(sample - DebounceMs);
        ReportSamples("change->publish minus debounce (analysis + publish)", netted);

        // The server's own AnalysisLatencyLog numbers, if its logging reaches the client as
        // window/logMessage. These exclude the debounce wait and are therefore the ones directly
        // comparable to the in-process harness rows.
        var serverLatencies = await CollectServerLatenciesAsync();
        if (serverLatencies.Count > 0)
        {
            ReportSamples($"server-side analysis ({AnalysisLatencyLogMarker})", serverLatencies);
        }
        else
        {
            _output.WriteLine(
                "[LSP trace] no server-side latency lines reached the client: the server's logging "
                + "is not surfaced as window/logMessage, so this trace can only report the "
                + "client-observed number above.");
        }
    }

    private const string AnalysisLatencyLogMarker = "LSP analysis latency";

    /// <summary>
    /// The trace document: the same source the in-process harness times, plus one marker
    /// declaration whose name carries the edit counter.
    /// </summary>
    private static string EditedSource(int edit)
        => LspAnalysisLatencyBaselineHarness.ValidMediumFileSource()
            + string.Create(CultureInfo.InvariantCulture,
                $"\n\ndef marker_{edit:D2}() -> int:\n    return {edit % 10}\n");

    /// <summary>
    /// Asserts a <c>textDocument/publishDiagnostics</c> notification reported no errors, so the
    /// samples that follow time a full analysis rather than a truncated one (#1224). Warnings are
    /// allowed — they do not stop the pipeline.
    /// </summary>
    private static void AssertNoErrorsPublished(JsonNode notification)
    {
        // LspTestClient enqueues the notification's `params` node, not the envelope.
        var diagnostics = notification["diagnostics"]?.AsArray();
        Assert.NotNull(diagnostics);

        // LSP DiagnosticSeverity.Error == 1.
        var errors = diagnostics!
            .Where(d => d?["severity"]?.GetValue<int>() == 1)
            .Select(d => d?["message"]?.GetValue<string>() ?? "<no message>")
            .ToList();

        Assert.True(errors.Count == 0,
            "The traced document must analyze cleanly for these samples to time a full analysis, "
            + "but the server published: " + string.Join(" | ", errors));
    }

    /// <summary>Consumes any already-queued notifications of the given method.</summary>
    private async Task DrainAsync(string method)
    {
        // Quiescence drain: consumes already-queued notifications between timed edits.
        // The 50ms timeout means "nothing left", not "first one hasn't arrived" (#1606).
        while (true)
        {
            try
            {
                await _client.WaitForNotificationAsync(method, TimeSpan.FromMilliseconds(50));
            }
            catch (TimeoutException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Pulls the elapsed-milliseconds figures out of whatever <c>window/logMessage</c>
    /// notifications the server sent during the session.
    /// </summary>
    private async Task<SCG.List<double>> CollectServerLatenciesAsync()
    {
        // Quiescence drain: collects server-side latency log messages after the edit
        // session has completed. Empty result is expected and not an error (#1606).
        var latencies = new SCG.List<double>();
        while (true)
        {
            JsonNode notification;
            try
            {
                notification = await _client.WaitForNotificationAsync(
                    "window/logMessage", TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException)
            {
                return latencies;
            }

            // As above: the queued node is `params`, so the message sits directly on it — reading
            // it as notification["params"]["message"] always yielded null. Fixing that does not by
            // itself populate this row: the server sends no window/logMessage at all during a
            // trace (measured: 36 publishDiagnostics, 0 logMessage), so the loop above times out
            // on the first wait and this line is never reached. The path is corrected anyway
            // because it would silently swallow every sample the moment logging is surfaced.
            var message = notification["message"]?.GetValue<string>();
            if (message is null || !message.Contains(AnalysisLatencyLogMarker, StringComparison.Ordinal))
                continue;

            const string key = "elapsedMs=";
            var at = message.IndexOf(key, StringComparison.Ordinal);
            if (at < 0)
                continue;

            var value = message[(at + key.Length)..].Split(' ')[0];
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                latencies.Add(ms);
        }
    }

    private void ReportSamples(string label, SCG.List<double> samples)
    {
        var sorted = new SCG.List<double>(samples);
        sorted.Sort();
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[LSP trace] {label}: samples={sorted.Count} median={sorted[sorted.Count / 2]:F1}ms "
            + $"min={sorted[0]:F1}ms max={sorted[^1]:F1}ms"));
    }
}
