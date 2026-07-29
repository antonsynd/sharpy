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

    /// <summary>
    /// Experimental feature names from <c>sharpy.features</c>, as written by the client. Names are
    /// validated where they become compiler options (<see cref="SharpyWorkspace.SetConfiguredFeatures"/>),
    /// which is the one place every source of features — this setting and the workspace
    /// <c>.spyproj</c> — converges.
    /// </summary>
    private volatile IReadOnlyList<string> _features = Array.Empty<string>();

    public bool TransitionHintsEnabled
    {
        get => _transitionHintsEnabled;
        private set => _transitionHintsEnabled = value;
    }

    /// <summary>
    /// The experimental features the editor asks analysis to enable (<c>sharpy.features</c>), so
    /// gated syntax the CLI accepts under <c>--enable-feature</c> is accepted in-editor too (#1149).
    /// A workspace <c>.spyproj</c>'s <c>&lt;Features&gt;</c> is layered under this — neither source
    /// can disable what the other enabled.
    /// </summary>
    public IReadOnlyList<string> Features
    {
        get => _features;
        private set => _features = value;
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

        var features = root["features"];
        if (features is not null)
        {
            Features = ReadFeatureNames(features);
        }
    }

    /// <summary>
    /// Reads <c>sharpy.features</c>, which the VS Code contribution declares as an array of strings.
    /// A bare string is also accepted (and split on <c>;</c>/<c>,</c>) so a user who writes the
    /// setting the way <c>&lt;Features&gt;</c> is written in a <c>.spyproj</c> is not silently
    /// ignored. Anything else (a number, an object) yields an empty list rather than a guess; the
    /// name is not validated here — <see cref="SharpyWorkspace.SetConfiguredFeatures"/> validates
    /// every source against <see cref="Sharpy.Compiler.Shared.FeatureFlags.KnownFeatures"/> and
    /// reports unknown names.
    /// </summary>
    private static IReadOnlyList<string> ReadFeatureNames(Newtonsoft.Json.Linq.JToken token)
    {
        var names = new List<string>();

        void AddSplit(string raw)
        {
            foreach (var part in raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = part.Trim();
                if (name.Length > 0)
                    names.Add(name);
            }
        }

        switch (token.Type)
        {
            case Newtonsoft.Json.Linq.JTokenType.Array:
                foreach (var item in token)
                {
                    if (item.Type == Newtonsoft.Json.Linq.JTokenType.String)
                        AddSplit(item.ToObject<string>() ?? string.Empty);
                }
                break;
            case Newtonsoft.Json.Linq.JTokenType.String:
                AddSplit(token.ToObject<string>() ?? string.Empty);
                break;
        }

        return names;
    }
}
