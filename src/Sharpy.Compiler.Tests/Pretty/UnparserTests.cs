using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

public class UnparserTests
{
    private static Sharpy.Compiler.Parser.Ast.Module Parse(string source)
    {
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void Roundtrip_SimpleAssignment()
    {
        var source = "x = 1\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_FunctionDefinition()
    {
        var source = "def foo():\n    pass\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_ClassDefinition()
    {
        var source = "class Foo:\n    def __init__(self):\n        pass\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_IfElse()
    {
        var source = "if x:\n    y = 1\nelse:\n    y = 2\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_ForLoop()
    {
        var source = "for i in range(10):\n    print(i)\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_WhileLoop()
    {
        var source = "while True:\n    break\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_ImportStatement()
    {
        var source = "import os\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_FromImport()
    {
        var source = "from os import path\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }

    [Fact]
    public void Roundtrip_EmptyModule()
    {
        var source = "";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Roundtrip_MultipleStatements()
    {
        var source = "x = 1\ny = 2\nz = x + y\n";
        var module = Parse(source);
        var result = Unparser.Unparse(module);

        result.Should().Be(source);
    }
}
