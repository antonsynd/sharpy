using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The whole-build half of warm ≡ cold (#2468, plan-21d2f5): a warm <c>--incremental</c> build whose
/// every input is the last successful build's is an up-to-date check, not a build. It loads no
/// reference, reports what that build reported under THIS build's policy, hands every consumer what
/// that build handed it, and leaves every output as written; a build with any one input moved is not
/// up to date and builds what a cold build of the moved inputs builds.
///
/// <para>Every build here goes through <see cref="CompilerOptionsFactory.ForProject"/> with
/// <c>incremental: true</c> after <see cref="DefaultReferences.ApplyTo"/>, as <c>sharpyc project
/// --incremental</c> does: the check is opt-in on that flag (D2), so a builder that sets only
/// <see cref="CompilerOptions.Incremental"/> would never reach it, and every assertion below would hold
/// with the check deleted (plan F1). Each not-up-to-date leg first asserts that its unchanged twin IS
/// up to date on the same builder and specimen (plan F2).</para>
/// </summary>
public class ColdWarmUpToDateCheckTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ColdWarmUpToDateCheckTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_uptodate_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    // Two unused locals: two SPY0451 warnings, so the --max-errors row has two to cap.
    private const string LibSource = @"@dataclass
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int) -> None:
        self.x = x
        self.y = y


class Counter:
    @final
    limit: long

    def __init__(self, limit: long) -> None:
        self.limit = limit

    @must_use
    def next_value(self) -> long:
        return self.limit

    def poke(self) -> None:
        scratch: int = 99

    def prod(self) -> None:
        other: int = 7
";

    private const string MainSource = @"from lib import Counter, Point


def main() -> None:
    c = Counter(10)
    total: long = c.next_value()
    p = Point(1, 2)
    print(total)
    print(p.x)
