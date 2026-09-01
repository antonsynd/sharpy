using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Shared;

/// <summary>
/// Totality guard for <see cref="Sharpy.Compiler.Shared.AstHelper.ExtractNarrowingKey"/>
/// and <c>ExtractIndexComponentKey</c>: pins the switch-expression arm patterns so that
/// adding or removing an arm fails this test, forcing deliberate classification (#1716).
/// </summary>
public class NarrowingKeyTotalityTests
{
    private const string SourceFile = "src/Sharpy.Compiler/Shared/AstHelper.cs";

    private readonly ITestOutputHelper _output;

    public NarrowingKeyTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] ExtractNarrowingKeyArmPatterns =
    {
        "Identifier id",
        "IndexAccess indexAccess",
        "MemberAccess ma",
        "_",
    };

    private static readonly string[] ExtractIndexComponentKeyArmPatterns =
    {
        "IntegerLiteral intLit",
        "UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negLit }",
        "StringLiteral strLit",
        "_",
    };

    [Fact]
    public void ExtractNarrowingKey_Arms_MatchPinnedPatterns()
    {
        var arms = SwitchArmScan.ArmPatternTexts(SourceFile, "ExtractNarrowingKey");
        Assert.NotEmpty(arms);

        foreach (var arm in arms)
            _output.WriteLine($"  {arm}");

        Assert.Equal(
            ExtractNarrowingKeyArmPatterns.OrderBy(x => x, StringComparer.Ordinal),
            arms.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ExtractIndexComponentKey_Arms_MatchPinnedPatterns()
    {
        var arms = SwitchArmScan.ArmPatternTexts(SourceFile, "ExtractIndexComponentKey");
        Assert.NotEmpty(arms);

        foreach (var arm in arms)
            _output.WriteLine($"  {arm}");

        Assert.Equal(
            ExtractIndexComponentKeyArmPatterns.OrderBy(x => x, StringComparer.Ordinal),
            arms.OrderBy(x => x, StringComparer.Ordinal));
    }
}
