using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// A lightweight JSON-RPC client that spawns the LSP server as a child process
/// and communicates over stdin/stdout using the LSP base protocol (Content-Length headers).
/// </summary>
public sealed class LspTestClient : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ITestOutputHelper? _output;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<JsonNode>> _notifications = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readTask;
    private int _nextId;
    private bool _disposed;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private LspTestClient(Process process, ITestOutputHelper? output)
    {
        _process = process;
        _output = output;
        _readTask = Task.Run(ReadLoopAsync);
    }

    /// <summary>
    /// Starts the LSP server process and returns a connected client.
    /// </summary>
    public static LspTestClient Start(ITestOutputHelper? output = null)
    {
        // Find the LSP project directory relative to the test assembly
        var repoRoot = FindRepoRoot();
        var lspProject = System.IO.Path.Combine(repoRoot, "src", "Sharpy.Lsp", "Sharpy.Lsp.csproj");

        if (!File.Exists(lspProject))
        {
            throw new InvalidOperationException($"LSP project not found at {lspProject}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{lspProject}\" --no-build",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start LSP server process");

        return new LspTestClient(process, output);
    }

    /// <summary>
    /// Sends an LSP initialize request and the initialized notification.
    /// Returns the InitializeResult as a JsonNode.
    /// </summary>
    public async Task<JsonNode?> InitializeAsync(string? rootUri = null, CancellationToken ct = default)
    {
        var initParams = new JsonObject
        {
            ["processId"] = Environment.ProcessId,
            ["capabilities"] = new JsonObject
            {
                ["textDocument"] = new JsonObject
                {
                    ["publishDiagnostics"] = new JsonObject
                    {
                        ["relatedInformation"] = true
                    },
                    ["hover"] = new JsonObject
                    {
                        ["contentFormat"] = new JsonArray("markdown", "plaintext")
                    },
                    ["completion"] = new JsonObject
                    {
                        ["completionItem"] = new JsonObject
                        {
                            ["snippetSupport"] = false
                        }
                    }
                }
            },
            ["rootUri"] = rootUri != null ? JsonValue.Create(rootUri) : null
        };

        var result = await SendRequestAsync("initialize", initParams, ct);

        // Send initialized notification
        await SendNotificationAsync("initialized", new JsonObject(), ct);

        return result;
    }

    /// <summary>
    /// Sends a shutdown request followed by an exit notification.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await SendRequestAsync("shutdown", null, ct);
        await SendNotificationAsync("exit", null, ct);
    }

    /// <summary>
    /// Sends a JSON-RPC request and waits for the response.
    /// </summary>
    public async Task<JsonNode?> SendRequestAsync(
        string method,
        JsonNode? @params,
        CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };

        if (@params != null)
        {
            message["params"] = @params;
        }

        await WriteMessageAsync(message, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);

        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"LSP request '{method}' (id={id}) timed out after {DefaultTimeout.TotalSeconds}s");
        }
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no response expected).
    /// </summary>
    public async Task SendNotificationAsync(
        string method,
        JsonNode? @params,
        CancellationToken ct = default)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };

        if (@params != null)
        {
            message["params"] = @params;
        }

        await WriteMessageAsync(message, ct);
    }

    /// <summary>
    /// Waits for a notification with the given method to arrive.
    /// </summary>
    public async Task<JsonNode> WaitForNotificationAsync(
        string method,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var deadline = timeout ?? DefaultTimeout;
        var queue = _notifications.GetOrAdd(method, _ => new ConcurrentQueue<JsonNode>());

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(deadline);

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var notification))
            {
                return notification;
            }

            try
            {
                await Task.Delay(50, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out waiting for notification '{method}' after {deadline.TotalSeconds}s");
            }
        }

        ct.ThrowIfCancellationRequested();
        throw new TimeoutException($"Timed out waiting for notification '{method}'");
    }

    /// <summary>
    /// Sends a textDocument/didOpen notification for the given URI and content.
    /// </summary>
    public Task DidOpenAsync(string uri, string text, CancellationToken ct = default)
    {
        var @params = new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = uri,
                ["languageId"] = "sharpy",
                ["version"] = 1,
                ["text"] = text
            }
        };
        return SendNotificationAsync("textDocument/didOpen", @params, ct);
    }

    /// <summary>
    /// Sends a textDocument/didChange notification (full content).
    /// </summary>
    public Task DidChangeAsync(string uri, string text, int version, CancellationToken ct = default)
    {
        var @params = new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = uri,
                ["version"] = version
            },
            ["contentChanges"] = new JsonArray(
                new JsonObject { ["text"] = text }
            )
        };
        return SendNotificationAsync("textDocument/didChange", @params, ct);
    }

    /// <summary>
    /// Sends a textDocument/hover request.
    /// </summary>
    public Task<JsonNode?> HoverAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        var @params = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = line, ["character"] = character }
        };
        return SendRequestAsync("textDocument/hover", @params, ct);
    }

    /// <summary>
    /// Sends a textDocument/completion request.
    /// </summary>
    public Task<JsonNode?> CompletionAsync(string uri, int line, int character, CancellationToken ct = default)
    {
        var @params = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = line, ["character"] = character }
        };
        return SendRequestAsync("textDocument/completion", @params, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await _cts.CancelAsync();

        try
        {
            // Give the read loop time to exit
            await _readTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore errors during cleanup
        }

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
            catch
            {
                // Process may have already exited
            }
        }

        _process.Dispose();
        _cts.Dispose();
    }

    private async Task WriteMessageAsync(JsonObject message, CancellationToken ct)
    {
        var json = message.ToJsonString();
        var content = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {content.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        _output?.WriteLine($">>> {message["method"]?.GetValue<string>() ?? "response"} (id={message["id"]})");

        var stdin = _process.StandardInput.BaseStream;
        await stdin.WriteAsync(headerBytes, ct);
        await stdin.WriteAsync(content, ct);
        await stdin.FlushAsync(ct);
    }

    private async Task ReadLoopAsync()
    {
        var stdout = _process.StandardOutput.BaseStream;

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Read headers
                var headers = await ReadHeadersAsync(stdout, _cts.Token);
                if (headers == null)
                    break;

                if (!headers.TryGetValue("Content-Length", out var lengthStr)
                    || !int.TryParse(lengthStr, out var contentLength))
                {
                    continue;
                }

                // Read content body
                var contentBytes = new byte[contentLength];
                var totalRead = 0;
                while (totalRead < contentLength)
                {
                    var read = await stdout.ReadAsync(
                        contentBytes.AsMemory(totalRead, contentLength - totalRead),
                        _cts.Token);
                    if (read == 0)
                        break;
                    totalRead += read;
                }

                if (totalRead < contentLength)
                    break;

                var json = Encoding.UTF8.GetString(contentBytes);
                var node = JsonNode.Parse(json);
                if (node == null)
                    continue;

                DispatchMessage(node);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (IOException)
        {
            // Process exited
        }
    }

    private void DispatchMessage(JsonNode message)
    {
        // Check if it's a response (has "id" but no "method")
        var id = message["id"];
        var method = message["method"]?.GetValue<string>();

        if (id != null && method == null)
        {
            // Response to a request
            var requestId = id.GetValue<int>();
            _output?.WriteLine($"<<< response (id={requestId})");

            if (_pendingRequests.TryRemove(requestId, out var tcs))
            {
                var error = message["error"];
                if (error != null)
                {
                    tcs.SetException(new InvalidOperationException(
                        $"LSP error {error["code"]}: {error["message"]}"));
                }
                else
                {
                    tcs.SetResult(message["result"]);
                }
            }
        }
        else if (method != null && id == null)
        {
            // Notification from server
            _output?.WriteLine($"<<< notification: {method}");

            var queue = _notifications.GetOrAdd(method, _ => new ConcurrentQueue<JsonNode>());
            queue.Enqueue(message["params"]!);
        }
        else if (method != null && id != null)
        {
            // Server-initiated request (e.g., window/workDoneProgress/create)
            _output?.WriteLine($"<<< server request: {method} (id={id})");

            // Auto-respond to known server requests
            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonNode.Parse(id.ToJsonString()),
                ["result"] = null
            };
            // Fire-and-forget the response
            _ = WriteMessageAsync(response, CancellationToken.None);
        }
    }

    private static async Task<Dictionary<string, string>?> ReadHeadersAsync(
        Stream stream, CancellationToken ct)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineBuffer = new StringBuilder();

        while (true)
        {
            var b = new byte[1];
            var read = await stream.ReadAsync(b, ct);
            if (read == 0)
                return null;

            lineBuffer.Append((char)b[0]);

            if (lineBuffer.Length >= 2
                && lineBuffer[^2] == '\r'
                && lineBuffer[^1] == '\n')
            {
                var line = lineBuffer.ToString(0, lineBuffer.Length - 2);
                lineBuffer.Clear();

                if (line.Length == 0)
                {
                    // Empty line = end of headers
                    return headers;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line[..colonIdx].Trim();
                    var value = line[(colonIdx + 1)..].Trim();
                    headers[key] = value;
                }
            }
        }
    }

    private static string FindRepoRoot()
    {
        // Walk up from the test assembly location to find the repo root
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(System.IO.Path.Combine(dir, ".git"))
                || File.Exists(System.IO.Path.Combine(dir, "sharpy.sln")))
            {
                return dir;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }

        // Fallback to known path
        return "/Users/anton/Documents/github/sharpy";
    }
}
