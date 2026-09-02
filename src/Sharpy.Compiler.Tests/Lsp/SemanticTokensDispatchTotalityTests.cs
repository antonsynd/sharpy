using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>SemanticTokensHandler</c> dispatch switches.
/// Kinds with no tokens are silently skipped — the skip is contractual, not a miss.
/// Each fact pins the arm set via <see cref="SwitchArmScan"/>.
/// </summary>
public class SemanticTokensDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public SemanticTokensDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> CollectStatementTokensExpected = new()
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
    };

    [Fact]
    public void CollectStatementTokens_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectStatementTokens");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectStatementTokens arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CollectStatementTokensExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CollectStatementTokensExpected))}\n" +
            $"  Missing: {string.Join(", ", CollectStatementTokensExpected.Except(arms))}");
    }

    private static readonly HashSet<string> CollectExpressionTokensExpected = new()
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
        "And",
        "Or",
        "In",
        "NotIn",
        "Is",
        "IsNot",
    };

    [Fact]
    public void CollectExpressionTokens_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectExpressionTokens");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectExpressionTokens arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CollectExpressionTokensExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CollectExpressionTokensExpected))}\n" +
            $"  Missing: {string.Join(", ", CollectExpressionTokensExpected.Except(arms))}");
    }

    [Fact]
    public void CollectComprehensionClauseTokens_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs",
            "CollectComprehensionClauseTokens");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectComprehensionClauseTokens arms: {string.Join(", ", arms.OrderBy(a => a))}");
        var expected = new HashSet<string> { nameof(ForClause), nameof(IfClause) };
        Assert.True(arms.SetEquals(expected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }
}
