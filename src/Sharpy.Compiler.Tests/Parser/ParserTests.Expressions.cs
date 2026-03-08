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

}
