using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// One path authority for imports (P14b, Decision 28 (d)): an absolute import names a module by its
/// path below the project's common source root — the root the layout (<c>ModuleIdentifiers</c>,
/// <c>ComputeSourceRootPath</c>) spells every namespace from — on every route.
/// </summary>
/// <remarks>
/// <para>
/// Before this, project mode handed the import resolver the PROJECT directory while the layout used
/// the source root. With sources under <c>src/</c> (the default <c>src/**/*.spy</c> layout) a module
/// in a subdirectory could not absolute-import anything — a false SPY0300 "Cannot find module" —
/// while <c>sharpyc run main.spy</c> (whose project directory IS the source root) ran the same tree,
/// and <c>&lt;ModulePath&gt;</c> did not help (measured at d6a7aef05 and acd1d40a2).
/// </para>
/// <para>
/// Cells: the importing module {root <c>main.spy</c>, <c>pkg/x.spy</c>, <c>pkg/sub/y.spy</c>} × the
/// imported module {a root module, a sibling in the importer's directory (named absolutely), a module
/// in another package} × the sources {at the project directory, under <c>src/</c>} × the route
/// {<c>project</c>, <c>run</c>}. Every cell prints the imported value.
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class ImportSourceRootParityTests : IntegrationTestBase
{
    public ImportSourceRootParityTests(ITestOutputHelper output) : base(output) { }

    private static readonly Dictionary<string, string> Importers = new()
    {
        ["root"] = "main.spy",
        ["depth1"] = "pkg/x.spy",
        ["depth2"] = "pkg/sub/y.spy",
    };

    private static string TargetOf(string importer, string target) => target switch
    {
        "root" => "top.spy",
        "sibling" => importer switch
        {
            "root" => "peer.spy",
            "depth1" => "pkg/w.spy",
            _ => "pkg/sub/v.spy",
        },
        _ => "other/z.spy",
    };

    private static string ModuleName(string path) => path[..^".spy".Length].Replace('/', '.');

    public static IEnumerable<object[]> Cells()
        => from importer in Importers.Keys
           from target in new[] { "root", "sibling", "other" }
           from layout in new[] { "project_root", "src" }
           from route in new[] { "project", "run" }
           select new object[] { importer, target, layout, route };

    [Theory]
    [MemberData(nameof(Cells))]
    public void AbsoluteImport_ResolvesFromTheSourceRoot(string importer, string target, string layout, string route)
    {
        var cell = $"{importer}/{target}/{layout}/{route}";
        var importerPath = Importers[importer];
        var targetPath = TargetOf(importer, target);

        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Paths").WithEntryPoint("main.spy");
        if (layout == "project_root")
        {
            helper.WithSourceDirectory(".");
            helper.Options.SourceFilePattern = "**/*.spy";
        }

        helper.AddSourceFile(targetPath, "def val() -> int:\n    return 42\n");
        if (importer == "root")
        {
            helper.AddSourceFile("main.spy",
                $"from {ModuleName(targetPath)} import val\n\ndef main() -> None:\n    print(val())\n");
        }
        else
        {
            helper.AddSourceFile(importerPath,
                $"from {ModuleName(targetPath)} import val\n\ndef go() -> int:\n    return val()\n");
            helper.AddSourceFile("main.spy",
                $"from {ModuleName(importerPath)} import go\n\ndef main() -> None:\n    print(go())\n");
        }
        helper.CreateProjectFile();

        var (success, stdout, errors) = route == "project"
            ? Outcome(helper.CompileAndExecute())
            : Outcome(CompileAndExecuteEntryFile(Path.Combine(helper.SourceDirectory, "main.spy")));

        success.Should().BeTrue($"[{cell}] must run on both routes\n{string.Join("\n", errors)}");
        stdout.Trim().Should().Be("42", $"[{cell}]");
    }

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(36);

    private static (bool, string, IEnumerable<string>) Outcome(Sharpy.Compiler.Tests.Helpers.ExecutionResult r)
        => (r.Success, r.StandardOutput, r.CompilationErrors);

    private static (bool, string, IEnumerable<string>) Outcome(IntegrationTestBase.ExecutionResult r)
        => (r.Success, r.StandardOutput, r.CompilationErrors);
}
