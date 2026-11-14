using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>The operator sequences in every further statement position, in the plain and the <c>T | None</c> forms. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 3): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialPositionTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialPositionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Plain_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Optional_AreTheirDesugaring() => RunSequenceFact();
}
