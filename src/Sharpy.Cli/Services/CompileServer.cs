using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Sharpy.Cli.Commands;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Cli.Services;

/// <summary>
/// Keep-alive compile server (D2, #1049). A single explicitly-started process listens on a named
/// pipe and serves compile/project requests one at a time, reusing the process — and therefore the
/// process-lifetime <see cref="MetadataReference"/> and overload-index caches — across every
/// request. The first request in a process pays the cold cost (JIT, reference load, index
/// deserialize); every request after is warm.
///
/// Modeled on VBCSCompiler conceptually and on the in-repo LSP stdio host as precedent, but kept
/// intentionally small: newline-delimited JSON request/response (<see cref="CompileServerProtocol"/>),
/// one compile at a time, an idle timeout, and no daemon auto-spawn — a server is started with
/// <c>sharpyc server</c> and clients opt in with <c>--server</c>.
/// </summary>
internal sealed class CompileServer
{
    private readonly string _pipeName;
    private readonly TimeSpan _idleTimeout;
    private readonly ICompilerLogger _logger;

    // Compiles mutate process-global Console.Out/Error (to capture compiler output) and the current
    // directory, so they must never overlap. A single-instance pipe already serializes connections;
    // this lock is the explicit "one compile at a time" guarantee the protocol promises.
    private readonly SemaphoreSlim _compileLock = new(1, 1);

    private int _compileCount;
    private volatile bool _shutdownRequested;

    public CompileServer(string pipeName, TimeSpan idleTimeout, ICompilerLogger? logger = null)
    {
        _pipeName = pipeName;
        _idleTimeout = idleTimeout;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Run the accept loop until an idle timeout elapses, a <c>shutdown</c> request arrives, or the
    /// host cancels (Ctrl+C). Returns a process exit code.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken externalToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        var idleDesc = _idleTimeout == Timeout.InfiniteTimeSpan
            ? "disabled"
            : $"{_idleTimeout.TotalSeconds:F0}s";
        await Console.Error.WriteLineAsync(
            $"sharpyc server listening on pipe '{_pipeName}' (idle timeout {idleDesc}). "
            + "Send a 'shutdown' request or press Ctrl+C to stop.").ConfigureAwait(false);

        // One reused server-stream instance for the whole lifetime: after a client disconnects we
        // Disconnect() and WaitForConnection() again on the SAME instance. Disposing and recreating
        // the stream per connection races on Unix, where the pipe is backed by a domain socket that
        // is unbound/rebound each cycle — a client reconnecting in that window (e.g. a ping
        // immediately followed by a compile) lands on a socket being torn down and gets a silently
        // closed connection.
        NamedPipeServerStream pipe;
        try
        {
            pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
        catch (IOException ex)
        {
            // The pipe name is already in use (another server) or otherwise unopenable.
            await Console.Error.WriteLineAsync($"sharpyc server: cannot open pipe '{_pipeName}': {ex.Message}").ConfigureAwait(false);
            return 1;
        }

        using (pipe)
        {
            while (!cts.IsCancellationRequested && !_shutdownRequested)
            {
                // A linked CTS that also fires on idle timeout, so WaitForConnectionAsync returns
                // and we can shut down a server nobody is using.
                using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                {
                    if (_idleTimeout != Timeout.InfiniteTimeSpan)
                    {
                        idleCts.CancelAfter(_idleTimeout);
                    }

                    try
                    {
                        await pipe.WaitForConnectionAsync(idleCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }

                        await Console.Error.WriteLineAsync("sharpyc server: idle timeout reached; shutting down.").ConfigureAwait(false);
                        break;
                    }
                }

                try
                {
                    await HandleConnectionAsync(pipe, cts).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A connection-level fault must not take the server down; log and accept the next.
                    await Console.Error.WriteLineAsync($"sharpyc server: dropped a connection after an error: {ex.Message}").ConfigureAwait(false);
                }

                // Reset the instance to accept the next client on the same socket.
                if (pipe.IsConnected)
                {
                    try
                    {
                        pipe.Disconnect();
                    }
                    catch (IOException)
                    {
                        // Already torn down by the peer; the next WaitForConnection re-arms it.
                    }
                }
            }
        }

        return 0;
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationTokenSource cts)
    {
        // leaveOpen: true on both wrappers — the pipe is owned by the caller's using(pipe). Without
        // it, one wrapper's dispose closes the pipe and the other's flush-on-dispose then faults.
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        while (pipe.IsConnected && !cts.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Client vanished mid-read.
                break;
            }

            if (line == null)
            {
                // Client closed the connection cleanly.
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            CompileServerRequest? request = null;
            string? parseError = null;
            try
            {
                request = CompileServerProtocol.Deserialize<CompileServerRequest>(line);
            }
            catch (JsonException ex)
            {
                parseError = ex.Message;
            }

            if (request == null)
            {
                await WriteAsync(writer, new CompileServerResponse
                {
                    ExitCode = CliHelpers.ExitInternalError,
                    Error = $"malformed request: {parseError ?? "empty"}",
                }).ConfigureAwait(false);
                continue;
            }

            // A fault handling one request (a compiler crash, a broken write) must never take the
            // server down or leave the client hanging on a silently-closed pipe: reply with a
            // structured error and keep serving. A write failure means the client is already gone,
            // so drop this connection but stay alive for the next one.
            try
            {
                switch (request.Command)
                {
                    case CompileServerProtocol.CommandShutdown:
                        await WriteAsync(writer, new CompileServerResponse { ExitCode = 0, Stdout = "shutting down" }).ConfigureAwait(false);
                        _shutdownRequested = true;
                        await cts.CancelAsync().ConfigureAwait(false);
                        return;

                    case CompileServerProtocol.CommandPing:
                        await WriteAsync(writer, new CompileServerResponse
                        {
                            ExitCode = 0,
                            Stdout = "pong",
                            CompileOrdinal = _compileCount,
                        }).ConfigureAwait(false);
                        break;

                    case CompileServerProtocol.CommandCompile:
                    case CompileServerProtocol.CommandProject:
                        var response = ExecuteCompile(request);
                        await WriteAsync(writer, response).ConfigureAwait(false);
                        break;

                    default:
                        await WriteAsync(writer, new CompileServerResponse
                        {
                            ExitCode = CliHelpers.ExitInternalError,
                            Error = $"unknown command '{request.Command}'",
                        }).ConfigureAwait(false);
                        break;
                }
            }
            catch (IOException)
            {
                // The client disconnected before we could reply; nothing more to do on this pipe.
                break;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"sharpyc server: error handling '{request.Command}': {ex}").ConfigureAwait(false);
                try
                {
                    await WriteAsync(writer, new CompileServerResponse
                    {
                        ExitCode = CliHelpers.ExitInternalError,
                        Error = $"server fault handling '{request.Command}': {ex.Message}",
                    }).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // The client is gone; drop this connection and accept the next.
                    break;
                }
            }
        }
    }

