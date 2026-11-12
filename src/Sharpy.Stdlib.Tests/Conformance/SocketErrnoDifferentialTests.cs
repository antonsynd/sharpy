using System.Net.Sockets;
using System.Text;
using Sharpy.Stdlib.Tests.Integration;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The errno contract of <c>socket</c> (owner ruling 2026-11-12, "POSIX value"): <c>e.errno</c> on a
/// <c>socket.error</c> raised for a failed socket call is the number CPython reports on the same platform.
/// Every row of <see cref="Probes"/> is a trigger that fails deterministically on a Linux host with no
/// external network; each runs as python3 and as Sharpy in ONE program per side, and the printed errno is
/// compared. The verdict is keyed on the measured outcome, never on a cell:
/// <list type="bullet">
/// <item>a probe whose errno differs is red unless it is a row of <see cref="KnownDivergences"/> (each
/// stating python's errno and why; the rows are cells of
/// <c>docs/deviations.yaml</c> <c>socket-errno-is-dotnet-socket-error</c>); a row whose probe now agrees,
/// or whose python errno moved, is red (stale);</item>
/// <item>a CLR exception escaping the Sharpy probe, or a python probe that does not raise, is red.</item>
/// </list>
/// The oracle is CPython on Linux: the kernel's errno is the value compared, so off Linux the
/// differential is SKIPPED; with no python3 it is SKIPPED, and FAILS under
/// <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c>.
/// </summary>
public class SocketErrnoDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public SocketErrnoDifferentialTests(ITestOutputHelper output) => _output = output;

    /// <summary>One trigger: a python body and its Sharpy twin, each raising from a socket call.</summary>
    internal sealed record Probe(string Id, string Python, string Sharpy);

    /// <summary>
    /// The trigger roster: every way a socket call fails that a Linux runner produces without a network.
    /// A probe that needs a refused or in-use port takes it from a socket it bound itself, so no fixed
    /// port is assumed free.
    /// </summary>
    internal static readonly IReadOnlyList<Probe> Probes = new[]
    {
        new Probe("bad_family",
            "socket.socket(999)",
            "socket.socket(999)"),
        new Probe("bad_type",
            "socket.socket(socket.AF_INET, 999)",
            "socket.socket(socket.AF_INET, 999)"),
        new Probe("bad_proto_stream",
            "socket.socket(socket.AF_INET, socket.SOCK_STREAM, 999)",
            "socket.socket(socket.AF_INET, socket.SOCK_STREAM, 999)"),
        new Probe("bad_proto_dgram",
            "socket.socket(socket.AF_INET, socket.SOCK_DGRAM, 999)",
            "socket.socket(socket.AF_INET, socket.SOCK_DGRAM, 999)"),
        new Probe("connect_refused",
            "a = socket.socket()\na.bind(('127.0.0.1', 0))\nb = socket.socket()\nb.connect(('127.0.0.1', a.getsockname()[1]))",
            "a = socket.socket()\na.bind((\"127.0.0.1\", 0))\nb = socket.socket()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))"),
        new Probe("bind_in_use",
            "a = socket.socket()\na.bind(('127.0.0.1', 0))\na.listen(1)\nb = socket.socket()\nb.bind(('127.0.0.1', a.getsockname()[1]))",
            "a = socket.socket()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\nb = socket.socket()\nb.bind((\"127.0.0.1\", a.getsockname()[1]))"),
        new Probe("bind_not_local",
            "socket.socket().bind(('192.0.2.1', 0))",
            "socket.socket().bind((\"192.0.2.1\", 0))"),
        new Probe("send_unconnected",
            "socket.socket().send(b'x')",
            "socket.socket().send(b\"x\")"),
        new Probe("recv_unconnected",
            "socket.socket().recv(1)",
            "socket.socket().recv(1)"),
        new Probe("listen_dgram",
            "socket.socket(socket.AF_INET, socket.SOCK_DGRAM).listen(1)",
            "socket.socket(socket.AF_INET, socket.SOCK_DGRAM).listen(1)"),
        new Probe("recv_timeout",
            "u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)\nu.bind(('127.0.0.1', 0))\nu.settimeout(0.2)\nu.recv(10)",
            "u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)\nu.bind((\"127.0.0.1\", 0))\nu.settimeout(0.2)\nu.recv(10)"),
        new Probe("accept_timeout",
            "a = socket.socket()\na.bind(('127.0.0.1', 0))\na.listen(1)\na.settimeout(0.2)\na.accept()",
            "a = socket.socket()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.settimeout(0.2)\na.accept()"),
    };

    /// <summary>
    /// Probes whose errno still differs: id → python's errno and the reason. Never Sharpy's value — it is
    /// not asserted, only that it still differs (a row that agrees is stale and red).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string PythonErrno, string Reason)> KnownDivergences =
        new System.Collections.Generic.Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["bad_type"] = ("22", ".NET validates the type before socket(2) and reports EPROTOTYPE (91); CPython's socket(2) gets EINVAL"),
            ["bad_proto_stream"] = ("22", ".NET reports EPROTONOSUPPORT (93) for an unknown protocol; CPython's socket(2) gets EINVAL"),
            ["bad_proto_dgram"] = ("22", ".NET reports EPROTONOSUPPORT (93) for an unknown protocol; CPython's socket(2) gets EINVAL"),
            ["recv_timeout"] = ("None", "python's TimeoutError carries no errno; Sharpy's errno is an int, and the timeout sites pass the SocketError value"),
            ["accept_timeout"] = ("None", "python's TimeoutError carries no errno; Sharpy's errno is an int, 0 on the select-timeout path"),
        };

    private static readonly object Gate = new();
    private static Run? _run;

    private static Run Execute()
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(),
            "the socket errno differential compares the Linux kernel's errno with CPython on Linux, so it is SKIPPED off Linux.");
        var python = PythonInterpreter.RequireOrSkip();
        lock (Gate)
            return _run ??= Run.Execute(python, Probes);
    }

    [SkippableFact]
    public void EveryProbe_RaisesOnBothSides_WithNoClrEscape()
    {
        var run = Execute();
        _output.WriteLine(run.Summary());
        Assert.True(run.Failures.Count == 0, "socket errno differential:\n  " + string.Join("\n  ", run.Failures));
        foreach (var p in Probes)
        {
            Assert.True(run.Python.TryGetValue(p.Id, out var py) && py.Type != "-", $"{p.Id}: python did not raise ({py})");
            Assert.True(run.Sharpy.TryGetValue(p.Id, out var sh) && sh.Type != "-", $"{p.Id}: Sharpy did not raise ({sh})");
            Assert.False(sh.Type == "ESCAPE", $"{p.Id}: a CLR exception escaped: {sh.Errno}");
        }
    }

    [SkippableFact]
    public void Errno_EqualsPython_ModuloKnownDivergences()
    {
        var run = Execute();
        _output.WriteLine(run.Summary());
        var unlisted = new System.Collections.Generic.List<string>();
        var stale = new System.Collections.Generic.List<string>();
        foreach (var p in Probes)
        {
            var py = run.Python.GetValueOrDefault(p.Id);
            var sh = run.Sharpy.GetValueOrDefault(p.Id);
            var pyErrno = py.Errno ?? "no outcome";
            var shErrno = sh.Errno ?? "no outcome";
            var listed = KnownDivergences.TryGetValue(p.Id, out var row);
            if (pyErrno != shErrno && !listed)
                unlisted.Add($"{p.Id}: python {py.Type} errno={pyErrno}, Sharpy {sh.Type} errno={shErrno}");
            else if (listed && (pyErrno == shErrno || pyErrno != row.PythonErrno))
                stale.Add($"{p.Id}: row says python errno={row.PythonErrno}; measured python={pyErrno} Sharpy={shErrno}");
        }
        Assert.True(unlisted.Count == 0 && stale.Count == 0,
            "socket errno differential (CPython " + run.PythonVersion + "):\n"
            + "Unlisted divergences (fix at error.from_socket_exception, never by a row):\n  " + string.Join("\n  ", unlisted)
            + "\nStale KnownDivergences rows (the probe now agrees, or python's errno moved — update or delete the row):\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Positive control: the probes the ruling names agree, so the comparison cannot pass vacuously.</summary>
    [SkippableFact]
    public void PositiveControl_PosixErrnosAgree()
    {
        var run = Execute();
        foreach (var (id, errno) in new[] { ("bad_family", "97"), ("connect_refused", "111"), ("bind_in_use", "98"), ("recv_unconnected", "107") })
        {
            Assert.DoesNotContain(id, KnownDivergences.Keys);
            Assert.Equal(errno, run.Python[id].Errno);
            Assert.Equal(errno, run.Sharpy[id].Errno);
        }
    }

    /// <summary>
    /// A name-resolution failure (no oracle: a resolver is not deterministic on a runner) carries the
    /// SocketError value, never .NET's NativeErrorCode, which on Unix is an internal value rather than an
    /// EAI_* code (docs/deviations.yaml socket-errno-is-dotnet-socket-error). A socket-call failure carries
    /// NativeErrorCode.
    /// </summary>
    [Theory]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.TryAgain)]
    [InlineData(SocketError.NoRecovery)]
    [InlineData(SocketError.NoData)]
    public void FromSocketException_NameResolutionFailure_KeepsTheSocketErrorValue(SocketError code)
    {
        var ex = new SocketException((int)code);
        Assert.Equal((int)code, global::Sharpy.SocketModule.Error.FromSocketException(ex).Errno);
    }

    [Theory]
    [InlineData(SocketError.ConnectionRefused)]
    [InlineData(SocketError.AddressAlreadyInUse)]
    [InlineData(SocketError.AddressFamilyNotSupported)]
    public void FromSocketException_SocketCallFailure_CarriesTheNativeErrorCode(SocketError code)
    {
        var ex = new SocketException((int)code);
        Assert.Equal(ex.NativeErrorCode, global::Sharpy.SocketModule.Error.FromSocketException(ex).Errno);
    }

    internal readonly record struct Outcome(string Type, string? Errno);

    internal sealed class Run
    {
        public System.Collections.Generic.Dictionary<string, Outcome> Python { get; } = new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<string, Outcome> Sharpy { get; } = new(StringComparer.Ordinal);
        public System.Collections.Generic.List<string> Failures { get; } = new();
        public string PythonVersion { get; private init; } = "";

        public static Run Execute(PythonInterpreter python, IReadOnlyList<Probe> probes)
        {
            var run = new Run { PythonVersion = python.Version };
            var work = SysPath.Combine(SysPath.GetTempPath(), "sharpy-socket-errno-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var script = SysPath.Combine(work, "socket_errno.py");
                File.WriteAllText(script, PythonProgram(probes));
                try
                {
                    Parse(python.RunBatch(script, Array.Empty<string>(), timeoutMilliseconds: 120_000), run.Python);
                }
                catch (InvalidOperationException ex)
                {
                    run.Failures.Add("the python program failed: " + ex.Message);
                }

                using var runner = new Runner();
                var result = runner.Execute(SharpyProgram(probes));
                if (!result.Success && result.CompilationErrors.Count > 0)
                    run.Failures.Add("the Sharpy program did not compile:\n    " + string.Join("\n    ", result.CompilationErrors.Take(10)));
                else if (result.TimedOut)
                    run.Failures.Add("the Sharpy program timed out");
                else if (!result.Success)
                    run.Failures.Add("the Sharpy program failed: " + result.Failure);
                Parse(result.StandardOutput.Replace("\r\n", "\n").Split('\n'), run.Sharpy);
            }
            finally
            {
                try
                { Directory.Delete(work, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (var p in probes)
            {
                if (!run.Python.ContainsKey(p.Id))
                    run.Failures.Add($"{p.Id}: python printed no outcome");
                if (!run.Sharpy.ContainsKey(p.Id))
                    run.Failures.Add($"{p.Id}: Sharpy printed no outcome");
            }
            return run;
        }

        public string Summary() =>
            $"socket errno @ CPython {PythonVersion}: "
            + string.Join("; ", Python.Keys.OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => $"{k} python={Python[k].Type}/{Python[k].Errno} sharpy={Sharpy.GetValueOrDefault(k).Type}/{Sharpy.GetValueOrDefault(k).Errno}"));

        /// <summary>
        /// Lines are <c>OUT &lt;id&gt; &lt;type&gt; &lt;errno&gt;</c> (type <c>-</c>: no raise; type
        /// <c>ESCAPE</c>: a CLR exception, its name in the last field); anything else is a program's own output.
        /// </summary>
        private static void Parse(IEnumerable<string> lines, System.Collections.Generic.Dictionary<string, Outcome> into)
        {
            foreach (var line in lines)
            {
                var parts = line.Trim().Split(' ');
                if (parts.Length == 4 && parts[0] == "OUT")
                    into[parts[1]] = new Outcome(parts[2], parts[3]);
            }
        }
    }

    private static string Indent(string body, string pad) =>
        string.Join("\n", body.Split('\n').Select(l => pad + l));

    internal static string PythonProgram(IReadOnlyList<Probe> probes)
    {
        var sb = new StringBuilder("import socket\n\n");
        foreach (var p in probes)
            sb.Append($"def p_{p.Id}():\n{Indent(p.Python, "    ")}\n\n");
        sb.Append("for _id in [" + string.Join(", ", probes.Select(p => $"'{p.Id}'")) + "]:\n");
        sb.Append("    try:\n        globals()['p_' + _id]()\n        print('OUT', _id, '-', '-')\n");
        sb.Append("    except OSError as e:\n        print('OUT', _id, type(e).__name__, e.errno)\n");
        return sb.ToString();
    }

    internal static string SharpyProgram(IReadOnlyList<Probe> probes)
    {
        var sb = new StringBuilder("import socket\n\n");
        foreach (var p in probes)
            sb.Append($"def p_{p.Id}() -> None:\n{Indent(p.Sharpy, "    ")}\n\n");
        sb.Append("def main() -> None:\n");
        foreach (var p in probes)
        {
            sb.Append($"    try:\n        p_{p.Id}()\n        print(\"OUT {p.Id} - -\")\n");
            sb.Append($"    except socket.error as e:\n        print(f\"OUT {p.Id} {{type(e).__name__}} {{e.errno}}\")\n");
            sb.Append($"    except Exception as e:\n        print(f\"OUT {p.Id} ESCAPE {{type(e).__name__}}\")\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compiles and runs the generated Sharpy program with the Stdlib referenced, and System.Net.Sockets,
    /// whose <c>Socket</c> the Stdlib's <c>socket</c> wrapper exposes in its generated C#.
    /// </summary>
    private sealed class Runner : StdlibIntegrationTestBase
    {
        public Runner() : base(new NullOutput())
        {
            MuteCompilePath = true;
        }

        protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths() =>
            base.GetAdditionalReferenceAssemblyPaths().Append(typeof(System.Net.Sockets.Socket).Assembly.Location);

        public (bool Success, IReadOnlyList<string> CompilationErrors, bool TimedOut, string StandardOutput, string Failure) Execute(string source)
        {
            var r = CompileAndExecute(source, "socket_errno.spy", executionTimeoutMs: 120_000);
            return (r.Success, r.CompilationErrors, r.TimedOut, r.StandardOutput, $"exit {r.ExitCode}; {r.Exception?.Message}\n{r.StandardError}");
        }

        private sealed class NullOutput : ITestOutputHelper
        {
            public void WriteLine(string message) { }
            public void WriteLine(string format, params object[] args) { }
        }
    }
}
