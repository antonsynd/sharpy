using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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

        result.ExitCode.Should().Be(0, "the compile must succeed via the in-process fallback: {0}", result.StdErr);
        result.StdErr.Should().Contain("no compile server", "the fallback must be announced");
        result.StdErr.Should().Contain("compiling in-process");
        File.Exists(output).Should().BeTrue("the in-process fallback still produces the binary");
    }

    /// <summary>
    /// The server route cannot carry the logger or metrics globals (the request has no channel for
    /// them), so with a LIVE server <c>build|run --server</c> given one compiles in process, says so,
    /// and honours it; they were dropped silently before (P46 verify, #2223). Control: the same
    /// command without the option is compiled by the server — the server's compile ordinal counts it,
    /// and does not count the probe.
    /// </summary>
    [Theory]
    [InlineData("build", "--log-level Info")]
    [InlineData("build", "--log-level Info --log-file {log}")]
    [InlineData("build", "--metrics-format json")]
    [InlineData("build", "--metrics-output {metrics}")]
    [InlineData("run", "--log-level Info")]
    [InlineData("run", "--metrics-output {metrics}")]
    public void ServerRoute_LoggerAndMetricsGlobals_CompileInProcess_AndAreHonoured(string command, string option)
    {
        var pipeName = "spy-g-" + Guid.NewGuid().ToString("N")[..8];
        var source = _ws.WriteSpy("def main():\n    print(\"via\")\n");
        var log = _ws.PathFor("probe.log");
        var metrics = _ws.PathFor("metrics.txt");
        var line = command == "build" ? $"build \"{source}\" --output \"{_ws.PathFor("a.exe")}\"" : $"run \"{source}\"";

        using var server = StartServer(pipeName);
        try
        {
            WaitUntilListening(pipeName, server);

            var control = CliTestHarness.Invoke($"{line} --server={pipeName}");
            control.ExitCode.Should().Be(0, control.StdErr);
            control.StdErr.Should().NotContain("compiling in-process", "control: the route reaches the live server");

            var result = CliTestHarness.Invoke($"{line} --server={pipeName} {option.Replace("{log}", $"\"{log}\"").Replace("{metrics}", $"\"{metrics}\"")}");
            var all = result.StdOut + result.StdErr;
            result.ExitCode.Should().Be(0, all);
            result.StdErr.Should().Contain("cannot be carried to the compile server; compiling in-process");
            if (option.Contains("{log}"))
                ReadShared(log).Should().Contain("[INFO]");
            else if (option.Contains("--log-level"))
                all.Should().Contain("[INFO]");
            else if (option.Contains("{metrics}"))
                File.Exists(metrics).Should().BeTrue("--metrics-output is honoured in process");
            else
                result.StdOut.Should().Contain("\"total_duration_ms\"");

            // The server compiled the control (ordinal 1) and not the probe: this is its second compile.
            SendCompile(pipeName, source, _ws.PathFor("ordinal.exe")).CompileOrdinal.Should().Be(2);
        }
        finally
        {
            TryShutdown(pipeName);
            StopServer(server);
        }
    }

    /// <summary>
    /// <c>--verbose</c> IS carried on the server route: both arms print the server's route-and-timing
    /// summary. <c>run --server</c> sent the request through its own path and printed nothing extra
    /// (P46 verify, #2223). Control: without <c>--verbose</c> the summary is absent.
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("run")]
    public void ServerRoute_Verbose_PrintsTheServerSummary_OnBothArms(string command)
    {
        var pipeName = "spy-v-" + Guid.NewGuid().ToString("N")[..8];
        var source = _ws.WriteSpy("def main():\n    print(\"via\")\n");
        var line = command == "build" ? $"build \"{source}\" --output \"{_ws.PathFor("v.exe")}\"" : $"run \"{source}\"";

        using var server = StartServer(pipeName);
        try
        {
            WaitUntilListening(pipeName, server);

            var control = CliTestHarness.Invoke($"{line} --server={pipeName}");
            control.ExitCode.Should().Be(0, control.StdErr);
            control.StdErr.Should().NotContain("compiling in-process", "control: the route reaches the live server")
                .And.NotContain("compiled via server on pipe", "control: no summary without --verbose");

            var result = CliTestHarness.Invoke($"{line} --server={pipeName} --verbose");
            result.ExitCode.Should().Be(0, result.StdOut + result.StdErr);
            result.StdErr.Should().NotContain("compiling in-process", "--verbose is carried to the server route");
            result.StdErr.Should().Contain($"compiled via server on pipe '{pipeName}'");
        }
        finally
        {
            TryShutdown(pipeName);
            StopServer(server);
        }
    }

    /// <summary>
    /// Deterministic base reproductions of the server request sequence, keyed by request; the value
    /// is the server's observed <see cref="RunFingerprint"/> at base. Each row drains in P54 Phase 2
    /// (#2250).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RequestSequenceKnownRed = new Dictionary<string, string>
    {
        // Empty since P54 Phase 2 task 1 (#2250): request 3 answered SPY0908(CS0400) @ 486c9a88a
        // because request 2's reference stayed visible to the process-wide scans; with every CLR name
        // resolved against the request's own AssemblyVisibility it answers SPY0300, exit 1, as a
        // fresh process does — the row reported stale and drained.
    };

    /// <summary>
    /// P54 (#2250) cell 4: a compile is a pure function of its inputs, so ONE server answering
    /// <c>run main.spy</c> (no <c>-r</c>) → <c>run -r Tablegreeter.dll main.spy</c> → <c>run main.spy</c>
    /// answers each request as a fresh <c>sharpyc</c> process answers the same command: exit code and
    /// diagnostic code set (and the program's line when it runs). Requests 1–2 are the positive
    /// controls (SPY0300; runs); request 3 was a <see cref="RequestSequenceKnownRed"/> row at base.
    /// Every client is a child process, so the library is never loaded into the test host.
    /// </summary>
    [Fact]
    public void RequestSequence_NoRef_Ref_NoRef_AnswersAsThreeFreshProcesses()
    {
        var library = new ReferenceLibraryFixture();
        _ws.WriteSpy("from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n");
        var requests = new (string Id, string[] Args)[]
        {
            ("1: run main.spy", new[] { "run", "main.spy" }),
            ("2: run -r Tablegreeter.dll main.spy", new[] { "run", "-r", library.DllPath, "main.spy" }),
            ("3: run main.spy", new[] { "run", "main.spy" }),
        };

        var pipeName = "spy-seq-" + Guid.NewGuid().ToString("N")[..8];
        var failures = new List<string>();
        var oracles = new List<string>();
        using var server = StartServer(pipeName);
        try
        {
            WaitUntilListening(pipeName, server);

            foreach (var (id, args) in requests)
            {
                var viaServer = ExecCli(args.Append($"--server={pipeName}").ToArray());
                var fresh = ExecCli(args);
                var served = RunFingerprint(viaServer);
                var oracle = RunFingerprint(fresh);
                oracles.Add(oracle);
                var report = $"server: {served}\n{viaServer.StdOut}{viaServer.StdErr}\nfresh: {oracle}\n{fresh.StdOut}{fresh.StdErr}";

                viaServer.StdErr.Should().NotContain("compiling in-process", $"[{id}] the request reaches the live server");

                if (RequestSequenceKnownRed.TryGetValue(id, out var recorded))
                {
                    if (served != recorded || served == oracle)
                        failures.Add($"[{id}] KnownRed row is stale — drain it (#2250): recorded '{recorded}'\n{report}");
                }
                else if (served != oracle)
                {
                    failures.Add($"[{id}] the server answers unlike a fresh process\n{report}");
                }
            }
        }
        finally
        {
            TryShutdown(pipeName);
            StopServer(server);
        }

        // The fresh-process oracle is what the issue says it is, so parity is not vacuous: no -r is
        // an unresolved import, -r runs the program.
        oracles.Should().Equal("exit 1 | SPY0300", "exit 0 | - | hello from lib", "exit 1 | SPY0300");
        failures.Should().BeEmpty(string.Join("\n\n", failures));
    }

    /// <summary>
    /// <c>exit N | sorted distinct diagnostic codes (a SPY0908 spelled with the C# error it wraps) |
    /// the program's line when it ran</c>.
    /// </summary>
    private static string RunFingerprint(ProcessResult result)
    {
        var all = result.StdOut + result.StdErr;
        var codes = Regex.Matches(all, @"(?:error|warning)\[(SPY\d{4})\]: ([^\n]*)")
            .Select(m => m.Groups[1].Value == "SPY0908"
                ? $"SPY0908({Regex.Match(m.Groups[2].Value, @"\bCS\d{4}\b").Value})"
                : m.Groups[1].Value)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        var fingerprint = $"exit {result.ExitCode} | {(codes.Count == 0 ? "-" : string.Join(",", codes))}";
        return all.Contains("hello from lib", StringComparison.Ordinal) ? fingerprint + " | hello from lib" : fingerprint;
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

    /// <summary>A <c>sharpyc</c> child process in the workspace — a fresh process per call.</summary>
    private ProcessResult ExecCli(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _ws.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"sharpyc {string.Join(' ', args)} did not exit within 2 minutes");
        }

        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
