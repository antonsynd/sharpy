using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests call hierarchy functionality including handler-level integration tests.
/// </summary>
public class CallHierarchyTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharplyCallHierarchyPrepareHandler _prepareHandler;
    private readonly SharplyCallHierarchyIncomingHandler _incomingHandler;
    private readonly SharplyCallHierarchyOutgoingHandler _outgoingHandler;

    public CallHierarchyTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _prepareHandler = new SharplyCallHierarchyPrepareHandler(_languageService, _api);
        _incomingHandler = new SharplyCallHierarchyIncomingHandler(_workspace, _languageService, _api);
        _outgoingHandler = new SharplyCallHierarchyOutgoingHandler(_languageService, _api);
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

    [Fact]
    public async Task PrepareHandler_OnFunctionCall_ReturnsItemAsync()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    greet()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Cursor on `greet()` call (line 4, col 5 → 0-based: line 3, col 4)
        var result = await _prepareHandler.Handle(
            new CallHierarchyPrepareParams
            {
                TextDocument = new TextDocumentIdentifier("file:///test.spy"),
                Position = new Position(3, 4)
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.First().Name.Should().Be("greet");
    }

    [Fact]
    public async Task PrepareHandler_OnMethodDef_ReturnsItemAsync()
    {
        var source = @"
class Calc:
    def add(self, a: int, b: int) -> int:
        return a + b
    def __init__(self):
        pass
def main():
    c = Calc()
    c.add(1, 2)
";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Cursor on `add` method definition (line 3, col 9 → 0-based: line 2, col 8)
        var result = await _prepareHandler.Handle(
            new CallHierarchyPrepareParams
            {
                TextDocument = new TextDocumentIdentifier("file:///test.spy"),
                Position = new Position(2, 8)
            },
            CancellationToken.None);

        // add is on self.add which resolves to an identifier
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task IncomingHandler_FindsCallersAsync()
    {
        var source = "def helper() -> int:\n    return 1\ndef foo() -> int:\n    return helper()\ndef main():\n    helper()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        // Prepare an item for helper
        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var symbol = analysis!.SymbolTable?.Lookup("helper") as FunctionSymbol;
        var preparedItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        preparedItem.Should().NotBeNull();

        var result = await _incomingHandler.Handle(
            new CallHierarchyIncomingCallsParams { Item = preparedItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().HaveCountGreaterThanOrEqualTo(2,
            "helper is called from both foo and main");
    }

    [Fact]
    public async Task IncomingHandler_NoCallers_ReturnsEmptyAsync()
    {
        var source = "def unused() -> int:\n    return 1\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var symbol = analysis!.SymbolTable?.Lookup("unused") as FunctionSymbol;
        var preparedItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        preparedItem.Should().NotBeNull();

        var result = await _incomingHandler.Handle(
            new CallHierarchyIncomingCallsParams { Item = preparedItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OutgoingHandler_FindsCallsAsync()
    {
        var source = "def a() -> int:\n    return 1\ndef b() -> int:\n    return 2\ndef caller() -> int:\n    return a() + b()\ndef main():\n    caller()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var symbol = analysis!.SymbolTable?.Lookup("caller") as FunctionSymbol;
        var preparedItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        preparedItem.Should().NotBeNull();

        var result = await _outgoingHandler.Handle(
            new CallHierarchyOutgoingCallsParams { Item = preparedItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().HaveCountGreaterThanOrEqualTo(2,
            "caller calls both a and b");
    }

    [Fact]
    public async Task OutgoingHandler_NoCalls_ReturnsEmptyAsync()
    {
        var source = "def empty() -> int:\n    return 42\ndef main():\n    empty()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var symbol = analysis!.SymbolTable?.Lookup("empty") as FunctionSymbol;
        var preparedItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        preparedItem.Should().NotBeNull();

        var result = await _outgoingHandler.Handle(
            new CallHierarchyOutgoingCallsParams { Item = preparedItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OutgoingHandler_DeduplicatesMultipleCallsToSameTargetAsync()
    {
        var source = "def helper() -> int:\n    return 1\ndef caller() -> int:\n    return helper() + helper()\ndef main():\n    caller()";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        var symbol = analysis!.SymbolTable?.Lookup("caller") as FunctionSymbol;
        var preparedItem = SharplyCallHierarchyPrepareHandler.CreateCallHierarchyItem(symbol!, "file:///test.spy");
        preparedItem.Should().NotBeNull();

        var result = await _outgoingHandler.Handle(
            new CallHierarchyOutgoingCallsParams { Item = preparedItem! },
            CancellationToken.None);

        result.Should().NotBeNull();
        // helper() called twice → should appear once with 2 fromRanges
        result!.Should().HaveCount(1, "same callee should be deduplicated");
        result!.First().FromRanges.Should().HaveCount(2, "two call sites for helper()");
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
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
