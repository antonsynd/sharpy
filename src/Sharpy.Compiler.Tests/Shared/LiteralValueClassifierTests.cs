using System.Text.RegularExpressions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Shared;

public class LiteralValueClassifierTests
{
    private readonly ITestOutputHelper _output;

    public LiteralValueClassifierTests(ITestOutputHelper output) => _output = output;

    #region StringLiteral

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("", "")]
    [InlineData("with spaces", "with spaces")]
    public void TryGetLiteralValue_StringLiteral_ReturnsStringValue(string input, string expected)
    {
        var expr = new StringLiteral { Value = input };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<string>(result);
        Assert.Equal(expected, result);
    }

    #endregion

    #region IntegerLiteral

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("1_000_000")]
    public void TryGetLiteralValue_IntegerLiteral_ReturnsRawString(string value)
    {
        var expr = new IntegerLiteral { Value = value };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<string>(result);
        Assert.Equal(value, result);
    }

    #endregion

    #region FloatLiteral

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("0.0", 0.0)]
    [InlineData("1e10", 1e10)]
    [InlineData("2_000.5", 2000.5)]
    public void TryGetLiteralValue_FloatLiteral_ReturnsDouble(string value, double expected)
    {
        var expr = new FloatLiteral { Value = value };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<double>(result);
        Assert.Equal(expected, (double)result!);
    }

    #endregion

    #region BooleanLiteral

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetLiteralValue_BooleanLiteral_ReturnsBool(bool value)
    {
        var expr = new BooleanLiteral { Value = value };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<bool>(result);
        Assert.Equal(value, result);
    }

    #endregion

    #region NoneLiteral

    [Fact]
    public void TryGetLiteralValue_NoneLiteral_ReturnsNoneValue()
    {
        var expr = new NoneLiteral();
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.Same(AstHelper.NoneValue, result);
    }

    #endregion

    #region Negated Literals

    [Theory]
    [InlineData("42", "-42")]
    [InlineData("0", "-0")]
    [InlineData("1_000", "-1_000")]
    public void TryGetLiteralValue_NegatedInteger_ReturnsNegatedString(string inner, string expected)
    {
        var expr = new UnaryOp
        {
            Operator = UnaryOperator.Minus,
            Operand = new IntegerLiteral { Value = inner }
        };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<string>(result);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14", -3.14)]
    [InlineData("0.0", 0.0)]
    [InlineData("1e10", -1e10)]
    public void TryGetLiteralValue_NegatedFloat_ReturnsNegatedDouble(string inner, double expected)
    {
        var expr = new UnaryOp
        {
            Operator = UnaryOperator.Minus,
            Operand = new FloatLiteral { Value = inner }
        };
        var result = AstHelper.TryGetLiteralValue(expr);
        Assert.IsType<double>(result);
        Assert.Equal(expected, (double)result!);
    }

    #endregion

    #region Non-literal expressions → null

    [Fact]
    public void TryGetLiteralValue_Identifier_ReturnsNull()
    {
        var expr = new Identifier { Name = "x" };
        Assert.Null(AstHelper.TryGetLiteralValue(expr));
    }

    [Fact]
    public void TryGetLiteralValue_FunctionCall_ReturnsNull()
    {
        var expr = new FunctionCall { Function = new Identifier { Name = "foo" } };
        Assert.Null(AstHelper.TryGetLiteralValue(expr));
    }

    [Fact]
    public void TryGetLiteralValue_BinaryOp_ReturnsNull()
    {
        var expr = new BinaryOp
        {
            Left = new IntegerLiteral { Value = "1" },
            Operator = BinaryOperator.Add,
            Right = new IntegerLiteral { Value = "2" }
        };
        Assert.Null(AstHelper.TryGetLiteralValue(expr));
    }

    [Fact]
    public void TryGetLiteralValue_UnaryPlus_ReturnsNull()
    {
        var expr = new UnaryOp
        {
            Operator = UnaryOperator.Plus,
            Operand = new IntegerLiteral { Value = "42" }
        };
        Assert.Null(AstHelper.TryGetLiteralValue(expr));
    }

    [Fact]
    public void TryGetLiteralValue_EllipsisLiteral_ReturnsNull()
    {
        var expr = new EllipsisLiteral();
        Assert.Null(AstHelper.TryGetLiteralValue(expr));
    }

    #endregion

    #region TryGetLiteralValue arm pinning

    private const string SourceFile = "src/Sharpy.Compiler/Shared/AstHelper.cs";

    private static readonly string[] ExpectedArmPatterns =
    {
        "StringLiteral s",
        "IntegerLiteral i",
        "FloatLiteral f",
        "BooleanLiteral b",
        "NoneLiteral",
        "UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral negInt }",
        "UnaryOp { Operator: UnaryOperator.Minus, Operand: FloatLiteral negFloat }",
        "_",
    };

    [Fact]
    public void TryGetLiteralValue_Arms_MatchPinnedPatterns()
    {
        var arms = SwitchArmScan.ArmPatternTexts(SourceFile, "TryGetLiteralValue");
        Assert.NotEmpty(arms);

        foreach (var arm in arms)
            _output.WriteLine($"  {arm}");

        Assert.Equal(
            ExpectedArmPatterns.OrderBy(x => x, StringComparer.Ordinal),
            arms.OrderBy(x => x, StringComparer.Ordinal));
    }

    #endregion

    #region Re-derivation guard

    [Fact]
    public void EmitterLruCacheFile_DoesNotContainLiteralExtractionSwitch()
    {
        var repoRoot = FindRepoRoot();
        var lruCacheFile = Path.Combine(repoRoot,
            "src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.LruCache.cs");
        var source = File.ReadAllText(lruCacheFile);

        Assert.DoesNotContain("GetLruCacheMaxSize", source);

        Assert.DoesNotMatch(
            @"IntegerLiteral\s+\w+\s+when\s+int\.TryParse",
            source);
    }

    #endregion

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException(
            $"Could not find repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
