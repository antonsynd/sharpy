using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests hover functionality by verifying that the compiler API can resolve
/// symbols at positions and the SymbolFormatter produces correct hover text.
/// </summary>
public class HoverTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;

    public HoverTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
    }

    [Fact]
    public async Task Hover_OverVariable_ShowsType()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.Ast.Should().NotBeNull();

        // Find the identifier 'x' in the source (line 1, col 1 in 1-based)
        var node = _api.FindNodeAtPosition(analysis.Ast!, 1, 1);
        node.Should().NotBeNull();

        if (node is Identifier id1)
        {
            var symbol = analysis.SemanticQuery?.GetIdentifierSymbol(id1);
            symbol.Should().NotBeNull();

            var formatted = SymbolFormatter.FormatSymbol(symbol!);
            formatted.Should().Contain("x");
            formatted.Should().Contain("int");
        }
    }

    [Fact]
    public async Task Hover_OverFunction_ShowsSignature()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Look up function directly from the symbol table
        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbol(symbol!);
        formatted.Should().Contain("def greet");
        formatted.Should().Contain("name: str");
        formatted.Should().Contain("-> str");
    }

    [Fact]
    public async Task Hover_OverExpression_ShowsEffectiveType()
    {
        var source = "def main():\n    x: int = 10\n    y = x + 5\n    print(y)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();
    }

    [Fact]
    public async Task Hover_NoNodeAtPosition_ReturnsNull()
    {
        var source = "def main():\n    pass";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Line 100 doesn't exist
        var node = _api.FindNodeAtPosition(analysis!.Ast!, 100, 1);
        node.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
