using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>The operator sequences of three chain links, in the form tagged on its first operand alone. The
/// generator, its axes and its allowlist are <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared
/// here, in a class whose name starts with T, so that they run in CI's compiler shard 2 (the Integration classes
/// whose name starts P, S, T, U or V): the generator's facts do not fit one shard job.</summary>
public sealed class TripleTaggedLeftMembershipTests : MembershipOperatorDifferentialBase
{
    public TripleTaggedLeftMembershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_TaggedLeft_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
