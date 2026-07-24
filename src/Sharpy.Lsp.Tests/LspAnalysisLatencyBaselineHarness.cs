using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
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

    private readonly CompilerApi _api = new();
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public LspAnalysisLatencyBaselineHarness(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_lsp_latency_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public async Task Measure_single_file_full_analysis_latency()
    {
        var source = MediumFileSource();
        using var workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);

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
            if (i >= Warmups)
                samples.Add(sw.Elapsed.TotalMilliseconds);
            workspace.CloseDocument(uri);
        }

        Report("single-file full analysis", LineCount(source), samples);
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

    private static (string Name, string Content)[] MediumProjectFiles()
    {
        // 6 interdependent files: a small library plus an entry point that imports all of it.
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
            ("shapes.spy",
                "from geometry import Vec\n" +
                "\n" +
                "class Triangle:\n" +
                "    def __init__(self, a: Vec, b: Vec, c: Vec):\n" +
                "        self.a: Vec = a\n" +
                "        self.b: Vec = b\n" +
                "        self.c: Vec = c\n" +
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
