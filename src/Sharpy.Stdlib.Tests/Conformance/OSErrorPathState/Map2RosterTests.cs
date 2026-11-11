using System.Reflection;
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
/// <item><b>R0</b> — every public method of D1's <see cref="PathErrors"/> is stamped with exactly one
/// requirement (<see cref="PathRequirementAttribute"/>), and every <see cref="PathRequirement"/> member has
/// its doors: a check door and a translate door, or the pair door — read by reflection, no list.</item>
/// <item><b>R1</b> — every <see cref="PathRequirement"/> member (by <c>Enum.GetValues</c>) is declared by
/// ≥ 1 probe or excluded with a reason (an asks-no-door probe declares none).</item>
/// <item><b>R2</b> — every (entry point, requirement) pair the stamped doors reach over the bound call
/// graph (<see cref="RequirementReach"/>) is declared by a probe or excluded as a pair in the closed form;
/// no probe declares a requirement its entry point does not reach; an asks-no-door probe's entry point
/// reaches no stamp.</item>
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
        var declared = ProbeRoster.Probes.SelectMany(p => p.Declared).ToHashSet(); // an asks-no-door probe declares none
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

    /// <summary>The shape of a <see cref="PathErrors"/> door, read off its parameter types.</summary>
    private static string DoorShape(MethodInfo door)
    {
        var parameters = door.GetParameters().Select(p => p.ParameterType).ToArray();
        if (parameters.SequenceEqual(new[] { typeof(string) }) && door.ReturnType == typeof(void))
            return "check";
        if (parameters.SequenceEqual(new[] { typeof(Exception), typeof(string) }) && door.ReturnType == typeof(Exception))
            return "translate";
        if (parameters.SequenceEqual(new[] { typeof(string), typeof(string) }) && door.ReturnType == typeof(void))
            return "pair";
        return "an unknown shape";
    }

    /// <summary>
    /// R0 (plan-638e8b C2 axis 4, D1 predicate 4): every public method of <see cref="PathErrors"/> carries
    /// exactly one <see cref="PathRequirementAttribute"/> and has a door shape; every requirement has
    /// either a check door and a translate door, or the pair door, and no shape twice. The enum and the
    /// stamp stay internal, so the classifier reserves no name a Sharpy program could declare.
    /// </summary>
    [Fact]
    public void R0_EveryPublicDoor_IsStampedWithOneRequirement_AndEveryRequirementHasItsDoors()
    {
        var problems = new System.Collections.Generic.List<string>();
        var doors = typeof(PathErrors).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.True(doors.Length >= Enum.GetValues<PathRequirement>().Length, "R0 must read the doors, not an empty surface");
        var shapes = new Dictionary<PathRequirement, System.Collections.Generic.List<string>>();
        foreach (var door in doors.OrderBy(d => d.ToString(), StringComparer.Ordinal))
        {
            var stamps = door.GetCustomAttributes<PathRequirementAttribute>(inherit: false).ToList();
            if (stamps.Count != 1)
            {
                problems.Add($"{door}: {stamps.Count} requirement stamps (exactly one)");
                continue;
            }
            var shape = DoorShape(door);
            if (shape == "an unknown shape")
                problems.Add($"{door}: {shape} (check: (string) → void; translate: (Exception, string) → Exception; pair: (string, string) → void)");
            if (!shapes.TryGetValue(stamps[0].Requirement, out var list))
                shapes[stamps[0].Requirement] = list = new System.Collections.Generic.List<string>();
            list.Add(shape);
        }
        foreach (var requirement in Enum.GetValues<PathRequirement>())
        {
            var have = shapes.TryGetValue(requirement, out var list) ? list.OrderBy(s => s, StringComparer.Ordinal).ToList() : new System.Collections.Generic.List<string>();
            var single = have.SequenceEqual(new[] { "check", "translate" });
            var pair = have.SequenceEqual(new[] { "pair" });
            if (!single && !pair)
                problems.Add($"{requirement}: doors [{string.Join(", ", have)}] — want a check and a translate door, or the pair door");
        }
        Assert.False(typeof(PathRequirement).IsPublic, "D1.4: a public Core enum reserves its name in every program");
        Assert.False(typeof(PathRequirementAttribute).IsPublic, "D1.4: the stamp stays internal");
        Assert.True(problems.Count == 0, "R0:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// R2's per-pair exclusions, in the closed form only (<c>unreachable with a user path: &lt;why the edge
    /// cannot fire&gt;</c>): an (entry point, requirement) pair the over-approximating reach finds but no
    /// user path can select. Probing the pair is preferred; a row here is held to the reach both ways.
    /// </summary>
    internal static readonly IReadOnlyDictionary<(string Entry, PathRequirement Requirement), string> PairExclusions =
        new Dictionary<(string, PathRequirement), string>
        {
            [("Sharpy.Builtins.Open(string)", PathRequirement.NeedsFileToWrite)] = ProbeRoster.Unreachable
                + "Open(string) → Open(string, string) → Open(string, string, string) → PathErrors.CheckNeedsFileToWrite; "
                + "Open(string) has no mode parameter and passes the literal \"r\", which never selects the write arm",
            [("Sharpy.Builtins.Open(string)", PathRequirement.CreatesExclusively)] = ProbeRoster.Unreachable
                + "Open(string) → Open(string, string) → Open(string, string, string) → PathErrors.CheckCreatesExclusively; "
                + "Open(string) has no mode parameter and passes the literal \"r\", which never selects the exclusive-create arm",
            [("Sharpy.TarFile.Extract(string, string)", PathRequirement.NeedsFileToRead)] = ProbeRoster.Unreachable
                + "TarFile.Extract → TarFile.OpenReadStream() → PathErrors.TranslateNeedsFileToRead(ex, _name); the door's path is "
                + "the archive's own receiver path (the constructor's position, probed at tarfile.open), never the probed `path`",
            [("Sharpy.TarFile.Extractall(string, Sharpy.List<Sharpy.TarInfo>)", PathRequirement.NeedsFileToRead)] = ProbeRoster.Unreachable
                + "TarFile.Extractall → TarFile.OpenReadStream() → PathErrors.TranslateNeedsFileToRead(ex, _name); the door's path is "
                + "the archive's own receiver path (the constructor's position, probed at tarfile.open), never the probed `path`",
        };

    /// <summary>
    /// R2 (plan-638e8b C2 axis 4): the bound scan lists every reference to a <see cref="PathErrors"/> door
    /// outside <c>PathErrors.cs</c>, reads the door's requirement from its stamp and attributes it to the
    /// entry points that reach it (<see cref="RequirementReach"/>); every (entry point, requirement) pair
    /// has a probe declaring it, and no probe declares a requirement its entry point does not reach. An
    /// entry point excluded as a whole (T3) carries no pairs.
    /// </summary>
    [Fact]
    public void R2_EveryReachedRequirement_IsProbed_AndEveryProbedRequirementIsReached()
    {
        var reach = RequirementReach.Of(Census);
        var entries = Census.EntryPoints;
        var excluded = AllExclusions();
        var declared = ProbeRoster.Probes.SelectMany(p => p.Declared.Select(r => (p.Entry, Requirement: r))).ToHashSet();
        var reached = entries.Keys.Where(k => !excluded.ContainsKey(k))
            .SelectMany(k => reach.TryGetValue(k, out var set) ? set.Keys.Select(r => (Entry: k, Requirement: r)) : Enumerable.Empty<(string Entry, PathRequirement Requirement)>())
            .ToHashSet();

        var problems = new System.Collections.Generic.List<string>();
        foreach (var pair in reached.Where(p => !declared.Contains(p) && !PairExclusions.ContainsKey(p)))
            problems.Add($"({pair.Entry}, {pair.Requirement}): reached via {reach[pair.Entry][pair.Requirement]} but no probe declares it");
        foreach (var pair in declared.Where(p => entries.ContainsKey(p.Entry) && !reached.Contains(p)))
            problems.Add($"({pair.Entry}, {pair.Requirement}): a probe declares it but the entry point reaches only "
                         + $"[{string.Join(", ", reach.TryGetValue(pair.Entry, out var set) ? set.Keys : Enumerable.Empty<PathRequirement>())}]");
        // An asks-no-door probe, both ways: its entry point reaches no stamped door at all.
        foreach (var probe in ProbeRoster.Probes.Where(p => p.Requirement == null))
        {
            if (reach.TryGetValue(probe.Entry, out var set) && set.Count > 0)
                problems.Add($"probe {probe.Id} declares that {probe.Entry} asks no door (`{probe.NoDoor}`), but it reaches "
                             + string.Join("; ", set.Select(kv => $"{kv.Key} via {kv.Value}")));
        }
        foreach (var (pair, why) in PairExclusions)
        {
            if (!why.StartsWith(ProbeRoster.Unreachable, StringComparison.Ordinal))
                problems.Add($"({pair.Entry}, {pair.Requirement}): a pair exclusion reads `{ProbeRoster.Unreachable}<why>`");
            if (!reached.Contains(pair))
                problems.Add($"({pair.Entry}, {pair.Requirement}): a stale pair exclusion (the pair is not reached)");
            if (declared.Contains(pair))
                problems.Add($"({pair.Entry}, {pair.Requirement}): probed and excluded");
        }

        Assert.True(reached.Count >= Enum.GetValues<PathRequirement>().Length, $"R2 must read the reach; found {reached.Count} pair(s)");
        Assert.True(problems.Count == 0, "R2:\n  " + string.Join("\n  ", problems.OrderBy(s => s, StringComparer.Ordinal)));
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
            if ((p.Requirement == null) != !string.IsNullOrWhiteSpace(p.NoDoor))
                problems.Add($"{p.Id}: a probe declares a requirement, or asks no door with a reason — exactly one");
            if (p.Requirement == null && p.Also is { Length: > 0 })
                problems.Add($"{p.Id}: an asks-no-door probe declares no further requirement");
            if (p.Requirement is { } r && p.Also != null && (p.Also.Contains(r) || p.Also.Distinct().Count() != p.Also.Length))
                problems.Add($"{p.Id}: a requirement declared twice");
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
