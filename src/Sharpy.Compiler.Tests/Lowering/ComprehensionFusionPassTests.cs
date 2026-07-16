using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Tests for the E3 comprehension pass (<c>opt_comprehension_fusion</c>, #1057). v1 generalizes D4
/// preallocation: a multi-<c>for</c> comprehension over sized, effect-free (variable/attribute)
/// sources with no <c>if</c> clause presizes the result to the product of the sources' counts. It is
/// inert when disabled, and excludes filtered comprehensions (over-reservation) and call sources
/// (possible side effects on re-evaluation).
/// </summary>
public class ComprehensionFusionPassTests
{
    private readonly CompilerApi _api = new();

    private string CompileCSharp(string source, bool fuse)
    {
        var options = new CompilerOptions
        {
            OutputType = "library",
            Features = fuse ? FeatureFlags.None.Enable("opt_comprehension_fusion") : FeatureFlags.None,
        };
        var result = _api.Compile(source, options);
        result.Success.Should().BeTrue(
            string.Join("\n", result.Diagnostics.Select(d => $"[{d.Code}] {d.Message}")));
        return (result.GeneratedCSharp ?? "").Replace(" ", "");
    }

    // The `.Count*(` token (a Count multiplied by another) uniquely marks a product-of-counts presize;
    // a single-source presize is `new List(((ISized)src).Count)` with no multiply.
    private const string ProductPresize = ".Count*(";

    private const string MultiFor =
        "def f(xs: list[int], ys: list[int]) -> int:\n    return len([x + y for x in xs for y in ys])\n";

    [Fact]
    public void Enabled_MultiForAllSized_PresizesToProductOfCounts()
    {
        CompileCSharp(MultiFor, fuse: true).Should().Contain(ProductPresize);
    }

    [Fact]
    public void Disabled_MultiFor_NoProductPresize()
    {
        CompileCSharp(MultiFor, fuse: false).Should().NotContain(ProductPresize);
    }

    [Fact]
    public void Enabled_FilteredMultiFor_NotPresized()
    {
        // A filter would make the product a gross over-reservation, so it is excluded.
        var src = "def f(xs: list[int], ys: list[int]) -> int:\n"
                + "    return len([x + y for x in xs for y in ys if x > 0])\n";
        CompileCSharp(src, fuse: true).Should().NotContain(ProductPresize);
    }

    [Fact]
    public void Enabled_CallSource_NotPresized()
    {
        // range(...) is a call — excluded (conservative v1: variable/attribute sources only).
        var src = "def f(n: int, m: int) -> int:\n"
                + "    return len([i + j for i in range(n) for j in range(m)])\n";
        CompileCSharp(src, fuse: true).Should().NotContain(ProductPresize);
    }
}
