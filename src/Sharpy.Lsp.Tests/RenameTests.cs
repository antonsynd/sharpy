using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for rename validation logic used by SharpyRenameHandler.
/// </summary>
public class RenameTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void SymbolDeclaration_HasLineAndColumn()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("x");
        symbol.Should().NotBeNull();
        symbol!.DeclarationLine.Should().NotBeNull();
        symbol!.DeclarationColumn.Should().NotBeNull();
    }

    [Fact]
    public void SymbolReferences_HaveLocations_ForRenameEdits()
    {
        var source = "x: int = 1\ndef main():\n    print(x)\n    y: int = x";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("x");
        symbol.Should().NotBeNull();

        var references = analysis.SemanticInfo!.GetReferences(symbol!);
        references.Should().NotBeNull();

        // Each reference should have valid location info
        foreach (var refLoc in references)
        {
            refLoc.Line.Should().BeGreaterThan(0);
            refLoc.Column.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void FunctionSymbol_CanBeLocated_ForRename()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    print(greet(\"world\"))";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("greet");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<FunctionSymbol>();
        symbol!.DeclarationLine.Should().NotBeNull();
        symbol!.Name.Should().Be("greet");
    }
}
