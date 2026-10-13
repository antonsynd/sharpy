using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Utilities;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class ProjectAnalysisResultTests
{
    private static ProjectConfig CreateConfig() => new()
    {
        ProjectFilePath = "/test/project.spyproj",
        ProjectDirectory = "/test",
        RootNamespace = "Test"
    };

    private static ProjectModel CreateModelWithFile(string filePath, string source, CompilationPhase phase)
    {
        var model = new ProjectModel(CreateConfig());
        var unit = model.CreateUnit(filePath, "test", source);
        unit.Phase = phase;
        return model;
    }

    [Fact]
    public void GetFileResult_ExistingFile_ReturnsResult()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.True(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_UnknownFile_ReturnsNull()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/unknown.spy");

        Assert.Null(fileResult);
    }

    [Fact]
    public void Dependencies_WhenDependencyGraphProvided_ReturnsQuery()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();

        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["/test/main.spy"] = ImmutableHashSet<string>.Empty
        };
        var graph = new DependencyGraph(deps);

        var result = new ProjectAnalysisResult(true, model, graph, diagnostics);

        Assert.NotNull(result.Dependencies);
        Assert.Contains(result.Dependencies.AllFiles, f => f.Contains("main.spy"));
    }

    [Fact]
    public void Dependencies_WhenNoDependencyGraph_ReturnsNull()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        Assert.Null(result.Dependencies);
    }

    [Fact]
    public void GetFileResult_FileNotTypeChecked_ReturnsNotSuccess()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.Parsed);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_TypeCheckedWithAccumulatedErrors_ReportsNotSuccess()
    {
        // Since #1360 a parse-errored unit can still advance to TypeChecked when the type
        // checker itself adds nothing, so Success must consult the unit's accumulated
        // diagnostics, not the phase alone.
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        model.GetUnit("/test/main.spy")!.Diagnostics.AddError("parse error survived to TypeChecked", 1, 1);
        var result = new ProjectAnalysisResult(true, model, null, new DiagnosticBag());

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_ParseErroredFileThatTypechecksCleanly_FailsWithArtifactsPresent()
    {
        // End-to-end positive control for the phase-vs-diagnostics divergence: the trailing
        // dot is a parse error (SPY0101) the type checker adds nothing to, so the unit
        // reaches TypeChecked while carrying an error — Success must be false, and the
        // partial-analysis artifacts the LSP serves completion from must still be present.
        const string brokenSource =
            "class Greeter:\n"
            + "    name: str = \"world\"\n"
            + "\n"
            + "def use() -> None:\n"
            + "    g: Greeter = Greeter()\n"
            + "    g.\n";

        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("ParseErrDelta")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(\"ok\")\n");
        helper.AddSourceFile("broken.spy", brokenSource);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "ParseErrDelta.spyproj"));
        var result = new CompilerApi().AnalyzeProject(config);

        var brokenPath = config.SourceFiles.Single(f => f.EndsWith("broken.spy"));
        var unit = result.ProjectModel.GetUnit(brokenPath);
        Assert.NotNull(unit);
        Assert.Equal(CompilationPhase.TypeChecked, unit.Phase);
        Assert.Contains(unit.Diagnostics.GetAll(), d => d.Code == DiagnosticCodes.Parser.ExpectedIdentifier);

        var fileResult = result.GetFileResult(brokenPath);
        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
        Assert.Contains(fileResult.Diagnostics, d => d.Code == DiagnosticCodes.Parser.ExpectedIdentifier);
        Assert.NotNull(fileResult.Ast);
        Assert.NotNull(fileResult.SymbolTable);
    }

    [Fact]
    public void Success_ReflectsConstructorArgument()
    {
        var model = new ProjectModel(CreateConfig());
        var diagnostics = new DiagnosticBag();

        var successResult = new ProjectAnalysisResult(true, model, null, diagnostics);
        var failResult = new ProjectAnalysisResult(false, model, null, diagnostics);

        Assert.True(successResult.Success);
        Assert.False(failResult.Success);
    }

    [Fact]
    public void Diagnostics_ReturnsBagFromConstructor()
    {
        var model = new ProjectModel(CreateConfig());
        var diagnostics = new DiagnosticBag();
        diagnostics.AddError("test error", 1, 1);

        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        Assert.Same(diagnostics, result.Diagnostics);
        Assert.True(result.Diagnostics.HasErrors);
    }

    // ---------------------------------------------------------------------------------------
    // #2242 (P53, D1): a file's result is the project bag projected onto that file. Phase × file
    // matrix: every phase that reports, in the entry file and in a listed non-entry file. Until
    // #2242 GetFileResult returned the unit's own bag, and the import, declaration, inheritance,
    // cycle and collision phases write only the project bag — their errors reached `sharpyc
    // project` and never the editor. The type-check and parse rows write BOTH bags: they are the
    // positive control that the projection is the union (green with the union removed) and the
    // dedupe witness (red with count 2 when dedupe is removed).
    // ---------------------------------------------------------------------------------------

    public enum ReportingPhase
    {
        ImportMissing,
        ImportUnlisted,
        Declaration,
        Inheritance,
        CircularImport,
        PackageCollision,
        TypeCheck,
        Parse,
    }

    public enum Site { Entry, NonEntry }

    private const string CleanMain = "import mod\n\ndef main() -> None:\n    print(mod.f())\n";
    private const string CleanMod = "def f() -> int:\n    return 1\n";

    /// <summary>
    /// One cell: the project's files (relative to <c>src/</c>), its source glob, the file the
    /// diagnostic is stamped with, and the code the build emits there (measured with
    /// <c>sharpyc project</c> @ 8be4576fd, not guessed).
    /// </summary>
    private sealed record PhaseCell(
        IReadOnlyDictionary<string, string> Files, string Pattern, string StampedFile, string Code);

    private static PhaseCell CellFor(ReportingPhase phase, Site site)
    {
        var entry = site == Site.Entry;
        var stamped = entry ? "main.spy" : "mod.spy";
        Dictionary<string, string> Files(string? main = null, string? mod = null, params (string, string)[] more)
        {
            var files = new Dictionary<string, string> { ["main.spy"] = main ?? CleanMain, ["mod.spy"] = mod ?? CleanMod };
            foreach (var (path, text) in more)
                files[path] = text;
            return files;
        }

        return phase switch
        {
            // SPY0300 at the import (plus the unused-import warning SPY0452 on the same line).
            ReportingPhase.ImportMissing => new PhaseCell(entry
                    ? Files(main: "import nosuch\nimport mod\n\ndef main() -> None:\n    print(mod.f())\n")
                    : Files(mod: "import nosuch\n\ndef f() -> int:\n    return 1\n"),
                "src/*.spy", stamped, DiagnosticCodes.Semantic.ModuleNotFound),
            // SPY0313: `lib.spy` sits beside the importer but the glob lists only m*.spy.
            ReportingPhase.ImportUnlisted => new PhaseCell(entry
                    ? Files(main: "import lib\nimport mod\n\ndef main() -> None:\n    print(mod.f() + lib.g())\n",
                        more: new[] { ("lib.spy", "def g() -> int:\n    return 2\n") })
                    : Files(mod: "import lib\n\ndef f() -> int:\n    return lib.g()\n",
                        more: new[] { ("lib.spy", "def g() -> int:\n    return 2\n") }),
                "src/m*.spy", stamped, DiagnosticCodes.Semantic.ImportOutsideCompilation),
            // SPY0204: a duplicate class, Phase 3 declarations.
            ReportingPhase.Declaration => new PhaseCell(entry
                    ? Files(main: "import mod\n\nclass W:\n    pass\n\nclass W:\n    pass\n\ndef main() -> None:\n    print(mod.f())\n")
                    : Files(mod: "class W:\n    pass\n\nclass W:\n    pass\n\ndef f() -> int:\n    return 1\n"),
                "src/*.spy", stamped, DiagnosticCodes.Semantic.DuplicateDefinition),
            // SPY0288 twice (A -> B -> A at A, B -> A -> B at B), Phase 4b inheritance.
            ReportingPhase.Inheritance => new PhaseCell(entry
                    ? Files(main: "import mod\n\nclass A(B):\n    pass\n\nclass B(A):\n    pass\n\ndef main() -> None:\n    print(mod.f())\n")
                    : Files(mod: "class A(B):\n    pass\n\nclass B(A):\n    pass\n\ndef f() -> int:\n    return 1\n"),
                "src/*.spy", stamped, DiagnosticCodes.Semantic.CircularInheritance),
            // SPY0302 on the first file of the cycle — main.spy ⇄ mod.spy, or mod.spy ⇄ other.spy.
            // Its FilePath is the NORMALIZED (lower-cased on macOS) path, not the source's spelling.
            ReportingPhase.CircularImport => new PhaseCell(entry
                    ? Files(mod: "import main\n\ndef f() -> int:\n    return 1\n")
                    : Files(mod: "import other\n\ndef f() -> int:\n    return 1\n",
                        more: new[] { ("other.spy", "import mod\n\ndef h() -> int:\n    return 1\n") }),
                "src/*.spy", stamped, DiagnosticCodes.Semantic.CircularImport),
            // SPY0526 on the module file whose package directory beside it emits the same name.
            ReportingPhase.PackageCollision => entry
                ? new PhaseCell(Files(more: new[] { ("main/__init__.spy", ""), ("main/x.spy", "def x() -> int:\n    return 1\n") }),
                    "src/**/*.spy", "main.spy", DiagnosticCodes.CodeGen.PackageModuleNameCollision)
                : new PhaseCell(Files(more: new[] { ("util.spy", "def u() -> int:\n    return 1\n"), ("util/__init__.spy", ""), ("util/x.spy", "def x() -> int:\n    return 1\n") }),
                    "src/**/*.spy", "util.spy", DiagnosticCodes.CodeGen.PackageModuleNameCollision),
            // Control, both bags: SPY0220 (entry), SPY0260 (non-entry).
            ReportingPhase.TypeCheck => entry
                ? new PhaseCell(Files(main: "import mod\n\ndef main() -> None:\n    x: int = \"s\"\n    print(mod.f())\n"),
                    "src/*.spy", stamped, DiagnosticCodes.Semantic.TypeMismatch)
                : new PhaseCell(Files(mod: "def f() -> int:\n    return \"s\"\n"),
                    "src/*.spy", stamped, DiagnosticCodes.Semantic.MissingReturnValue),
            // Control, both bags: SPY0104.
            ReportingPhase.Parse => new PhaseCell(entry
                    ? Files(main: "import mod\n\ndef main() -> None:\n    print(mod.f()\n")
                    : Files(mod: "def f() -> int:\n    return (1\n"),
                "src/*.spy", stamped, DiagnosticCodes.Parser.ExpectedToken),
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };
    }

    public static IEnumerable<object[]> PhaseFileCells()
    {
        foreach (var phase in Enum.GetValues<ReportingPhase>())
            foreach (var site in Enum.GetValues<Site>())
                yield return new object[] { phase, site };
    }

    private static (ProjectConfig Config, ProjectAnalysisResult Result) Analyze(
        ProjectCompilationHelper helper, IReadOnlyDictionary<string, string> files, string pattern,
        string? propertyGroupExtra = null, CompilerApi? api = null, CancellationToken ct = default)
    {
        helper.Options.SourceFilePattern = pattern;
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(helper.SourceDirectory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        helper.CreateProjectFile();
        var projectFile = Path.Combine(helper.ProjectDirectory, $"{helper.Options.RootNamespace}.spyproj");
        if (propertyGroupExtra != null)
        {
            File.WriteAllText(projectFile, File.ReadAllText(projectFile)
                .Replace("</PropertyGroup>", propertyGroupExtra + "\n  </PropertyGroup>"));
        }

        var config = ProjectFileParser.Load(projectFile);
        return (config, (api ?? new CompilerApi()).AnalyzeProject(config, cancellationToken: ct));
    }

    /// <summary>The bag's dedupe key (DiagnosticBag.GetDeduplicationKey), path normalized.</summary>
    private static (string?, int?, int?, string?, int?, int?, string?) Key(CompilerDiagnostic d)
        => (d.Code, d.Line, d.Column, string.IsNullOrEmpty(d.Code) ? d.Message : null,
            d.Span?.Start, d.Span?.Length,
            string.IsNullOrEmpty(d.FilePath) ? null : PathNormalizer.Normalize(d.FilePath));

    [Theory]
    [MemberData(nameof(PhaseFileCells))]
    public void GetFileResult_CarriesEveryProjectDiagnosticStampedWithTheFile_Once(ReportingPhase phase, Site site)
    {
        var cell = CellFor(phase, site);
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("PhaseFile")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        var (config, result) = Analyze(helper, cell.Files, cell.Pattern);
        var label = $"[{phase}/{site}]";

        var stampedPath = PathNormalizer.Normalize(Path.Combine(helper.SourceDirectory, cell.StampedFile));
        var sources = config.SourceFiles.Select(PathNormalizer.Normalize).ToList();
        Assert.Contains(stampedPath, sources);

        // The measured cell: the build reports the code, and only on the stamped file.
        var all = result.Diagnostics.GetAll();
        var measured = all.Where(d => d.Code == cell.Code).ToList();
        Assert.True(measured.Count > 0, $"{label} the build reports {cell.Code}: {Dump(all)}");
        Assert.All(measured, d => Assert.Equal(stampedPath, PathNormalizer.Normalize(d.FilePath ?? "")));

        // Every project-bag diagnostic belongs to some project file (or is unattributed): a bag
        // entry in no file's projection is a missing entry, not a filtered one.
        Assert.All(all, d => Assert.True(
            string.IsNullOrEmpty(d.FilePath) || sources.Contains(PathNormalizer.Normalize(d.FilePath)),
            $"{label} {d.Code} stamped with '{d.FilePath}', which is no source of the project"));

        foreach (var source in config.SourceFiles)
        {
            var file = PathNormalizer.Normalize(source);
            var fileResult = result.GetFileResult(source);
            Assert.NotNull(fileResult);
            var projected = fileResult.Diagnostics;

            // Exactly once in the file it is stamped with, never in another file.
            foreach (var d in all)
            {
                var belongs = string.IsNullOrEmpty(d.FilePath)
                    ? file == result.UnattributedTarget
                    : PathNormalizer.Normalize(d.FilePath) == file;
                var copies = projected.Count(p => Key(p) == Key(d));
                Assert.True(copies == (belongs ? 1 : 0),
                    $"{label} {d.Code} @{Path.GetFileName(d.FilePath)}:{d.Line}:{d.Column} appears {copies}× in "
                    + $"{Path.GetFileName(source)}'s result (expected {(belongs ? 1 : 0)}): {Dump(projected)}");
            }

            // Nothing else: no entry the build does not print for this file.
            Assert.All(projected, p => Assert.Contains(all, d => Key(d) == Key(p)));

            var unit = result.ProjectModel.GetUnit(source)!;
            Assert.Equal(unit.Phase == CompilationPhase.TypeChecked && !projected.Any(d => d.IsError),
                fileResult.Success);
            if (file == stampedPath)
                Assert.False(fileResult.Success, $"{label} {Path.GetFileName(source)} carries {cell.Code}");
        }
    }

    /// <summary>
    /// The projection applies the project's warning policy: a unit's bag is policy-less, so its copy
    /// of an unused-import warning (SPY0452, written to both bags by the type-check phase) must be
    /// dropped under <c>&lt;NoWarn&gt;</c> and promoted under <c>&lt;WarningsAsErrors&gt;</c> exactly as
    /// the build's bag is. The no-policy row is the positive control that the warning exists.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("nowarn")]
    [InlineData("werror")]
    public void GetFileResult_AppliesTheProjectsWarningPolicy(string policy)
    {
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("WarnPolicy")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        var extra = policy switch
        {
            "nowarn" => "    <NoWarn>SPY0452</NoWarn>",
            "werror" => "    <WarningsAsErrors>true</WarningsAsErrors>",
            _ => null,
        };
        var files = new Dictionary<string, string>
        {
            ["main.spy"] = "import mod\n\ndef main() -> None:\n    print(1)\n",
            ["mod.spy"] = CleanMod,
        };
        var (config, result) = Analyze(helper, files, "src/*.spy", extra);
        var main = config.SourceFiles.Single(f => f.EndsWith("main.spy"));

        var inBuild = result.Diagnostics.GetAll().Where(d => d.Code == DiagnosticCodes.Validation.UnusedImport).ToList();
        var inFile = result.GetFileResult(main)!.Diagnostics
            .Where(d => d.Code == DiagnosticCodes.Validation.UnusedImport).ToList();

        Assert.Equal(policy == "nowarn" ? 0 : 1, inBuild.Count);
        Assert.Equal(inBuild.Count, inFile.Count);
        Assert.Equal(inBuild.Select(d => d.Severity), inFile.Select(d => d.Severity));
        Assert.Equal(policy == "werror", inFile.Any(d => d.IsError));
        Assert.Equal(policy != "werror", result.GetFileResult(main)!.Success);
    }

    // ---------------------------------------------------------------------------------------
    // D5: an unattributed (path-less) project diagnostic lands on ONE defined file — the entry
    // point when the project names one, else the first source — and on no other.
    // ---------------------------------------------------------------------------------------

    /// <summary>Cancels <paramref name="cts"/> when the analysis announces Phase 3 (after parsing).</summary>
    private sealed class CancelAtPhase3Logger(CancellationTokenSource cts) : Sharpy.Compiler.Logging.ICompilerLogger
    {
        public void LogInfo(string message)
        {
            if (message.StartsWith("Phase 3:", StringComparison.Ordinal))
                cts.Cancel();
        }

        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(Sharpy.Compiler.Logging.CompilerLogLevel level)
            => level <= Sharpy.Compiler.Logging.CompilerLogLevel.Info;
    }

    /// <summary>
    /// A cancellation after parsing records SPY0901 with no file; it is projected onto the D5 file
    /// and no other. Rows: an exe whose entry point is NOT the first source (so "entry" and
    /// "first source" disagree), an exe with no entry point whose `main` file is not the first source
    /// (the compiler's default entry), and a library with no entry point (first source).
    /// </summary>
    /// <remarks>
    /// A token cancelled BEFORE the call does not reach this: <c>ParseAllFiles</c> checks it before
    /// creating the first unit, so the bag holds SPY0901 and the model holds no unit — every
    /// <see cref="ProjectAnalysisResult.GetFileResult"/> is null (pinned below), and only
    /// <see cref="ProjectAnalysisResult.UnattributedTarget"/> names where it belongs.
    /// </remarks>
    [Theory]
    [InlineData("exe")]
    [InlineData("exe-default")]
    [InlineData("library")]
    public void UnattributedDiagnostic_LandsOnTheD5File_AndNoOther(string row)
    {
        // "exe-default": an executable with no <EntryPoint> enters at its `main` file (the
        // compiler's default), which is NOT the first source here.
        var outputType = row == "library" ? "library" : "exe";
        var entryName = row == "exe-default" ? "main.spy" : "run.spy";
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("D5Target")
            .WithOutputType(outputType);
        if (row == "exe")
            helper.WithEntryPoint("run.spy");
        var files = new Dictionary<string, string>
        {
            ["alpha.spy"] = CleanMod,
            [entryName] = "import alpha\n\ndef main() -> None:\n    print(alpha.f())\n",
        };
        using var cts = new CancellationTokenSource();
        var api = new CompilerApi(new CancelAtPhase3Logger(cts));
        var (config, result) = Analyze(helper, files, "src/*.spy", api: api, ct: cts.Token);

        var runPath = PathNormalizer.Normalize(config.SourceFiles.Single(f => f.EndsWith(entryName)));
        Assert.NotEqual(runPath, PathNormalizer.Normalize(config.SourceFiles[0]));
        var expectedTarget = row == "library" ? PathNormalizer.Normalize(config.SourceFiles[0]) : runPath;
        Assert.Equal(expectedTarget, result.UnattributedTarget);

        var cancelled = Assert.Single(result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
        Assert.True(string.IsNullOrEmpty(cancelled.FilePath), "the cancellation names no file");

        foreach (var source in config.SourceFiles)
        {
            var fileResult = result.GetFileResult(source);
            Assert.NotNull(fileResult);
            var copies = fileResult.Diagnostics.Count(d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
            var isTarget = PathNormalizer.Normalize(source) == expectedTarget;
            Assert.Equal(isTarget ? 1 : 0, copies);
            if (isTarget)
                Assert.False(fileResult.Success);
        }
    }

    [Fact]
    public void PreCancelledAnalysis_CreatesNoUnit_TheUnattributedTargetStillNamesTheEntry()
    {
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("PreCancelled")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var files = new Dictionary<string, string> { ["main.spy"] = CleanMain, ["mod.spy"] = CleanMod };
        var (config, result) = Analyze(helper, files, "src/*.spy", ct: cts.Token);

        Assert.Contains(result.Diagnostics.GetAll(), d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled
            && string.IsNullOrEmpty(d.FilePath));
        Assert.All(config.SourceFiles, f => Assert.Null(result.GetFileResult(f)));
        Assert.Equal(PathNormalizer.Normalize(config.SourceFiles.Single(f => f.EndsWith("main.spy"))),
            result.UnattributedTarget);
    }

    private static string Dump(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join("; ", diagnostics.Select(d =>
            $"{d.Code}@{Path.GetFileName(d.FilePath)}:{d.Line}:{d.Column}"));
}
