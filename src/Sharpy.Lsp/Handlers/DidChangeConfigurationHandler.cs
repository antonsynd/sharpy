using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles workspace/didChangeConfiguration notifications.
/// Updates the shared <see cref="LspConfiguration"/> with client-supplied settings,
/// then has the project's diagnostics republished through the project route so editors reflect
/// changes (e.g., toggling transitionHints).
/// </summary>
internal sealed class SharpyDidChangeConfigurationHandler : IDidChangeConfigurationHandler
{
    private readonly LspConfiguration _configuration;
    private readonly LanguageService _languageService;
    private readonly ILanguageServerFacade _server;
    private readonly ILogger<SharpyDidChangeConfigurationHandler> _logger;

    public SharpyDidChangeConfigurationHandler(
        LspConfiguration configuration,
        LanguageService languageService,
        ILanguageServerFacade server,
        ILogger<SharpyDidChangeConfigurationHandler> logger)
    {
        _configuration = configuration;
        _languageService = languageService;
        _server = server;
        _logger = logger;
    }

    public async Task<Unit> Handle(DidChangeConfigurationParams request, CancellationToken ct)
    {
        try
        {
            // Settings are arbitrary JSON — client convention is to send the
            // sharpy-namespaced settings under a "sharpy" key. UpdateFrom probes
            // both shapes.
            var settings = request.Settings as JToken;
            _configuration.UpdateFrom(settings);

            _logger.LogInformation(
                "Configuration updated: TransitionHintsEnabled={Enabled}, Features=[{Features}]",
                _configuration.TransitionHintsEnabled,
                string.Join(", ", _configuration.Features));

            // Features change what analysis produces (SPY0331 gating), not just how it is rendered,
            // so a feature change reanalyzes — the workspace re-runs open documents and a loaded
            // project is reindexed, whose completion publishes every project file (#1149, #2242).
            var reanalyzed = await _languageService
                .ApplyWorkspaceFeaturesAsync(_configuration.Features, ct)
                .ConfigureAwait(false);

            // No feature changed, so nothing was reanalyzed — but the transition-hint filter may
            // have: it is applied when a diagnostic is converted, not at analysis, so the project's
            // cached results are republished through the same seam to apply it without an edit.
            if (!reanalyzed)
            {
                await _languageService.RepublishProjectDiagnosticsAsync(ct).ConfigureAwait(false);
            }

            RequestInlayHintRefresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration change");
        }

        return Unit.Value;
    }

    /// <summary>
    /// Asks the client to re-request inlay hints so a configuration change shows up without an
    /// edit. Both settings that reach hints change them: <c>inlayHints.typeAnnotations</c> gates
    /// the type hints directly, and <c>features</c> changes the inferred types they display.
    /// Clients that did not declare <c>workspace.inlayHint.refreshSupport</c> are left alone —
    /// their hints update on the next edit.
    /// </summary>
    private void RequestInlayHintRefresh()
    {
        var inlayHint = _server.ClientSettings?.Capabilities?.Workspace?.InlayHint;
        if (inlayHint is not { IsSupported: true, Value.RefreshSupport: true })
        {
            return;
        }

        _server.Workspace.SendInlayHintRefresh(new InlayHintRefreshParams());
    }

    public void SetCapability(DidChangeConfigurationCapability capability, ClientCapabilities clientCapabilities)
    {
        // No client capabilities are inspected; defaults are used for all settings.
    }
}
