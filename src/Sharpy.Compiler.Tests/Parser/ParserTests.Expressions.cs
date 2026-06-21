using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests: Function calls, conditionals, lambdas, and type annotations
/// </summary>
public partial class ParserTests
{
    #region Function Calls

    [Fact]
    public void ParseFunctionCall()
    {
        var module = Parse("foo()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.Function.Should().BeOfType<Identifier>().Which.Name.Should().Be("foo");
        call.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void ParseFunctionCallWithArgs()
    {
        var module = Parse("foo(1, 2)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(2);
        call.Arguments[0].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseMethodCall()
    {
        var module = Parse("obj.method(42)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var member = call.Function.Should().BeOfType<MemberAccess>().Subject;
        member.Member.Should().Be("method");
        call.Arguments.Should().HaveCount(1);
    }

    #endregion

    #region Conditional and Lambda

    [Fact]
    public void ParseConditionalExpression()
    {
        var module = Parse("1 if True else 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var cond = exprStmt.Expression.Should().BeOfType<ConditionalExpression>().Subject;
        cond.ThenValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        cond.Test.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();
        cond.ElseValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    [Fact]
    public void ParseLambdaExpression()
    {
        var module = Parse("lambda x: x + 1");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("x");
        lambda.Body.Should().BeOfType<BinaryOp>();
    }

    [Fact]
    public void ParseLambdaNoParams()
    {
        var module = Parse("lambda: 42");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().BeEmpty();
        lambda.Body.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseLambdaBodyParameterSubscript()
    {
        // #899: `lambda t: t[0]` is a subscript in the body, not a generic
        // type annotation on the parameter.
        var module = Parse("lambda t: t[0]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("t");
        lambda.Parameters[0].Type.Should().BeNull();
        var index = lambda.Body.Should().BeOfType<IndexAccess>().Subject;
        index.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("t");
        index.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
    }

    [Fact]
    public void ParseLambdaBodyCapturedVariableSubscript()
    {
        // #899: subscript on a captured variable (not the parameter).
        var module = Parse("lambda t: x[0]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        var index = lambda.Body.Should().BeOfType<IndexAccess>().Subject;
        index.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ParseLambdaGenericParameterAnnotationStillParses()
    {
        // #899 regression guard: a generic type annotation on the parameter
        // (`list[int]`) must keep parsing as an annotation, distinguished from
        // a body subscript by the trailing `:` after the matching `]`.
        var module = Parse("lambda t: list[int]: len(t)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("t");
        lambda.Parameters[0].Type.Should().NotBeNull();
        lambda.Parameters[0].Type!.Name.Should().Be("list");
        lambda.Parameters[0].Type!.TypeArguments.Should().HaveCount(1);
        lambda.Parameters[0].Type!.TypeArguments[0].Name.Should().Be("int");
        lambda.Body.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseLambdaNestedGenericParameterAnnotationStillParses()
    {
        // #899 regression guard: nested generics in the parameter annotation.
        var module = Parse("lambda t: dict[str, list[int]]: len(t)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters[0].Type.Should().NotBeNull();
        lambda.Parameters[0].Type!.Name.Should().Be("dict");
        lambda.Parameters[0].Type!.TypeArguments.Should().HaveCount(2);
        lambda.Body.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseLambdaBodySubscriptEofAfterBracketIsCleanError()
    {
        // #899 edge case: EOF while scanning the bracket must not crash; it
        // should surface a clean parser diagnostic.
        Action act = () => Parse("lambda t: t[");
        act.Should().NotThrow();
    }

    [Fact]
    public void ParseLambdaBodySubscriptUnbalancedBracketIsCleanError()
    {
        // #899 edge case: unbalanced brackets must produce a clean parse error,
        // not an exception/crash.
        Action act = () => Parse("lambda t: t[0");
        act.Should().NotThrow();
    }

    #endregion

    #region Type Annotations and Casts

    [Fact]
    public void ParseTypeAnnotation()
    {
        var module = Parse("x: int");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.Type.Should().NotBeNull();
        varDecl.Type.Name.Should().Be("int");
        varDecl.Type.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void ParseNullableTypeAnnotation()
    {
        var module = Parse("x: int?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.IsOptional.Should().BeTrue();
        varDecl.Type.Name.Should().Be("int");
    }

    [Fact]
    public void ParseTypeCheck()
    {
        var module = Parse("x is int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var check = exprStmt.Expression.Should().BeOfType<TypeCheck>().Subject;
        check.Value.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        check.CheckType.Name.Should().Be("int");
    }

    [Fact]
    public void ParseNullableTypeInFunctionParameter()
    {
        var module = Parse(@"
def greet(name: str?):
    pass
");
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Parameters.Should().HaveCount(1);
        funcDef.Parameters[0].Type.Should().NotBeNull();
        funcDef.Parameters[0].Type.Name.Should().Be("str");
        funcDef.Parameters[0].Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseNullableReturnType()
    {
        var module = Parse(@"
def find_user(id: int) -> User?:
    pass
");
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.ReturnType.Should().NotBeNull();
        funcDef.ReturnType.Name.Should().Be("User");
        funcDef.ReturnType.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseNullableDictType()
    {
        var module = Parse("mapping: dict[str, int?]?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Should().NotBeNull();
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.IsOptional.Should().BeTrue();
        varDecl.Type.TypeArguments.Should().HaveCount(2);
        varDecl.Type.TypeArguments[0].Name.Should().Be("str");
        varDecl.Type.TypeArguments[0].IsOptional.Should().BeFalse();
        varDecl.Type.TypeArguments[1].Name.Should().Be("int");
        varDecl.Type.TypeArguments[1].IsOptional.Should().BeTrue();
    }

    #endregion

    #region QuestionMark (Early-Return Operator)

    [Fact]
    public void ParseQuestionMark_PostfixAfterCall()
    {
        // foo()? → QuestionMarkExpression { Operand: FunctionCall }
        var module = Parse("foo()?");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var qm = exprStmt.Expression.Should().BeOfType<QuestionMarkExpression>().Subject;
        var call = qm.Operand.Should().BeOfType<FunctionCall>().Subject;
        call.Function.Should().BeOfType<Identifier>().Which.Name.Should().Be("foo");
    }

    [Fact]
    public void ParseQuestionMark_CoalesceWithSpaces()
    {
        // x ?? y → BinaryOp { NullCoalesce }
        var module = Parse("x ?? y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
        binOp.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        binOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void ParseQuestionMark_CoalesceNoSpaces()
    {
        // x??y → BinaryOp { NullCoalesce } (not double early-return)
        var module = Parse("x??y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
        binOp.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        binOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void ParseQuestionMark_DoubleEarlyReturn()
    {
        // x?? (at end of expression, no RHS) → nested QuestionMarkExpression
        var module = Parse("x??");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<QuestionMarkExpression>().Subject;
        var inner = outer.Operand.Should().BeOfType<QuestionMarkExpression>().Subject;
        inner.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ParseQuestionMark_TripleWithCoalesce()
    {
        // x???y → BinaryOp { NullCoalesce, Left: QuestionMarkExpression{Name{x}}, Right: Name{y} }
        var module = Parse("x???y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
        var qm = binOp.Left.Should().BeOfType<QuestionMarkExpression>().Subject;
        qm.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        binOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void ParseQuestionMark_EarlyReturnFollowedByBinaryOp()
    {
        // x? + 1 → BinaryOp { Add, Left: QuestionMarkExpression{Name{x}}, Right: IntLiteral{1} }
        var module = Parse("x? + 1");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        var qm = binOp.Left.Should().BeOfType<QuestionMarkExpression>().Subject;
        qm.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        binOp.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseQuestionMark_AfterNullConditionalChain()
    {
        // x?.foo()? → QuestionMarkExpression { Operand: FunctionCall { Object: MemberAccess{NullConditional} } }
        var module = Parse("x?.foo()?");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var qm = exprStmt.Expression.Should().BeOfType<QuestionMarkExpression>().Subject;
        var call = qm.Operand.Should().BeOfType<FunctionCall>().Subject;
        var member = call.Function.Should().BeOfType<MemberAccess>().Subject;
        member.IsNullConditional.Should().BeTrue();
        member.Member.Should().Be("foo");
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ParseQuestionMark_TypeAnnotationStillWorks()
    {
        // int? in type annotation position → IsOptional = true (no regression)
        var module = Parse("x: int? = None");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Should().NotBeNull();
        varDecl.Type!.Name.Should().Be("int");
        varDecl.Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseQuestionMark_CoalesceWithBinaryOpRHS()
    {
        // x?? + 1 → BinaryOp { NullCoalesce, Left: x, Right: UnaryOp{+, 1} }
        // N=2, followed by +, which is expression-start → reserve 2 for coalesce
        var module = Parse("x?? + 1");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
        binOp.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        var unary = binOp.Right.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Plus);
        unary.Operand.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseQuestionMark_QuadrupleWithCoalesce()
    {
        // x????y → BinaryOp { NullCoalesce, Left: QM{QM{x}}, Right: y }
        // N=4, followed by expr → earlyReturnCount = 4-2 = 2, consume 2 as postfix
        var module = Parse("x????y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
        var outerQm = binOp.Left.Should().BeOfType<QuestionMarkExpression>().Subject;
        var innerQm = outerQm.Operand.Should().BeOfType<QuestionMarkExpression>().Subject;
        innerQm.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        binOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    #endregion

}
