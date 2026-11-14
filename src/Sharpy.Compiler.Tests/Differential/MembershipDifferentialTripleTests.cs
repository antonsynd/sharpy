using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Differential;

/// <summary>The operator sequences of three chain links. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (the seven facts of the generator do not fit one shard job).</summary>
public sealed class MembershipDifferentialTripleTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_AreTheirDesugaring() => RunSequencesOfThreeChainLinks();
}
