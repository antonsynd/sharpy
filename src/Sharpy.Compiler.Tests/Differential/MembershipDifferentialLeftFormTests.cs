using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Differential;

/// <summary>The left-operand forms. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the facts are declared here so that they run in this
/// namespace's CI shard (compiler shard 7): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialLeftFormTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialLeftFormTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Membership_EveryLeftOperandForm_IsItsDesugaring() => RunLeftOperandForms();
}
