using System.Collections.Concurrent;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using SysBarrier = System.Threading.Barrier;
using SysRandom = System.Random;
using SysThread = System.Threading.Thread;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// Every written stdlib module global (<see cref="StdlibModuleGlobalRosterTests.Roster"/>) is
/// exercised through its PUBLIC module API under operation {write, read, write-then-read-on-another-
/// thread} × threads {1, 2, 8} × <see cref="Iterations"/> iterations, against CPython's model: a
/// module global is process-wide, and every call that reads or writes it is atomic (#2250, P54
/// Design Decision 5). Each cell asserts no exception, every value in its domain, and — for the
/// cross-thread operation — that a write on thread A is what a read on thread B sees.
///
/// <para>The CPython facts each exerciser encodes were verified with python3 3.12.13 / numpy 2.5.1
/// on 2026-10-14 (the literal results are in the comments). Threads are dedicated
/// <see cref="SysThread"/>s released together by a start barrier, never the xUnit thread, so a
/// <c>[ThreadStatic]</c> global cannot pass by being touched on the thread that checks it.</para>
///
/// <para><b>KnownRed.</b> A cell red at the base commit is a literal <see cref="KnownRed"/> row citing
/// #2250 with its measured reproduction. A <i>deterministic</i> row asserts the defect still
/// reproduces (the Phase 3 cure turns it red as "stale — drain this row"); a <i>probabilistic</i>
/// row reports k/R and asserts nothing; every other cell asserts k == 0. The calendar, logging,
/// HTTPStatus and typed-load-cache rows are the GREEN positive controls (the oracle is not "always
/// red"), as are random's write and cross-thread cells, socket's and xml's cross-thread cells, and
/// numpy.random/cross-thread/1 (one thread: the per-thread generator is the right one).</para>
///
/// <para><b>Isolation.</b> Each module's cells sit in the xUnit collection of the spy tests that
/// read the same global, so a concurrency cell never runs beside a seeded or set-then-read test of
/// the same module (Stdlib.Tests runs maxParallelThreads 4): random → <c>PrngState</c> (existing),
/// socket → <c>SocketDefaultTimeout</c>, calendar → <c>CalendarFirstweekday</c>, xml →
/// <c>XmlRegisteredNamespaces</c>, numpy.random → <c>NumpyRandomState</c> (definitions and spy
/// twins in <c>Spy/SpyTestCollections.cs</c>). Logging uses fresh logger names per cell and never
/// touches the root logger, HTTPStatus and the typed-load cache are only read, so those three need
/// no collection. Every cell restores the global it wrote.</para>
/// </summary>
public class StdlibModuleGlobalConcurrencyTests
{
    internal const int Iterations = 2000;

    internal static readonly int[] ThreadCounts = { 1, 2, 8 };

    internal const string OpWrite = "write";
    internal const string OpRead = "read";
    internal const string OpCrossThread = "cross-thread";

    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(60);

    /// <summary>A cell red at base: deterministic (asserted to reproduce) or probabilistic (reported only).</summary>
    internal sealed record KnownRedRow(bool Deterministic, string Evidence);

    /// <summary>
    /// The literal KnownRed rows, keyed "module/operation/threads"; each cites #2250. Measured over six runs of this class @ 486c9a88a (production code at base) on 2026-10-14,
    /// Apple-silicon arm64, under the other lanes' load; "k/R" is the violating checks of one run.
    /// </summary>
    internal static readonly Dictionary<string, KnownRedRow> KnownRed = new(StringComparer.Ordinal)
    {
        // random and socket: drained by Phase 3 task 1 — every read and write of RandomModuleModule._Random and
        // SocketModuleModule._DefaultTimeout now happens under one module lock (random_module.spy /
        // socket_module.spy). random/read/8 was red in 4/6 runs @ 486c9a88a and is 0/R after the cure (R in the
        // commit body); the socket rows were never reproduced (0/6) — they drain because the cure lands: the
        // 16-byte double? write now happens under the lock.

        // xml: concurrent writes to one Dictionary<string, string> lose entries, throw
        // InvalidOperationException ("concurrent update ... corrupted its state"), and make a
        // concurrent read miss a present key.
        ["xml/write/2"] = new(false, "#2250 probabilistic — red in 3/6 runs @ 486c9a88a, e.g. 484 of 4000 prefixes lost"),
        ["xml/write/8"] = new(false, "#2250 probabilistic — red in 6/6 runs @ 486c9a88a, e.g. 3891/16001 (InvalidOperationException)"),
        ["xml/read/2"] = new(false, "#2250 probabilistic — red in 1/6 runs @ 486c9a88a, 27/4000 reads missed a present prefix"),
        ["xml/read/8"] = new(false, "#2250 probabilistic — red in 5/6 runs @ 486c9a88a, e.g. 654/16000 reads missed a present prefix"),

        // numpy.random: [ThreadStatic] — a seed on one thread is invisible to every other thread.
        ["numpy.random/write/1"] = new(true, "#2250 deterministic — 1/2001 in 6/6 runs @ 486c9a88a: a draw on another thread ignores the seed"),
        ["numpy.random/write/2"] = new(true, "#2250 deterministic — 1/4001 in 6/6 runs @ 486c9a88a"),
        ["numpy.random/write/8"] = new(true, "#2250 deterministic — 1/16001 in 6/6 runs @ 486c9a88a"),
        ["numpy.random/read/1"] = new(true, "#2250 deterministic — 2000/2000 in 6/6 runs @ 486c9a88a: the worker's draws are its own unseeded stream"),
        ["numpy.random/read/2"] = new(true, "#2250 deterministic — 4000/4000 in 6/6 runs @ 486c9a88a"),
        ["numpy.random/read/8"] = new(true, "#2250 deterministic — 16000/16000 in 6/6 runs @ 486c9a88a"),
        ["numpy.random/cross-thread/2"] = new(true, "#2250 deterministic — 2000/4000 (every read) in 6/6 runs @ 486c9a88a"),
        ["numpy.random/cross-thread/8"] = new(true, "#2250 deterministic — 2000/4000 (every read) in 6/6 runs @ 486c9a88a"),
    };

