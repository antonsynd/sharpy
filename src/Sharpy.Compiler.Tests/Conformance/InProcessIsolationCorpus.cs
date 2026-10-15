using Sharpy.Compiler.Tests.Fuzz;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>One program of the in-process isolation corpus: a stable id (the allowlist key), the
/// file name the compile names it by, and its source.</summary>
internal sealed record IsolationProgram(string Id, string FileName, string Source);

/// <summary>The two operations the sweep drives on every instance kind.</summary>
internal enum IsolationOperation
{
    /// <summary>Lexer → parser → semantic → validation; no code generation.</summary>
    Analyze,

    /// <summary>The whole pipeline through the C# → IL emit (the stage SPY0908 is raised at).</summary>
    Compile,
}

/// <summary>One step of a thread's schedule.</summary>
internal readonly record struct IsolationStep(IsolationProgram Program, IsolationOperation Operation);

/// <summary>
/// The corpus and the schedule generator of <see cref="InProcessIsolationSweepTests"/> (P54, #2250).
///
/// <para><b>The roster</b> is literal: each named entry is a program a #2250 cell was measured on, or
/// one that drives a process-wide resolution path the plan's census names —
/// <list type="bullet">
///   <item><c>fuzz/seed2025_iter18</c> — cell 1: <c>SemanticPropertyTests.Compilation_IsDeterministic</c>
///   seed 2025, iteration 18, regenerated from <see cref="SharpyFuzzer"/> (the generator is seeded, so
///   the text is a pure function of the seed; <see cref="Named"/> refuses a regenerated text that no
///   longer carries the measured program's first line).</item>
///   <item>the three P47 stems of cells 2–3 (<c>fstrings/format_iformattable_class_1988</c>,
///   <c>fstrings/format_no_dunder_format_refused_1988</c>, <c>deferred_overload_annotated_pin_1589</c>),
///   read from the fixture corpus.</item>
///   <item><c>tpa/drive_info</c> — <c>from system.io import DriveInfo</c>: the first use of
///   <c>System.IO</c> loads a trusted-platform assembly through the process-wide gate.</item>
///   <item><c>decorators/bracket_attr_import_used_1429</c> — a CLR attribute resolved through an
///   imported namespace (the attribute resolver's static presence cache).</item>
/// </list>
/// plus a stride-<see cref="SampleStride"/> sample of the single-file fixture corpus in ordinal
/// test-name order (<see cref="Sample"/>). The <c>main.spy</c> + <c>Greeter</c> library pair of
/// cell 4 is lane "server"'s (<c>CompileServerE2ETests</c>, <c>RegistryIsolationTests</c>) — building a
/// library here would cost a compile per run for a cell another test pins.</para>
///
/// <para><b>Schedules</b> are seeded: thread <c>t</c> runs <see cref="Schedule"/> drawn from
/// <c>new Random(ThreadSeeds[t])</c> — every named program under both operations plus
/// <see cref="SampleDrawsPerThread"/> draws from the sample, shuffled. The seeds are literals, so a
/// schedule is the same in every mode, every repetition and every process; no unseeded
/// <see cref="Random"/> and no <c>GetHashCode</c>.</para>
/// </summary>
internal static class InProcessIsolationCorpus
{
    /// <summary>Every 16th single-file fixture (ordinal test-name order) joins the corpus.</summary>
    internal const int SampleStride = 16;

    /// <summary>Sample draws per thread schedule, on top of the named roster's 12 steps.</summary>
    internal const int SampleDrawsPerThread = 36;

    /// <summary>The fuzz seed and iteration of cell 1 (P41 P5.1, @ af6f30901).</summary>
    internal const int Cell1FuzzSeed = 2025;

    internal const int Cell1FuzzIteration = 18;

    /// <summary>The first line of the measured cell-1 program (#2250's issue text).</summary>
    internal const string Cell1FirstLine = "from snake_case import __dunder__";

    /// <summary>One literal seed per thread index; a schedule never depends on anything else.</summary>
    internal static readonly int[] ThreadSeeds =
    {
        2250_01, 2250_02, 2250_03, 2250_04, 2250_05, 2250_06, 2250_07, 2250_08,
    };

    /// <summary>The roster's fixture entries (test name = path under TestFixtures, no extension).</summary>
    private static readonly string[] NamedFixtureStems =
    {
        "fstrings/format_iformattable_class_1988",
        "fstrings/format_no_dunder_format_refused_1988",
        "deferred_overload_annotated_pin_1589",
        "decorators/bracket_attr_import_used_1429",
    };

