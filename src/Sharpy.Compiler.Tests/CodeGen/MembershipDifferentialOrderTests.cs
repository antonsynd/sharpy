using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>The membership matrix by its order observation, and the operator sequences whose last operand hoists. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (the seven facts of the generator do not fit one shard job).</summary>
public sealed class MembershipDifferentialOrderTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialOrderTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public void Membership_EveryContainerOperatorPosition_EvaluatesItsOperandsLeftToRight() => RunOrderMatrix();

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_AreTheirDesugaring() => RunSequencesWithAHoistingLastOperand();
}
