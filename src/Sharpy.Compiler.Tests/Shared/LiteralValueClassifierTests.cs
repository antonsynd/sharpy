using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    #region Re-derivation guard (literal-value family)

    /// <summary>
    /// The four former re-derivation sites of the literal-value classifier (#1716): each used to
    /// hold its own <c>switch</c> over literal AST kinds; all now call
    /// <see cref="AstHelper.TryGetLiteralValue"/>. The only switch that may name a literal kind
    /// lives in <see cref="SourceFile"/> (pinned arm-by-arm above).
    /// </summary>
    private static readonly string[] LiteralValueFamilyFiles =
    {
        "src/Sharpy.Compiler/Project/GeneratorContextBuilder.cs",
        "src/Sharpy.Compiler/Semantic/Validation/DecoratorValidator.Caching.cs",
        "src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs",
        "src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.LruCache.cs",
    };

    private static readonly HashSet<string> LiteralAstKinds = new(StringComparer.Ordinal)
    {
        nameof(IntegerLiteral),
        nameof(FloatLiteral),
        nameof(StringLiteral),
        nameof(BooleanLiteral),
        nameof(NoneLiteral),
    };

    /// <summary>
    /// Family-wide guard (verify-round finding P3b.4 — the previous guard scanned ONE file for
    /// ONE regex): every switch statement and switch expression in each family file is
    /// enumerated with Roslyn, and no arm pattern may name a literal AST kind. A re-derived
    /// classifier in any of the four files fails its own row.
    /// </summary>
    [Theory]
    [InlineData("src/Sharpy.Compiler/Project/GeneratorContextBuilder.cs")]
    [InlineData("src/Sharpy.Compiler/Semantic/Validation/DecoratorValidator.Caching.cs")]
    [InlineData("src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs")]
    [InlineData("src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.LruCache.cs")]
    public void LiteralValueFamilyFile_HasNoSwitchArmNamingALiteralKind(string repoRelativePath)
    {
        Assert.Contains(repoRelativePath, LiteralValueFamilyFiles);

        var hits = LiteralKindSwitchArms(repoRelativePath);
        foreach (var hit in hits)
            _output.WriteLine($"  RE-DERIVATION: {hit}");

        Assert.True(hits.Count == 0,
            $"{repoRelativePath} has {hits.Count} switch arm(s) naming a literal AST kind — " +
            $"the literal-value classifier is AstHelper.TryGetLiteralValue, not a local switch:\n  " +
            string.Join("\n  ", hits));
    }

    /// <summary>
    /// Positive control for the absence assertion above: the same probe, applied to the file
    /// that legitimately holds the classifier switch, must hit — once per literal kind.
    /// </summary>
    [Fact]
    public void LiteralKindSwitchArmProbe_HitsEveryKindInTheClassifierFile()
    {
        var hits = LiteralKindSwitchArms(SourceFile);
        foreach (var hit in hits)
            _output.WriteLine($"  CLASSIFIER ARM: {hit}");

        Assert.NotEmpty(hits);
        foreach (var kind in LiteralAstKinds)
            Assert.Contains(hits, h => h.Contains(kind));
    }

    /// <summary>
    /// The emitter reads the materialized <c>FunctionSymbol.CacheMaxSize</c>; the deleted
    /// re-derivation helper must not come back under its old name in any family file.
    /// </summary>
    [Theory]
    [InlineData("src/Sharpy.Compiler/Project/GeneratorContextBuilder.cs")]
    [InlineData("src/Sharpy.Compiler/Semantic/Validation/DecoratorValidator.Caching.cs")]
    [InlineData("src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs")]
    [InlineData("src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.LruCache.cs")]
    public void LiteralValueFamilyFile_DoesNotNameGetLruCacheMaxSize(string repoRelativePath)
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), repoRelativePath));
        Assert.DoesNotContain("GetLruCacheMaxSize", source);
    }

    /// <summary>
    /// Every switch arm / case label in the file whose pattern names one of
    /// <see cref="LiteralAstKinds"/>, as "line: pattern names Kind[, Kind]". Handles both
    /// switch statements (pattern labels and, for parse-only <c>case Kind:</c>, constant
    /// labels) and switch expressions. Nested property patterns count
    /// (<c>UnaryOp { Operand: IntegerLiteral }</c> is still a literal-kind dispatch).
    /// <c>when</c> clauses are outside the pattern and are not scanned.
    /// </summary>
    private static List<string> LiteralKindSwitchArms(string repoRelativePath)
    {
        var fullPath = Path.Combine(FindRepoRoot(), repoRelativePath);
        Assert.True(File.Exists(fullPath), $"family file not found: {fullPath}");

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath)).GetCompilationUnitRoot();
        var hits = new List<string>();

        foreach (var switchStmt in root.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            foreach (var label in switchStmt.Sections.SelectMany(s => s.Labels))
            {
                SyntaxNode? patternNode = label switch
                {
                    CasePatternSwitchLabelSyntax patternLabel => patternLabel.Pattern,
                    CaseSwitchLabelSyntax constantLabel => constantLabel.Value,
                    _ => null,
                };
                if (patternNode != null)
                    AddIfNamesLiteralKind(patternNode, hits);
            }
        }

        foreach (var switchExpr in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            foreach (var arm in switchExpr.Arms)
                AddIfNamesLiteralKind(arm.Pattern, hits);
        }

        return hits;
    }

    private static void AddIfNamesLiteralKind(SyntaxNode patternNode, List<string> hits)
    {
        var named = patternNode.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Select(id => id.Identifier.Text)
            .Where(LiteralAstKinds.Contains)
            .Distinct()
            .ToList();
        if (named.Count == 0)
            return;

        var line = patternNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var patternText = string.Join(" ", patternNode.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        hits.Add($"{line}: {patternText} names {string.Join(", ", named)}");
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
