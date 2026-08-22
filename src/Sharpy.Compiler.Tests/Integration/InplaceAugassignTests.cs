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
}
