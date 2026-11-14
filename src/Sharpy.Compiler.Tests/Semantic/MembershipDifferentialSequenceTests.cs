using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>The operator sequences, and their pivots in every other form. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 3): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialSequenceTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialSequenceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OverEffectfulOperands_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_WithAPivotOperandInEveryOtherForm_AreTheirDesugaring() => RunSequenceFact();
}
