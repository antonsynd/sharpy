using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;
// Sharpy.Core's collection wrappers (Sharpy.List<T> etc.) are in scope via the compiler
// reference and shadow BCL List<T>; alias the BCL namespace so plain List<T> here is unambiguous.
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// On-demand measurement harness for the LSP change→publish analysis latency baseline
/// (Wave 4 Phase 14, borrowing-list "measure first" for #1099). Excluded from the normal test
/// run via <c>Category=Benchmark</c>; run explicitly to refresh the numbers recorded in
/// <c>benchmarks/BASELINE.md</c>:
/// <code>
/// .claude/scripts/dotnet-serialized test \
///   --filter "FullyQualifiedName~LspAnalysisLatencyBaselineHarness" -- \
///   RunConfiguration.TestSessionTimeout=600000
/// </code>
/// It drives the real instrumented paths: single-file full analysis via <see cref="SharpyWorkspace"/>
/// and full project reanalysis via <see cref="LanguageService.OnDocumentChangedAsync"/>. Numbers are
/// warm (post-JIT) medians on the runner's machine and are not asserted — they are printed for
/// transcription into the baseline doc, so this harness never fails on machine-speed differences.
/// </summary>
public sealed class LspAnalysisLatencyBaselineHarness : IDisposable
{
    private const int Warmups = 3;
    private const int TimedRuns = 15;

