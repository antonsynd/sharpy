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

    // An isinstance-narrowed receiver now produces SPY0276 in both gate modes (#1615).
    [Fact]
    public void IsinstanceNarrowed_RefusedWithSPY0276_BothGateModes()
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

        ungated.Success.Should().BeFalse();
        gated.Success.Should().BeFalse();

        ungated.CompilationErrors.Should().Contain(e =>
            e.Contains("SPY0276") || e.Contains("narrowed receiver"));
        gated.CompilationErrors.Should().Contain(e =>
            e.Contains("SPY0276") || e.Contains("narrowed receiver"));
    }
}
