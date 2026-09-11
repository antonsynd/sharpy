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

    // ── the generator-expression kind (#1774) ───────────────────────────────────────────────

    private const string MultiForGenerator =
        "def f(xs: list[int], ys: list[int]) -> int:\n"
        + "    return sum(x + y for x in xs for y in ys)\n";

    /// <summary>
    /// The pass SKIPS <c>IrLoweredGenerator</c>, and that is the right answer rather than an
    /// oversight: presizing is a materialization decision, and a generator expression materializes
    /// nothing — it is a deferred LINQ chain. Design Decision 5 required a test that states which of
    /// "handles" or "skips" the pass does for the new kind, so this is it.
    /// </summary>
    /// <remarks>
    /// The ASSERTION is a comparison, not an absence: the same program under the flag and without it
    /// must produce byte-identical C#. An absence assertion ("no product presize") would pass
    /// vacuously for any reason at all, including a generator that stopped being lowered; the
    /// positive control that the flag DOES change output on a comprehension is
    /// <see cref="Enabled_MultiForAllSized_PresizesToProductOfCounts"/> on the same shape of program.
    /// </remarks>
    [Fact]
    public void GeneratorExpression_IsSkippedByThePass_OutputIsIdenticalWithAndWithoutTheFlag()
    {
        var fused = CompileCSharp(MultiForGenerator, fuse: true);
        var plain = CompileCSharp(MultiForGenerator, fuse: false);

        fused.Should().Be(plain,
            "a generator expression materializes no collection, so there is nothing for the "
            + "presizing pass to size — it must pass through unchanged");
        fused.Should().NotContain(ProductPresize,
            "and in particular it is never presized");
        fused.Should().Contain("SelectMany",
            "positive control: the two-for generator really did lower to a nested LINQ chain, so "
            + "the equality above compared a generator program rather than an empty one");
    }
}
