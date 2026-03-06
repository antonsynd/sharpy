using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for document symbol functionality used by SharplyDocumentSymbolHandler.
/// Tests AST structure for symbol outline generation.
/// </summary>
public class DocumentSymbolTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void Parse_ModuleLevelFunction_InAstBody()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();
        analysis.Ast.Should().NotBeNull();

        var functions = analysis.Ast!.Body.OfType<FunctionDef>().ToList();
        functions.Should().HaveCount(2);
        functions.Should().Contain(f => f.Name == "greet");
        functions.Should().Contain(f => f.Name == "main");
    }

    [Fact]
    public void Parse_ClassWithMethods_InAstBody()
    {
        var source = @"
class Animal:
    name: str
    def __init__(self, name: str):
        self.name = name
    def speak(self) -> str:
        return self.name

def main():
    pass
";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var classes = analysis.Ast!.Body.OfType<ClassDef>().ToList();
        classes.Should().HaveCount(1);
        classes[0].Name.Should().Be("Animal");

        var methods = classes[0].Body.OfType<FunctionDef>().ToList();
        methods.Should().HaveCountGreaterThanOrEqualTo(2);
        methods.Should().Contain(m => m.Name == "__init__");
        methods.Should().Contain(m => m.Name == "speak");
    }

    [Fact]
    public void Parse_VariableDeclaration_InAstBody()
    {
        var source = "x: int = 42\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var vars = analysis.Ast!.Body.OfType<VariableDeclaration>().ToList();
        vars.Should().HaveCountGreaterThanOrEqualTo(1);
        vars.Should().Contain(v => v.Name == "x");
    }

    [Fact]
    public void Parse_NodesHaveLinePositions()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var functions = analysis.Ast!.Body.OfType<FunctionDef>().ToList();
        foreach (var f in functions)
        {
            f.LineStart.Should().BeGreaterThan(0, $"Function {f.Name} should have a line start");
        }
    }

    [Fact]
    public void Parse_Enum_HasMembers()
    {
        var source = "enum Color:\n    RED = 0\n    GREEN = 1\n    BLUE = 2\ndef main():\n    c: Color = Color.RED\n    print(c)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var enums = analysis.Ast!.Body.OfType<EnumDef>().ToList();
        enums.Should().HaveCount(1);
        enums[0].Name.Should().Be("Color");
        enums[0].Members.Should().HaveCount(3);
    }
}
