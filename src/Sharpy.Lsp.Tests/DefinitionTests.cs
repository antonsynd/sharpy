using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests go-to-definition functionality by verifying that the compiler API
/// can resolve symbols and their declaration locations.
/// </summary>
public class DefinitionTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;

    public DefinitionTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
    }

    [Fact]
    public async Task Definition_Function_HasDeclarationSpan()
    {
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Functions are always in the symbol table
        var symbol = analysis!.SymbolTable?.Lookup("foo");
        symbol.Should().NotBeNull();
        symbol!.DeclarationSpan.Should().NotBeNull("function 'foo' should have a declaration span");
    }

    [Fact]
    public async Task Definition_Function_HasDeclarationLocation()
    {
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("foo");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<FunctionSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull();
    }

    [Fact]
    public async Task Definition_Class_HasDeclarationLocation()
    {
        var source = "class Animal:\n    def __init__(self):\n        pass\ndef main():\n    a = Animal()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("Animal");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<TypeSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull();
    }

    [Fact]
    public async Task Definition_BuiltinSymbol_NoDeclarationSpan()
    {
        var source = "def main():\n    print(42)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("print");
        symbol.Should().NotBeNull();
        // Builtins have no file path
        symbol!.DeclaringFilePath.Should().BeNull();
    }

    [Fact]
    public void PositionConverter_RoundTrip()
    {
        // Verify 0-based LSP → 1-based compiler → 0-based LSP round-trip
        var (compLine, compCol) = PositionConverter.ToCompiler(
            new OmniSharp.Extensions.LanguageServer.Protocol.Models.Position(5, 10));
        compLine.Should().Be(6);
        compCol.Should().Be(11);

        var lspPos = PositionConverter.ToLsp(compLine, compCol);
        lspPos.Line.Should().Be(5);
        lspPos.Character.Should().Be(10);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
