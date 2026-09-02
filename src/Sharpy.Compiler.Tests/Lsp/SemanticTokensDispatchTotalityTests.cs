using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>SemanticTokensHandler</c>'s three dispatch switches, run through
/// <see cref="LspDispatchTotality"/>: arms == roster, roster ∪ justified-default == the
/// reflection universe, disjoint, phantom-free, every default tagged with its reason class.
/// The representative justified-default kinds are probed through the real collector in
/// <c>Sharpy.Lsp.Tests.SemanticTokensTests</c> (<c>PassStatement_YieldsNoToken_WhileSiblingCallDoes</c>,
/// <c>IntegerLiteral_YieldsNoToken_WhileSiblingStringDoes</c>) — each absence next to a
/// positive control on the same input.
/// Mutation (worktree @ 277f54543 + this change): DeferStatement arm deleted from
/// CollectStatementTokens → red (1 failed, 2 passed); restored → green (3 passed).
/// </summary>
public class SemanticTokensDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public SemanticTokensDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    // ── CollectStatementTokens: Statement universe ──

    private static readonly HashSet<string> CollectStatementTokensArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
        nameof(IfStatement),
        nameof(ForStatement),
        nameof(WhileStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(MatchStatement),
        nameof(ExpressionStatement),
        nameof(ReturnStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(RaiseStatement),
        nameof(YieldStatement),
        nameof(DecoratedStatement),
        // Phase 2 remediation: the deferred suite, the alias name, and the union / delegate /
        // event declaration heads were silent (no tokens → tokens; cells in SemanticTokensTests).
        nameof(DeferStatement),
        nameof(TypeAlias),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
    };

    private static readonly Dictionary<string, string> CollectStatementTokensDefault = new()
    {
        [nameof(BreakStatement)] = "CONTRACTUAL: keyword-only statement — the legend carries no keyword token for plain keywords; the client grammar colors them",
        [nameof(ContinueStatement)] = "CONTRACTUAL: keyword-only statement — the client grammar colors it",
        [nameof(PassStatement)] = "CONTRACTUAL: keyword-only statement — the client grammar colors it",
        [nameof(BreakWithFlagStatement)] = "UNREACHABLE: emitter-synthesized (never parsed) — it has no source position to tokenize",
        [nameof(ImportStatement)] = "CONTRACTUAL: the legend has no namespace/module token type; imported names are left to the client grammar",
        [nameof(FromImportStatement)] = "CONTRACTUAL: the legend has no namespace/module token type; imported names are left to the client grammar",
    };

    [Fact]
    public void CollectStatementTokens_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectStatementTokens",
            typeof(Statement),
            CollectStatementTokensArms,
            CollectStatementTokensDefault);
    }

    // ── CollectExpressionTokens: Expression universe ──

    private static readonly HashSet<string> CollectExpressionTokensArms = new()
    {
        nameof(UnaryOp),
        nameof(BinaryOp),
        nameof(ComparisonChain),
        nameof(Identifier),
        nameof(ConditionalExpression),
        nameof(FunctionCall),
        nameof(MemberAccess),
        nameof(IndexAccess),
        nameof(SliceAccess),
        nameof(MultiAxisAccess),
        nameof(ListLiteral),
        nameof(DictLiteral),
        nameof(SetLiteral),
        nameof(TupleLiteral),
        nameof(ListComprehension),
        nameof(SetComprehension),
        nameof(DictComprehension),
        nameof(Parenthesized),
        nameof(LambdaExpression),
        nameof(TypeCoercion),
        nameof(TypeCheck),
        nameof(WalrusExpression),
        nameof(FStringLiteral),
        nameof(TStringLiteral),
        nameof(TryExpression),
        nameof(MaybeExpression),
        nameof(QuestionMarkExpression),
        nameof(StarExpression),
        nameof(SpreadElement),
        nameof(StringLiteral),
        nameof(BytesLiteralExpression),
        nameof(ModifiedArgument),
        // Phase 2 remediation: operands of await / match-expression arms / dict-spread
        // comprehensions were never walked (no tokens → tokens; cells in SemanticTokensTests).
        nameof(AwaitExpression),
        nameof(MatchExpression),
        nameof(DictSpreadComprehension),
    };

    /// <summary>
    /// The BinaryOp arm's positional fallback switches on <c>BinaryOperator</c>; the scanner
    /// reads those constant labels as names. They are operator sub-arms, not Expression kinds,
    /// and stay out of the kind universe explicitly.
    /// </summary>
    private static readonly HashSet<string> CollectExpressionTokensOperatorSubArms = new()
    {
        "And", "Or", "In", "NotIn", "Is", "IsNot",
    };

    private static readonly Dictionary<string, string> CollectExpressionTokensDefault = new()
    {
        [nameof(BooleanLiteral)] = "CONTRACTUAL: literal colored by the client grammar; TNumber is registered in the legend but the handler pushes no numeric/keyword-literal token",
        [nameof(EllipsisLiteral)] = "CONTRACTUAL: literal colored by the client grammar",
        [nameof(FloatLiteral)] = "CONTRACTUAL: literal colored by the client grammar (TNumber registered, unused)",
        [nameof(IntegerLiteral)] = "CONTRACTUAL: literal colored by the client grammar (TNumber registered, unused)",
        [nameof(NoneLiteral)] = "CONTRACTUAL: literal colored by the client grammar",
        [nameof(SuperExpression)] = "CONTRACTUAL: the `super` keyword is colored by the client grammar; the node has no sub-expression",
    };

    [Fact]
    public void CollectExpressionTokens_ArmsPlusJustifiedDefaults_EqualTheExpressionUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectExpressionTokens",
            typeof(Expression),
            CollectExpressionTokensArms,
            CollectExpressionTokensDefault,
            CollectExpressionTokensOperatorSubArms);
    }

    // ── CollectComprehensionClauseTokens: ComprehensionClause universe ──

    private static readonly HashSet<string> CollectComprehensionClauseTokensArms = new()
    {
        nameof(ForClause),
        nameof(IfClause),
    };

    [Fact]
    public void CollectComprehensionClauseTokens_Arms_EqualTheComprehensionClauseUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectComprehensionClauseTokens",
            typeof(ComprehensionClause),
            CollectComprehensionClauseTokensArms,
            new Dictionary<string, string>());
    }
}
