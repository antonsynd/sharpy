using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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
    [InlineData("5.0", "5.0")]
    [InlineData("100.0", "100.0")]
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
        // A bare None with no Optional target context emits `null` (valid for object/
        // nullable targets and `case null:` patterns). Optional<T> targets are handled
        // separately via GenerateOptionalNone.
        result.ToString().Should().Be("null");
    }

    #endregion

    #region Collection Literal Tests

    [Fact]
    public void GenerateExpression_EmptyListLiteral_GeneratesList()
    {
        // Arrange
        var expr = new ListLiteral { Elements = ImmutableArray<Expression>.Empty };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        result.ToString().Should().Contain("Sharpy.List");
    }

    [Fact]
    public void GenerateExpression_ListOfIntegers_GeneratesListInt()
    {
        // Arrange
        var expr = new ListLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" },
                new IntegerLiteral { Value = "3" }
            }.ToImmutableArray()
        };

        // Element type is authoritative from SemanticInfo (TypeChecker), not syntactic inference.
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetExpressionType(expr, new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { BuiltinType.Int }
        });
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.List<int>");
        code.Should().Contain("1");
        code.Should().Contain("2");
        code.Should().Contain("3");
    }

    [Fact]
    public void GenerateExpression_DictLiteral_GeneratesDictionary()
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
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Dict");
        code.Should().Contain("\"a\"");
        code.Should().Contain("\"b\"");
    }

    [Fact]
    public void GenerateExpression_SetLiteral_GeneratesHashSet()
    {
        // Arrange
        var expr = new SetLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" }
            }.ToImmutableArray()
        };

        // Element type is authoritative from SemanticInfo (TypeChecker), not syntactic inference.
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetExpressionType(expr, new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { BuiltinType.Int }
        });
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Set<int>");
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
            }.ToImmutableArray()
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
    public void GenerateExpression_Modulo_UsesFloorMod()
    {
        // Arrange - integer % lowers to Python floored modulo (sign of divisor), not C#'s
        // truncated `%`. Numeric operands (here bare int literals) route to Builtins.FloorMod (#1153).
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Modulo,
            Left = new IntegerLiteral { Value = "5" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Builtins.FloorMod");
        code.Should().Contain("5");
        code.Should().Contain("3");
    }

    [Fact]
    public void GenerateExpression_Modulo_DecimalOperands_UsesDecimalModHelper()
    {
        // Arrange - decimal % keeps native truncating semantics and routes through the Core
        // helper Builtins.DecimalMod, which owns the zero guard (the InvalidOperation that
        // mirrors CPython decimal.InvalidOperation, #1189). The guard used to be an emitted
        // ternary that spliced the divisor twice, so a side-effecting divisor ran twice
        // (#1216); the invocation form splices each operand exactly once. The CS0020 dodge
        // that forced Decimal.Remainder into the emitter does not apply inside the helper,
        // whose operands are runtime parameters rather than constants.
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Modulo,
            Left = new FloatLiteral { Value = "7", Suffix = "m" },
            Right = new FloatLiteral { Value = "3", Suffix = "m" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.DecimalMod");
        code.Should().NotContain("global::System.Decimal.Remainder");
        code.Should().NotContain("global::Sharpy.InvalidOperation");
        code.Should().NotContain("decimal modulo by zero");
        code.Should().NotContain("FloorMod");
        // Single evaluation: the divisor appears once in the emitted expression.
        CountOccurrences(code, "3m").Should().Be(1);
    }

    [Fact]
    public void GenerateExpression_Modulo_MixedDecimalInt_UsesDecimalModHelper()
    {
        // Arrange - one decimal operand is enough to select the decimal path (#1188 promotion:
        // decimal mixes with integer kinds); the int operand implicit-converts to decimal at
        // the helper's parameter.
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Modulo,
            Left = new FloatLiteral { Value = "7", Suffix = "m" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.DecimalMod");
        code.Should().NotContain("global::System.Decimal.Remainder");
        code.Should().NotContain("FloorMod");
    }

    [Fact]
    public void GenerateExpression_FloorDivide_DecimalOperands_UsesDecimalFloorDivHelper()
    {
        // Arrange - the `//` counterpart of the two `%` tests above (#1174 introduced the
        // decimal arm; #1216 moved its guard into Core). Decimal `//` truncates toward zero
        // rather than flooring, and its zero divisor raises ZeroDivisionError — deliberately
        // NOT the InvalidOperation that `%` raises (#1189). Both facts now live in
        // Builtins.DecimalFloorDiv, so the emitted form is a single invocation.
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new FloatLiteral { Value = "7", Suffix = "m" },
            Right = new FloatLiteral { Value = "3", Suffix = "m" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.DecimalFloorDiv");
        code.Should().NotContain("global::System.Decimal.Divide");
        code.Should().NotContain("global::System.Decimal.Truncate");
        code.Should().NotContain("global::Sharpy.ZeroDivisionError");
        code.Should().NotContain("System.Math.Floor");
        // Single evaluation: the divisor appears once in the emitted expression.
        CountOccurrences(code, "3m").Should().Be(1);
    }

    [Fact]
    public void GenerateExpression_FloorDivide_MixedDecimalInt_UsesDecimalFloorDivHelper()
    {
        // Arrange - a single decimal operand selects the decimal arm for `//` too (#1188).
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new FloatLiteral { Value = "7", Suffix = "m" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.DecimalFloorDiv");
        code.Should().NotContain("global::System.Decimal.Divide");
        code.Should().NotContain("System.Math.Floor");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, System.StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, System.StringComparison.Ordinal);
        }

        return count;
    }

    [Fact]
    public void GenerateExpression_PowerOperator_EmitsOneCheckedIntPowInvocation()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.Power,
            Left = new IntegerLiteral { Value = "2" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr).ToString();

        // Assert — integer ** emits ONE CheckedIntPow invocation splicing each operand once (#1228).
        // This test previously asserted "System.Math.Pow", which was the SATURATING fallback the
        // emitter dropped into when its side-effect gate declined; that path is gone, along with the
        // gate, the negative-exponent dispatch ternary and the operand regeneration behind it.
        // Renamed rather than edited in place, because the old name asserted the old lowering.
        result.Should().Contain("CheckedIntPow", "integer ** lowers to the checked helper");
        result.Should().NotContain("Math.Pow", "the saturating double path is no longer emitted");
        CountOccurrences(result, "(2)").Should().Be(1, "the base is spliced exactly once");
        CountOccurrences(result, "(3)").Should().Be(1, "the exponent is spliced exactly once");
    }

    [Fact]
    public void GenerateExpression_FloorDivide_EmitsOneFloorDivInvocation()
    {
        // Arrange
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new IntegerLiteral { Value = "10" },
            Right = new IntegerLiteral { Value = "3" }
        };

        // Act
        var result = InvokeGenerateExpression(expr).ToString();

        // Assert — integer // emits ONE Builtins.FloorDiv invocation (#1226). It previously built
        // `(int)Math.Floor((double)x / y)` wrapped in a `y == 0 ? throw ... : ...` ternary, which
        // spliced the DIVISOR TWICE; the zero guard now lives inside the helper (the #1216 shape),
        // and the quotient is computed in integer arithmetic rather than through a double.
        result.Should().Contain("FloorDiv", "integer // lowers to the Core helper");
        result.Should().NotContain("Math.Floor", "the double round-trip is no longer emitted");
        result.Should().NotContain("ZeroDivisionError", "the guard moved into the helper");
        CountOccurrences(result, "3").Should().Be(1, "the divisor is spliced exactly once");
    }


    [Fact]
    public void GenerateExpression_FloorDivide_WithFloatOperand_ReturnsFloat()
    {
        // Arrange - Float operands should return float without cast to long
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new FloatLiteral { Value = "7.5" },
            Right = new FloatLiteral { Value = "2.0" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert - Float floor division delegates to the Core helper, which carries CPython's
        // float_floor_div algorithm and the zero guard (#1185). Math.Floor(a / b) is not
        // equivalent: 1.0 // 0.1 would give 10.0 instead of 9.0.
        // 7.5 // 2.0 = 3.0 (float), not 3 (int)
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.FloorDiv");
        code.Should().NotContain("System.Math.Floor");
        code.Should().NotContain("(long)");
        code.Should().NotContain("(int)");
    }

    [Fact]
    public void GenerateExpression_FloorDivide_MixedIntFloat_ReturnsFloat()
    {
        // Arrange - Mixed int/float should return float
        var expr = new BinaryOp
        {
            Operator = BinaryOperator.FloorDivide,
            Left = new IntegerLiteral { Value = "7" },
            Right = new FloatLiteral { Value = "2.0" }
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert - Mixed floor division should return float type, via the same Core helper
        // 7 // 2.0 = 3.0 (float), not 3 (int)
        var code = result.ToString();
        code.Should().Contain("global::Sharpy.Builtins.FloorDiv");
        code.Should().NotContain("System.Math.Floor");
        code.Should().NotContain("(long)");
        code.Should().NotContain("(int)");
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
                }.ToImmutableArray()
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
                }.ToImmutableArray()
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
                    Arguments = new List<Expression> { new Identifier { Name = "y" } }.ToImmutableArray()
                }
            },
            Right = new FunctionCall
            {
                Function = new Identifier { Name = "g" },
                Arguments = new List<Expression> { new Identifier { Name = "z" } }.ToImmutableArray()
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
        // Arrange: x |> obj.method → obj.Method(x) (method names are PascalCase in C#)
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

        // Assert - method names are transformed to PascalCase (method -> Method)
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain("Method");
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
                Arguments = new List<Expression> { new Identifier { Name = "y" } }.ToImmutableArray(),
                KeywordArguments = new List<KeywordArgument>
                {
                    new KeywordArgument { Name = "key", Value = new Identifier { Name = "value" } }
                }.ToImmutableArray()
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
        code.Should().Contain("Contains");
        code.Should().NotContain("__Contains__");
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
        code.Should().Contain("Contains");
        code.Should().NotContain("__Contains__");
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

        // Assert - Member names are transformed to PascalCase (field -> Field)
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain(".");
        code.Should().Contain("Field");
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

        // Assert - Member names are transformed to PascalCase (field -> Field)
        var code = result.ToString();
        code.Should().Contain("obj");
        code.Should().Contain("?");
        code.Should().Contain("Field");
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
        // Arrange. The emitter is a pure translator: it refuses to generate a slice whose
        // receiver semantic analysis did not classify (#1608), so the lowering fact must be
        // recorded the way the pipeline records it.
        var expr = new SliceAccess
        {
            Object = new Identifier { Name = "arr" },
            Start = new IntegerLiteral { Value = "1" },
            Stop = new IntegerLiteral { Value = "5" },
            Step = null
        };
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetSliceLowering(expr, new SliceLowering(SliceLoweringKind.List));
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("Sharpy.Slice");
        code.Should().Contain("arr");
        code.Should().Contain("1");
        code.Should().Contain("5");
    }

    [Fact]
    public void GenerateExpression_SliceAccess_NdArrayLowering_GeneratesSliceSpecCall()
    {
        // Arrange
        var expr = new SliceAccess
        {
            Object = new Identifier { Name = "arr" },
            Start = new IntegerLiteral { Value = "1" },
            Stop = new IntegerLiteral { Value = "4" },
            Step = null
        };
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetSliceLowering(expr, new SliceLowering(SliceLoweringKind.NdArray));
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert: arr.Slice(new Sharpy.SliceSpec((int?)1, (int?)4)) — the per-axis NdArray
        // shape, not Sharpy.Slice.GetSlice (which has no NdArray overload).
        var code = result.ToString();
        code.Should().Contain("arr.Slice");
        code.Should().Contain("SliceSpec");
        code.Should().NotContain("GetSlice");
    }

    [Fact]
    public void GenerateExpression_SliceAccess_WithoutRecordedLowering_Throws()
    {
        // Arrange: no SliceLowering fact recorded — the emitter must fail loudly rather than
        // guess a lowering (#1608, Design Decision 1).
        var expr = new SliceAccess
        {
            Object = new Identifier { Name = "arr" },
            Start = new IntegerLiteral { Value = "1" },
            Stop = new IntegerLiteral { Value = "5" },
            Step = null
        };
        _context.SemanticInfo = new SemanticInfo();

        // Act
        var act = () => InvokeGenerateExpression(expr);

        // Assert
        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*No SliceLowering recorded*");
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
            }.ToImmutableArray(),
            Operators = new List<ComparisonOperator>
            {
                ComparisonOperator.LessThan,
                ComparisonOperator.LessThan
            }.ToImmutableArray()
        };
        // The emitter is a pure translator: every chain link lowers by the fact CheckComparisonChain
        // records (#1642), so the test records the native links the pipeline would.
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetComparisonChainLowering(expr, new ComparisonChainLowering(
            ImmutableArray.Create(
                new ComparisonLinkLowering(OperatorLoweringKind.Native, null),
                new ComparisonLinkLowering(OperatorLoweringKind.Native, null))));
        _context.SemanticInfo = semanticInfo;

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
            Parameters = ImmutableArray<Parameter>.Empty,
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
            }.ToImmutableArray(),
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
            }.ToImmutableArray(),
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

    #region Type Coercion and Check Tests

    [Fact]
    public void GenerateExpression_TypeCoercion_NonNullable_GeneratesCastExpression()
    {
        // Arrange: value to int → (int)value
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "int", IsOptional = false }
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
        // Arrange: value to int? → value is int _temp ? Optional<int>.Some(_temp) : default
        // The parser sets Mode from the target's nullability for `to`-forms (#1029).
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "int", IsOptional = true },
            Mode = CastFailureMode.Null
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        // Should use is pattern with Optional<T>.Some and default
        code.Should().Contain("is");
        code.Should().Contain("int");
        code.Should().Contain("Optional<int>.Some(");
        code.Should().Contain("default");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NullableReferenceType_GeneratesIsPattern()
    {
        // Arrange: value to str? → value is string _temp ? Optional<string>.Some(_temp) : default
        // The parser sets Mode from the target's nullability for `to`-forms (#1029).
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "obj" },
            TargetType = new TypeAnnotation { Name = "str", IsOptional = true },
            Mode = CastFailureMode.Null
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("is");
        code.Should().Contain("string");
        code.Should().Contain("Optional<string>.Some(");
        code.Should().Contain("default");
    }

    [Fact]
    public void GenerateExpression_TypeCoercion_NullableUserType_GeneratesIsPattern()
    {
        // Arrange: value to Dog? → value is Dog _temp ? Optional<Dog>.Some(_temp) : default
        // The parser sets Mode from the target's nullability for `to`-forms (#1029).
        var expr = new TypeCoercion
        {
            Value = new Identifier { Name = "animal" },
            TargetType = new TypeAnnotation { Name = "Dog", IsOptional = true },
            Mode = CastFailureMode.Null
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Contain("is");
        code.Should().Contain("Dog");
        code.Should().Contain("Optional<Dog>.Some(");
        code.Should().Contain("default");
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
            }.ToImmutableArray()
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
            }.ToImmutableArray()
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

    [Fact]
    public void GenerateExpression_ClassInstantiation_GeneratesNewExpression()
    {
        // Arrange: Register a class type in the symbol table
        var classSymbol = new TypeSymbol
        {
            Name = "Point",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        _context.SymbolTable.Define(classSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Point" },
            Arguments = new List<Expression>
            {
                new IntegerLiteral { Value = "3" },
                new IntegerLiteral { Value = "4" }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Point");
        code.Should().Contain("3");
        code.Should().Contain("4");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_ClassInstantiation_WithSnakeCaseName_AppliesNameMangling()
    {
        // Arrange: Register a class type with snake_case name
        var classSymbol = new TypeSymbol
        {
            Name = "user_profile",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        _context.SymbolTable.Define(classSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "user_profile" },
            Arguments = new List<Expression>
            {
                new StringLiteral { Value = "john" }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("UserProfile");
        code.Should().Contain("\"john\"");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_ClassInstantiation_WithKeywordArguments_GeneratesNamedArguments()
    {
        // Arrange: Register a class type
        var classSymbol = new TypeSymbol
        {
            Name = "Rectangle",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        _context.SymbolTable.Define(classSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Rectangle" },
            Arguments = ImmutableArray<Expression>.Empty,
            KeywordArguments = new List<KeywordArgument>
            {
                new KeywordArgument
                {
                    Name = "width",
                    Value = new IntegerLiteral { Value = "10" }
                },
                new KeywordArgument
                {
                    Name = "height",
                    Value = new IntegerLiteral { Value = "20" }
                }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Rectangle");
        code.Should().Contain("width");
        code.Should().Contain("10");
        code.Should().Contain("height");
        code.Should().Contain("20");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_ClassInstantiation_ParameterlessConstructor_GeneratesEmptyArgList()
    {
        // Arrange: Register a class type
        var classSymbol = new TypeSymbol
        {
            Name = "Empty",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        _context.SymbolTable.Define(classSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Empty" },
            Arguments = ImmutableArray<Expression>.Empty
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Empty");
        code.Should().Contain("()");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_StructInstantiation_WithArguments_GeneratesObjectCreation()
    {
        // Arrange: Register a struct type
        var structSymbol = new TypeSymbol
        {
            Name = "Point",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct
        };
        _context.SymbolTable.Define(structSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Point" },
            Arguments = new List<Expression>
            {
                new IntegerLiteral { Value = "10" },
                new IntegerLiteral { Value = "20" }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Point");
        code.Should().Contain("10");
        code.Should().Contain("20");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_StructInstantiation_ParameterlessConstructor_GeneratesEmptyArgList()
    {
        // Arrange: Register a struct type
        var structSymbol = new TypeSymbol
        {
            Name = "Vector2",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct
        };
        _context.SymbolTable.Define(structSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Vector2" },
            Arguments = ImmutableArray<Expression>.Empty
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Vector2");
        code.Should().Contain("()");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_StructInstantiation_WithSnakeCaseName_AppliesNameMangling()
    {
        // Arrange: Register a struct type with snake_case name
        var structSymbol = new TypeSymbol
        {
            Name = "game_state",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct
        };
        _context.SymbolTable.Define(structSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "game_state" },
            Arguments = new List<Expression>
            {
                new IntegerLiteral { Value = "1" }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("GameState");
        code.Should().Contain("1");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_StructInstantiation_WithKeywordArguments_GeneratesNamedArguments()
    {
        // Arrange: Register a struct type
        var structSymbol = new TypeSymbol
        {
            Name = "Color",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Struct
        };
        _context.SymbolTable.Define(structSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "Color" },
            Arguments = ImmutableArray<Expression>.Empty,
            KeywordArguments = new List<KeywordArgument>
            {
                new KeywordArgument
                {
                    Name = "r",
                    Value = new IntegerLiteral { Value = "255" }
                },
                new KeywordArgument
                {
                    Name = "g",
                    Value = new IntegerLiteral { Value = "128" }
                },
                new KeywordArgument
                {
                    Name = "b",
                    Value = new IntegerLiteral { Value = "0" }
                }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().StartWith("new");
        code.Should().Contain("Color");
        code.Should().Contain("r");
        code.Should().Contain("255");
        code.Should().Contain("g");
        code.Should().Contain("128");
        code.Should().Contain("b");
        code.Should().Contain("0");
        result.Should().BeOfType<ObjectCreationExpressionSyntax>();
    }

    [Fact]
    public void GenerateExpression_RegularFunctionCall_NotClass_GeneratesInvocation()
    {
        // Arrange: Register a regular function (not a class)
        var funcSymbol = new FunctionSymbol
        {
            Name = "calculate",
            Kind = SymbolKind.Function
        };
        _context.SymbolTable.Define(funcSymbol);

        var expr = new FunctionCall
        {
            Function = new Identifier { Name = "calculate" },
            Arguments = new List<Expression>
            {
                new IntegerLiteral { Value = "5" }
            }.ToImmutableArray()
        };

        // Act
        var result = InvokeGenerateExpression(expr);

        // Assert
        var code = result.ToString();
        code.Should().Be("Calculate(5)");
        code.Should().NotContain("new");
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

    #region Collection Literal SemanticInfo Fallback Tests

    [Fact]
    public void GenerateExpression_ListLiteralWithIdentifiers_UsesSemanticInfoFallback()
    {
        // Arrange: Create a list literal with identifier elements (not literal values)
        // so that element-type inference from literals fails and the SemanticInfo fallback is used
        var listLiteral = new ListLiteral
        {
            Elements = new List<Expression>
            {
                new Identifier { Name = "x" },
                new Identifier { Name = "y" }
            }.ToImmutableArray()
        };

        // Set up SemanticInfo with the type for this list literal node
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetExpressionType(listLiteral, new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { BuiltinType.Int }
        });
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(listLiteral);

        // Assert: The generated code should use int from SemanticInfo, not object from fallback
        var code = result.ToString();
        code.Should().Contain("Sharpy.List<int>");
        code.Should().Contain("x");
        code.Should().Contain("y");
    }

    [Fact]
    public void GenerateExpression_SetLiteralWithIdentifiers_UsesSemanticInfoFallback()
    {
        // Arrange: Create a set literal with identifier elements
        var setLiteral = new SetLiteral
        {
            Elements = new List<Expression>
            {
                new Identifier { Name = "a" },
                new Identifier { Name = "b" }
            }.ToImmutableArray()
        };

        // Set up SemanticInfo with the type for this set literal node
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetExpressionType(setLiteral, new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { BuiltinType.Str }
        });
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(setLiteral);

        // Assert: The generated code should use string from SemanticInfo
        var code = result.ToString();
        code.Should().Contain("Sharpy.Set<string>");
    }

    [Fact]
    public void GenerateExpression_DictLiteralWithIdentifiers_UsesSemanticInfoFallback()
    {
        // Arrange: Create a dict literal with identifier keys/values
        var dictLiteral = new DictLiteral
        {
            Entries = new List<DictEntry>
            {
                new DictEntry
                {
                    Key = new Identifier { Name = "k" },
                    Value = new Identifier { Name = "v" }
                }
            }.ToImmutableArray()
        };

        // Set up SemanticInfo with the type for this dict literal node
        var semanticInfo = new SemanticInfo();
        semanticInfo.SetExpressionType(dictLiteral, new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { BuiltinType.Str, BuiltinType.Int }
        });
        _context.SemanticInfo = semanticInfo;

        // Act
        var result = InvokeGenerateExpression(dictLiteral);

        // Assert: The generated code should use string,int from SemanticInfo
        var code = result.ToString();
        code.Should().Contain("Dict<string,int>");
    }

    [Fact]
    public void GenerateExpression_ListLiteralWithoutSemanticInfo_UsesNeutralObjectElementType()
    {
        // Arrange: List with integer literals but NO SemanticInfo set.
        // The emitter's syntactic element-type inference tier was removed (#1039/#973): the emitter
        // is a pure translator, so with no authoritative element type it emits the neutral object.
        // In production the TypeChecker always records the concrete element type in SemanticInfo.
        var listLiteral = new ListLiteral
        {
            Elements = new List<Expression>
            {
                new IntegerLiteral { Value = "1" },
                new IntegerLiteral { Value = "2" }
            }.ToImmutableArray()
        };

        // SemanticInfo is NOT set (default null on context)

        // Act
        var result = InvokeGenerateExpression(listLiteral);

        // Assert: no authoritative element type available -> neutral object element type
        var code = result.ToString();
        code.Should().Contain("Sharpy.List<object>");
    }

    #endregion
}
