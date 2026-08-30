using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <see cref="Sharpy.Compiler.Semantic.ExecutionOrderAnalyzer"/>:
/// <c>CollectReferencedIdentifiers</c> dispatches on Expression subtypes. Every concrete
/// Expression subtype must be classified here. A new subtype that is not listed fails this
/// test, forcing deliberate classification.
/// </summary>
public class ExecutionOrderAnalyzerTotalityTests
{
    private readonly ITestOutputHelper _output;

    public ExecutionOrderAnalyzerTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Expression types handled by CollectReferencedIdentifiers — they contain sub-expressions
    /// or identifiers that are recursed into.
    /// </summary>
    private static readonly HashSet<string> Handled = new()
    {
        nameof(Identifier),
        nameof(BinaryOp),
        nameof(UnaryOp),
        nameof(FunctionCall),
        nameof(MemberAccess),
        nameof(IndexAccess),
        nameof(SliceAccess),
        nameof(ConditionalExpression),
        nameof(Parenthesized),
        nameof(ListLiteral),
        nameof(DictLiteral),
        nameof(SetLiteral),
        nameof(TupleLiteral),
        nameof(LambdaExpression),
        nameof(ListComprehension),
        nameof(SetComprehension),
        nameof(DictComprehension),
        nameof(DictSpreadComprehension),
        nameof(ComparisonChain),
        nameof(FStringLiteral),
        nameof(TStringLiteral),
    };

    /// <summary>
    /// Expression types that are leaf literals and don't reference any identifiers.
    /// </summary>
    private static readonly HashSet<string> Leaf = new()
    {
        nameof(IntegerLiteral),
        nameof(FloatLiteral),
        nameof(StringLiteral),
        nameof(BooleanLiteral),
        nameof(NoneLiteral),
        nameof(EllipsisLiteral),
        nameof(BytesLiteralExpression),
        nameof(SuperExpression),
    };

    /// <summary>
    /// Expression types that are not reached from module-level variable initializers
    /// (the only context ExecutionOrderAnalyzer runs in) — they are either statement-scoped
    /// (walrus, try/maybe), require a function body (yield, star/spread), or are synthetic
    /// nodes the parser doesn't produce in an initializer position.
    /// </summary>
    private static readonly HashSet<string> NotReachable = new()
    {
        nameof(WalrusExpression),
        nameof(TryExpression),
        nameof(MaybeExpression),
        nameof(StarExpression),
        nameof(SpreadElement),
        nameof(ModifiedArgument),
        nameof(TypeCoercion),
        nameof(TypeCheck),
        nameof(QuestionMarkExpression),
        nameof(MultiAxisAccess),
        nameof(AwaitExpression),
        nameof(MatchExpression),
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

        var allClassified = new HashSet<string>(Handled);
        allClassified.UnionWith(Leaf);
        allClassified.UnionWith(NotReachable);

        var unclassified = concreteExpressions.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteExpressions.Contains(n)).ToList();

        _output.WriteLine($"Concrete Expression subtypes: {concreteExpressions.Count}");
        foreach (var name in concreteExpressions)
        {
            var group = Handled.Contains(name) ? "HANDLED"
                : Leaf.Contains(name) ? "LEAF"
                : NotReachable.Contains(name) ? "NOT-REACHABLE"
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
    public void SwitchArms_MatchHandledClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs",
            "CollectReferencedIdentifiers");

        Assert.NotEmpty(switchArms);

        var handledNotInSwitch = Handled.Except(switchArms).ToList();
        Assert.True(handledNotInSwitch.Count == 0,
            $"Handled types missing from switch: {string.Join(", ", handledNotInSwitch)}");

        var allClassified = new HashSet<string>(Handled);
        allClassified.UnionWith(Leaf);
        allClassified.UnionWith(NotReachable);
        var unclassifiedArms = switchArms.Except(allClassified).ToList();
        Assert.True(unclassifiedArms.Count == 0,
            $"Switch arms not classified: {string.Join(", ", unclassifiedArms)}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var handledAndLeaf = Handled.Intersect(Leaf).ToList();
        var handledAndNr = Handled.Intersect(NotReachable).ToList();
        var leafAndNr = Leaf.Intersect(NotReachable).ToList();

        Assert.Empty(handledAndLeaf);
        Assert.Empty(handledAndNr);
        Assert.Empty(leafAndNr);
    }
}
