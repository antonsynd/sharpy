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
/// then republishes diagnostics for all open documents so editors reflect changes
/// (e.g., toggling transitionHints).
/// </summary>
internal sealed class SharpyDidChangeConfigurationHandler : IDidChangeConfigurationHandler
{
    private readonly LspConfiguration _configuration;
    private readonly LanguageService _languageService;
    private readonly DiagnosticPublisher _diagnosticPublisher;
    private readonly ILogger<SharpyDidChangeConfigurationHandler> _logger;

    public SharpyDidChangeConfigurationHandler(
        LspConfiguration configuration,
        LanguageService languageService,
        DiagnosticPublisher diagnosticPublisher,
        ILogger<SharpyDidChangeConfigurationHandler> logger)
    {
        _configuration = configuration;
        _languageService = languageService;
        _diagnosticPublisher = diagnosticPublisher;
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
                "Configuration updated: TransitionHintsEnabled={Enabled}",
                _configuration.TransitionHintsEnabled);

            // Republish cached analysis results so the new filter is applied
            // without waiting for the next edit. This works because hint filtering
            // happens at ConvertDiagnostics time, not at analysis time — no
            // reanalysis is needed to reflect the configuration change.
            foreach (var fileUri in _languageService.GetProjectFileUris())
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var result = await _languageService.GetAnalysisAsync(fileUri, ct).ConfigureAwait(false);
                if (result is not null)
                {
                    _diagnosticPublisher.PublishDiagnostics(fileUri, result, sourceText: null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration change");
        }

        return Unit.Value;
    }

    public void SetCapability(DidChangeConfigurationCapability capability, ClientCapabilities clientCapabilities)
    {
        // No client capabilities are inspected; defaults are used for all settings.
    }
}
