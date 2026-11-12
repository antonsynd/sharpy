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
/// recording both measured errnos and why; the rows are cells of
/// <c>docs/deviations.yaml</c> <c>socket-errno-is-dotnet-socket-error</c>); a row whose probe now agrees,
/// or whose divergence moved, is red (stale);</item>
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

    /// <summary>
    /// One trigger: a body that raises from a socket call, the same text on both sides (double-quoted
    /// strings and a bytes literal spell alike in python and Sharpy). Every socket a body opens comes from
    /// <c>_s(...)</c> or is passed to <c>_keep(...)</c>, so the program closes it after the probe.
    /// </summary>
    internal sealed record Probe(string Id, string Body);

    /// <summary>
    /// The trigger roster: every way a socket call fails that a Linux runner produces without a network.
    /// A probe that needs a refused or in-use port takes it from a socket it bound itself, so no fixed
    /// port is assumed free.
    /// </summary>
    internal static readonly IReadOnlyList<Probe> Probes = new[]
    {
        new Probe("bad_family", "_s(999)"),
        new Probe("bad_type", "_s(socket.AF_INET, 999)"),
        new Probe("bad_proto_stream", "_s(socket.AF_INET, socket.SOCK_STREAM, 999)"),
        new Probe("bad_proto_dgram", "_s(socket.AF_INET, socket.SOCK_DGRAM, 999)"),
        new Probe("connect_refused",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\n_s().connect((\"127.0.0.1\", a.getsockname()[1]))"),
        new Probe("bind_in_use",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\n_s().bind((\"127.0.0.1\", a.getsockname()[1]))"),
        new Probe("bind_not_local", "_s().bind((\"192.0.2.1\", 0))"),
        new Probe("bind_twice", "a = _s()\na.bind((\"127.0.0.1\", 0))\na.bind((\"127.0.0.1\", 0))"),
        new Probe("send_unconnected", "_s().send(b\"x\")"),
        new Probe("recv_unconnected", "_s().recv(1)"),
        new Probe("shutdown_unconnected", "_s().shutdown(socket.SHUT_RDWR)"),
        new Probe("send_after_shut_wr",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\nb = _s()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))\n"
            + "_keep(a.accept()[0])\nb.shutdown(socket.SHUT_WR)\nb.send(b\"x\")"),
        new Probe("listen_dgram", "_s(socket.AF_INET, socket.SOCK_DGRAM).listen(1)"),
        new Probe("sendto_dgram_too_big",
            "_s(socket.AF_INET, socket.SOCK_DGRAM).sendto(b\"x\" * 70000, (\"127.0.0.1\", 9))"),
        new Probe("nb_recv_dgram",
            "u = _s(socket.AF_INET, socket.SOCK_DGRAM)\nu.bind((\"127.0.0.1\", 0))\nu.setblocking(False)\nu.recv(10)"),
        new Probe("nb_accept",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.setblocking(False)\na.accept()"),
        new Probe("nb_connect",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\nb = _s()\nb.setblocking(False)\nb.connect((\"127.0.0.1\", a.getsockname()[1]))"),
        new Probe("recv_timeout",
            "u = _s(socket.AF_INET, socket.SOCK_DGRAM)\nu.bind((\"127.0.0.1\", 0))\nu.settimeout(0.2)\nu.recv(10)"),
        new Probe("accept_timeout",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.settimeout(0.2)\na.accept()"),
        // listen(0) admits one connection on Linux; the next SYN is dropped, so a timed connect expires.
        new Probe("connect_timeout",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(0)\n_s().connect((\"127.0.0.1\", a.getsockname()[1]))\n"
            + "c = _s()\nc.settimeout(0.3)\nc.connect((\"127.0.0.1\", a.getsockname()[1]))"),
        // A valid option round-trips on both sides; the recv that follows is the raise compared.
        new Probe("sockopt_valid",
            "s = _s()\ns.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)\nif s.getsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR) != 0:\n    s.recv(1)"),
    }.Concat(SockoptProbes()).ToArray();

    /// <summary>
    /// The unknown-option matrix, generated: {setsockopt, getsockopt} × {SOCK_STREAM, SOCK_DGRAM} × every
    /// level (SOL_SOCKET, IPPROTO_IP, IPPROTO_TCP, IPPROTO_UDP, an unknown one) with an option name no
    /// level defines (9999), plus an unknown level with a real name. Linux answers ENOPROTOOPT or
    /// EOPNOTSUPP by level and socket type; .NET refuses the option itself, so every cell is compared.
    /// The two <c>_real</c> rows are the twins the mapping must not rewrite: TCP_NODELAY on a UDP socket
    /// reaches the kernel, which answers setsockopt ENOPROTOOPT and getsockopt EOPNOTSUPP.
    /// </summary>
    private static IEnumerable<Probe> SockoptProbes()
    {
        var types = new[] { ("stream", "socket.SOCK_STREAM"), ("dgram", "socket.SOCK_DGRAM") };
        var targets = new[]
        {
            ("sol_socket", "socket.SOL_SOCKET, 9999"), ("ip", "socket.IPPROTO_IP, 9999"),
            ("tcp", "socket.IPPROTO_TCP, 9999"), ("udp", "socket.IPPROTO_UDP, 9999"),
            ("level9999", "9999, 9999"), ("level9999_name", "9999, socket.TCP_NODELAY"),
        };
        foreach (var (typeId, type) in types)
        {
            foreach (var (targetId, target) in targets)
            {
                yield return new Probe($"sockopt_set_{typeId}_{targetId}", $"_s(socket.AF_INET, {type}).setsockopt({target}, 1)");
                yield return new Probe($"sockopt_get_{typeId}_{targetId}", $"_s(socket.AF_INET, {type}).getsockopt({target})");
            }
        }
        yield return new Probe("sockopt_set_dgram_tcp_real", "_s(socket.AF_INET, socket.SOCK_DGRAM).setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)");
        yield return new Probe("sockopt_get_dgram_tcp_real", "_s(socket.AF_INET, socket.SOCK_DGRAM).getsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY)");
    }

    /// <summary>
    /// Probes whose errno still differs (cells of <c>docs/deviations.yaml</c>
    /// <c>socket-errno-is-dotnet-socket-error</c>): id → python's errno, Sharpy's errno, and the reason.
    /// Both numbers are RECORDED MEASUREMENTS of a known difference (CPython 3.15.2 and Sharpy, Linux),
    /// not expected values: the Sharpy one is pinned so a listed cell cannot drift unnoticed (91 → 0).
    /// A row whose probe now agrees, or whose either side moved, is red; so is a row naming no probe.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (string PythonErrno, string SharpyErrno, string Reason)> KnownDivergences =
        new System.Collections.Generic.Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["bad_type"] = ("22", "91", ".NET rejects the type itself with EPROTOTYPE; CPython's socket(2) gets EINVAL"),
            ["bad_proto_stream"] = ("22", "93", ".NET rejects the protocol itself with EPROTONOSUPPORT; CPython's socket(2) gets EINVAL"),
            ["bad_proto_dgram"] = ("22", "93", ".NET rejects the protocol itself with EPROTONOSUPPORT; CPython's socket(2) gets EINVAL"),
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
            else if (listed && pyErrno == shErrno)
                stale.Add($"{p.Id}: the cell now agrees (errno={pyErrno}); delete the row");
            else if (listed && (pyErrno != row.PythonErrno || shErrno != row.SharpyErrno))
                stale.Add($"{p.Id}: the divergence moved: row says python={row.PythonErrno} Sharpy={row.SharpyErrno}; measured python={pyErrno} Sharpy={shErrno}");
        }
        Assert.True(unlisted.Count == 0 && stale.Count == 0,
            "socket errno differential (CPython " + run.PythonVersion + "):\n"
            + "Unlisted divergences (fix at error.from_socket_exception, never by a row):\n  " + string.Join("\n  ", unlisted)
            + "\nStale KnownDivergences rows:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>A KnownDivergences row that names no probe is never checked, so it is red here.</summary>
    [Fact]
    public void KnownDivergences_NameOnlyProbes()
    {
        var ids = Probes.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(Probes.Count, ids.Count);
        var orphans = KnownDivergences.Keys.Where(k => !ids.Contains(k)).ToList();
        Assert.True(orphans.Count == 0, "KnownDivergences rows naming no probe: " + string.Join(", ", orphans));
    }

    /// <summary>Positive control: the probes the ruling names agree, so the comparison cannot pass vacuously.</summary>
    [SkippableFact]
    public void PositiveControl_PosixErrnosAgree()
    {
        var run = Execute();
        foreach (var (id, errno) in new[]
        {
            ("bad_family", "97"), ("connect_refused", "111"), ("bind_in_use", "98"), ("recv_unconnected", "107"),
            ("nb_connect", "115"), ("nb_accept", "11"), ("sockopt_set_stream_sol_socket", "92"),
            ("sockopt_get_dgram_tcp_real", "95"), ("recv_timeout", "None"),
        })
        {
            Assert.DoesNotContain(id, KnownDivergences.Keys);
            Assert.Equal(errno, run.Python[id].Errno);
            Assert.Equal(errno, run.Sharpy[id].Errno);
        }
    }

    /// <summary>
    /// The seam's fallbacks (no oracle): a name-resolution failure (a resolver is not deterministic on a
    /// runner) and a code whose NativeErrorCode is not positive keep the SocketError value, never .NET's
    /// NativeErrorCode, which there is an internal value on Unix (HostNotFound -131073,
    /// SocketError.SocketError -131074) or another errno (TryAgain 11, NoData 61) rather than an EAI_* code
    /// (docs/deviations.yaml socket-errno-is-dotnet-socket-error).
    /// </summary>
    [Theory]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.TryAgain)]
    [InlineData(SocketError.NoRecovery)]
    [InlineData(SocketError.NoData)]
    [InlineData(SocketError.SocketError)]
    [InlineData(SocketError.Success)]
    public void FromSocketException_Fallbacks_KeepTheSocketErrorValue(SocketError code)
    {
        var ex = new SocketException((int)code);
        Assert.Equal((int)code, global::Sharpy.SocketModule.Error.FromSocketException(ex).Errno);
    }

    /// <summary>A socket-call failure carries the platform errno: on Linux, the POSIX number CPython 3.15.2 reports.</summary>
    [SkippableTheory]
    [InlineData(SocketError.ConnectionRefused, 111)]
    [InlineData(SocketError.AddressAlreadyInUse, 98)]
    [InlineData(SocketError.AddressFamilyNotSupported, 97)]
    public void FromSocketException_SocketCallFailure_CarriesTheLinuxErrno(SocketError code, int linuxErrno)
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the pinned numbers are Linux's errno values.");
        var ex = new SocketException((int)code);
        Assert.Equal(linuxErrno, global::Sharpy.SocketModule.Error.FromSocketException(ex).Errno);
    }

    /// <summary>
    /// A .NET <c>TimedOut</c> is the socket's own timeout (errno None, python's <c>timed out</c>) or, where no
    /// timeout is set, the kernel's <c>ETIMEDOUT</c> (110 on Linux, python's <c>TimeoutError</c> errno). The
    /// kernel arm cannot be probed without an unanswering peer, so the rule is pinned here (no oracle).
    /// </summary>
    [SkippableTheory]
    [InlineData(true, null)]
    [InlineData(false, 110)]
    public void TimedOut_OwnTimeoutHasNoErrno_KernelTimeoutCarriesETIMEDOUT(bool ownTimeout, int? linuxErrno)
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the pinned number is Linux's errno value.");
        var t = global::Sharpy.SocketModule.SocketModuleModule._TimedOut(new SocketException((int)SocketError.TimedOut), ownTimeout);
        Assert.Equal(linuxErrno, t.Errno);
    }

    /// <summary>
    /// The call-site rule (<c>_call_error</c>), oracle-free: each (call site, .NET code) it rewrites, and
    /// the neighbours it must leave to the seam. "new descriptor" is the only witness of EMFILE: running out
    /// of descriptors needs a process-wide limit, so the end-to-end cell is measured by hand
    /// (<c>ulimit -n 1024</c>, opening sockets until one fails: CPython 24, Sharpy 24) and recorded in
    /// docs/deviations.yaml. A genuine system-wide ENFILE (23) shares <c>TooManyOpenSockets</c> and also
    /// reports 24 (a recorded cell, not producible here).
    /// </summary>
    [SkippableTheory]
    [InlineData("new descriptor", SocketError.TooManyOpenSockets, 24)]
    [InlineData("nonblocking connect", SocketError.WouldBlock, 115)]
    [InlineData("unknown option", SocketError.OperationNotSupported, 92)]
    [InlineData("new descriptor", SocketError.WouldBlock, 11)]
    [InlineData("nonblocking connect", SocketError.ConnectionRefused, 111)]
    [InlineData("nonblocking connect", SocketError.TooManyOpenSockets, 23)]
    [InlineData("unknown option", SocketError.ProtocolOption, 92)]
    [InlineData("unknown option", SocketError.WouldBlock, 11)]
    [InlineData("connect", SocketError.WouldBlock, 11)]
    public void CallError_RewritesOnlyItsCallSiteAndCode(string call, SocketError code, int linuxErrno)
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the pinned numbers are Linux's errno values.");
        var e = global::Sharpy.SocketModule.SocketModuleModule._CallError(new SocketException((int)code), call);
        Assert.Equal(linuxErrno, e.Errno);
    }

    /// <summary>
    /// The unknown-option rule rewrites only an option .NET refused because it has no enum member for the
    /// level or name. A member .NET has but does not map on Linux (<c>UseLoopback</c>, 64) is refused by .NET
    /// with 95 too and has no CPython counterpart, so it keeps 95 (docs/deviations.yaml cell 6); the
    /// differential cannot witness this guard (every probed defined option reaches the kernel), so it is
    /// pinned here, Sharpy only.
    /// </summary>
    [SkippableTheory]
    [InlineData(65535, 9999, true, 92)]
    [InlineData(65535, 9999, false, 92)]
    [InlineData(65535, (int)SocketOptionName.UseLoopback, true, 95)]
    [InlineData(65535, (int)SocketOptionName.UseLoopback, false, 95)]
    public void UnknownOption_OnlyAnOptionWithNoEnumMember_IsRewritten(int level, int optname, bool setting, int linuxErrno)
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the pinned numbers are Linux's errno values.");
        using var s = new global::Sharpy.SocketModule.Socket(2, 1, 0);
        var e = Assert.ThrowsAny<global::Sharpy.SocketModule.Error>(() =>
        {
            if (setting)
                s.Setsockopt(level, optname, 1);
            else
                s.Getsockopt(level, optname);
        });
        Assert.Equal(linuxErrno, e.Errno);
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
        var sb = new StringBuilder("import socket\n\n_opened = []\n\n");
        sb.Append("def _keep(s):\n    _opened.append(s)\n    return s\n\n");
        sb.Append("def _s(*args):\n    return _keep(socket.socket(*args))\n\n");
        sb.Append("def _close_all():\n    for s in _opened:\n        s.close()\n    _opened.clear()\n\n");
        foreach (var p in probes)
            sb.Append($"def p_{p.Id}():\n{Indent(p.Body, "    ")}\n\n");
        sb.Append("for _id in [" + string.Join(", ", probes.Select(p => $"'{p.Id}'")) + "]:\n");
        sb.Append("    try:\n        globals()['p_' + _id]()\n        print('OUT', _id, '-', '-')\n");
        sb.Append("    except OSError as e:\n        print('OUT', _id, type(e).__name__, e.errno)\n");
        sb.Append("    finally:\n        _close_all()\n");
        return sb.ToString();
    }

    internal static string SharpyProgram(IReadOnlyList<Probe> probes)
    {
        var sb = new StringBuilder("import socket\n\n_opened: list[socket.socket] = []\n\n");
        sb.Append("def _keep(s: socket.socket) -> socket.socket:\n    _opened.append(s)\n    return s\n\n");
        // Literal defaults, as socket.socket's own: a default naming socket.AF_INET is SPY0908 (CS1736) today.
        sb.Append("def _s(family: int = 2, sock_type: int = 1, proto: int = 0) -> socket.socket:\n"
                  + "    return _keep(socket.socket(family, sock_type, proto))\n\n");
        sb.Append("def _close_all() -> None:\n    for s in _opened:\n        s.close()\n    _opened.clear()\n\n");
        foreach (var p in probes)
            sb.Append($"def p_{p.Id}() -> None:\n{Indent(p.Body, "    ")}\n\n");
        sb.Append("def main() -> None:\n");
        foreach (var p in probes)
        {
            sb.Append($"    try:\n        p_{p.Id}()\n        print(\"OUT {p.Id} - -\")\n");
            sb.Append($"    except socket.error as e:\n        print(f\"OUT {p.Id} {{type(e).__name__}} {{e.errno}}\")\n");
            sb.Append($"    except Exception as e:\n        print(f\"OUT {p.Id} ESCAPE {{type(e).__name__}}\")\n");
            sb.Append("    finally:\n        _close_all()\n");
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