    private static Task WriteAsync(StreamWriter writer, CompileServerResponse response)
        => writer.WriteLineAsync(CompileServerProtocol.Serialize(response));

    /// <summary>
    /// Run one compile with the process's global Console redirected to string buffers (so the
    /// captured stdout/stderr can be shipped back verbatim) and the working directory set to the
    /// client's, under the single-compile lock.
    /// </summary>
    private CompileServerResponse ExecuteCompile(CompileServerRequest request)
    {
        _compileLock.Wait();

        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalCwd = Directory.GetCurrentDirectory();
        var outBuffer = new StringWriter();
        var errBuffer = new StringWriter();
        var ordinal = Interlocked.Increment(ref _compileCount);

        try
        {
            if (!string.IsNullOrEmpty(request.WorkingDirectory) && Directory.Exists(request.WorkingDirectory))
            {
                Directory.SetCurrentDirectory(request.WorkingDirectory);
            }

            Console.SetOut(outBuffer);
            Console.SetError(errBuffer);

            var (exitCode, metrics, usedAssemblyPaths) = RunCompile(request);

            var response = new CompileServerResponse
            {
                ExitCode = exitCode,
                CompileOrdinal = ordinal,
                Warm = ordinal > 1,
                UsedAssemblyPaths = usedAssemblyPaths?.ToArray() ?? Array.Empty<string>(),
            };

            if (metrics != null)
            {
                foreach (var (phase, data) in metrics.AggregatePhaseMetrics)
                {
                    response.PhaseMillis[phase] = data.Duration.TotalMilliseconds;
                }

                response.TotalMillis = metrics.TotalDuration.TotalMilliseconds;
            }

            response.Stdout = outBuffer.ToString();
            response.Stderr = errBuffer.ToString();
            return response;
        }
        catch (Exception ex)
        {
            return new CompileServerResponse
            {
                ExitCode = CliHelpers.ExitInternalError,
                CompileOrdinal = ordinal,
                Warm = ordinal > 1,
                Error = ex.Message,
                Stdout = outBuffer.ToString(),
                Stderr = errBuffer.ToString(),
            };
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            try
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
            catch (IOException)
            {
                // The client's directory may have gone away; ignore — the next compile sets its own.
            }

            _compileLock.Release();
        }
    }

    private static (int ExitCode, ProjectCompilationMetrics? Metrics, IReadOnlySet<string>? UsedAssemblyPaths) RunCompile(
        CompileServerRequest request)
    {
        if (string.IsNullOrEmpty(request.Input))
        {
            Console.Error.WriteLine("sharpyc server: request is missing an input path.");
            return (CliHelpers.ExitInternalError, null, null);
        }

        var inputFile = new FileInfo(request.Input);

        if (request.Command == CompileServerProtocol.CommandProject)
        {
            var projectExit = ProjectCommand.CompileProjectForServer(
                inputFile, request.Configuration, request.Incremental, NullLogger.Instance, out var projectMetrics);
            return (projectExit, projectMetrics, null);
        }

        var output = string.IsNullOrEmpty(request.Output) ? null : new FileInfo(request.Output);
        var result = BuildCommand.CompileToBinary(
            inputFile,
            request.OutputType,
            output,
            request.References,
            request.ProjectReferences,
            request.ModulePaths,
            NullLogger.Instance,
            metricsFormat: null,
            metricsOutput: null,
            warnAsError: request.WarnAsError,
            nowarn: request.Nowarn,
            maxErrors: request.MaxErrors,
            configuration: request.Configuration,
            features: request.Features);

        var exitCode = result == null ? CliHelpers.LastFailureExitCode : 0;
        return (exitCode, result?.ProjectMetrics, result?.UsedAssemblyPaths);
    }
}
