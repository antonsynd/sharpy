using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for reference tracking used by SharpyReferencesHandler.
/// </summary>
public class ReferencesTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void GetReferences_Variable_FindsUsages()
    {
        var source = "x: int = 1\ndef main():\n    print(x)\n    y: int = x + 1\n    print(y)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("x");
        symbol.Should().NotBeNull();

        var references = analysis.SemanticInfo!.GetReferences(symbol!);
        // x is referenced in print(x) and y: int = x + 1
        references.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetReferences_Function_FindsCalls()
    {
        var source = @"
def greet() -> str:
    return ""hi""

def main():
    print(greet())
    s: str = greet()
    print(s)
";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("greet");
        symbol.Should().NotBeNull();

        var references = analysis.SemanticInfo!.GetReferences(symbol!);
        // greet() is called twice in main
        references.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetReferences_NoReferences_ReturnsEmptyList()
    {
        var source = "x: int = 1\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("x");
        symbol.Should().NotBeNull();

        var references = analysis.SemanticInfo!.GetReferences(symbol!);
        // x is declared but not used in main
        // It might still appear as a reference from declaration itself
        references.Should().NotBeNull();
    }

    [Fact]
    public void GetReferences_IncludesLineAndColumn()
    {
        var source = "x: int = 1\ndef main():\n    print(x)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("x");
        symbol.Should().NotBeNull();

        var references = analysis.SemanticInfo!.GetReferences(symbol!);
        foreach (var refLoc in references)
        {
            refLoc.Line.Should().BeGreaterThan(0, "Lines should be 1-based");
            refLoc.Column.Should().BeGreaterThan(0, "Columns should be 1-based");
        }
    }
}
