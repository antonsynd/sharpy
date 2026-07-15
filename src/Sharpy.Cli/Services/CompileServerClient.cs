using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Sharpy.Cli.Services;

/// <summary>
/// Thin client for the keep-alive <see cref="CompileServer"/> (D2, #1049). Opens the named pipe,
/// sends one request, reads one response. If no server is listening it fails fast so the caller can
/// fall back to an in-process compile — the server is a fast path, never a hard dependency.
/// </summary>
internal static class CompileServerClient
{
    // Short connect timeout: when no server is running we want the fallback to be near-instant, not
    // a multi-second stall. A running server accepts immediately.
    private const int ConnectTimeoutMs = 500;

    /// <summary>
    /// Attempt the compile through a server on <paramref name="pipeName"/>. Returns the process exit
    /// code on success (having written the server's captured stdout/stderr to the console), or
    /// <c>null</c> if no usable server responded — signalling the caller to compile in-process. The
    /// fallback reason is written to <paramref name="stderr"/> so the choice is visible.
    /// </summary>
    public static int? TryRun(
        string pipeName, CompileServerRequest request, TextWriter stdout, TextWriter stderr, bool verbose = false)
    {
        if (!TrySend(pipeName, request, out var response, out var failureReason) || response == null)
        {
            stderr.WriteLine($"sharpyc: no compile server on pipe '{pipeName}' ({failureReason}); compiling in-process.");
            return null;
        }

        if (response.Error != null)
        {
            stderr.WriteLine($"sharpyc: compile server error ({response.Error}); compiling in-process.");
            return null;
        }

        stdout.Write(response.Stdout);
        stderr.Write(response.Stderr);

        if (verbose)
        {
            var warmth = response.Warm ? "warm" : "cold";
            stderr.WriteLine(
                $"sharpyc: compiled via server on pipe '{pipeName}' "
                + $"({warmth}, compile #{response.CompileOrdinal}, server-side {response.TotalMillis:F1} ms).");
        }

        return response.ExitCode;
    }

    /// <summary>
    /// Low-level send/receive. Returns false (with a human-readable <paramref name="failureReason"/>)
    /// on any connection or protocol failure rather than throwing.
    /// </summary>
    public static bool TrySend(
        string pipeName,
        CompileServerRequest request,
        out CompileServerResponse? response,
        out string? failureReason)
    {
        response = null;
        failureReason = null;

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            client.Connect(ConnectTimeoutMs);

            // leaveOpen: true on both wrappers — the pipe is owned by `client`'s using. Without it,
            // the reader disposes first and closes the pipe, then the writer's flush-on-dispose hits
            // a closed pipe (ObjectDisposedException).
            using var writer = new StreamWriter(client, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

            writer.WriteLine(CompileServerProtocol.Serialize(request));

            var line = reader.ReadLine();
            if (line == null)
            {
                failureReason = "server closed the connection without responding";
                return false;
            }

            response = CompileServerProtocol.Deserialize<CompileServerResponse>(line);
            if (response == null)
            {
                failureReason = "server sent an unparseable response";
                return false;
            }

            return true;
        }
        catch (TimeoutException)
        {
            failureReason = "no server listening";
            return false;
        }
        catch (ArgumentException ex)
        {
            // A malformed/over-long pipe name (e.g. on macOS the Unix-domain-socket path backing the
            // pipe is capped at 104 chars) throws here — fall back rather than crash the client.
            failureReason = $"invalid pipe name: {ex.Message}";
            return false;
        }
        catch (JsonException ex)
        {
            failureReason = $"unparseable response: {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            failureReason = ex.Message;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }
}
