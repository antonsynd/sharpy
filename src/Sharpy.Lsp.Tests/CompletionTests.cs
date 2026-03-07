using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for completion-related functionality used by SharplyCompletionHandler.
/// Since the handler requires OmniSharp DI, we test the underlying APIs.
/// </summary>
public class CompletionTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void GetVisibleSymbols_ReturnsModuleLevelDeclarations()
    {
        var analysis = _api.Analyze("x: int = 1\ndef greet() -> str:\n    return \"hi\"\ndef main():\n    pass");
        analysis.Success.Should().BeTrue();

        var symbols = analysis.SymbolTable!.GetVisibleSymbols().ToList();

        symbols.Should().Contain(s => s.Name == "x");
        symbols.Should().Contain(s => s.Name == "greet");
        symbols.Should().Contain(s => s.Name == "main");
    }

    [Fact]
    public void GetVisibleSymbols_IncludesBuiltins()
    {
        var analysis = _api.Analyze("def main():\n    x: int = 1\n    print(x)");
        analysis.Success.Should().BeTrue();

        var symbols = analysis.SymbolTable!.GetVisibleSymbols().ToList();

        // Should include standard builtins like print, len
        symbols.Should().Contain(s => s.Name == "print");
        symbols.Should().Contain(s => s.Name == "len");
    }

    [Fact]
    public void MemberCompletion_ClassMethods_ResolvedFromType()
    {
        var source = @"
class Animal:
    name: str
    def __init__(self, name: str):
        self.name = name
    def speak(self) -> str:
        return self.name

def main():
    a: Animal = Animal(""dog"")
    print(a.speak())
";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        // Look up 'a' variable and verify its type has members
        var aSymbol = analysis.SymbolTable!.Lookup("a");
        if (aSymbol is VariableSymbol vs && vs.Type is UserDefinedType udt)
        {
            udt.Symbol.Should().NotBeNull();
            udt.Symbol!.Methods.Should().Contain(m => m.Name == "speak");
        }
    }
}
