using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Differential;

/// <summary>The operator sequences of three chain links whose last operand hoists behind a walrus. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 7): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleHoistWalrusTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleHoistWalrusTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Walrus_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Walrus_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
