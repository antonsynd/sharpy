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

    // #1001: tuple subscripts must unparse without the tuple's wrapping parens so
    // the round-trip is string-stable (parser maps x[a, b], x[a,], x[(a, b)] to the
    // same IndexAccess(TupleLiteral)).
    [Fact]
    public void Roundtrip_TupleSubscript_TwoElements()
    {
        Unparser.Unparse(Parse("x[y, s]\n")).Should().Be("x[y, s]\n");
    }

    [Fact]
    public void Roundtrip_TupleSubscript_OneElement_PreservesTrailingComma()
    {
        // x[y,] is a 1-tuple subscript, distinct from x[y]; the comma must survive.
        Unparser.Unparse(Parse("x[y,]\n")).Should().Be("x[y,]\n");
    }

    [Fact]
    public void Roundtrip_ParenthesizedTupleSubscript_NormalizesToBareTuple()
    {
        Unparser.Unparse(Parse("x[(y, s)]\n")).Should().Be("x[y, s]\n");
    }

    [Fact]
    public void Roundtrip_GenericTupleSubscript()
    {
        Unparser.Unparse(Parse("dict[str, int]\n")).Should().Be("dict[str, int]\n");
    }

    [Fact]
    public void Roundtrip_PlainSubscript_Unchanged()
    {
        Unparser.Unparse(Parse("x[0]\n")).Should().Be("x[0]\n");
    }

    [Fact]
    public void Roundtrip_TupleSubscript_StableInsideComplexExpression()
    {
        // Minimized counterexample from the seed-flaky UnparseIdempotence property
        // test: x[y, s] previously re-emitted as x[(y, s)] after reparse. Assert the
        // string-level unparse is idempotent.
        var source = "assert {} <= (idx := \"world\") >= x[y, s] > (super() is int)\n";
        var once = Unparser.Unparse(Parse(source));
        var twice = Unparser.Unparse(Parse(once));
        twice.Should().Be(once);
        once.Should().Contain("x[y, s]");
        once.Should().NotContain("x[(y, s)]");
    }
}
