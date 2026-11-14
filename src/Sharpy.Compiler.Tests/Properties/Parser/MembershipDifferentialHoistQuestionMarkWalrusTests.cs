using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

/// <summary>The operator sequences whose last operand hoists behind <c>?</c> or a walrus. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 4): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialHoistQuestionMarkWalrusTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialHoistQuestionMarkWalrusTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_QuestionMark_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_Walrus_AreTheirDesugaring() => RunSequenceFact();
}
