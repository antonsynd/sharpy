using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>The operator sequences of three chain links, in the form tagged on its first operand alone. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 1): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleTaggedLeftTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleTaggedLeftTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
