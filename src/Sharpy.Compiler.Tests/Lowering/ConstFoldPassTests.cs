using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Tests for the E3 constant-folding pass (<c>opt_const_fold</c>, #640): it reduces constant
/// arithmetic / comparison / boolean expressions to literals when the flag is enabled, is inert
/// (leaves expressions untouched) when disabled, and never folds a division (a trap must stay runtime
/// code — Python raises on a zero divisor).
/// </summary>
public class ConstFoldPassTests
{
    private readonly CompilerApi _api = new();

    private const string Source = """
        def compute() -> int:
            a: int = 2 + 3 * 4
            b: bool = 5 < 3
            c: int = -5 + 8
            return a
        """;

    private string CompileCSharp(string source, bool fold)
    {
        var options = new CompilerOptions
        {
            OutputType = "library",
            Features = fold ? FeatureFlags.None.Enable("opt_const_fold") : FeatureFlags.None,
        };
        var result = _api.Compile(source, options);
        result.Success.Should().BeTrue(
            string.Join("\n", result.Diagnostics.Select(d => $"[{d.Code}] {d.Message}")));
        return result.GeneratedCSharp ?? "";
    }

    [Fact]
    public void Enabled_FoldsConstantExpressionsToLiterals()
    {
        var cs = CompileCSharp(Source, fold: true);

        cs.Should().Contain("a = 14");     // 2 + 3 * 4
        cs.Should().Contain("b = false");  // 5 < 3
        cs.Should().Contain("c = 3");      // -5 + 8
        cs.Should().NotContain("2 + 3 * 4");
    }

    [Fact]
    public void Disabled_LeavesExpressionsUnfolded()
    {
        var cs = CompileCSharp(Source, fold: false);

        cs.Should().Contain("2 + 3 * 4");
        cs.Should().NotContain("a = 14");
    }

    [Fact]
    public void NeverFoldsDivision_TrapStaysRuntimeCode()
    {
        // Integer division is not folded in v1 — a zero divisor traps (Python raises), so division
        // stays runtime code with its ZeroDivisionError guard intact, even with the flag on.
        var cs = CompileCSharp("def f() -> int:\n    x: int = 88 // 4\n    return x\n", fold: true);

        // Assert the INTENT — the division survives as a runtime call rather than becoming a
        // literal. The previous assertion grepped for "ZeroDivisionError", which was a proxy that
        // only worked while the zero guard was spliced inline at the call site; #1226 moved it into
        // Builtins.FloorDiv, so the name no longer appears in emitted code even though the trap is
        // as live as ever. Testing the intent directly is stronger than testing the old shape.
        cs.Should().Contain("FloorDiv", "division must stay a runtime call, not be folded to a literal");
        cs.Should().NotContain("= 22", "88 // 4 must not be folded to its value");
    }

    /// <summary>
    /// Supersedes <c>Enabled_IntArithmeticWrapsAtInt32</c> and
    /// <c>Enabled_LongArithmeticWrapsAtInt64</c>, which asserted that an overflowing constant folds
    /// to its WRAPPED value ("Design Decision 6: fold with exactly the emitted C#'s semantics").
    /// <para>
    /// SPY0348 (#1234) now refuses constant integer overflow during type checking, which runs
    /// before the fold pass — so a folded wrapped constant is no longer reachable, and the pass's
    /// wrap-at-width arm is dead for integer constants (#1319). The expectations were not edited
    /// to chase an implementation change: the language rule itself changed, deliberately and as a
    /// recorded breaking change, and these two cases encoded the behaviour it replaced.
    /// </para>
    /// <para>
    /// What replaces them is stronger than what they asserted. An optimization flag must never
    /// change whether a program compiles, so both spellings are pinned as refused with
    /// <c>opt_const_fold</c> both ON and OFF. The old tests only ever exercised the ON path.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("def f() -> int:\n    x: int = 2147483647 + 1\n    return x\n")]
    [InlineData("def f() -> int:\n    y: long = 4294967296 * 4294967296\n    return 0\n")]
    public void OverflowingConstant_IsRefused_RegardlessOfFoldFlag(string source)
    {
        foreach (var fold in new[] { true, false })
        {
            var options = new CompilerOptions
            {
                OutputType = "library",
                Features = fold ? FeatureFlags.None.Enable("opt_const_fold") : FeatureFlags.None,
            };

            var result = _api.Compile(source, options);

            result.Success.Should().BeFalse(
                $"constant integer overflow is refused, not wrapped (opt_const_fold: {fold})");
            result.Diagnostics.Should().Contain(
                d => d.Code == DiagnosticCodes.Semantic.ConstantIntegerOverflow,
                "SPY0348 is the diagnostic for constant integer overflow, and enabling "
                + $"opt_const_fold must not change whether the program compiles (fold: {fold})");
        }
    }

    /// <summary>
    /// P82 (#2443): an in-place collection mutation is a typed IR node (<c>IrCollectionMutation</c>),
    /// and the shared rewriter descends into it, so the fold reaches its value: <c>xs += [2 + 3]</c>
    /// still emits the mutator, with the folded element.
    /// </summary>
    [Fact]
    public void Enabled_FoldsInsideCollectionMutationValue()
    {
        const string source = "def f() -> list[int]:\n    xs: list[int] = [1]\n    xs += [2 + 3]\n    return xs\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().Contain(".Extend(", "the augmented assignment stays an in-place mutation");
        cs.Should().NotContain("2 + 3", "the mutation's value is reached by the fold pass");
        CompileCSharp(source, fold: false).Should().Contain("2 + 3", "the fold is the flag's doing");
    }

    /// <summary>
    /// P82 (#2443): an unpacking element stored into a narrowed Optional target is a typed IR node
    /// (<c>IrOptionalWrappedElement</c>) holding the element's own node, and the shared rewriter
    /// descends into it, so the fold reaches the element: <c>p, q = 2 + 3, 4</c> folds and keeps its
    /// wrap.
    /// </summary>
    [Fact]
    public void Enabled_FoldsInsideOptionalWrappedElement_WrapKept()
    {
        const string source = "def f() -> int:\n    p: int? = Some(1)\n    q: int? = Some(2)\n"
            + "    if p is not None and q is not None:\n        p, q = 2 + 3, 4\n        return p + q\n"
            + "    return 0\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().Contain("Optional<int>.Some(5)", "the wrapped element folds and keeps its wrap");
        cs.Should().NotContain("2 + 3", "the wrapped element is reached by the fold pass");
        CompileCSharp(source, fold: false).Should().Contain("Optional<int>.Some(2 + 3)", "the fold is the flag's doing");
    }

    /// <summary>
    /// #2363: a try whose covered handlers are left out is a typed IR node (<c>IrPrunedTry</c>), and the
    /// shared rewriter descends into it as it did into the opaque try, so the fold reaches the try body
    /// and a surviving handler's body; the covered handler is still left out.
    /// </summary>
    [Fact]
    public void Enabled_FoldsInsidePrunedTry_BodyAndSurvivingHandler()
    {
        const string source = "def f() -> int:\n    x: int = 0\n    try:\n        x = 2 + 3\n"
            + "    except LookupError:\n        x = 7 + 11\n    except KeyError:\n        x = 9\n    return x\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().NotContain("2 + 3", "the pruned try's body is reached by the fold pass");
        cs.Should().NotContain("7 + 11", "the surviving handler's body is reached by the fold pass");
        cs.Should().Contain("18", "the surviving handler's value folds");
        cs.Should().NotContain("KeyError", "the covered handler is still left out");
        var unfolded = CompileCSharp(source, fold: false);
        unfolded.Should().Contain("2 + 3", "the fold is the flag's doing");
        unfolded.Should().Contain("7 + 11", "the fold is the flag's doing");
    }

    /// <summary>
    /// P82 tranche 3 (#2443, plan-92249e D2): <c>-6</c> records <c>NegateLiteralInt</c>, so it lowers to
    /// an <c>IrOperatorExpression</c>, the only operator kind that both carries an entry and folds. The
    /// fold pass folds it as it folded the opaque node, so the enclosing product folds too.
    /// </summary>
    [Fact]
    public void Enabled_FoldsNegatedIntLiteral_UnderOperatorExpression()
    {
        const string source = "def f() -> int:\n    x: int = -6 * 7\n    return x\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().Contain("x = -42", "the negated literal folds, so its enclosing product folds");
        cs.Should().NotContain("* 7", "the product is reduced");
        CompileCSharp(source, fold: false).Should().Contain("* 7", "the fold is the flag's doing");
    }

    /// <summary>
    /// P82 tranche 3 (#2443, plan-92249e D2): <c>(2 + 3) // n</c> records <c>IntegerFloorDivide</c>, so it
    /// lowers to an <c>IrOperatorExpression</c>; the shared rewriter descends into it as it did into the
    /// opaque node, so its foldable left operand folds while the division stays runtime code.
    /// </summary>
    [Fact]
    public void Enabled_FoldsOperandUnderOperatorExpression()
    {
        const string source = "def f(n: int) -> int:\n    return (2 + 3) // n\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().Contain("FloorDiv", "the floor division keeps its lowering");
        cs.Should().NotContain("2 + 3", "the operator expression's operand is reached by the fold pass");
        CompileCSharp(source, fold: false).Should().Contain("2 + 3", "the fold is the flag's doing");
    }

    /// <summary>
    /// P82 tranche 3 (#2443, plan-92249e D2): <c>x //= 2 + 3</c> on an <c>int</c> records
    /// <c>IntegerFloorDivide</c>, so it lowers to an <c>IrOperatorAssignment</c>; the shared rewriter
    /// descends into it as it did into the opaque assignment, so its value folds.
    /// </summary>
    [Fact]
    public void Enabled_FoldsValueUnderOperatorAssignment()
    {
        const string source = "def f(n: int) -> int:\n    x: int = n\n    x //= 2 + 3\n    return x\n";

        var cs = CompileCSharp(source, fold: true);

        cs.Should().Contain("FloorDiv", "the augmented floor division keeps its lowering");
        cs.Should().NotContain("2 + 3", "the operator assignment's value is reached by the fold pass");
        CompileCSharp(source, fold: false).Should().Contain("2 + 3", "the fold is the flag's doing");
    }

    [Fact]
    public void NonFiniteDoubleResult_StaysUnfolded()
    {
        // inf/NaN have no C# literal form, so a fold that would produce one is skipped and the
        // multiplication stays runtime code.
        var cs = CompileCSharp("def f() -> float:\n    z: float = 1e308 * 10.0\n    return z\n", fold: true);

        cs.Should().NotContain("Infinity", "a non-finite result must not be folded");
        cs.Should().Contain("*", "the multiplication must stay runtime code");
    }
}
