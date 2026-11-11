using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// G1 axis 1 (plan-638e8b C2, #2476): the door roster is closed against the bound external-callee
/// census of <c>src/Sharpy.Core</c> and <c>src/Sharpy.Stdlib</c>.
/// <list type="bullet">
/// <item><b>T1</b> — every external type the census references is covered by a namespace or type row.</item>
/// <item><b>T2</b> — every referenced member of a <c>door</c> type has a member row; and the
/// path-capable members found under each negative row equal the committed, machine-written
/// <c>oserror-door-census.txt</c> in both directions.</item>
/// <item><b>T4</b> — a namespace, type or member row the scan no longer finds is red (stale).</item>
/// </list>
/// Both compilations are asserted error-free with every candidate bound before a roster is read: a
/// degraded compilation cannot pass by finding nothing.
/// </summary>
public class DoorCensusTests
{
    private readonly ITestOutputHelper _output;

    public DoorCensusTests(ITestOutputHelper output) => _output = output;

    private static DoorCensus Census => DoorCensus.Instance.Value;

    [Fact]
    public void BothCompilations_AreErrorFree_AndEveryCandidateBinds()
    {
        var census = Census;
        foreach (var (name, pass, floor) in new[] { ("Core", census.Core, 150), ("Stdlib", census.Stdlib, 200) })
        {
            Assert.True(pass.Files > floor, $"{name}: the scan must read the sources; read {pass.Files} file(s)");
            Assert.True(pass.Errors.Count == 0,
                $"{name}: {pass.Errors.Count} compilation error(s) — the semantic model is degraded:\n  " + string.Join("\n  ", pass.Errors.Take(20)));
            Assert.True(pass.Unbound.Count == 0,
                $"{name}: reference candidates that bind to no symbol (red, never a skip):\n  " + string.Join("\n  ", pass.Unbound.Take(40)));
            Assert.True(pass.External.Count > 1000, $"{name}: the census found only {pass.External.Count} external references");
        }
    }

    [Fact]
    public void T1_EveryExternalType_IsCoveredByANamespaceOrTypeRow()
    {
        var problems = Census.Passes.SelectMany(p => p.Unclassified)
            .Concat(Census.Passes.SelectMany(p => p.DoubleClassified).Select(t => $"{t}: both a namespace row and a type row cover it"))
            .GroupBy(s => s.Split(' ')[0]).Select(g => g.First())
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(problems.Count == 0,
            "T1: external types with no Map 1 row (add a `door` or `not a door: <reason>` row in DoorRoster):\n  "
            + string.Join("\n  ", problems));
    }

