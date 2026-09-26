using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2039 (P14c, Decision 28 (e)): the recorded module layout reaches the project-level
/// <see cref="SemanticInfo"/> that code generation reads — through the per-file →
/// project <c>SemanticInfo.MergeFrom</c> — on EVERY entry path, and every path records the SAME
/// namespace segments for one module: single-file <c>run</c> (a synthetic project of the import
/// closure), <c>project</c> (a <c>.spyproj</c>), the LSP workspace analysis
/// (<c>CompilerApi.AnalyzeProject</c>) and the LSP single-document analysis (a synthetic project
/// whose entry has no path identity). Imported facts cross a warm incremental build on the cache
/// wire (<c>CachedCodeGenInfo</c>), not by re-derivation. Expectations are literals.
/// </summary>
public class ModuleLayoutFactEntryPathTests
{
    private readonly ITestOutputHelper _output;

    public ModuleLayoutFactEntryPathTests(ITestOutputHelper output) => _output = output;

    private const string MainSource =
        "from pkg.thing import Foo, helper\n\ndef main() -> None:\n    print(Foo().x + helper())\n";

    private const string ThingSource =
        "class Foo:\n    x: int = 3\n\ndef helper() -> int:\n    return 4\n";

    // unit (relative to the source root) → "segments|<X>" of its own recorded layout.
    private static readonly Dictionary<string, string> ExpectedOwnLayouts = new()
    {
        ["main.spy"] = "Main|MainModule",
        ["pkg/thing.spy"] = "Pkg.Thing|ThingModule",
    };

    private ProjectCompilationHelper CreateProject()
    {
        var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("LayoutParity")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", MainSource)
            .AddSourceFile("pkg/thing.spy", ThingSource);
        helper.CreateProjectFile();
        return helper;
    }

    private static string Key(CompilationUnit unit)
    {
        var name = Path.GetFileName(unit.FilePath);
        return Path.GetFileName(Path.GetDirectoryName(unit.FilePath)) == "pkg" ? "pkg/" + name : name;
    }

    private static string Render(ModuleLayout? layout) =>
        layout == null ? "<none>" : string.Join(".", layout.NamespaceSegments) + "|" + layout.MembersClassName;

    /// <summary>What code generation would read: the project-level (merged) SemanticInfo.</summary>
    private static Dictionary<string, string> Observe(ProjectModel model)
    {
        var merged = model.SemanticInfo!;
        var observed = model.Units.Values.ToDictionary(Key, u => Render(merged.GetModuleLayout(u.Ast!)));

        var main = model.Units.Values.Single(u => Key(u) == "main.spy");
        var fromImport = main.Ast!.Body.OfType<FromImportStatement>().Single();
        observed["main.spy:from pkg.thing"] = Render(merged.GetModuleLayout(fromImport));

        var thing = model.Units.Values.Single(u => Key(u) == "pkg/thing.spy");
        var foo = model.GlobalSymbols!.GetModuleScope(thing.ModulePath)!.Lookup("Foo", searchParent: false)!;
        observed["Foo.IsNamespaceSibling"] = (model.SemanticBinding.GetCodeGenInfo(foo)?.IsNamespaceSibling ?? false).ToString();
        return observed;
    }

    private static Dictionary<string, string> Expected() => new(ExpectedOwnLayouts)
    {
        ["main.spy:from pkg.thing"] = "Pkg.Thing|ThingModule",
        ["Foo.IsNamespaceSibling"] = "True",
    };

    private static string SpyProj(ProjectCompilationHelper helper) =>
        Path.Combine(helper.ProjectDirectory, "LayoutParity.spyproj");

    private static string Entry(ProjectCompilationHelper helper) =>
        Path.Combine(helper.SourceDirectory, "main.spy");

    public static IEnumerable<object[]> EntryPaths() =>
        new[] { "run", "project", "lsp-project", "lsp-document" }.Select(p => new object[] { p });

