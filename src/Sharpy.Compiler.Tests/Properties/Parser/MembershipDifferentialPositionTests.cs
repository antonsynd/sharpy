using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

/// <summary>The operator sequences in every further statement position, a fact per form. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 4): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialPositionTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialPositionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Plain_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Optional_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_Tagged_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_TaggedLeft_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_NoneLast_AreTheirDesugaring() => RunSequenceFact();
}
