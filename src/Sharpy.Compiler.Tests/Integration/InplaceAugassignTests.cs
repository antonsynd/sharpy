using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

[Collection("HeavyCompilation")]
public class InplaceAugassignTests : IntegrationTestBase
{
    private static readonly FeatureFlags WithGate =
        FeatureFlags.None.Enable(new[] { "inplace_augassign" });

    public InplaceAugassignTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void UngatedSetUnion_RebindSemantics_AliasSeesSizeTwo()
    {
        var source = @"
def main() -> None:
    s: set[int] = {1, 2}
    t: set[int] = s
    s |= {3}
    print(len(t))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(result.StandardError);
        result.StandardOutput.TrimEnd().Should().Be("2");
    }

    [Fact]
    public void GatedSetUnion_MutationSemantics_AliasSeesSizeThree()
    {
        var source = @"
def main() -> None:
    s: set[int] = {1, 2}
    t: set[int] = s
    s |= {3}
    print(len(t))
";
        var result = CompileAndExecute(source, features: WithGate);
        result.Success.Should().BeTrue(result.StandardError);
        result.StandardOutput.TrimEnd().Should().Be("3");
    }

    // A nullable-narrowed receiver (`x: list[int]?` inside `if x is not None:`) reads through
    // the narrowed-read lowering (x.Unwrap()). The mutation path must apply it too — found as a
    // gated-only SPY0908 (CS1061 on Optional<List<int>>) in the plan-55f329 verify round.
    private const string NullableNarrowedSource = @"
def f(x: list[int]?) -> None:
    if x is not None:
        x += [4]


def main() -> None:
    xs: list[int] = [1, 2, 3]
    f(xs)
    print(len(xs))
";

    [Fact]
    public void UngatedNullableNarrowed_RebindSemantics_CallerUnchanged()
    {
        var result = CompileAndExecute(NullableNarrowedSource);
        result.Success.Should().BeTrue(result.StandardError);
        result.StandardOutput.TrimEnd().Should().Be("3");
    }

    [Fact]
    public void GatedNullableNarrowed_MutationAppliesNarrowedReadLowering_CallerSeesAppend()
    {
        var result = CompileAndExecute(NullableNarrowedSource, features: WithGate);
        result.Success.Should().BeTrue(result.StandardError);
        // CPython prints 4 here (mutation through the callee) — verified with python3 3.12.
        result.StandardOutput.TrimEnd().Should().Be("4");
        result.GeneratedCSharp.Should().Contain(".Unwrap().Extend(");
    }

    // An isinstance-narrowed receiver erases to the non-generic protocol interface (#912), which
    // has no mutation methods — the TypeChecker must NOT materialize a mutation for it (#1615:
    // the shape is a pre-existing rebind ICE in both modes; the gate must not add a second face).
    [Fact]
    public void GatedIsinstanceNarrowed_DoesNotMaterialize_EmitsSameCSharpAsUngated()
    {
        var source = @"
def f(x: object) -> None:
    if isinstance(x, list):
        x += [4]


def main() -> None:
    f([1, 2, 3])
    print(""ok"")
";
        var ungated = CompileAndExecute(source);
        var gated = CompileAndExecute(source, features: WithGate);
        gated.GeneratedCSharp.Should().NotBeNull(
            "the generated C# must be captured even when assembly compilation fails");
        gated.GeneratedCSharp.Should().NotContain(".Extend(");
        // The emitted source embeds a per-compilation temp directory (sharpy_src_<guid>);
        // normalize it so the comparison sees only real codegen differences.
        NormalizeTempPaths(gated.GeneratedCSharp!).Should().Be(
            NormalizeTempPaths(ungated.GeneratedCSharp!));
    }

    private static string NormalizeTempPaths(string generatedCSharp) =>
        System.Text.RegularExpressions.Regex.Replace(
            generatedCSharp, "sharpy_src_[0-9a-f]{32}", "sharpy_src_NORMALIZED");
}