    private static ProjectModel Compile(string entryPath, ProjectCompilationHelper helper)
    {
        var options = new CompilerOptions { OutputType = "exe" };
        switch (entryPath)
        {
            case "run":
                {
                    // `sharpyc run main.spy`: Compiler.Compile's synthetic project of the import closure.
                    var entry = Entry(helper);
                    var config = SyntheticProject.BuildConfig(File.ReadAllText(entry), entry, options, NullLogger.Instance);
                    var result = new ProjectCompiler(NullLogger.Instance).Compile(config, CancellationToken.None, emitAssembly: false);
                    result.Success.Should().BeTrue(string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
                    return result.ProjectModel!;
                }
            case "project":
                {
                    var result = helper.Compile();
                    result.Success.Should().BeTrue(string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
                    return result.ProjectModel!;
                }
            case "lsp-project":
                {
                    var result = new CompilerApi().AnalyzeProject(ProjectFileParser.Load(SpyProj(helper)));
                    result.Success.Should().BeTrue(string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
                    return result.ProjectModel;
                }
            case "lsp-document":
                {
                    // CompilerApi.Analyze(source, options): the entry file has no path identity (#1087).
                    var entry = Entry(helper);
                    var config = SyntheticProject.BuildConfig(File.ReadAllText(entry), entry, options, NullLogger.Instance,
                        preserveTrivia: true, nullifyEntryFilePath: true);
                    var analysis = SyntheticProject.Analyze(config, options, NullLogger.Instance, null, null, CancellationToken.None).Analysis;
                    analysis.Success.Should().BeTrue(string.Join("; ", analysis.Diagnostics.GetErrors().Select(d => d.Message)));
                    return analysis.ProjectModel;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(entryPath), entryPath, null);
        }
    }

    [Theory]
    [MemberData(nameof(EntryPaths))]
    public void EveryEntryPath_RecordsTheLayout_InTheMergedSemanticInfo(string entryPath)
    {
        using var helper = CreateProject();
        Observe(Compile(entryPath, helper)).Should().Equal(Expected());
    }

    [Fact]
    public void SingleFileRun_AndProject_RecordTheSameNamespaceSegments()
    {
        using var helper = CreateProject();
        var run = Observe(Compile("run", helper));
        var project = Observe(Compile("project", helper));

        run["pkg/thing.spy"].Should().Be("Pkg.Thing|ThingModule");
        run.Should().Equal(project, "single-file run and project mode lay one module out identically");
    }

    [Fact]
    public void WarmBuild_CarriesTheImportedFacts_FromTheCache()
    {
        // thing.spy is served from the cache on the warm build; the consumer reads the sibling bit of
        // `thing.Foo` (a qualified reference — no from-import on the consumer side to recompute it)
        // from the restored symbol's CachedCodeGenInfo.
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("LayoutWarm")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .WithIncremental()
            .AddSourceFile("main.spy", "import thing\n\ndef main() -> None:\n    print(thing.Foo().x)\n")
            .AddSourceFile("thing.spy", ThingSource);
        helper.CreateProjectFile();

        string SiblingBit(ProjectCompilationResult result)
        {
            var model = result.ProjectModel!;
            var thing = model.Units.Values.Single(u => Path.GetFileName(u.FilePath) == "thing.spy");
            var foo = model.GlobalSymbols!.GetModuleScope(thing.ModulePath)!.Lookup("Foo", searchParent: false)!;
            var main = model.Units.Values.Single(u => Path.GetFileName(u.FilePath) == "main.spy");
            var module = model.GlobalSymbols!.GetModuleScope(main.ModulePath)!.Lookup("thing", searchParent: false)!;
            var moduleInfo = model.SemanticBinding.GetCodeGenInfo(module);
            return $"{model.SemanticBinding.GetCodeGenInfo(foo)?.IsNamespaceSibling} " +
                   $"{string.Join(".", moduleInfo?.NamespaceSegments ?? Array.Empty<string>())}|{moduleInfo?.MembersClassName}";
        }

        var cold = helper.AssertCompilationSucceeded(helper.Compile());
        SiblingBit(cold).Should().Be("True Thing|ThingModule");

        helper.UpdateSourceFile("main.spy", "import thing\n\ndef main() -> None:\n    print(thing.Foo().x + 1)\n");
        var warm = helper.AssertIncrementalSkipped(helper.Compile(), "thing.spy");
        SiblingBit(warm).Should().Be("True Thing|ThingModule",
            "a cache-served type keeps its recorded sibling bit (CachedCodeGenInfo, schema 39)");
    }
}
