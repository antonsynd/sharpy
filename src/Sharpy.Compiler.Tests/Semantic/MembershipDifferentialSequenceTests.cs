using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>The operator sequences and their further statement positions. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (the seven facts of the generator do not fit one shard job).</summary>
public sealed class MembershipDifferentialSequenceTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialSequenceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OverEffectfulOperands_AreTheirDesugaring() => RunSequences();

    [Fact]
    public void OperatorSequences_InEveryStatementPosition_AreTheirDesugaring() => RunSequencePositions();
}
