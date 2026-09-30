using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The override rule reads a base member identically whether the base is Sharpy- or CLR-declared,
/// on every import route (#2138 owner ruling; refs #2039, #1122): overriding an abstract/virtual
/// member of a CLR base REQUIRES <c>@override</c>, exactly as for a Sharpy base; the only exemption
/// is <c>__str__</c>/<c>__eq__</c>/<c>__hash__</c>. A <c>[SharpyModule]</c>-stamped module
/// (<c>sharpy.generators</c>, <c>json</c>, <c>threading</c>) bridges its types' members into the
/// symbol; a bare CLR namespace (<c>system.collections.generic</c>, <c>sharpy</c>, or
/// <c>sharpy.generators</c> when Sharpy.Core is not a discovery reference) leaves them to
/// reflection, where the member is found through the one forward name rule
/// (<c>compare</c> → <c>Compare</c>).
/// Cells: base form × {no @override → SPY0248, @override → runs} × {single-file run, project};
/// every running cell dispatches through the base-typed reference.
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

    private const string StampedStdlibThread = @"
import threading

class T(threading.Thread):
{0}    def run(self) -> None:
        print(""ran"")

def main():
    t: threading.Thread = T()
    t.start()
    t.join()
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

    // The same stdlib JSONEncoder, reached through the bare `sharpy` namespace. Its @override form
    // is not a running cell: constructing it ICEs on the namespace route's constructor surface
    // (CS7036, filed separately), independent of the override rule.
    private const string ClrNamespaceStdlibVirtual = @"
import sharpy

class E(sharpy.JSONEncoder):
{0}    def default(self, obj: object) -> object:
        return ""custom""

def main():
    enc: sharpy.JSONEncoder = E()
    print(enc.encode(object()))
";

    private const string RequiresOverride = "and requires the @override decorator";

    /// <summary>
    /// form, source template, @override?, expected stdout (null = refused with
    /// <see cref="RequiresOverride"/>), with Sharpy.Core and Sharpy.Stdlib as discovery
    /// references — the CLI's channel (<c>CliHelpers.GetDefaultReferences</c>).
    /// </summary>
    public static IEnumerable<object?[]> Cells()
    {
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, false, null };
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, true, "MyGen" };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, false, null };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, true, "MyGen" };
        yield return new object?[] { "json_stamped", StampedStdlibVirtual, false, null };
        yield return new object?[] { "json_stamped", StampedStdlibVirtual, true, "\"custom\"" };
        yield return new object?[] { "thread_stamped", StampedStdlibThread, false, null };
        yield return new object?[] { "thread_stamped", StampedStdlibThread, true, "ran" };
        yield return new object?[] { "scg_namespace", ClrNamespaceAbstract, false, null };
        yield return new object?[] { "scg_namespace", ClrNamespaceAbstract, true, "1" };
        yield return new object?[] { "json_namespace", ClrNamespaceStdlibVirtual, false, null };
    }

    /// <summary>
    /// The SAME <c>SourceGenerator</c> on the bare-namespace route: without Sharpy.Core as a
    /// discovery reference nothing declares <c>sharpy.generators</c> by attribute, so it resolves as
    /// the CLR namespace <c>Sharpy.Generators</c> and its members are read by reflection.
    /// </summary>
    public static IEnumerable<object?[]> NamespaceRouteCells()
    {
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, false, null };
        yield return new object?[] { "sg_from", SourceGeneratorFromImport, true, "MyGen" };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, false, null };
        yield return new object?[] { "sg_qualified", SourceGeneratorModuleQualified, true, "MyGen" };
    }

    private static string Render(string template, bool decorated) =>
        template.Replace("{0}", decorated ? "    @override\n" : string.Empty);

    private static void AssertCell(string form, bool decorated, string? expected,
        bool success, string stdout, IEnumerable<string> compilationErrors)
    {
        var errors = string.Join("; ", compilationErrors);

        if (expected == null)
        {
            success.Should().BeFalse($"{form}: a CLR-base override without @override is refused (#2138)");
            errors.Should().Contain(RequiresOverride, form);
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
        // The helper builds with the CLI's reference set (Core + Stdlib, #2140).
        using var helper = new ProjectCompilationHelper(Output);
        helper.AddSourceFile("main.spy", Render(template, decorated));
        helper.WithRootNamespace("OverrideRoute").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.CompileAndExecute();
        AssertCell(form, decorated, expected, result.Success, result.StandardOutput, result.CompilationErrors);
    }

    [Theory]
    [MemberData(nameof(NamespaceRouteCells))]
    public void Project_NamespaceRoute(string form, string template, bool decorated, string? expected)
    {
        // A narrower reference set than sharpyc passes: Stdlib only, no Core (#2140).
        using var helper = new ProjectCompilationHelper(Output).WithoutCliReferences().WithRuntimeReferences();
        helper.ModuleReferences.AddRange(TestProjectScaffold.ResolveRuntimeDllPaths()
            .Where(p => Path.GetFileName(p) == "Sharpy.Stdlib.dll"));
        helper.AddSourceFile("main.spy", Render(template, decorated));
        helper.WithRootNamespace("OverrideRoute").WithEntryPoint("main.spy").CreateProjectFile();
        var result = helper.CompileAndExecute();
        AssertCell(form, decorated, expected, result.Success, result.StandardOutput, result.CompilationErrors);
    }

    // The spec's exemption: __str__/__eq__/__hash__ implicitly override System.Object at any depth,
    // on a CLR-derived class too — accepted with and without @override.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectDunder_OnClrDerivedClass_IsOptional(bool decorated)
    {
        var source = Render(@"
import system.collections.generic as scg

class Rev(scg.Comparer[int]):
    @override
    def compare(self, x: int, y: int) -> int:
        return y - x
{0}    def __str__(self) -> str:
        return ""rev""
{0}    def __eq__(self, other: object) -> bool:
        return True
{0}    def __hash__(self) -> int:
        return 7

def main():
    o: object = Rev()
    print(str(o), o == Rev(), hash(o))
", decorated);
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"@override={decorated}: {string.Join("; ", result.CompilationErrors)}");
        result.StandardOutput.Trim().Should().Be("rev True 7");
    }

    // Diagnostics that are not the missing-@override rule keep their shape.
    [Theory]
    [InlineData("arity_mismatch_bridged", "requires the @override decorator", @"
from json import JSONEncoder

class E(JSONEncoder):
    def default(self) -> object:
        return ""custom""

def main():
    print(type(E()).__name__)
")]
    [InlineData("sharpy_intermediate", "overrides a virtual method in base class 'A' and requires the @override decorator", @"
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
    [InlineData("not_virtual", "is not marked @virtual or @abstract", @"
from json import JSONEncoder

class E(JSONEncoder):
    @override
    def encode(self, obj: object) -> str:
        return ""bad""

def main():
    print(type(E()).__name__)
")]
    [InlineData("no_such_member", "is marked @override but no matching method exists in base class", @"
import system.collections.generic as scg

class Rev(scg.Comparer[int]):
    @override
    def frobnicate(self, x: int) -> int:
        return x

def main():
    print(type(Rev()).__name__)
")]
    public void OtherOverrideDiagnostics_Unchanged(string form, string message, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(form);
        string.Join("; ", result.CompilationErrors).Should().Contain(message, form);
    }
}
