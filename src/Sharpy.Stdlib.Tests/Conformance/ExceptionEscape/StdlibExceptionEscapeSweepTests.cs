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
    /// The swept modules, each with the members the side-effect rule may exclude there — reviewed, by key. A module
    /// whose reachable IL holds a side effect or a shape the rule cannot follow (an extern, a BCL-interface dispatch,
    /// a delegate parameter's invoke, reflection) excludes those members; enabling it is red until each exclusion is
    /// read and pinned here, so the rule's blind spots are never silently inherited. Off the roster for now: the
    /// filesystem and process modules (<c>os</c>, <c>pathlib</c>, <c>shutil</c>, <c>io</c>), P33's lane (#2476, #2556).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Roster = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["datetime"] = Array.Empty<string>(),
    };

    private static readonly Regex AllowlistRow = new(
        @"^(?<key>(?<module>[a-z_0-9]+):\S+:\S+) # #(?<issue>\d+) (?<reason>\S.*)$", RegexOptions.Compiled);

    private static readonly Lazy<Dictionary<string, SweepResult>> Results = new(() =>
        Roster.Keys.ToDictionary(m => m, m => EscapeSweep.Run(m, EscapeSweep.ModuleTypes(typeof(Date).Assembly, m)), StringComparer.Ordinal));

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
            foreach (var (key, reason) in result.Excluded)
                _output.WriteLine("  excluded " + key + ": " + reason);
            foreach (var (key, clock) in result.ClockReaders)
                _output.WriteLine("  reads the clock (results not pooled) " + key + ": " + clock);
            foreach (var (key, untried) in result.Untried)
                _output.WriteLine($"  over the {EscapeSweep.CartesianCap}-combination cap: {key}: {untried:G6} combinations not tried");
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
    /// Each roster module excludes exactly its pinned members: a new exclusion (a side effect, or a shape the rule
    /// cannot follow) is red with the reason, and so is a pinned one that no longer happens.
    /// </summary>
    [Fact]
    public void EveryRosterModulesExclusionsArePinned()
    {
        foreach (var (module, result) in Results.Value)
        {
            var pinned = new HashSet<string>(Roster[module], StringComparer.Ordinal);
            var unpinned = result.Excluded.Where(kv => !pinned.Contains(kv.Key)).Select(kv => kv.Key + "\n      " + kv.Value).ToList();
            var stale = pinned.Where(k => !result.Excluded.ContainsKey(k)).ToList();
            Assert.True(unpinned.Count == 0 && stale.Count == 0,
                $"{module}: the side-effect rule's exclusions disagree with the roster's pinned list.\n"
                + "An exclusion means the rule found a side effect or a shape it cannot follow; read the reason, then pin the key\n"
                + "in Roster (or change the module so the rule can vouch for it).\nUnpinned:\n  " + string.Join("\n  ", unpinned)
                + "\nPinned but not excluded:\n  " + string.Join("\n  ", stale));
        }
    }

    /// <summary>
    /// Determinism: the members that read the clock or the host time zone are known, called, and kept out of the pool, so
    /// the instances the sweep combines do not depend on the machine, its time zone or the time of day.
    /// </summary>
    [Fact]
    public void Datetime_ClockReadersAreCalledButNotPooled()
    {
        var result = Results.Value["datetime"];
        foreach (var member in new[] { "datetime:Date.Today()", "datetime:DateTime.Now()", "datetime:DateTime.Utcnow()" })
        {
            Assert.Contains(member, result.ClockReaders.Keys);
            Assert.Contains(member, result.Swept);
        }
    }

    /// <summary>
    /// The module's type roster cannot shrink unseen: every public Stdlib type a roster module's public signatures name
    /// is itself one of the module's swept types, and every swept type contributed members to the enumeration.
    /// </summary>
    [Fact]
    public void EveryRosterModulesTypeSetIsClosedAndEnumerated()
    {
        var stdlib = typeof(Date).Assembly;
        foreach (var (module, result) in Results.Value)
        {
            var types = EscapeSweep.ModuleTypes(stdlib, module);
            var names = new HashSet<string>(types.Select(t => t.Name), StringComparer.Ordinal);
            var enumeratedTypes = result.Enumerated.Select(k => k[(module.Length + 1)..].Split('.')[0]).ToHashSet(StringComparer.Ordinal);
            foreach (var type in types.Where(t => !t.IsInterface))
                Assert.True(enumeratedTypes.Contains(type.Name), $"{module}: type {type.Name} contributed no member to the sweep");

            const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
            var named = types.SelectMany(t => t.GetMethods(Public).Cast<System.Reflection.MethodBase>().Concat(t.GetConstructors(Public)))
                .SelectMany(m => m.GetParameters().Select(p => p.ParameterType)
                    .Concat(m is System.Reflection.MethodInfo mi ? new[] { mi.ReturnType } : Array.Empty<Type>()))
                .Select(t => Nullable.GetUnderlyingType(t) ?? t)
                .Where(t => t.Assembly == stdlib && t.IsPublic)
                .Select(t => t.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.NotEmpty(named);
            Assert.All(named, n => Assert.True(names.Contains(n) || UnattributedTypes.Contains(n),
                $"{module}: its signatures name {n}, which is not one of its swept types"));
            Assert.All(UnattributedTypes, n => Assert.True(!names.Contains(n), $"{n} is now a {module} type: delete it from UnattributedTypes"));
        }
    }

    /// <summary>
    /// Public Stdlib types a roster module's signatures name that carry no <c>[SharpyModuleType]</c>, so the module's
    /// reflected roster misses them: #2053 (they leak as builtins). Delete a row when its type is stamped.
    /// </summary>
    private static readonly HashSet<string> UnattributedTypes = new(StringComparer.Ordinal)
    {
        "ITzinfo", // #2053
    };

    /// <summary>The members the known leaks lived on were called (the skip path is not what ran).</summary>
    [Fact]
    public void Datetime_TheLeakProneMembersWereCalled()
    {
        var result = Results.Value["datetime"];
        // Every member the reflection enumerated was called, unless the rule excluded it or no generator fed it.
        var expected = result.Enumerated.Except(result.Excluded.Keys).Except(result.Unswept.Keys).ToList();
        Assert.NotEmpty(result.Enumerated);
        Assert.Equal(expected, result.Swept);
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
        Assert.Contains("control:EscapeControlModule.Lazily(Int32)#iterate:System.InvalidOperationException", result.Leaks.Keys);
    }

    /// <summary>
    /// Positive control: each shape the rule cannot follow excludes its member, with the shape named, and a static
    /// constructor with I/O is judged before its fields are read.
    /// </summary>
    [Fact]
    public void PositiveControl_UnfollowableShapesExcludeTheirMember()
    {
        var result = EscapeSweep.Run("shapes", new[] { typeof(EscapeShapeModule), typeof(EscapeIoStatics) });
        string Reason(string key)
        {
            Assert.True(result.Excluded.TryGetValue(key, out var reason), $"{key} was not excluded; excluded: {string.Join(", ", result.Excluded.Keys)}");
            return reason!;
        }

        Assert.Contains("extern", Reason("shapes:EscapeShapeModule.CallsExtern(Int32)"), StringComparison.Ordinal);
        Assert.Contains("interface dispatch", Reason("shapes:EscapeShapeModule.CountsItems(IEnumerable<T>)"), StringComparison.Ordinal);
        Assert.Contains("delegate invoke", Reason("shapes:EscapeShapeModule.CallsDelegate(Func<Int32>)"), StringComparison.Ordinal);
        Assert.Contains("reflection", Reason("shapes:EscapeShapeModule.Reflects(String)"), StringComparison.Ordinal);
        Assert.Contains("System.IO.File", Reason("shapes:EscapeShapeModule.ReadsAnIoStatic()"), StringComparison.Ordinal);
        Assert.Contains("System.IO.File", Reason("shapes:EscapeIoStatics..cctor"), StringComparison.Ordinal);
        Assert.Contains("System.IO.File", Reason("shapes:EscapeShapeModule.CallsAnIoImplementation(Int32)"), StringComparison.Ordinal);
        Assert.Contains("shapes:EscapeShapeModule.UsesItsOwnLambda(Int32)", result.Swept);
        Assert.Empty(result.Leaks);
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
            Assert.True(Roster.ContainsKey(m.Groups["module"].Value), $"row for a module not on the roster: {line}");
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

    public static IEnumerable<int> Lazily(int n)
    {
        if (n > -10)
            throw new InvalidOperationException("raised on the first MoveNext");
        yield return n;
    }
}

/// <summary>A synthetic module for the unfollowable-shape control: one member per shape.</summary>
public static class EscapeShapeModule
{
    [System.Runtime.InteropServices.DllImport("sharpy-no-such-library")]
    private static extern int Missing(int x);

    public static int CallsExtern(int x) => Missing(x);

    public static int CountsItems<T>(IEnumerable<T> items)
    {
        int n = 0;
        foreach (var _ in items)
            n++;
        return n;
    }

    public static int CallsDelegate(Func<int> f) => f();

    public static object? Reflects(string name) => typeof(EscapeShapeModule).GetMethod(name)?.Invoke(null, null);

    public static int ReadsAnIoStatic() => EscapeIoStatics.Text.Length;

    public static int CallsAnIoImplementation(int n) => ((IEscapeProbe)new EscapeIoProbe()).Probe(n);

    public static int UsesItsOwnLambda(int n)
    {
        Func<int, int> twice = x => x * 2;
        return twice(n);
    }
}

/// <summary>A type whose static constructor does I/O: reading its field must be judged first.</summary>
public static class EscapeIoStatics
{
    public static readonly string Text = File.ReadAllText("/sharpy-no-such-file");
}

/// <summary>A Sharpy interface whose only implementation does I/O: the walk follows the implementation.</summary>
public interface IEscapeProbe
{
    int Probe(int n);
}

/// <summary>The I/O implementation of <see cref="IEscapeProbe"/>.</summary>
public sealed class EscapeIoProbe : IEscapeProbe
{
    public int Probe(int n) => File.ReadAllText("/sharpy-no-such-file").Length + n;
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
