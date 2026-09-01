using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

public class ConstantCycleDetectionTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/TypeChecker.cs";

    private readonly ITestOutputHelper _output;

    public ConstantCycleDetectionTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> ExpectedArms = new()
    {
        "Identifier",
        "UnaryOp",
        "BinaryOp",
        "Parenthesized",
    };

    [Fact]
    public void ReferencesUnfoldedConst_SwitchArms_MatchExpected()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ReferencesUnfoldedConst");
        Assert.NotEmpty(switchArms);

        _output.WriteLine($"Switch arms found: {switchArms.Count}");
        foreach (var arm in switchArms.OrderBy(a => a, StringComparer.Ordinal))
            _output.WriteLine($"  {arm}");

        Assert.True(switchArms.SetEquals(ExpectedArms),
            $"ReferencesUnfoldedConst switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(ExpectedArms))}\n" +
            $"  Missing from switch: {string.Join(", ", ExpectedArms.Except(switchArms))}");
    }
}
