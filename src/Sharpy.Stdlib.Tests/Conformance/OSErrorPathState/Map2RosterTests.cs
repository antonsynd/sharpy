using System.Text.RegularExpressions;
using Sharpy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// G1 Map 2, path positions and requirements (plan-638e8b C2 axes 1, 2 and 4, #2476) — the roster
/// checks that need no python and run everywhere:
/// <list type="bullet">
/// <item><b>T3</b> — every entry point the census derives has a probe row or an exclusion in one of the
/// two closed forms, never both.</item>
/// <item><b>T4</b> (Map 2's half) — a probe, exclusion or position row whose entry point the census no
/// longer finds is red.</item>
/// <item><b>T5</b> — every position of every probed entry point (read off its signature) is classified
/// <c>path</c> | <c>not a path: &lt;reason&gt;</c>, every <c>path</c> position is a placeholder of ≥ 1 probe
/// of that entry point, and every placeholder names a <c>path</c> position.</item>
/// <item><b>R1</b> — every <see cref="PathRequirement"/> member (by <c>Enum.GetValues</c>) is declared by
/// ≥ 1 probe or excluded with a reason. R0 (every public door of D1's <c>PathErrors</c> stamped with
/// exactly one requirement, every member with a door) and R2 (every (entry point, requirement) pair the
/// stamped doors reach has a probe) land with D1's helper and its callers (W2-E, W3-F): they read the
/// stamps by reflection and the reach from <see cref="DoorScan"/>, and are not stubbed here.</item>
/// </list>
/// </summary>
public class Map2RosterTests
{
    private static DoorCensus Census => DoorCensus.Instance.Value;

    private static readonly Regex OutOfContractRow = new(@"^(?<entry>.+?) # out of contract: #(?<issue>\d+) (?<reason>\S.*)$", RegexOptions.Compiled);

    public static string OutOfContractPath => SysPath.Combine(Sharpy.TestInfrastructure.Integration.FixtureRoots.RepositoryRoot,
        "src", "Sharpy.Stdlib.Tests", "Conformance", "OSErrorPathState", "oserror-path-state-out-of-contract-allowlist.txt");

