using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sharpy.Cli.Services;

/// <summary>
/// Wire protocol shared by the <c>sharpyc server</c> keep-alive compiler (D2, #1049) and its
/// thin client. Requests and responses are single-line JSON, newline-delimited over a named
/// pipe (System.Text.Json escapes embedded newlines, so captured stdout/stderr stay on one
/// line). The protocol is deliberately small: one request in, one response out, one compile at
/// a time. There is no daemon auto-spawn — a server is started explicitly with
/// <c>sharpyc server</c> and clients opt in with <c>--server</c>.
/// </summary>
internal static class CompileServerProtocol
{
    /// <summary>
    /// Default pipe name when the user does not pass <c>--pipe</c>/<c>--server=NAME</c>. Kept
    /// stable so a client's <c>--server</c> (no value) finds a plain <c>sharpyc server</c>.
    /// </summary>
    public const string DefaultPipeName = "sharpyc-compile-server";

    public const string CommandCompile = "compile";
    public const string CommandProject = "project";
    public const string CommandShutdown = "shutdown";
    public const string CommandPing = "ping";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
}

/// <summary>
/// A single compile/project/control request sent to the keep-alive server. File paths are
/// resolved to absolute on the client (against the client's working directory) before sending,
/// and <see cref="WorkingDirectory"/> carries that directory so any remaining relative paths
/// (e.g. a defaulted output location) resolve as they would in-process.
/// </summary>
internal sealed class CompileServerRequest
{
    public string Command { get; set; } = CompileServerProtocol.CommandCompile;

    /// <summary>Absolute path to the <c>.spy</c> entry file (compile) or <c>.spyproj</c> (project).</summary>
    public string? Input { get; set; }

    public string? Output { get; set; }

    public string OutputType { get; set; } = "exe";

    public string[] References { get; set; } = Array.Empty<string>();

    public string[] ProjectReferences { get; set; } = Array.Empty<string>();

    public string[] ModulePaths { get; set; } = Array.Empty<string>();

    public string[] Features { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The <c>--namespace</c> override, if the client supplied one. Carried on the wire so a
    /// server-side compile wraps the generated code in the same namespace an in-process compile
    /// would; dropping it here would silently change the emitted output (#1171).
    /// </summary>
    public string? Namespace { get; set; }

    public string Configuration { get; set; } = "Debug";

    public bool WarnAsError { get; set; }

    public string? Nowarn { get; set; }

    public int? MaxErrors { get; set; }

    public bool Incremental { get; set; }

    /// <summary>The client's current working directory, so the server resolves paths identically.</summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// The server's reply. <see cref="Stdout"/>/<see cref="Stderr"/> carry the captured compiler
/// output verbatim (the client re-emits them so the user sees identical text to an in-process
/// build); <see cref="PhaseMillis"/> and <see cref="Warm"/> expose the warm-path timing the D2
/// benchmark and E2E test assert on.
/// </summary>
internal sealed class CompileServerResponse
{
    public int ExitCode { get; set; }

    public string Stdout { get; set; } = string.Empty;

    public string Stderr { get; set; } = string.Empty;

    /// <summary>False on this server process's very first compile (cold), true afterwards.</summary>
    public bool Warm { get; set; }

    /// <summary>1-based ordinal of this compile within the server process lifetime.</summary>
    public int CompileOrdinal { get; set; }

    /// <summary>Aggregate per-phase wall-clock in milliseconds (empty when unavailable).</summary>
    public Dictionary<string, double> PhaseMillis { get; set; } = new();

    public double TotalMillis { get; set; }

    /// <summary>
    /// Runtime assembly paths the compiled program uses. The <c>run</c> client copies these next to
    /// the produced binary before executing it, exactly as an in-process build would.
    /// </summary>
    public string[] UsedAssemblyPaths { get; set; } = Array.Empty<string>();

    /// <summary>Protocol-level error (malformed request, server fault) — distinct from a compile failure.</summary>
    public string? Error { get; set; }
}
