namespace Sharpy.Lsp;

/// <summary>
/// Mutable client-supplied configuration for the Sharpy language server.
/// Updated via <c>workspace/didChangeConfiguration</c> notifications.
/// </summary>
/// <remarks>
/// This is registered as a singleton in DI and read by handlers/services on
/// each request. All mutations come from the configuration handler; reads
/// happen on LSP request threads. Thread safety relies on Volatile to ensure
/// cross-thread visibility of flag updates.
/// </remarks>
internal sealed class LspConfiguration
{
    /// <summary>
    /// When false, transition hints (SPY0470-SPY0489) are filtered out before
    /// diagnostics are published to the editor. Default: true.
    /// </summary>
    private volatile bool _transitionHintsEnabled = true;

    public bool TransitionHintsEnabled
    {
        get => _transitionHintsEnabled;
        private set => _transitionHintsEnabled = value;
    }

    /// <summary>
    /// Updates this configuration from a parsed settings object.
    /// </summary>
    /// <param name="settings">
    /// A <see cref="Newtonsoft.Json.Linq.JToken"/> containing client settings under
    /// the <c>sharpy</c> root key (or null/empty for defaults).
    /// </param>
    public void UpdateFrom(Newtonsoft.Json.Linq.JToken? settings)
    {
        if (settings is null)
        {
            return;
        }

        // Settings can arrive as either { "sharpy": {...} } or directly as the
        // sharpy section. Probe both.
        var root = settings["sharpy"] ?? settings;

        var transitionHints = root["transitionHints"];
        if (transitionHints is { } th)
        {
            var enabled = th["enabled"];
            if (enabled is not null && enabled.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
            {
                TransitionHintsEnabled = enabled.ToObject<bool>();
            }
        }
    }
}