    /// <summary>Every exerciser, keyed by the roster field it drives.</summary>
    internal static readonly GlobalExerciser[] Exercisers =
    {
        new RandomExerciser(), new SocketExerciser(), new CalendarExerciser(), new XmlExerciser(),
        new LoggingExerciser(), new NumpyRandomExerciser(), new HttpStatusExerciser(), new TypedLoadCacheExerciser(),
    };

    public static IEnumerable<object[]> Matrix(string module)
    {
        var exerciser = Exercisers.Single(e => e.Module == module);
        foreach (var operation in new[] { OpWrite, OpRead, OpCrossThread })
        {
            if (exerciser.NotApplicable.ContainsKey(operation))
                continue;
            foreach (var threads in ThreadCounts)
                yield return new object[] { operation, threads };
        }
    }

    /// <summary>Totality: every roster row has exactly one exerciser, and every exerciser drives a roster row.</summary>
    [Fact]
    public void EveryRosterRow_HasAnExerciser()
    {
        Assert.Equal(
            StdlibModuleGlobalRosterTests.Roster.Select(r => r.Field).OrderBy(f => f, StringComparer.Ordinal),
            Exercisers.Select(e => e.Field).OrderBy(f => f, StringComparer.Ordinal));
        Assert.Equal(Exercisers.Length, Exercisers.Select(e => e.Module).Distinct().Count());
    }

    /// <summary>Only the plan's six globals run every operation; a skipped operation names why it cannot race.</summary>
    [Fact]
    public void EverySkippedOperation_NamesItsReason()
    {
        foreach (var exerciser in Exercisers)
        {
            foreach (var (operation, reason) in exerciser.NotApplicable)
            {
                Assert.Contains(operation, new[] { OpWrite, OpRead, OpCrossThread });
                Assert.False(string.IsNullOrWhiteSpace(reason), $"{exerciser.Module}/{operation}");
            }
        }

        Assert.Equal(
            new[] { "calendar", "logging", "numpy.random", "random", "socket", "xml" },
            Exercisers.Where(e => e.NotApplicable.Count == 0).Select(e => e.Module).OrderBy(m => m, StringComparer.Ordinal));
    }

