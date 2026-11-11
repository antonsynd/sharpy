using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// A bracket-attribute argument may be a constant expression built with <c>==</c>/<c>!=</c>
/// (<c>ConstEligibility</c> admits them as operand-type-independent native operators). The emitter
/// reads an equality's lowering from the IR, so a decorator argument is a position lowering must
/// visit, or the required read throws SPY0909 on a valid program (#2443).
/// </summary>
public class BracketAttributeComparisonArgumentTests : IntegrationTestBase
{
    public BracketAttributeComparisonArgumentTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("@[obsolete(\"old\", 1 == 2)]\ndef f() -> int:\n    return 1\n", "1 == 2")]
    [InlineData("@[obsolete(\"a\" if 1 != 2 else \"b\")]\ndef f() -> int:\n    return 1\n", "1 != 2")]
    [InlineData("@[obsolete(\"c\" if 3 == 3 else \"d\")]\nclass K:\n    pass\n", "3 == 3")]
    public void EqualityInAttributeArgument_CompilesAndRuns(string declaration, string emittedComparison)
    {
        var source = declaration + "\ndef main() -> None:\n    print(\"ok\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary());
        result.StandardOutput.Trim().Should().Be("ok");
        result.GeneratedCSharp.Should().Contain(emittedComparison,
            "the comparison is a C# constant expression in the attribute argument");
    }
}
