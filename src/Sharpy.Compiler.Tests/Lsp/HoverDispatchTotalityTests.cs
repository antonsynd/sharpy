using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>HoverService</c>'s five dispatch switches, run through
/// <see cref="LspDispatchTotality"/>. Hover resolves on the innermost node under the cursor
/// (<c>AstPositionService.FindInnermostNode</c> over <c>GetChildNodes</c>), so a container
/// node is reached only on its own keyword/punctuation — its operands are children and answer
/// for themselves — and every Expression kind without an arm of its own falls to the
/// <c>case Expression</c> base arm (type hover). The representative justified-default kinds are
/// probed in <c>Sharpy.Lsp.Tests.HoverServiceTests</c> (<c>PassStatement_YieldsNoHover_WhileSiblingReturnDoes</c>,
/// <c>WalrusExpression_FallsToTheExpressionArm</c>), each absence next to a positive control on
/// the same input; the pattern-head gaps with no recorded name extent are #1735.
/// Mutation (worktree @ 277f54543 + this change): TypeAnnotation arm deleted from
/// GetHoverMarkdownForNode → red (1 failed, 4 passed); restored → green (5 passed).
/// </summary>
public class HoverDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public HoverDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private const string Synthesized = "UNREACHABLE: emitter-synthesized (never parsed)";
    private const string KeywordOnly = "CONTRACTUAL: keyword-only statement — no operand to describe";
    private const string ContainerStatement =
        "CONTRACTUAL: container statement — every operand is a child node, so the innermost-node search lands on the child; the statement itself is under the cursor only on its keyword/punctuation, where there is nothing to describe";
    private const string ExpressionBaseArm =
        "BASE-ARM: falls to the `case Expression` arm — hover shows the expression's effective type";
    private const string StructuralPattern =
        "CONTRACTUAL: structural pattern — its sub-patterns are children and answer for themselves; the node itself is under the cursor only on punctuation";

    // ── GetHoverMarkdownForNode: Node universe (94) ──

    private static readonly HashSet<string> GetHoverMarkdownArms = new()
    {
        nameof(Identifier),
        nameof(MemberAccess),
        nameof(FunctionCall),
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(WithStatement),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(AwaitExpression),
        nameof(YieldStatement),
        nameof(ReturnStatement),
        nameof(SuperExpression),
        nameof(BinaryOp),
        nameof(UnaryOp),
        nameof(ComparisonChain),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(LambdaExpression),
        // Phase 2 remediation (no hover → hover; cells in HoverServiceTests): the union /
        // delegate / event declaration heads, the `except … as name` binding, a type annotation
        // reached as its own node (TypePattern exposes it), and the type head of a class /
        // property / union-case pattern.
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
        nameof(TryStatement),
        nameof(TypeAnnotation),
        nameof(PropertyPattern),
    };

    /// <summary>
    /// <c>Expression</c> is the abstract base arm (the typed catch-all); the other four are
    /// the arms of the nested <c>SemanticType</c> switch inside the MemberAccess arm.
    /// </summary>
    private static readonly HashSet<string> GetHoverMarkdownNonKindArms = new()
    {
        nameof(Expression), "GenericType", "ResultType", "OptionalType", "BuiltinType",
    };

    private static readonly Dictionary<string, string> GetHoverMarkdownDefault = new()
    {
        // Expression kinds covered by the base arm.
        [nameof(BooleanLiteral)] = ExpressionBaseArm,
        [nameof(BytesLiteralExpression)] = ExpressionBaseArm,
        [nameof(ConditionalExpression)] = ExpressionBaseArm,
        [nameof(DictComprehension)] = ExpressionBaseArm,
        [nameof(DictLiteral)] = ExpressionBaseArm,
        [nameof(DictSpreadComprehension)] = ExpressionBaseArm,
        [nameof(EllipsisLiteral)] = ExpressionBaseArm,
        [nameof(FloatLiteral)] = ExpressionBaseArm,
        [nameof(FStringLiteral)] = ExpressionBaseArm,
        [nameof(IndexAccess)] = ExpressionBaseArm,
        [nameof(IntegerLiteral)] = ExpressionBaseArm,
        [nameof(ListComprehension)] = ExpressionBaseArm,
        [nameof(ListLiteral)] = ExpressionBaseArm,
        [nameof(MatchExpression)] = ExpressionBaseArm,
        [nameof(MaybeExpression)] = ExpressionBaseArm,
        [nameof(ModifiedArgument)] = ExpressionBaseArm,
        [nameof(MultiAxisAccess)] = ExpressionBaseArm,
        [nameof(NoneLiteral)] = ExpressionBaseArm,
        [nameof(Parenthesized)] = ExpressionBaseArm,
        [nameof(QuestionMarkExpression)] = ExpressionBaseArm,
        [nameof(SetComprehension)] = ExpressionBaseArm,
        [nameof(SetLiteral)] = ExpressionBaseArm,
        [nameof(SliceAccess)] = ExpressionBaseArm,
        [nameof(SpreadElement)] = ExpressionBaseArm,
        [nameof(StarExpression)] = ExpressionBaseArm,
        [nameof(StringLiteral)] = ExpressionBaseArm,
        [nameof(TryExpression)] = ExpressionBaseArm,
        [nameof(TStringLiteral)] = ExpressionBaseArm,
        [nameof(TupleLiteral)] = ExpressionBaseArm,
        [nameof(TypeCheck)] = ExpressionBaseArm,
        [nameof(TypeCoercion)] = ExpressionBaseArm,
        [nameof(WalrusExpression)] = "BASE-ARM: the target is a string, not a child node, so the walrus itself is innermost on both `name` and `:=` and falls to the `case Expression` arm (type hover; probed in HoverServiceTests)",

        // Statement kinds without an arm.
        [nameof(DecoratedStatement)] = "UNREACHABLE: unwrapped through Statement.UnwrapDecorated at the top of GetHoverMarkdownForNode before the switch",
        [nameof(BreakWithFlagStatement)] = Synthesized,
        [nameof(BreakStatement)] = KeywordOnly,
        [nameof(ContinueStatement)] = KeywordOnly,
        [nameof(PassStatement)] = KeywordOnly,
        [nameof(AssertStatement)] = ContainerStatement,
        [nameof(Assignment)] = ContainerStatement,
        [nameof(DeferStatement)] = ContainerStatement,
        [nameof(ExpressionStatement)] = ContainerStatement,
        [nameof(RaiseStatement)] = ContainerStatement,
        [nameof(ForStatement)] = ContainerStatement,
        [nameof(IfStatement)] = ContainerStatement,
        [nameof(WhileStatement)] = ContainerStatement,
        [nameof(MatchStatement)] = ContainerStatement,

        // Pattern kinds (reached through MatchStatement / MatchExpression children).
        [nameof(BindingPattern)] = "CONTRACTUAL: the capture name is an Identifier child (BindingPattern.Name) — the Identifier arm answers on it",
        [nameof(AsPattern)] = "CONTRACTUAL: the capture name is an Identifier child (AsPattern.Name) and the inner pattern a child — each answers for itself",
        [nameof(LiteralPattern)] = "CONTRACTUAL: the literal is an Expression child — the base arm answers with its type",
        [nameof(RelationalPattern)] = "CONTRACTUAL: the operand is an Expression child — the base arm answers with its type; the operator has nothing to describe",
        [nameof(TypePattern)] = "CONTRACTUAL: exposes its TypeAnnotation as a child, so the annotation node is innermost and the TypeAnnotation arm answers",
        [nameof(WildcardPattern)] = "CONTRACTUAL: `_` binds nothing and names nothing",
        [nameof(MemberAccessPattern)] = "MISS #1735: records only Parts (strings) — no name extent to hover on, so `case Color.RED:` shows nothing while `Color.RED` in expression position resolves",
        [nameof(PositionalPattern)] = "MISS #1735: its Type names a union case (`case Circle(r):`), for which the semantic layer records no type and LookupType finds nothing — a delegating arm returned null on every input (measured), so none is claimed",
        [nameof(AndPattern)] = StructuralPattern,
        [nameof(OrPattern)] = StructuralPattern,
        [nameof(GuardPattern)] = StructuralPattern,
        [nameof(TuplePattern)] = StructuralPattern,
        [nameof(ListPattern)] = StructuralPattern,
        [nameof(StarPattern)] = StructuralPattern,

        // Remaining Node kinds.
        [nameof(ForClause)] = "CONTRACTUAL: comprehension clause — the target and iterator are children; the clause itself is under the cursor only on `for`/`in`",
        [nameof(IfClause)] = "CONTRACTUAL: comprehension clause — the condition is a child; the clause itself is under the cursor only on `if`",
        [nameof(Module)] = "CONTRACTUAL: the root — innermost only when the cursor is on no statement (blank line / trailing whitespace); nothing to describe",
        [nameof(SubscriptDimension)] = "CONTRACTUAL: the operands of a multi-axis subscript dimension are children; the dimension itself is under the cursor only on `:`/`,`",
        [nameof(PropertyPatternField)] = "CONTRACTUAL: the field label is a keyword-argument-style label; keyword labels are not hovered anywhere (KeywordArgument is not a node either) and the field's pattern is a child",
    };

    [Fact]
    public void GetHoverMarkdownForNode_ArmsPlusJustifiedDefaults_EqualTheNodeUniverse()
    {
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/HoverService.cs",
            "GetHoverMarkdownForNode",
            typeof(Node),
            GetHoverMarkdownArms,
            GetHoverMarkdownDefault,
            GetHoverMarkdownNonKindArms);
    }

    // ── TryNarrowHighlight: Node universe (94) — one rule ──

    private static readonly HashSet<string> TryNarrowHighlightArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        // Phase 2 remediation: the union / delegate / event heads narrow to their name like
        // the other declaration heads.
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
        nameof(AwaitExpression),
        nameof(ReturnStatement),
    };

    [Fact]
    public void TryNarrowHighlight_ArmsPlusJustifiedDefaults_EqualTheNodeUniverse()
    {
        var roster = LspDispatchTotality.UniformDefault(
            LspDispatchTotality.Universe(typeof(Node)),
            TryNarrowHighlightArms,
            "CONTRACTUAL: the hover range is the node's own span — narrowing exists only for declaration heads (to the name token) and keyword-led nodes (await / return)");
        roster[nameof(YieldStatement)] =
            "CONTRACTUAL: narrowed earlier by TryNarrowToKeyword (to the `yield` keyword); this switch never sees it with a hover";
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/HoverService.cs",
            "TryNarrowHighlight",
            typeof(Node),
            TryNarrowHighlightArms,
            roster);
    }

    // ── TryNarrowToKeyword: Node universe (94) — one rule ──

    private static readonly HashSet<string> TryNarrowToKeywordArms = new()
    {
        nameof(YieldStatement),
        nameof(UnaryOp),
    };

    [Fact]
    public void TryNarrowToKeyword_ArmsPlusJustifiedDefaults_EqualTheNodeUniverse()
    {
        var roster = LspDispatchTotality.UniformDefault(
            LspDispatchTotality.Universe(typeof(Node)),
            TryNarrowToKeywordArms,
            "CONTRACTUAL: not a keyword-led node whose hover text is its operand's — only `yield`/`yield from` and `not` delegate to the operand and highlight the keyword");
        roster[nameof(AwaitExpression)] =
            "CONTRACTUAL: `await` has its own hover arm (result type) and narrows through TryNarrowHighlight instead";
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/HoverService.cs",
            "TryNarrowToKeyword",
            typeof(Node),
            TryNarrowToKeywordArms,
            roster);
    }

    // ── GetDecorators: Statement universe (31) — the parser's decorator-target list ──

    private static readonly HashSet<string> GetDecoratorsArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
        // Phase 2 remediation: the parser attaches decorators to unions and events too
        // (Parser.cs "Attach decorators"); a bracket attribute on either is now found.
        nameof(UnionDef),
        nameof(EventDef),
    };

    [Fact]
    public void GetDecorators_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        var roster = LspDispatchTotality.UniformDefault(
            LspDispatchTotality.Universe(typeof(Statement)),
            GetDecoratorsArms,
            "CONTRACTUAL: not a decorator target — the parser reports InvalidDecoratorTarget for any decorator on this kind, so it never carries one");
        const string SuppressWrapped =
            "CONTRACTUAL: carries no decorators itself — a statement-scoped @suppress on this kind lands on the DecoratedStatement wrapper (Parser.cs)";
        roster[nameof(ImportStatement)] = SuppressWrapped;
        roster[nameof(FromImportStatement)] = SuppressWrapped;
        roster[nameof(ExpressionStatement)] = SuppressWrapped;
        roster[nameof(Assignment)] = SuppressWrapped;
        roster[nameof(DecoratedStatement)] =
            "CONTRACTUAL: carries only statement-scoped @suppress decorators, which are never bracket attributes — the sole consumer looks for `@[Generator]`";
        roster[nameof(BreakWithFlagStatement)] = Synthesized;
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/HoverService.cs",
            "GetDecorators",
            typeof(Statement),
            GetDecoratorsArms,
            roster);
    }

    // ── GetBody: Statement universe (31) — kinds whose body holds type members ──

    private static readonly HashSet<string> GetBodyArms = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(PropertyDef),
        // Phase 2 remediation: a union body holds methods (decorated members) like a class body.
        nameof(UnionDef),
    };

    [Fact]
    public void GetBody_ArmsPlusJustifiedDefaults_EqualTheStatementUniverse()
    {
        var roster = LspDispatchTotality.UniformDefault(
            LspDispatchTotality.Universe(typeof(Statement)),
            GetBodyArms,
            "CONTRACTUAL: no member body — bracket-attribute lookup and generated-member counting need a body that can hold type members");
        const string Suite =
            "CONTRACTUAL: a statement suite, not a member body — it cannot hold a bracket-attributed member";
        roster[nameof(FunctionDef)] =
            "CONTRACTUAL: a function body holds no type members (no local types); SearchStatements walks it through its own FunctionDef branch for generator attribution";
        roster[nameof(EventDef)] = Suite;
        roster[nameof(DeferStatement)] = Suite;
        roster[nameof(IfStatement)] = Suite;
        roster[nameof(ForStatement)] = Suite;
        roster[nameof(WhileStatement)] = Suite;
        roster[nameof(TryStatement)] = Suite;
        roster[nameof(WithStatement)] = Suite;
        roster[nameof(MatchStatement)] = Suite;
        roster[nameof(BreakWithFlagStatement)] = Synthesized;
        LspDispatchTotality.Verify(
            _output,
            "src/Sharpy.Lsp/HoverService.cs",
            "GetBody",
            typeof(Statement),
            GetBodyArms,
            roster);
    }
}
