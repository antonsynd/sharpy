using System.Globalization;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Crash × crash cell of the in-process isolation class (#2250, plan cell 12): every crash gets ONE
/// bundle directory of its own, holding ONE complete report — its own. <see cref="CrashBundleWriter"/>
/// names a bundle by the clock's millisecond and resolves a collision check-then-create
/// (<c>while (Directory.Exists(candidate)) suffix++</c>, then <c>CreateDirectory</c>, which succeeds on
/// an existing directory), so two writers that read the same free suffix share one directory and the
/// later <c>report.md</c> overwrites the earlier. The internal clock overload pins every writer to one
/// tick, so every write collides on the name and only the suffix scan can separate them.
/// </summary>
public sealed class CrashBundleExclusivityTests : IDisposable
{
    private const int BundlesPerWriter = 10;

    /// <summary>A writer that stops reading the clock must not hang the others.</summary>
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The one tick every writer reads — a same-millisecond collision on every write.</summary>
    private static readonly DateTime OneTick = new(2026, 10, 14, 12, 0, 0, 123, DateTimeKind.Utc);

    private readonly string _tempRoot;
    private readonly ITestOutputHelper _output;

    public CrashBundleExclusivityTests(ITestOutputHelper output)
    {
        _output = output;
        _tempRoot = Path.Combine(Path.GetTempPath(), $"sharpy_crash_excl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>
    /// Writer counts whose cell is a live defect @ base. A row ASSERTS THE DEFECT REPRODUCES, so the
    /// fix (Phase 3 task 3: a create-exclusive <c>.owner</c> marker + pid in the name) turns it red as
    /// "stale — drain this row". Every other writer count asserts exclusivity.
    /// </summary>
    private static readonly Dictionary<int, string> KnownRed = new()
    {
        // Deterministic under the aligned clock: every round starts all writers on one directory
        // listing. Measured with an R-iteration loop of RunWriters at 486c9a88a.
        [2] = "#2250 check-then-create suffix scan — two writers read one free suffix and share its directory; "
            + "reproduced 1000/1000 @ 486c9a88a (at most 12 of 20 distinct); drains in P54 Phase 3 task 3",
        [8] = "#2250 check-then-create suffix scan — eight writers read one free suffix and share its directory; "
            + "reproduced 300/300 @ 486c9a88a (at most 37 of 80 distinct); drains in P54 Phase 3 task 3",
    };

    [Theory]
    [InlineData(1)] // positive control: one writer, ten crashes at one tick → ten directories
    [InlineData(2)]
    [InlineData(8)]
    [Trait("Category", "Conformance")]
    public void ConcurrentWriters_AtOneClockTick_EachCrashGetsItsOwnDirectoryAndReport(int writers)
    {
        var outcome = RunWriters(writers, Path.Combine(_tempRoot, $"w{writers}"));
        _output.WriteLine($"writers={writers}: {outcome}");

        if (KnownRed.TryGetValue(writers, out var reason))
        {
            Assert.False(outcome.IsExclusive,
                $"Known-red cell writers={writers} is now exclusive — stale, drain its KnownRed row "
                + $"({reason}). {outcome}");
        }
        else
        {
            Assert.True(outcome.IsExclusive, $"writers={writers}: {outcome}");
        }
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedRows_CiteTheIssue()
    {
        foreach (var (_, reason) in KnownRed)
            Assert.Contains("#2250", reason, StringComparison.Ordinal);
    }

    private static Outcome RunWriters(int writers, string outputDirectory)
    {
        var results = new (string Marker, string? Path)[writers * BundlesPerWriter];
        using var barrier = new Barrier(writers);
        // The clock reads one constant tick AND re-aligns the writers: every TryWrite reads the clock
        // once, before it scans for a free suffix, so round b of every writer scans the same directory
        // listing. A scheduler skew that lets one writer run ahead cannot serialize the cell green.
        using var roundBarrier = new Barrier(writers);
        DateTime AlignedOneTick()
        {
            roundBarrier.SignalAndWait(RoundTimeout);
            return OneTick;
        }
        var threads = new List<Thread>();
        for (var w = 0; w < writers; w++)
        {
            var writer = w;
            var thread = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var b = 0; b < BundlesPerWriter; b++)
                {
                    var marker = string.Create(CultureInfo.InvariantCulture, $"exclusivity-marker-w{writer}-b{b}");
                    var request = new CrashBundleRequest
                    {
                        Producer = marker,
                        Component = "CrashBundleExclusivityTests",
                        Exception = new InvalidOperationException(marker),
                    };
                    var path = CrashBundleWriter.TryWrite(outputDirectory, request, AlignedOneTick);
                    results[(writer * BundlesPerWriter) + b] = (marker, path);
                }
            })
            { IsBackground = true };
            threads.Add(thread);
            thread.Start();
        }
        foreach (var thread in threads)
            thread.Join();

        var expected = writers * BundlesPerWriter;
        var nullPaths = results.Count(r => r.Path == null);
        var distinctPaths = results.Where(r => r.Path != null)
            .Select(r => r.Path!).Distinct(StringComparer.Ordinal).Count();
        var crashRoot = Path.Combine(outputDirectory, CrashBundleWriter.CrashRootDirectoryName);
        var directoriesOnDisk = Directory.Exists(crashRoot) ? Directory.GetDirectories(crashRoot).Length : 0;

        // A report is the writer's own and complete: it names its own marker as producer AND as the
        // exception message (the last section), and no other writer's marker.
        var foreignOrIncomplete = 0;
        foreach (var (marker, path) in results)
        {
            if (path == null)
                continue;
            var reportPath = Path.Combine(path, "report.md");
            var report = File.Exists(reportPath) ? File.ReadAllText(reportPath) : "";
            var ownMarker = report.Contains("- Producer: " + marker + "\n", StringComparison.Ordinal)
                && report.Contains("- Message: " + marker + "\n", StringComparison.Ordinal);
            var markerCount = CountOccurrences(report, "- Producer: exclusivity-marker-");
            if (!ownMarker || markerCount != 1)
                foreignOrIncomplete++;
        }

        return new Outcome(expected, nullPaths, distinctPaths, directoriesOnDisk, foreignOrIncomplete);
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private sealed record Outcome(int Expected, int NullPaths, int DistinctPaths, int DirectoriesOnDisk, int ForeignOrIncompleteReports)
    {
        public bool IsExclusive => NullPaths == 0
            && DistinctPaths == Expected
            && DirectoriesOnDisk == Expected
            && ForeignOrIncompleteReports == 0;

        public override string ToString()
            => $"expected {Expected} bundles: {DistinctPaths} distinct returned paths, {DirectoriesOnDisk} directories on disk, "
                + $"{NullPaths} null returns, {ForeignOrIncompleteReports} foreign/incomplete reports";
    }
}
