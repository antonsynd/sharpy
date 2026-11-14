using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>The operator sequences in every further statement position, in the two tagged forms. The generator, its
/// axes and its allowlist are <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here, in a
/// class whose name starts with P, so that they run in CI's compiler shard 2 (the Integration classes whose name
/// starts P, S, T, U or V): the generator's facts do not fit one shard job.</summary>
public sealed class PositionTaggedMembershipTests : MembershipOperatorDifferentialBase
{
    public PositionTaggedMembershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Tagged_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_TaggedLeft_AreTheirDesugaring() => RunSequenceFact();
}
