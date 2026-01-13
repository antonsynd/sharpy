using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using System.Reflection;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RoslynEmitterExpressionTests
{
    private readonly RoslynEmitter _emitter;
    private readonly CodeGenContext _context;
    private readonly MethodInfo _generateExpression;

    public RoslynEmitterExpressionTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
        _generateExpression = GetGenerateExpressionMethod();
    }

    private MethodInfo GetGenerateExpressionMethod()
    {
        var method = typeof(RoslynEmitter).GetMethod(
            "GenerateExpression",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException("GenerateExpression method not found");
        }

        return method;
    }

    private ExpressionSyntax InvokeGenerateExpression(Expression expr)
    {
        var result = _generateExpression.Invoke(_emitter, new object[] { expr });
        return (ExpressionSyntax)result!;
    }

    #region Literal Expression Tests

    [Theory]
    [InlineData("42", "42")]
    [InlineData("0", "0")]
    [InlineData("1000", "1000")]
    public void GenerateExpression_IntegerLiteral_GeneratesCorrectSyntax(string value, string expected)
    {
        // Arrange
        var expr = new IntegerLiteral { Value = value };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("3.14", "3.14")]
    [InlineData("0.5", "0.5")]
    [InlineData("2.718", "2.718")]
    public void GenerateExpression_FloatLiteral_GeneratesCorrectSyntax(string value, string expected)
    {
        // Arrange
        var expr = new FloatLiteral { Value = value };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expected);
    }

    [Theory]
    [InlineData("hello", "\"hello\"")]
    [InlineData("world", "\"world\"")]
    public void GenerateExpression_StringLiteral_GeneratesCorrectSyntax(string value, string expected)
    {
        // Arrange
        var expr = new StringLiteral { Value = value };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void GenerateExpression_BooleanLiteral_GeneratesCorrectSyntax(bool value, string expected)
    {
        // Arrange
        var expr = new BooleanLiteral { Value = value };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Be(expected);
    }

    [Fact]
    public void GenerateExpression_NoneLiteral_GeneratesNull()
    {
        // Arrange
        var expr = new NoneLiteral();

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Be("null");
    }

    #endregion

    #region Collection Literal Tests

    [Fact]
    public void GenerateExpression_EmptyListLiteral_GeneratesSharpyList()
    {
        // Arrange
        var expr = new ListLiteral { Elements = new List<Expression>() };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain("Sharpy.Core.List");
    }

    [Fact]
    public void GenerateExpression_ListOfIntegers_GeneratesSharpyListInt()
    {
        // Arrange
        var expr = new ListLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" },
                new IntegerLiteral { Value = "3" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Core.List<int>");
        code.Should().Contain("1");
        code.Should().Contain("2");
        code.Should().Contain("3");
    }

    [Fact]
    public void GenerateExpression_DictLiteral_GeneratesSharpyDict()
    {
        // Arrange
        var expr = new DictLiteral
        {
            Entries = new List<DictEntry>
            {
                new DictEntry
                {
                    Key = new StringLiteral { Value = "a" },
                    Value = new IntegerLiteral { Value = "1" }
                },
                new DictEntry
                {
                    Key = new StringLiteral { Value = "b" },
                    Value = new IntegerLiteral { Value = "2" }
                }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Core.Dict");
        code.Should().Contain("\"a\"");
        code.Should().Contain("\"b\"");
    }

    [Fact]
    public void GenerateExpression_SetLiteral_GeneratesSharpySet()
    {
        // Arrange
        var expr = new SetLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Core.Set<int>");
    }

    [Fact]
    public void GenerateExpression_TupleLiteral_GeneratesValueTuple()
    {
        // Arrange
        var expr = new TupleLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new StringLiteral { Value = "test" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("(");
        code.Should().Contain("1");
        code.Should().Contain("\"test\"");
        code.Should().Contain(")");
    }

    #endregion

    #region Binary Operation Tests

    [Theory]
    [InlineData(BinaryOperator.Add, "+")]
    [InlineData(BinaryOperator.Subtract, "-")]
    [InlineData(BinaryOperator.Multiply, "*")]
    [InlineData(BinaryOperator.Divide, "/")]
    [InlineData(BinaryOperator.Modulo, "%")]
    public void GenerateExpression_ArithmeticBinaryOp_GeneratesCorrectOperator(BinaryOperator op, string expectedOp)
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = op,
            Left = new IntegerLiteral { Value = "5" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expectedOp);
        result.ToString().Should().Contain("5");
        result.ToString().Should().Contain("3");
    }

    [Fact]
    public void GenerateExpression_PowerOperator_GeneratesMathPow()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Power,
            Left = new IntegerLiteral { Value = "2" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Math.Pow");
        code.Should().Contain("2");
        code.Should().Contain("3");
    }

    [Fact]
    public void GenerateExpression_FloorDivide_UsesFloorSemantics()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new IntegerLiteral { Value = "10" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert - Floor division should use Math.Floor for correct negative number handling
        // (int)Math.Floor((double)x / y) rounds toward negative infinity (Python semantics)
        var code = result.ToString();
        code.Should().Contain("(int)");
        code.Should().Contain("Math.Floor");
        code.Should().Contain("(double)");
        code.Should().Contain("/");
    }

    [Theory]
    [InlineData(BinaryOperator.Equal, "==")]
    [InlineData(BinaryOperator.NotEqual, "!=")]
    [InlineData(BinaryOperator.LessThan, "<")]
    [InlineData(BinaryOperator.GreaterThan, ">")]
    [InlineData(BinaryOperator.LessThanOrEqual, "<=")]
    [InlineData(BinaryOperator.GreaterThanOrEqual, ">=")]
    public void GenerateExpression_ComparisonOp_GeneratesCorrectOperator(BinaryOperator op, string expectedOp)
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = op,
            Left = new IntegerLiteral { Value = "5" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expectedOp);
    }

    [Theory]
    [InlineData(BinaryOperator.And, "&&")]
    [InlineData(BinaryOperator.Or, "||")]
    public void GenerateExpression_LogicalOp_GeneratesCorrectOperator(BinaryOperator op, string expectedOp)
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = op,
            Left = new BooleanLiteral { Value = true },
            Right = new BooleanLiteral { Value = false }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expectedOp);
    }

    [Theory]
    [InlineData(BinaryOperator.BitwiseAnd, "&")]
    [InlineData(BinaryOperator.BitwiseOr, "|")]
    [InlineData(BinaryOperator.BitwiseXor, "^")]
    [InlineData(BinaryOperator.LeftShift, "<<")]
    [InlineData(BinaryOperator.RightShift, ">>")]
    public void GenerateExpression_BitwiseOp_GeneratesCorrectOperator(BinaryOperator op, string expectedOp)
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = op,
            Left = new IntegerLiteral { Value = "5" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expectedOp);
    }

    [Fact]
    public void GenerateExpression_NullCoalesce_GeneratesQuestionQuestion()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.NullCoalesce,
            Left = new Identifier { Name = "value" },
            Right = new IntegerLiteral { Value = "0" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain("??");
    }

    #endregion

    #region Pipe Forward Operator Tests

    [Fact]
    public void GenerateExpression_PipeForward_SimpleFunction_GeneratesFunctionCall()
    {
        // Arrange: x |> f → F(x) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new Identifier { Name = "x" },
            Right = new Identifier { Name = "f" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("F(x)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_FunctionWithArgs_PrependsToPArguments()
    {
        // Arrange: x |> f(y) → F(x, y) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new Identifier { Name = "x" },
            Right = new FunctionCall
            {
                Function = new Identifier { Name = "f" },
                Arguments = new List<Expression>
                {
                    new Identifier { Name = "y" }
                }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("F(x,y)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_FunctionWithMultipleArgs_PrependsToPArguments()
    {
        // Arrange: x |> f(y, z) → F(x, y, z) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new Identifier { Name = "x" },
            Right = new FunctionCall
            {
                Function = new Identifier { Name = "f" },
                Arguments = new List<Expression>
                {
                    new Identifier { Name = "y" },
                    new Identifier { Name = "z" }
                }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("F(x,y,z)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_Chained_GeneratesNestedCalls()
    {
        // Arrange: x |> f |> g → G(F(x)) (function names are PascalCase in C#)
        // Parser makes this left-associative: (x |> f) |> g
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new BinaryOp
            {
                Operator = BinaryOperator.PipeForward,
                Left = new Identifier { Name = "x" },
                Right = new Identifier { Name = "f" }
            },
            Right = new Identifier { Name = "g" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("G(F(x))");
    }

    [Fact]
    public void GenerateExpression_PipeForward_ChainedWithArgs_GeneratesNestedCallsWithArgs()
    {
        // Arrange: x |> f(y) |> g(z) → G(F(x, y), z) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new BinaryOp
            {
                Operator = BinaryOperator.PipeForward,
                Left = new Identifier { Name = "x" },
                Right = new FunctionCall
                {
                    Function = new Identifier { Name = "f" },
                    Arguments = new List<Expression> { new Identifier { Name = "y" } }
                }
            },
            Right = new FunctionCall
            {
                Function = new Identifier { Name = "g" },
                Arguments = new List<Expression> { new Identifier { Name = "z" } }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("G(F(x,y),z)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_MemberAccess_GeneratesMethodCall()
    {
        // Arrange: x |> obj.method → obj.method(x)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new Identifier { Name = "x" },
            Right = new MemberAccess
            {
                Object = new Identifier { Name = "obj" },
                Member = "method"
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain("method");
        code.Should().Contain("(x)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_WithKeywordArgs_PreservesKeywordArgs()
    {
        // Arrange: x |> f(y, key=value) → F(x, y, key: value) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new Identifier { Name = "x" },
            Right = new FunctionCall
            {
                Function = new Identifier { Name = "f" },
                Arguments = new List<Expression> { new Identifier { Name = "y" } },
                KeywordArguments = new List<KeywordArgument>
                {
                    new KeywordArgument { Name = "key", Value = new Identifier { Name = "value" } }
                }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("F(x,y,key:value)");
    }

    [Fact]
    public void GenerateExpression_PipeForward_LiteralValue_GeneratesFunctionCallWithLiteral()
    {
        // Arrange: 42 |> f → F(42) (function names are PascalCase in C#)
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.PipeForward,
            Left = new IntegerLiteral { Value = "42" },
            Right = new Identifier { Name = "f" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("F(42)");
    }

    #endregion

    #region Membership and Identity Operator Tests

    [Fact]
    public void GenerateExpression_InOperator_GeneratesContainsCall()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.In,
            Left = new IntegerLiteral { Value = "5" },
            Right = new Identifier { Name = "items" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("__Contains__");
        code.Should().Contain("items");
        code.Should().Contain("5");
    }

    [Fact]
    public void GenerateExpression_NotInOperator_GeneratesNegatedContainsCall()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.NotIn,
            Left = new IntegerLiteral { Value = "5" },
            Right = new Identifier { Name = "items" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("!");
        code.Should().Contain("__Contains__");
        code.Should().Contain("items");
        code.Should().Contain("5");
    }

    [Fact]
    public void GenerateExpression_IsOperator_GeneratesReferenceEquals()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Is,
            Left = new Identifier { Name = "obj1" },
            Right = new Identifier { Name = "obj2" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("ReferenceEquals");
        code.Should().Contain("obj1");
        code.Should().Contain("obj2");
    }

    [Fact]
    public void GenerateExpression_IsNotOperator_GeneratesNegatedReferenceEquals()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.IsNot,
            Left = new Identifier { Name = "obj1" },
            Right = new Identifier { Name = "obj2" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("!");
        code.Should().Contain("ReferenceEquals");
        code.Should().Contain("obj1");
        code.Should().Contain("obj2");
    }

    [Fact]
    public void GenerateExpression_IsNone_GeneratesNullCheck()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Is,
            Left = new Identifier { Name = "value" },
            Right = new NoneLiteral()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        // Should generate: value == null (optimized from ReferenceEquals)
        (code.Contains("==null") || code.Contains("== null") || code.Contains("is null") || code.Contains("ReferenceEquals")).Should().BeTrue();
    }

    [Fact]
    public void GenerateExpression_IsNotNone_GeneratesNotNullCheck()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.IsNot,
            Left = new Identifier { Name = "value" },
            Right = new NoneLiteral()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        // Should generate: value != null (optimized from !ReferenceEquals)
        (code.Contains("!=null") || code.Contains("!= null") || code.Contains("is not null") || (code.Contains("!") && code.Contains("ReferenceEquals"))).Should().BeTrue();
    }

    #endregion

    #region Unary Operation Tests

    [Theory]
    [InlineData(UnaryOperator.Plus, "+")]
    [InlineData(UnaryOperator.Minus, "-")]
    [InlineData(UnaryOperator.Not, "!")]
    [InlineData(UnaryOperator.BitwiseNot, "~")]
    public void GenerateExpression_UnaryOp_GeneratesCorrectOperator(UnaryOperator op, string expectedOp)
    {
        // Arrange
        var expr = new UnaryOp
        {
            Operator = op,
            Operand = new IntegerLiteral { Value = "5" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain(expectedOp);
        result.ToString().Should().Contain("5");
    }

    #endregion

    #region Member Access Tests

    [Fact]
    public void GenerateExpression_SimpleMemberAccess_GeneratesDotAccess()
    {
        // Arrange
        var expr = new MemberAccess
        {
            Object = new Identifier { Name = "obj" },
            Member = "field",
            IsNullConditional = false
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain(".");
        code.Should().Contain("field");
        code.Should().NotContain("?");
    }

    [Fact]
    public void GenerateExpression_NullConditionalMemberAccess_GeneratesQuestionDot()
    {
        // Arrange
        var expr = new MemberAccess
        {
            Object = new Identifier { Name = "obj" },
            Member = "field",
            IsNullConditional = true
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain("?");
        code.Should().Contain("field");
    }

    #endregion

    #region Index and Slice Tests

    [Fact]
    public void GenerateExpression_IndexAccess_GeneratesBrackets()
    {
        // Arrange
        var expr = new IndexAccess
        {
            Object = new Identifier { Name = "arr" },
            Index = new IntegerLiteral { Value = "0" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("arr");
        code.Should().Contain("[");
        code.Should().Contain("0");
        code.Should().Contain("]");
    }

    [Fact]
    public void GenerateExpression_SliceAccess_GeneratesRuntimeSliceCall()
    {
        // Arrange
        var expr = new SliceAccess
        {
            Object = new Identifier { Name = "arr" },
            Start = new IntegerLiteral { Value = "1" },
            Stop = new IntegerLiteral { Value = "5" },
            Step = null
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Core.Slice");
        code.Should().Contain("arr");
        code.Should().Contain("1");
        code.Should().Contain("5");
    }

    #endregion

    #region Comparison Chain Tests

    [Fact]
    public void GenerateExpression_ComparisonChain_GeneratesAndedComparisons()
    {
        // Arrange
        var expr = new ComparisonChain
        {
            Operands = new List<Expression>
            {
                new Identifier { Name = "a" },
                new Identifier { Name = "b" },
                new Identifier { Name = "c" }
            },
            Operators = new List<ComparisonOperator>
            {
                ComparisonOperator.LessThan,
                ComparisonOperator.LessThan
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("a");
        code.Should().Contain("b");
        code.Should().Contain("c");
        code.Should().Contain("<");
        code.Should().Contain("&&");
    }

    #endregion

    #region Conditional Expression Tests

    [Fact]
    public void GenerateExpression_ConditionalExpression_GeneratesTernary()
    {
        // Arrange
        var expr = new ConditionalExpression
        {
            Test = new BooleanLiteral { Value = true },
            ThenValue = new IntegerLiteral { Value = "1" },
            ElseValue = new IntegerLiteral { Value = "2" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("?");
        code.Should().Contain(":");
        code.Should().Contain("1");
        code.Should().Contain("2");
    }

    #endregion

    #region Lambda Expression Tests

    [Fact]
    public void GenerateExpression_LambdaNoParams_GeneratesParenthesizedLambda()
    {
        // Arrange
        var expr = new LambdaExpression
        {
            Parameters = new List<Parameter>(),
            Body = new IntegerLiteral { Value = "42" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("()");
        code.Should().Contain("=>");
        code.Should().Contain("42");
    }

    [Fact]
    public void GenerateExpression_LambdaOneParam_GeneratesSimpleLambda()
    {
        // Arrange
        var expr = new LambdaExpression
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "x" }
            },
            Body = new BinaryOp
            {
                Operator = BinaryOperator.Multiply,
                Left = new Identifier { Name = "x" },
                Right = new IntegerLiteral { Value = "2" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("x");
        code.Should().Contain("=>");
        code.Should().Contain("*");
        code.Should().Contain("2");
    }

    [Fact]
    public void GenerateExpression_LambdaTwoParams_GeneratesParenthesizedLambda()
    {
        // Arrange
        var expr = new LambdaExpression
        {
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "x" },
                new Parameter { Name = "y" }
            },
            Body = new BinaryOp
            {
                Operator = BinaryOperator.Add,
                Left = new Identifier { Name = "x" },
                Right = new Identifier { Name = "y" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("(x,y)");
        code.Should().Contain("=>");
        code.Should().Contain("+");
    }

    #endregion

    #region Type Cast and Check Tests

    [Fact]
    public void GenerateExpression_TypeCast_GeneratesCastExpression()
    {
        // Arrange
        var expr = new TypeCast
        {
            Value = new Identifier { Name = "value" },
            TargetType = new TypeAnnotation { Name = "int" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("(int)");
        code.Should().Contain("value");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NonNullable_GeneratesCastExpression()
    {
        // Arrange: value to int → (int)value
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "int", IsNullable = false }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("(int)");
        code.Should().Contain("obj");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NullableValueType_GeneratesIsPattern()
    {
        // Arrange: value to int? → value is int _temp ? (int?)_temp : (int?)null
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "int", IsNullable = true }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        // Should use is pattern for value types
        code.Should().Contain("is");
        code.Should().Contain("int");
        code.Should().Contain("?");
        code.Should().Contain(":");
        code.Should().Contain("null");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NullableReferenceType_GeneratesAsExpression()
    {
        // Arrange: value to str? → value as string
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "str", IsNullable = true }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("as");
        code.Should().Contain("string");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NullableUserType_GeneratesAsExpression()
    {
        // Arrange: value to Dog? → value as Dog
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "animal" },
            TargetType = new TypeAnnotation { Name = "Dog", IsNullable = true }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("as");
        code.Should().Contain("Dog");
    }

    [Fact]
    public void GenerateExpression_TypeCheck_GeneratesIsExpression()
    {
        // Arrange
        var expr = new TypeCheck
        {
            Value = new Identifier { Name = "obj" },
            CheckType = new TypeAnnotation { Name = "str" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain("is");
        code.Should().Contain("string");
    }

    #endregion

    #region F-String Tests

    [Fact]
    public void GenerateExpression_FString_GeneratesInterpolatedString()
    {
        // Arrange
        var expr = new FStringLiteral
        {
            Parts = new List<FStringPart>
            {
                new FStringPart { Text = "Hello " },
                new FStringPart { Expression = new Identifier { Name = "name" } },
                new FStringPart { Text = "!" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("$");
        code.Should().Contain("Hello ");
        code.Should().Contain("name");
        code.Should().Contain("!");
    }

    #endregion

    #region Function Call Tests

    [Fact]
    public void GenerateExpression_SimpleFunctionCall_GeneratesInvocation()
    {
        // Arrange
        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "print" },
            Arguments = new List<Expression>
            {
                new StringLiteral { Value = "hello" }
            }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Print");
        code.Should().Contain("(");
        code.Should().Contain("\"hello\"");
        code.Should().Contain(")");
    }

    #endregion

    #region Parenthesized Expression Tests

    [Fact]
    public void GenerateExpression_Parenthesized_PreservesParentheses()
    {
        // Arrange
        var expr = new Parenthesized
        {
            Expression = new IntegerLiteral { Value = "42" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert - parentheses must be preserved to maintain correct operator precedence
        result.ToString().Should().Be("(42)");
    }

    #endregion
}
