using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>The operator sequences whose last operand hoists behind a walrus. The generator, its axes and its
/// allowlist are <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in
/// this namespace's CI shard (compiler shard 9): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialHoistWalrusTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialHoistWalrusTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_WithAHoistingLastOperand_Walrus_AreTheirDesugaring() => RunSequenceFact();
}
