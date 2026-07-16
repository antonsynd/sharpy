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
}
