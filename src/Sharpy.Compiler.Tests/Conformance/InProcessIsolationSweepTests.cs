using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
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
/// operation) a schedule names runs ALONE on the test thread on a FRESH instance. A fingerprint is
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
/// <para><b>Ratchet.</b> Rows <c>program sharing threads operation bucket [probabilistic]  # #2250
/// reason; reproduced k/R @ sha</c> in <c>Conformance/in-process-isolation-allowlist.txt</c>. A red
/// cell without a row fails; a row whose cell was exercised and reproduced in no repetition fails as
/// stale (drain on fix) unless it is marked <c>probabilistic</c> — those are reported, not judged. A
/// row whose cell the mode does not exercise (another thread count) is neither. Reports:
/// <c>.claude/tmp/in-process-isolation-report.json</c> (GapDiscovery) and
/// <c>.claude/tmp/in-process-isolation-slice-report.json</c> (regular slice).</para>
/// </summary>
public class InProcessIsolationSweepTests
{
    private const string AllowlistFileName = "in-process-isolation-allowlist.txt";
    private const string FullModeVariable = "SHARPY_ISOLATION_SWEEP_FULL";
    private const string IssueCite = "#2250";
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
        => RunSweep("gap-discovery", new[] { 8 }, repetitions: 8, "in-process-isolation-report.json");

