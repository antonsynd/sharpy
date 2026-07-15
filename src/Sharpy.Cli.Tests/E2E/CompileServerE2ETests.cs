using System.Diagnostics;
using FluentAssertions;
using Sharpy.Cli.Services;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// D2 (#1049) end-to-end coverage for <c>sharpyc server</c>. The positive test starts a real
/// keep-alive server process and drives two sequential compiles through it over the named pipe,
/// asserting the second reports warm per-phase timings (proving the process — and its
/// process-lifetime reference/overload-index caches — is genuinely reused). The negative test
/// asserts a client falls back to an in-process compile when no server is listening.
/// </summary>
public class CompileServerE2ETests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void TwoSequentialCompiles_ThroughOneServerProcess_SecondReportsWarmPhaseTimes()
    {
        // Short name: on macOS a pipe is backed by a Unix domain socket at $TMPDIR/CoreFxPipe_<name>,
        // whose path is capped at 104 chars, so keep the suffix short.
        var pipeName = "spy-e2e-" + Guid.NewGuid().ToString("N")[..8];
        var source = _ws.WriteSpy("""
            def main():
                print("hello from the server")
            """);

        using var server = StartServer(pipeName);
        try
        {
            WaitUntilListening(pipeName, server);

            var first = SendCompile(pipeName, source, _ws.PathFor("out1.exe"));
            var second = SendCompile(pipeName, source, _ws.PathFor("out2.exe"));

            // Both compiles succeed through the one server process.
            first.ExitCode.Should().Be(0, "first compile should succeed: {0}", first.Stderr);
            second.ExitCode.Should().Be(0, "second compile should succeed: {0}", second.Stderr);

            // Ordinals prove a single reused process served both requests.
            first.CompileOrdinal.Should().Be(1);
            second.CompileOrdinal.Should().Be(2);
            first.Warm.Should().BeFalse("the process's first compile is cold");
            second.Warm.Should().BeTrue("subsequent compiles in the same process are warm");

            // The warm compile reports real per-phase timings (the D2 measurable).
            second.PhaseMillis.Should().NotBeEmpty("the warm compile must report per-phase timings");
            second.TotalMillis.Should().BeGreaterThan(0);
        }
        finally
        {
            TryShutdown(pipeName);
            StopServer(server);
        }
    }

    [Fact]
    public void Client_FallsBackToInProcess_WhenNoServerIsRunning()
    {
        // A pipe nobody is listening on: the build must still succeed in-process and say so.
        var deadPipe = "spy-absent-" + Guid.NewGuid().ToString("N")[..8];
        var source = _ws.WriteSpy("""
            def main():
                print("compiled in-process")
            """);
        var output = _ws.PathFor("fallback.exe");

        var result = CliTestHarness.Invoke($"build \"{source}\" --output \"{output}\" --server={deadPipe}");

        result.ExitCode.Should().Be(0, "the compile must succeed via the in-process fallback: {0}", result.Err);
        result.Err.Should().Contain("no compile server", "the fallback must be announced");
        result.Err.Should().Contain("compiling in-process");
        File.Exists(output).Should().BeTrue("the in-process fallback still produces the binary");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Process StartServer(string pipeName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        psi.ArgumentList.Add("server");
        psi.ArgumentList.Add("--pipe");
        psi.ArgumentList.Add(pipeName);
        // Small idle timeout so a leaked server self-terminates even if shutdown/kill is missed.
        psi.ArgumentList.Add("--idle-timeout");
        psi.ArgumentList.Add("60");

        var process = Process.Start(psi)!;
        // Drain the pipes so the child never blocks on a full stdout/stderr buffer.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void WaitUntilListening(string pipeName, Process server)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            server.HasExited.Should().BeFalse("the server process must stay alive while starting");

            if (CompileServerClient.TrySend(
                    pipeName,
                    new CompileServerRequest { Command = CompileServerProtocol.CommandPing },
                    out var response,
                    out _)
                && response != null)
            {
                return;
            }

            Thread.Sleep(200);
        }

        throw new Xunit.Sdk.XunitException($"server on pipe '{pipeName}' never started listening");
    }

    private static CompileServerResponse SendCompile(string pipeName, string sourcePath, string outputPath)
    {
        var request = new CompileServerRequest
        {
            Command = CompileServerProtocol.CommandCompile,
            Input = sourcePath,
            Output = outputPath,
            OutputType = "exe",
            Configuration = "Debug",
            WorkingDirectory = Path.GetDirectoryName(sourcePath),
        };

        CompileServerClient.TrySend(pipeName, request, out var response, out var reason)
            .Should().BeTrue("the compile request should reach the server ({0})", reason);
        response.Should().NotBeNull();
        return response!;
    }

    private static void TryShutdown(string pipeName)
    {
        CompileServerClient.TrySend(
            pipeName,
            new CompileServerRequest { Command = CompileServerProtocol.CommandShutdown },
            out _,
            out _);
    }

    private static void StopServer(Process server)
    {
        try
        {
            if (!server.WaitForExit(10_000))
            {
                server.Kill(entireProcessTree: true);
                server.WaitForExit(5_000);
            }
        }
        catch
        {
            // Best-effort teardown.
        }
    }
}
