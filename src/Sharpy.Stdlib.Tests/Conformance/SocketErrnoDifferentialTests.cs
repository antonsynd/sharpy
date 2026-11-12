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
        // An accepted socket is blocking unless a default timeout is set, whatever the listener's
        // timeout or mode: the peer sends after 0.6 s, and the accepted socket must receive it rather
        // than time out at the listener's 0.3 s (or raise EAGAIN off a non-blocking listener).
        new Probe("accepted_recv_listener_timeout",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.settimeout(0.3)\nb = _s()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))\nconn = _keep(a.accept()[0])\n_send_later(b, 0.6)\n"
            + "if conn.recv(10) == b\"hi\":\n    _s().recv(1)"),
        new Probe("accepted_recv_listener_nonblocking",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.setblocking(False)\nb = _s()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))\ntime.sleep(0.1)\nconn = _keep(a.accept()[0])\n_send_later(b, 0.6)\n"
            + "if conn.gettimeout() is None and conn.getblocking() and conn.recv(10) == b\"hi\":\n    _s().recv(1)"),
        new Probe("accepted_gettimeout_no_default",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.settimeout(0.3)\nb = _s()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))\nconn = _keep(a.accept()[0])\n"
            + "if conn.gettimeout() is None and conn.getblocking():\n    _s().recv(1)"),
        new Probe("accepted_gettimeout_default",
            "a = _s()\na.bind((\"127.0.0.1\", 0))\na.listen(1)\na.settimeout(0.3)\nb = _s()\nb.connect((\"127.0.0.1\", a.getsockname()[1]))\nsocket.setdefaulttimeout(1.5)\nconn = _keep(a.accept()[0])\n"
            + "socket.setdefaulttimeout(None)\nif conn.gettimeout() == 1.5:\n    _s().recv(1)"),
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
        // Options that ARE .NET enum members but have no Linux mapping: .NET handles them itself (95, or a
        // success for ExclusiveAddressUse), never the kernel, while python passes the same numbers to the
        // kernel. A recv on the unconnected socket follows, so a call that succeeds still raises (107).
        var unmapped = new[]
        {
            ("dontlinger", "socket.SOL_SOCKET, -129"), ("maxconnections", "socket.SOL_SOCKET, 2147483647"),
            ("useloopback", "socket.SOL_SOCKET, 64"), ("tcp64", "socket.IPPROTO_TCP, 64"),
            ("exclusiveaddressuse", "socket.SOL_SOCKET, -5"),
        };
        foreach (var (id, target) in unmapped)
        {
            yield return new Probe($"member_unmapped_set_{id}", $"s = _s()\ns.setsockopt({target}, 1)\ns.recv(1)");
            yield return new Probe($"member_unmapped_get_{id}", $"s = _s()\ns.getsockopt({target})\ns.recv(1)");
        }
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
            // Cell (6): a .NET option member with no Linux mapping is handled by .NET, never the kernel.
            ["member_unmapped_set_dontlinger"] = ("92", "95", ".NET refuses DontLinger (-129) itself; the kernel answers the raw number ENOPROTOOPT"),
            ["member_unmapped_get_dontlinger"] = ("92", "95", ".NET refuses DontLinger (-129) itself; the kernel answers the raw number ENOPROTOOPT"),
            ["member_unmapped_set_maxconnections"] = ("92", "95", ".NET refuses MaxConnections (2147483647) itself; the kernel answers ENOPROTOOPT"),
            ["member_unmapped_get_maxconnections"] = ("92", "95", ".NET refuses MaxConnections (2147483647) itself; the kernel answers ENOPROTOOPT"),
            ["member_unmapped_set_tcp64"] = ("92", "95", ".NET refuses IPPROTO_TCP option 64 itself; the kernel answers ENOPROTOOPT"),
            ["member_unmapped_get_tcp64"] = ("92", "95", ".NET refuses IPPROTO_TCP option 64 itself; the kernel answers ENOPROTOOPT"),
            ["member_unmapped_set_useloopback"] = ("107", "95", "SOL_SOCKET 64 is UseLoopback to .NET, refused with 95; to the Linux kernel it is a real option (on this kernel; an older one would answer 92), so python's call SUCCEEDS and the following recv raises 107"),
            ["member_unmapped_get_useloopback"] = ("107", "95", "SOL_SOCKET 64 is UseLoopback to .NET, refused with 95; to the Linux kernel it is a real option (on this kernel; an older one would answer 92), so python's call SUCCEEDS and the following recv raises 107"),
            ["member_unmapped_set_exclusiveaddressuse"] = ("92", "107", "SOL_SOCKET -5 is ExclusiveAddressUse, which .NET's setsockopt accepts on Linux: Sharpy's call SUCCEEDS and the following recv raises 107; the kernel answers the raw number ENOPROTOOPT"),
            ["member_unmapped_get_exclusiveaddressuse"] = ("92", "107", "SOL_SOCKET -5 is ExclusiveAddressUse, which .NET's getsockopt answers on Linux: Sharpy's call SUCCEEDS and the following recv raises 107; the kernel answers the raw number ENOPROTOOPT"),
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
    /// The call sites of the errno rule, each bound to its real entry point in the module. The connect
    /// site's rule depends on the socket, so each kind of socket it can fail on is its own site.
    /// </summary>
    public enum Site
    {
        ConnectStreamNonblocking, ConnectStreamBlocking, ConnectDgramNonblocking, ConnectDgramBlocking,
        UnknownOption, NewDescriptor,
    }

    private static global::Sharpy.SocketModule.Error ErrorAt(Site site, SocketException ex)
    {
        switch (site)
        {
            case Site.UnknownOption:
                return global::Sharpy.SocketModule.SocketModuleModule._UnknownOptionError(ex);
            case Site.NewDescriptor:
                return global::Sharpy.SocketModule.SocketModuleModule._NewDescriptorError(ex);
        }
        var stream = site is Site.ConnectStreamNonblocking or Site.ConnectStreamBlocking;
        using var sock = new Socket(AddressFamily.InterNetwork, stream ? SocketType.Stream : SocketType.Dgram,
            stream ? ProtocolType.Tcp : ProtocolType.Udp);
        sock.Blocking = site is Site.ConnectStreamBlocking or Site.ConnectDgramBlocking;
        return global::Sharpy.SocketModule.SocketModuleModule._ConnectError(ex, sock);
    }

    /// <summary>
    /// The call-site rule, oracle-free, through each site's own entry point: the .NET code it rewrites,
    /// and neighbours it must leave to the seam. A genuine system-wide ENFILE (23) shares
    /// <c>TooManyOpenSockets</c> and also reports 24 (a recorded cell, not producible here); EMFILE end to
    /// end is <see cref="NewDescriptor_OutOfDescriptors_EqualsPython"/>.
    /// </summary>
    [SkippableTheory]
    [InlineData(Site.NewDescriptor, SocketError.TooManyOpenSockets, 24)]
    [InlineData(Site.ConnectStreamNonblocking, SocketError.WouldBlock, 115)]
    [InlineData(Site.UnknownOption, SocketError.OperationNotSupported, 92)]
    [InlineData(Site.NewDescriptor, SocketError.WouldBlock, 11)]
    [InlineData(Site.ConnectStreamNonblocking, SocketError.ConnectionRefused, 111)]
    [InlineData(Site.ConnectStreamNonblocking, SocketError.TooManyOpenSockets, 23)]
    // Only a non-blocking stream connect can be in progress; a WouldBlock anywhere else is a real
    // EAGAIN (a datagram connect that cannot auto-bind a port: ConnectEagain_EqualsPython measures it).
    [InlineData(Site.ConnectStreamBlocking, SocketError.WouldBlock, 11)]
    [InlineData(Site.ConnectDgramNonblocking, SocketError.WouldBlock, 11)]
    [InlineData(Site.ConnectDgramBlocking, SocketError.WouldBlock, 11)]
    [InlineData(Site.UnknownOption, SocketError.ProtocolOption, 92)]
    [InlineData(Site.UnknownOption, SocketError.WouldBlock, 11)]
    [InlineData(Site.NewDescriptor, SocketError.OperationNotSupported, 95)]
    public void ErrnoRule_EachSiteRewritesOnlyItsOwnCode(Site site, SocketError code, int linuxErrno)
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the pinned numbers are Linux's errno values.");
        Assert.Equal(linuxErrno, ErrorAt(site, new SocketException((int)code)).Errno);
    }

    private const string EagainPython = """
        import socket
        held = []
        for p in (40000, 40001):
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.bind(("0.0.0.0", p))
            held.append(s)
        for mode in ("blocking", "nonblocking"):
            u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            u.setblocking(mode == "blocking")
            try:
                u.connect(("127.0.0.1", 9))
                print("OUT", mode, "-")
            except OSError as e:
                print("OUT", mode, e.errno)
        """;

    // The Sharpy program runs under the harness's own `dotnet exec`; it re-runs itself (its own
    // assembly, argv[0]) inside `unshare -n`, where the ephemeral range is narrowed, and the inner run's
    // output goes to the same stdout. The outer run then reports the inner run's exit code, which the test
    // requires to be 0 (permission was established beforehand, so any failure here is the probe's own).
    private const string EagainSharpy = """
        import socket
        from system import Environment
        from system.diagnostics import Process

        def probe() -> None:
            held: list[socket.socket] = []
            for p in [40000, 40001]:
                s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                s.bind(("0.0.0.0", p))
                held.append(s)
            for mode in ["blocking", "nonblocking"]:
                u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                u.setblocking(mode == "blocking")
                try:
                    u.connect(("127.0.0.1", 9))
                    print(f"OUT {mode} -")
                except socket.error as e:
                    print(f"OUT {mode} {e.errno}")

        def main() -> None:
            if Environment.get_environment_variable("SHARPY_EAGAIN_INNER") == "1":
                probe()
                return
            me: str = Environment.get_command_line_args()[0]
            script: str = "echo 40000 40001 > /proc/sys/net/ipv4/ip_local_port_range && SHARPY_EAGAIN_INNER=1 exec dotnet exec '" + me + "'"
            p = Process.start("unshare", f"-n sh -c \"{script}\"")
            p.wait_for_exit()
            print(f"INNER EXIT {p.exit_code}")
        """;

    private const string NamespacePortRange = "echo 40000 40001 > /proc/sys/net/ipv4/ip_local_port_range";

    /// <summary>Runs <c>sh -c <paramref name="command"/></c> inside <c>unshare -n</c>.</summary>
    private static (int Exit, string Out, string Err) RunInNetworkNamespace(string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("unshare")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "-n", "sh", "-c", command })
            psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEndAsync();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, stdout.Result, stderr);
    }

    private static System.Collections.Generic.List<string> OutLines(string stdout) =>
        stdout.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("OUT ", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// A real EAGAIN from <c>connect</c>: inside a private network namespace (<c>unshare -n</c>) whose
    /// ephemeral range is two ports, both bound, a UDP connect cannot auto-bind a port and Linux answers
    /// EAGAIN, which .NET reports as <c>WouldBlock</c> like an in-progress connect. CPython 3.15.2 reports 11
    /// for a blocking and a non-blocking socket; Sharpy must too (only a stream connect is in progress).
    /// The host's network is untouched.
    /// <para>It runs only where this (possibly unprivileged) process may create a network namespace and
    /// write its port range, which is most likely NOT the case on the CI runner; there the rule is carried by
    /// the oracle-free <see cref="ErrnoRule_EachSiteRewritesOnlyItsOwnCode"/> connect rows. Permission is
    /// probed first, on its own: only that probe failing is a skip. After it succeeds, each side's real run
    /// must exit 0 and print both lines, or the test FAILS with the output.</para>
    /// </summary>
    [SkippableFact]
    public void ConnectEagain_EqualsPython()
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "the namespace and the errno are Linux's.");
        TestPreconditions.RequireOrSkip(File.Exists("/usr/bin/unshare") || File.Exists("/bin/unshare"),
            "unshare(1) (util-linux) is not installed.");
        var python = PythonInterpreter.RequireOrSkip();
        var work = SysPath.Combine(SysPath.GetTempPath(), "sharpy-socket-eagain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var permission = RunInNetworkNamespace(NamespacePortRange);
            TestPreconditions.RequireOrSkip(permission.Exit == 0,
                $"this process may not create a network namespace and set its port range (`unshare -n sh -c '{NamespacePortRange}'` "
                + $"exit {permission.Exit}: {permission.Err.Trim()})");

            var script = SysPath.Combine(work, "eagain.py");
            File.WriteAllText(script, EagainPython);
            var pyRun = RunInNetworkNamespace($"{NamespacePortRange} && exec '{python.Executable}' '{script}'");
            Assert.True(pyRun.Exit == 0, $"the python probe failed (exit {pyRun.Exit}):\n{pyRun.Out}\n{pyRun.Err}");
            var py = OutLines(pyRun.Out);

            using var runner = new Runner();
            var result = runner.Execute(EagainSharpy);
            Assert.True(result.Success, "the Sharpy program failed: " + string.Join("\n", result.CompilationErrors.Take(10)) + result.Failure);
            Assert.True(result.StandardOutput.Contains("INNER EXIT 0", StringComparison.Ordinal),
                $"the Sharpy probe's inner run failed:\n{result.StandardOutput}\n{result.Failure}");
            var sh = OutLines(result.StandardOutput);
            _output.WriteLine($"python: {string.Join("; ", py)}; Sharpy: {string.Join("; ", sh)}");
            Assert.Equal(new[] { "OUT blocking 11", "OUT nonblocking 11" }, py);
            Assert.Equal(py, sh);
        }
        finally
        {
            try
            { Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private const string EmfilePython = """
        import resource, socket
        soft, hard = resource.getrlimit(resource.RLIMIT_NOFILE)
        a = socket.socket()
        a.bind(("127.0.0.1", 0))
        a.listen(5)
        b = socket.socket()
        b.connect(("127.0.0.1", a.getsockname()[1]))
        kept = []
        create = accept = "-"
        resource.setrlimit(resource.RLIMIT_NOFILE, (512, hard))
        if resource.getrlimit(resource.RLIMIT_NOFILE)[0] != 512:
            print("OUT SKIP the soft descriptor limit did not become 512")
            raise SystemExit(0)
        try:
            while True:
                kept.append(socket.socket())
        except OSError as e:
            create = e.errno
        try:
            a.accept()
        except OSError as e:
            accept = e.errno
        for s in kept:
            s.close()
        resource.setrlimit(resource.RLIMIT_NOFILE, (soft, hard))
        print("OUT", len(kept) > 0, create, accept)
        """;

    // Sharpy has no resource module: the program lowers its own soft limit with prlimit(1), and nothing
    // that loads an assembly (an exception's type name) runs until the limit is raised again.
    private const string EmfileSharpy = """
        import socket
        from system import Environment
        from system.diagnostics import Process

        def _soft_limit() -> str:
            with open("/proc/self/limits") as f:
                for line in f.read().split("\n"):
                    if line.startswith("Max open files"):
                        return line.split()[3]
            return "?"

        def _limit(n: str) -> bool:
            p = Process.start("prlimit", f"--pid {Environment.process_id} --nofile={n}:")
            p.wait_for_exit()
            return p.exit_code == 0 and _soft_limit() == n

        def main() -> None:
            original: str = _soft_limit()
            a = socket.socket()
            a.bind(("127.0.0.1", 0))
            a.listen(5)
            b = socket.socket()
            b.connect(("127.0.0.1", a.getsockname()[1]))
            kept: list[socket.socket] = []
            create: str = "-"
            accept: str = "-"
            if not _limit("512"):
                print(f"OUT SKIP prlimit could not set the soft descriptor limit to 512 (it is {_soft_limit()})")
                return
            try:
                while True:
                    kept.append(socket.socket())
            except socket.error as e:
                create = str(e.errno)
            try:
                a.accept()
            except socket.error as e:
                accept = str(e.errno)
            for s in kept:
                s.close()
            _limit(original)
            final: str = _soft_limit()
            print(f"OUT {len(kept) > 0} {create} {accept}")
            if final != original:
                print(f"RESTORE FAILED: soft limit {final}, was {original}")
        """;

    /// <summary>
    /// EMFILE end to end, at both descriptor-allocating call sites (<c>socket()</c> and <c>accept</c>):
    /// each side lowers its own soft descriptor limit to 512, opens sockets until one fails, then
    /// accepts a pending connection, and the printed errnos are compared (CPython 3.15.2: 24 and 24).
    /// </summary>
    [SkippableFact]
    public void NewDescriptor_OutOfDescriptors_EqualsPython()
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(), "EMFILE's number is Linux's errno value.");
        TestPreconditions.RequireOrSkip(File.Exists("/usr/bin/prlimit") || File.Exists("/bin/prlimit"),
            "the Sharpy side lowers its descriptor limit with prlimit(1) (util-linux), which is not installed.");
        var python = PythonInterpreter.RequireOrSkip();
        var work = SysPath.Combine(SysPath.GetTempPath(), "sharpy-socket-emfile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var script = SysPath.Combine(work, "emfile.py");
            File.WriteAllText(script, EmfilePython);
            var py = python.RunBatch(script, Array.Empty<string>(), timeoutMilliseconds: 120_000)
                .Select(l => l.Trim()).Single(l => l.StartsWith("OUT ", StringComparison.Ordinal));
            TestPreconditions.RequireOrSkip(!py.StartsWith("OUT SKIP", StringComparison.Ordinal), "python: " + py);
            using var runner = new Runner();
            var result = runner.Execute(EmfileSharpy);
            Assert.True(result.Success, "the Sharpy program failed: " + string.Join("\n", result.CompilationErrors.Take(10)) + result.Failure);
            var lines = result.StandardOutput.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
            var sh = lines.Single(l => l.StartsWith("OUT ", StringComparison.Ordinal));
            // A failed prlimit is a skip, never a run to the host's own (possibly huge) limit.
            TestPreconditions.RequireOrSkip(!sh.StartsWith("OUT SKIP", StringComparison.Ordinal), "Sharpy: " + sh);
            Assert.DoesNotContain(lines, l => l.StartsWith("RESTORE FAILED", StringComparison.Ordinal));
            _output.WriteLine($"python: {py}; Sharpy: {sh}");
            Assert.Equal("OUT True 24 24", py);
            Assert.Equal(py, sh);
        }
        finally
        {
            try
            { Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// The unknown-option rule rewrites only an option .NET refused because it has no enum member for the
    /// level or name. A member .NET has but does not map on Linux (<c>UseLoopback</c>, 64) is refused by .NET
    /// with 95 too and keeps it, unrewritten (docs/deviations.yaml cell 6; CPython passes SOL_SOCKET 64 to
    /// the kernel, where it is a real option, and SUCCEEDS: the <c>member_unmapped_*_useloopback</c> rows).
    /// The guard (enum member or not) is pinned here directly, Sharpy only.
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
        var sb = new StringBuilder("import socket\nimport threading\nimport time\n\n_opened = []\n_timers = []\n\n");
        sb.Append("def _keep(s):\n    _opened.append(s)\n    return s\n\n");
        sb.Append("def _send_later(s, delay):\n    t = threading.Timer(delay, lambda: s.send(b\"hi\"))\n    _timers.append(t)\n    t.start()\n\n");
        sb.Append("def _s(*args):\n    return _keep(socket.socket(*args))\n\n");
        sb.Append("def _close_all():\n    for t in _timers:\n        t.join()\n    _timers.clear()\n"
                  + "    for s in _opened:\n        s.close()\n    _opened.clear()\n    socket.setdefaulttimeout(None)\n\n");
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
        var sb = new StringBuilder("import socket\nimport threading\nimport time\n\n_opened: list[socket.socket] = []\n"
                                   + "_timers: list[threading.Timer] = []\n\n");
        sb.Append("def _keep(s: socket.socket) -> socket.socket:\n    _opened.append(s)\n    return s\n\n");
        sb.Append("def _send_later(s: socket.socket, delay: float) -> None:\n    def send() -> None:\n        s.send(b\"hi\")\n"
                  + "    t = threading.Timer(delay, send)\n    _timers.append(t)\n    t.start()\n\n");
        // Literal defaults, as socket.socket's own: a default naming socket.AF_INET is SPY0908 (CS1736) today.
        sb.Append("def _s(family: int = 2, sock_type: int = 1, proto: int = 0) -> socket.socket:\n"
                  + "    return _keep(socket.socket(family, sock_type, proto))\n\n");
        sb.Append("def _close_all() -> None:\n    for t in _timers:\n        t.join()\n    _timers.clear()\n"
                  + "    for s in _opened:\n        s.close()\n    _opened.clear()\n    socket.setdefaulttimeout(None)\n\n");
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
            base.GetAdditionalReferenceAssemblyPaths()
                .Append(typeof(System.Net.Sockets.Socket).Assembly.Location)
                .Append(typeof(System.Diagnostics.Process).Assembly.Location)
                .Append(typeof(System.ComponentModel.Component).Assembly.Location);

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
