using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2028: a project build is successful iff its diagnostic bag has no errors, on every arm —
/// not iff Roslyn accepted the C#.
/// </summary>
/// <remarks>
/// <para>
/// The discriminating input is an EMITTER-time refusal in a module whose C# is then dropped. When
/// nothing references that module the remaining C# compiles cleanly, so a verdict keyed on Roslyn
/// alone reports success while the bag holds the refusal. At acd1d40a2 the un-imported cell printed
/// <c>Build succeeded.</c> and wrote a complete assembly (measured). A semantic-time refusal (SPY0523
/// and every other CodeGenInfoComputer code) stops the build before the assembly phase and never
/// reaches the verdict under test, so it cannot guard it.
/// </para>
/// <para>
/// Cells: producer {every emitter-time error reachable from a project: <c>@lru_cache</c> on an
/// <c>async</c> function, on a generator, on an <c>async</c> method — all SPY0500} × the refused
/// module's import state {referenced by the entry, imported but unreferenced, not imported} ×
/// {cold build, warm incremental build}. The referenced cells fail in Roslyn too (the dropped C# is
/// missing) and are the controls that the verdict is not an import-state accident; the other two
/// states are the ones only a bag-keyed verdict refuses.
/// </para>
/// </remarks>
public class BuildVerdictMatrixTests
{
    private readonly ITestOutputHelper _output;

    public BuildVerdictMatrixTests(ITestOutputHelper output) => _output = output;

    private const string Helper = "\n\ndef helper() -> int:\n    return 7\n";

    private static readonly Dictionary<string, (string Source, int Line, int Column)> Producers = new()
    {
        ["lru_async_function"] = ("@lru_cache\nasync def f() -> int:\n    return 1\n", 2, 1),
        ["lru_generator_function"] = ("@lru_cache\ndef g() -> int:\n    yield 1\n", 2, 1),
        ["lru_async_method"] = ("class C:\n    @lru_cache\n    async def f(self) -> int:\n        return 1\n", 3, 5),
    };

    private static readonly Dictionary<string, string> Entries = new()
    {
        ["referenced"] = "from thing import helper\n\ndef main() -> None:\n    print(helper())\n",
        ["unreferenced"] = "import thing\n\ndef main() -> None:\n    print(7)\n",
        ["unimported"] = "def main() -> None:\n    print(7)\n",
    };

    public static IEnumerable<object[]> Cells()
        => from producer in Producers.Keys
           from entry in Entries.Keys
           from warm in new[] { false, true }
           select new object[] { producer, entry, warm };

    [Theory]
    [MemberData(nameof(Cells))]
    public void EmitterRefusal_FailsTheBuild(string producer, string entry, bool warm)
    {
        var (source, line, column) = Producers[producer];
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Verdict").WithEntryPoint("main.spy").WithIncremental(warm);
        helper.AddSourceFile("main.spy", Entries[entry]);

        if (warm)
        {
            // Build 1 caches a clean tree; build 2 then sees the refusal with a cache on disk.
            helper.AddSourceFile("thing.spy", Helper.TrimStart('\n'));
            helper.CreateProjectFile();
            var clean = helper.Compile();
            clean.Success.Should().BeTrue($"[{producer}/{entry}] positive control: the clean tree builds\n{Describe(clean)}");
            Artifacts(helper).Should().NotBeEmpty("positive control for the cold cells' absence assertion");
            helper.UpdateSourceFile("thing.spy", source + Helper);
        }
        else
        {
            helper.AddSourceFile("thing.spy", source + Helper);
            helper.CreateProjectFile();
        }

        var result = helper.Compile();
        AssertRefused(result, helper, producer, entry, line, column);

        if (warm)
        {
            // A refused build saves no cache: an unchanged rebuild must refuse again.
            AssertRefused(helper.Compile(), helper, producer, entry, line, column);
        }
        else
        {
            Artifacts(helper).Should().BeEmpty($"[{producer}/{entry}] a refused cold build writes no assembly (#2028)");
        }
    }

    private static void AssertRefused(
        ProjectCompilationResult result, ProjectCompilationHelper helper,
        string producer, string entry, int line, int column)
    {
        var refusal = result.Diagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.CodeGen.EmitError)
            .ToList();
        refusal.Should().ContainSingle($"[{producer}/{entry}] positive control: the emitter refuses\n{Describe(result)}");
        refusal[0].Phase.Should().Be(CompilerPhase.CodeGeneration, "the refusal is emitter-time, not semantic");
        Path.GetFileName(refusal[0].FilePath).Should().Be("thing.spy");
        (refusal[0].Line, refusal[0].Column).Should().Be((line, column));

        result.Success.Should().BeFalse(
            $"[{producer}/{entry}] the bag holds an error, so the build failed (#2028)\n{Describe(result)}");
        result.OutputAssemblyPath.Should().BeNull($"[{producer}/{entry}] a failed build names no assembly");
    }

    private static IEnumerable<string> Artifacts(ProjectCompilationHelper helper)
        => Directory.EnumerateFiles(helper.ProjectDirectory, "Verdict.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".dll" or ".exe" or ".pdb");

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(18);

    private static string Describe(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll()
            .Select(d => $"{d.Severity} {d.Code} {d.Phase} '{d.FilePath}' {d.Line}:{d.Column} {d.Message}"));
}
