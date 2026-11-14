using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>The operator sequences of one operator and of two chain links. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 3): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialSequenceTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialSequenceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfOneOperator_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfTwoChainLinks_AreTheirDesugaring() => RunSequenceFact();
}
