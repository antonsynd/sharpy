using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for document symbol functionality used by SharpyDocumentSymbolHandler.
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

    /// <summary>
    /// The enum-member outline entry's SelectionRange is the recorded NAME extent, not the
    /// whole <c>NAME = value</c> span, and it covers backticks on an escaped spelling (#1604).
    /// </summary>
    [Fact]
    public async Task EnumMember_SelectionRange_IsTheRecordedNameExtent()
    {
        using var workspace = new Sharpy.Lsp.SharpyWorkspace(
            _api, Microsoft.Extensions.Logging.Abstractions.NullLogger<Sharpy.Lsp.SharpyWorkspace>.Instance);
        using var service = new Sharpy.Lsp.LanguageService(
            workspace, _api, Microsoft.Extensions.Logging.Abstractions.NullLogger<Sharpy.Lsp.LanguageService>.Instance);
        var handler = new Sharpy.Lsp.Handlers.SharpyDocumentSymbolHandler(service);

        // L1 (0-based): "    `class` = 1"  name at chars 4-11, member span 4-15
        // L2 (0-based): "    RED = 2"      name at chars 4-7
        workspace.OpenDocument("file:///test.spy", "enum E:\n    `class` = 1\n    RED = 2\n", 1);
        var result = await handler.Handle(
            new OmniSharp.Extensions.LanguageServer.Protocol.Models.DocumentSymbolParams
            {
                TextDocument = new OmniSharp.Extensions.LanguageServer.Protocol.Models.TextDocumentIdentifier("file:///test.spy"),
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        var enumSymbol = result!
            .Select(s => s.DocumentSymbol)
            .First(s => s != null && s.Kind == OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Enum)!;
        var members = enumSymbol.Children!.ToList();

        var escaped = members[0];
        escaped.SelectionRange.Start.Character.Should().Be(4);
        escaped.SelectionRange.End.Character.Should().Be(4 + "`class`".Length,
            "the selection is the recorded name extent, backticks included (#1604)");
        escaped.Range.End.Character.Should().BeGreaterThan(escaped.SelectionRange.End.Character,
            "the member Range still spans the `= value` tail");

        var bare = members[1];
        bare.SelectionRange.Start.Character.Should().Be(4);
        bare.SelectionRange.End.Character.Should().Be(4 + "RED".Length);
    }
}