    // The historical rows: no default references, so CompilerApi.BuildModuleRegistry short-circuits
    // to null and reference loading costs exactly zero. Kept as-is for continuity with the numbers
    // already recorded in benchmarks/BASELINE.md.
    private readonly CompilerApi _api = CreateApi(withDefaultReferences: false);
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public LspAnalysisLatencyBaselineHarness(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_lsp_latency_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// The historical row, kept byte-identical for continuity with the number recorded in
    /// <c>benchmarks/BASELINE.md</c> — <b>including its input's parse error</b>. See
    /// <see cref="MediumFileSource"/>: the source stops at phase 1, so this row has never timed a
    /// full analysis. Do not "fix" the input here; the corrected input is
    /// <see cref="ValidMediumFileSource"/> and has rows of its own below.
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_full_analysis_latency()
    {
        await MeasureSingleFileAsync(_api, "single-file, parse-truncated input (no references)",
            MediumFileSource(), expectSuccess: false);
    }

    /// <summary>
    /// The same single-file analysis, driven through a <see cref="CompilerApi"/> carrying the
    /// default references the real server passes (<c>Program.cs:49-56</c>): <c>Sharpy.Core.dll</c>
    /// plus <c>Sharpy.Stdlib.dll</c> when present next to it.
    /// </summary>
    /// <remarks>
    /// The no-reference row above measures a configuration the server never runs. With no default
    /// references and no project references, <c>CompilerApi.BuildModuleRegistry</c> returns null
    /// before constructing a <c>ModuleRegistry</c> or issuing a single <c>LoadReference</c> call —
    /// so reference loading costs that row exactly zero while costing the server something on every
    /// keystroke. The delta between the two rows is that per-call cost, which the recorded baseline
    /// has never separated from "the unified pipeline's structural per-call overhead" (#1140).
    /// </remarks>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_full_analysis_latency_with_default_references()
    {
        var api = CreateApi(withDefaultReferences: true);
        await MeasureSingleFileAsync(api, "single-file, parse-truncated input (server default references)",
            MediumFileSource(), expectSuccess: false);
    }

    /// <summary>
    /// The row the historical one was always meant to be: an input that actually reaches the end of
    /// the pipeline, so the number covers name resolution, imports, inheritance, type checking and
    /// validation rather than stopping at the parser (#1140).
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_full_analysis_latency_valid_input()
    {
        await MeasureSingleFileAsync(_api, "single-file full analysis (no references)",
            ValidMediumFileSource(), expectSuccess: true);
    }

    /// <summary>
    /// A full analysis of a valid input through the server's real default references — the only one
    /// of the four single-file rows that measures what the LSP actually does on a keystroke.
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_full_analysis_latency_valid_input_with_default_references()
    {
        var api = CreateApi(withDefaultReferences: true);
        await MeasureSingleFileAsync(api, "single-file full analysis (server default references)",
            ValidMediumFileSource(), expectSuccess: true);
    }

    /// <summary>
    /// Breaks the default-references row apart into the per-call pipeline stages a single-file
    /// analysis runs through (#1140), so the row's total can be attributed rather than guessed at.
    /// Wall time and stage breakdown come from the <em>same</em> call, so the residual between them
    /// is real workspace overhead (the analysis task hop, the semaphores, the parse-result
    /// projection) and not a comparison across two different measurements.
    /// </summary>
    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_analysis_stage_attribution()
    {
        var api = CreateApi(withDefaultReferences: true);
        var source = ValidMediumFileSource();
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);

        var wall = new SCG.List<double>();
        var stageTotals = new SCG.List<double>();
        var stageSamples = new SCG.Dictionary<string, SCG.List<double>>(StringComparer.Ordinal);
        var stageOrder = new SCG.List<string>();

        for (var i = 0; i < Warmups + TimedRuns; i++)
        {
            var uri = $"file:///stages_{i}.spy";
            workspace.OpenDocument(uri, source, 1);

            var metrics = new CompilationMetrics(fileName: uri);
            var sw = Stopwatch.StartNew();
            var result = await workspace.GetAnalysisAsync(uri, default, metrics);
            sw.Stop();

            Assert.NotNull(result);
            AssertAnalysisOutcome(result, expectSuccess: true);
            if (i >= Warmups)
            {
                wall.Add(sw.Elapsed.TotalMilliseconds);
                double total = 0;
                foreach (var phase in metrics.Phases)
                {
                    if (!stageSamples.TryGetValue(phase.Name, out var samples))
                    {
                        samples = new SCG.List<double>();
                        stageSamples[phase.Name] = samples;
                        stageOrder.Add(phase.Name);
                    }
                    samples.Add(phase.Duration.TotalMilliseconds);
                    total += phase.Duration.TotalMilliseconds;
                }
                stageTotals.Add(total);
            }
            workspace.CloseDocument(uri);
        }

        Report("single-file stage attribution (server default references)", LineCount(source), wall);
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[LSP stages] stage-sum median={Median(stageTotals):F2}ms vs wall median={Median(wall):F2}ms"));
        foreach (var name in stageOrder)
        {
            var median = Median(stageSamples[name]);
            var share = Median(wall) > 0 ? median / Median(wall) * 100 : 0;
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"[LSP stages]   {name}: median={median:F2}ms ({share:F0}% of wall)"));
        }
    }

    private async Task MeasureSingleFileAsync(CompilerApi api, string label, string source, bool expectSuccess)
    {
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);

        var samples = new SCG.List<double>();
        for (var i = 0; i < Warmups + TimedRuns; i++)
        {
            // A fresh document each iteration forces the full-analysis branch (no incremental reuse),
            // which is exactly the per-change cost the incremental frontend (#1099) aims to avoid.
            var uri = $"file:///medium_{i}.spy";
            workspace.OpenDocument(uri, source, 1);

            var sw = Stopwatch.StartNew();
            var result = await workspace.GetAnalysisAsync(uri);
            sw.Stop();

            Assert.NotNull(result);
            AssertAnalysisOutcome(result, expectSuccess);
            if (i >= Warmups)
                samples.Add(sw.Elapsed.TotalMilliseconds);
            workspace.CloseDocument(uri);
        }

        Report(label, LineCount(source), samples);
    }

    /// <summary>
    /// The project-row counterpart of <see cref="AssertAnalysisOutcome"/>: pins that every file in
    /// the initialized project actually analyzes, so a project row cannot time a parse failure and
    /// an early return (#1224). Like its single-file sibling this asserts correctness only, never
    /// timing.
    /// </summary>
    /// <remarks>
    /// This has to be a <em>positive</em> assertion. Both project rows previously "guarded"
    /// themselves with <c>Assert.Empty(affected)</c> and an <c>affectedFiles=0</c> log check —
    /// which is exactly what a project that never analyzed also produces, so neither could tell a
    /// working fast path from a broken input.
    /// </remarks>
    private static async Task AssertProjectAnalyzesCleanlyAsync(LanguageService service)
    {
        var uris = service.GetProjectFileUris();
        Assert.NotEmpty(uris);

        // Collected across the whole project rather than asserted per file: a parse failure in one
        // member fails analysis for every member, and the first file iterated is usually not the
        // one at fault — reporting only that one names an innocent file and prints no errors.
        var failures = new SCG.List<string>();
        foreach (var uri in uris)
        {
            var result = await service.GetAnalysisAsync(uri);
            if (result is null)
            {
                failures.Add($"{IOPath.GetFileName(uri)}: no analysis was produced");
                continue;
            }

            if (result.Success)
                continue;

            var errors = result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code} {d.Message}").ToList();
            failures.Add($"{IOPath.GetFileName(uri)}: "
                + (errors.Count > 0
                    ? string.Join(", ", errors)
                    : "analysis failed with no diagnostics of its own (another project file did not parse)"));
        }

        Assert.True(failures.Count == 0,
            "The project rows are supposed to time a complete analysis, but the project did not "
            + "analyze cleanly:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// Pins how far down the pipeline a row's input actually gets. This is a correctness assertion,
    /// never a timing one, so it keeps the harness's never-fail-on-machine-speed contract.
    /// </summary>
    /// <remarks>
    /// It matters in both directions. A row labelled "full analysis" whose input stops at the
    /// parser is measuring a third of the pipeline under a name that claims all of it — the defect
    /// that made every previously recorded single-file number an underestimate. And the
    /// parse-truncated rows are only comparable to the recorded baseline for as long as their input
    /// keeps failing the same way, so silently repairing it would break the continuity it exists
    /// to provide.
    /// </remarks>
    private static void AssertAnalysisOutcome(SemanticResult result, bool expectSuccess)
    {
        if (expectSuccess)
        {
            Assert.True(result.Success,
                "This row is supposed to time a complete analysis, but the input did not analyze cleanly: "
                + string.Join(" | ", result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code} {d.Message}")));
        }
        else
        {
            Assert.Contains(result.Diagnostics,
                d => d.Code == DiagnosticCodes.Parser.InvalidTypeAnnotationTarget);
        }
    }

    /// <summary>
    /// Builds the harness's compiler API in one of the two configurations under comparison:
    /// no default references (the historical rows), or the server's own default references.
    /// </summary>
    private static CompilerApi CreateApi(bool withDefaultReferences)
        => withDefaultReferences ? new CompilerApi(null, ServerDefaultReferences()) : new CompilerApi();

    /// <summary>
    /// Reproduces the default reference list <c>Sharpy.Lsp.Program</c> computes at startup:
    /// <c>Sharpy.Core.dll</c>, plus <c>Sharpy.Stdlib.dll</c> when it sits next to it. Both ship into
    /// this test project's output directory, so no copy step is needed. Missing DLLs fail loudly —
    /// silently measuring the no-reference configuration under the default-references label would
    /// make the whole comparison a lie. (This asserts on file layout, never on timings.)
    /// </summary>
    private static string[] ServerDefaultReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        var corePath = IOPath.Combine(baseDir, "Sharpy.Core.dll");
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found next to the test assembly: {corePath}");

        var refs = new SCG.List<string> { corePath };
        var stdlibPath = IOPath.Combine(baseDir, "Sharpy.Stdlib.dll");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found next to the test assembly: {stdlibPath}");
        refs.Add(stdlibPath);
        return refs.ToArray();
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_project_full_reanalysis_latency()
    {
        var files = MediumProjectFiles();
        CreateProject(files);
        using var workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        var service = new LanguageService(workspace, _api, _serviceLogger);
        await service.InitializeProjectAsync(_tempDir);
        await AssertProjectAnalyzesCleanlyAsync(service);

        var mainUri = new Uri(IOPath.Combine(_tempDir, "main.spy")).ToString();

        var samples = new SCG.List<double>();
        for (var i = 0; i < Warmups + TimedRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            await service.OnDocumentChangedAsync(mainUri);
            sw.Stop();
            if (i >= Warmups)
                samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Confirm the real instrumentation emitted a project latency line.
        Assert.Contains(_serviceLogger.Lines, l => l.Contains(AnalysisLatencyLog.Marker)
            && l.Contains($"path={AnalysisLatencyLog.ProjectPath}"));

        var totalLines = files.Sum(f => LineCount(f.Content));
        Report($"project full reanalysis ({files.Length} files)", totalLines, samples);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_project_no_change_edit_latency()
    {
        var files = MediumProjectFiles();
        CreateProject(files);
        using var workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        using var service = new LanguageService(workspace, _api, _serviceLogger);
        await service.InitializeProjectAsync(_tempDir);
        await AssertProjectAnalyzesCleanlyAsync(service);

        // stats.spy is used deliberately: AstFingerprint conservatively reports BodyOnly for any
        // function body it cannot prove equal (e.g. list literals/comprehensions, which main.spy
        // has), so only files whose bodies stay within its provable subset ever classify NoChange.
        // stats.spy qualifies, so a whitespace/comment-only edit to it exercises the real skip.
        var statsUri = new Uri(IOPath.Combine(_tempDir, "stats.spy")).ToString();
        var statsSource = files.First(f => f.Name == "stats.spy").Content;

        // Open the file with its on-disk content: every subsequent change is structurally identical
        // (a comment/whitespace-only edit), which the NoChange fast path (#1099) must skip without
        // re-running whole-project analysis — the cost this row isolates.
        workspace.OpenDocument(statsUri, statsSource, 1);

        var samples = new SCG.List<double>();
        for (var i = 0; i < Warmups + TimedRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            var affected = await service.OnDocumentChangedAsync(statsUri);
            sw.Stop();
            Assert.Empty(affected); // the fast path publishes nothing
            if (i >= Warmups)
                samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Confirm the skip is the path exercised: a project latency line with affectedFiles=0.
        Assert.Contains(_serviceLogger.Lines, l => l.Contains(AnalysisLatencyLog.Marker)
            && l.Contains($"path={AnalysisLatencyLog.ProjectPath}")
            && l.Contains("affectedFiles=0"));

        var totalLines = files.Sum(f => LineCount(f.Content));
        Report($"project no-change edit skip ({files.Length} files)", totalLines, samples);
    }

    private readonly CapturingLogger<LanguageService> _serviceLogger = new();

    /// <summary>Median of a sample set, leaving the caller's list order untouched.</summary>
    private static double Median(SCG.List<double> samples)
    {
        var sorted = new SCG.List<double>(samples);
        sorted.Sort();
        return sorted[sorted.Count / 2];
    }

    private void Report(string label, int lines, SCG.List<double> samples)
    {
        samples.Sort();
        int n = samples.Count;
        double median = samples[n / 2];
        double min = samples[0];
        double max = samples[n - 1];
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[LSP latency] {label}: lines={lines} runs={n} median={median:F1}ms min={min:F1}ms max={max:F1}ms"));
    }

    private static int LineCount(string s) => s.Count(c => c == '\n') + 1;

    private void CreateProject((string Name, string Content)[] files)
    {
        var spyFiles = string.Join("\n        ",
            files.Select(f => $"<SpyFile Include=\"{f.Name}\" />"));
        var projectContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>Test</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        {spyFiles}
    </ItemGroup>
</Project>";
        File.WriteAllText(IOPath.Combine(_tempDir, "test.spyproj"), projectContent);
        foreach (var (name, content) in files)
            File.WriteAllText(IOPath.Combine(_tempDir, name), content);
    }

    /// <summary>
    /// The input every previously recorded single-file number was measured against.
    /// </summary>
    /// <remarks>
    /// <b>It does not parse.</b> <c>Grid.__init__</c> annotates assignments to attributes
    /// (<c>self.width: int = width</c>), which Sharpy rejects with SPY0107 — a type annotation
    /// targets a bare identifier, and fields are declared at class level the way <c>Point</c> does
    /// it here. <c>ProjectCompiler.ParseAllFiles</c> therefore returns false and
    /// <c>AnalyzeProject</c> returns after phase 1, so name resolution, import resolution,
    /// inheritance, type checking and validation never run. Every number recorded from this input
    /// times a lexer, a parser, and an early return under a label that says "full analysis" (#1140).
    /// <para>
    /// Kept verbatim so the historical rows stay comparable to what is already written down.
    /// <see cref="ValidMediumFileSource"/> is the corrected input; new rows use that.
    /// </para>
    /// </remarks>
    private static string MediumFileSource()
    {
        // ~130 lines: classes, methods, comprehensions, control flow — a representative edit target.
        var lines = new SCG.List<string>
        {
            "class Point:",
            "    x: int = 0",
            "    y: int = 0",
            "",
            "    def __init__(self, x: int, y: int):",
            "        self.x = x",
            "        self.y = y",
            "",
            "    def manhattan(self, other: Point) -> int:",
            "        return abs(self.x - other.x) + abs(self.y - other.y)",
            "",
            "class Grid:",
            "    def __init__(self, width: int, height: int):",
            "        self.width: int = width",
            "        self.height: int = height",
            "",
            "    def cells(self) -> list[Point]:",
            "        result: list[Point] = []",
            "        for gx in range(self.width):",
            "            for gy in range(self.height):",
            "                result.append(Point(gx, gy))",
            "        return result",
            "",
        };
        for (var i = 0; i < 20; i++)
        {
            lines.Add($"def compute_{i}(values: list[int]) -> int:");
            lines.Add("    total: int = 0");
            lines.Add("    for v in values:");
            lines.Add("        if v % 2 == 0:");
            lines.Add("            total = total + v");
            lines.Add("        else:");
            lines.Add("            total = total - v");
            lines.Add($"    doubled: list[int] = [n * 2 for n in values if n > {i}]");
            lines.Add("    return total + len(doubled)");
            lines.Add("");
        }
        lines.Add("def main():");
        lines.Add("    grid: Grid = Grid(8, 8)");
        lines.Add("    pts: list[Point] = grid.cells()");
        lines.Add("    print(len(pts))");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// <see cref="MediumFileSource"/> with its two SPY0107 lines corrected, so analysis runs to
    /// completion. Same shape and roughly the same size — classes, methods, comprehensions,
    /// control flow — and the only change is that <c>Grid</c> declares its fields at class level
    /// and assigns them unannotated in <c>__init__</c>, exactly as <c>Point</c> already did.
    /// </summary>
    internal static string ValidMediumFileSource()
    {
        var lines = new SCG.List<string>
        {
            "class Point:",
            "    x: int = 0",
            "    y: int = 0",
            "",
            "    def __init__(self, x: int, y: int):",
            "        self.x = x",
            "        self.y = y",
            "",
            "    def manhattan(self, other: Point) -> int:",
            "        return abs(self.x - other.x) + abs(self.y - other.y)",
            "",
            "class Grid:",
            "    width: int = 0",
            "    height: int = 0",
            "",
            "    def __init__(self, width: int, height: int):",
            "        self.width = width",
            "        self.height = height",
            "",
            "    def cells(self) -> list[Point]:",
            "        result: list[Point] = []",
            "        for gx in range(self.width):",
            "            for gy in range(self.height):",
            "                result.append(Point(gx, gy))",
            "        return result",
            "",
        };
        for (var i = 0; i < 20; i++)
        {
            lines.Add($"def compute_{i}(values: list[int]) -> int:");
            lines.Add("    total: int = 0");
            lines.Add("    for v in values:");
            lines.Add("        if v % 2 == 0:");
            lines.Add("            total = total + v");
            lines.Add("        else:");
            lines.Add("            total = total - v");
            lines.Add($"    doubled: list[int] = [n * 2 for n in values if n > {i}]");
            lines.Add("    return total + len(doubled)");
            lines.Add("");
        }
        lines.Add("def main():");
        lines.Add("    grid: Grid = Grid(8, 8)");
        lines.Add("    pts: list[Point] = grid.cells()");
        lines.Add("    print(len(pts))");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The 6-file project the two project rows are measured against: a small library plus an entry
    /// point that imports all of it.
    /// </summary>
    /// <remarks>
    /// <b>Until #1224 this project did not parse.</b> <c>shapes.spy</c> annotated assignments to
    /// attributes (<c>self.a: Vec = a</c>) — the same SPY0107 spelling that made every single-file
    /// number an underestimate (#1140). #1140 corrected the single-file input
    /// (<see cref="MediumFileSource"/> → <see cref="ValidMediumFileSource"/>) but left this
    /// constructor, in the same file, carrying the identical defect. <c>registry.spy</c> and
    /// <c>main.spy</c> both import <c>Triangle</c>, so <c>ProjectCompiler.ParseAllFiles</c> returned
    /// false and <c>AnalyzeProject</c> returned after phase 1 — meaning the recorded
    /// "project full reanalysis" and "project no-change edit skip" rows in
    /// <c>benchmarks/BASELINE.md</c> timed a parse and an early return, never a reanalysis.
    /// <para>
    /// Unlike the single-file case there is no published comparison table depending on the broken
    /// spelling, so the input is corrected here rather than kept alongside a valid twin; the
    /// superseded rows are marked in the baseline doc instead.
    /// </para>
    /// </remarks>
    private static (string Name, string Content)[] MediumProjectFiles()
    {
        return new (string, string)[]
        {
            ("geometry.spy",
                "class Vec:\n" +
                "    x: int = 0\n" +
                "    y: int = 0\n" +
                "\n" +
                "    def __init__(self, x: int, y: int):\n" +
                "        self.x = x\n" +
                "        self.y = y\n" +
                "\n" +
                "    def dot(self, other: Vec) -> int:\n" +
                "        return self.x * other.x + self.y * other.y\n"),
            ("stats.spy",
                "def mean(values: list[int]) -> int:\n" +
                "    if len(values) == 0:\n" +
                "        return 0\n" +
                "    total: int = 0\n" +
                "    for v in values:\n" +
                "        total = total + v\n" +
                "    return total // len(values)\n"),
            // Fields are declared at class level and assigned unannotated in __init__. Annotating
            // the assignment instead (`self.a: Vec = a`) is SPY0107, which is how this file used to
            // be spelled — see the remark on MediumProjectFiles.
            ("shapes.spy",
                "from geometry import Vec\n" +
                "\n" +
                "class Triangle:\n" +
                "    a: Vec\n" +
                "    b: Vec\n" +
                "    c: Vec\n" +
                "\n" +
                "    def __init__(self, a: Vec, b: Vec, c: Vec):\n" +
                "        self.a = a\n" +
                "        self.b = b\n" +
                "        self.c = c\n" +
                "\n" +
                "    def perimeter_dot(self) -> int:\n" +
                "        return self.a.dot(self.b) + self.b.dot(self.c)\n"),
            ("pipeline.spy",
                "from stats import mean\n" +
                "\n" +
                "def summarize(rows: list[int]) -> int:\n" +
                "    scaled: list[int] = [r * 3 for r in rows if r > 0]\n" +
                "    return mean(scaled)\n"),
            ("registry.spy",
                "from geometry import Vec\n" +
                "from shapes import Triangle\n" +
                "\n" +
                "def make_triangle() -> Triangle:\n" +
                "    return Triangle(Vec(0, 0), Vec(1, 0), Vec(0, 1))\n"),
            ("main.spy",
                "from geometry import Vec\n" +
                "from shapes import Triangle\n" +
                "from stats import mean\n" +
                "from pipeline import summarize\n" +
                "from registry import make_triangle\n" +
                "\n" +
                "def main():\n" +
                "    t: Triangle = make_triangle()\n" +
                "    print(t.perimeter_dot())\n" +
                "    print(summarize([1, 2, 3, 4, 5]))\n" +
                "    print(mean([10, 20, 30]))\n"),
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a leftover temp dir is harmless.
        }
    }

    /// <summary>Minimal ILogger that records formatted messages for assertion.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public SCG.List<string> Lines { get; } = new();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
