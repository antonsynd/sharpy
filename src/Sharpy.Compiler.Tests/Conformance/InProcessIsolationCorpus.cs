using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Tests.Fuzz;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Registry;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>One program of the in-process isolation corpus: a stable id (the allowlist key), the
/// file name the compile names it by, and its source.</summary>
internal sealed record IsolationProgram(string Id, string FileName, string Source)
{
    /// <summary>Assembly paths this program is compiled against, on top of the instance's own (Sharpy.Core).</summary>
    public IReadOnlyList<string> References { get; init; } = Array.Empty<string>();

    /// <summary>
    /// A positive control on the solo oracle, or null: <see cref="OracleSucceeds"/> (the fresh answer
    /// must be a success) or a diagnostic code the fresh answer must carry — the probe reads what it
    /// is about, so a divergence from it is a difference in the thing the seam decides.
    /// </summary>
    public string? OracleMustCarry { get; init; }

    internal const string OracleSucceeds = "success";
}

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
/// The programs one sweep run schedules: the literal roster (every program under both operations),
/// the builtin-shadowing triggers (analyze only — the write the seam refuses happens in analysis),
/// the typed probes that read what a write would change (both operations, and once more after the
/// last trigger), and the referenced-library pair of this run (both operations, first in every
/// schedule so an instance that caches one analysis context per reference set switches contexts
/// before the trigger → probe body, never inside it).
/// </summary>
internal sealed record IsolationRoster(
    IReadOnlyList<IsolationProgram> Named,
    IReadOnlyList<IsolationProgram> LibraryPair,
    IReadOnlyList<IsolationProgram> Triggers,
    IReadOnlyList<IsolationProgram> Probes);

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
/// test-name order (<see cref="Sample"/>).</para>
///
/// <para><b>Triggers and probes</b> (the frozen-master seam, cells 2–3 and the F1/F2 type-definition
/// twins): a trigger defines a builtin's name — <see cref="WarmInstanceTriggers.AllTexts"/> verbatim,
/// plus a generated kind × name × position matrix (<see cref="GeneratedTriggers"/>: the sibling-cell
/// prober's generator) — and a probe (<see cref="Probes"/>) later USES the builtin in a typed position
/// whose diagnostic differs when the master was written: <c>b: bool = len("abc")</c> reads
/// <c>len</c>'s return type (SPY0220 names <c>int32</c>), <c>s._size</c> reads whether the master
/// <c>str</c> gained the block-class's backing field (SPY0203 vanishes), <c>xs._size</c> the same
/// through <c>list</c> and <c>object</c>, <c>b: bool = format(5, "&gt;3")</c> reads <c>format</c>'s
/// return type, a user <c>@dataclass</c> reads the master's constructor arity. Each probe's oracle is
/// a positive control (<see cref="IsolationProgram.OracleMustCarry"/>). The regular slice carries
/// <see cref="RegularGeneratedTriggerCount"/> generated triggers; the GapDiscovery mode the whole matrix.</para>
///
/// <para><b>The referenced-library pair</b> (the visibility seam, cells 4–5): a tiny C# library built
/// per sweep run by Roslyn into a per-run namespace (<see cref="BuildLibraryPair"/>), and ONE
/// <c>main.spy</c> importing it compiled twice — <c>library/greeter_referenced</c> with the reference
/// (success) and <c>library/greeter_unreferenced</c> with none (SPY0300). A reference another registry
/// loaded must stay invisible; a visibility that scans the process answers the unreferenced compile
/// from the leaked copy. The namespace is unique per run so the unreferenced oracle, taken before any
/// load of THIS run's library, cannot be answered by an earlier run's copy in the same process.</para>
///
/// <para><b>Schedules</b> are seeded: thread <c>t</c> runs <see cref="Schedule"/> drawn from
/// <c>new Random(ThreadSeeds[t])</c> — the library pair first, then every named program under both
/// operations, every trigger under <c>analyze</c>, every probe under both operations and
/// <see cref="SampleDrawsPerThread"/> draws from the sample, shuffled, then every probe once more under
/// <c>analyze</c> (after the schedule's last trigger). The seeds are literals, so a schedule is the same
/// in every mode, every repetition and every process; no unseeded <see cref="Random"/> and no
/// <c>GetHashCode</c>.</para>
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

    internal const string ReferencedProgramId = "library/greeter_referenced";

    internal const string UnreferencedProgramId = "library/greeter_unreferenced";

    /// <summary>The code an import of a module no reference declares is refused with.</summary>
    internal const string ModuleNotFound = "SPY0300";

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> NamedValue = new(BuildNamed);

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> SampleValue = new(BuildSample);

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> ProbesValue = new(BuildProbes);

    private static readonly Lazy<IReadOnlyList<IsolationProgram>> GeneratedTriggersValue = new(BuildGeneratedTriggers);

    /// <summary>The literal roster, in roster order.</summary>
    internal static IReadOnlyList<IsolationProgram> Named => NamedValue.Value;

    /// <summary>The stride sample of the single-file fixture corpus (named entries excluded).</summary>
    internal static IReadOnlyList<IsolationProgram> Sample => SampleValue.Value;

    /// <summary>The typed probes, each with its oracle control.</summary>
    internal static IReadOnlyList<IsolationProgram> Probes => ProbesValue.Value;

    /// <summary>The generated trigger matrix (kind × name × position), in generator order.</summary>
    internal static IReadOnlyList<IsolationProgram> GeneratedTriggers => GeneratedTriggersValue.Value;

    /// <summary>How many generated triggers the regular slice carries (the first of the matrix); the full set is the GapDiscovery mode's.</summary>
    internal const int RegularGeneratedTriggerCount = 8;

    /// <summary>The trigger programs of a mode: the verbatim <see cref="WarmInstanceTriggers"/> texts, then the generated matrix (whole or its regular head).</summary>
    internal static IReadOnlyList<IsolationProgram> Triggers(bool full)
    {
        var verbatim = WarmInstanceTriggers.AllTexts
            .Select(t => new IsolationProgram("trigger/" + t.Name, "trigger.spy", t.Text));
        var generated = full ? GeneratedTriggers : GeneratedTriggers.Take(RegularGeneratedTriggerCount);
        return verbatim.Concat(generated).ToList();
    }

    /// <summary>
    /// Every program id any mode can schedule — the allowlist's universe: the literal roster, the
    /// sample, the probes, the full trigger set and the library pair's two ids (the pair's TEXT is
    /// per run, its ids are constant). A row naming anything else is refused.
    /// </summary>
    internal static IReadOnlySet<string> AllIds => AllIdsValue.Value;

    private static readonly Lazy<IReadOnlySet<string>> AllIdsValue = new(() =>
        Named.Concat(Sample).Concat(Probes).Concat(Triggers(full: true)).Select(p => p.Id)
            .Concat(new[] { ReferencedProgramId, UnreferencedProgramId })
            .ToHashSet(StringComparer.Ordinal));

    /// <summary>The roster of one run: the literal roster, this run's library pair, the mode's triggers and the probes.</summary>
    internal static IsolationRoster Roster(bool full, IReadOnlyList<IsolationProgram> libraryPair)
        => new(Named, libraryPair, Triggers(full), Probes);

    /// <summary>
    /// Thread <paramref name="threadIndex"/>'s schedule over <paramref name="roster"/>: the library
    /// pair (both operations, in roster order, first), then — shuffled on
    /// <c>new Random(ThreadSeeds[threadIndex])</c> — every named program under both operations,
    /// every trigger under <c>analyze</c>, every probe under both operations and
    /// <see cref="SampleDrawsPerThread"/> (program, operation) draws from the sample; then every probe
    /// under <c>analyze</c>, after the last trigger.
    /// </summary>
    internal static IReadOnlyList<IsolationStep> Schedule(int threadIndex, IsolationRoster roster)
    {
        var random = new Random(ThreadSeeds[threadIndex]);
        var steps = new List<IsolationStep>();
        foreach (var program in roster.LibraryPair)
        {
            steps.Add(new IsolationStep(program, IsolationOperation.Analyze));
            steps.Add(new IsolationStep(program, IsolationOperation.Compile));
        }

        var head = steps.Count;
        foreach (var program in roster.Named.Concat(roster.Probes))
        {
            steps.Add(new IsolationStep(program, IsolationOperation.Analyze));
            steps.Add(new IsolationStep(program, IsolationOperation.Compile));
        }

        foreach (var program in roster.Triggers)
            steps.Add(new IsolationStep(program, IsolationOperation.Analyze));

        for (var i = 0; i < SampleDrawsPerThread; i++)
        {
            var program = Sample[random.Next(Sample.Count)];
            var operation = random.Next(2) == 0 ? IsolationOperation.Analyze : IsolationOperation.Compile;
            steps.Add(new IsolationStep(program, operation));
        }

        // Fisher–Yates over the body, on the same seeded generator.
        for (var i = steps.Count - 1; i > head; i--)
        {
            var j = head + random.Next(i - head + 1);
            (steps[i], steps[j]) = (steps[j], steps[i]);
        }

        foreach (var program in roster.Probes)
            steps.Add(new IsolationStep(program, IsolationOperation.Analyze));

        return steps;
    }

    // ================================================================
    // The referenced-library pair
    // ================================================================

    /// <summary>
    /// Builds this run's library into <paramref name="directory"/> and returns the pair: the same
    /// <c>main.spy</c> compiled with the reference (<see cref="ReferencedProgramId"/>, oracle: success)
    /// and without it (<see cref="UnreferencedProgramId"/>, oracle: <see cref="ModuleNotFound"/>).
    /// <paramref name="tag"/> is lowercase hex unique to the run: the module is <c>sweeplib_{tag}</c>,
    /// the namespace <c>Sweeplib{Tag}</c> (the registry's PascalCase mapping of a non-system module).
    /// </summary>
    internal static IReadOnlyList<IsolationProgram> BuildLibraryPair(string directory, string tag)
    {
        var moduleName = "sweeplib_" + tag;
        var namespaceName = "Sweeplib" + char.ToUpperInvariant(tag[0]) + tag.Substring(1);
        var assemblyName = "P54SweepLib_" + tag;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, assemblyName + ".dll");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    $"namespace {namespaceName} {{ public static class Greeter {{ public static string Hello() => \"hello\"; }} }}"),
            },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        if (!emit.Success)
            throw new InvalidOperationException("The isolation library did not build (an instrument fault):\n" + string.Join("\n", emit.Diagnostics));

        var source =
            $"from {moduleName} import Greeter\n" +
            "\n" +
            "\n" +
            "def main() -> None:\n" +
            "    print(Greeter.hello())\n";
        return new[]
        {
            new IsolationProgram(ReferencedProgramId, "main.spy", source)
            {
                References = new[] { path },
                OracleMustCarry = IsolationProgram.OracleSucceeds,
            },
            new IsolationProgram(UnreferencedProgramId, "main.spy", source)
            {
                OracleMustCarry = ModuleNotFound,
            },
        };
    }

    // ================================================================
    // Triggers and probes
    // ================================================================

    private static IReadOnlyList<IsolationProgram> BuildProbes()
    {
        IsolationProgram Probe(string name, string control, params string[] lines)
            => new("probe/" + name, "probe.spy", string.Join("\n", lines) + "\n") { OracleMustCarry = control };

        return new[]
        {
            Probe("builtins_ok", IsolationProgram.OracleSucceeds,
                "def main() -> None:",
                "    print(len(\"abc\"))",
                "    print(str(5))",
                "    xs = list[int]()",
                "    xs.append(1)",
                "    print(len(xs))",
                "    d = dict[str, int]()",
                "    d[\"a\"] = 1",
                "    print(format(5, \">3\"))",
                "    o: object = 5",
                "    print(o)"),
            Probe("len_typed", "SPY0220",
                "def main() -> None:",
                "    b: bool = len(\"abc\")"),
            Probe("str_no_member", "SPY0203",
                "def main() -> None:",
                "    s = \"abc\"",
                "    print(s._size)",
                "    print(s.x)"),
            Probe("list_no_member", "SPY0203",
                "def main() -> None:",
                "    xs = list[int]()",
                "    print(xs._size)",
                "    print(xs.x)"),
            Probe("exception_typed", "SPY0220",
                "class MyErr(Exception):",
                "    pass",
                "",
                "def main() -> None:",
                "    try:",
                "        raise MyErr(\"x\")",
                "    except Exception as e:",
                "        b: bool = e",
                "        print(e)"),
            Probe("format_typed", "SPY0220",
                "def main() -> None:",
                "    b: bool = format(5, \">3\")",
                "    c: bool = print"),
            Probe("dataclass_user", "SPY0220",
                "@dataclass",
                "class P:",
                "    x: int",
                "",
                "def main() -> None:",
                "    p = P(1)",
                "    b: bool = p.x",
                "    print(p)"),
            Probe("str_upper_control", IsolationProgram.OracleSucceeds,
                "def main() -> None:",
                "    s = \"abc\"",
                "    print(s.upper())"),
        };
    }

    /// <summary>The generated matrix: kind × name × position, every cell a builtin's name defined where NameResolver declares no symbol.</summary>
    private static IReadOnlyList<IsolationProgram> BuildGeneratedTriggers()
    {
        var kinds = new (string Kind, Func<string, string[]> Body)[]
        {
            ("class", n => new[]
            {
                $"class {n}:",
                "    property size: int = 0",
                "    property set size(self, v: int) -> None:",
                "        self._size = v",
                "    def __len__(self) -> int:",
                "        return 1",
            }),
            ("def", n => new[] { $"def {n}(x: int) -> int:", "    return x" }),
            ("dataclass", n => new[] { "@dataclass", $"class {n}:", "    x: int" }),
            ("struct", n => new[] { $"struct {n}:", "    x: int" }),
            ("interface", n => new[] { $"interface {n}:", "    def probe(self) -> int: ..." }),
            ("union", n => new[] { $"union {n}:", "    case Probe(value: int)" }),
            ("delegate", n => new[] { $"delegate {n}(value: int) -> int" }),
        };
        var positions = new (string Position, string[] Before, int Indent, string[] After)[]
        {
            ("def-body", new[] { "def helper() -> None:" }, 4, new[] { "    print('helper')" }),
            ("mod-try", new[] { "try:" }, 4, new[] { "except Exception:", "    pass" }),
        };
        var names = new[] { "list", "format", "str", "len" };

        // The regular head first: {class, def} × {list, format} × both positions — the member write
        // through `list`/`object` and the retyped `format` the prober measured at base.
        var cells = new List<(string Kind, string Name, string Position)>();
        foreach (var kind in kinds.Take(2))
        {
            foreach (var name in names.Take(2))
            {
                foreach (var position in positions)
                    cells.Add((kind.Kind, name, position.Position));
            }
        }

        foreach (var kind in kinds)
        {
            foreach (var name in names)
            {
                foreach (var position in positions)
                {
                    if (!cells.Contains((kind.Kind, name, position.Position)))
                        cells.Add((kind.Kind, name, position.Position));
                }
            }
        }

        var programs = new List<IsolationProgram>();
        foreach (var (kind, name, position) in cells)
        {
            var body = kinds.First(k => k.Kind == kind).Body(name);
            var layout = positions.First(p => p.Position == position);
            var lines = layout.Before
                .Concat(body.Select(l => new string(' ', layout.Indent) + l))
                .Concat(layout.After)
                .Concat(new[] { "", "def main() -> None:", "    print('ok')" });
            programs.Add(new IsolationProgram($"trigger/{kind}_{name}_{position}", "trigger.spy", string.Join("\n", lines) + "\n"));
        }

        return programs;
    }

    // ================================================================
    // The literal roster and the sample
    // ================================================================

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
