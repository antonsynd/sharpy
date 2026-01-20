using Xunit;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests that parser correctly populates TextSpan on AST nodes.
/// </summary>
public class ParserSpanTests
{
    private Module Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        return parser.ParseModule();
    }

    #region Literals

    [Fact]
    public void Identifier_HasSpan()
    {
        var module = Parse("x = 42");
        var assignment = module.Body[0] as Assignment;
        var identifier = assignment?.Target as Identifier;

        Assert.NotNull(identifier?.Span);
        Assert.Equal(0, identifier.Span.Value.Start);
        Assert.Equal(1, identifier.Span.Value.Length);
    }

    [Fact]
    public void IntegerLiteral_HasSpan()
    {
        var module = Parse("42");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as IntegerLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(2, literal.Span.Value.Length);
    }

    [Fact]
    public void FloatLiteral_HasSpan()
    {
        var module = Parse("3.14");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as FloatLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(4, literal.Span.Value.Length);
    }

    [Fact]
    public void StringLiteral_HasSpan()
    {
        var module = Parse("x = \"hello\"");
        var assignment = module.Body[0] as Assignment;
        var literal = assignment?.Value as StringLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(4, literal.Span.Value.Start);
        // String token includes quotes, so "hello" is 7 chars
        Assert.True(literal.Span.Value.Length > 0);
    }

    [Fact]
    public void BooleanLiteral_HasSpan()
    {
        var module = Parse("True");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as BooleanLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(4, literal.Span.Value.Length);
    }

    [Fact]
    public void NoneLiteral_HasSpan()
    {
        var module = Parse("None");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as NoneLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(4, literal.Span.Value.Length);
    }

    #endregion

    #region Collections

    [Fact]
    public void ListLiteral_HasSpan()
    {
        var module = Parse("[1, 2, 3]");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as ListLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(9, literal.Span.Value.Length);
    }

    [Fact]
    public void DictLiteral_HasSpan()
    {
        var module = Parse("{\"a\": 1}");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as DictLiteral;

        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
    }

    #endregion

    #region Operators

    [Fact]
    public void BinaryOp_HasSpan_CoveringBothOperands()
    {
        var module = Parse("1 + 2");
        var stmt = module.Body[0] as ExpressionStatement;
        var binOp = stmt?.Expression as BinaryOp;

        Assert.NotNull(binOp?.Span);
        // Span should cover "1 + 2"
        Assert.Equal(0, binOp.Span.Value.Start);
        Assert.Equal(5, binOp.Span.Value.Length);
    }

    [Fact]
    public void UnaryOp_HasSpan()
    {
        var module = Parse("-42");
        var stmt = module.Body[0] as ExpressionStatement;
        var unaryOp = stmt?.Expression as UnaryOp;

        Assert.NotNull(unaryOp?.Span);
        Assert.Equal(0, unaryOp.Span.Value.Start);
        Assert.Equal(3, unaryOp.Span.Value.Length);
    }

    #endregion

    #region Access Expressions

    [Fact]
    public void FunctionCall_HasSpan()
    {
        var module = Parse("foo(x, y)");
        var stmt = module.Body[0] as ExpressionStatement;
        var call = stmt?.Expression as FunctionCall;

        Assert.NotNull(call?.Span);
        // Span should cover "foo(x, y)"
        Assert.Equal(0, call.Span.Value.Start);
        Assert.Equal(9, call.Span.Value.Length);
    }

    [Fact]
    public void MemberAccess_HasSpan()
    {
        var module = Parse("obj.method");
        var stmt = module.Body[0] as ExpressionStatement;
        var access = stmt?.Expression as MemberAccess;

        Assert.NotNull(access?.Span);
        Assert.Equal(0, access.Span.Value.Start);
        Assert.Equal(10, access.Span.Value.Length);
    }

    [Fact]
    public void IndexAccess_HasSpan()
    {
        var module = Parse("arr[0]");
        var stmt = module.Body[0] as ExpressionStatement;
        var access = stmt?.Expression as IndexAccess;

        Assert.NotNull(access?.Span);
        Assert.Equal(0, access.Span.Value.Start);
        Assert.Equal(6, access.Span.Value.Length);
    }

    #endregion

    #region Statements

    [Fact]
    public void Assignment_HasSpan()
    {
        var module = Parse("x = 42");
        var assignment = module.Body[0] as Assignment;

        Assert.NotNull(assignment?.Span);
        Assert.Equal(0, assignment.Span.Value.Start);
        Assert.Equal(6, assignment.Span.Value.Length);
    }

    [Fact]
    public void IfStatement_HasSpan()
    {
        var module = Parse(@"if True:
    pass");
        var ifStmt = module.Body[0] as IfStatement;

        Assert.NotNull(ifStmt?.Span);
        Assert.Equal(0, ifStmt.Span.Value.Start);
    }

    [Fact]
    public void ReturnStatement_HasSpan()
    {
        var module = Parse(@"def foo():
    return 42");
        var funcDef = module.Body[0] as FunctionDef;
        var returnStmt = funcDef?.Body[0] as ReturnStatement;

        Assert.NotNull(returnStmt?.Span);
    }

    #endregion

    #region Definitions

    [Fact]
    public void FunctionDef_HasSpan()
    {
        var module = Parse(@"def foo():
    pass");
        var funcDef = module.Body[0] as FunctionDef;

        Assert.NotNull(funcDef?.Span);
        Assert.Equal(0, funcDef.Span.Value.Start);
    }

    [Fact]
    public void ClassDef_HasSpan()
    {
        var module = Parse(@"class Foo:
    x: int");
        var classDef = module.Body[0] as ClassDef;

        Assert.NotNull(classDef?.Span);
        Assert.Equal(0, classDef.Span.Value.Start);
    }

    [Fact]
    public void EnumDef_HasSpan()
    {
        var code = "enum Color:\n    Red\n    Green\n    Blue\n";
        var module = Parse(code);
        var enumDef = module.Body[0] as EnumDef;

        Assert.NotNull(enumDef?.Span);
        Assert.Equal(0, enumDef.Span.Value.Start);
    }

    #endregion

    #region Type Annotations

    [Fact]
    public void TypeAnnotation_HasSpan()
    {
        var module = Parse("x: int = 42");
        var varDecl = module.Body[0] as VariableDeclaration;

        Assert.NotNull(varDecl?.Type?.Span);
    }

    [Fact]
    public void GenericTypeAnnotation_HasSpan()
    {
        var module = Parse("x: list[int] = []");
        var varDecl = module.Body[0] as VariableDeclaration;

        Assert.NotNull(varDecl?.Type?.Span);
        // "list[int]" should be the span
    }

    #endregion
}
