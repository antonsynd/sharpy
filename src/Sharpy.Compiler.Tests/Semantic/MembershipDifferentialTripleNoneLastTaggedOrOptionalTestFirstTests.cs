using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>The operator sequences of three chain links, in the form whose last operand is the literal <c>None</c>,
/// the operands before it typed <c>T?</c> or <c>T | None</c> (the tagged-or-optional half of the none-last form's
/// subject typings), a test link first. The generator, its axes and its allowlist are
/// <see cref="MembershipOperatorDifferentialBase"/>; the fact is declared here so that it runs in this namespace's
/// CI shard (compiler shard 3): the generator's facts do not fit one shard job.</summary>
public sealed class MembershipDifferentialTripleNoneLastTaggedOrOptionalTestFirstTests : MembershipOperatorDifferentialBase
{
    public MembershipDifferentialTripleNoneLastTaggedOrOptionalTestFirstTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OperatorSequences_OfThreeChainLinks_NoneLastTaggedOrOptional_TestFirst_AreTheirDesugaring() => RunSequenceFact();
}
