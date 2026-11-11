using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// The two passes of G1's census (plan-638e8b C2 axis 1), run once per test process: Core first,
/// then the Stdlib with every Core member a door exactly when the Core pass found it reaching one.
/// </summary>
internal sealed class DoorCensus
{
    public const string CensusFileName = "oserror-door-census.txt";
    public const string UpdateSwitch = "UPDATE_DOOR_CENSUS";

    public DoorScanResult Core { get; }
    public DoorScanResult Stdlib { get; }
    public DoorMap Map { get; }

    private DoorCensus(DoorScanResult core, DoorScanResult stdlib, DoorMap map)
    {
        Core = core;
        Stdlib = stdlib;
        Map = map;
    }

    public static readonly Lazy<DoorCensus> Instance = new(() => Run(DoorRoster.Build()));

    public static DoorCensus Run(DoorMap map)
    {
        var core = DoorScan.Run(DoorScan.CoreCompilation(), map, DoorScanOptions.None);
        var stdlib = DoorScan.Run(DoorScan.StdlibCompilation(), map,
            new DoorScanOptions("Sharpy.Core", new HashSet<string>(core.Reaching.Keys, StringComparer.Ordinal)));
        return new DoorCensus(core, stdlib, map);
    }

    public IEnumerable<DoorScanResult> Passes => new[] { Core, Stdlib };

    /// <summary>Every entry point of both passes: Core's (open, …) and the Stdlib's.</summary>
    public SortedDictionary<string, EntryPoint> EntryPoints
    {
        get
        {
            var all = new SortedDictionary<string, EntryPoint>(StringComparer.Ordinal);
            foreach (var pass in Passes)
            {
                foreach (var (k, v) in pass.EntryPoints)
                    all[k] = v;
            }
            return all;
        }
    }

    /// <summary>The census file's lines, sorted: <c>&lt;negative row&gt; :: &lt;path-capable member&gt;</c>.</summary>
    public IReadOnlyList<string> CensusLines()
        => Passes.SelectMany(p => p.Census).Select(c => $"{c.Row} :: {c.Member}")
            .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

    public static string CensusPath => SysPath.Combine(FixtureRoots.RepositoryRoot,
        "src", "Sharpy.Stdlib.Tests", "Conformance", "OSErrorPathState", CensusFileName);

    public const string CensusHeader =
        "# MACHINE-WRITTEN by DoorCensusTests (plan-638e8b C2 axis 1, T2) — never edit by hand.\n" +
        "# Regenerate: UPDATE_DOOR_CENSUS=1 dotnet test src/Sharpy.Stdlib.Tests --filter \"FullyQualifiedName~DoorCensusTests\"\n" +
        "# Every path-capable member (a parameter, or the property or field written, of type string, object or a\n" +
        "# sequence of either) the bound scan finds referenced under a NEGATIVE row of DoorRoster (a namespace row or a\n" +
        "# `not a door` type row). A negative row is a judgment on these members: a new line in review is a member the\n" +
        "# row has not yet judged — re-judge the row (or make its type a door) before regenerating.\n";

    public static IReadOnlyList<string> ReadCensusFile()
    {
        var path = CensusPath;
        if (!File.Exists(path))
            throw new FileNotFoundException($"{CensusFileName} is missing; regenerate it with {UpdateSwitch}=1.", path);
        return File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
    }
}
