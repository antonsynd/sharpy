using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// A CLR base's members, and the override rule that reads them, are the same whichever import
/// route resolved the base (refs #2039, #1122). A <c>[SharpyModule]</c>-stamped module
/// (<c>sharpy.generators</c>, <c>json</c>) bridges its types' members into the symbol; a bare CLR
/// namespace (<c>system.collections.generic</c>, or <c>sharpy.generators</c> when Sharpy.Core is not
/// a discovery reference) leaves them to reflection. Overriding an abstract/virtual member of
/// either WITHOUT <c>@override</c> is the implicit CLR override (#1122) on both routes — once the
/// stamped route bridged the members, the "requires @override" rule read them and refused
/// <c>SourceGenerator.generate</c>, which ran while <c>sharpy.generators</c> took the namespace route.
/// Cells: base form × {no @override, @override} × {single-file run, project}; every cell executes
/// and dispatches through the base-typed reference.
/// </summary>
[Collection("HeavyCompilation")]
public class ClrBaseOverrideRouteMatrixTests : StdlibAwareIntegrationTestBase
{
    public ClrBaseOverrideRouteMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SourceGeneratorFromImport = @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class MyGen(SourceGenerator):
{0}    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput(""x"")

def main():
    b: SourceGenerator = MyGen()
    print(type(b).__name__)
";

    private const string SourceGeneratorModuleQualified = @"
import sharpy.generators as g
from sharpy.generators import GeneratorContext, GeneratorOutput

class MyGen(g.SourceGenerator):
{0}    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput(""x"")

def main():
    b: g.SourceGenerator = MyGen()
    print(type(b).__name__)
";

    private const string StampedStdlibVirtual = @"
from json import JSONEncoder

class E(JSONEncoder):
{0}    def default(self, obj: object) -> object:
        return ""custom""

def main():
    enc: JSONEncoder = E()
    print(enc.encode(object()))
";

    private const string ClrNamespaceAbstract = @"
import system.collections.generic as scg

class Rev(scg.Comparer[int]):
{0}    def compare(self, x: int, y: int) -> int:
        return y - x

def main():
    c: scg.Comparer[int] = Rev()
    print(c.compare(1, 2))
";

    // The pre-existing namespace-route refusal of @override on a CLR base (the route's symbol
    // carries no members, so the @override check finds none; #2138) — pinned, not endorsed.
    private const string NoMatchingBaseMethod = "is marked @override but no matching method exists in base class";

    /// <summary>
    /// form, source template, @override?, expected stdout (null = refused with
    /// <see cref="NoMatchingBaseMethod"/>), with Sharpy.Core and Sharpy.Stdlib as discovery
    /// references — the CLI's channel (<c>CliHelpers.GetDefaultReferences</c>).
    /// </summary>
    public static IEnumerable<object?[]> Cells()
    {
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, false, "MyGen" };
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, true, "MyGen" };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, false, "MyGen" };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, true, "MyGen" };
        yield return new object?[] { "json_stamped", StampedStdlibVirtual, false, "\"custom\"" };
        yield return new object?[] { "json_stamped", StampedStdlibVirtual, true, "\"custom\"" };
        yield return new object?[] { "scg_namespace", ClrNamespaceAbstract, false, "1" };
        yield return new object?[] { "scg_namespace", ClrNamespaceAbstract, true, null };
    }

    /// <summary>
    /// The SAME <c>SourceGenerator</c> on the bare-namespace route: without Sharpy.Core as a
    /// discovery reference nothing declares <c>sharpy.generators</c> by attribute, so it resolves as
    /// the CLR namespace <c>Sharpy.Generators</c>.
    /// </summary>
    public static IEnumerable<object?[]> NamespaceRouteCells()
    {
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, false, "MyGen" };
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, true, null };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, false, "MyGen" };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, true, null };
    }

    private static string Render(string template, bool decorated) =>
        template.Replace("{0}", decorated ? "    @override\n" : string.Empty);

    private static void AssertCell(string form, bool decorated, string? expected,
        bool success, string stdout, IEnumerable<string> compilationErrors)
    {
        var errors = string.Join("; ", compilationErrors);

        if (expected == null)
        {
            success.Should().BeFalse($"{form}: @override on a namespace-route CLR base is refused today");
            errors.Should().Contain(NoMatchingBaseMethod, form);
            return;
        }

        success.Should().BeTrue($"{form} (@override={decorated}) should run: {errors}");
        stdout.Trim().Should().Be(expected, form);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void SingleFile(string form, string template, bool decorated, string? expected)
    {
        var result = CompileAndExecute(Render(template, decorated));
        AssertCell(form, decorated, expected, result.Success, result.StandardOutput, result.CompilationErrors);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Project(string form, string template, bool decorated, string? expected)
    {
        using var helper = new ProjectCompilationHelper(Output).WithStdlibModules();
        helper.ModuleReferences.AddRange(TestProjectScaffold.ResolveRuntimeDllPaths()
            .Where(p => Path.GetFileName(p) == "Sharpy.Core.dll"));
        helper.AddSourceFile("main.spy", Render(template, decorated));
        helper.WithRootNamespace("OverrideRoute").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.CompileAndExecute();
        AssertCell(form, decorated, expected, result.Success, result.StandardOutput, result.CompilationErrors);
    }

    [Theory]
    [MemberData(nameof(NamespaceRouteCells))]
    public void Project_NamespaceRoute(string form, string template, bool decorated, string? expected)
    {
        using var helper = new ProjectCompilationHelper(Output).WithStdlibModules();
        helper.AddSourceFile("main.spy", Render(template, decorated));
        helper.WithRootNamespace("OverrideRoute").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.CompileAndExecute();
        AssertCell(form, decorated, expected, result.Success, result.StandardOutput, result.CompilationErrors);
    }

    // Boundaries of the implicit override: only a method the #1122 detector itself emits as
    // `override` (same name AND arity, member declared by the CLR base) is exempt from @override.
    [Theory]
    [InlineData("arity_mismatch", @"
from json import JSONEncoder

class E(JSONEncoder):
    def default(self) -> object:
        return ""custom""

def main():
    print(type(E()).__name__)
")]
    [InlineData("sharpy_intermediate", @"
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class A(SourceGenerator):
    @override
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput(""a"")

class B(A):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput(""b"")

def main():
    print(type(B()).__name__)
")]
    public void NotImplicit_StillRequiresOverride(string form, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(form);
        string.Join("; ", result.CompilationErrors).Should().Contain("requires the @override decorator", form);
    }
}
