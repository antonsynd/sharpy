using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>The operator sequences of three chain links whose last operand hoists behind a comprehension. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 2): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleHoistComprehensionTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleHoistComprehensionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Comprehension_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Comprehension_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
