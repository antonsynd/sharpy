using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>The operator sequences of three chain links whose last operand hoists behind a comprehension, a
/// comparison link first. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here, in a class whose name starts with
/// T, so that they run in CI's compiler shard 2 (the Integration classes whose name starts P, S, T, U or V): the
/// generator's facts do not fit one shard job.</summary>
public sealed class TripleHoistComprehensionComparisonFirstMembershipTests : MembershipOperatorDifferentialBase
{
    public TripleHoistComprehensionComparisonFirstMembershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinksAndAHoistingLastOperand_Comprehension_ComparisonFirst_AreTheirDesugaring() => RunSequenceFact();
}
