using Microsoft.Extensions.Logging;

namespace Sharpy.Lsp;

/// <summary>
/// A resolved logging configuration: the minimum <paramref name="Level"/>, and whether anything
/// actually asked for one.
/// </summary>
/// <param name="Level">The minimum level to log at.</param>
/// <param name="IsConfigured">
/// True when a flag or the environment variable asked for a level — including when the value it
/// asked with was unusable, since that is still someone asking for logs. False means nothing did,
/// and the server leaves its log records unrouted exactly as it did before #1225: the per-keystroke
/// instrumentation has to stay free when unobserved (#1140).
/// </param>
public readonly record struct ServerLogging(LogLevel Level, bool IsConfigured);

/// <summary>
/// The language server's own command line: the minimum log level and <c>--help</c> (#1225).
/// </summary>
/// <remarks>
/// <para>
/// The server is normally launched by an editor, so the level is resolvable two ways:
/// <c>--log-level &lt;level&gt;</c> for a launcher that can pass server arguments, and the
/// <see cref="EnvironmentVariable"/> for one that cannot. The flag wins when both are set.
/// </para>
/// <para>
/// Resolution lives here rather than inline in the server builder so its precedence is unit
/// testable without starting a server. It is public because <c>sharpyc lsp</c> — the launcher the
/// VS Code extension uses — forwards the flag to <see cref="Program.Main"/> and needs its spelling.
/// </para>
/// </remarks>
public static class ServerCommandLine
{
    /// <summary>The command-line flag, accepted as <c>--log-level X</c> and <c>--log-level=X</c>.</summary>
    public const string LogLevelFlag = "--log-level";

    /// <summary>The environment variable read when the flag is absent.</summary>
    public const string EnvironmentVariable = "SHARPY_LSP_LOG_LEVEL";

    /// <summary>
    /// The level used when nothing asks for another one. Unchanged from before #1225, so a server
    /// nobody configured behaves exactly as it did.
    /// </summary>
    public const LogLevel DefaultLogLevel = LogLevel.Information;

    /// <summary>Returns true if the arguments ask for usage text rather than a server.</summary>
    public static bool IsHelpRequested(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is "--help" or "-h" or "-?" or "/?")
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the minimum log level: <see cref="LogLevelFlag"/> &gt; <paramref name="environmentValue"/>
    /// &gt; <see cref="DefaultLogLevel"/>. An unusable value is reported once on
    /// <paramref name="warnings"/> and then ignored — a stale editor configuration must not stop the
    /// server from starting, which is the one thing a language server has to do.
    /// </summary>
    /// <param name="args">The process arguments (the flag and its value, if present).</param>
    /// <param name="environmentValue">
    /// The value of <see cref="EnvironmentVariable"/>, or null when it is unset.
    /// </param>
    /// <param name="warnings">Where a rejected value is reported; normally standard error.</param>
    public static ServerLogging ResolveLogging(
        IReadOnlyList<string> args,
        string? environmentValue,
        TextWriter warnings)
    {
        if (TryReadFlagValue(args, out var flagValue))
            return new ServerLogging(Parse(flagValue, LogLevelFlag, warnings), IsConfigured: true);

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new ServerLogging(
                Parse(environmentValue, EnvironmentVariable, warnings), IsConfigured: true);
        }

        return new ServerLogging(DefaultLogLevel, IsConfigured: false);
    }

    /// <summary>
    /// Reads the value of the first <see cref="LogLevelFlag"/> occurrence. A flag with no value
    /// counts as present with an empty value, so it is reported as unusable rather than silently
    /// falling through to the environment variable — the caller asked explicitly and was wrong.
    /// </summary>
    private static bool TryReadFlagValue(IReadOnlyList<string> args, out string value)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg.StartsWith(LogLevelFlag + "=", StringComparison.Ordinal))
            {
                value = arg[(LogLevelFlag.Length + 1)..];
                return true;
            }

            if (string.Equals(arg, LogLevelFlag, StringComparison.Ordinal))
            {
                value = i + 1 < args.Count ? args[i + 1] : string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Parses a level name. Names are the <see cref="LogLevel"/> members, case-insensitively;
    /// a numeric value is rejected even though <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
    /// accepts one, because "3" is far more likely to be a mistake than a request for Warning.
    /// </summary>
    private static LogLevel Parse(string value, string source, TextWriter warnings)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 0
            && !char.IsDigit(trimmed[0])
            && trimmed[0] != '-'
            && Enum.TryParse<LogLevel>(trimmed, ignoreCase: true, out var level)
            && Enum.IsDefined(level))
        {
            return level;
        }

        warnings.WriteLine(
            $"[warn] Sharpy LSP: {source} value '{value}' is not a log level; using {DefaultLogLevel}. "
            + "Valid levels: Trace, Debug, Information, Warning, Error, Critical, None.");
        return DefaultLogLevel;
    }

    /// <summary>
    /// The <c>--help</c> text. It states the fast-path semantics of the per-stage attribution
    /// (#1140) because the reason to raise the level to Debug is to read that attribution, and a
    /// reader who does not know that an empty breakdown means "served without a compiler call"
    /// will read it as missing data.
    /// </summary>
    public static string UsageText { get; } = string.Join(Environment.NewLine, new[]
    {
        "Sharpy Language Server — Language Server Protocol over stdio.",
        "",
        "Usage:",
        "  sharpyc lsp [options]",
        "  Sharpy.Lsp [options]",
        "",
        "Options:",
        "  --log-level <level>  Minimum log level: Trace, Debug, Information (default),",
        "                       Warning, Error, Critical, None. Also settable via the",
        $"                       {EnvironmentVariable} environment variable, for editors whose",
        "                       LSP client configuration cannot pass server arguments; the",
        "                       flag wins when both are set.",
        "  -h, --help           Show this help and exit.",
        "",
        "Reading a Debug trace:",
        "  Log output goes to standard error, which editors surface in the server's output",
        "  channel; nothing is logged at all unless a level is asked for. At Debug the protocol",
        "  library's own per-request lines are interleaved with the server's.",
        $"  '{AnalysisLatencyLog.Marker}' lines are logged at Information, one per analysis.",
        $"  '{AnalysisLatencyLog.StagesMarker}' lines break one of those numbers into pipeline",
        "  stages and are logged at Debug. A latency line with no stage line means the",
        "  incremental fast path served that edit without calling the compiler: there are no",
        "  stages to attribute. The absent breakdown is the signal that the fast path worked,",
        "  not a gap in the measurement.",
        "",
    });
}
