using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>The operator sequences whose last operand hoists behind a comprehension or a spread. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 2): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialHoistTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialHoistTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_Comprehension_AreTheirDesugaring() => RunSequenceFact();

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_Spread_AreTheirDesugaring() => RunSequenceFact();
}
