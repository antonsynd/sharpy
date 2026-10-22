using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// P54 (#2250) — <b>a compile is a function of its inputs alone</b>: the same under 1, 2 or 8
/// concurrent compilations, on a cold or a warm instance. The interleaving generator: corpus
/// (<see cref="InProcessIsolationCorpus"/>) × seeded per-thread schedules × instance sharing ×
/// thread count × operation, each observation compared with an oracle.
///
/// <para><b>Sharing.</b> <c>compiler-per-thread</c> — each thread builds one <see cref="Compiler"/>
/// (cell 1's instance type; one module registry, built in its constructor, for the whole schedule)
/// and runs its whole schedule on it; <c>api-per-thread</c> — each thread builds one
/// <see cref="CompilerApi"/> (the LSP / harness shape: a fresh <see cref="Compiler"/> per
/// <c>Compile</c>, a cached analysis context per reference set); both reference Sharpy.Core;
/// <c>api-shared</c> — ONE <see cref="CompilerApi"/> serves every thread (the LSP server and the
/// compile server hold one for the life of the process).</para>
///
/// <para><b>Operations.</b> <c>analyze</c> (<see cref="Compiler.Analyze(string, string, CancellationToken, bool)"/>,
/// <see cref="CompilerApi.AnalyzeDocument"/>) and <c>compile</c> (the production pipeline through the
/// C# → IL emit: <see cref="CompilerOptions.OutputAssemblyPath"/> names a per-call temp directory, the
/// image is emitted in memory and written only for a successful build, then deleted). The
/// code-action Extract and applied-text <c>Analyze</c> operations of the plan's matrix are lane
/// "warm"'s LSP twin (<c>Sharpy.Lsp.Tests</c> <c>WarmInstanceParityTests</c>) — this project does
/// not reference the LSP handlers.</para>
///
/// <para><b>Oracle and fingerprint.</b> Before any concurrent phase, every (instance kind, program,
/// operation) a schedule names runs ALONE on the test thread on a FRESH instance — the programs that
/// name no reference FIRST, so the oracle of <c>library/greeter_unreferenced</c> is taken before this
/// run's library has been loaded by anything in the process (its namespace is unique to the run, so
/// no earlier run's copy can answer it either); then a bystander <see cref="Compiler"/> holding the
/// library stays alive through the concurrent phase — the plan's cell-4 shape, "another registry in
/// the process has loaded it". Every program with an <see cref="IsolationProgram.OracleMustCarry"/>
/// control is checked against its oracle on every (kind, operation): the probes read what the seams
/// decide (SPY0220/SPY0203 on a fresh instance; success for the referenced compile; SPY0300 for the
/// unreferenced one), or the run is an instrument fault, not a green. A fingerprint is
/// success + the sorted multiset of diagnostic <c>(code, line, col)</c> + the used-assembly set
/// where the operation exposes one (<c>compile</c> on both kinds — <see cref="CompileResult.UsedAssemblyPaths"/>
/// and the <see cref="Compiler"/>'s registry — and <c>analyze</c> on <see cref="Compiler"/>;
/// <see cref="SemanticResult"/> exposes none, so <c>analyze</c> on a <see cref="CompilerApi"/> compares
/// the first two only) + the type of an escaped exception. A cancelled operation (SPY0901, past
/// <see cref="OperationTimeout"/>) is skipped, never compared.</para>
///
/// <para><b>Buckets</b> (<see cref="Classify"/>): <c>crash</c> — an exception escaped where the oracle
/// returned; <c>order</c> — on ONE thread the warm instance's answer differs from the fresh one's (a
/// warm-sequential divergence: no concurrency is involved); <c>drift</c> — under 2 or 8 threads the
/// answer differs from the solo oracle.</para>
///
/// <para><b>Modes.</b> <see cref="Sweep_EightThreads_EightRepetitions"/> (<c>Category=GapDiscovery</c>,
/// its own CI step) = 8 threads × R=8 repetitions of the same schedules; <see cref="Sweep_RegularSlice"/>
/// (regular suite) = threads {1, 2} × R=1, or the whole matrix {1, 2, 8} × R=8 when
/// <c>SHARPY_ISOLATION_SWEEP_FULL=1</c> is set in the environment (read only — a test never sets it).
/// A cell is red in a repetition when any of its observations in that repetition is; <c>k/R</c> is
/// the number of red repetitions.</para>
///
/// <para><b>Ratchet.</b> Rows <c>program sharing threads operation bucket [probabilistic]  # #NNNN
/// reason; reproduced k/R @ sha</c> in <c>Conformance/in-process-isolation-allowlist.txt</c>. A red
/// cell without a row fails; a row whose cell was exercised and reproduced in no repetition fails as
/// stale (drain on fix) unless it is marked <c>probabilistic</c> — those are reported, not judged. A
/// row of another thread count is not judged; a row of a thread count the mode runs whose cell no
/// schedule carries fails as unscheduled (R-ID, #2343: a row is never dropped by a sample that moved). Reports:
/// <c>.claude/tmp/in-process-isolation-report.json</c> (GapDiscovery) and
/// <c>.claude/tmp/in-process-isolation-slice-report.json</c> (regular slice).</para>
/// </summary>
public class InProcessIsolationSweepTests
{
    private const string AllowlistFileName = "in-process-isolation-allowlist.txt";
    private const string FullModeVariable = "SHARPY_ISOLATION_SWEEP_FULL";
    /// <summary>
    /// A row cites an issue by number (<c>#NNNN</c>) — any issue, so the ratchet outlives the class tracker it was
    /// born under (#2250 is closed): whether the cited issue is still OPEN is <c>build_tools/check_allowlist_issue_state.sh</c>'s
    /// question, asked of every tracked allowlist file.
    /// </summary>
    private static readonly Regex IssueCitePattern = new(@"(^|\s)#\d+\b", RegexOptions.Compiled);
    private const string IssueCite = "#NNNN";
    private const string Probabilistic = "probabilistic";

