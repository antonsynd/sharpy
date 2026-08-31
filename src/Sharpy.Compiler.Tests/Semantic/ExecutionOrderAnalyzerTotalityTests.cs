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
    /// Expression types with an EXPLICIT no-op arm in the switch ("Literals don't
    /// reference identifiers"): handled, contributing nothing by design. Reconciled
    /// from the switch when the arms==roster assertion became SetEquals — these five
    /// were rostered as fall-through Leaf while the switch names them (the same
    /// roster/switch disagreement as the CfgStatementTotalityTests DeferStatement tell).
    /// </summary>
    private static readonly HashSet<string> HandledNoOp = new()
    {
        nameof(IntegerLiteral),
        nameof(FloatLiteral),
        nameof(StringLiteral),
        nameof(BooleanLiteral),
        nameof(NoneLiteral),
    };

    /// <summary>
    /// Expression types that are leaf literals with NO arm — they fall through the
    /// switch and reference no identifiers.
    /// </summary>
    private static readonly HashSet<string> Leaf = new()
    {
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
        allClassified.UnionWith(HandledNoOp);
        allClassified.UnionWith(Leaf);
        allClassified.UnionWith(NotReachable);

        var unclassified = concreteExpressions.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteExpressions.Contains(n)).ToList();

        _output.WriteLine($"Concrete Expression subtypes: {concreteExpressions.Count}");
        foreach (var name in concreteExpressions)
        {
            var group = Handled.Contains(name) ? "HANDLED"
                : HandledNoOp.Contains(name) ? "HANDLED-NO-OP"
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

        // SetEquals, not subset: an arm ADDED for a type rostered Leaf/NotReachable is
        // drift the roster must acknowledge, exactly as a deleted Handled arm is.
        var expectedArms = new HashSet<string>(Handled);
        expectedArms.UnionWith(HandledNoOp);
        Assert.True(switchArms.SetEquals(expectedArms),
            $"CollectReferencedIdentifiers switch arms differ from Handled+HandledNoOp.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(expectedArms))}\n" +
            $"  Missing from switch: {string.Join(", ", expectedArms.Except(switchArms))}");
    }

    // ── CollectDeclarationNames (Statement axis) ──

    /// <summary>
    /// Statement types with a case arm in CollectDeclarationNames —
    /// they contribute a name to _typeAndFunctionNames or _constVariables.
    /// </summary>
    private static readonly HashSet<string> DeclCollected = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(FunctionDef),
        nameof(EnumDef),
        nameof(InterfaceDef),
        nameof(VariableDeclaration),
    };

    /// <summary>
    /// Statement types that declare a name but are deliberately not collected
    /// because they are future-syntax, experimental, or handled by other passes.
    /// Each entry needs a justification string.
    /// </summary>
    private static readonly Dictionary<string, string> DeclDeclaresNameNotCollected = new()
    {
        [nameof(TypeAlias)] = "type alias names resolved by a separate alias pass",
        [nameof(UnionDef)] = "future syntax — discriminated unions resolved separately",
        [nameof(DelegateDef)] = "future syntax — delegate types resolved separately",
        [nameof(EventDef)] = "future syntax — events resolved separately",
        // A DecoratedStatement wrapping a FunctionDef/ClassDef DOES declare the wrapped name
        // (DetectBasicIssues unwraps it; CollectDeclarationNames does not). Probed benign at
        // f77a775d5 (plan-e31e76 verify round): a @suppress-decorated module-level def
        // referenced from an earlier initializer compiles and runs identically to its
        // undecorated control — the symbol-table fallback in DetectUseBeforeDefinition masks
        // the uncollected name — and the const arm is unreachable because SPY0322 refuses
        // decorators on module-level variable declarations.
        [nameof(DecoratedStatement)] = "wrapped def/class name uncollected but masked by the "
            + "symbol-table fallback (probed); const arm unreachable via SPY0322",
    };

    /// <summary>
    /// Statement types that do not declare a module-level name —
    /// the default fall-through is correct for them.
    /// </summary>
    private static readonly HashSet<string> DeclNoDeclaredName = new()
    {
        nameof(ExpressionStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(BreakWithFlagStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(MatchStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(PropertyDef),
    };

    [Fact]
    public void AllConcreteStatementSubtypes_AreClassifiedForDeclarations()
    {
        var statementBaseType = typeof(Statement);
        var concrete = statementBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementBaseType) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .ToHashSet();

        var allClassified = new HashSet<string>(DeclCollected);
        allClassified.UnionWith(DeclDeclaresNameNotCollected.Keys);
        allClassified.UnionWith(DeclNoDeclaredName);

        var unclassified = concrete.Except(allClassified).OrderBy(n => n).ToList();
        var phantom = allClassified.Except(concrete).OrderBy(n => n).ToList();

        if (unclassified.Count > 0)
            _output.WriteLine($"Unclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"Phantom: {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void CollectDeclarationNames_SwitchArms_MatchCollectedClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs",
            "CollectDeclarationNames");

        Assert.True(switchArms.SetEquals(DeclCollected),
            $"CollectDeclarationNames switch arms differ from DeclCollected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(DeclCollected))}\n" +
            $"  Missing from switch: {string.Join(", ", DeclCollected.Except(switchArms))}");
    }

    [Fact]
    public void DeclarationClassificationSets_AreDisjoint()
    {
        var overlap1 = DeclCollected.Intersect(DeclDeclaresNameNotCollected.Keys).ToList();
        var overlap2 = DeclCollected.Intersect(DeclNoDeclaredName).ToList();
        var overlap3 = DeclDeclaresNameNotCollected.Keys.Intersect(DeclNoDeclaredName).ToList();

        Assert.Empty(overlap1);
        Assert.Empty(overlap2);
        Assert.Empty(overlap3);
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var handledAndLeaf = Handled.Intersect(Leaf).ToList();
        var handledAndNr = Handled.Intersect(NotReachable).ToList();
        var leafAndNr = Leaf.Intersect(NotReachable).ToList();
        var noOpOverlap = HandledNoOp.Intersect(Handled)
            .Concat(HandledNoOp.Intersect(Leaf))
            .Concat(HandledNoOp.Intersect(NotReachable)).ToList();

        Assert.Empty(handledAndLeaf);
        Assert.Empty(handledAndNr);
        Assert.Empty(leafAndNr);
        Assert.Empty(noOpOverlap);
    }
}
