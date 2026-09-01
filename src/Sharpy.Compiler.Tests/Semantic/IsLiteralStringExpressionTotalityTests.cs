using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>TypeChecker.IsLiteralStringExpression</c>: the expression switch
/// dispatches on AST node kinds. Every arm pattern text must appear in the documented
/// roster or the discard catch-all. A new arm that is not listed here will fail (#1716).
/// </summary>
public class IsLiteralStringExpressionTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.Calls.cs";

    private readonly ITestOutputHelper _output;

    public IsLiteralStringExpressionTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> AcceptArms = new()
    {
        "StringLiteral",
        "BinaryOp { Operator: BinaryOperator.Add, Left: var left, Right: var right }",
    };

    private static readonly HashSet<string> RefusalArms = new()
    {
        "_",
    };

    [Fact]
    public void ArmPatterns_MatchRoster()
    {
        var armTexts = SwitchArmScan.ArmPatternTexts(SourceFile, "IsLiteralStringExpression");
        Assert.NotEmpty(armTexts);

        var allExpected = new HashSet<string>(AcceptArms);
        allExpected.UnionWith(RefusalArms);

        var armSet = new HashSet<string>(armTexts);
        var unexpected = armSet.Where(a => !allExpected.Contains(a)).ToList();
        var missing = allExpected.Where(a => !armSet.Contains(a)).ToList();

        _output.WriteLine($"IsLiteralStringExpression arm patterns: {armTexts.Count}");
        foreach (var text in armTexts)
        {
            var group = AcceptArms.Contains(text) ? "ACCEPT"
                : RefusalArms.Contains(text) ? "REFUSAL"
                : "*** UNEXPECTED ***";
            _output.WriteLine($"  {text,-70} {group}");
        }

        if (unexpected.Count > 0)
            _output.WriteLine($"\nUnexpected: {string.Join(", ", unexpected)}");
        if (missing.Count > 0)
            _output.WriteLine($"\nMissing: {string.Join(", ", missing)}");

        Assert.Empty(unexpected);
        Assert.Empty(missing);
    }

    [Fact]
    public void AcceptAndRefusal_AreDisjoint()
    {
        var overlap = AcceptArms.Intersect(RefusalArms).ToList();
        Assert.Empty(overlap);
    }
}
