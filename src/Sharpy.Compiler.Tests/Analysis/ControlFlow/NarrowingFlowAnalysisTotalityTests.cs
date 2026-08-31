using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Totality guard for <c>NarrowingConditionInterpreter.Recognize</c>,
/// <c>NarrowingConditionInterpreter.RecognizeLeaf</c>, and
/// <c>NarrowingFlowAnalysis.CollectAssignedKeys</c>: every concrete
/// <see cref="Expression"/> subtype must be classified as recognized (has a dedicated
/// arm in one of the two interpreter methods) or unrecognized-by-design (falls through
/// to the default, returning an empty sequence or delegating to RecognizeLeaf); the
/// CollectAssignedKeys recursion arms are cross-checked against its switch (its default
/// arm contributes via <c>AstHelper.ExtractNarrowingKey</c> by design). A new
/// Expression subtype that is not listed here will fail this test (#1694).
/// </summary>
public class NarrowingFlowAnalysisTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Analysis/ControlFlow/NarrowingFlowAnalysis.cs";

    private readonly ITestOutputHelper _output;

    public NarrowingFlowAnalysisTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> RecognizedByRecognize = new()
    {
        nameof(Parenthesized),
        nameof(UnaryOp),
        nameof(BinaryOp),
    };

    private static readonly HashSet<string> RecognizedByRecognizeLeaf = new()
    {
        nameof(BinaryOp),
        nameof(FunctionCall),
    };

    private static readonly HashSet<string> UnrecognizedByDesign = new()
    {
        nameof(AwaitExpression),
        nameof(IntegerLiteral),
        nameof(FloatLiteral),
        nameof(StringLiteral),
        nameof(BytesLiteralExpression),
        nameof(FStringLiteral),
        nameof(TStringLiteral),
        nameof(BooleanLiteral),
        nameof(NoneLiteral),
        nameof(EllipsisLiteral),
        nameof(ListLiteral),
        nameof(DictLiteral),
        nameof(SetLiteral),
        nameof(TupleLiteral),
        nameof(ListComprehension),
        nameof(SetComprehension),
        nameof(DictComprehension),
        nameof(DictSpreadComprehension),
        nameof(Identifier),
        nameof(MemberAccess),
        nameof(IndexAccess),
        nameof(MatchExpression),
        nameof(SliceAccess),
        nameof(MultiAxisAccess),
        nameof(QuestionMarkExpression),
        nameof(ComparisonChain),
        nameof(ConditionalExpression),
        nameof(LambdaExpression),
        nameof(ModifiedArgument),
        nameof(TypeCoercion),
        nameof(TypeCheck),
        nameof(SuperExpression),
        nameof(WalrusExpression),
        nameof(TryExpression),
        nameof(MaybeExpression),
        nameof(StarExpression),
        nameof(SpreadElement),
    };

    [Fact]
    public void AllConcreteExpressionSubtypes_AreClassified()
    {
        var expressionBaseType = typeof(Expression);
        var assembly = expressionBaseType.Assembly;

        var concreteExpressions = assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(expressionBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var allRecognized = new HashSet<string>(RecognizedByRecognize);
        allRecognized.UnionWith(RecognizedByRecognizeLeaf);

        var allClassified = new HashSet<string>(allRecognized);
        allClassified.UnionWith(UnrecognizedByDesign);

        var unclassified = concreteExpressions.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteExpressions.Contains(n)).ToList();

        _output.WriteLine($"Concrete Expression subtypes: {concreteExpressions.Count}");
        foreach (var name in concreteExpressions)
        {
            var group = RecognizedByRecognize.Contains(name) ? "RECOGNIZE"
                : RecognizedByRecognizeLeaf.Contains(name) ? "RECOGNIZE-LEAF"
                : UnrecognizedByDesign.Contains(name) ? "UNRECOGNIZED-BY-DESIGN"
                : "*** UNCLASSIFIED ***";
            _output.WriteLine($"  {name,-30} {group}");
        }

        if (unclassified.Count > 0)
            _output.WriteLine($"\nUnclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom (listed but not found): {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void Recognize_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "Recognize");
        Assert.NotEmpty(switchArms);

        var missing = RecognizedByRecognize.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"RecognizedByRecognize types missing from Recognize switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(RecognizedByRecognize),
            $"Recognize switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(RecognizedByRecognize))}\n" +
            $"  Missing from switch: {string.Join(", ", RecognizedByRecognize.Except(switchArms))}");
    }

    [Fact]
    public void RecognizeLeaf_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "RecognizeLeaf");
        Assert.NotEmpty(switchArms);

        var missing = RecognizedByRecognizeLeaf.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"RecognizedByRecognizeLeaf types missing from RecognizeLeaf switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(RecognizedByRecognizeLeaf),
            $"RecognizeLeaf switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(RecognizedByRecognizeLeaf))}\n" +
            $"  Missing from switch: {string.Join(", ", RecognizedByRecognizeLeaf.Except(switchArms))}");
    }

    /// <summary>
    /// Recursion-shell arms of <c>CollectAssignedKeys</c> (NarrowingFlowAnalysis.cs): every
    /// other Expression falls to the default arm, which contributes via
    /// <c>AstHelper.ExtractNarrowingKey</c> — that fallthrough is the design, so the guard
    /// pins exactly the dedicated arms.
    /// </summary>
    private static readonly HashSet<string> RecursedByCollectAssignedKeys = new()
    {
        nameof(Parenthesized),
        nameof(TupleLiteral),
    };

    [Fact]
    public void CollectAssignedKeys_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "CollectAssignedKeys");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(RecursedByCollectAssignedKeys),
            $"CollectAssignedKeys switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(RecursedByCollectAssignedKeys))}\n" +
            $"  Missing from switch: {string.Join(", ", RecursedByCollectAssignedKeys.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint_WithUnrecognized()
    {
        var allRecognized = new HashSet<string>(RecognizedByRecognize);
        allRecognized.UnionWith(RecognizedByRecognizeLeaf);

        var overlap = allRecognized.Intersect(UnrecognizedByDesign).ToList();
        Assert.Empty(overlap);
    }
}
