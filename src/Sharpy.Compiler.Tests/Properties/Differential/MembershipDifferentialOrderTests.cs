using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>The order matrix, by needle group. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 5): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialOrderTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialOrderTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public void PlainNeedle_EveryContainerOperatorPosition_EvaluatesItsOperandsLeftToRight() => RunOrderMatrix(plainNeedle: true);

    [SkippableFact]
    public void OptionalNeedle_EveryContainerOperatorPosition_EvaluatesItsOperandsLeftToRight() => RunOrderMatrix(plainNeedle: false);
}
