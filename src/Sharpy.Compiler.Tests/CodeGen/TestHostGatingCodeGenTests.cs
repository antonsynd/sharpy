using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The <c>@test</c> surfaces that are host-conditional (#1495, #1532): a <c>@test</c> program is an
/// ordinary runnable program under <c>sharpyc run</c>, so outside a test host its test surfaces lower
/// framework-free rather than emitting Xunit that no reference satisfies (CS0246/CS0103/CS1503 behind
/// SPY0908). These tests compile with <see cref="CompilerOptions.TargetsTestHost"/> FALSE — the answer
/// for <c>run</c>/<c>compile</c>/<c>emit</c>/REPL/LSP — and pin the two surfaces #1495 left as residue:
///
/// <list type="number">
///   <item>A module-level <c>@test</c> function is emitted as an ordinary static method ON the module
///     class, not lifted into a sibling <c>{Module}Tests</c> class, so a module-level caller can reach
///     it (the lift left the caller naming a symbol not in scope — CS0103).</item>
///   <item><c>assert isinstance(x, (A, B))</c> — the tuple form — lowers to the framework-free
///     disjunction <c>x is A || x is B</c>, not to a raw <c>Builtins.Isinstance</c> with a tuple
///     argument that does not bind (CS1503).</item>
/// </list>
///
/// Each test carries a companion TEST-HOST assertion (<see cref="CompilerOptions.TargetsTestHost"/>
/// TRUE) proving the Xunit lowering is UNCHANGED — the byte-identity control the owner set on #1495.
/// </summary>
public class TestHostGatingCodeGenTests
{
    private readonly ITestOutputHelper _output;

    public TestHostGatingCodeGenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private string Compile(string source, bool targetsTestHost, string fileName = "gating_test.spy")
    {
        var options = new CompilerOptions { TargetsTestHost = targetsTestHost };
        var compiler = new Compiler(options);
        var result = compiler.Compile(source, fileName);
        result.Success.Should().BeTrue(
            $"compilation should succeed (TargetsTestHost={targetsTestHost}), errors: "
            + $"{string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        result.GeneratedCSharpCode.Should().NotBeNull();
        _output.WriteLine($"=== Generated C# (TargetsTestHost={targetsTestHost}) ===");
        _output.WriteLine(result.GeneratedCSharpCode);
        return result.GeneratedCSharpCode!;
    }

    // ── Surface 1: the test-class lift is host-conditional (#1532) ──────────────────────────

    [Fact]
    public void ModuleLevelTest_UnderRun_EmitsOrdinaryStaticMethod_ReachableByCaller()
    {
        // MUTATION: drop the `_context.TargetsTestHost &&` guard on the @test-function collection in
        // RoslynEmitter.ModuleClass.cs → this function is lifted into the sibling test class, a
        // module-level caller can no longer reach it, and this test goes red (the assertions below fail
        // because PassingAssert lands on GatingTestTests, not the module class).
        var source = @"
@test
def passing_assert() -> None:
    assert 2 == 2

def main() -> None:
    passing_assert()
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: false);

        // The @test function is a static method on the module class, next to Main().
        code.Should().Contain("public static void PassingAssert()");
        // No sibling test class exists outside a test host.
        code.Should().NotContain("GatingTestTests");
        // The framework the run path cannot satisfy is absent.
        code.Should().NotContain("Xunit");
        code.Should().NotContain("FactAttribute");
        // The assert lowered framework-free.
        code.Should().Contain("throw new global::Sharpy.AssertionError");
    }

    [Fact]
    public void ModuleLevelTest_UnderTestHost_LiftsToSiblingTestClass_Unchanged()
    {
        // The byte-identity control: under a test host the lift and the [Fact] attribute stay.
        var source = @"
@test
def passing_assert() -> None:
    assert 2 == 2

def main() -> None:
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: true, fileName: "gating_test.spy");
        code.Should().Contain("GatingTestTests");
        code.Should().Contain("Xunit.FactAttribute");
    }

    // ── Surface 2: the isinstance-tuple assert lowers framework-free (#1532) ─────────────────

    [Fact]
    public void IsinstanceTupleAssert_UnderRun_LowersToIsDisjunction()
    {
        // MUTATION: remove the TryBuildIsinstanceTupleCondition arm in GenerateAssert →
        // GenerateExpression hands the tuple to Builtins.Isinstance (CS1503), the compilation fails,
        // and Compile()'s Success assertion goes red.
        var source = @"
@test
def check() -> None:
    x: object = 42
    assert isinstance(x, (int, str))

def main() -> None:
    check()
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: false);
        code.Should().Contain("x is int || x is string");
        // The tuple never reaches Builtins.Isinstance (which takes a single Type), and no Xunit.
        code.Should().NotContain("Builtins.Isinstance");
        code.Should().NotContain("Xunit");
    }

    [Fact]
    public void NegatedIsinstanceTupleAssert_UnderRun_LowersToNegatedDisjunction()
    {
        var source = @"
@test
def check() -> None:
    x: object = 3.14
    assert not isinstance(x, (int, str))

def main() -> None:
    check()
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: false);
        code.Should().Contain("!(x is int || x is string)");
        code.Should().NotContain("Builtins.Isinstance");
        code.Should().NotContain("Xunit");
    }

    [Fact]
    public void IsinstanceTupleOfCollections_UnderRun_ErasesEachAlternative()
    {
        // The classifier's #912 collection-erasure decision reaches the framework-free arm through the
        // SAME MapTestAssertTypeOperand the test-host arm uses — each alternative erases to its protocol
        // interface, read from the recorded decision (Critical Rule 2), not re-derived in the emitter.
        var source = @"
@test
def check() -> None:
    x: object = [1]
    assert isinstance(x, (list, dict))

def main() -> None:
    check()
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: false);
        code.Should().Contain("x is global::Sharpy.IList || x is global::Sharpy.IDict");
        code.Should().NotContain("Builtins.Isinstance");
    }

    [Fact]
    public void IsinstanceTupleAssert_UnderTestHost_KeepsXunitAssertTrue_Unchanged()
    {
        // The byte-identity control for surface 2: under a test host the tuple assert is still the
        // Xunit.Assert.True disjunction, unchanged.
        var source = @"
@test
def check() -> None:
    x: object = 42
    assert isinstance(x, (int, str))

def main() -> None:
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: true);
        code.Should().Contain("Xunit.Assert.True(x is int || x is string)");
    }

    [Fact]
    public void SingleTypeIsinstanceAssert_UnderRun_UnaffectedByTupleArm()
    {
        // Control: the single-type isinstance is an ordinary expression that already lowered correctly;
        // the tuple helper returns null for it, so it keeps its ordinary lowering (no disjunction, no
        // Xunit) and still compiles.
        var source = @"
@test
def check() -> None:
    x: object = 42
    assert isinstance(x, int)

def main() -> None:
    check()
    print(""ok"")
";
        var code = Compile(source, targetsTestHost: false);
        code.Should().NotContain("Xunit");
        code.Should().Contain("throw new global::Sharpy.AssertionError");
    }
}
