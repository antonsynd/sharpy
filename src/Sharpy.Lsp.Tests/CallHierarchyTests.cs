using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests call hierarchy functionality by verifying that the compiler API
/// can resolve call relationships between functions.
/// </summary>
public class CallHierarchyTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;

    public CallHierarchyTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
    }

    [Fact]
    public async Task Prepare_Function_ReturnsCallHierarchyItem()
    {
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("foo") as FunctionSymbol;
        symbol.Should().NotBeNull();

        var item = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        item.Should().NotBeNull();
        item!.Name.Should().Be("foo");
        item.Kind.Should().Be(OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind.Function);
    }

    [Fact]
    public async Task Prepare_BuiltinFunction_ReturnsNull()
    {
        var source = "def main():\n    print(42)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("print") as FunctionSymbol;
        symbol.Should().NotBeNull();

        // Builtins have no declaration location, so CreateCallHierarchyItem returns null
        var item = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        item.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_NonFunctionSymbol_NotResolved()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Variables should not produce call hierarchy items
        var symbol = analysis!.SymbolTable?.Lookup("x");
        symbol.Should().NotBeNull();
        symbol.Should().BeOfType<VariableSymbol>();
    }

    [Fact]
    public async Task OutgoingCalls_FindsCallsInFunctionBody()
    {
        var source = "def helper() -> int:\n    return 1\ndef foo() -> int:\n    return helper()\ndef main():\n    foo()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.Ast.Should().NotBeNull();

        // Find the function call nodes in foo's body
        var fooDef = analysis.Ast!.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "foo");
        fooDef.Should().NotBeNull();

        // Walk foo's body for FunctionCall nodes
        var calls = new List<FunctionCall>();
        CollectCalls(fooDef!, calls);
        calls.Should().NotBeEmpty();

        // Verify we can resolve the call target
        var callTarget = analysis.SemanticQuery?.GetCallTarget(calls[0]);
        callTarget.Should().NotBeNull();
        callTarget!.Name.Should().Be("helper");
    }

    [Fact]
    public async Task IncomingCalls_FindsReferencesToFunction()
    {
        var source = "def helper() -> int:\n    return 1\ndef foo() -> int:\n    return helper()\ndef main():\n    helper()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Get references to helper
        var helperSymbol = analysis!.SymbolTable?.Lookup("helper");
        helperSymbol.Should().NotBeNull();

        var references = analysis.SemanticQuery?.GetReferences(helperSymbol!);
        references.Should().NotBeNull();
        references!.Count.Should().BeGreaterThanOrEqualTo(2, "helper is called from both foo and main");
    }

    [Fact]
    public async Task Prepare_DataContainsSymbolIdentity()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    greet()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("greet") as FunctionSymbol;
        symbol.Should().NotBeNull();

        var item = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        item.Should().NotBeNull();

        // Data should carry identity for incoming/outgoing resolution
        var data = item!.Data as Newtonsoft.Json.Linq.JObject;
        data.Should().NotBeNull();
        data!["name"]!.ToString().Should().Be("greet");
    }

    private static void CollectCalls(Node node, List<FunctionCall> calls)
    {
        if (node is FunctionCall call)
            calls.Add(call);

        foreach (var child in node.GetChildNodes())
            CollectCalls(child, calls);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
