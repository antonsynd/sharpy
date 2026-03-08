using System.Linq;
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Comprehensive tests for line and column position tracking in the parser.
/// Tests verify that all AST nodes capture correct source location information.
/// </summary>
public class ParserPositionTests
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

    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeTrue("Expected parser to report an error for input: " + source);
        return string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message));
    }

    #region Basic Expressions

    [Fact]
    public void Position_IntegerLiteral_TrackedCorrectly()
    {
        var module = Parse("42");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;

        literal.LineStart.Should().Be(1);
        literal.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_StringLiteral_TrackedCorrectly()
    {
        var module = Parse("x = \"hello\""); // Need assignment to create a statement
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assign.Value.Should().BeOfType<StringLiteral>().Subject;

        literal.LineStart.Should().Be(1);
        literal.ColumnStart.Should().Be(5); // After "x = "
    }

    [Fact]
    public void Position_Identifier_TrackedCorrectly()
    {
        var module = Parse("x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var ident = exprStmt.Expression.Should().BeOfType<Identifier>().Subject;

        ident.LineStart.Should().Be(1);
        ident.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_BinaryOperation_TrackedCorrectly()
    {
        var module = Parse("1 + 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        binOp.LineStart.Should().Be(1);
        binOp.ColumnStart.Should().Be(1);
        binOp.Left.Should().BeOfType<IntegerLiteral>().Which.ColumnStart.Should().Be(1);
        binOp.Right.Should().BeOfType<IntegerLiteral>().Which.ColumnStart.Should().Be(5);
    }

    [Fact]
    public void Position_UnaryOperation_TrackedCorrectly()
    {
        var module = Parse("not x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unaryOp = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;

        unaryOp.LineStart.Should().Be(1);
        unaryOp.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ParenthesizedExpression_TrackedCorrectly()
    {
        var module = Parse("(42)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var paren = exprStmt.Expression.Should().BeOfType<Parenthesized>().Subject;

        paren.LineStart.Should().Be(1);
        paren.ColumnStart.Should().Be(1);
    }

    #endregion

    #region Collection Literals

    [Fact]
    public void Position_ListLiteral_TrackedCorrectly()
    {
        var module = Parse("[1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var list = exprStmt.Expression.Should().BeOfType<ListLiteral>().Subject;

        list.LineStart.Should().Be(1);
        list.ColumnStart.Should().Be(1);
        list.Elements[0].Should().BeOfType<IntegerLiteral>().Which.ColumnStart.Should().Be(2);
        list.Elements[1].Should().BeOfType<IntegerLiteral>().Which.ColumnStart.Should().Be(5);
        list.Elements[2].Should().BeOfType<IntegerLiteral>().Which.ColumnStart.Should().Be(8);
    }

    [Fact]
    public void Position_DictLiteral_TrackedCorrectly()
    {
        var module = Parse("{\"a\": 1, \"b\": 2}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var dict = exprStmt.Expression.Should().BeOfType<DictLiteral>().Subject;

        dict.LineStart.Should().Be(1);
        dict.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_SetLiteral_TrackedCorrectly()
    {
        var module = Parse("{1, 2, 3}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var set = exprStmt.Expression.Should().BeOfType<SetLiteral>().Subject;

        set.LineStart.Should().Be(1);
        set.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_TupleLiteral_TrackedCorrectly()
    {
        var module = Parse("(1, 2, 3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;

        tuple.LineStart.Should().Be(1);
        tuple.ColumnStart.Should().Be(1);
    }

    #endregion

    #region Member Access and Calls

    [Fact]
    public void Position_MemberAccess_TrackedCorrectly()
    {
        var module = Parse("obj.field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;

        member.LineStart.Should().Be(1);
        member.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_IndexAccess_TrackedCorrectly()
    {
        var module = Parse("result = arr[0]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var index = assign.Value.Should().BeOfType<IndexAccess>().Subject;

        index.LineStart.Should().Be(1);
        index.ColumnStart.Should().Be(10); // After "result = " - currently fails, reports 15
        index.Object.Should().BeOfType<Identifier>().Which.ColumnStart.Should().Be(10);
    }

    [Fact]
    public void Position_FunctionCall_TrackedCorrectly()
    {
        var module = Parse("foo(1, 2)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;

        call.LineStart.Should().Be(1);
        call.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ChainedMemberAccess_TrackedCorrectly()
    {
        var module = Parse("a.b.c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;

        outer.LineStart.Should().Be(1);
        outer.ColumnStart.Should().Be(1);
    }

    #endregion

    #region Statements

    [Fact]
    public void Position_Assignment_TrackedCorrectly()
    {
        var module = Parse("x = 42");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.LineStart.Should().Be(1);
        assign.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_VariableDeclaration_TrackedCorrectly()
    {
        var module = Parse("x: int = 42");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.LineStart.Should().Be(1);
        varDecl.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_AssertStatement_TrackedCorrectly()
    {
        var module = Parse("assert x > 0");
        var assert = module.Body[0].Should().BeOfType<AssertStatement>().Subject;

        assert.LineStart.Should().Be(1);
        assert.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ReturnStatement_TrackedCorrectly()
    {
        var module = Parse("def foo():\n    return 42");
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        var ret = funcDef.Body[0].Should().BeOfType<ReturnStatement>().Subject;

        ret.LineStart.Should().Be(2);
        ret.ColumnStart.Should().Be(5);
    }

    [Fact]
    public void Position_RaiseStatement_TrackedCorrectly()
    {
        var module = Parse("raise ValueError()");
        var raise = module.Body[0].Should().BeOfType<RaiseStatement>().Subject;

        raise.LineStart.Should().Be(1);
        raise.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_PassStatement_TrackedCorrectly()
    {
        var module = Parse("pass");
        var pass = module.Body[0].Should().BeOfType<PassStatement>().Subject;

        pass.LineStart.Should().Be(1);
        pass.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_BreakStatement_TrackedCorrectly()
    {
        var module = Parse("while True:\n    break");
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        var breakStmt = whileStmt.Body[0].Should().BeOfType<BreakStatement>().Subject;

        breakStmt.LineStart.Should().Be(2);
        breakStmt.ColumnStart.Should().Be(5);
    }

    [Fact]
    public void Position_ContinueStatement_TrackedCorrectly()
    {
        var module = Parse("while True:\n    continue");
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;
        var continueStmt = whileStmt.Body[0].Should().BeOfType<ContinueStatement>().Subject;

        continueStmt.LineStart.Should().Be(2);
        continueStmt.ColumnStart.Should().Be(5);
    }

    #endregion

    #region Control Flow

    [Fact]
    public void Position_IfStatement_TrackedCorrectly()
    {
        var source = @"if x > 0:
    pass";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;

        ifStmt.LineStart.Should().Be(1);
        ifStmt.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_IfElifElse_TrackedCorrectly()
    {
        var source = @"if x > 0:
    pass
elif x < 0:
    pass
else:
    pass";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;

        ifStmt.LineStart.Should().Be(1);
        ifStmt.ElifClauses[0].Should().Match<ElifClause>(e => e.LineStart == 3 && e.ColumnStart == 1);
    }

    [Fact]
    public void Position_WhileStatement_TrackedCorrectly()
    {
        var source = @"while x > 0:
    x = x - 1";
        var module = Parse(source);
        var whileStmt = module.Body[0].Should().BeOfType<WhileStatement>().Subject;

        whileStmt.LineStart.Should().Be(1);
        whileStmt.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ForStatement_TrackedCorrectly()
    {
        var source = @"for x in range(10):
    print(x)";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;

        forStmt.LineStart.Should().Be(1);
        forStmt.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_TryExceptFinally_TrackedCorrectly()
    {
        var source = @"try:
    risky()
except Exception as e:
    handle(e)
finally:
    cleanup()";
        var module = Parse(source);
        var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;

        tryStmt.LineStart.Should().Be(1);
        tryStmt.ColumnStart.Should().Be(1);
        tryStmt.Handlers[0].Should().Match<ExceptHandler>(h => h.LineStart == 3);
    }

    #endregion

    #region Function Definitions

    [Fact]
    public void Position_SimpleFunctionDef_TrackedCorrectly()
    {
        var source = @"def greet():
    pass";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.LineStart.Should().Be(1);
        funcDef.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_FunctionWithParameters_TrackedCorrectly()
    {
        var source = @"def add(a: int, b: int):
    return a + b";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.LineStart.Should().Be(1);
        funcDef.Parameters[0].Should().Match<Parameter>(p => p.LineStart == 1 && p.ColumnStart == 9);
        funcDef.Parameters[1].Should().Match<Parameter>(p => p.LineStart == 1 && p.ColumnStart == 17);
    }

    [Fact]
    public void Position_DecoratedFunction_TrackedCorrectly()
    {
        var source = @"@staticmethod
def foo():
    pass";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.LineStart.Should().Be(2);
        funcDef.Decorators[0].Should().Match<Decorator>(d => d.LineStart == 1 && d.ColumnStart == 1);
    }

    [Fact]
    public void Position_MultipleDecorators_TrackedCorrectly()
    {
        var source = @"@override
@staticmethod
def foo():
    pass";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.Decorators[0].Should().Match<Decorator>(d => d.LineStart == 1);
        funcDef.Decorators[1].Should().Match<Decorator>(d => d.LineStart == 2);
    }

    [Fact]
    public void Position_FunctionWithComplexReturnType_TrackedCorrectly()
    {
        var source = @"def complex() -> dict[str, list[int]]:
    pass";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.LineStart.Should().Be(1);
        funcDef.ReturnType.Should().NotBeNull();
        funcDef.ReturnType!.Should().Match<TypeAnnotation>(t => t.LineStart == 1);
    }

    #endregion

    #region Class Definitions

    [Fact]
    public void Position_SimpleClassDef_TrackedCorrectly()
    {
        var source = @"class Person:
    pass";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(1);
        classDef.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ClassWithBase_TrackedCorrectly()
    {
        var source = @"class Employee(Person):
    pass";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(1);
        classDef.BaseClasses[0].Should().Match<TypeAnnotation>(t => t.LineStart == 1);
    }

    [Fact]
    public void Position_ClassWithMethods_TrackedCorrectly()
    {
        var source = @"class Counter:
    def increment(self):
        self.count = self.count + 1";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(1);
        classDef.Body[0].Should().BeOfType<FunctionDef>().Which.LineStart.Should().Be(2);
    }

    [Fact]
    public void Position_DecoratedClass_TrackedCorrectly()
    {
        var source = @"@dataclass
class Point:
    x: int
    y: int";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(2);
        classDef.Decorators[0].Should().Match<Decorator>(d => d.LineStart == 1);
    }

    [Fact]
    public void Position_GenericClassDefinition_TrackedCorrectly()
    {
        var source = @"class Container[T]:
    pass";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(1);
        classDef.TypeParameters.Should().HaveCount(1);
    }

    #endregion

    #region Struct, Interface, and Enum

    [Fact]
    public void Position_StructDefinition_TrackedCorrectly()
    {
        var source = @"struct Point:
    x: int
    y: int";
        var module = Parse(source);
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;

        structDef.LineStart.Should().Be(1);
        structDef.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_InterfaceDefinition_TrackedCorrectly()
    {
        var source = @"interface IDrawable:
    def draw():
        ...";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;

        interfaceDef.LineStart.Should().Be(1);
        interfaceDef.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_EnumDefinition_TrackedCorrectly()
    {
        var source = @"enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;

        enumDef.LineStart.Should().Be(1);
        enumDef.ColumnStart.Should().Be(1);
        enumDef.Members[0].Should().Match<EnumMember>(m => m.LineStart == 2);
        enumDef.Members[1].Should().Match<EnumMember>(m => m.LineStart == 3);
        enumDef.Members[2].Should().Match<EnumMember>(m => m.LineStart == 4);
    }

    #endregion

    #region Import Statements

    [Fact]
    public void Position_ImportStatement_TrackedCorrectly()
    {
        var module = Parse("import math");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;

        import.LineStart.Should().Be(1);
        import.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_FromImport_TrackedCorrectly()
    {
        var module = Parse("from math import pi");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;

        fromImport.LineStart.Should().Be(1);
        fromImport.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ImportWithAlias_TrackedCorrectly()
    {
        var module = Parse("import numpy as np");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;

        import.LineStart.Should().Be(1);
    }

    #endregion

    #region Type Annotations

    [Fact]
    public void Position_SimpleTypeAnnotation_TrackedCorrectly()
    {
        var module = Parse("x: int");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type.Should().NotBeNull();
        varDecl.Type!.Should().Match<TypeAnnotation>(t => t.LineStart == 1);
    }

    [Fact]
    public void Position_NullableTypeAnnotation_TrackedCorrectly()
    {
        var module = Parse("x: int?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type.Should().Match<TypeAnnotation>(t => t.LineStart == 1 && t.IsOptional);
    }

    [Fact]
    public void Position_GenericTypeAnnotation_TrackedCorrectly()
    {
        var module = Parse("x: list[int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type.Should().Match<TypeAnnotation>(t => t.LineStart == 1);
    }

    [Fact]
    public void Position_NestedGenericType_TrackedCorrectly()
    {
        var module = Parse("x: dict[str, list[int]]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type.Should().Match<TypeAnnotation>(t => t.LineStart == 1);
    }

    #endregion

    #region Lambda and Conditional Expressions

    [Fact]
    public void Position_LambdaExpression_TrackedCorrectly()
    {
        var module = Parse("lambda x: x + 1");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;

        lambda.LineStart.Should().Be(1);
        lambda.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_ConditionalExpression_TrackedCorrectly()
    {
        var module = Parse("1 if True else 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var cond = exprStmt.Expression.Should().BeOfType<ConditionalExpression>().Subject;

        cond.LineStart.Should().Be(1);
        cond.ColumnStart.Should().Be(1);
    }

    #endregion

    #region Multiline Constructs

    [Fact]
    public void Position_MultilineFunction_AllStatementsTracked()
    {
        var source = @"def factorial(n: int) -> int:
    if n <= 1:
        return 1
    else:
        return n * factorial(n - 1)";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.LineStart.Should().Be(1);
        var ifStmt = funcDef.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.LineStart.Should().Be(2);
        ifStmt.ThenBody[0].Should().BeOfType<ReturnStatement>().Which.LineStart.Should().Be(3);
        ifStmt.ElseBody[0].Should().BeOfType<ReturnStatement>().Which.LineStart.Should().Be(5);
    }

    [Fact]
    public void Position_MultilineClass_AllMembersTracked()
    {
        var source = @"class BankAccount:
    def __init__(self, balance: float):
        self.balance = balance

    def deposit(self, amount: float):
        self.balance = self.balance + amount

    def withdraw(self, amount: float) -> bool:
        if self.balance >= amount:
            self.balance = self.balance - amount
            return True
        return False";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.LineStart.Should().Be(1);
        classDef.Body[0].Should().BeOfType<FunctionDef>().Which.LineStart.Should().Be(2);
        classDef.Body[1].Should().BeOfType<FunctionDef>().Which.LineStart.Should().Be(5);
        classDef.Body[2].Should().BeOfType<FunctionDef>().Which.LineStart.Should().Be(8);
    }

    [Fact]
    public void Position_NestedControlFlow_AllLevelsTracked()
    {
        var source = @"for i in range(10):
    if i % 2 == 0:
        for j in range(i):
            print(j)";
        var module = Parse(source);
        var outerFor = module.Body[0].Should().BeOfType<ForStatement>().Subject;

        outerFor.LineStart.Should().Be(1);
        var ifStmt = outerFor.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.LineStart.Should().Be(2);
        var innerFor = ifStmt.ThenBody[0].Should().BeOfType<ForStatement>().Subject;
        innerFor.LineStart.Should().Be(3);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Position_EmptyLines_DoNotAffectPositions()
    {
        var source = @"x = 1


y = 2";
        var module = Parse(source);

        module.Body[0].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(1);
        module.Body[1].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(4);
    }

    [Fact]
    public void Position_CommentsIgnored_PositionsCorrect()
    {
        var source = @"# This is a comment
x = 1
# Another comment
y = 2";
        var module = Parse(source);

        module.Body[0].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(2);
        module.Body[1].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(4);
    }

    [Fact]
    public void Position_LineContinuation_PositionsCorrect()
    {
        var source = @"x = 1 + \
    2 + \
    3";
        var module = Parse(source);
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.LineStart.Should().Be(1);
    }

    [Fact]
    public void Position_ImplicitLineContinuation_PositionsCorrect()
    {
        var source = @"result = (
    1 +
    2
)";
        var module = Parse(source);
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.LineStart.Should().Be(1);
    }

    [Fact]
    public void Position_ComplexNestedExpression_AllPositionsTracked()
    {
        var source = "result = obj.method(a + b, c).property[0].nested()";
        var module = Parse(source);
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.LineStart.Should().Be(1);
        assign.Value.LineStart.Should().Be(1);
    }

    [Fact]
    public void Position_SliceAccess_PositionsTracked()
    {
        var module = Parse("result = arr[1:10:2]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.LineStart.Should().Be(1);
        slice.ColumnStart.Should().Be(10); // After "result = " - currently fails, reports 20
        slice.Object.Should().BeOfType<Identifier>().Which.ColumnStart.Should().Be(10);
    }

    [Fact]
    public void Position_TypeCast_AsKeywordRejected()
    {
        var errors = ParseExpectingError("x as int");
        errors.Should().Contain("Expected end of statement");
    }

    [Fact]
    public void Position_TypeCheck_PositionsTracked()
    {
        var module = Parse("x is int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var check = exprStmt.Expression.Should().BeOfType<TypeCheck>().Subject;

        check.LineStart.Should().Be(1);
    }

    #endregion

    #region Comprehensive Program

    [Fact]
    public void Position_CompleteProgram_AllPositionsCorrect()
    {
        var source = @"# Calculator module
import math

class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def multiply(self, x: int, y: int) -> int:
        return x * y

@staticmethod
def create_calculator() -> Calculator:
    return Calculator()

# Main program
calc = create_calculator()
result = calc.add(5, 3)
print(f""Result: {result}"")";

        var module = Parse(source);

        // Verify positions
        module.Body[0].Should().BeOfType<ImportStatement>().Which.LineStart.Should().Be(2);
        module.Body[1].Should().BeOfType<ClassDef>().Which.LineStart.Should().Be(4);
        module.Body[2].Should().BeOfType<FunctionDef>().Which.LineStart.Should().Be(12);
        module.Body[3].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(16);
        module.Body[4].Should().BeOfType<Assignment>().Which.LineStart.Should().Be(17);
        module.Body[5].Should().BeOfType<ExpressionStatement>().Which.LineStart.Should().Be(18);
    }

    [Fact]
    public void Position_DeeplyNestedStructure_AllPositionsCorrect()
    {
        var source = @"class Outer:
    class Middle:
        class Inner:
            def deeply_nested(self):
                if True:
                    for i in range(5):
                        while i > 0:
                            print(i)
                            i = i - 1";
        var module = Parse(source);
        var outerClass = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        outerClass.LineStart.Should().Be(1);
        var middleClass = outerClass.Body[0].Should().BeOfType<ClassDef>().Subject;
        middleClass.LineStart.Should().Be(2);
        var innerClass = middleClass.Body[0].Should().BeOfType<ClassDef>().Subject;
        innerClass.LineStart.Should().Be(3);
        var method = innerClass.Body[0].Should().BeOfType<FunctionDef>().Subject;
        method.LineStart.Should().Be(4);
        var ifStmt = method.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.LineStart.Should().Be(5);
    }

    [Fact]
    public void Position_AllStatementTypes_CorrectPositions()
    {
        var source = @"# Variable declarations
x: int = 5
const MAX: int = 100
y: auto = ""hello""

# Control flow
if x > 0:
    pass
elif x < 0:
    pass
else:
    pass

while x > 0:
    x = x - 1
    if x == 5:
        break
    if x == 8:
        continue

for i in range(10):
    print(i)

# Exception handling
try:
    risky()
except ValueError:
    pass
except:
    pass
finally:
    cleanup()

# Assertions
assert x > 0, ""x must be positive""

# Return and raise
def foo():
    return 42

def bar():
    raise ValueError()
";

        var module = Parse(source);

        // Just verify that all statements have valid positions (> 0)
        foreach (var stmt in module.Body)
        {
            stmt.LineStart.Should().BeGreaterThan(0);
            stmt.ColumnStart.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region Column Precision Tests

    [Fact]
    public void Position_PreciseColumnTracking_SimpleExpression()
    {
        var module = Parse("x = 42");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.ColumnStart.Should().Be(1);
    }

    [Fact]
    public void Position_PreciseColumnTracking_ComplexExpression()
    {
        var module = Parse("result = (a + b) * (c - d)");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;

        assign.ColumnStart.Should().Be(1);
        assign.Target.Should().BeOfType<Identifier>().Which.ColumnStart.Should().Be(1);
        assign.Value.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_IndentedBlock_CorrectColumns()
    {
        var source = @"def foo():
    x = 1
    y = 2";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        funcDef.Body[0].Should().BeOfType<Assignment>().Which.ColumnStart.Should().Be(5);
        funcDef.Body[1].Should().BeOfType<Assignment>().Which.ColumnStart.Should().Be(5);
    }

    [Fact]
    public void Position_DeeplyIndentedBlock_CorrectColumns()
    {
        var source = @"if True:
    if True:
        if True:
            x = 1";
        var module = Parse(source);
        var outer = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        var middle = outer.ThenBody[0].Should().BeOfType<IfStatement>().Subject;
        var inner = middle.ThenBody[0].Should().BeOfType<IfStatement>().Subject;
        var assign = inner.ThenBody[0].Should().BeOfType<Assignment>().Subject;

        assign.ColumnStart.Should().Be(13); // After 12 spaces (3 levels * 4)
    }

    #endregion

    #region IndexAccess and SliceAccess Position Edge Cases

    [Fact]
    public void Position_ChainedIndexAccess_TrackedCorrectly()
    {
        var module = Parse("result = matrix[0][1]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var outer = assign.Value.Should().BeOfType<IndexAccess>().Subject;

        outer.LineStart.Should().Be(1);
        outer.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_IndexAccessAfterMember_TrackedCorrectly()
    {
        var module = Parse("result = obj.list[0]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var index = assign.Value.Should().BeOfType<IndexAccess>().Subject;

        index.LineStart.Should().Be(1);
        index.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_SliceAccessAfterMember_TrackedCorrectly()
    {
        var module = Parse("result = obj.list[1:5]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.LineStart.Should().Be(1);
        slice.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_IndexAccessInExpression_TrackedCorrectly()
    {
        var module = Parse("result = arr[0] + arr[1]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var binOp = assign.Value.Should().BeOfType<BinaryOp>().Subject;

        var leftIndex = binOp.Left.Should().BeOfType<IndexAccess>().Subject;
        leftIndex.ColumnStart.Should().Be(10); // After "result = "

        var rightIndex = binOp.Right.Should().BeOfType<IndexAccess>().Subject;
        rightIndex.ColumnStart.Should().Be(19); // After "result = arr[0] + "
    }

    [Fact]
    public void Position_SliceAccessWithAllComponents_TrackedCorrectly()
    {
        var module = Parse("result = arr[1:10:2]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.LineStart.Should().Be(1);
        slice.ColumnStart.Should().Be(10); // After "result = "
        slice.Object.Should().BeOfType<Identifier>().Which.ColumnStart.Should().Be(10);
    }

    [Fact]
    public void Position_SliceAccessWithOnlyStart_TrackedCorrectly()
    {
        var module = Parse("result = arr[5:]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_SliceAccessWithOnlyStop_TrackedCorrectly()
    {
        var module = Parse("result = arr[:10]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_SliceAccessWithOnlyStep_TrackedCorrectly()
    {
        var module = Parse("result = arr[::2]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var slice = assign.Value.Should().BeOfType<SliceAccess>().Subject;

        slice.ColumnStart.Should().Be(10); // After "result = "
    }

    [Fact]
    public void Position_NestedIndexAndSlice_TrackedCorrectly()
    {
        var module = Parse("result = matrix[0:2][1]");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var index = assign.Value.Should().BeOfType<IndexAccess>().Subject;

        index.ColumnStart.Should().Be(10); // After "result = "
        index.Object.Should().BeOfType<SliceAccess>().Which.ColumnStart.Should().Be(10);
    }

    [Fact]
    public void Position_IndexAccessInFunctionCall_TrackedCorrectly()
    {
        var module = Parse("result = foo(arr[0])");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var call = assign.Value.Should().BeOfType<FunctionCall>().Subject;
        var index = call.Arguments[0].Should().BeOfType<IndexAccess>().Subject;

        index.ColumnStart.Should().Be(14); // After "result = foo("
    }

    #endregion
}
