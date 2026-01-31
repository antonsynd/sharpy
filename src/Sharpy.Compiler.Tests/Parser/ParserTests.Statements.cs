#pragma warning disable CS0618 // ParserError is obsolete
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests: Statements, control flow, exception handling, function definitions
/// </summary>
public partial class ParserTests
{
    #region Statements

    [Fact]
    public void ParseAssignment()
    {
        var module = Parse("x = 42");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        assign.Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseVariableDeclarationWithInitializer()
    {
        var module = Parse("x: int = 42");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.Type.Name.Should().Be("int");
        varDecl.InitialValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseAssertStatement()
    {
        var module = Parse("assert x > 0");
        var assert = module.Body[0].Should().BeOfType<AssertStatement>().Subject;
        // Single comparison can be BinaryOp or ComparisonChain
        (assert.Test is ComparisonChain || assert.Test is BinaryOp).Should().BeTrue();
        assert.Message.Should().BeNull();
    }

    [Fact]
    public void ParseAssertWithMessage()
    {
        var module = Parse("assert x > 0, \"x must be positive\"");
        var assert = module.Body[0].Should().BeOfType<AssertStatement>().Subject;
        assert.Message.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("x must be positive");
    }

    [Fact]
    public void ParsePassStatement()
    {
        var module = Parse("pass");
        module.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseBreakStatement()
    {
        var module = Parse("while True:\n    break");
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        whileStmt.Body.Should().HaveCount(1);
        whileStmt.Body[0].Should().BeOfType<BreakStatement>();
    }

    [Fact]
    public void ParseContinueStatement()
    {
        var module = Parse("while True:\n    continue");
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        whileStmt.Body[0].Should().BeOfType<ContinueStatement>();
    }

    [Fact]
    public void ParseReturnStatement()
    {
        var module = Parse("def foo():\n    return 42");
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        var ret = funcDef.Body[0].Should().BeOfType<ReturnStatement>().Subject;
        ret.Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseReturnVoid()
    {
        var module = Parse("def foo():\n    return");
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        var ret = funcDef.Body[0].Should().BeOfType<ReturnStatement>().Subject;
        ret.Value.Should().BeNull();
    }

    [Fact]
    public void ParseRaiseStatement()
    {
        var module = Parse("raise ValueError(\"error\")");
        var raise = module.Body[0].Should().BeOfType<RaiseStatement>().Subject;
        raise.Exception.Should().BeOfType<FunctionCall>();
    }

    #endregion

    #region Control Flow

    [Fact]
    public void ParseIfStatement()
    {
        var source = @"
if x > 0:
    print(x)
";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        // Single comparison can be either BinaryOp or ComparisonChain depending on implementation
        (ifStmt.Test is ComparisonChain || ifStmt.Test is BinaryOp).Should().BeTrue();
        ifStmt.ThenBody.Should().HaveCount(1);
        ifStmt.ElifClauses.Should().BeEmpty();
        ifStmt.ElseBody.Should().BeEmpty();
    }

    [Fact]
    public void ParseIfElifElseStatement()
    {
        var source = @"
if x > 0:
    print(""positive"")
elif x < 0:
    print(""negative"")
else:
    print(""zero"")
";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.ElifClauses.Should().HaveCount(1);
        (ifStmt.ElifClauses[0].Test is ComparisonChain || ifStmt.ElifClauses[0].Test is BinaryOp).Should().BeTrue();
        ifStmt.ElifClauses[0].Body.Should().HaveCount(1);
        ifStmt.ElseBody.Should().HaveCount(1);
    }

    [Fact]
    public void ParseWhileStatement()
    {
        var source = @"
while x > 0:
    x = x - 1
";
        var module = Parse(source);
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        (whileStmt.Test is ComparisonChain || whileStmt.Test is BinaryOp).Should().BeTrue();
        whileStmt.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseForStatement()
    {
        var source = @"
for x in range(10):
    print(x)
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        forStmt.Iterator.Should().BeOfType<FunctionCall>();
        forStmt.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseWhileElse_ParsesElseClause()
    {
        var source = @"
while x > 0:
    x = x - 1
else:
    print(""done"")
";
        var module = Parse(source);
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        whileStmt.Body.Should().HaveCount(1);
        whileStmt.ElseBody.Should().HaveCount(1);
        whileStmt.ElseBody[0].Should().BeOfType<ExpressionStatement>();
    }

    [Fact]
    public void ParseForElse_ParsesElseClause()
    {
        var source = @"
for x in items:
    if x == target:
        break
else:
    print(""not found"")
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Body.Should().HaveCount(1);
        forStmt.Body[0].Should().BeOfType<IfStatement>();
        forStmt.ElseBody.Should().HaveCount(1);
        forStmt.ElseBody[0].Should().BeOfType<ExpressionStatement>();
    }

    [Fact]
    public void ParseWhileNoElse_ElseBodyIsEmpty()
    {
        var source = @"
while x > 0:
    x = x - 1
";
        var module = Parse(source);
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        whileStmt.ElseBody.Should().BeEmpty();
    }

    [Fact]
    public void ParseForNoElse_ElseBodyIsEmpty()
    {
        var source = @"
for x in items:
    print(x)
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.ElseBody.Should().BeEmpty();
    }

    #endregion

    #region Exception Handling

    [Fact]
    public void ParseTryExceptStatement()
    {
        var source = @"
try:
    risky()
except Exception as e:
    handle(e)
";
        var module = Parse(source);
        var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
        tryStmt.Body.Should().HaveCount(1);
        tryStmt.Handlers.Should().HaveCount(1);

        var handler = tryStmt.Handlers[0];
        handler.ExceptionType.Should().NotBeNull();
        handler.ExceptionType!.Name.Should().Be("Exception");
        handler.Name.Should().Be("e");
        handler.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseTryExceptFinally()
    {
        var source = @"
try:
    risky()
except:
    handle()
finally:
    cleanup()
";
        var module = Parse(source);
        var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
        tryStmt.Handlers.Should().HaveCount(1);
        tryStmt.Handlers[0].ExceptionType.Should().BeNull();
        tryStmt.Handlers[0].Name.Should().BeNull();
        tryStmt.FinallyBody.Should().HaveCount(1);
    }

    [Fact]
    public void ParseTryMultipleExcept()
    {
        var source = @"
try:
    risky()
except ValueError:
    pass
except KeyError:
    pass
";
        var module = Parse(source);
        var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
        tryStmt.Handlers.Should().HaveCount(2);
        tryStmt.Handlers[0].ExceptionType!.Name.Should().Be("ValueError");
        tryStmt.Handlers[1].ExceptionType!.Name.Should().Be("KeyError");
    }

    #endregion

    #region Function Definitions

    [Fact]
    public void ParseSimpleFunctionDef()
    {
        var source = @"
def greet():
    print(""Hello"")
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("greet");
        funcDef.Parameters.Should().BeEmpty();
        funcDef.ReturnType.Should().BeNull();
        funcDef.Body.Should().HaveCount(1);
        funcDef.Decorators.Should().BeEmpty();
    }

    [Fact]
    public void ParseFunctionWithParameters()
    {
        var source = @"
def add(a: int, b: int):
    return a + b
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Parameters.Should().HaveCount(2);
        funcDef.Parameters[0].Name.Should().Be("a");
        funcDef.Parameters[0].Type!.Name.Should().Be("int");
        funcDef.Parameters[0].DefaultValue.Should().BeNull();
    }

    [Fact]
    public void ParseFunctionWithReturnType()
    {
        var source = @"
def get_value() -> int:
    return 42
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.ReturnType.Should().NotBeNull();
        funcDef.ReturnType!.Name.Should().Be("int");
    }

    [Fact]
    public void ParseFunctionWithDefaultParams()
    {
        var source = @"
def greet(name: str = ""World""):
    print(name)
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Parameters[0].DefaultValue.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("World");
    }

    [Fact]
    public void ParseDecoratedFunction()
    {
        var source = @"
@staticmethod
def foo():
    pass
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Decorators.Should().HaveCount(1);
        funcDef.Decorators[0].Name.Should().Be("staticmethod");
    }

    [Fact]
    public void ParseMultipleDecorators()
    {
        var source = @"
@override
@staticmethod
def foo():
    pass
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Decorators.Should().HaveCount(2);
        funcDef.Decorators[0].Name.Should().Be("override");
        funcDef.Decorators[1].Name.Should().Be("staticmethod");
    }

    #endregion

}
