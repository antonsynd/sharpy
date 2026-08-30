using FluentAssertions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A collection literal takes its contextual type only from a RESOLVED callee — never from an
/// overload SET (#1671 regression, introduced by 9aae978de).
///
/// <para>The defect class: <c>CheckCallArguments</c> pushed a parameter type as the argument's
/// expected type from whichever single candidate the callee happened to bind to. The literal was
/// then recorded with that candidate's element type, and overload resolution selected on the
/// recorded type — so <c>h([1, 2])</c> picked <c>h(list[float])</c> merely because that
/// declaration was written first, and picked <c>h(list[int])</c> when the two <c>def</c>s were
/// swapped. Declaration order is not a resolution input (<c>overload_resolution.md</c>): an exact
/// match beats a widening conversion, in either order.</para>
///
/// <para>The matrix is order × literal kind × route, where "route" is the candidate-set source
/// the gate consults: user overloads, imported overloads, module-qualified overloads,
/// instance-method overloads, and a CLR method group behind a stdlib module. Single-candidate
/// control cells assert the OTHER direction — contextual typing from a resolved callee still
/// works, which is what 9aae978de was for.</para>
///
/// <para>MUTATION-TESTED: see this file's commit body. With <c>CalleeDenotesOverloadSet</c>
/// forced to return <c>false</c>, the float-first cells of the order-independence theories fail
/// (they print "float") while the control cells stay green.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class OverloadResolutionContextualLiteralTests : StdlibAwareIntegrationTestBase
{
    public OverloadResolutionContextualLiteralTests(ITestOutputHelper output) : base(output) { }

    /// <summary>(float-overload parameter spelling, int-overload parameter spelling, call argument).</summary>
    private static (string Float, string Int, string Argument) Shapes(string kind) => kind switch
    {
        "list" => ("list[float]", "list[int]", "[1, 2]"),
        "set" => ("set[float]", "set[int]", "{1, 2}"),
        "dict" => ("dict[str, float]", "dict[str, int]", "{\"a\": 1}"),
        "tuple" => ("tuple[float, float]", "tuple[int, int]", "(1, 2)"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "unknown literal kind")
    };

    private static string FormatErrors(ExecutionResult result)
        => string.Join("\n", result.CompilationErrors);

    // ── Route 1: module-level user overloads ──

    [Theory]
    [InlineData("list", true)]
    [InlineData("list", false)]
    [InlineData("set", true)]
    [InlineData("set", false)]
    [InlineData("dict", true)]
    [InlineData("dict", false)]
    [InlineData("tuple", true)]
    [InlineData("tuple", false)]
    public void UserOverloads_ExactMatchWins_InEitherDeclarationOrder(string kind, bool floatFirst)
    {
        var (floatParam, intParam, argument) = Shapes(kind);
        var floatDecl = $"def h(v: {floatParam}) -> str:\n    return \"float\"\n";
        var intDecl = $"def h(v: {intParam}) -> str:\n    return \"int\"\n";
        var source = (floatFirst ? floatDecl + "\n" + intDecl : intDecl + "\n" + floatDecl)
            + $"\ndef main() -> None:\n    print(h({argument}))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be("int",
            $"the {kind} literal's elements are int, so the exact-match overload wins regardless "
            + $"of which declaration comes first (floatFirst={floatFirst})");
    }

    // ── Route 2: instance-method overloads ──

    [Theory]
    [InlineData("list", true)]
    [InlineData("list", false)]
    [InlineData("set", true)]
    [InlineData("set", false)]
    [InlineData("dict", true)]
    [InlineData("dict", false)]
    [InlineData("tuple", true)]
    [InlineData("tuple", false)]
    public void InstanceMethodOverloads_ExactMatchWins_InEitherDeclarationOrder(string kind, bool floatFirst)
    {
        var (floatParam, intParam, argument) = Shapes(kind);
        var floatDecl = $"    def m(self, v: {floatParam}) -> str:\n        return \"float\"\n";
        var intDecl = $"    def m(self, v: {intParam}) -> str:\n        return \"int\"\n";
        var source = "class C:\n"
            + (floatFirst ? floatDecl + "\n" + intDecl : intDecl + "\n" + floatDecl)
            + $"\ndef main() -> None:\n    c: C = C()\n    print(c.m({argument}))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be("int",
            $"an instance-method overload set is a candidate set too (kind={kind}, floatFirst={floatFirst})");
    }

    // ── Routes 3 and 4: imported and module-qualified overloads (two files) ──

    [Theory]
    [InlineData("list", true, true)]
    [InlineData("list", true, false)]
    [InlineData("list", false, true)]
    [InlineData("list", false, false)]
    [InlineData("dict", true, true)]
    [InlineData("dict", true, false)]
    [InlineData("dict", false, true)]
    [InlineData("dict", false, false)]
    public void CrossModuleOverloads_ExactMatchWins_InEitherDeclarationOrder(
        string kind, bool floatFirst, bool qualified)
    {
        var (floatParam, intParam, argument) = Shapes(kind);
        var floatDecl = $"def h(v: {floatParam}) -> str:\n    return \"float\"\n";
        var intDecl = $"def h(v: {intParam}) -> str:\n    return \"int\"\n";
        var lib = floatFirst ? floatDecl + "\n" + intDecl : intDecl + "\n" + floatDecl;

        var main = qualified
            ? $"import lib\n\ndef main() -> None:\n    print(lib.h({argument}))\n"
            : $"from lib import h\n\ndef main() -> None:\n    print(h({argument}))\n";

        // One assembly name per cell: the in-process runner loads each compiled project into this
        // AppDomain, and two cells sharing a name fail with "assembly with same name is already
        // loaded" — a harness collision that would masquerade as a resolution failure.
        var assemblyName = "ContextualLiteralOverloads" + kind
            + (floatFirst ? "FloatFirst" : "IntFirst")
            + (qualified ? "Qualified" : "Imported");

        using var helper = new Helpers.ProjectCompilationHelper(Output);
        helper.WithRootNamespace(assemblyName)
            .AddSourceFile("lib.spy", lib)
            .AddSourceFile("main.spy", main, isEntryPoint: true)
            .CreateProjectFile();

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("int",
            $"a {(qualified ? "module-qualified" : "from-imported")} overload set is a candidate set "
            + $"(kind={kind}, floatFirst={floatFirst})");
    }

    // ── Route 5: a CLR method group behind a stdlib module ──

    [Fact]
    public void ModuleClrOverloads_StatisticsMean_BindsTheListIntOverload()
    {
        // statistics.mean is Mean(List<double>) / Mean(List<int>) / Mean(List<long>). The literal
        // must type from its own elements (int), so the emitted argument is Sharpy.List<int> and
        // the int overload binds. The VALUE is 2.0 for all three, so the emitted C# is the only
        // witness that discriminates them.
        var source = @"
import statistics

def main() -> None:
    print(statistics.mean([1, 2, 3]))
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be("2.0");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("Sharpy.List<int>",
            "the argument literal types from its own elements, not from an unresolved candidate");
        result.GeneratedCSharp!.Should().NotContain("Sharpy.List<long>");
        result.GeneratedCSharp!.Should().NotContain("Sharpy.List<double>");
    }

    // ── Controls: a RESOLVED callee still supplies the contextual type ──

    [Theory]
    [InlineData("list[Base]", "[Derived(), Derived()]")]
    [InlineData("set[Base]", "{Derived()}")]
    [InlineData("dict[str, Base]", "{\"a\": Derived()}")]
    [InlineData("list[float]", "[1, 2]")]
    public void SingleCandidate_StillTakesTheContextualType(string parameterType, string argument)
    {
        var source = $@"
class Base:
    def kind(self) -> str:
        return ""base""

class Derived(Base):
    def kind(self) -> str:
        return ""derived""

def g(v: {parameterType}) -> str:
    return ""ok""

def main() -> None:
    print(g({argument}))
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be("ok",
            $"one candidate IS a resolved target, so '{argument}' still takes '{parameterType}' "
            + "as its contextual type (#1671's accepted direction)");
    }

    [Fact]
    public void SingleCandidateMethod_StillTakesTheContextualType()
    {
        var source = @"
class Base:
    pass

class Derived(Base):
    pass

class Sink:
    def take(self, v: list[Base]) -> str:
        return ""ok""

def main() -> None:
    s: Sink = Sink()
    print(s.take([Derived(), Derived()]))
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be("ok");
    }
}