    internal const string Drift = "drift";
    internal const string Crash = "crash";
    internal const string Order = "order";

    /// <summary>A healthy compile takes well under a second; past this the instance is stuck, not slow.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Worker stack: the compiler recurses over deep ASTs; a stack overflow would take the testhost down.</summary>
    private const int WorkerStackBytes = 16 * 1024 * 1024;

    private readonly ITestOutputHelper _output;

    public InProcessIsolationSweepTests(ITestOutputHelper output)
    {
        _output = output;
    }

    internal enum Sharing
    {
        CompilerPerThread,
        ApiPerThread,
        ApiShared,
    }

    private static readonly Sharing[] AllSharing = { Sharing.CompilerPerThread, Sharing.ApiPerThread, Sharing.ApiShared };

    internal static string Name(Sharing sharing) => sharing switch
    {
        Sharing.CompilerPerThread => "compiler-per-thread",
        Sharing.ApiPerThread => "api-per-thread",
        Sharing.ApiShared => "api-shared",
        _ => throw new ArgumentOutOfRangeException(nameof(sharing)),
    };

    internal static string Name(IsolationOperation operation) => operation switch
    {
        IsolationOperation.Analyze => "analyze",
        IsolationOperation.Compile => "compile",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private enum InstanceKind
    {
        Compiler,
        Api,
    }

    private static InstanceKind KindOf(Sharing sharing) => sharing == Sharing.CompilerPerThread ? InstanceKind.Compiler : InstanceKind.Api;

    // ================================================================
    // Modes
    // ================================================================

    [Fact]
    [Trait("Category", "GapDiscovery")]
    public void Sweep_EightThreads_EightRepetitions()
        => RunSweep("gap-discovery", new[] { 8 }, repetitions: 8, fullTriggers: true, "in-process-isolation-report.json");

    [Fact]
    public void Sweep_RegularSlice()
    {
        if (Environment.GetEnvironmentVariable(FullModeVariable) == "1")
            RunSweep("full", new[] { 1, 2, 8 }, repetitions: 8, fullTriggers: true, "in-process-isolation-slice-report.json");
        else
            RunSweep("regular", new[] { 1, 2 }, repetitions: 1, fullTriggers: false, "in-process-isolation-slice-report.json");
    }

    // ================================================================
    // Controls
    // ================================================================

    /// <summary>
    /// The oracle discriminates: two DIFFERENT programs observed through the sweep's own
    /// <see cref="Observe"/> have different fingerprints, and <see cref="Classify"/> buckets the
    /// difference by thread count (order on 1, drift on 2 and 8) and an escaped exception as crash;
    /// the same program on two fresh instances is equal (the no-difference control), and a diagnostic
    /// moved by one line is a different fingerprint (positions are compared, not just codes).
    /// </summary>
    [Fact]
    public void Oracle_DiscriminatesPrograms_PositionsAndBuckets()
    {
        var clean = new IsolationProgram("control/clean", "control.spy", "def main() -> None:\n    print(1)\n");
        var refused = new IsolationProgram("control/refused", "control.spy", "def main() -> None:\n    print(undefined_name)\n");
        var moved = new IsolationProgram("control/moved", "control.spy", "\ndef main() -> None:\n    print(undefined_name)\n");

        foreach (var kind in new[] { InstanceKind.Compiler, InstanceKind.Api })
        {
            foreach (var operation in new[] { IsolationOperation.Analyze, IsolationOperation.Compile })
            {
                var a = Solo(kind, new IsolationStep(clean, operation));
                var a2 = Solo(kind, new IsolationStep(clean, operation));
                var b = Solo(kind, new IsolationStep(refused, operation));
                var c = Solo(kind, new IsolationStep(moved, operation));
                var cell = $"{kind}/{Name(operation)}";

                Assert.False(a.Cancelled || b.Cancelled || c.Cancelled, cell);
                Assert.True(a.Exception == null && b.Exception == null && c.Exception == null, $"{cell}: {a.Exception}{b.Exception}{c.Exception}");
                Assert.True(a.Success, $"{cell}: the clean control must compile: {a}");
                Assert.False(b.Success, $"{cell}: the refused control must be refused: {b}");
                Assert.Equal(a, a2);
                Assert.Null(Classify(a, a2, threads: 1));
                Assert.NotEqual(a, b);
                Assert.NotEqual(b, c);
                Assert.Equal(b.Diagnostics.Split(',').Select(d => d.Split('@')[0]), c.Diagnostics.Split(',').Select(d => d.Split('@')[0]));
                Assert.Equal(Order, Classify(a, b, threads: 1));
                Assert.Equal(Drift, Classify(a, b, threads: 2));
                Assert.Equal(Drift, Classify(b, c, threads: 8));
                Assert.Equal(Crash, Classify(a, a with { Exception = nameof(InvalidOperationException) }, threads: 8));
            }
        }
    }

    /// <summary>The schedules are pure functions of the literal seeds; every thread's schedule opens
    /// with the library pair, carries every named program and probe under both operations and every
    /// trigger under analyze, and reads every probe once more after its last trigger; the regular
    /// trigger set is a head of the full one; every probe and the pair carry an oracle control.</summary>
    /// <summary>
    /// R-ID (#2343): adding a program moves only that program. Inserting a test name changes the
    /// sample by at most that name, and inserting a sample member changes no thread's schedule except
    /// by that member's own steps — the stride-by-index sample this replaced moved 194 of 213 members
    /// for one new fixture. Membership and ranks are pure functions of a literal string:
    /// <see cref="InProcessIsolationCorpus.StableHash"/> is pinned to FNV-1a's published vectors.
    /// </summary>
    [Fact]
    public void Sample_AndSchedules_MoveOnlyTheInsertedProgram()
    {
        Assert.Equal(2166136261u, InProcessIsolationCorpus.StableHash(""));
        Assert.Equal(0xe40c292cu, InProcessIsolationCorpus.StableHash("a"));
        Assert.Equal(0xbf9cf968u, InProcessIsolationCorpus.StableHash("foobar"));

        var names = Enumerable.Range(0, 4000).Select(i => $"corpus/fixture_{i:D4}").ToList();
        var before = InProcessIsolationCorpus.SelectSample(names);
        Assert.InRange(before.Count, 4000 / InProcessIsolationCorpus.SampleStride / 2, 4000 / InProcessIsolationCorpus.SampleStride * 2);
        var inserted = 0;
        foreach (var extra in Enumerable.Range(0, 64).Select(i => $"corpus/aaa_inserted_{i}"))
        {
            var after = InProcessIsolationCorpus.SelectSample(names.Append(extra));
            Assert.Equal(before, after.Where(n => n != extra));
            if (after.Contains(extra))
                inserted++;
        }

        Assert.True(inserted > 0, "positive control: some inserted name must join the sample, or the schedule half below proves nothing");

        var pair = new[]
        {
            new IsolationProgram(InProcessIsolationCorpus.ReferencedProgramId, "main.spy", ""),
            new IsolationProgram(InProcessIsolationCorpus.UnreferencedProgramId, "main.spy", ""),
        };
        var roster = InProcessIsolationCorpus.Roster(full: false, pair);
        var sample = InProcessIsolationCorpus.Sample;
        var newcomer = new IsolationProgram("aaa/inserted_first", "x.spy", "");
        var drawnSomewhere = false;
        for (var t = 0; t < InProcessIsolationCorpus.ThreadSeeds.Length; t++)
        {
            var baseline = InProcessIsolationCorpus.Schedule(t, roster, sample).Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
            var grown = InProcessIsolationCorpus.Schedule(t, roster, new[] { newcomer }.Concat(sample).ToList())
                .Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
            drawnSomewhere |= grown.Count > baseline.Count;
            Assert.Equal(baseline, grown.Where(s => !s.StartsWith(newcomer.Id + "/", StringComparison.Ordinal)));
        }

        Assert.True(drawnSomewhere, "positive control: the newcomer must be drawn by at least one thread");
    }

    [Fact]
    public void Schedules_AreSeededAndCarryTheRoster()
    {
        Assert.Equal(8, InProcessIsolationCorpus.ThreadSeeds.Distinct().Count());
        Assert.StartsWith(InProcessIsolationCorpus.Cell1FirstLine, InProcessIsolationCorpus.RegenerateCell1Program().Source, StringComparison.Ordinal);
        Assert.True(InProcessIsolationCorpus.Sample.Count > 100, $"hash-keyed sample has {InProcessIsolationCorpus.Sample.Count} programs");

        var pair = new[]
        {
            new IsolationProgram(InProcessIsolationCorpus.ReferencedProgramId, "main.spy", "") { References = new[] { "x.dll" }, OracleMustCarry = IsolationProgram.OracleSucceeds },
            new IsolationProgram(InProcessIsolationCorpus.UnreferencedProgramId, "main.spy", "") { OracleMustCarry = InProcessIsolationCorpus.ModuleNotFound },
        };
        var regular = InProcessIsolationCorpus.Roster(full: false, pair);
        var full = InProcessIsolationCorpus.Roster(full: true, pair);
        Assert.Equal(WarmInstanceTriggers.AllTexts.Length + InProcessIsolationCorpus.RegularGeneratedTriggerCount, regular.Triggers.Count);
        Assert.Equal(regular.Triggers.Select(t => t.Id), full.Triggers.Take(regular.Triggers.Count).Select(t => t.Id));
        Assert.True(full.Triggers.Count > regular.Triggers.Count, $"full carries {full.Triggers.Count} triggers");
        Assert.Equal(full.Triggers.Count, full.Triggers.Select(t => t.Id).Distinct().Count());
        Assert.All(full.Probes.Concat(pair), p => Assert.NotNull(p.OracleMustCarry));
        Assert.Contains(full.Probes, p => p.OracleMustCarry == "SPY0220");
        Assert.Contains(full.Probes, p => p.OracleMustCarry == "SPY0203");

        foreach (var roster in new[] { regular, full })
        {
            for (var t = 0; t < InProcessIsolationCorpus.ThreadSeeds.Length; t++)
            {
                var first = InProcessIsolationCorpus.Schedule(t, roster).Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
                var second = InProcessIsolationCorpus.Schedule(t, roster).Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
                Assert.Equal(first, second);
                Assert.Equal(
                    pair.SelectMany(p => new[] { $"{p.Id}/{IsolationOperation.Analyze}", $"{p.Id}/{IsolationOperation.Compile}" }),
                    first.Take(4));
                foreach (var program in roster.Named.Concat(roster.Probes))
                {
                    Assert.Contains($"{program.Id}/{IsolationOperation.Analyze}", first);
                    Assert.Contains($"{program.Id}/{IsolationOperation.Compile}", first);
                }

                var lastTrigger = first.FindLastIndex(s => s.StartsWith("trigger/", StringComparison.Ordinal));
                Assert.True(lastTrigger >= 0, "the schedule carries no trigger");
                foreach (var program in roster.Triggers)
                    Assert.Contains($"{program.Id}/{IsolationOperation.Analyze}", first);
                foreach (var program in roster.Probes)
                    Assert.Contains($"{program.Id}/{IsolationOperation.Analyze}", first.Skip(lastTrigger + 1));
            }
        }

        Assert.NotEqual(
            InProcessIsolationCorpus.Schedule(0, regular).Select(s => s.Program.Id),
            InProcessIsolationCorpus.Schedule(1, regular).Select(s => s.Program.Id));
    }

    // ================================================================
    // The sweep
    // ================================================================

    private sealed record Observation(int Repetition, Sharing Sharing, int Threads, int Thread, int Step, IsolationStep Work, Fingerprint Fingerprint);

    private sealed record RedCell(string Key, string Bucket, int Reds, int Repetitions, string Oracle, string Observed);

    private void RunSweep(string mode, int[] threadCounts, int repetitions, bool fullTriggers, string reportFileName)
    {
        var wall = Stopwatch.StartNew();
        var maxThreads = threadCounts.Max();
        var runTag = Guid.NewGuid().ToString("N").Substring(0, 8);
        var libraryDir = Path.Combine(Path.GetTempPath(), $"sharpy_isolation_lib_{runTag}");
        try
        {
            var roster = InProcessIsolationCorpus.Roster(fullTriggers, InProcessIsolationCorpus.BuildLibraryPair(libraryDir, runTag));
            var schedules = Enumerable.Range(0, maxThreads).Select(t => InProcessIsolationCorpus.Schedule(t, roster)).ToList();

            // 1. The solo oracle, before any concurrent phase: a fresh instance per (kind, program, operation) —
            //    reference-free programs first, so the unreferenced library compile is answered before this run's
            //    library has been loaded by anything in the process.
            var oracle = new Dictionary<(InstanceKind, string, IsolationOperation), Fingerprint>();
            foreach (var referenced in new[] { false, true })
            {
                foreach (var kind in new[] { InstanceKind.Compiler, InstanceKind.Api })
                {
                    foreach (var step in schedules.SelectMany(s => s).Where(s => (s.Program.References.Count > 0) == referenced))
                    {
                        var key = (kind, step.Program.Id, step.Operation);
                        if (!oracle.ContainsKey(key))
                            oracle[key] = Solo(kind, step);
                    }
                }
            }

            var oracleSeconds = wall.Elapsed.TotalSeconds;
            AssertOracleControls(oracle, roster);

            // The bystander: a registry that has loaded this run's library and lives through the concurrent phase.
            using var bystander = IsolationInstance.Create(InstanceKind.Compiler);
            var bystanderReading = bystander.Observe(new IsolationStep(roster.LibraryPair[0], IsolationOperation.Analyze));
            Assert.True(bystanderReading.Success, $"the bystander's referenced analysis must succeed: {bystanderReading}");

            // 2. The concurrent phase: repetitions × sharing × threads, each a barrier-started set of workers.
            var observations = new List<Observation>();
            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                foreach (var sharing in AllSharing)
                {
                    foreach (var threads in threadCounts)
                        observations.AddRange(RunConfiguration(repetition, sharing, threads, schedules));
                }
            }

            Judge(mode, threadCounts, repetitions, reportFileName, roster, oracle, observations, oracleSeconds, wall);
        }
        finally
        {
            try
            {
                if (Directory.Exists(libraryDir))
                    Directory.Delete(libraryDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; the loaded library may still be mapped.
            }
        }
    }

    /// <summary>
    /// Every oracle control holds on every (kind, operation): a probe's fresh answer carries the code
    /// it reads for, the referenced compile succeeds, the unreferenced one is refused — else the
    /// probe is not reading what the seam decides and the run is an instrument fault.
    /// </summary>
    private static void AssertOracleControls(IReadOnlyDictionary<(InstanceKind, string, IsolationOperation), Fingerprint> oracle, IsolationRoster roster)
    {
        var problems = new List<string>();
        foreach (var program in roster.Probes.Concat(roster.LibraryPair).Where(p => p.OracleMustCarry != null))
        {
            foreach (var kind in new[] { InstanceKind.Compiler, InstanceKind.Api })
            {
                foreach (var operation in new[] { IsolationOperation.Analyze, IsolationOperation.Compile })
                {
                    var fingerprint = oracle[(kind, program.Id, operation)];
                    var holds = program.OracleMustCarry == IsolationProgram.OracleSucceeds
                        ? fingerprint.Success && fingerprint.Exception == null
                        : !fingerprint.Success && fingerprint.Diagnostics.Contains(program.OracleMustCarry + "@", StringComparison.Ordinal);
                    if (!holds)
                        problems.Add($"{program.Id} {kind}/{Name(operation)}: the solo oracle must carry {program.OracleMustCarry}; observed {fingerprint}");
                }
            }
        }

        Assert.True(problems.Count == 0, "oracle control(s) failed (an instrument fault, not a cell):\n  " + string.Join("\n  ", problems));
    }

    private void Judge(string mode, int[] threadCounts, int repetitions, string reportFileName, IsolationRoster roster,
        Dictionary<(InstanceKind, string, IsolationOperation), Fingerprint> oracle, List<Observation> observations, double oracleSeconds, Stopwatch wall)
    {

        // 3. Classification: a cell is (program, sharing, threads, operation); red in a repetition when any observation is.
        var exercised = new HashSet<string>(StringComparer.Ordinal);
        var cancelledCells = new HashSet<string>(StringComparer.Ordinal);
        var redByKey = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var example = new Dictionary<string, (string Oracle, string Observed)>(StringComparer.Ordinal);
        var cancelled = 0;
        var observed = 0;
        foreach (var o in observations)
        {
            var solo = oracle[(KindOf(o.Sharing), o.Work.Program.Id, o.Work.Operation)];
            var cell = $"{o.Work.Program.Id} {Name(o.Sharing)} {o.Threads} {Name(o.Work.Operation)}";
            if (o.Fingerprint.Cancelled || solo.Cancelled)
            {
                cancelled++;
                cancelledCells.Add(cell);
                continue;
            }

            observed++;
            exercised.Add(cell);
            var bucket = Classify(solo, o.Fingerprint, o.Threads);
            if (bucket == null)
                continue;
            var key = $"{cell} {bucket}";
            if (!redByKey.TryGetValue(key, out var reps))
                redByKey[key] = reps = new HashSet<int>();
            reps.Add(o.Repetition);
            example.TryAdd(key, (solo.ToString(), o.Fingerprint.ToString()));
        }

        var reds = redByKey
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new RedCell(kv.Key, kv.Key.Split(' ')[4], kv.Value.Count, repetitions, example[kv.Key].Oracle, example[kv.Key].Observed))
            .ToList();

        // 4. The ratchet.
        var allowlist = LoadAllowlist(InProcessIsolationCorpus.AllIds);
        var unlisted = reds.Where(r => !allowlist.ContainsKey(r.Key)).ToList();
        var notExercised = allowlist.Keys.Where(k => !exercised.Contains(CellOf(k))).OrderBy(k => k, StringComparer.Ordinal).ToList();
        // A row of a thread count this mode runs whose cell no schedule carried: the sample or a thread's
        // draws moved off it. Reported, never dropped (R-ID, #2343) — a cell only cancellations hid is not one.
        var unscheduled = notExercised
            .Where(k => threadCounts.Contains(int.Parse(k.Split(' ')[2])) && !cancelledCells.Contains(CellOf(k)))
            .ToList();
        var silent = allowlist
            .Where(kv => exercised.Contains(CellOf(kv.Key)) && !redByKey.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();
        var stale = silent.Where(kv => !kv.Value.Probabilistic).Select(kv => kv.Key).ToList();
        var silentProbabilistic = silent.Where(kv => kv.Value.Probabilistic).Select(kv => kv.Key).ToList();

        var bucketCounts = new[] { Drift, Crash, Order }.ToDictionary(b => b, b => reds.Count(r => r.Bucket == b));
        var census = $"ISOLATION-CENSUS mode={mode} threads={{{string.Join(",", threadCounts)}}} R={repetitions} "
            + $"corpus={roster.Named.Count}+{roster.LibraryPair.Count}+{roster.Triggers.Count}+{roster.Probes.Count}+{InProcessIsolationCorpus.Sample.Count} "
            + $"oracle={oracle.Count} observations={observations.Count} compared={observed} cancelled={cancelled} "
            + $"cells={exercised.Count} red={reds.Count} drift={bucketCounts[Drift]} crash={bucketCounts[Crash]} order={bucketCounts[Order]} "
            + $"allowlisted={allowlist.Count} oracle-elapsed={oracleSeconds:F0}s elapsed={wall.Elapsed.TotalSeconds:F0}s";
        _output.WriteLine(census);
        foreach (var red in reds)
            _output.WriteLine($"ISOLATION {red.Key} reproduced {red.Reds}/{red.Repetitions}\n    solo:     {red.Oracle}\n    observed: {red.Observed}");
        foreach (var key in silentProbabilistic)
            _output.WriteLine($"ISOLATION probabilistic row not reproduced this run: {key}");

        WriteReport(reportFileName, new
        {
            census,
            mode,
            threads = threadCounts,
            repetitions,
            buckets = bucketCounts,
            reds = reds.Select(r => new { cell = r.Key, bucket = r.Bucket, reproduced = $"{r.Reds}/{r.Repetitions}", solo = r.Oracle, observed = r.Observed }),
            unlisted = unlisted.Select(r => r.Key),
            stale,
            silentProbabilistic,
            notExercised,
            unscheduled,
            oracle = oracle
                .OrderBy(kv => kv.Key.Item2, StringComparer.Ordinal).ThenBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item3)
                .Select(kv => new { kind = kv.Key.Item1.ToString(), program = kv.Key.Item2, operation = Name(kv.Key.Item3), fingerprint = kv.Value.ToString() }),
        });

        var problems = new List<string>();
        problems.AddRange(unlisted.Select(r =>
            $"{r.Key}: reproduced {r.Reds}/{r.Repetitions} — the {r.Bucket} is not allowlisted.\n      solo:     {r.Oracle}\n      observed: {r.Observed}"));
        problems.AddRange(stale.Select(k =>
            $"{k}: exercised and reproduced 0/{repetitions} — drain the row from Conformance/{AllowlistFileName} (or mark it `{Probabilistic}` with its measured k/R)."));
        problems.AddRange(unscheduled.Select(k =>
            $"{k}: this mode runs {k.Split(' ')[2]} thread(s) but no schedule carries the cell — the sample or a thread's draws moved off it; re-measure the defect and drain or re-pin the row."));
        Assert.True(problems.Count == 0,
            $"{problems.Count} in-process isolation violation(s) ({mode}):\n  " + string.Join("\n  ", problems)
            + $"\nReport: .claude/tmp/{reportFileName}");
    }

