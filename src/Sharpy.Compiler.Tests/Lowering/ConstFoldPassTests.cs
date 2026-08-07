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
