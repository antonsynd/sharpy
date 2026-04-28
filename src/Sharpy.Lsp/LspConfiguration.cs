namespace Sharpy.Lsp;

/// <summary>
/// Mutable client-supplied configuration for the Sharpy language server.
/// Updated via <c>workspace/didChangeConfiguration</c> notifications.
/// </summary>
/// <remarks>
/// This is registered as a singleton in DI and read by handlers/services on
/// each request. All mutations from the configuration handler use the
/// <see cref="UpdateFrom"/> method; reads are simple property accesses
/// (no locking required for boolean flags).
/// </remarks>
internal sealed class LspConfiguration
{
    /// <summary>
    /// When false, transition hints (SPY0470-SPY0489) are filtered out before
    /// diagnostics are published to the editor. Default: true.
    /// </summary>
    public bool TransitionHintsEnabled { get; private set; } = true;

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