    /// <summary>Every KnownRed row names a real cell and cites #2250 with a measured reproduction.</summary>
    [Fact]
    public void EveryKnownRedRow_IsARealCellCitingTheIssue()
    {
        var cells = Exercisers
            .SelectMany(e => Matrix(e.Module).Select(c => $"{e.Module}/{c[0]}/{c[1]}"))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (cell, row) in KnownRed)
        {
            Assert.Contains(cell, cells);
            Assert.Contains("#2250", row.Evidence, StringComparison.Ordinal);
            Assert.Contains(" @ ", row.Evidence, StringComparison.Ordinal);
        }
    }

    // ---- The cell classes, one per collection ----

    [Collection("PrngState")]
    public sealed class RandomCells : CellRunner
    {
        public RandomCells(ITestOutputHelper output) : base(output, "random") { }

        [Theory]
        [MemberData(nameof(Matrix), "random", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    [Collection("SocketDefaultTimeout")]
    public sealed class SocketCells : CellRunner
    {
        public SocketCells(ITestOutputHelper output) : base(output, "socket") { }

        [Theory]
        [MemberData(nameof(Matrix), "socket", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    [Collection("CalendarFirstweekday")]
    public sealed class CalendarCells : CellRunner
    {
        public CalendarCells(ITestOutputHelper output) : base(output, "calendar") { }

        [Theory]
        [MemberData(nameof(Matrix), "calendar", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    [Collection("XmlRegisteredNamespaces")]
    public sealed class XmlCells : CellRunner
    {
        public XmlCells(ITestOutputHelper output) : base(output, "xml") { }

        [Theory]
        [MemberData(nameof(Matrix), "xml", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    public sealed class LoggingCells : CellRunner
    {
        public LoggingCells(ITestOutputHelper output) : base(output, "logging") { }

        [Theory]
        [MemberData(nameof(Matrix), "logging", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    [Collection("NumpyRandomState")]
    public sealed class NumpyRandomCells : CellRunner
    {
        public NumpyRandomCells(ITestOutputHelper output) : base(output, "numpy.random") { }

        [Theory]
        [MemberData(nameof(Matrix), "numpy.random", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    public sealed class HttpStatusCells : CellRunner
    {
        public HttpStatusCells(ITestOutputHelper output) : base(output, "http.HTTPStatus") { }

        [Theory]
        [MemberData(nameof(Matrix), "http.HTTPStatus", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    public sealed class TypedLoadCacheCells : CellRunner
    {
        public TypedLoadCacheCells(ITestOutputHelper output) : base(output, "json typed load") { }

        [Theory]
        [MemberData(nameof(Matrix), "json typed load", MemberType = typeof(StdlibModuleGlobalConcurrencyTests))]
        public void Cell(string operation, int threads) => Run(operation, threads);
    }

    public abstract class CellRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly GlobalExerciser _exerciser;

        protected CellRunner(ITestOutputHelper output, string module)
        {
            _output = output;
            _exerciser = Exercisers.Single(e => e.Module == module);
        }

        protected void Run(string operation, int threads)
        {
            var cell = $"{_exerciser.Module}/{operation}/{threads}";
            var tally = new Tally();
            try
            {
                switch (operation)
                {
                    case OpWrite:
                        _exerciser.Write(threads, tally);
                        break;
                    case OpRead:
                        _exerciser.Read(threads, tally);
                        break;
                    case OpCrossThread:
                        _exerciser.CrossThread(threads, tally);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
                }
            }
            finally
            {
                _exerciser.Restore();
            }

            Assert.True(tally.Checks > 0, $"{cell}: the cell made no checks");
            var report = $"{cell}: {tally.Violations}/{tally.Checks} violating checks ({tally.Exceptions} exception(s)); "
                         + $"field {_exerciser.Field}; samples: [{string.Join(" | ", tally.Samples)}]";
            _output.WriteLine(report);

            if (KnownRed.TryGetValue(cell, out var row))
            {
                _output.WriteLine($"KnownRed ({(row.Deterministic ? "deterministic" : "probabilistic")}): {row.Evidence}");
                if (row.Deterministic)
                {
                    Assert.True(tally.Violations > 0,
                        $"{cell} is a deterministic KnownRed row but no longer reproduces (0/{tally.Checks}) — stale: drain the row. {row.Evidence}");
                }
                return;
            }

            Assert.True(tally.Violations == 0, report);
        }
    }

    // ---- The harness ----

    /// <summary>Counts checks, violating checks and exceptions across threads; keeps a few samples.</summary>
    internal sealed class Tally
    {
        private int _checks;
        private int _violations;
        private int _exceptions;
        private readonly ConcurrentQueue<string> _samples = new();

        public int Checks => Volatile.Read(ref _checks);
        public int Violations => Volatile.Read(ref _violations);
        public int Exceptions => Volatile.Read(ref _exceptions);
        public IEnumerable<string> Samples => _samples.Take(5);

        /// <summary>One check: <paramref name="ok"/> false or throwing is a violation.</summary>
        public void Check(Func<bool> ok, Func<string> describe)
        {
            Interlocked.Increment(ref _checks);
            try
            {
                if (!ok())
                {
                    Interlocked.Increment(ref _violations);
                    Sample(describe());
                }
            }
            catch (Exception e)
            {
                Interlocked.Increment(ref _exceptions);
                Interlocked.Increment(ref _violations);
                Sample($"{e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>One operation that must not throw.</summary>
        public void Do(Action action) => Check(() => { action(); return true; }, () => "");

        /// <summary>A whole-run tally: <paramref name="bad"/> of <paramref name="total"/> checks violated.</summary>
        public void Count(int total, int bad, string describe)
        {
            Interlocked.Add(ref _checks, total);
            if (bad > 0)
            {
                Interlocked.Add(ref _violations, bad);
                Sample(describe);
            }
        }

        public void Exception(Exception e)
        {
            Interlocked.Increment(ref _checks);
            Interlocked.Increment(ref _exceptions);
            Interlocked.Increment(ref _violations);
            Sample($"{e.GetType().Name}: {e.Message}");
        }

        private void Sample(string text)
        {
            if (_samples.Count < 5)
                _samples.Enqueue(text);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/>(id) on <paramref name="threads"/> dedicated threads released
    /// together by a start barrier; an escaped exception or a thread that does not finish is a violation.
    /// </summary>
    internal static void OnThreads(int threads, Tally tally, Action<int> body)
    {
        using var start = new SysBarrier(threads);
        var workers = Enumerable.Range(0, threads).Select(id => new SysThread(() =>
        {
            try
            {
                start.SignalAndWait();
                body(id);
            }
            catch (Exception e)
            {
                tally.Exception(e);
            }
        })
        { IsBackground = true, Name = $"p54-global-{id}" }).ToArray();

        foreach (var worker in workers)
            worker.Start();
        foreach (var worker in workers)
        {
            if (!worker.Join(JoinTimeout))
                tally.Exception(new TimeoutException($"{worker.Name} did not finish in {JoinTimeout.TotalSeconds:0} s (a corrupted structure looping?)"));
        }
    }

    /// <summary>Evaluates <paramref name="f"/> on one fresh dedicated thread.</summary>
    internal static T OnFreshThread<T>(Func<T> f)
    {
        T result = default!;
        Exception? error = null;
        var thread = new SysThread(() =>
        {
            try
            {
                result = f();
            }
            catch (Exception e)
            {
                error = e;
            }
        })
        { IsBackground = true, Name = "p54-global-fresh" };
        thread.Start();
        if (!thread.Join(JoinTimeout))
            throw new TimeoutException("the fresh thread did not finish");
        if (error != null)
            throw new InvalidOperationException("the fresh thread threw", error);
        return result;
    }

    /// <summary>
    /// The handoff schedule: in iteration i, thread (i mod T) writes, then thread ((i + 1) mod T)
    /// reads and checks. A shared stage counter orders the steps (stage 2i: the write; 2i + 1: the
    /// read) with a release/acquire pair — the happens-before edge any cross-thread call sequence
    /// has; the other threads wait for their own turn. With T = 1 the reader is the writer; with
    /// T ≥ 2 the read is always on another thread. (An 8-party barrier per step cost ~25 s per cell
    /// on a loaded machine; two threads per step cost milliseconds.)
    /// </summary>
    internal static void Handoff(int threads, Tally tally, Action<int> write, Action<int> readAndCheck)
    {
        var stage = new int[1];
        OnThreads(threads, tally, id =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                if (i % threads == id)
                {
                    AwaitStage(stage, 2 * i);
                    tally.Do(() => write(i));
                    Volatile.Write(ref stage[0], 2 * i + 1);
                }

                if ((i + 1) % threads == id)
                {
                    AwaitStage(stage, 2 * i + 1);
                    readAndCheck(i);
                    Volatile.Write(ref stage[0], 2 * i + 2);
                }
            }
        });
    }

    private static void AwaitStage(int[] stage, int value)
    {
        var spin = new SpinWait();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (Volatile.Read(ref stage[0]) != value)
        {
            if (clock.Elapsed > JoinTimeout)
                throw new TimeoutException($"stage {value} never came (at {Volatile.Read(ref stage[0])})");
            spin.SpinOnce(sleep1Threshold: -1);
        }
    }

    /// <summary>How many of <paramref name="actual"/> are not matched one-for-one in <paramref name="expected"/>.</summary>
    internal static int MultisetMisses(IEnumerable<double> expected, IEnumerable<double> actual)
    {
        var remaining = new Dictionary<double, int>();
        foreach (var value in expected)
            remaining[value] = remaining.TryGetValue(value, out var n) ? n + 1 : 1;
        var misses = 0;
        foreach (var value in actual)
        {
            if (remaining.TryGetValue(value, out var n) && n > 0)
                remaining[value] = n - 1;
            else
                misses++;
        }

        return misses;
    }

    // ---- The exercisers ----

    internal abstract class GlobalExerciser
    {
        /// <summary>The roster key of the global this exerciser drives.</summary>
        public abstract string Field { get; }

        /// <summary>The cell-id prefix (the Python module).</summary>
        public abstract string Module { get; }

        /// <summary>Operations with no post-initialization write to race, each with the reason.</summary>
        public virtual IReadOnlyDictionary<string, string> NotApplicable { get; } = new Dictionary<string, string>();

        public virtual void Write(int threads, Tally tally) => throw new NotSupportedException();

        public abstract void Read(int threads, Tally tally);

        public virtual void CrossThread(int threads, Tally tally) => throw new NotSupportedException();

        public virtual void Restore()
        {
        }
    }

    /// <summary>
    /// random — CPython 3.12: random.__doc__ "...and is, therefore, threadsafe."; seed(7) on one thread
    /// then random() on another == the re-seeded draw (True, 0.32383276483316237); 8 threads x 2000
    /// draws after seed(7) are, as a multiset, exactly the first 16000 draws of the seed-7 stream
    /// (True), with 0 zeros; seed(i) on one thread then random() on another == the seed-i stream's
    /// first value for every sampled i in [0, 2000) (True). Sharpy spells random() next_double().
    /// </summary>
    private sealed class RandomExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.RandomModule.RandomModuleModule._Random";
        public override string Module => "random";

        private static double[] Stream(int n, int seed = 7)
        {
            Sharpy.RandomModule.RandomModuleModule.Seed(seed);
            return Enumerable.Range(0, n).Select(_ => Sharpy.RandomModule.RandomModuleModule.NextDouble()).ToArray();
        }

        public override void Write(int threads, Tally tally)
        {
            var first = Stream(1)[0];
            OnThreads(threads, tally, _ =>
            {
                for (var i = 0; i < Iterations; i++)
                    tally.Do(() => Sharpy.RandomModule.RandomModuleModule.Seed(7));
            });
            var draw = OnFreshThread(Sharpy.RandomModule.RandomModuleModule.NextDouble);
            tally.Check(() => draw == first, () => $"after every thread seeded 7 the next draw was {draw}, expected {first}");
        }

        public override void Read(int threads, Tally tally)
        {
            var expected = Stream(threads * Iterations);
            Sharpy.RandomModule.RandomModuleModule.Seed(7);
            var draws = new double[threads][];
            OnThreads(threads, tally, id =>
            {
                draws[id] = new double[Iterations];
                for (var i = 0; i < Iterations; i++)
                    draws[id][i] = Sharpy.RandomModule.RandomModuleModule.NextDouble();
            });
            var all = draws.Where(d => d != null).SelectMany(d => d).ToArray();
            var outOfDomain = all.Count(d => !(d >= 0.0 && d < 1.0));
            var zeros = all.Count(d => d == 0.0);
            var misses = MultisetMisses(expected, all);
            tally.Count(all.Length, misses,
                $"{misses} draw(s) are not the seed-7 stream's first {expected.Length} (lost or duplicated by a race); {zeros} zero(s), {outOfDomain} out of [0, 1)");
        }

        public override void CrossThread(int threads, Tally tally)
        {
            // A different seed per iteration, so a reader cannot pass on a generator it seeded itself.
            var firsts = Enumerable.Range(0, Iterations).Select(seed => Stream(1, seed)[0]).ToArray();
            Handoff(threads, tally,
                i => Sharpy.RandomModule.RandomModuleModule.Seed(i),
                i => tally.Check(() => Sharpy.RandomModule.RandomModuleModule.NextDouble() == firsts[i],
                    () => $"iteration {i}: the draw after another thread's seed({i}) is not the seed-{i} stream's first value"));
        }

        // The unseeded process state: no PrngState test reads before it seeds.
        public override void Restore() => Sharpy.RandomModule.RandomModuleModule._Random = new SysRandom();
    }

    /// <summary>
    /// socket — CPython 3.12: 8 threads x 2000 setdefaulttimeout(i % 5 + 1)/getdefaulttimeout() raise
    /// nothing and every read is one of the written values; setdefaulttimeout(12.5) on a thread, then
    /// getdefaulttimeout() on the main thread == 12.5.
    /// </summary>
    private sealed class SocketExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.SocketModule.SocketModuleModule._DefaultTimeout";
        public override string Module => "socket";

        private static readonly double?[] Domain = { null, 1.0, 2.0, 3.0, 4.0, 5.0 };

        private static bool InDomain(double? value) => Domain.Contains(value);

        public override void Write(int threads, Tally tally)
        {
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var value = Domain[(id + i) % Domain.Length];
                    tally.Do(() => Sharpy.SocketModule.SocketModuleModule.Setdefaulttimeout(value));
                }
            });
            var final = OnFreshThread(Sharpy.SocketModule.SocketModuleModule.Getdefaulttimeout);
            tally.Check(() => InDomain(final), () => $"after the writes getdefaulttimeout() is {final}");
        }

        public override void Read(int threads, Tally tally)
        {
            // Every thread alternates write and read, so reads race writes at T >= 2.
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    if ((id + i) % 2 == 0)
                    {
                        var value = Domain[(id + i) % Domain.Length];
                        tally.Do(() => Sharpy.SocketModule.SocketModuleModule.Setdefaulttimeout(value));
                    }
                    else
                    {
                        double? read = null;
                        tally.Check(() => InDomain(read = Sharpy.SocketModule.SocketModuleModule.Getdefaulttimeout()),
                            () => $"getdefaulttimeout() read {read?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "None"}, not a written value (torn)");
                    }
                }
            });
        }

        public override void CrossThread(int threads, Tally tally)
        {
            Handoff(threads, tally,
                i => Sharpy.SocketModule.SocketModuleModule.Setdefaulttimeout(1.0 + (i % 5)),
                i => tally.Check(() => Sharpy.SocketModule.SocketModuleModule.Getdefaulttimeout() == 1.0 + (i % 5),
                    () => $"iteration {i}: another thread's setdefaulttimeout({1.0 + (i % 5)}) is not seen"));
        }

        public override void Restore() => Sharpy.SocketModule.SocketModuleModule.Setdefaulttimeout(null);
    }

    /// <summary>
    /// calendar — CPython 3.12: setfirstweekday(6) on a thread, then firstweekday() on the main thread
    /// == 6 and monthcalendar(2024, 1)[0] == [0, 1, 2, 3, 4, 5, 6]; with 0, [1, 2, 3, 4, 5, 6, 7].
    /// Sharpy has no calendar.firstweekday() (findings ledger), so the read derives the first weekday
    /// from monthcalendar(2024, 1): 1 January 2024 is a Monday, at index (0 − fw) mod 7.
    /// </summary>
    private sealed class CalendarExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.CalendarModule._moduleFirstweekday";
        public override string Module => "calendar";

        private static int FirstWeekday()
        {
            var week = CalendarModule.Monthcalendar(2024, 1)[0];
            for (var j = 0; j < 7; j++)
            {
                if (week[j] == 1)
                    return (7 - j) % 7;
            }

            return -1;
        }

        public override void Write(int threads, Tally tally)
        {
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var weekday = (id + i) % 7;
                    tally.Do(() => CalendarModule.Setfirstweekday(weekday));
                }
            });
            var final = OnFreshThread(FirstWeekday);
            tally.Check(() => final is >= 0 and <= 6, () => $"after the writes the first weekday is {final}");
        }

        public override void Read(int threads, Tally tally)
        {
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    if ((id + i) % 2 == 0)
                    {
                        var weekday = (id + i) % 7;
                        tally.Do(() => CalendarModule.Setfirstweekday(weekday));
                    }
                    else
                    {
                        var read = -1;
                        tally.Check(() => (read = FirstWeekday()) is >= 0 and <= 6, () => $"first weekday read {read}");
                    }
                }
            });
        }

        public override void CrossThread(int threads, Tally tally)
        {
            Handoff(threads, tally,
                i => CalendarModule.Setfirstweekday(i % 7),
                i => tally.Check(() => FirstWeekday() == i % 7,
                    () => $"iteration {i}: another thread's setfirstweekday({i % 7}) is not seen"));
        }

        public override void Restore() => CalendarModule.Setfirstweekday(0);
    }

    /// <summary>
    /// xml — CPython 3.12: 8 threads x 2000 distinct register_namespace(prefix, uri) calls all land
    /// (every pair present in ElementTree._namespace_map afterwards: True). Nothing in Sharpy reads
    /// Xml._registeredNamespaces @ 486c9a88a, so the reads read the field (InternalsVisibleTo).
    /// </summary>
    private sealed class XmlExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.Xml._registeredNamespaces";
        public override string Module => "xml";

        private static bool Has(string prefix, string uri)
            => Xml._registeredNamespaces.TryGetValue(prefix, out var found) && found == uri;

        public override void Write(int threads, Tally tally)
        {
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var (prefix, uri) = ($"p54w{id}x{i}", $"http://p54.example/{id}/{i}");
                    tally.Do(() => Xml.RegisterNamespace(prefix, uri));
                }
            });

            var lost = 0;
            try
            {
                for (var id = 0; id < threads; id++)
                {
                    for (var i = 0; i < Iterations; i++)
                    {
                        if (!Has($"p54w{id}x{i}", $"http://p54.example/{id}/{i}"))
                            lost++;
                    }
                }
            }
            catch (Exception e)
            {
                tally.Exception(e);
            }

            tally.Count(1, lost > 0 ? 1 : 0, $"{lost} of {threads * Iterations} registered prefixes are missing or wrong afterwards");
        }

        public override void Read(int threads, Tally tally)
        {
            const int Seeded = 64;
            for (var k = 0; k < Seeded; k++)
                Xml.RegisterNamespace($"p54r{k}", $"http://p54.example/r/{k}");

            // Every thread alternates registering a new prefix and reading a seeded one.
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    if ((id + i) % 2 == 0)
                    {
                        var (prefix, uri) = ($"p54n{id}x{i}", $"http://p54.example/n/{id}/{i}");
                        tally.Do(() => Xml.RegisterNamespace(prefix, uri));
                    }
                    else
                    {
                        var k = i % Seeded;
                        tally.Check(() => Has($"p54r{k}", $"http://p54.example/r/{k}"),
                            () => $"the seeded prefix p54r{k} was not found while another thread registered");
                    }
                }
            });
        }

        public override void CrossThread(int threads, Tally tally)
        {
            Handoff(threads, tally,
                i => Xml.RegisterNamespace("p54x", $"http://p54.example/x/{i}"),
                i => tally.Check(() => Has("p54x", $"http://p54.example/x/{i}"),
                    () => $"iteration {i}: another thread's register_namespace is not seen"));
        }

        // Clear() also resets a table a race corrupted, so the xml spy test that shares the
        // collection starts from a sound dictionary.
        public override void Restore() => Xml._registeredNamespaces.Clear();
    }

    /// <summary>
    /// logging — CPython 3.12: getLogger("p54.x") on a thread is the same object as getLogger("p54.x")
    /// on the main thread (True). Names are fresh per cell and the root logger is never touched, so
    /// no other test observes these loggers.
    /// </summary>
    private sealed class LoggingExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.Logging._loggers";
        public override string Module => "logging";

        private static string FreshPrefix() => "p54." + Guid.NewGuid().ToString("N");

        public override void Write(int threads, Tally tally)
        {
            // Every thread asks for the same 64 new names: each name must yield ONE logger.
            var prefix = FreshPrefix();
            var seen = new Logger?[threads, 64];
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var k = i % 64;
                    tally.Check(() =>
                    {
                        var logger = Logging.GetLogger($"{prefix}.{k}");
                        if (seen[id, k] == null)
                            seen[id, k] = logger;
                        return ReferenceEquals(seen[id, k], logger);
                    }, () => $"{prefix}.{k} returned two loggers on one thread");
                }
            });

            for (var k = 0; k < 64; k++)
            {
                var canonical = Logging.GetLogger($"{prefix}.{k}");
                var split = Enumerable.Range(0, threads).Count(id => !ReferenceEquals(seen[id, k], canonical));
                tally.Count(threads, split, $"{split} thread(s) hold a different logger for {prefix}.{k}");
            }
        }

        public override void Read(int threads, Tally tally)
        {
            var prefix = FreshPrefix();
            var created = Enumerable.Range(0, 64).Select(k => Logging.GetLogger($"{prefix}.{k}")).ToArray();
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var k = (id + i) % 64;
                    tally.Check(() => ReferenceEquals(Logging.GetLogger($"{prefix}.{k}"), created[k]),
                        () => $"{prefix}.{k} is not the logger created before the threads started");
                }
            });
        }

        public override void CrossThread(int threads, Tally tally)
        {
            var prefix = FreshPrefix();
            var created = new Logger?[Iterations];
            Handoff(threads, tally,
                i => created[i] = Logging.GetLogger($"{prefix}.{i}"),
                i => tally.Check(() => ReferenceEquals(Logging.GetLogger($"{prefix}.{i}"), created[i]),
                    () => $"iteration {i}: getLogger on another thread is not the logger it created"));
        }
    }

    /// <summary>
    /// numpy.random — numpy 2.5.1: np.random.seed(7) on a thread, then np.random.rand() on another ==
    /// the re-seeded draw (True, 0.07630828937395717); 8 threads x 2000 rand() after seed(7) are, as a
    /// multiset, the first 16000 draws of the seed-7 stream (True); 8 threads x 2000 seed(7), then a
    /// draw on another thread == the stream's first value (True); seed(i) on one thread then rand() on
    /// another == the seed-i stream's first value for every sampled i (True). The global RandomState is
    /// process-wide.
    /// </summary>
    private sealed class NumpyRandomExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.NumpyRandom._rng";
        public override string Module => "numpy.random";

        private static double Draw() => NumpyRandom.Rand(1)[0];

        /// <summary>The seed-7 stream as one thread sees it (the oracle numpy's process-wide state gives every thread).</summary>
        private static double[] Stream(int n) => OnFreshThread(() =>
        {
            NumpyRandom.Seed(7);
            return Enumerable.Range(0, n).Select(_ => Draw()).ToArray();
        });

        public override void Write(int threads, Tally tally)
        {
            var first = Stream(1)[0];
            OnThreads(threads, tally, _ =>
            {
                for (var i = 0; i < Iterations; i++)
                    tally.Do(() => NumpyRandom.Seed(7));
            });
            var draw = OnFreshThread(Draw);
            tally.Check(() => draw == first, () => $"after every thread seeded 7 a draw on another thread was {draw}, expected {first}");
        }

        public override void Read(int threads, Tally tally)
        {
            var expected = Stream(threads * Iterations);
            OnFreshThread(() =>
            {
                NumpyRandom.Seed(7);
                return 0;
            });
            var draws = new double[threads][];
            OnThreads(threads, tally, id =>
            {
                draws[id] = new double[Iterations];
                for (var i = 0; i < Iterations; i++)
                    draws[id][i] = Draw();
            });
            var all = draws.Where(d => d != null).SelectMany(d => d).ToArray();
            var outOfDomain = all.Count(d => !(d >= 0.0 && d < 1.0));
            var misses = MultisetMisses(expected, all);
            tally.Count(all.Length, misses,
                $"{misses} draw(s) are not the seed-7 stream's first {expected.Length} (seed on another thread not seen); {outOfDomain} out of [0, 1)");
        }

        public override void CrossThread(int threads, Tally tally)
        {
            // A different seed per iteration, so a reader cannot pass on a generator it seeded itself
            // (with one seed, a per-thread generator re-seeded by its own earlier write looks right).
            var firsts = OnFreshThread(() => Enumerable.Range(0, Iterations).Select(seed =>
            {
                NumpyRandom.Seed(seed);
                return Draw();
            }).ToArray());
            Handoff(threads, tally,
                i => NumpyRandom.Seed(i),
                i => tally.Check(() => Draw() == firsts[i],
                    () => $"iteration {i}: the draw after another thread's seed({i}) is not the seed-{i} stream's first value"));
        }
    }

    /// <summary>
    /// http.HTTPStatus — CPython 3.12: HTTPStatus is an IntEnum of 62 members; 8 threads x 2000 x 62
    /// HTTPStatus(value) lookups each return the member (0 mismatches). The value map is written only
    /// by the type initializer, so only the read operation exists.
    /// </summary>
    private sealed class HttpStatusExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.HTTPStatus._valueMap";
        public override string Module => "http.HTTPStatus";

        public override IReadOnlyDictionary<string, string> NotApplicable { get; } = new Dictionary<string, string>
        {
            [OpWrite] = "written only by HTTPStatus's private constructor, called only from its own static readonly initializers — inside the CLR type initializer, before any reader",
            [OpCrossThread] = "no write after type initialization to hand off",
        };

        private static readonly HTTPStatus[] Members = typeof(HTTPStatus)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(HTTPStatus))
            .Select(f => (HTTPStatus)f.GetValue(null)!)
            .ToArray();

        public override void Read(int threads, Tally tally)
        {
            Assert.Equal(62, Members.Length);
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var member = Members[(id + i) % Members.Length];
                    tally.Check(() => ReferenceEquals(HTTPStatus.FromValue(member.Value), member),
                        () => $"HTTPStatus.FromValue({member.Value}) is not {member.Name}");
                }
            });
        }
    }

    /// <summary>A typed-load target: one all-fields constructor, as a dataclass emits.</summary>
    public sealed class P54Point
    {
        public int X { get; }
        public int Y { get; }

        public P54Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// json.loads[T] — the typed doors' field-descriptor cache (no CPython counterpart). The cache is a
    /// ConcurrentDictionary whose value is a function of its key, so only the read operation runs:
    /// concurrent typed loads of one type all decode, and a missing field is refused by name.
    /// </summary>
    private sealed class TypedLoadCacheExerciser : GlobalExerciser
    {
        public override string Field => "Sharpy.TypedLoadContract.FieldCache";
        public override string Module => "json typed load";

        public override IReadOnlyDictionary<string, string> NotApplicable { get; } = new Dictionary<string, string>
        {
            [OpWrite] = "GetOrAdd keyed by a Type with a factory that is a function of that Type: a concurrent first touch publishes one of several equal values",
            [OpCrossThread] = "the cached value is a function of its key; there is no written state to hand off",
        };

        public override void Read(int threads, Tally tally)
        {
            OnThreads(threads, tally, id =>
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var (x, y) = (id, i);
                    if (i % 2 == 0)
                    {
                        tally.Check(() =>
                        {
                            var result = Sharpy.Json.Loads<P54Point>($"{{\"x\": {x}, \"y\": {y}}}");
                            return result.IsOk && result.Unwrap().X == x && result.Unwrap().Y == y;
                        }, () => $"json.loads[P54Point] of ({x}, {y}) did not decode");
                    }
                    else
                    {
                        tally.Check(() =>
                        {
                            var result = Sharpy.Json.Loads<P54Point>($"{{\"x\": {x}}}");
                            return result.IsErr && result.UnwrapErr().Msg.Contains("y", StringComparison.Ordinal);
                        }, () => "json.loads[P54Point] without y was not refused naming y");
                    }
                }
            });
        }
    }
}
