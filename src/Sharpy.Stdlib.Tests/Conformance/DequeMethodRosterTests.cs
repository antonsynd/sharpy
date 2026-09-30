using System.Reflection;
using Sharpy.Stdlib.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The <c>collections.deque</c> method roster (#2107). Contract: <c>deque</c> exposes python's
/// method surface, and a python name Sharpy does not carry is SPY0203 "has no member" — never a
/// stray CLR extension method that happens to share the name. Before this roster three of python's
/// names reached exactly such an extension: <c>d.remove(x)</c> bound
/// <c>CollectionExtensions.Remove(IDictionary…)</c> (SPY0908 CS7036), <c>d.count(x)</c> reached
/// LINQ's <c>Count(predicate)</c> (SPY0220), and <c>d.reverse()</c> bound LINQ's
/// <c>Enumerable.Reverse</c>, which returns a reversed copy and left the deque unchanged — a silent
/// wrong. <c>d.index(x)</c> was SPY0224 (it read the indexer), and <c>copy</c>/<c>insert</c>/<c>rotate</c>
/// were SPY0203.
///
/// <para>
/// The roster is python3 3.12.13's <c>[n for n in dir(deque) if not n.startswith('_')]</c>, written
/// as a literal (not derived from the CLR type, so an omission cannot pass vacuously). Every name is
/// either a public member of <see cref="Deque{T}"/> under the compiler's member mangling
/// (<see cref="NameMangling.ToPascalCase"/>) or a row of <see cref="Excluded"/>, whose rows must stay
/// absent (a present excluded name fails: drain on fix) and must be SPY0203 when used. The reverse
/// direction holds too: every public member <see cref="Deque{T}"/> declares is a python name or a row
/// of <see cref="ClrPlumbing"/> — a stray public <c>Count</c> property is what surfaced a non-python
/// <c>d.count</c> attribute.
/// </para>
/// </summary>
public class DequeMethodRosterTests : StdlibIntegrationTestBase
{
    public DequeMethodRosterTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>python3 3.12.13: <c>[n for n in dir(collections.deque) if not n.startswith('_')]</c>.</summary>
    private static readonly string[] PythonPublicNames =
    {
        "append", "appendleft", "clear", "copy", "count", "extend", "extendleft", "index", "insert",
        "maxlen", "pop", "popleft", "remove", "reverse", "rotate",
    };

    /// <summary>
    /// Python names Sharpy's deque deliberately does not carry → why. Each row must be absent from the
    /// CLR surface and must be SPY0203 at a use site (never a stray CLR binding).
    /// </summary>
    private static readonly Dictionary<string, string> Excluded = new()
    {
        // Sharpy's deque is unbounded: there is no `deque(iterable, maxlen)` constructor, so the
        // attribute reporting the bound has nothing to report (Deque<T>.ToString documents the
        // unbounded repr). A bounded deque is a feature, not a missing method (#2107 roster).
        ["maxlen"] = "unbounded deque — no maxlen= constructor argument (#2107)",
    };

    /// <summary>
    /// Public members <see cref="Deque{T}"/> declares that are .NET plumbing for a python protocol,
    /// not python attribute names: iteration (<c>__iter__</c>), repr (<c>__repr__</c>), and the
    /// <c>d[i]</c> indexer (<c>__getitem__</c>/<c>__setitem__</c>, the property <c>Item</c>).
    /// </summary>
    private static readonly string[] ClrPlumbing = { "GetEnumerator", "ToString", "Item" };

    // Accessor methods (get_X/set_X) are special-name and read through their property, so the
    // roster sees one name per property.
    private static HashSet<string> DeclaredPublicMemberNames()
        => typeof(Deque<>)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Field
                || (m is MethodInfo method && !method.IsSpecialName))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Roster_IsTheFifteenPythonNames()
    {
        // Totality anchor: the literal is python3 3.12.13's count, so a dropped row fails here.
        Assert.Equal(15, PythonPublicNames.Distinct().Count());
        Assert.All(Excluded.Keys, name => Assert.Contains(name, PythonPublicNames));
    }

    [Fact]
    public void EveryPythonName_IsOnTheSurface_OrExcluded()
    {
        var declared = DeclaredPublicMemberNames();
        var missing = PythonPublicNames
            .Where(n => !Excluded.ContainsKey(n) && !declared.Contains(NameMangling.ToPascalCase(n)))
            .ToList();
        Assert.True(missing.Count == 0,
            "python deque names with no Deque<T> member (add the member, or an Excluded row with a reason): "
            + string.Join(", ", missing));

        var staleExclusions = Excluded.Keys.Where(n => declared.Contains(NameMangling.ToPascalCase(n))).ToList();
        Assert.True(staleExclusions.Count == 0,
            "Excluded rows now present on Deque<T> — delete the row: " + string.Join(", ", staleExclusions));
    }

    [Fact]
    public void EveryDeclaredPublicMember_IsAPythonName_OrClrPlumbing()
    {
        var python = PythonPublicNames.Select(NameMangling.ToPascalCase).ToHashSet(StringComparer.Ordinal);
        var extra = DeclaredPublicMemberNames()
            .Where(n => !python.Contains(n) && !ClrPlumbing.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.True(extra.Count == 0,
            "public Deque<T> members with no python deque name (a non-python attribute on the surface): "
            + string.Join(", ", extra));
    }

    // Every non-excluded roster name is called below; the totality check reads the program text.
    private const string Program = @"from collections import deque

def main() -> None:
    d = deque[int]([1, 5, 1, 3])
    d.append(4)
    d.appendleft(0)
    print(d)
    print(d.count(1), d.count(9))
    print(d.index(1), d.index(1, 2), d.index(1, -3), d.index(3, 0, 5))
    try:
        d.index(5, 3, -1)
    except ValueError as e:
        print(e)
    c = d.copy()
    c.append(7)
    print(d, c)
    d.remove(1)
    print(d)
    try:
        d.remove(9)
    except ValueError as e:
        print(e)
    d.insert(1, 8)
    d.insert(-100, 6)
    d.insert(100, 2)
    print(d)
    d.reverse()
    print(d)
    d.rotate()
    print(d)
    d.rotate(-2)
    print(d)
    print(d.pop(), d.popleft())
    d.extend([10, 11])
    d.extendleft([20, 21])
    print(d)
    d.clear()
    print(d, len(d))
";

    // python3 3.12.13, the same program over collections.deque (`def main():` + `main()`).
    private const string Expected =
        "deque([0, 1, 5, 1, 3, 4])\n" +
        "2 0\n" +
        "1 3 3 4\n" +
        "5 is not in deque\n" +
        "deque([0, 1, 5, 1, 3, 4]) deque([0, 1, 5, 1, 3, 4, 7])\n" +
        "deque([0, 5, 1, 3, 4])\n" +
        "9 is not in deque\n" +
        "deque([6, 0, 8, 5, 1, 3, 4, 2])\n" +
        "deque([2, 4, 3, 1, 5, 8, 0, 6])\n" +
        "deque([6, 2, 4, 3, 1, 5, 8, 0])\n" +
        "deque([4, 3, 1, 5, 8, 0, 6, 2])\n" +
        "2 4\n" +
        "deque([21, 20, 3, 1, 5, 8, 0, 6, 10, 11])\n" +
        "deque([]) 0";

    [Fact]
    public void EveryPythonMethod_Executes_MatchesPython()
    {
        var uncalled = PythonPublicNames
            .Where(n => !Excluded.ContainsKey(n) && !Program.Contains("." + n + "(", StringComparison.Ordinal))
            .ToList();
        Assert.True(uncalled.Count == 0, "roster names the program never calls: " + string.Join(", ", uncalled));

        var result = CompileAndExecute(Program);
        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(Expected, result.StandardOutput.TrimEnd('\n', '\r'));
    }

    public static IEnumerable<object[]> ExcludedNames() => Excluded.Keys.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(ExcludedNames))]
    public void ExcludedName_IsSPY0203_NotAStrayClrBinding(string name)
    {
        var result = CompileAndExecute(
            "from collections import deque\n\ndef main() -> None:\n    d = deque[int]([1])\n    print(d." + name + ")\n");
        Assert.False(result.Success, $"d.{name} compiled; an excluded name must be refused");
        var codes = result.RawDiagnostics.Select(d => d.Code + ": " + d.Message).ToList();
        Assert.True(codes.Count > 0 && codes.All(c => c.StartsWith("SPY0203:", StringComparison.Ordinal)),
            $"d.{name} must be SPY0203 alone (never a stray CLR binding), got: " + string.Join("; ", codes));
    }

    [Fact]
    public void ExcludedName_Control_APresentName_IsNotSPY0203()
    {
        // Positive control for the SPY0203 cell: the same program shape over a carried name runs.
        var result = CompileAndExecute(
            "from collections import deque\n\ndef main() -> None:\n    d = deque[int]([1])\n    print(d.copy())\n");
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == "SPY0203");
        Assert.Equal("deque([1])", result.StandardOutput.TrimEnd('\n', '\r'));
    }
}