    private static string CellOf(string key) => key.Substring(0, key.LastIndexOf(' '));

    private static List<Observation> RunConfiguration(int repetition, Sharing sharing, int threads, IReadOnlyList<IReadOnlyList<IsolationStep>> schedules)
    {
        var observations = new ConcurrentBag<Observation>();
        var failures = new ConcurrentBag<Exception>();
        using var shared = sharing == Sharing.ApiShared ? IsolationInstance.Create(InstanceKind.Api) : null;
        using var barrier = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(t => new Thread(() =>
        {
            try
            {
                using var own = shared == null ? IsolationInstance.Create(KindOf(sharing)) : null;
                var instance = shared ?? own!;
                barrier.SignalAndWait();
                var schedule = schedules[t];
                for (var step = 0; step < schedule.Count; step++)
                    observations.Add(new Observation(repetition, sharing, threads, t, step, schedule[step], instance.Observe(schedule[step])));
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }, WorkerStackBytes)
        { IsBackground = true, Name = $"isolation-{Name(sharing)}-{threads}-{t}" }).ToList();

        foreach (var worker in workers)
            worker.Start();
        foreach (var worker in workers)
            worker.Join();

        Assert.True(failures.IsEmpty, "the sweep's own worker failed (an instrument fault, not a cell): " + string.Join("\n", failures));
        return observations.ToList();
    }

    /// <summary>The bucket of an observation against its solo oracle, or null when they agree.</summary>
    internal static string? Classify(Fingerprint solo, Fingerprint observed, int threads)
    {
        if (observed.Exception != null && solo.Exception == null)
            return Crash;
        if (observed == solo)
            return null;
        return threads == 1 ? Order : Drift;
    }

    private static Fingerprint Solo(InstanceKind kind, IsolationStep step)
    {
        using var instance = IsolationInstance.Create(kind);
        return instance.Observe(step);
    }

    // ================================================================
    // Instances and fingerprints
    // ================================================================

    /// <summary>
    /// What one operation answered. Equality is the comparison: success, the sorted
    /// <c>(code, line, col)</c> multiset, the used-assembly set (null where not exposed) and the type
    /// of an escaped exception. <see cref="Cancelled"/> marks an observation that is never compared.
    /// </summary>
    internal sealed record Fingerprint(bool Success, string Diagnostics, string? UsedAssemblies, string? Exception, bool Cancelled)
    {
        /// <summary>The first error's message, or the escaped exception's — for the report only, never compared.</summary>
        public string Detail { get; init; } = "";

        public bool Equals(Fingerprint? other)
            => other is not null && Success == other.Success && Diagnostics == other.Diagnostics
                && UsedAssemblies == other.UsedAssemblies && Exception == other.Exception && Cancelled == other.Cancelled;

        public override int GetHashCode() => HashCode.Combine(Success, Diagnostics, UsedAssemblies, Exception, Cancelled);

        public override string ToString()
            => (Exception != null
                ? $"threw {Exception}"
                : $"success={Success} diagnostics=[{Diagnostics}] used=[{UsedAssemblies ?? "n/a"}]{(Cancelled ? " CANCELLED" : "")}")
               + (Detail.Length == 0 ? "" : $" — {Detail}");

        internal static Fingerprint Of(bool success, IEnumerable<CompilerDiagnostic> diagnostics, IEnumerable<string>? usedAssemblies)
        {
            var list = diagnostics.ToList();
            var cancelled = list.Any(d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
            var multiset = string.Join(",", list
                .Select(d => $"{d.Code ?? "-"}@{d.Line ?? 0}:{d.Column ?? 0}")
                .OrderBy(s => s, StringComparer.Ordinal));
            var used = usedAssemblies == null
                ? null
                : string.Join(";", usedAssemblies.OrderBy(p => p, StringComparer.Ordinal));
            var firstError = list.FirstOrDefault(d => d.IsError)?.Message ?? "";
            return new Fingerprint(success, multiset, used, null, cancelled) { Detail = FirstLine(firstError) };
        }

        internal static Fingerprint Threw(Exception ex)
            => new(false, "", null, ex.GetType().FullName, false) { Detail = FirstLine(ex.Message) };

        private static string FirstLine(string text)
        {
            var line = text.Split('\n')[0];
            return line.Length <= 240 ? line : line.Substring(0, 240) + "…";
        }
    }

    /// <summary>One compiler instance of a sharing mode, with the scratch directory its compiles write into.</summary>
    private abstract class IsolationInstance : IDisposable
    {
        /// <summary>
        /// Named as the CLI names it (<see cref="CompilerOptionsFactory.ForCli"/>): with an output path
        /// and no assembly name a single-file compile emits an assembly named <c>""</c> — SPY0908
        /// CS8203 (measured; a findings-ledger row, outside #2250).
        /// </summary>
        protected const string AssemblyName = "IsolationProgram";

        protected readonly string Root = Path.Combine(Path.GetTempPath(), $"sharpy_isolation_{Guid.NewGuid():N}");

        internal static IsolationInstance Create(InstanceKind kind)
            => kind == InstanceKind.Compiler ? new CompilerInstance() : new ApiInstance();

        internal Fingerprint Observe(IsolationStep step)
        {
            try
            {
                using var cts = new CancellationTokenSource(OperationTimeout);
                return step.Operation == IsolationOperation.Analyze
                    ? Analyze(step.Program, cts.Token)
                    : Compile(step.Program, cts.Token);
            }
            catch (Exception ex)
            {
                return Fingerprint.Threw(ex);
            }
        }

        protected abstract Fingerprint Analyze(IsolationProgram program, CancellationToken cancellationToken);

        protected abstract Fingerprint Compile(IsolationProgram program, CancellationToken cancellationToken);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>
    /// One <see cref="Compiler"/> for the instance's life — its module registry is built once, in the
    /// constructor, and serves every reference-free program of the schedule; a program that names a
    /// reference gets a sibling <see cref="Compiler"/> on the same instance (one per distinct
    /// reference set, built on first use — a <see cref="Compiler"/> binds its references at
    /// construction), so the leak the library pair guards flows between two registries on one
    /// thread as well as across threads. Sharpy.Core is referenced (without it the C# → IL emit of
    /// <c>print(1)</c> is SPY0908 — measured), and the output path makes <c>Compile</c> reach IL.
    /// </summary>
    private sealed class CompilerInstance : IsolationInstance
    {
        private readonly Dictionary<string, Compiler> _compilers = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public CompilerInstance()
        {
            Directory.CreateDirectory(Root);
            _compilers[""] = Build(Array.Empty<string>());
        }

        private Compiler Build(IReadOnlyList<string> references)
            => new(CompilerOptionsFactory.ForCli(
                references: new[] { SharpyCoreReference.Location }.Concat(references).ToArray(),
                assemblyName: AssemblyName,
                outputAssemblyPath: Path.Combine(Root, AssemblyName + ".dll")), NullLogger.Instance);

        private Compiler For(IsolationProgram program)
        {
            var key = string.Join(";", program.References);
            lock (_gate)
            {
                if (!_compilers.TryGetValue(key, out var compiler))
                    _compilers[key] = compiler = Build(program.References);
                return compiler;
            }
        }

        protected override Fingerprint Analyze(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = For(program).Analyze(program.Source, program.FileName, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics.GetAll(), result.UsedAssemblyPaths);
        }

        protected override Fingerprint Compile(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = For(program).Compile(program.Source, program.FileName, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics.GetAll(), result.UsedAssemblyPaths);
        }
    }

    /// <summary>One <see cref="CompilerApi"/> referencing Sharpy.Core; a program's own references ride on its options; each <c>Compile</c> writes into its own directory.</summary>
    private sealed class ApiInstance : IsolationInstance
    {
        private readonly CompilerApi _api = new(NullLogger.Instance, new[] { SharpyCoreReference.Location });

        protected override Fingerprint Analyze(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = _api.AnalyzeDocument(program.Source, program.FileName, CompilerOptionsFactory.ForCli(references: program.References.ToArray()),
                stageMetrics: null, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics, usedAssemblies: null);
        }

        protected override Fingerprint Compile(IsolationProgram program, CancellationToken cancellationToken)
        {
            var callDir = Path.Combine(Root, Guid.NewGuid().ToString("N"));
            try
            {
                var options = CompilerOptionsFactory.ForCli(
                    references: program.References.ToArray(),
                    assemblyName: AssemblyName,
                    outputAssemblyPath: Path.Combine(callDir, AssemblyName + ".dll"));
                var result = _api.Compile(program.Source, options, program.FileName, cancellationToken);
                return Fingerprint.Of(result.Success, result.Diagnostics, result.UsedAssemblyPaths);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(callDir))
                        Directory.Delete(callDir, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    // ================================================================
    // Allowlist and report
    // ================================================================

    private sealed record AllowlistRow(bool Probabilistic, string Comment);

    /// <summary>
    /// <c>program sharing threads operation bucket [probabilistic]  # #NNNN reason; reproduced k/R @ sha</c>.
    /// A row that does not parse, names a program that is no corpus id (<paramref name="corpusIds"/>:
    /// a typo would park a red cell as "not exercised"), names an unknown sharing/operation/bucket, or
    /// cites no issue number is refused (an instrument fault), never silently skipped.
    /// </summary>
    private static Dictionary<string, AllowlistRow> LoadAllowlist(IReadOnlySet<string> corpusIds)
    {
        var path = Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");

        var sharingNames = AllSharing.Select(Name).ToHashSet(StringComparer.Ordinal);
        var operationNames = new[] { IsolationOperation.Analyze, IsolationOperation.Compile }.Select(Name).ToHashSet(StringComparer.Ordinal);
        var rows = new Dictionary<string, AllowlistRow>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var comment = split < 0 ? "" : line.Substring(split + 2).Trim();
            var fields = (split < 0 ? line : line.Substring(0, split)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var probabilistic = fields.Length == 6 && fields[5] == Probabilistic;
            if (fields.Length != 5 && !probabilistic)
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — expected `program sharing threads operation bucket [{Probabilistic}]  # {IssueCite} …`.");
            if (!corpusIds.Contains(fields[0]))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — '{fields[0]}' is not a corpus program id (InProcessIsolationCorpus.AllIds).");
            if (!sharingNames.Contains(fields[1]) || fields[2] is not ("1" or "2" or "8") || !operationNames.Contains(fields[3])
                || fields[4] is not (Drift or Crash or Order))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — unknown sharing, thread count, operation or bucket.");
            if (!IssueCitePattern.IsMatch(comment))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — every row cites an issue by number ({IssueCite}).");
            var key = string.Join(' ', fields.Take(5));
            if (!rows.TryAdd(key, new AllowlistRow(probabilistic, comment)))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{key}' is listed twice.");
        }

        return rows;
    }

    private void WriteReport(string fileName, object report)
    {
        var reportDir = Path.Combine(FixtureRoots.RepositoryRoot, ".claude", "tmp");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, fileName);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to: {reportPath}");
    }
}
