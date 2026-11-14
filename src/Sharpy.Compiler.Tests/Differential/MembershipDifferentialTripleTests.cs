using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Differential;

/// <summary>The operator sequences of three chain links, in the form tagged on its first operand alone (the other forms have a class each, in the other shards). The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 7): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
