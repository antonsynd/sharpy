using System.Linq;
using FluentAssertions;
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

        cs.Should().Contain("ZeroDivisionError", "division must stay runtime code, not be folded to a literal");
    }

    [Fact]
    public void Enabled_IntArithmeticWrapsAtInt32()
    {
        // Design Decision 6: fold with exactly the emitted C#'s semantics — Sharpy `int` is C# int32
        // with unchecked arithmetic, so int32.MaxValue + 1 folds to the wrapped value, not Python's
        // unbounded 2147483648.
        var cs = CompileCSharp("def f() -> int:\n    x: int = 2147483647 + 1\n    return x\n", fold: true);

        cs.Should().Contain("-2147483648", "the fold must wrap at the mapped int32 width");
        cs.Should().NotContain("2147483647 + 1");
    }

    [Fact]
    public void Enabled_LongArithmeticWrapsAtInt64()
    {
        // 2^32 * 2^32 = 2^64 wraps to 0 in unchecked int64 arithmetic — the mapped width for `long`.
        var cs = CompileCSharp("def f() -> int:\n    y: long = 4294967296 * 4294967296\n    return 0\n", fold: true);

        cs.Should().Contain("y = 0", "the fold must wrap at the mapped int64 width");
        cs.Should().NotContain("4294967296 * 4294967296");
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
