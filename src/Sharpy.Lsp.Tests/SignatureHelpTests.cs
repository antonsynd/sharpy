using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for signature help functionality used by SharplySignatureHelpHandler.
/// Tests function symbol resolution and parameter info.
/// </summary>
public class SignatureHelpTests
{
    private readonly CompilerApi _api = new();

    [Fact]
    public void FunctionSymbol_HasParameters()
    {
        var source = "def greet(name: str, count: int) -> str:\n    return name\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("greet") as FunctionSymbol;
        symbol.Should().NotBeNull();
        symbol!.Parameters.Should().HaveCount(2);
        symbol.Parameters[0].Name.Should().Be("name");
        symbol.Parameters[1].Name.Should().Be("count");
    }

    [Fact]
    public void FunctionSymbol_Parameters_HaveTypes()
    {
        var source = "def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("add") as FunctionSymbol;
        symbol.Should().NotBeNull();
        symbol!.Parameters[0].Type.Should().NotBeNull();
        symbol!.Parameters[1].Type.Should().NotBeNull();
    }

    [Fact]
    public void FunctionSymbol_HasReturnType()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.Lookup("greet") as FunctionSymbol;
        symbol.Should().NotBeNull();
        symbol!.ReturnType.Should().NotBeNull();
    }

    [Fact]
    public void FunctionCall_InAst_HasArguments()
    {
        var source = "def greet(name: str) -> str:\n    return name\ndef main():\n    greet(\"world\")";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        // Find function call in the AST
        var mainFunc = analysis.Ast!.Body.OfType<FunctionDef>().First(f => f.Name == "main");
        // The body should contain an ExpressionStatement with a FunctionCall
        var callFound = false;
        foreach (var stmt in mainFunc.Body)
        {
            if (stmt is ExpressionStatement es && es.Expression is FunctionCall call)
            {
                call.Arguments.Should().HaveCount(1);
                callFound = true;
            }
        }

        callFound.Should().BeTrue("Expected a function call in main body");
    }

    [Fact]
    public void GetCallTarget_ResolvesFunctionSymbol()
    {
        var source = "def greet(name: str) -> str:\n    return name\ndef main():\n    greet(\"world\")";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        // Find the FunctionCall node
        var mainFunc = analysis.Ast!.Body.OfType<FunctionDef>().First(f => f.Name == "main");
        FunctionCall? foundCall = null;
        foreach (var stmt in mainFunc.Body)
        {
            if (stmt is ExpressionStatement es && es.Expression is FunctionCall call)
            {
                foundCall = call;
            }
        }

        foundCall.Should().NotBeNull();

        var target = analysis.SemanticQuery!.GetCallTarget(foundCall!);
        target.Should().NotBeNull();
        target!.Name.Should().Be("greet");
    }

    [Fact]
    public void FindNodeAtPosition_FindsFunctionCall()
    {
        var source = "def greet(name: str) -> str:\n    return name\ndef main():\n    greet(\"world\")";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        // Line 4, somewhere in greet("world")
        var node = _api.FindNodeAtPosition(analysis.Ast!, 4, 5);
        node.Should().NotBeNull();
    }
}
