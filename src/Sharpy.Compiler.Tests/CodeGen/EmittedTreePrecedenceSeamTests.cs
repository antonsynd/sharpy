using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Unit-level positive controls for the precedence-inversion class (#1727, #1712): each test emits a
/// Sharpy spelling that hands a COMPOSITE generated operand to one emitter seam without source
/// parentheses, through <see cref="EmitterTestPipeline"/> (whose <c>Violations</c> assertion fires on
/// every emission), and pins the parenthesized text. Without these, no CodeGen unit test emitted a
/// composite receiver and the pipeline assertion was vacuous for the class (class-cure audit,
/// 2026-09-02): re-breaking a seam left all 1,195 CodeGen unit tests green.
/// </summary>
public class EmittedTreePrecedenceSeamTests
{
    private static string Emit(string body) =>
        EmitterTestPipeline.CompileToCSharp("def main() -> None:\n" + body);

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void TruthinessSeam_StringBinaryReceiver_IsParenthesized()
    {
        var cs = Emit("    a: str = \"x\"\n    b: str = \"y\"\n    if a + b:\n        print(1)\n");
        cs.Should().Contain("(a + b).Length > 0");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void TruthinessSeam_ConditionalOptionalReceiver_IsParenthesized()
    {
        var cs = Emit("    flag: bool = True\n    o1: int? = 42\n    o2: int? = None()\n    if o1 if flag else o2:\n        print(1)\n");
        cs.Should().Contain("(flag ? o1 : o2).IsSome");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void MembershipSeam_CompositeContainer_IsParenthesized()
    {
        var cs = Emit("    xs: list[int] = [1]\n    ys: list[int] = [2]\n    print(2 in xs + ys)\n    print(9 not in xs + ys)\n");
        cs.Should().Contain("(xs + ys).Contains(2)");
        cs.Should().Contain("!(xs + ys).Contains(9)");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void ThrowingCastSeam_CompositeOperand_IsParenthesized()
    {
        var cs = Emit("    n: int = 1\n    m: int = 2\n    big: long = n + m as! long\n    print(big)\n");
        cs.Should().Contain("(long)(n + m)");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void OptionalNoneTestSeam_CoercionReceiver_IsParenthesized()
    {
        var cs = Emit("    o: object = long(42)\n    print(o as? long is not None)\n");
        cs.Should().Contain(").IsSome");
        cs.Should().NotContain("default.IsSome");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void GenericBinarySeam_BitwiseUnderEquality_IsParenthesized()
    {
        // Python groups `p & q == 2` as (p & q) == 2; C# ranks & below ==.
        var cs = Emit("    p: int = 6\n    q: int = 3\n    print(p & q == 2)\n");
        cs.Should().Contain("(p & q) == 2");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LenSeam_CompositeReceiver_IsParenthesized()
    {
        var cs = Emit("    a: str = \"x\"\n    b: str = \"y\"\n    print(len(a + b))\n");
        cs.Should().Contain("(a + b).Length");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void PrimaryOperands_PassThroughUnparenthesized()
    {
        // The seam adds parentheses only where precedence requires them: an identifier receiver and
        // an invocation operand print exactly as before the migration (the snapshot no-diff, §7).
        var cs = Emit("    s: str = \"x\"\n    xs: list[int] = [1]\n    if s:\n        print(1 in xs)\n");
        cs.Should().Contain("s.Length > 0");
        cs.Should().Contain("xs.Contains(1)");
        cs.Should().NotContain("(s).Length");
    }
}
