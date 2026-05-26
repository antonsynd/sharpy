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
}