";

    private const string OtherSource = "def helper() -> int:\n    return 1\n";

    private const string UpToDateLogPrefix = "Up to date: every input of";
    private const string EmissionSkipped = "Phase 7: Assembly up to date; IL emission skipped";

    private string Dir(string area) => Path.Combine(_tempDir, area);

    private string Write(string area, string name, string content)
    {
        var dir = Dir(area);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private ProjectConfig Config(string area, IEnumerable<string> sources, string? assemblyName = null,
        string? outputOverride = null, List<string>? features = null)
    {
        var dir = Dir(area);
        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(dir, "test.spyproj"),
            ProjectDirectory = dir,
            RootNamespace = "UpToDate",
            AssemblyName = assemblyName,
            SourceFiles = sources.ToList(),
            Configuration = "Debug",
            Features = features ?? new List<string>(),
            OutputAssemblyPathOverride = outputOverride,
        };
    }

    /// <summary>
    /// The CLI's builder (<c>ProjectCommand</c>): the defaults join the project's references, and the
    /// options come from <see cref="CompilerOptionsFactory.ForProject"/>. A fresh <see cref="Compiler"/>
    /// per build, so one build's discovery cannot leak into the next build's stamp.
    /// </summary>
    private static ProjectCompilationResult Build(ProjectConfig config, bool incremental = true,
        bool warnAsError = false, string[]? nowarn = null, int maxErrors = 0, ICompilerLogger? logger = null)
    {
        var defaults = DefaultReferences.Resolve();
        DefaultReferences.ApplyTo(config, defaults);
        var options = CompilerOptionsFactory.ForProject(config, defaultReferences: defaults,
            warningsAsErrors: warnAsError, additionalSuppressedWarnings: nowarn, maxErrors: maxErrors,
            incremental: incremental);
        return new Compiler(options, logger ?? NullLogger.Instance).CompileProject(config);
    }

    /// <summary>A cold build: the same builder with no cache.</summary>
    private static ProjectCompilationResult Cold(ProjectConfig config, bool warnAsError = false,
        string[]? nowarn = null, int maxErrors = 0)
        => Build(config, incremental: false, warnAsError: warnAsError, nowarn: nowarn, maxErrors: maxErrors);

    private static bool UpToDate(ProjectCompilationResult result) => result.Metrics?.UpToDate == true;

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll()
            .Select(d => $"{d.Severity}:{d.Code}@{Path.GetFileName(d.FilePath ?? "")}:{d.Line}:{d.Column}"
                + $"[{d.Span?.Start.ToString() ?? "-"},{d.Span?.Length.ToString() ?? "-"}] {d.Message}")
            .OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>The generated C# under the keys the result hands its consumers (<c>--emit-cs-to</c>).</summary>
    private static IReadOnlyDictionary<string, string> Generated(ProjectCompilationResult result)
        => new SortedDictionary<string, string>(result.GeneratedCSharpFiles, StringComparer.Ordinal);

    private string CachePath(string area) => Path.Combine(Dir(area), "obj", "Debug", ".sharpy-cache");

    private string SymbolsPath(string area) => Path.Combine(Dir(area), "obj", "Debug", ".sharpy-symbols");

    private void DoctorCache(string area, Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(File.ReadAllText(CachePath(area)))!.AsObject();
        edit(root);
        File.WriteAllText(CachePath(area), root.ToJsonString());
    }

    private sealed class InfoCapturingLogger : ICompilerLogger
    {
        public List<string> InfoMessages { get; } = new();

        public void LogInfo(string message) => InfoMessages.Add(message);
        public bool IsEnabled(CompilerLogLevel level)
            => level is CompilerLogLevel.Error or CompilerLogLevel.Warning or CompilerLogLevel.Info;

        public void LogDebug(string message) { }
        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) { }
        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
    }

    // -----------------------------------------------------------------------
    // Positive theory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Cold, then warm with nothing changed: the warm build is up to date. It ran no discovery (the
    /// discovery-cost theory, structural: the stamp is the real discovery time, so a reference load
    /// that ran anyway shows), and everything it hands out is the cold build's: diagnostics, C#, the
    /// output path, and the assembly as the cold build wrote it. Positive control: the cold build is
    /// not up to date and did run discovery.
    /// </summary>
    [Fact]
    public void WarmNoChangeBuild_IsAnUpToDateCheck_AndHandsOutWhatTheColdBuildDid()
    {
        const string area = "positive";
        var lib = Write(area, "lib.spy", LibSource);
        var main = Write(area, "main.spy", MainSource);
        var other = Write(area, "other.spy", OtherSource);
        var config = Config(area, new[] { lib, main, other });

        var coldLog = new InfoCapturingLogger();
        var cold = Build(config, logger: coldLog);
        cold.Success.Should().BeTrue("the specimen must compile. Diagnostics:\n" + Diagnostics(cold));
        UpToDate(cold).Should().BeFalse("control: a build with no cache is not up to date");
        cold.Metrics!.DiscoveryTime.Should().BeGreaterThan(TimeSpan.Zero,
            "control: the cold build loads the default references, so the absence below can be seen");
        cold.Metrics.FormatAsText().Should().Contain("Module Discovery");
        coldLog.InfoMessages.Should().NotContain(m => m.StartsWith(UpToDateLogPrefix, StringComparison.Ordinal));
        Diagnostics(cold).Should().Contain("Warning", "non-vacuity: the replay must carry something");
        var assemblyPath = cold.OutputAssemblyPath!;
        var coldBytes = File.ReadAllBytes(assemblyPath);
        var coldWriteTime = File.GetLastWriteTimeUtc(assemblyPath);

        var warmLog = new InfoCapturingLogger();
        var warm = Build(config, logger: warmLog);

        UpToDate(warm).Should().BeTrue("nothing changed, so the build is the up-to-date check");
        warm.Success.Should().BeTrue("Diagnostics:\n" + Diagnostics(warm));
        warm.Metrics!.DiscoveryTime.Should().Be(TimeSpan.Zero,
            "an up-to-date build loads no reference (#2468: discovery was the floor)");
        warm.Metrics.FormatAsText().Should().NotContain("Module Discovery")
            .And.Contain("Up to date:");
        warm.Metrics.FormatAsJson().Should().Contain("\"up_to_date\": true");
        cold.Metrics.FormatAsJson().Should().Contain("\"up_to_date\": false");
        warmLog.InfoMessages.Should().Contain(m => m.StartsWith(UpToDateLogPrefix, StringComparison.Ordinal));

        Diagnostics(warm).Should().Be(Diagnostics(cold), "the replay reports what the cold build reported");
        Generated(warm).Should().Equal(Generated(cold), "the replay serves the cold build's C# under its keys");
        warm.OutputAssemblyPath.Should().Be(cold.OutputAssemblyPath, "F4: the replay names the output");
        File.ReadAllBytes(assemblyPath).Should().Equal(coldBytes, "the assembly is left as written");
        File.GetLastWriteTimeUtc(assemblyPath).Should().Be(coldWriteTime, "the assembly is not rewritten");
        warm.Metrics.SkippedFiles.Should().Equal(new[] { lib, main, other }.OrderBy(f => f, StringComparer.Ordinal),
            "every source is served");
        warm.Metrics.TotalFiles.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // Soundness roster (the plan's Defect Class rows 1-11)
    // -----------------------------------------------------------------------

    /// <summary>Every roster row; the census below maps each input target onto one of these.</summary>
    public static readonly string[] RosterRowNames =
    {
        "compiler_version",     // 1
        "facts",                // 2
        "source_added",         // 3
        "source_removed",       // 3
        "source_renamed",       // 3
        "source_edited",        // 4
        "assembly_name",        // 5
        "runtime_line",         // 5
        "dll_deleted",          // 6
        "dll_touched",          // 6
        "warn_as_error",        // 7
        "nowarn_lifted",        // 7
        "max_errors",           // 7
        "consumer_csharp",      // 8
        "liveness",             // 9
        "pre_w16_cache",        // 10
        "symbols_deleted",      // 11
        "symbols_truncated",    // 11
    };

    public static IEnumerable<object[]> RosterRows => RosterRowNames.Select(r => new object[] { r });

    /// <summary>
    /// One input moved alone. Rows 1-6, 8's C# leg, 9's first leg, 10's first leg and 11: the next build
    /// is NOT up to date and equals a cold build of the moved inputs. Row 7: the build IS up to date and
    /// its replay equals cold under the live policy. Row 9's and 10's last legs: up to date again.
    /// </summary>
    [Theory]
    [MemberData(nameof(RosterRows))]
    public void SoundnessRoster_OneInputMoved(string row)
    {
        var area = "row_" + row;
        var lib = Write(area, "lib.spy", LibSource);
        var main = Write(area, "main.spy", MainSource);
        var other = Write(area, "other.spy", OtherSource);
        var sources = new List<string> { lib, main, other };
        var fixedOutput = Path.Combine(Dir(area), "out", "fixed.dll");
        var baseline = row == "assembly_name"
            ? Config(area, sources, assemblyName: "First", outputOverride: fixedOutput)
            : Config(area, sources);
        var baseNowarn = row == "nowarn_lifted" ? new[] { "SPY0451" } : null;

        var first = Build(baseline, nowarn: baseNowarn);
        first.Success.Should().BeTrue($"[{row}] the specimen must compile. Diagnostics:\n" + Diagnostics(first));
        UpToDate(first).Should().BeFalse($"[{row}] control: the first build has no record");

        // F2: the unchanged twin, same builder and specimen, IS up to date — so every "not up to date"
        // below is the moved input's doing, not a check that never fires.
        var twin = Build(baseline, nowarn: baseNowarn);
        UpToDate(twin).Should().BeTrue($"[{row}] F2 control: the unchanged twin is up to date");
        Diagnostics(twin).Should().Be(Diagnostics(first), $"[{row}] the twin replays the first build");

        switch (row)
        {
            case "compiler_version":
                DoctorCache(area, root => root["CompilerVersion"] = "0.0.0-doctored");
                AssertRebuiltAsCold(row, baseline);
                break;

            case "facts":
                AssertRebuiltAsCold(row, Config(area, sources, features: new List<string> { "opt_const_fold" }));
                break;

            case "source_added":
                AssertRebuiltAsCold(row, Config(area, sources.Append(Write(area, "added.spy", OtherSource))));
                break;

            case "source_removed":
                File.Delete(other);
                AssertRebuiltAsCold(row, Config(area, new[] { lib, main }));
                break;

            case "source_renamed":
                var renamed = Path.Combine(Dir(area), "renamed.spy");
                File.Move(other, renamed);
                AssertRebuiltAsCold(row, Config(area, new[] { lib, main, renamed }));
                break;

            case "source_edited":
                File.WriteAllText(main, MainSource + "    print(\"edited\")\n");
                var edited = AssertRebuiltAsCold(row, baseline);
                string.Concat(edited.GeneratedCSharpFiles.Values).Should().Contain("edited",
                    $"[{row}] the rebuilt C# carries the edit; a replay would serve the old C#");
                break;

            case "assembly_name":
                AssertRebuiltAsCold(row,
                    Config(area, sources, assemblyName: "Second", outputOverride: fixedOutput));
                break;

            case "runtime_line":
                DoctorCache(area, root =>
                {
                    var record = root["LastBuild"]!.AsObject();
                    var environment = record["Environment"]!.GetValue<string>();
                    environment.Should().Contain("runtime=");
                    record["Environment"] = string.Join("\n", environment.Split('\n')
                        .Select(l => l.StartsWith("runtime=", StringComparison.Ordinal) ? "runtime=0.0.0" : l));
                });
                AssertRebuiltAsCold(row, baseline);
                break;

            case "dll_deleted":
                File.Delete(first.OutputAssemblyPath!);
                AssertRebuiltAsCold(row, baseline);
                File.Exists(first.OutputAssemblyPath!).Should().BeTrue($"[{row}] the rebuild writes the dll again");
                break;

            case "dll_touched":
                File.SetLastWriteTimeUtc(first.OutputAssemblyPath!, DateTime.UtcNow.AddMinutes(5));
                AssertRebuiltAsCold(row, baseline);
                break;

            case "warn_as_error":
                {
                    var replay = Build(baseline, warnAsError: true);
                    UpToDate(replay).Should().BeTrue($"[{row}] policy is not an input: the build is up to date");
                    replay.Success.Should().BeFalse($"[{row}] the replayed warnings fail under --warn-as-error");
                    replay.OutputAssemblyPath.Should().BeNull($"[{row}] a failed build names no output (#2028)");
                    var cold = Cold(baseline, warnAsError: true);
                    cold.Success.Should().BeFalse($"[{row}] control: cold fails under the same policy");
                    Diagnostics(replay).Should().Be(Diagnostics(cold), $"[{row}] the replay is cold under the live policy");
                    break;
                }

            case "nowarn_lifted":
                {
                    Diagnostics(twin).Should().NotContain("SPY0451", $"[{row}] control: --nowarn hides the warning");
                    var replay = Build(baseline);
                    UpToDate(replay).Should().BeTrue($"[{row}] policy is not an input: the build is up to date");
                    var cold = Cold(baseline);
                    Diagnostics(cold).Should().Contain("SPY0451", $"[{row}] control: cold without --nowarn warns");
                    Diagnostics(replay).Should().Be(Diagnostics(cold),
                        $"[{row}] the record is policy-free, so the lifted warning reappears");
                    replay.Success.Should().Be(cold.Success);
                    break;
                }

            case "max_errors":
                {
                    var replay = Build(baseline, warnAsError: true, maxErrors: 1);
                    UpToDate(replay).Should().BeTrue($"[{row}] policy is not an input: the build is up to date");
                    var cold = Cold(baseline, warnAsError: true, maxErrors: 1);
                    Diagnostics(cold).Should().Contain("SPY0905", $"[{row}] control: cold caps at one error");
                    Diagnostics(replay).Should().Be(Diagnostics(cold), $"[{row}] the replay is capped as cold is");
                    replay.Diagnostics.ErrorCount.Should().Be(1);
                    break;
                }

            case "consumer_csharp":
                {
                    var cold = Cold(baseline);
                    Generated(twin).Should().NotBeEmpty($"[{row}] the replay serves C#");
                    Generated(twin).Should().Equal(Generated(cold),
                        $"[{row}] --emit-cs-to / --emit-csharp on an up-to-date build write what cold writes");
                    twin.OutputAssemblyPath.Should().Be(cold.OutputAssemblyPath,
                        $"[{row}] F4: --emit-csharp writes beside the output path, as cold's");
                    break;
                }

            case "liveness":
                {
                    File.Delete(other);
                    var removed = Config(area, new[] { lib, main });
                    var rebuilt = AssertRebuiltAsCold(row, removed, coldLast: false);
                    rebuilt.Success.Should().BeTrue();
                    var again = Build(removed);
                    UpToDate(again).Should().BeTrue(
                        $"[{row}] the removed file's stale hash is inert: the next no-change build is up to date");
                    Diagnostics(again).Should().Be(Diagnostics(rebuilt));
                    break;
                }

            case "pre_w16_cache":
                {
                    DoctorCache(area, root => root.Remove("LastBuild").Should().BeTrue($"[{row}] the record exists"));
                    var log = new InfoCapturingLogger();
                    var perUnit = Build(baseline, logger: log);
                    UpToDate(perUnit).Should().BeFalse($"[{row}] a cache without the record is not up to date");
                    perUnit.Success.Should().BeTrue();
                    log.InfoMessages.Should().Contain(EmissionSkipped,
                        $"[{row}] the per-unit path replays Phase 7 (W1.3), the record's second write site");
                    var again = Build(baseline);
                    UpToDate(again).Should().BeTrue($"[{row}] the replay-path save wrote the record");
                    Diagnostics(again).Should().Be(Diagnostics(perUnit),
                        $"[{row}] the replay equals the build that wrote the record");
                    break;
                }

            case "symbols_deleted":
                File.Delete(SymbolsPath(area));
                AssertRebuiltAsCold(row, baseline);
                break;

            case "symbols_truncated":
                File.WriteAllText(SymbolsPath(area), string.Empty);
                AssertRebuiltAsCold(row, baseline);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(row), row, "a roster row needs a leg");
        }
    }

    /// <summary>
    /// The moved build is not up to date, and reports and emits what a cold build of the same inputs
    /// does. The cold build runs last (it rewrites the outputs) unless <paramref name="coldLast"/> says
    /// the caller has more to build on the warm cache.
    /// </summary>
    private ProjectCompilationResult AssertRebuiltAsCold(string row, ProjectConfig moved, bool coldLast = true)
    {
        var warm = Build(moved);
        UpToDate(warm).Should().BeFalse($"[{row}] one input moved, so the build is not up to date");
        if (!coldLast)
            return warm;

        var cold = Cold(moved);
        _output.WriteLine($"[{row}] warm:\n{Diagnostics(warm)}\ncold:\n{Diagnostics(cold)}");
        warm.Success.Should().Be(cold.Success, $"[{row}]");
        Diagnostics(warm).Should().Be(Diagnostics(cold), $"[{row}] warm reports what cold reports");
        Generated(warm).Should().NotBeEmpty($"[{row}] non-vacuity");
        Generated(warm).Should().Equal(Generated(cold), $"[{row}] warm emits what cold emits");
        return warm;
    }

    // -----------------------------------------------------------------------
    // Totality census
    // -----------------------------------------------------------------------

    /// <summary>What a build input is, for the check.</summary>
    public enum Input
    {
        CompilerVersion,
        Facts,
        SourceSet,
        Content,
        Environment,
        Outputs,
        SymbolCache,
        PolicyAtReplay,
        NotAnInput,
    }

    /// <summary>The roster row that moves each input target alone.</summary>
    private static readonly IReadOnlyDictionary<Input, string> RowOf = new Dictionary<Input, string>
    {
        [Input.CompilerVersion] = "compiler_version",
        [Input.Facts] = "facts",
        [Input.SourceSet] = "source_removed",
        [Input.Content] = "source_edited",
        [Input.Environment] = "assembly_name",
        [Input.Outputs] = "dll_deleted",
        [Input.SymbolCache] = "symbols_deleted",
        [Input.PolicyAtReplay] = "warn_as_error",
    };

    private const string SyntheticOnly =
        "set only by the synthetic project-of-one-file, the REPL, the formatter and the LSP overlay, "
        + "which build with incremental:false, so never on a checked build";

    /// <summary>
    /// (a) every property of the configuration and option types, (b) every <c>key=</c> line of the facts
    /// and the emission key plus the output identity, (c) every property of the cache records — each to
    /// exactly one input target. No default arm: an entry missing here fails the census.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (Input Target, string Why)> Census =
        new Dictionary<string, (Input, string)>(StringComparer.Ordinal)
        {
            // (a) ProjectConfig
            ["ProjectConfig.ProjectFilePath"] = (Input.NotAnInput, "the compiler never reads it; the .spyproj reaches the build as the properties below"),
            ["ProjectConfig.ProjectDirectory"] = (Input.Facts, "source-root= (and the cache's own location under obj/)"),
            ["ProjectConfig.RootNamespace"] = (Input.Facts, "root-namespace="),
            ["ProjectConfig.OutputType"] = (Input.Facts, "output="),
            ["ProjectConfig.TargetFramework"] = (Input.Facts, "target-framework="),
            ["ProjectConfig.AssemblyName"] = (Input.Environment, "assembly-name="),
            ["ProjectConfig.EntryPoint"] = (Input.Facts, "entry="),
            ["ProjectConfig.SourceFiles"] = (Input.SourceSet, "UpToDateRecord.SourceSet"),
            ["ProjectConfig.SourceRoot"] = (Input.Facts, "source-root="),
            ["ProjectConfig.InMemorySources"] = (Input.NotAnInput, SyntheticOnly),
            ["ProjectConfig.PreParsedUnits"] = (Input.NotAnInput, SyntheticOnly),
            ["ProjectConfig.PreserveTrivia"] = (Input.NotAnInput, SyntheticOnly),
            ["ProjectConfig.NullifyEntryFilePath"] = (Input.NotAnInput, SyntheticOnly),
            ["ProjectConfig.EmitLineDirectives"] = (Input.Facts, "line-directives="),
            ["ProjectConfig.References"] = (Input.Facts, "reference="),
            ["ProjectConfig.ModulePaths"] = (Input.Facts, "module-path=, module-path-assembly="),
            ["ProjectConfig.PackageReferences"] = (Input.Facts, "package=, package-assembly="),
            ["ProjectConfig.ResolvedVersions"] = (Input.Facts, "package-assembly= is resolved through it"),
            ["ProjectConfig.Configuration"] = (Input.Facts, "configuration="),
            ["ProjectConfig.WarningsAsErrors"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["ProjectConfig.TestHost"] = (Input.Facts, "test-host="),
            ["ProjectConfig.SuppressedWarnings"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["ProjectConfig.Features"] = (Input.Facts, "features="),
            ["ProjectConfig.OutputPath"] = (Input.Environment, "output-path= (OutputAssemblyPath derives from it)"),
            ["ProjectConfig.OutputAssemblyPathOverride"] = (Input.Environment, "output-path="),
            ["ProjectConfig.OutputAssemblyPath"] = (Input.Environment, "output-path="),
            ["ProjectConfig.CrashRoot"] = (Input.NotAnInput, "where a crash bundle goes; a crashed build is not a success and writes no record"),
            ["ProjectConfig.UsePrecomputedCodeGenInfo"] = (Input.NotAnInput, "no product route sets it false; false fails code generation, which writes no record"),

            // (a) CompilerOptions
            ["CompilerOptions.ModulePaths"] = (Input.Facts, "every CLI route adds them to the config first (module-path=)"),
            ["CompilerOptions.References"] = (Input.Facts, "every CLI route adds them to the config first (reference=)"),
            ["CompilerOptions.WarningsAsErrors"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["CompilerOptions.TargetsTestHost"] = (Input.Facts, "ForProject copies config.TestHost (test-host=)"),
            ["CompilerOptions.SuppressedWarnings"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["CompilerOptions.MaxErrors"] = (Input.PolicyAtReplay, "the replay's project sink's error cap"),
            ["CompilerOptions.Incremental"] = (Input.NotAnInput, "selects the cache the check reads; off, there is no check"),
            ["CompilerOptions.UpToDateCheck"] = (Input.NotAnInput, "selects the check itself"),
            ["CompilerOptions.OutputType"] = (Input.NotAnInput, "project mode reads config.OutputType; ForProject leaves the default"),
            ["CompilerOptions.Namespace"] = (Input.NotAnInput, "emit csharp --namespace only; project mode never reads it"),
            ["CompilerOptions.Features"] = (Input.Facts, "the effective set is features="),
            ["CompilerOptions.Configuration"] = (Input.NotAnInput, "project mode reads config.Configuration; ForProject leaves the default"),
            ["CompilerOptions.AssemblyName"] = (Input.NotAnInput, "project mode reads config.AssemblyName; ForProject leaves the default"),
            ["CompilerOptions.OutputAssemblyPath"] = (Input.NotAnInput, "project mode reads config.OutputAssemblyPath; ForProject leaves the default"),

            // (a) ProjectCompilerOptions
            ["ProjectCompilerOptions.WarningsAsErrors"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["ProjectCompilerOptions.SuppressedWarnings"] = (Input.PolicyAtReplay, "the replay's project sink"),
            ["ProjectCompilerOptions.MaxErrors"] = (Input.PolicyAtReplay, "the replay's project sink's error cap"),
            ["ProjectCompilerOptions.Incremental"] = (Input.NotAnInput, "selects the cache the check reads"),
            ["ProjectCompilerOptions.UpToDateCheck"] = (Input.NotAnInput, "selects the check itself"),
            ["ProjectCompilerOptions.Features"] = (Input.Facts, "features="),
            ["ProjectCompilerOptions.SharedBuiltinRegistry"] = (Input.NotAnInput, "the analyze paths' shared registry; a checked build never passes one"),

            // (b) key lines and the output identity
            ["key:entry"] = (Input.Facts, "ProjectFactsOf"),
            ["key:output"] = (Input.Facts, "ProjectFactsOf"),
            ["key:root-namespace"] = (Input.Facts, "ProjectFactsOf"),
            ["key:source-root"] = (Input.Facts, "ProjectFactsOf"),
            ["key:features"] = (Input.Facts, "ProjectFactsOf"),
            ["key:target-framework"] = (Input.Facts, "ProjectFactsOf"),
            ["key:configuration"] = (Input.Facts, "ProjectFactsOf"),
            ["key:test-host"] = (Input.Facts, "ProjectFactsOf"),
            ["key:line-directives"] = (Input.Facts, "ProjectFactsOf"),
            ["key:reference"] = (Input.Facts, "ProjectFactsOf"),
            ["key:module-path"] = (Input.Facts, "ProjectFactsOf"),
            ["key:module-path-assembly"] = (Input.Facts, "ProjectFactsOf"),
            ["key:package"] = (Input.Facts, "ProjectFactsOf"),
            ["key:package-assembly"] = (Input.Facts, "ProjectFactsOf"),
            ["key:csharp"] = (Input.Content, "the C# is a function of the sources' content, the facts and the compiler"),
            ["key:entry-type"] = (Input.Facts, "the output type (facts) over the source names (source set) and the entry unit's layout (content)"),
            ["key:assembly-name"] = (Input.Environment, "EnvironmentOf"),
            ["key:output-path"] = (Input.Environment, "EnvironmentOf"),
            ["key:runtime"] = (Input.Environment, "EnvironmentOf"),
            ["key:corlib"] = (Input.Environment, "EnvironmentOf"),
            ["key:sharpy-core"] = (Input.Environment, "EnvironmentOf"),
            ["outputs"] = (Input.Outputs, "OutputIdentityOf"),

            // (c) CacheMetadata
            ["CacheMetadata.CompilerVersion"] = (Input.CompilerVersion, "the constructor drops the record on a mismatch"),
            ["CacheMetadata.FileHashes"] = (Input.NotAnInput, "read by the per-unit path only; the check re-hashes through HasValidFileCache, so stale keys are inert (liveness row)"),
            ["CacheMetadata.ProjectFacts"] = (Input.Facts, "the constructor drops the record on a mismatch"),
            ["CacheMetadata.EmittedAssembly"] = (Input.Outputs, "its Outputs are compared"),
            ["CacheMetadata.LastBuild"] = (Input.NotAnInput, "the record itself; its members are mapped under UpToDateRecord"),
            ["CacheMetadata.EqualityContract"] = (Input.NotAnInput, "compiler-generated record member"),

            // (c) EmittedAssemblyRecord
            ["EmittedAssemblyRecord.EmissionKey"] = (Input.NotAnInput, "Phase 7's key; the check compares its environment lines through LastBuild.Environment and the C# through content"),
            ["EmittedAssemblyRecord.Outputs"] = (Input.Outputs, "compared to OutputIdentityOf"),
            ["EmittedAssemblyRecord.Diagnostics"] = (Input.NotAnInput, "Phase 7's slice of the report; LastBuild.Diagnostics carries the whole bag"),
            ["EmittedAssemblyRecord.EqualityContract"] = (Input.NotAnInput, "compiler-generated record member"),

            // (c) UpToDateRecord
            ["UpToDateRecord.SourceSet"] = (Input.SourceSet, "compared to SourceSetOf"),
            ["UpToDateRecord.Environment"] = (Input.Environment, "compared to EnvironmentOf"),
            ["UpToDateRecord.Diagnostics"] = (Input.PolicyAtReplay, "replayed through the live policy"),
            ["UpToDateRecord.EqualityContract"] = (Input.NotAnInput, "compiler-generated record member"),

            // (c) SymbolCacheEnvelope
            ["SymbolCacheEnvelope.SchemaVersion"] = (Input.SymbolCache, "a mismatch loads no entries, so no source has a valid entry"),
            ["SymbolCacheEnvelope.Files"] = (Input.SymbolCache, "HasValidFileCache per source"),
            ["SymbolCacheEnvelope.EqualityContract"] = (Input.NotAnInput, "compiler-generated record member"),

            // (c) FileCacheEntry
            ["FileCacheEntry.ContentHash"] = (Input.Content, "HasValidFileCache re-hashes the source against it"),
            ["FileCacheEntry.Symbols"] = (Input.NotAnInput, "restored by the per-unit path only; the replay reads no symbol"),
            ["FileCacheEntry.GeneratedCSharp"] = (Input.SymbolCache, "served as the replay's C#"),
            ["FileCacheEntry.Dependencies"] = (Input.NotAnInput, "per-unit transitive staleness; with every source's content unchanged no dependent is stale"),
            ["FileCacheEntry.ModulePath"] = (Input.NotAnInput, "per-unit restore only"),
            ["FileCacheEntry.GeneratorOutputs"] = (Input.NotAnInput, "a generator is a source (content) or a referenced assembly (facts)"),
            ["FileCacheEntry.Diagnostics"] = (Input.NotAnInput, "per-unit replay; LastBuild.Diagnostics carries the whole bag"),
            ["FileCacheEntry.LayoutNamespaceSegments"] = (Input.NotAnInput, "per-unit entry type; the layout is a function of the facts and the source set"),
            ["FileCacheEntry.LayoutMembersClassName"] = (Input.NotAnInput, "per-unit entry type; the layout is a function of the facts and the source set"),
            ["FileCacheEntry.ReferencedClrTypeNames"] = (Input.NotAnInput, "the SPY0615 check over newly compiled units; an up-to-date build compiles none"),
            ["FileCacheEntry.EqualityContract"] = (Input.NotAnInput, "compiler-generated record member"),
        };

    private static readonly Type[] CensusTypes =
    {
        typeof(ProjectConfig), typeof(CompilerOptions), typeof(ProjectCompilerOptions),
        typeof(CacheMetadata), typeof(EmittedAssemblyRecord), typeof(UpToDateRecord),
        typeof(SymbolCacheEnvelope), typeof(FileCacheEntry),
    };

    private static IEnumerable<string> PropertiesOf(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(p => $"{type.Name}.{p.Name}");

    /// <summary>
    /// The census of the key lines: <c>ProjectFactsOf</c> on a specimen with a reference, a module path
    /// holding an assembly and a restored package, and <c>EmissionKeyOf</c>, plus the output identity.
    /// </summary>
    private IEnumerable<string> KeyLines()
    {
        var area = "census";
        var reference = Write(area, "Dep.dll", "v1");
        var moduleDir = Path.Combine(Dir(area), "modules");
        Write(Path.Combine(area, "modules"), "Mod.dll", "v1");
        var source = Write(area, "app.spy", "def main() -> None:\n    print(1)\n");

        // A package this machine has restored: the test project's own FluentAssertions, so
        // package-assembly= appears (a key that appears only conditionally is caught by the control).
        var version = typeof(AssertionExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var package = new PackageRef("FluentAssertions", version);
        NuGetResolver.ResolvePackage(package, "net10.0").Should().NotBeEmpty(
            "the census specimen needs a restored package so package-assembly= is enumerated");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(Dir(area), "c.spyproj"),
            ProjectDirectory = Dir(area),
            RootNamespace = "Census",
            SourceFiles = new List<string> { source },
            References = new List<string> { reference },
            ModulePaths = new List<string> { moduleDir },
            PackageReferences = new List<PackageRef> { package },
        };

        static IEnumerable<string> Keys(string lines)
            => lines.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => "key:" + l.Split('=', 2)[0]);

        return Keys(IncrementalCompilationCache.ProjectFactsOf(config))
            .Concat(Keys(IncrementalCompilationCache.EmissionKeyOf(
                config, new Dictionary<string, string> { ["app.cs"] = "class C {}" }, "Census.App")))
            .Append("outputs")
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// Totality, closed against enumerations rather than a hand list: every enumerated input maps to
    /// exactly one target (a dictionary key cannot map twice), every target that is an input has a
    /// roster row that moves it, and every map entry names an existing property or key — the
    /// bidirectional positive control, which also fails a reflection or key scan that found nothing.
    /// </summary>
    [Fact]
    public void Census_EveryInputMapsToOneTargetWithARosterRow()
    {
        var enumerated = CensusTypes.SelectMany(PropertiesOf).Concat(KeyLines()).ToList();

        var unmapped = enumerated.Where(name => !Census.ContainsKey(name)).ToList();
        unmapped.Should().BeEmpty(
            "every input of a build must be mapped to what the up-to-date check compares it through, or "
            + "to not-an-input with the reason; an unmapped one is an input the check may ignore:\n"
            + string.Join("\n", unmapped));

        var stale = Census.Keys.Where(name => !enumerated.Contains(name, StringComparer.Ordinal)).ToList();
        stale.Should().BeEmpty("positive control: every map entry names an existing property or key line:\n"
            + string.Join("\n", stale));

        foreach (var (name, (target, why)) in Census)
        {
            why.Should().NotBeNullOrWhiteSpace($"{name} says how the check sees it");
            if (target == Input.NotAnInput)
                continue;
            RowOf.Should().ContainKey(target, $"{name}'s target {target} needs a roster row");
            RosterRowNames.Should().Contain(RowOf[target], $"{target}'s roster row must exist");
        }

        foreach (var type in CensusTypes)
            PropertiesOf(type).Should().NotBeEmpty($"the reflection over {type.Name} must see its members");
    }
}
