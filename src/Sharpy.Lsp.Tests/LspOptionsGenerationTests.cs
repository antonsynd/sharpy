using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// #1375: <c>ConfiguredFeatureChange_InvalidatesCachedAnalysis</c> was load-flaky — after
/// <c>didChangeConfiguration</c> enabled <c>defer</c>, the pre-change SPY0331 was served once.
/// </summary>
/// <remarks>
/// <para>
/// The mechanism is NOT the one the issue guessed at ("an analysis already in flight when the
/// options change re-caches afterwards"). That window was already closed: <c>GetOrRunAnalysisAsync</c>
/// captures <c>_analysisVersion</c> at the start and every cache write requires it to be unchanged,
/// and <c>InvalidateAnalysis</c> bumps it.
/// </para>
/// <para>
/// The window that was open is the opposite order. A request reads <c>_workspaceOptions</c>, is
/// descheduled past an entire <c>RebuildOptions</c>, and only then starts analysing — so it begins
/// with a version that is ALREADY bumped, the version check passes, and the result caches under
/// options the workspace has abandoned. `volatile` gives visibility, not atomicity of a
/// read-then-use. Closing it takes an options generation read atomically with the options and
/// re-checked at the write.
/// </para>
/// <para>
/// These tests force that interleaving directly rather than trying to provoke it under load: a
/// timing-dependent test that passes 3x proves nothing, which is how the original went flaky in the
/// first place.
/// </para>
/// </remarks>
public class LspOptionsGenerationTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;

    private const string Uri = "file:///options-generation/main.spy";

    // `defer` is experimental: always parsed, but its use reports SPY0331 unless enabled. That
    // makes the diagnostic a clean marker for which options an analysis actually ran under.
    private const string DeferSource =
        "def main() -> None:\n    defer print(\"cleanup\")\n    print(\"body\")\n";

    public LspOptionsGenerationTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
    }

    [Fact]
    public async Task AnAnalysisCarryingSupersededOptions_IsNotCached()
    {
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var staleOptions = _workspace.Options;
        var document = _workspace.GetDocument(Uri);
        document.Should().NotBeNull();

        // Generation 0: the feature is off, so SPY0331 fires. This is the positive control — it
        // proves the arrangement really does produce the diagnostic whose absence we assert below,
        // so a silently-broken setup cannot make the test pass for free.
        var before = await document!.GetOrRunAnalysisAsync(
            _api, staleOptions, optionsGeneration: 0, CancellationToken.None);
        before.Diagnostics.Select(d => d.Code).Should().Contain(
            DiagnosticCodes.Semantic.FeatureNotEnabled,
            "control: without the feature the gate must fire, or this test asserts nothing");

        // The workspace moves on. This invalidates and stamps the document with generation 1.
        _workspace.SetConfiguredFeatures(new[] { "defer" }).Should().BeTrue();

        // Now the descheduled request finally runs, still holding generation 0's options.
        var superseded = await document.GetOrRunAnalysisAsync(
            _api, staleOptions, optionsGeneration: 0, CancellationToken.None);

        superseded.Diagnostics.Select(d => d.Code).Should().Contain(
            DiagnosticCodes.Semantic.FeatureNotEnabled,
            "it ran under the old options, so it necessarily produces the old answer — the fix is "
            + "not to change what it computes but to stop that answer becoming the cache");

        // The contract: whatever is served next must not be the superseded answer. Phrased over the
        // served result rather than over CachedAnalysis being null, because RebuildOptions also arms
        // a 300 ms debounce whose (correct, generation-1) analysis may legitimately populate the
        // cache first — asserting null would be a race, which is the bug's own failure mode.
        var served = await _workspace.GetAnalysisAsync(Uri);
        served.Should().NotBeNull();
        served!.Diagnostics.Select(d => d.Code).Should().NotContain(
            DiagnosticCodes.Semantic.FeatureNotEnabled,
            "a didChangeConfiguration must not leave a pre-change diagnostic reachable");
    }

    [Fact]
    public async Task AnAnalysisAtTheCurrentGeneration_IsCached()
    {
        // The complement, and the reason the guard cannot simply refuse everything: a result at the
        // live generation must still cache, or every request would re-analyse from scratch.
        _workspace.OpenDocument(Uri, DeferSource, 1);
        var document = _workspace.GetDocument(Uri)!;

        await document.GetOrRunAnalysisAsync(
            _api, _workspace.Options, optionsGeneration: 0, CancellationToken.None);

        document.CachedAnalysis.Should().NotBeNull(
            "generation 0 is current for a freshly opened document");
    }

    [Fact]
    public async Task ConfigurationChange_ThenImmediateRequest_ServesThePostChangeAnalysis()
    {
        // The originally-flaky assertion, restated without a wait. It is deterministic now because
        // the product race is closed rather than because the test sleeps: no analysis holding the
        // old options can reach the cache, so there is nothing for the next request to trip over.
        _workspace.OpenDocument(Uri, DeferSource, 1);

        var before = await _workspace.GetAnalysisAsync(Uri);
        before!.Diagnostics.Select(d => d.Code).Should().Contain(
            DiagnosticCodes.Semantic.FeatureNotEnabled);

        _workspace.SetConfiguredFeatures(new[] { "defer" }).Should().BeTrue();

        var after = await _workspace.GetAnalysisAsync(Uri);
        after!.Diagnostics.Select(d => d.Code).Should().NotContain(
            DiagnosticCodes.Semantic.FeatureNotEnabled);
    }

    [Fact]
    public async Task ReopeningAfterAConfigurationChange_StartsAtTheCurrentGeneration()
    {
        // A document opened AFTER the options changed must not be stamped with generation 0, or its
        // first analysis would be refused the cache forever.
        _workspace.OpenDocument(Uri, DeferSource, 1);
        _workspace.SetConfiguredFeatures(new[] { "defer" }).Should().BeTrue();
        _workspace.CloseDocument(Uri);

        _workspace.OpenDocument(Uri, DeferSource, 1);
        var analysis = await _workspace.GetAnalysisAsync(Uri);

        analysis.Should().NotBeNull();
        analysis!.Diagnostics.Select(d => d.Code).Should().NotContain(
            DiagnosticCodes.Semantic.FeatureNotEnabled);
        _workspace.GetDocument(Uri)!.CachedAnalysis.Should().NotBeNull(
            "a document opened at the current generation must be cacheable");
    }

    public void Dispose() => _workspace.Dispose();
}
