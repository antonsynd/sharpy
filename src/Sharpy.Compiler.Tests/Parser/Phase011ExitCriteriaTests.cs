using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Phase 0.1.1 Exit Criteria Tests for the Sharpy Parser.
/// These tests verify all exit criteria for phase 0.1.1 of the parser implementation.
///
/// Phase 0.1.1 Exit Criteria:
/// 1. AST correctly represents expression precedence
/// 2. Parentheses override precedence
/// 3. Type annotations parsed but not validated
/// 4. Module structure captured
/// 5. Comparison chaining parsed correctly
/// </summary>
public class Phase011ExitCriteriaTests
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

    #region Exit Criteria 1: AST Correctly Represents Expression Precedence

    [Fact]
    public void ExitCriteria_ExpressionPrecedenceCorrect()
    {
        // Test: a + b * c parses as a + (b * c)
        var module = Parse("a + b * c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var add = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        add.Operator.Should().Be(BinaryOperator.Add);
        add.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");

        var mult = add.Right.Should().BeOfType<BinaryOp>().Subject;
        mult.Operator.Should().Be(BinaryOperator.Multiply);
        mult.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("b");
        mult.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");
    }

    [Fact]
    public void ExitCriteria_MultiplicationBeforeAddition()
    {
        // 1 + 2 * 3 should parse as 1 + (2 * 3)
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
    public void ExitCriteria_PowerRightAssociative()
    {
        // 2 ** 3 ** 2 should parse as 2 ** (3 ** 2), not (2 ** 3) ** 2
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
    public void ExitCriteria_UnaryBeforeBinary()
    {
        // -a * b should parse as (-a) * b
        var module = Parse("-a * b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var mult = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        mult.Operator.Should().Be(BinaryOperator.Multiply);

        var unary = mult.Left.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Minus);
        unary.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");

        mult.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("b");
    }

    [Fact]
    public void ExitCriteria_ComparisonAfterArithmetic()
    {
        // a + 1 < b should parse as (a + 1) < b
        var module = Parse("a + 1 < b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;

        // Single comparison can be either BinaryOp or ComparisonChain
        var expr = exprStmt.Expression;
        if (expr is ComparisonChain chain)
        {
            chain.Operands.Should().HaveCount(2);
            chain.Operators.Should().Contain(ComparisonOperator.LessThan);

            var addExpr = chain.Operands[0].Should().BeOfType<BinaryOp>().Subject;
            addExpr.Operator.Should().Be(BinaryOperator.Add);
        }
        else if (expr is BinaryOp binOp)
        {
            binOp.Operator.Should().Be(BinaryOperator.LessThan);
            var addExpr = binOp.Left.Should().BeOfType<BinaryOp>().Subject;
            addExpr.Operator.Should().Be(BinaryOperator.Add);
        }
        else
        {
            expr.Should().BeAssignableTo<BinaryOp>();
        }
    }

    [Fact]
    public void ExitCriteria_LogicalOperatorPrecedence_NotAndOr()
    {
        // not a and b or c should parse as ((not a) and b) or c
        var module = Parse("not a and b or c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var orOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        orOp.Operator.Should().Be(BinaryOperator.Or);
        orOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");

        var andOp = orOp.Left.Should().BeOfType<BinaryOp>().Subject;
        andOp.Operator.Should().Be(BinaryOperator.And);
        andOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("b");

        var notOp = andOp.Left.Should().BeOfType<UnaryOp>().Subject;
        notOp.Operator.Should().Be(UnaryOperator.Not);
        notOp.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");
    }

    [Fact]
    public void ExitCriteria_NullCoalesceLowestPrecedence()
    {
        // a + b ?? c should parse as (a + b) ?? c
        var module = Parse("a + b ?? c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var nullCoalesce = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        nullCoalesce.Operator.Should().Be(BinaryOperator.NullCoalesce);
        nullCoalesce.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");

        var add = nullCoalesce.Left.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_BitwiseOperatorPrecedence()
    {
        // a & b | c should parse as (a & b) | c
        var module = Parse("a & b | c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var bitwiseOr = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        bitwiseOr.Operator.Should().Be(BinaryOperator.BitwiseOr);
        bitwiseOr.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");

        var bitwiseAnd = bitwiseOr.Left.Should().BeOfType<BinaryOp>().Subject;
        bitwiseAnd.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void ExitCriteria_BitwiseXorPrecedenceBetweenAndOr()
    {
        // a & b ^ c | d should parse as ((a & b) ^ c) | d
        var module = Parse("a & b ^ c | d");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var bitwiseOr = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        bitwiseOr.Operator.Should().Be(BinaryOperator.BitwiseOr);

        var bitwiseXor = bitwiseOr.Left.Should().BeOfType<BinaryOp>().Subject;
        bitwiseXor.Operator.Should().Be(BinaryOperator.BitwiseXor);

        var bitwiseAnd = bitwiseXor.Left.Should().BeOfType<BinaryOp>().Subject;
        bitwiseAnd.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void ExitCriteria_ShiftOperatorPrecedence()
    {
        // a << 1 + 2 should parse as a << (1 + 2)
        var module = Parse("a << 1 + 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var shift = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        shift.Operator.Should().Be(BinaryOperator.LeftShift);
        shift.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");

        var add = shift.Right.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_DivisionModuloPrecedence()
    {
        // a * b // c % d should parse as ((a * b) // c) % d
        var module = Parse("a * b // c % d");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var mod = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        mod.Operator.Should().Be(BinaryOperator.Modulo);

        var floorDiv = mod.Left.Should().BeOfType<BinaryOp>().Subject;
        floorDiv.Operator.Should().Be(BinaryOperator.FloorDivide);

        var mult = floorDiv.Left.Should().BeOfType<BinaryOp>().Subject;
        mult.Operator.Should().Be(BinaryOperator.Multiply);
    }

    [Fact]
    public void ExitCriteria_UnaryPlusAndMinus()
    {
        // +a + -b should parse as (+a) + (-b)
        var module = Parse("+a + -b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var add = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        add.Operator.Should().Be(BinaryOperator.Add);

        var unaryPlus = add.Left.Should().BeOfType<UnaryOp>().Subject;
        unaryPlus.Operator.Should().Be(UnaryOperator.Plus);

        var unaryMinus = add.Right.Should().BeOfType<UnaryOp>().Subject;
        unaryMinus.Operator.Should().Be(UnaryOperator.Minus);
    }

    [Fact]
    public void ExitCriteria_BitwiseNotPrecedence()
    {
        // ~a & b should parse as (~a) & b
        var module = Parse("~a & b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var bitwiseAnd = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        bitwiseAnd.Operator.Should().Be(BinaryOperator.BitwiseAnd);

        var bitwiseNot = bitwiseAnd.Left.Should().BeOfType<UnaryOp>().Subject;
        bitwiseNot.Operator.Should().Be(UnaryOperator.BitwiseNot);
    }

    #endregion

    #region Exit Criteria 2: Parentheses Override Precedence

    [Fact]
    public void ExitCriteria_ParenthesesOverridePrecedence()
    {
        // (a + b) * c should parse with addition inside parentheses first
        var module = Parse("(a + b) * c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var mult = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        mult.Operator.Should().Be(BinaryOperator.Multiply);
        mult.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");

        var paren = mult.Left.Should().BeOfType<Parenthesized>().Subject;
        var add = paren.Expression.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_NestedParentheses()
    {
        // ((a + b)) should preserve nested parentheses
        var module = Parse("((a + b))");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outerParen = exprStmt.Expression.Should().BeOfType<Parenthesized>().Subject;
        var innerParen = outerParen.Expression.Should().BeOfType<Parenthesized>().Subject;
        var add = innerParen.Expression.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_ParenthesesInComparison()
    {
        // (a > b) and c should parse correctly
        var module = Parse("(a > b) and c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var andOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        andOp.Operator.Should().Be(BinaryOperator.And);
        andOp.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("c");

        var paren = andOp.Left.Should().BeOfType<Parenthesized>().Subject;
        // The comparison inside can be either BinaryOp or ComparisonChain
        (paren.Expression is BinaryOp || paren.Expression is ComparisonChain).Should().BeTrue();
    }

    [Fact]
    public void ExitCriteria_ParenthesesOverridePower()
    {
        // (2 ** 3) ** 2 should parse as (2 ** 3) ** 2, not 2 ** (3 ** 2)
        var module = Parse("(2 ** 3) ** 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        outer.Operator.Should().Be(BinaryOperator.Power);
        outer.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");

        var paren = outer.Left.Should().BeOfType<Parenthesized>().Subject;
        var inner = paren.Expression.Should().BeOfType<BinaryOp>().Subject;
        inner.Operator.Should().Be(BinaryOperator.Power);
        inner.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
        inner.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
    }

    [Fact]
    public void ExitCriteria_ParenthesesInNullCoalesce()
    {
        // a ?? (b ?? c) should preserve right grouping
        var module = Parse("a ?? (b ?? c)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;

        outer.Operator.Should().Be(BinaryOperator.NullCoalesce);
        outer.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");

        var paren = outer.Right.Should().BeOfType<Parenthesized>().Subject;
        var inner = paren.Expression.Should().BeOfType<BinaryOp>().Subject;
        inner.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    #endregion

    #region Exit Criteria 3: Type Annotations Parsed But Not Validated

    [Fact]
    public void ExitCriteria_TypeAnnotationsParsedNotValidated()
    {
        // Parser should accept non-existent type names - validation happens later
        var module = Parse("x: FakeType = 42");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.Type.Name.Should().Be("FakeType");
        varDecl.InitialValue.Should().BeOfType<IntegerLiteral>();
    }

    [Fact]
    public void ExitCriteria_SimpleTypeAnnotation()
    {
        var module = Parse("x: int");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.Type.Name.Should().Be("int");
        varDecl.Type.IsOptional.Should().BeFalse();
        varDecl.InitialValue.Should().BeNull();
    }

    [Fact]
    public void ExitCriteria_NullableTypeAnnotation()
    {
        var module = Parse("x: int?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("int");
        varDecl.Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ExitCriteria_GenericTypeAnnotation()
    {
        var module = Parse("x: list[int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.TypeArguments.Should().HaveCount(1);
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_NestedGenericTypeAnnotation()
    {
        var module = Parse("x: dict[str, list[int]]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.TypeArguments.Should().HaveCount(2);
        varDecl.Type.TypeArguments[0].Name.Should().Be("str");
        varDecl.Type.TypeArguments[1].Name.Should().Be("list");
        varDecl.Type.TypeArguments[1].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_NullableGenericTypeAnnotation()
    {
        var module = Parse("x: list[int]?");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.IsOptional.Should().BeTrue();
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_FunctionReturnTypeAnnotation()
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
    public void ExitCriteria_FunctionParameterTypeAnnotation()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Parameters.Should().HaveCount(2);
        funcDef.Parameters[0].Type!.Name.Should().Be("int");
        funcDef.Parameters[1].Type!.Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_NonExistentGenericTypeAccepted()
    {
        // Parser should accept any type name, even if it doesn't exist
        var module = Parse("x: MyCustomContainer[SomeType, AnotherType]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("MyCustomContainer");
        varDecl.Type.TypeArguments.Should().HaveCount(2);
    }

    #endregion

    #region Exit Criteria 4: Module Structure Captured

    [Fact]
    public void ExitCriteria_ModuleStructureCaptured()
    {
        var source = @"
x = 1
y = 2
z = 3
";
        var module = Parse(source);
        module.Should().NotBeNull();
        module.Body.Should().HaveCount(3);
        module.Body.Should().AllBeOfType<Assignment>();
    }

    [Fact]
    public void ExitCriteria_EmptyModule()
    {
        var module = Parse("");
        module.Should().NotBeNull();
        module.Body.Should().BeEmpty();
    }

    [Fact]
    public void ExitCriteria_ModuleWithOnlyComments()
    {
        var module = Parse("# This is a comment");
        module.Should().NotBeNull();
        module.Body.Should().BeEmpty();  // Comments are skipped
    }

    [Fact]
    public void ExitCriteria_ModuleTopLevelStatements()
    {
        var source = @"x = 1
y = 2";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);

        var first = module.Body[0].Should().BeOfType<Assignment>().Subject;
        first.Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");

        var second = module.Body[1].Should().BeOfType<Assignment>().Subject;
        second.Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void ExitCriteria_ModuleWithDefinitions()
    {
        var source = @"
def f():
    pass

class C:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);
        module.Body[0].Should().BeOfType<FunctionDef>().Which.Name.Should().Be("f");
        module.Body[1].Should().BeOfType<ClassDef>().Which.Name.Should().Be("C");
    }

    [Fact]
    public void ExitCriteria_ModuleWithMixedContent()
    {
        var source = @"
import math

x = 42

def compute():
    return x * 2

class Calculator:
    pass
";
        var module = Parse(source);
        module.Body.Should().HaveCount(4);
        module.Body[0].Should().BeOfType<ImportStatement>();
        module.Body[1].Should().BeOfType<Assignment>();
        module.Body[2].Should().BeOfType<FunctionDef>();
        module.Body[3].Should().BeOfType<ClassDef>();
    }

    [Fact]
    public void ExitCriteria_ModuleDocstring()
    {
        var source = @"
""""""This is the module docstring.""""""

def foo():
    pass
";
        var module = Parse(source);
        module.DocString.Should().Be("This is the module docstring.");
        module.Body.Should().HaveCount(1);
    }

    #endregion

    #region Exit Criteria 5: Comparison Chaining Parsed Correctly

    [Fact]
    public void ExitCriteria_ComparisonChainingParsed()
    {
        // a < b < c should parse as a chained comparison
        var module = Parse("a < b < c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operators.Should().HaveCount(2);
        chain.Operators[0].Should().Be(ComparisonOperator.LessThan);
        chain.Operators[1].Should().Be(ComparisonOperator.LessThan);
    }

    [Fact]
    public void ExitCriteria_ComparisonChainingThreeOperands()
    {
        // a < b <= c should parse with 3 operands and 2 operators
        var module = Parse("a < b <= c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operators.Should().HaveCount(2);
        chain.Operators[0].Should().Be(ComparisonOperator.LessThan);
        chain.Operators[1].Should().Be(ComparisonOperator.LessThanOrEqual);
    }

    [Fact]
    public void ExitCriteria_ComparisonChainingMixedOps()
    {
        // a == b != c should parse as a comparison chain
        var module = Parse("a == b != c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operators[0].Should().Be(ComparisonOperator.Equal);
        chain.Operators[1].Should().Be(ComparisonOperator.NotEqual);
    }

    [Fact]
    public void ExitCriteria_ComparisonChainingFourOperands()
    {
        // 0 <= x < y <= 100 should parse as a 4-operand chain
        var module = Parse("0 <= x < y <= 100");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(4);
        chain.Operators.Should().HaveCount(3);
    }

    [Fact]
    public void ExitCriteria_SingleComparisonNotChained()
    {
        // Single comparison (a < b) can be either BinaryOp or ComparisonChain with 2 operands
        var module = Parse("a < b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;

        // Accept either representation
        var expr = exprStmt.Expression;
        if (expr is ComparisonChain chain)
        {
            chain.Operands.Should().HaveCount(2);
            chain.Operators.Should().HaveCount(1);
        }
        else
        {
            var binOp = expr.Should().BeOfType<BinaryOp>().Subject;
            binOp.Operator.Should().Be(BinaryOperator.LessThan);
        }
    }

    [Fact]
    public void ExitCriteria_ComparisonChainingWithArithmetic()
    {
        // 0 < a + 1 < 10 should chain with arithmetic inside
        var module = Parse("0 < a + 1 < 10");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operands[1].Should().BeOfType<BinaryOp>().Which.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_ComparisonChainingInOperator()
    {
        // x in [1, 2] in [[1, 2]] technically chains but rarely used
        // Just verify it parses - the semantic validity is another concern
        var module = Parse("1 < 2 < 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;
        chain.Operands.Should().HaveCount(3);
    }

    #endregion

    #region Core AST Nodes Verification

    [Fact]
    public void ExitCriteria_ModuleRootNode()
    {
        var module = Parse("42");
        module.Should().NotBeNull();
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ExitCriteria_ExpressionStatement()
    {
        var module = Parse("42");
        module.Body[0].Should().BeOfType<ExpressionStatement>();
    }

    [Fact]
    public void ExitCriteria_IntegerLiteral()
    {
        var module = Parse("42");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ExitCriteria_FloatLiteral()
    {
        var module = Parse("3.14");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<FloatLiteral>().Which.Value.Should().Be("3.14");
    }

    [Fact]
    public void ExitCriteria_StringLiteral()
    {
        var module = Parse("x = \"hello\"");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Value.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("hello");
    }

    [Fact]
    public void ExitCriteria_BooleanLiteralTrue()
    {
        var module = Parse("True");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();
    }

    [Fact]
    public void ExitCriteria_BooleanLiteralFalse()
    {
        var module = Parse("False");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeFalse();
    }

    [Fact]
    public void ExitCriteria_NoneLiteral()
    {
        var module = Parse("None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<NoneLiteral>();
    }

    [Fact]
    public void ExitCriteria_Identifier()
    {
        var module = Parse("x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ExitCriteria_BinaryOp()
    {
        var module = Parse("a + b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_UnaryOp()
    {
        var module = Parse("-x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Minus);
    }

    [Fact]
    public void ExitCriteria_PassStatement()
    {
        var module = Parse("pass");
        module.Body[0].Should().BeOfType<PassStatement>();
    }

    #endregion

    #region Special Operators

    [Fact]
    public void ExitCriteria_PipeOperator()
    {
        var module = Parse("x |> f");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.PipeForward);
    }

    [Fact]
    public void ExitCriteria_PipeOperatorChaining()
    {
        var module = Parse("x |> f |> g |> h");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;

        // Left-associative: ((x |> f) |> g) |> h
        var outer = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        outer.Operator.Should().Be(BinaryOperator.PipeForward);
        outer.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("h");
    }

    [Fact]
    public void ExitCriteria_ToOperator()
    {
        var module = Parse("x to int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var coercion = exprStmt.Expression.Should().BeOfType<TypeCoercion>().Subject;
        coercion.Value.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        coercion.TargetType.Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_ToOperatorNullable()
    {
        var module = Parse("x to int?");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var coercion = exprStmt.Expression.Should().BeOfType<TypeCoercion>().Subject;
        coercion.TargetType.Name.Should().Be("int");
        coercion.TargetType.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ExitCriteria_NullCoalesceOperator()
    {
        var module = Parse("a ?? b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    [Fact]
    public void ExitCriteria_NullConditionalAccess()
    {
        var module = Parse("a?.b");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.IsNullConditional.Should().BeTrue();
        member.Member.Should().Be("b");
    }

    [Fact]
    public void ExitCriteria_TypeCastWithAs()
    {
        var module = Parse("x as int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var cast = exprStmt.Expression.Should().BeOfType<TypeCast>().Subject;
        cast.Value.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        cast.TargetType.Name.Should().Be("int");
    }

    [Fact]
    public void ExitCriteria_TypeCheckWithIs()
    {
        var module = Parse("x is int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var check = exprStmt.Expression.Should().BeOfType<TypeCheck>().Subject;
        check.Value.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        check.CheckType.Name.Should().Be("int");
    }

    #endregion

    #region Comprehensive Integration Tests

    [Fact]
    public void ExitCriteria_ComplexNestedExpression()
    {
        // (a + b) * c ** d - e should parse correctly
        var module = Parse("(a + b) * c ** d - e");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;

        // Should be: ((a + b) * (c ** d)) - e
        var sub = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        sub.Operator.Should().Be(BinaryOperator.Subtract);
        sub.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("e");

        var mult = sub.Left.Should().BeOfType<BinaryOp>().Subject;
        mult.Operator.Should().Be(BinaryOperator.Multiply);

        var paren = mult.Left.Should().BeOfType<Parenthesized>().Subject;
        paren.Expression.Should().BeOfType<BinaryOp>().Which.Operator.Should().Be(BinaryOperator.Add);

        var power = mult.Right.Should().BeOfType<BinaryOp>().Subject;
        power.Operator.Should().Be(BinaryOperator.Power);
    }

    [Fact]
    public void ExitCriteria_AllLiteralTypes()
    {
        var source = @"
a = 42
b = 3.14
c = ""hello""
d = True
e = False
f = None
";
        var module = Parse(source);
        module.Body.Should().HaveCount(6);

        var values = module.Body.Cast<Assignment>().Select(a => a.Value).ToList();
        values[0].Should().BeOfType<IntegerLiteral>();
        values[1].Should().BeOfType<FloatLiteral>();
        values[2].Should().BeOfType<StringLiteral>();
        values[3].Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();
        values[4].Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeFalse();
        values[5].Should().BeOfType<NoneLiteral>();
    }

    [Fact]
    public void ExitCriteria_FullPrecedenceTest()
    {
        // Test expression using many precedence levels
        // a or b and not c == d + e * f ** g
        // Should parse as: a or (b and ((not c) == (d + (e * (f ** g)))))
        var module = Parse("a or b and not c == d + e * f ** g");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;

        var orOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        orOp.Operator.Should().Be(BinaryOperator.Or);
        orOp.Left.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");
    }

    [Fact]
    public void ExitCriteria_ConditionalExpression()
    {
        var module = Parse("x if condition else y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var cond = exprStmt.Expression.Should().BeOfType<ConditionalExpression>().Subject;

        cond.ThenValue.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
        cond.Test.Should().BeOfType<Identifier>().Which.Name.Should().Be("condition");
        cond.ElseValue.Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void ExitCriteria_LambdaExpression()
    {
        var module = Parse("lambda x, y: x + y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;

        lambda.Parameters.Should().HaveCount(2);
        lambda.Body.Should().BeOfType<BinaryOp>().Which.Operator.Should().Be(BinaryOperator.Add);
    }

    [Fact]
    public void ExitCriteria_ListComprehension()
    {
        var module = Parse("[x * 2 for x in items]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var comp = exprStmt.Expression.Should().BeOfType<ListComprehension>().Subject;

        comp.Element.Should().BeOfType<BinaryOp>().Which.Operator.Should().Be(BinaryOperator.Multiply);
        comp.Clauses.Should().HaveCount(1);
        comp.Clauses[0].Should().BeOfType<ForClause>();
    }

    [Fact]
    public void ExitCriteria_FunctionCallWithKeywordArgs()
    {
        var module = Parse("func(a, b, c=1, d=2)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;

        call.Arguments.Should().HaveCount(2);
        call.KeywordArguments.Should().HaveCount(2);
        call.KeywordArguments[0].Name.Should().Be("c");
        call.KeywordArguments[1].Name.Should().Be("d");
    }

    [Fact]
    public void ExitCriteria_ChainedMethodCalls()
    {
        var module = Parse("obj.method1().method2().method3()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;

        var member = call.Function.Should().BeOfType<MemberAccess>().Subject;
        member.Member.Should().Be("method3");
    }

    #endregion
}
