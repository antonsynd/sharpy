using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

public class AstNormalizerTests
{
    private static Sharpy.Compiler.Parser.Ast.Module Parse(string source)
    {
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void NormalizeModule_ZerosPositions()
    {
        var source = "x = 1\ny = 2\n";
        var module = Parse(source);

        module.Body[0].LineStart.Should().BeGreaterThan(0);

        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        normalized.LineStart.Should().Be(0);
        normalized.ColumnStart.Should().Be(0);
        normalized.Body[0].LineStart.Should().Be(0);
        normalized.Body[0].ColumnStart.Should().Be(0);
    }

    [Fact]
    public void NormalizeModule_PreservesStructure()
    {
        var source = "def foo():\n    return 42\n";
        var module = Parse(source);
        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        normalized.Body.Should().HaveCount(module.Body.Length);
    }

    [Fact]
    public void NormalizeModule_EmptyModule()
    {
        var module = Parse("");
        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        normalized.Body.Should().BeEmpty();
        normalized.LineStart.Should().Be(0);
    }

    [Fact]
    public void NormalizeModule_NestedClass_AllPositionsZeroed()
    {
        var source = "class Foo:\n    def bar(self):\n        pass\n";
        var module = Parse(source);
        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        normalized.LineStart.Should().Be(0);
        normalized.Body[0].LineStart.Should().Be(0);
    }

    [Fact]
    public void NormalizeModule_ZerosHeaderEndPositions()
    {
        // P22b Decision 5: the header-end fields are positions; a parsed if/else and try/finally
        // carry non-zero values (positive control) that normalization must erase.
        var module = Parse("if a:\n    pass\nelse:\n    pass\ntry:\n    pass\nexcept E:\n    pass\nfinally:\n    pass\n");
        var parsedIf = (Sharpy.Compiler.Parser.Ast.IfStatement)module.Body[0];
        var parsedTry = (Sharpy.Compiler.Parser.Ast.TryStatement)module.Body[1];
        parsedIf.HeaderLineEnd.Should().Be(1);
        parsedIf.ElseHeaderLine.Should().Be(3);
        parsedTry.FinallyHeaderLine.Should().Be(9);
        parsedTry.Handlers[0].HeaderLineEnd.Should().Be(7);

        var normalized = AstNormalizer.Instance.NormalizeModule(module);
        var ifStmt = (Sharpy.Compiler.Parser.Ast.IfStatement)normalized.Body[0];
        var tryStmt = (Sharpy.Compiler.Parser.Ast.TryStatement)normalized.Body[1];
        (ifStmt.HeaderLineEnd, ifStmt.HeaderEndOffset, ifStmt.ElseHeaderLine).Should().Be((0, 0, 0));
        (tryStmt.HeaderLineEnd, tryStmt.ElseHeaderLine, tryStmt.FinallyHeaderLine).Should().Be((0, 0, 0));
        (tryStmt.Handlers[0].HeaderLineEnd, tryStmt.Handlers[0].HeaderEndOffset).Should().Be((0, 0));
        ifStmt.ThenBody[0].HeaderLineEnd.Should().Be(0, "nested statements are zeroed too");
    }

    [Fact]
    public void NormalizeModule_CarriesBacktickEscapeFlags()
    {
        // #2157: the escape flags are structural syntax facts (like Identifier.IsNameBacktickEscaped),
        // so normalization — which zeroes positions — must carry them. The union arm rebuilds its
        // cases with `new`, so it is the one site that has to copy them by hand; the others use `with`.
        var source = "import a.`b` as `m`\n"
            + "from `p` import `x` as `y`\n"
            + "f(`class`=7)\n"
            + "union Shape:\n    case `K`(`class`: int)\n"
            + "match e:\n    case E.`A`:\n        pass\n    case P(`class`=v):\n        pass\n";
        var module = Parse(source);
        var normalized = AstNormalizer.Instance.NormalizeModule(module);

        var import = (Sharpy.Compiler.Parser.Ast.ImportStatement)normalized.Body[0];
        import.Names[0].BacktickEscapedParts.Should().Equal(false, true);
        import.Names[0].IsAsNameBacktickEscaped.Should().BeTrue();

        var from = (Sharpy.Compiler.Parser.Ast.FromImportStatement)normalized.Body[1];
        from.BacktickEscapedParts.Should().Equal(true);
        from.Names[0].BacktickEscapedParts.Should().Equal(true);
        from.Names[0].IsAsNameBacktickEscaped.Should().BeTrue();

        var call = (Sharpy.Compiler.Parser.Ast.FunctionCall)((Sharpy.Compiler.Parser.Ast.ExpressionStatement)normalized.Body[2]).Expression;
        call.KeywordArguments[0].IsNameBacktickEscaped.Should().BeTrue();

        var union = (Sharpy.Compiler.Parser.Ast.UnionDef)normalized.Body[3];
        union.Cases[0].IsNameBacktickEscaped.Should().BeTrue();
        union.Cases[0].Fields[0].IsNameBacktickEscaped.Should().BeTrue();

        var match = (Sharpy.Compiler.Parser.Ast.MatchStatement)normalized.Body[4];
        ((Sharpy.Compiler.Parser.Ast.MemberAccessPattern)match.Cases[0].Pattern).BacktickEscapedParts.Should().Equal(false, true);
        ((Sharpy.Compiler.Parser.Ast.PropertyPattern)match.Cases[1].Pattern).Fields[0].IsNameBacktickEscaped.Should().BeTrue();
    }
}