    [Fact]
    public void Sweep_RegularSlice()
    {
        if (Environment.GetEnvironmentVariable(FullModeVariable) == "1")
            RunSweep("full", new[] { 1, 2, 8 }, repetitions: 8, "in-process-isolation-slice-report.json");
        else
            RunSweep("regular", new[] { 1, 2 }, repetitions: 1, "in-process-isolation-slice-report.json");
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

    /// <summary>The schedules are pure functions of the literal seeds, and every thread's schedule
    /// carries every named program under both operations.</summary>
    [Fact]
    public void Schedules_AreSeededAndCarryTheRoster()
    {
        Assert.Equal(8, InProcessIsolationCorpus.ThreadSeeds.Distinct().Count());
        Assert.StartsWith(InProcessIsolationCorpus.Cell1FirstLine, InProcessIsolationCorpus.RegenerateCell1Program().Source, StringComparison.Ordinal);
        Assert.True(InProcessIsolationCorpus.Sample.Count > 100, $"stride sample has {InProcessIsolationCorpus.Sample.Count} programs");
        for (var t = 0; t < InProcessIsolationCorpus.ThreadSeeds.Length; t++)
        {
            var first = InProcessIsolationCorpus.Schedule(t).Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
            var second = InProcessIsolationCorpus.Schedule(t).Select(s => $"{s.Program.Id}/{s.Operation}").ToList();
            Assert.Equal(first, second);
            foreach (var program in InProcessIsolationCorpus.Named)
            {
                Assert.Contains($"{program.Id}/{IsolationOperation.Analyze}", first);
                Assert.Contains($"{program.Id}/{IsolationOperation.Compile}", first);
            }
        }

        Assert.NotEqual(
            InProcessIsolationCorpus.Schedule(0).Select(s => s.Program.Id),
            InProcessIsolationCorpus.Schedule(1).Select(s => s.Program.Id));
    }

    // ================================================================
    // The sweep
    // ================================================================

    private sealed record Observation(int Repetition, Sharing Sharing, int Threads, int Thread, int Step, IsolationStep Work, Fingerprint Fingerprint);

    private sealed record RedCell(string Key, string Bucket, int Reds, int Repetitions, string Oracle, string Observed);

    private void RunSweep(string mode, int[] threadCounts, int repetitions, string reportFileName)
    {
        var wall = Stopwatch.StartNew();
        var maxThreads = threadCounts.Max();
        var schedules = Enumerable.Range(0, maxThreads).Select(InProcessIsolationCorpus.Schedule).ToList();

        // 1. The solo oracle, before any concurrent phase: a fresh instance per (kind, program, operation).
        var oracle = new Dictionary<(InstanceKind, string, IsolationOperation), Fingerprint>();
        foreach (var kind in new[] { InstanceKind.Compiler, InstanceKind.Api })
        {
            foreach (var step in schedules.SelectMany(s => s))
            {
                var key = (kind, step.Program.Id, step.Operation);
                if (!oracle.ContainsKey(key))
                    oracle[key] = Solo(kind, step);
            }
        }

        var oracleSeconds = wall.Elapsed.TotalSeconds;

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

        // 3. Classification: a cell is (program, sharing, threads, operation); red in a repetition when any observation is.
        var exercised = new HashSet<string>(StringComparer.Ordinal);
        var redByKey = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var example = new Dictionary<string, (string Oracle, string Observed)>(StringComparer.Ordinal);
        var cancelled = 0;
        var observed = 0;
        foreach (var o in observations)
        {
            var solo = oracle[(KindOf(o.Sharing), o.Work.Program.Id, o.Work.Operation)];
            if (o.Fingerprint.Cancelled || solo.Cancelled)
            {
                cancelled++;
                continue;
            }

            observed++;
            var cell = $"{o.Work.Program.Id} {Name(o.Sharing)} {o.Threads} {Name(o.Work.Operation)}";
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
        var allowlist = LoadAllowlist();
        var unlisted = reds.Where(r => !allowlist.ContainsKey(r.Key)).ToList();
        var notExercised = allowlist.Keys.Where(k => !exercised.Contains(CellOf(k))).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var silent = allowlist
            .Where(kv => exercised.Contains(CellOf(kv.Key)) && !redByKey.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();
        var stale = silent.Where(kv => !kv.Value.Probabilistic).Select(kv => kv.Key).ToList();
        var silentProbabilistic = silent.Where(kv => kv.Value.Probabilistic).Select(kv => kv.Key).ToList();

        var bucketCounts = new[] { Drift, Crash, Order }.ToDictionary(b => b, b => reds.Count(r => r.Bucket == b));
        var census = $"ISOLATION-CENSUS mode={mode} threads={{{string.Join(",", threadCounts)}}} R={repetitions} "
            + $"corpus={InProcessIsolationCorpus.Named.Count}+{InProcessIsolationCorpus.Sample.Count} "
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
            oracle = oracle
                .OrderBy(kv => kv.Key.Item2, StringComparer.Ordinal).ThenBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item3)
                .Select(kv => new { kind = kv.Key.Item1.ToString(), program = kv.Key.Item2, operation = Name(kv.Key.Item3), fingerprint = kv.Value.ToString() }),
        });

        var problems = new List<string>();
        problems.AddRange(unlisted.Select(r =>
            $"{r.Key}: reproduced {r.Reds}/{r.Repetitions} — the {r.Bucket} is not allowlisted.\n      solo:     {r.Oracle}\n      observed: {r.Observed}"));
        problems.AddRange(stale.Select(k =>
            $"{k}: exercised and reproduced 0/{repetitions} — drain the row from Conformance/{AllowlistFileName} (or mark it `{Probabilistic}` with its measured k/R)."));
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
    /// constructor, and serves every program of the schedule. Sharpy.Core is referenced (without it
    /// the C# → IL emit of <c>print(1)</c> is SPY0908 — measured), and the output path makes
    /// <c>Compile</c> reach IL.
    /// </summary>
    private sealed class CompilerInstance : IsolationInstance
    {
        private readonly Compiler _compiler;

        public CompilerInstance()
        {
            Directory.CreateDirectory(Root);
            _compiler = new Compiler(CompilerOptionsFactory.ForCli(
                references: new[] { SharpyCoreReference.Location },
                assemblyName: AssemblyName,
                outputAssemblyPath: Path.Combine(Root, AssemblyName + ".dll")), NullLogger.Instance);
        }

        protected override Fingerprint Analyze(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = _compiler.Analyze(program.Source, program.FileName, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics.GetAll(), result.ModuleRegistry?.GetUsedAssemblyPaths());
        }

        protected override Fingerprint Compile(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = _compiler.Compile(program.Source, program.FileName, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics.GetAll(), result.ModuleRegistry?.GetUsedAssemblyPaths());
        }
    }

    /// <summary>One <see cref="CompilerApi"/> referencing Sharpy.Core; each <c>Compile</c> writes into its own directory.</summary>
    private sealed class ApiInstance : IsolationInstance
    {
        private readonly CompilerApi _api = new(NullLogger.Instance, new[] { SharpyCoreReference.Location });

        protected override Fingerprint Analyze(IsolationProgram program, CancellationToken cancellationToken)
        {
            var result = _api.AnalyzeDocument(program.Source, program.FileName, CompilerOptionsFactory.ForCli(),
                stageMetrics: null, cancellationToken);
            return Fingerprint.Of(result.Success, result.Diagnostics, usedAssemblies: null);
        }

        protected override Fingerprint Compile(IsolationProgram program, CancellationToken cancellationToken)
        {
            var callDir = Path.Combine(Root, Guid.NewGuid().ToString("N"));
            try
            {
                var options = CompilerOptionsFactory.ForCli(
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
    /// <c>program sharing threads operation bucket [probabilistic]  # #2250 reason; reproduced k/R @ sha</c>.
    /// A row that does not parse, names an unknown sharing/operation/bucket, or does not cite #2250 is
    /// refused (an instrument fault), never silently skipped.
    /// </summary>
    private static Dictionary<string, AllowlistRow> LoadAllowlist()
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
            if (!sharingNames.Contains(fields[1]) || fields[2] is not ("1" or "2" or "8") || !operationNames.Contains(fields[3])
                || fields[4] is not (Drift or Crash or Order))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — unknown sharing, thread count, operation or bucket.");
            if (!comment.Contains(IssueCite, StringComparison.Ordinal))
                throw new InvalidOperationException($"Conformance/{AllowlistFileName}: '{line}' — every row cites {IssueCite}.");
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
