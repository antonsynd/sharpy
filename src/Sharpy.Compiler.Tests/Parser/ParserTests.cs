using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

public class ParserTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    #region Literal Expressions

    [Fact]
    public void ParseIntegerLiteral()
    {
        var module = Parse("42");
        module.Body.Should().HaveCount(1);
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;
        literal.Value.Should().Be("42");
        literal.Suffix.Should().BeNull();
    }

    [Fact]
    public void ParseIntegerLiteralWithSuffix()
    {
        var module = Parse("42L");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;
        literal.Value.Should().Be("42");
        literal.Suffix.Should().Be("L");
    }

    [Fact]
    public void ParseFloatLiteral()
    {
        var module = Parse("3.14");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<FloatLiteral>().Subject;
        literal.Value.Should().Be("3.14");
    }

    [Fact]
    public void ParseStringLiteral()
    {
        var module = Parse("x = \"hello\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<StringLiteral>().Subject;
        literal.Value.Should().Be("hello");
        literal.IsRaw.Should().BeFalse();
    }

    [Fact]
    public void ParseRawStringLiteral()
    {
        var module = Parse("x = r\"hello\\n\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<StringLiteral>().Subject;
        literal.Value.Should().Be("hello\\n");
        literal.IsRaw.Should().BeTrue();
    }

    [Fact]
    public void ParseFStringLiteral()
    {
        var module = Parse("x = f\"hello {name}\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var fstring = assignment.Value.Should().BeOfType<FStringLiteral>().Subject;
        fstring.Parts.Should().HaveCount(2);
        fstring.Parts[0].Text.Should().Be("hello ");
        fstring.Parts[0].Expression.Should().BeNull();
        fstring.Parts[1].Text.Should().BeNull();
        fstring.Parts[1].Expression.Should().BeOfType<Identifier>();
    }

    [Fact]
    public void ParseBooleanLiterals()
    {
        var trueModule = Parse("True");
        var trueExpr = trueModule.Body[0].Should().BeOfType<ExpressionStatement>().Subject.Expression;
        trueExpr.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();

        var falseModule = Parse("False");
        var falseExpr = falseModule.Body[0].Should().BeOfType<ExpressionStatement>().Subject.Expression;
        falseExpr.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeFalse();
    }

    [Fact]
    public void ParseNoneLiteral()
    {
        var module = Parse("None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<NoneLiteral>();
    }

    [Fact]
    public void ParseEllipsisLiteral()
    {
        var module = Parse("...");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    #endregion

    #region Collection Literals

    [Fact]
    public void ParseListLiteral()
    {
        var module = Parse("[1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var list = exprStmt.Expression.Should().BeOfType<ListLiteral>().Subject;
        list.Elements.Should().HaveCount(3);
        list.Elements[0].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        list.Elements[1].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
        list.Elements[2].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
    }

    [Fact]
    public void ParseEmptyList()
    {
        var module = Parse("[]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var list = exprStmt.Expression.Should().BeOfType<ListLiteral>().Subject;
        list.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ParseDictLiteral()
    {
        var module = Parse("{\"a\": 1, \"b\": 2}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var dict = exprStmt.Expression.Should().BeOfType<DictLiteral>().Subject;
        dict.Entries.Should().HaveCount(2);
        dict.Entries[0].Key.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("a");
        dict.Entries[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseEmptyDict()
    {
        var module = Parse("{}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<DictLiteral>().Which.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ParseSetLiteral()
    {
        var module = Parse("{1, 2, 3}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var set = exprStmt.Expression.Should().BeOfType<SetLiteral>().Subject;
        set.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void ParseTupleLiteral()
    {
        var module = Parse("(1, 2, 3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void ParseSingleElementTuple()
    {
        var module = Parse("(1,)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void ParseParenthesizedExpression_NotTuple()
    {
        var module = Parse("(1)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var paren = exprStmt.Expression.Should().BeOfType<Parenthesized>().Subject;
        paren.Expression.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    #endregion

    #region Binary and Unary Operators

    [Fact]
    public void ParseBinaryAdd()
    {
        var module = Parse("1 + 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        binOp.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        binOp.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    [Fact]
    public void ParseBinaryMultiply()
    {
        var module = Parse("3 * 4");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Multiply);
    }

    [Fact]
    public void ParseOperatorPrecedence()
    {
        var module = Parse("1 + 2 * 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var add = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
        add.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");

        var mult = add.Right.Should().BeOfType<BinaryOp>().Subject;
        mult.Operator.Should().Be(BinaryOperator.Multiply);
        mult.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
        mult.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
    }

    [Fact]
    public void ParseUnaryNot()
    {
        var module = Parse("not x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Not);
        unary.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ParseUnaryMinus()
    {
        var module = Parse("-5");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Minus);
        unary.Operand.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
    }

    [Fact]
    public void ParseComparisonChain()
    {
        var module = Parse("1 < 2 <= 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;
        chain.Operands.Should().HaveCount(3);
        chain.Operators.Should().HaveCount(2);
        chain.Operators[0].Should().Be(ComparisonOperator.LessThan);
        chain.Operators[1].Should().Be(ComparisonOperator.LessThanOrEqual);
    }

    #endregion

    #region Member Access and Indexing

    [Fact]
    public void ParseMemberAccess()
    {
        var module = Parse("obj.field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("obj");
        member.Member.Should().Be("field");
        member.IsNullConditional.Should().BeFalse();
    }

    [Fact]
    public void ParseNullConditionalMemberAccess()
    {
        var module = Parse("obj?.field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.IsNullConditional.Should().BeTrue();
    }

    [Fact]
    public void ParseIndexAccess()
    {
        var module = Parse("arr[0]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var index = exprStmt.Expression.Should().BeOfType<IndexAccess>().Subject;
        index.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("arr");
        index.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
    }

    [Fact]
    public void ParseSliceAccess()
    {
        var module = Parse("arr[1:5]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var slice = exprStmt.Expression.Should().BeOfType<SliceAccess>().Subject;
        slice.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("arr");
        slice.Start.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        slice.Stop.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
        slice.Step.Should().BeNull();
    }

    [Fact]
    public void ParseSliceWithStep()
    {
        var module = Parse("arr[::2]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var slice = exprStmt.Expression.Should().BeOfType<SliceAccess>().Subject;
        slice.Start.Should().BeNull();
        slice.Stop.Should().BeNull();
        slice.Step.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    #endregion

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
        varDecl.Type.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ParseNullableTypeAnnotation()
    {
        var module = Parse("x: int?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void ParseTypeCast()
    {
        var module = Parse("x as int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var cast = exprStmt.Expression.Should().BeOfType<TypeCast>().Subject;
        cast.Value.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        cast.TargetType.Name.Should().Be("int");
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

    #endregion

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

    #region Class Definitions

    [Fact]
    public void ParseSimpleClassDef()
    {
        var source = @"
class Person:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Name.Should().Be("Person");
        classDef.BaseClasses.Should().BeEmpty();
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseClassWithBase()
    {
        var source = @"
class Employee(Person):
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("Person");
    }

    [Fact]
    public void ParseClassWithMultipleBases()
    {
        var source = @"
class Manager(Person, ILeader):
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.BaseClasses.Should().HaveCount(2);
    }

    [Fact]
    public void ParseClassWithMethods()
    {
        var source = @"
class Counter:
    def increment(self):
        self.count = self.count + 1
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<FunctionDef>().Which.Name.Should().Be("increment");
    }

    [Fact]
    public void ParseDecoratedClass()
    {
        var source = @"
@dataclass
class Point:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Decorators.Should().HaveCount(1);
        classDef.Decorators[0].Name.Should().Be("dataclass");
    }

    #endregion

    #region Struct Definitions

    [Fact]
    public void ParseSimpleStructDef()
    {
        var source = @"
struct Point:
    x: int
    y: int
";
        var module = Parse(source);
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;
        structDef.Name.Should().Be("Point");
        structDef.Body.Should().HaveCount(2);
        structDef.Body[0].Should().BeOfType<VariableDeclaration>().Which.Name.Should().Be("x");
        structDef.Body[1].Should().BeOfType<VariableDeclaration>().Which.Name.Should().Be("y");
    }

    #endregion

    #region Interface Definitions

    [Fact]
    public void ParseSimpleInterfaceDef()
    {
        var source = @"
interface IDrawable:
    def draw():
        ...
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        interfaceDef.Name.Should().Be("IDrawable");
        interfaceDef.Body.Should().HaveCount(1);
    }

    #endregion

    #region Enum Definitions

    [Fact]
    public void ParseSimpleEnumDef()
    {
        var source = @"
enum Color:
    RED
    GREEN
    BLUE
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.Name.Should().Be("Color");
        enumDef.Members.Should().HaveCount(3);
        enumDef.Members[0].Name.Should().Be("RED");
        enumDef.Members[0].Value.Should().BeNull();
    }

    [Fact]
    public void ParseEnumWithValues()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.Members.Should().HaveCount(3);
        enumDef.Members[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
    }

    #endregion

    #region Import Statements

    [Fact]
    public void ParseImportStatement()
    {
        var module = Parse("import math");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(1);
        import.Names[0].Name.Should().Be("math");
        import.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseImportWithAlias()
    {
        var module = Parse("import numpy as np");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names[0].Name.Should().Be("numpy");
        import.Names[0].AsName.Should().Be("np");
    }

    [Fact]
    public void ParseMultipleImports()
    {
        var module = Parse("import math, sys");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(2);
        import.Names[0].Name.Should().Be("math");
        import.Names[1].Name.Should().Be("sys");
    }

    [Fact]
    public void ParseFromImport()
    {
        var module = Parse("from math import pi");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("math");
        fromImport.Names.Should().HaveCount(1);
        fromImport.Names[0].Name.Should().Be("pi");
        fromImport.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseFromImportWithAlias()
    {
        var module = Parse("from math import sqrt as square_root");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Names[0].Name.Should().Be("sqrt");
        fromImport.Names[0].AsName.Should().Be("square_root");
    }

    [Fact]
    public void ParseFromImportMultiple()
    {
        var module = Parse("from math import pi, e");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Names.Should().HaveCount(2);
    }

    #endregion

    #region Complex Examples

    [Fact]
    public void ParseComplexFunction()
    {
        var source = @"
def factorial(n: int) -> int:
    if n <= 1:
        return 1
    else:
        return n * factorial(n - 1)
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("factorial");
        funcDef.Body.Should().HaveCount(1);
        funcDef.Body[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParseComplexClass()
    {
        var source = @"
class BankAccount:
    def __init__(self, balance: float):
        self.balance = balance

    def deposit(self, amount: float):
        self.balance = self.balance + amount

    def withdraw(self, amount: float) -> bool:
        if self.balance >= amount:
            self.balance = self.balance - amount
            return True
        return False
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Name.Should().Be("BankAccount");
        classDef.Body.Should().HaveCount(3);
        classDef.Body.All(s => s is FunctionDef).Should().BeTrue();
    }

    [Fact]
    public void ParseNestedStructures()
    {
        var source = @"
for i in range(10):
    if i % 2 == 0:
        print(i)
    else:
        continue
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Body.Should().HaveCount(1);
        forStmt.Body[0].Should().BeOfType<IfStatement>();
    }

    #endregion

    #region Error Cases

    [Fact]
    public void ParseError_MissingColon()
    {
        Action act = () => Parse("if True\n    pass");
        act.Should().Throw<ParserError>().WithMessage("*Expected Colon*");
    }

    [Fact]
    public void ParseError_InvalidIndentation()
    {
        Action act = () => Parse("def foo():\npass");
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void ParseError_UnexpectedToken()
    {
        Action act = () => Parse("def 123():\n    pass");
        act.Should().Throw<ParserError>();
    }

    [Fact]
    public void ParseError_UnclosedBracket()
    {
        Action act = () => Parse("[1, 2, 3");
        act.Should().Throw<ParserError>().WithMessage("*Expected RightBracket*");
    }

    #endregion

    #region Additional Operator Tests

    [Fact]
    public void ParseBitwiseAnd()
    {
        var module = Parse("x & y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void ParseBitwiseOr()
    {
        var module = Parse("x | y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseOr);
    }

    [Fact]
    public void ParseBitwiseXor()
    {
        var module = Parse("x ^ y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseXor);
    }

    [Fact]
    public void ParseBitwiseNot()
    {
        var module = Parse("~x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.BitwiseNot);
    }

    [Fact]
    public void ParseLeftShift()
    {
        var module = Parse("x << 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.LeftShift);
    }

    [Fact]
    public void ParseRightShift()
    {
        var module = Parse("x >> 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.RightShift);
    }

    [Fact]
    public void ParseFloorDivide()
    {
        var module = Parse("10 // 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.FloorDivide);
    }

    [Fact]
    public void ParseModulo()
    {
        var module = Parse("10 % 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Modulo);
    }

    [Fact]
    public void ParsePower()
    {
        var module = Parse("2 ** 8");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Power);
    }

    [Fact]
    public void ParsePowerRightAssociative()
    {
        // 2 ** 3 ** 2 should parse as 2 ** (3 ** 2) = 2 ** 9 = 512
        var module = Parse("2 ** 3 ** 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        outer.Operator.Should().Be(BinaryOperator.Power);
        outer.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");

        var inner = outer.Right.Should().BeOfType<BinaryOp>().Subject;
        inner.Operator.Should().Be(BinaryOperator.Power);
        inner.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
        inner.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    [Fact]
    public void ParseNullCoalesce()
    {
        var module = Parse("x ?? y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    [Fact]
    public void ParseLogicalOr()
    {
        var module = Parse("x or y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Or);
    }

    [Fact]
    public void ParseLogicalAnd()
    {
        var module = Parse("x and y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.And);
    }

    [Fact]
    public void ParseInOperator()
    {
        var module = Parse("x in [1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.In);
    }

    [Fact]
    public void ParseNotInOperator()
    {
        var module = Parse("x not in [1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NotIn);
    }

    [Fact]
    public void ParseIsNone()
    {
        var module = Parse("x is None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Is);
    }

    [Fact]
    public void ParseIsNotNone()
    {
        var module = Parse("x is not None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.IsNot);
    }

    #endregion

    #region Augmented Assignment Tests

    [Fact]
    public void ParsePlusAssign()
    {
        var module = Parse("x += 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PlusAssign);
    }

    [Fact]
    public void ParseMinusAssign()
    {
        var module = Parse("x -= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.MinusAssign);
    }

    [Fact]
    public void ParseStarAssign()
    {
        var module = Parse("x *= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.StarAssign);
    }

    [Fact]
    public void ParseSlashAssign()
    {
        var module = Parse("x /= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.SlashAssign);
    }

    [Fact]
    public void ParseFloorDivAssign()
    {
        var module = Parse("x //= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.DoubleSlashAssign);
    }

    [Fact]
    public void ParseModuloAssign()
    {
        var module = Parse("x %= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PercentAssign);
    }

    [Fact]
    public void ParsePowerAssign()
    {
        var module = Parse("x **= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PowerAssign);
    }

    [Fact]
    public void ParseBitwiseAndAssign()
    {
        var module = Parse("x &= 0xFF");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.AndAssign);
    }

    [Fact]
    public void ParseBitwiseOrAssign()
    {
        var module = Parse("x |= 0x01");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.OrAssign);
    }

    [Fact]
    public void ParseBitwiseXorAssign()
    {
        var module = Parse("x ^= 0x10");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.XorAssign);
    }

    [Fact]
    public void ParseLeftShiftAssign()
    {
        var module = Parse("x <<= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.LeftShiftAssign);
    }

    [Fact]
    public void ParseRightShiftAssign()
    {
        var module = Parse("x >>= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.RightShiftAssign);
    }

    #endregion

    #region Const Declaration Tests

    [Fact]
    public void ParseConstDeclaration()
    {
        var module = Parse("const MAX: int = 100");
        var constDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        constDecl.Name.Should().Be("MAX");
        constDecl.IsConst.Should().BeTrue();
        constDecl.Type.Name.Should().Be("int");
        constDecl.InitialValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("100");
    }

    #endregion

    #region Auto Type Inference Tests

    [Fact]
    public void ParseAutoDeclaration()
    {
        var module = Parse("x: auto = 42");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.IsConst.Should().BeFalse();
        varDecl.Type.Name.Should().Be("auto");
        varDecl.InitialValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseAutoShadowing()
    {
        var source = @"x: int = 5
x: auto = ""hello""";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);

        var firstDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        firstDecl.Name.Should().Be("x");
        firstDecl.Type.Name.Should().Be("int");

        var secondDecl = module.Body[1].Should().BeOfType<VariableDeclaration>().Subject;
        secondDecl.Name.Should().Be("x");
        secondDecl.Type.Name.Should().Be("auto");
        secondDecl.InitialValue.Should().BeOfType<StringLiteral>();
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void ParseGenericType()
    {
        var module = Parse("x: list[int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.TypeArguments.Should().HaveCount(1);
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseNestedGenericType()
    {
        var module = Parse("x: list[list[int]]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.TypeArguments[0].Name.Should().Be("list");
        varDecl.Type.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseDictGenericType()
    {
        var module = Parse("x: dict[str, int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.TypeArguments.Should().HaveCount(2);
        varDecl.Type.TypeArguments[0].Name.Should().Be("str");
        varDecl.Type.TypeArguments[1].Name.Should().Be("int");
    }

    [Fact]
    public void ParseGenericClassDefinition()
    {
        var source = @"
class Container[T]:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters.Should().HaveCount(1);
        classDef.TypeParameters[0].Should().Be("T");
    }

    [Fact]
    public void ParseMultipleTypeParameters()
    {
        var source = @"
class Pair[T, U]:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters.Should().HaveCount(2);
        classDef.TypeParameters[0].Should().Be("T");
        classDef.TypeParameters[1].Should().Be("U");
    }

    #endregion

    #region Keyword Argument Tests

    [Fact]
    public void ParseFunctionCallWithKeywordArgs()
    {
        var module = Parse("foo(x=1, y=2)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.KeywordArguments.Should().HaveCount(2);
        call.KeywordArguments[0].Name.Should().Be("x");
        call.KeywordArguments[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseFunctionCallMixedArgs()
    {
        var module = Parse("foo(1, 2, z=3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(2);
        call.KeywordArguments.Should().HaveCount(1);
        call.KeywordArguments[0].Name.Should().Be("z");
    }

    #endregion

    #region From Import All Tests

    [Fact]
    public void ParseFromImportAll()
    {
        var module = Parse("from math import *");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("math");
        fromImport.ImportAll.Should().BeTrue();
    }

    #endregion

    #region Chained Member Access Tests

    [Fact]
    public void ParseChainedMemberAccess()
    {
        var module = Parse("a.b.c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        outer.Member.Should().Be("c");

        var inner = outer.Object.Should().BeOfType<MemberAccess>().Subject;
        inner.Member.Should().Be("b");
        inner.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");
    }

    [Fact]
    public void ParseChainedIndexAccess()
    {
        var module = Parse("matrix[0][1]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<IndexAccess>().Subject;
        outer.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");

        var inner = outer.Object.Should().BeOfType<IndexAccess>().Subject;
        inner.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
        inner.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("matrix");
    }

    [Fact]
    public void ParseMixedMemberAndIndexAccess()
    {
        var module = Parse("obj.list[0].field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        outer.Member.Should().Be("field");

        var index = outer.Object.Should().BeOfType<IndexAccess>().Subject;
        var member = index.Object.Should().BeOfType<MemberAccess>().Subject;
        member.Member.Should().Be("list");
    }

    #endregion

    #region Empty Collection Tests

    [Fact]
    public void ParseEmptyTuple()
    {
        var module = Parse("()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ParseEmptySet_SpecialSyntax()
    {
        // According to v0.5 spec, {/} is empty set, {} is empty dict
        var module = Parse("{/}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<SetLiteral>().Which.Elements.Should().BeEmpty();
    }

    #endregion

    #region Docstring Tests

    [Fact]
    public void ParseModuleDocstring()
    {
        var source = @"
""""""This is a module docstring""""""

def foo():
    pass
";
        var module = Parse(source);
        module.DocString.Should().Be("This is a module docstring");
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseFunctionDocstring()
    {
        var source = @"
def greet(name: str):
    """"""Greet a person by name""""""
    print(name)
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.DocString.Should().Be("Greet a person by name");
    }

    [Fact]
    public void ParseClassDocstring()
    {
        var source = @"
class Person:
    """"""Represents a person""""""
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.DocString.Should().Be("Represents a person");
    }

    #endregion

    #region Type Check Disambiguation Tests

    [Fact]
    public void ParseTypeCheckVsIdentityComparison()
    {
        // According to spec: "x is MyClass" is type check, "x is None" is identity comparison
        // Current implementation treats both as comparison operators
        // This requires lookahead to check if RHS is a type name vs None/identifier
        var module = Parse("x is int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<TypeCheck>();
    }

    #endregion

    #region Additional Control Flow Tests

    [Fact]
    public void ParseMultipleElifClauses()
    {
        var source = @"
if x == 1:
    print(""one"")
elif x == 2:
    print(""two"")
elif x == 3:
    print(""three"")
else:
    print(""other"")
";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.ElifClauses.Should().HaveCount(2);
    }

    [Fact]
    public void ParseNestedIfStatements()
    {
        var source = @"
if x > 0:
    if y > 0:
        print(""both positive"")
";
        var module = Parse(source);
        var outer = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        outer.ThenBody.Should().HaveCount(1);
        outer.ThenBody[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParseForWithTupleUnpacking()
    {
        var source = @"
for x, y in pairs:
    print(x, y)
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Target.Should().BeOfType<TupleLiteral>().Which.Elements.Should().HaveCount(2);
    }

    #endregion
}