    internal const string DriveInfoProgramId = "tpa/drive_info";

    internal const string DriveInfoSource =
        "from system.io import DriveInfo\n" +
        "\n" +
        "\n" +
        "def main() -> None:\n" +
        "    drive = DriveInfo(\"/\")\n" +
        "    print(drive.name)\n";

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> NamedValue = new(BuildNamed);

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> SampleValue = new(BuildSample);

    /// <summary>The literal roster, in roster order.</summary>
    internal static IReadOnlyList<IsolationProgram> Named => NamedValue.Value;

    /// <summary>The stride sample of the single-file fixture corpus (named entries excluded).</summary>
    internal static IReadOnlyList<IsolationProgram> Sample => SampleValue.Value;

    /// <summary>
    /// Thread <paramref name="threadIndex"/>'s schedule: every named program under both operations
    /// plus <see cref="SampleDrawsPerThread"/> (program, operation) draws from the sample, shuffled —
    /// all from <c>new Random(ThreadSeeds[threadIndex])</c>.
    /// </summary>
    internal static IReadOnlyList<IsolationStep> Schedule(int threadIndex)
    {
        var random = new Random(ThreadSeeds[threadIndex]);
        var steps = new List<IsolationStep>();
        foreach (var program in Named)
        {
            steps.Add(new IsolationStep(program, IsolationOperation.Analyze));
            steps.Add(new IsolationStep(program, IsolationOperation.Compile));
        }

        for (var i = 0; i < SampleDrawsPerThread; i++)
        {
            var program = Sample[random.Next(Sample.Count)];
            var operation = random.Next(2) == 0 ? IsolationOperation.Analyze : IsolationOperation.Compile;
            steps.Add(new IsolationStep(program, operation));
        }

        // Fisher–Yates on the same seeded generator.
        for (var i = steps.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (steps[i], steps[j]) = (steps[j], steps[i]);
        }

        return steps;
    }

    private static IReadOnlyList<IsolationProgram> BuildNamed()
    {
        var programs = new List<IsolationProgram> { RegenerateCell1Program() };
        var fixtures = SingleFileFixtures().ToDictionary(f => f.TestName, StringComparer.Ordinal);
        foreach (var stem in NamedFixtureStems)
        {
            if (!fixtures.TryGetValue(stem, out var fixture))
                throw new InvalidOperationException($"The isolation roster names fixture '{stem}', which no longer exists as a single-file fixture.");
            programs.Add(FromFixture(fixture));
        }

        programs.Add(new IsolationProgram(DriveInfoProgramId, "drive_info.spy", DriveInfoSource));
        return programs;
    }

    /// <summary>Cell 1's program: the fuzzer's <see cref="Cell1FuzzIteration"/>-th draw (0-based) for <see cref="Cell1FuzzSeed"/>.</summary>
    internal static IsolationProgram RegenerateCell1Program()
    {
        var fuzzer = new SharpyFuzzer(Cell1FuzzSeed);
        var source = "";
        for (var i = 0; i <= Cell1FuzzIteration; i++)
            source = fuzzer.GenerateValidLooking();
        if (!source.StartsWith(Cell1FirstLine, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"SharpyFuzzer({Cell1FuzzSeed}) iteration {Cell1FuzzIteration} no longer regenerates #2250's cell-1 program " +
                $"(expected it to start with `{Cell1FirstLine}`); the generator changed — pin the measured text instead:\n{source}");
        return new IsolationProgram("fuzz/seed2025_iter18", "fuzz_det.spy", source);
    }

    private static IReadOnlyList<IsolationProgram> BuildSample()
    {
        var named = new HashSet<string>(NamedFixtureStems, StringComparer.Ordinal);
        return SingleFileFixtures()
            .Where(f => !named.Contains(f.TestName))
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .Where((_, index) => index % SampleStride == 0)
            .Select(FromFixture)
            .ToList();
    }

    /// <summary>
    /// Single-file, non-skipped fixtures WITHOUT a <c>.features</c> sidecar: the per-thread
    /// <c>Compiler</c> binds its options (and so its feature set) at construction, so a feature-gated
    /// fixture cannot share an instance with the rest of a schedule.
    /// </summary>
    private static IEnumerable<TestFixtureInfo> SingleFileFixtures()
        => FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsMultiFile && f.Features.Count == 0);

    private static IsolationProgram FromFixture(TestFixtureInfo fixture)
        => new(fixture.TestName, Path.GetFileName(fixture.SpyFilePath), File.ReadAllText(fixture.SpyFilePath));
}
