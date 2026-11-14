using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties;

/// <summary>The operator sequences of three chain links, in the form whose last operand is the literal <c>None</c>, a
/// test link first. The generator, its axes and its allowlist are <see cref="MembershipOperatorDifferentialBase"/>;
/// the facts are declared here so that they run in this namespace's CI shard (compiler shard 5): the generator's
/// facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleNoneLastTestFirstTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleNoneLastTestFirstTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_NoneLast_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