    [Fact]
    public void T2_EveryReferencedMemberOfADoorType_HasAMemberRow()
    {
        var missing = Census.Passes.SelectMany(p => p.MissingMemberRows)
            .GroupBy(s => s.Split(" (")[0]).Select(g => g.First())
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            "T2: referenced members of door types with no member row (name its path parameters, or `takes no path: <reason>`):\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void T2_PathCapableMembersUnderNegativeRows_EqualTheCommittedCensus()
    {
        var found = Census.CensusLines();
        if (Environment.GetEnvironmentVariable(DoorCensus.UpdateSwitch) == "1")
        {
            File.WriteAllText(DoorCensus.CensusPath, DoorCensus.CensusHeader + string.Join("\n", found) + "\n");
            _output.WriteLine($"wrote {found.Count} line(s) to {DoorCensus.CensusPath}");
        }

        var committed = DoorCensus.ReadCensusFile();
        var unjudged = found.Except(committed, StringComparer.Ordinal).ToList();
        var stale = committed.Except(found, StringComparer.Ordinal).ToList();
        Assert.True(unjudged.Count == 0 && stale.Count == 0,
            $"T2: the path-capable members under negative rows differ from {DoorCensus.CensusFileName}.\n"
            + "New (a member the row has not judged — re-judge the row, then regenerate with "
            + $"{DoorCensus.UpdateSwitch}=1):\n  " + string.Join("\n  ", unjudged)
            + "\nNo longer referenced (regenerate):\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void T4_EveryRow_IsStillFoundByTheScan()
    {
        var map = Census.Map;
        var seenNs = Census.Passes.SelectMany(p => p.SeenNamespaceRows).ToHashSet(StringComparer.Ordinal);
        var seenTypes = Census.Passes.SelectMany(p => p.SeenTypeRows).ToHashSet(StringComparer.Ordinal);
        var seenMembers = Census.Passes.SelectMany(p => p.SeenMemberRows).ToHashSet(StringComparer.Ordinal);
        var stale = map.Namespaces.Keys.Where(k => !seenNs.Contains(k)).Select(k => "namespace row " + k)
            .Concat(map.Types.Keys.Where(k => !seenTypes.Contains(k)).Select(k => "type row " + k))
            .Concat(map.Members.Keys.Where(k => !seenMembers.Contains(k)).Select(k => "member row " + k))
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0, "T4: Map 1 rows the scan no longer finds (delete them):\n  " + string.Join("\n  ", stale));
    }

    /// <summary>A member row's path parameters are real parameters of the member, of a path-carrier type.</summary>
    [Fact]
    public void DoorMemberRows_NameRealPathParameters()
    {
        var bound = Census.Passes.SelectMany(p => p.External)
            .GroupBy(r => DoorScan.MemberKey(r.Member))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var problems = new System.Collections.Generic.List<string>();
        foreach (var (key, row) in Census.Map.Members)
        {
            if (!bound.TryGetValue(key, out var reference))
                continue; // T4's business
            if (row.IsDoor)
            {
                foreach (var name in row.PathParameters)
                {
                    var ok = reference.Bound switch
                    {
                        IMethodSymbol m => m.Parameters.Any(p => p.Name == name && DoorScan.IsPathCarrier(p.Type)),
                        IPropertySymbol p => name == "value" && DoorScan.IsPathCarrier(p.Type),
                        IFieldSymbol f => name == "value" && DoorScan.IsPathCarrier(f.Type),
                        _ => false,
                    };
                    if (!ok)
                        problems.Add($"{key}: `{name}` is not a path-carrier parameter of the member");
                }
            }
            else if (string.IsNullOrWhiteSpace(row.NoPathReason))
            {
                problems.Add($"{key}: a `takes no path` row needs its reason");
            }
        }
        foreach (var (key, row) in Census.Map.Types.Where(t => !t.Value.Door))
        {
            if (string.IsNullOrWhiteSpace(row.Reason))
                problems.Add($"{key}: a `not a door` row needs its reason");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// Positive control: the three doors the plan's first pass found outside the old namespace list are
    /// census members, and Core's <c>open</c> is a Core entry point (the upstream doors the Stdlib pass reads).
    /// </summary>
    [Fact]
    public void PositiveControl_TheKnownDoorsAreFoundAndReached()
    {
        var census = Census;
        var doorRefs = census.Passes.SelectMany(p => p.External).Select(r => (DoorScan.MemberKey(r.Member), r.Kind)).ToHashSet();
        Assert.Contains(("System.Xml.Linq.XDocument.Load(string)", DoorScan.Invocation), doorRefs);
        Assert.Contains(("System.Diagnostics.ProcessStartInfo.WorkingDirectory", DoorScan.PropertyWrite), doorRefs);
        Assert.Contains(("Microsoft.Data.Sqlite.SqliteConnection.SqliteConnection(string)", DoorScan.Creation), doorRefs);

        var entries = census.EntryPoints;
        Assert.Contains(entries.Keys, k => k.StartsWith("Sharpy.Builtins.Open(", StringComparison.Ordinal));
        // The upstream arm: Core members reaching a door are what the Stdlib pass treats as doors.
        Assert.Contains(census.Core.Reaching.Keys, k => k.StartsWith("Sharpy.Builtins.Open(", StringComparison.Ordinal));
    }

    /// <summary>The entry points and their witness chains, for review (no assertion beyond a floor).</summary>
    [Fact]
    public void EntryPoints_AreListed()
    {
        var entries = Census.EntryPoints;
        foreach (var (key, entry) in entries)
            _output.WriteLine($"{key}\n    positions [{string.Join(", ", PathPositions.Of(entry.Symbol))}] {(entry.Symbol is IMethodSymbol m ? m.ToDisplayString() : "")}\n    via {entry.Witness}");
        _output.WriteLine($"{entries.Count} entry point(s); census {Census.CensusLines().Count} line(s); "
                          + $"Core reaching {Census.Core.Reaching.Count}, Stdlib reaching {Census.Stdlib.Reaching.Count}");
        Assert.True(entries.Count > 20, $"only {entries.Count} entry point(s): the reach is degraded");
    }
}
