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

    /// <summary>
    /// Regression test for #597: Module-level non-const VariableDeclaration sets
    /// DeclarationSpan and DeclaringFilePath on the VariableSymbol.
    /// </summary>
    [Fact]
    public void ModuleLevelVariable_HasDeclarationSpanAndFilePath()
    {
        var source = "counter: int = 0\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("counter");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<VariableSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull("Phase 1 of #597 sets DeclarationSpan");
        symbol!.DeclaringFilePath.Should().NotBeNull("Phase 1 of #597 sets DeclaringFilePath");
    }

    /// <summary>
    /// Regression test for #597: Module-level const VariableDeclaration sets
    /// DeclarationSpan (set by NameResolver). DeclaringFilePath is null in
    /// single-file analysis (NameResolver._currentFilePath not set), but the
    /// RenameHandler falls back to the request URI in that case.
    /// </summary>
    [Fact]
    public void ConstVariable_HasDeclarationSpan()
    {
        var source = "const MAX: int = 100\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("MAX");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<VariableSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull();
    }

    /// <summary>
    /// Regression test for #597: Assignment variable in function body sets
    /// DeclarationSpan and DeclaringFilePath.
    /// </summary>
    [Fact]
    public void AssignmentVariable_HasDeclarationSpanAndFilePath()
    {
        var source = "def main():\n    x = 42\n    print(x)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SemanticInfo!.FindSymbolByDeclaration("x", 2, 5);
        symbol.Should().NotBeNull("TypeChecker creates VariableSymbol for assignment");
        symbol.Should().BeOfType<VariableSymbol>();
        symbol!.DeclarationSpan.Should().NotBeNull("Phase 1 of #597 sets DeclarationSpan");
        symbol!.DeclaringFilePath.Should().NotBeNull("Phase 1 of #597 sets DeclaringFilePath");
    }
}
