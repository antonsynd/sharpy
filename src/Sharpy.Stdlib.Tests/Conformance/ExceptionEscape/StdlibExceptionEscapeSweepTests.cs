using SCG = System.Collections.Generic;
using System.Text.RegularExpressions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance.ExceptionEscape;

/// <summary>
/// No CLR exception escapes a Stdlib module: a Sharpy program can only catch Python-named exceptions, so every
/// expected failure a module reports — including one thrown from INSIDE a .NET API the module calls
/// (<c>DateTime.AddTicks</c>, <c>DateTime.ParseExact</c>, a <c>checked</c> conversion) — must reach the program as
/// a Python exception. <see cref="StdlibClrThrowScanTests"/> reads the source for literal <c>throw new</c> of four
/// CLR types; this sweep measures behaviour.
///
/// <para><b>Generated.</b> For each module on <see cref="Roster"/>, every public member of its types
/// (<see cref="EscapeSweep"/>) is invoked with boundary arguments per parameter type
/// (<see cref="BoundaryArguments"/>), receivers and Sharpy-typed arguments drawn from the instances the sweep
/// itself produced. An escaping exception is a leak unless <see cref="AcceptedExceptions"/> (derived from Core's
/// exception hierarchy by reflection) accepts it. Members whose IL reaches the file system, console, processes,
/// network, sleeping or exit are excluded by <see cref="SideEffectRule"/>, a rule, not a member list.</para>
///
/// <para><b>Ratchet.</b> The leak set must equal <c>exception-escape-allowlist.txt</c> beside this file, rows of
/// roster modules only, each citing an OPEN issue: an unlisted leak is red, a listed leak the sweep no longer
/// finds is red (stale: delete the row). Enabling a module is one name on <see cref="Roster"/>.</para>
/// </summary>
public class StdlibExceptionEscapeSweepTests
{
    /// <summary>
    /// The swept modules. Off the roster for now: the filesystem and process modules (<c>os</c>, <c>pathlib</c>,
    /// <c>shutil</c>, <c>io</c>) whose OSError translation is P33's lane (#2476, #2556).
    /// </summary>
    public static readonly string[] Roster = { "datetime" };

    private static readonly Regex AllowlistRow = new(
        @"^(?<key>(?<module>[a-z_0-9]+):\S+:\S+) # #(?<issue>\d+) (?<reason>\S.*)$", RegexOptions.Compiled);

    private static readonly Lazy<Dictionary<string, SweepResult>> Results = new(() =>
        Roster.ToDictionary(m => m, m => EscapeSweep.Run(m, EscapeSweep.ModuleTypes(typeof(Date).Assembly, m)), StringComparer.Ordinal));

    private readonly ITestOutputHelper _output;

    public StdlibExceptionEscapeSweepTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void NoClrExceptionEscapesARosterModule_BeyondTheAllowlist()
    {
        var allowed = ReadAllowlist();
        var found = new SortedDictionary<string, Leak>(StringComparer.Ordinal);
        foreach (var (module, result) in Results.Value)
        {
            _output.WriteLine($"{module}: {result.Swept.Count} members swept, {result.Invocations} calls, " +
                $"{result.Excluded.Count} excluded, {result.Unswept.Count} unswept, {result.Leaks.Count} leaks, " +
                $"{result.Elapsed.TotalSeconds:F1}s; accepted escapes: {string.Join(", ", result.Accepted)}");
            foreach (var (key, leak) in result.Leaks)
            {
                found[key] = leak;
                _output.WriteLine("  leak " + key + "  e.g. " + leak.Example);
            }
            Assert.True(result.HarnessErrors.Count == 0,
                $"{module}: the sweep passed arguments its own members cannot take:\n  " + string.Join("\n  ", result.HarnessErrors.Take(20)));
        }

        var unlisted = found.Where(kv => !allowed.ContainsKey(kv.Key)).Select(kv => kv.Key + "\n      e.g. " + kv.Value.Example).ToList();
        var stale = allowed.Keys.Where(k => !found.ContainsKey(k)).ToList();
        Assert.True(unlisted.Count == 0 && stale.Count == 0,
            "CLR exceptions escaping Stdlib modules disagree with Conformance/ExceptionEscape/exception-escape-allowlist.txt.\n"
            + "A Sharpy program catches only Python exceptions: translate the CLR exception at the module boundary\n"
            + "(ValueError, OverflowError, ...), or, while an OPEN issue tracks it, list it with that issue.\n"
            + "Unlisted leaks:\n  " + string.Join("\n  ", unlisted)
            + "\nStale rows (the leak is gone: delete the row):\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Every public member of a roster module is called: a member the generators cannot feed is red, not skipped.</summary>
    [Fact]
    public void EveryMemberOfARosterModuleIsSwept()
    {
        foreach (var (module, result) in Results.Value)
        {
            Assert.True(result.Unswept.Count == 0,
                $"{module}: members the sweep could not call (extend BoundaryArguments):\n  "
                + string.Join("\n  ", result.Unswept.Select(kv => kv.Key + ": " + kv.Value)));
        }
    }

    /// <summary>
    /// The skip path is not what ran: datetime has no side-effecting member, so the rule excludes nothing there, and the
    /// members the known leaks live on were called.
    /// </summary>
    [Fact]
    public void Datetime_IsSweptWhole_AndTheLeakProneMembersWereCalled()
    {
        var result = Results.Value["datetime"];
        Assert.Empty(result.Excluded);
        Assert.True(result.Swept.Count >= 60, $"only {result.Swept.Count} datetime members swept");
        foreach (var member in new[]
                 {
                     "datetime:Date.op_Addition(Date,Timedelta)", "datetime:Date.op_Subtraction(Date,Timedelta)",
                     "datetime:Date.Fromisoformat(String)", "datetime:DateTime.Fromisoformat(String)",
                     "datetime:DateTime.Strptime(String,String)", "datetime:DateTime.Astimezone(ITzinfo)",
                     "datetime:Timedelta..ctor(Int32,Int32,Int32,Int32,Int32,Int32,Int32)",
                     "datetime:Timedelta.op_Division(Timedelta,Int32)", "datetime:Date.IRepr.Repr()",
                 })
        {
            Assert.Contains(member, result.Swept);
        }
        Assert.Contains("ValueError", result.Accepted);
    }

    /// <summary>Positive control: the sweep catches a CLR leak, accepts a Python exception, and excludes I/O by the rule.</summary>
    [Fact]
    public void PositiveControl_SweepFlagsLeaksAcceptsPythonExceptionsAndExcludesIo()
    {
        var result = EscapeSweep.Run("control", new[] { typeof(EscapeControlModule), typeof(EscapeControlWidget) });

        Assert.Contains("control:EscapeControlModule.ParseLeaks(String):System.FormatException", result.Leaks.Keys);
        Assert.Contains("control:EscapeControlModule.DivideLeaks(Int32):System.DivideByZeroException", result.Leaks.Keys);
        Assert.Contains("control:EscapeControlWidget.Shift(Int32):System.OverflowException", result.Leaks.Keys);
        Assert.DoesNotContain(result.Leaks.Keys, k => k.Contains(".Checked(", StringComparison.Ordinal));
        Assert.Contains("ValueError", result.Accepted);
        Assert.Contains("control:EscapeControlModule.ReadsAFile(String)", result.Excluded.Keys);
        Assert.Contains("control:EscapeControlModule.ReadsAFileThroughAHelper(String)", result.Excluded.Keys);
        Assert.Contains("control:EscapeControlModule.Sleeps(Int32)", result.Excluded.Keys);
        Assert.Contains("control:EscapeControlModule.Prints(String)", result.Excluded.Keys);
        Assert.DoesNotContain(result.Swept, k => k.Contains("ReadsAFile", StringComparison.Ordinal));
        Assert.Contains("control:EscapeControlModule.Checked(Int32)", result.Swept);
        Assert.Empty(result.HarnessErrors);
        Assert.Empty(result.Unswept);
    }

    /// <summary>Positive control: the accepted set is Core's hierarchy, and a CLR type is never in it.</summary>
    [Fact]
    public void PositiveControl_TheAcceptedSetIsDerivedFromCore()
    {
        Assert.Contains(typeof(ValueError), AcceptedExceptions.CoreTypes);
        Assert.Contains(typeof(OverflowError), AcceptedExceptions.CoreTypes);
        Assert.Contains(typeof(OSError), AcceptedExceptions.CoreTypes);
        Assert.True(AcceptedExceptions.IsPythonException(typeof(Exception)));
        Assert.True(AcceptedExceptions.IsPythonException(typeof(FileNotFoundError)));
        Assert.False(AcceptedExceptions.IsPythonException(typeof(FormatException)));
        Assert.False(AcceptedExceptions.IsPythonException(typeof(ArgumentOutOfRangeException)));
        Assert.False(AcceptedExceptions.IsPythonException(typeof(System.IO.IOException)));
        Assert.False(AcceptedExceptions.IsPythonException(typeof(System.OverflowException)));
    }

    private static Dictionary<string, string> ReadAllowlist()
    {
        var path = SysPath.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Stdlib.Tests", "Conformance", "ExceptionEscape",
            "exception-escape-allowlist.txt");
        if (!File.Exists(path))
            throw new FileNotFoundException("exception-escape-allowlist.txt is missing; its presence arms the ratchet.", path);
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var m = AllowlistRow.Match(line);
            Assert.True(m.Success, $"malformed row (want `module:Type.Member(Params):Exception.FullName # #N reason`): {line}");
            Assert.True(Roster.Contains(m.Groups["module"].Value), $"row for a module not on the roster: {line}");
            Assert.False(rows.ContainsKey(m.Groups["key"].Value), $"duplicate row: {line}");
            rows[m.Groups["key"].Value] = m.Groups["issue"].Value;
        }
        return rows;
    }
}

/// <summary>A synthetic module for the positive control: one member per verdict.</summary>
public static class EscapeControlModule
{
    public static int ParseLeaks(string s) => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    public static int DivideLeaks(int n) => 10 / n;

    public static int Checked(int n) => n < 0 ? throw new ValueError("negative") : n;

    public static EscapeControlWidget Make(int n) => new(n);

    public static string ReadsAFile(string path) => File.ReadAllText(path);

    public static string ReadsAFileThroughAHelper(string path) => Helper(path);

    public static void Sleeps(int ms) => System.Threading.Thread.Sleep(ms);

    public static void Prints(string s) => Console.WriteLine(s);

    private static string Helper(string path) => File.ReadAllText(path);
}

/// <summary>A receiver type for the positive control: its instances come from <see cref="EscapeControlModule.Make"/>.</summary>
public sealed class EscapeControlWidget
{
    private readonly int _n;

    internal EscapeControlWidget(int n) => _n = n;

    public int Shift(int k) => checked(_n + k);

    public override bool Equals(object? obj) => obj is EscapeControlWidget w && w._n == _n;

    public override int GetHashCode() => _n;
}
