using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>The operator sequences of three chain links whose last operand hoists behind a spread, a comparison link
/// first. The generator, its axes and its allowlist are <see cref="MembershipOperatorDifferentialBase"/>; the facts
/// are declared here so that they run in this namespace's CI shard (compiler shard 1): the generator's facts do not
/// fit one shard job.</summary>
public sealed class MembershipDifferentialTripleHoistSpreadComparisonFirstTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleHoistSpreadComparisonFirstTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Spread_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();
}