    /// <summary>The <c>out of contract: #N</c> exclusions: entry → "out of contract: #N reason".</summary>
    internal static IReadOnlyDictionary<string, string> OutOfContract()
    {
        var path = OutOfContractPath;
        if (!File.Exists(path))
            throw new FileNotFoundException("the out-of-contract exclusion file is missing", path);
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var m = OutOfContractRow.Match(line);
            Assert.True(m.Success, $"malformed row (want `<entry> # out of contract: #N <reason>`): {line}");
            Assert.False(rows.ContainsKey(m.Groups["entry"].Value), "duplicate row: " + line);
            rows[m.Groups["entry"].Value] = $"out of contract: #{m.Groups["issue"].Value} {m.Groups["reason"].Value}";
        }
        return rows;
    }

    internal static IReadOnlyDictionary<string, string> AllExclusions()
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in ProbeRoster.Exclusions)
            all.Add(k, v);
        foreach (var (k, v) in OutOfContract())
        {
            Assert.False(all.ContainsKey(k), $"{k} is excluded in both forms");
            all.Add(k, v);
        }
        return all;
    }

    [Fact]
    public void ExclusionsAreInTheTwoClosedForms()
    {
        foreach (var (entry, why) in ProbeRoster.Exclusions)
        {
            Assert.True(why.StartsWith(ProbeRoster.Unreachable, StringComparison.Ordinal) && why.Length > ProbeRoster.Unreachable.Length + 10,
                $"{entry}: an in-code exclusion is `unreachable with a user path: <why the edge cannot fire>` (the `out of contract: #N` form lives in the allowlist file)");
        }
        Assert.NotEmpty(OutOfContract());
    }

    [Fact]
    public void T3_EveryEntryPoint_HasAProbeOrAnExclusion_NeverBoth()
    {
        var probed = ProbeRoster.Probes.Select(p => p.Entry).ToHashSet(StringComparer.Ordinal);
        var excluded = AllExclusions();
        var problems = new System.Collections.Generic.List<string>();
        foreach (var (key, entry) in Census.EntryPoints)
        {
            var p = probed.Contains(key);
            var x = excluded.ContainsKey(key);
            if (!p && !x)
                problems.Add($"{key}: no probe row and no exclusion (reached via {entry.Witness})");
            if (p && x)
                problems.Add($"{key}: both probed and excluded ({excluded[key]})");
        }
        Assert.True(problems.Count == 0, "T3:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void T4_EveryMap2Row_NamesAnEntryPointTheCensusStillFinds()
    {
        var entries = Census.EntryPoints;
        var stale = ProbeRoster.Probes.Where(p => !entries.ContainsKey(p.Entry)).Select(p => $"probe row {p.Id} → {p.Entry}")
            .Concat(AllExclusions().Keys.Where(k => !entries.ContainsKey(k)).Select(k => "exclusion row " + k))
            .Concat(ProbeRoster.Positions.Keys.Select(k => k.Split(" :: ")[0]).Distinct().Where(k => !entries.ContainsKey(k)).Select(k => "position rows of " + k))
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0, "T4: Map 2 rows the census no longer finds as entry points:\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void T5_EveryPositionOfEveryProbedEntryPoint_IsClassified_AndEveryPathPositionIsAPlaceholder()
    {
        var entries = Census.EntryPoints;
        var problems = new System.Collections.Generic.List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in ProbeRoster.Probes.GroupBy(p => p.Entry))
        {
            if (!entries.TryGetValue(group.Key, out var entry))
                continue; // T4's business
            var placeholders = group.SelectMany(p => p.Placeholders).ToHashSet(StringComparer.Ordinal);
            foreach (var position in PathPositions.Of(entry.Symbol))
            {
                var key = $"{group.Key} :: {position}";
                expected.Add(key);
                if (!ProbeRoster.Positions.TryGetValue(key, out var reason))
                {
                    problems.Add($"{key}: an unclassified position (`path`, or `not a path: <reason>`)");
                    continue;
                }
                if (reason == null && !placeholders.Contains(position))
                    problems.Add($"{key}: a `path` position no probe of {group.Key} has as a placeholder");
                if (reason != null && !reason.StartsWith("not a path: ", StringComparison.Ordinal))
                    problems.Add($"{key}: a negative position reads `not a path: <reason>`");
            }
            foreach (var probe in group)
            {
                foreach (var name in probe.Placeholders)
                {
                    if (!ProbeRoster.Positions.TryGetValue($"{group.Key} :: {name}", out var r) || r != null)
                        problems.Add($"probe {probe.Id}: placeholder `{name}` is not a `path` position of {group.Key}");
                }
            }
        }
        foreach (var stale in ProbeRoster.Positions.Keys.Where(k => !expected.Contains(k)))
            problems.Add($"{stale}: a position row the signature no longer has");
        Assert.True(problems.Count == 0, "T5:\n  " + string.Join("\n  ", problems.OrderBy(s => s, StringComparer.Ordinal)));
    }

    [Fact]
    public void R1_EveryRequirement_IsDeclaredByAProbe_OrExcluded()
    {
        var declared = ProbeRoster.Probes.Select(p => p.Requirement).ToHashSet();
        var problems = new System.Collections.Generic.List<string>();
        foreach (var requirement in Enum.GetValues<PathRequirement>())
        {
            var excluded = ProbeRoster.RequirementExclusions.TryGetValue(requirement, out var why);
            if (!declared.Contains(requirement) && !excluded)
                problems.Add($"{requirement}: no probe declares it and it is not excluded");
            if (declared.Contains(requirement) && excluded)
                problems.Add($"{requirement}: probed and excluded (`{why}`) — drain the exclusion");
            if (excluded && string.IsNullOrWhiteSpace(why))
                problems.Add($"{requirement}: an exclusion needs its reason");
        }
        Assert.True(Enum.GetValues<PathRequirement>().Length >= 2, "the requirement enum must be read, not empty");
        Assert.True(problems.Count == 0, "R1:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void ProbeRows_AreWellFormed()
    {
        var problems = new System.Collections.Generic.List<string>();
        foreach (var dup in ProbeRoster.Probes.GroupBy(p => p.Id).Where(g => g.Count() > 1))
            problems.Add($"duplicate probe id {dup.Key}");
        foreach (var p in ProbeRoster.Probes)
        {
            if (p.Id.Contains(' ', StringComparison.Ordinal))
                problems.Add($"probe id `{p.Id}` has a space (it keys allowlist rows)");
            if (!ProbeRoster.Headers.ContainsKey(p.Module))
                problems.Add($"{p.Id}: module {p.Module} has no header");
            foreach (var template in new[] { p.Sharpy, p.Python })
            {
                for (var i = 0; i < 4; i++)
                {
                    var uses = template.Contains("@" + i, StringComparison.Ordinal);
                    if (uses != (i < p.Placeholders.Length))
                        problems.Add($"{p.Id}: template `{template}` and placeholders [{string.Join(", ", p.Placeholders)}] disagree on @{i}");
                }
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
