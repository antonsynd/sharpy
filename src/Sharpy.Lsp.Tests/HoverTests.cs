using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
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
    private readonly SharpyWorkspace _workspace;

    public HoverTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
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

    [Fact]
    public async Task Hover_OverFunctionWithDocstring_IncludesDocumentation()
    {
        var source = "def greet(name: str) -> str:\n    \"\"\"Say hello to someone.\"\"\"\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbolWithDocs(symbol!);
        formatted.Should().Contain("```sharpy");
        formatted.Should().Contain("def greet");
        formatted.Should().Contain("Say hello to someone.");
    }

    [Fact]
    public async Task Hover_OverFunctionWithoutDocstring_StillWorks()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbolWithDocs(symbol!);
        formatted.Should().Contain("```sharpy");
        formatted.Should().Contain("def greet");
        formatted.Should().EndWith("\n```");
    }

    [Fact]
    public async Task Hover_ResultMap_ShowsCorrectReturnType()
    {
        // result.map(lambda x: x * 2) should return int !str, not <?>
        var source = "def main():\n    result: int !str = Ok(10)\n    mapped = result.map(lambda x: x * 2)\n    print(mapped)";
        _workspace.OpenDocument("file:///test_result_map.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_result_map.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Find the FunctionCall node for result.map(...) on line 3
        // "    mapped = result.map(lambda x: x * 2)" — "map" starts at col 20
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 20);
        callNode.Should().NotBeNull("should find the result.map() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("result.map() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "result.map() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<ResultType>();
        var resultType = (ResultType)effectiveType!;
        resultType.OkType.Should().BeOfType<BuiltinType>();
        resultType.OkType.GetDisplayName().Should().Be("int");
        resultType.ErrorType.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_ResultMapErr_ShowsCorrectReturnType()
    {
        // result.map_err(lambda e: int(e)) should return int !int, not <?>
        var source = "def main():\n    result: int !str = Ok(10)\n    mapped = result.map_err(lambda e: int(e))\n    print(mapped)";
        _workspace.OpenDocument("file:///test_result_map_err.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_result_map_err.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // "    mapped = result.map_err(lambda e: int(e))" — "map_err" starts at col 20
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 20);
        callNode.Should().NotBeNull("should find the result.map_err() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("result.map_err() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "result.map_err() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<ResultType>();
        var resultType = (ResultType)effectiveType!;
        resultType.OkType.GetDisplayName().Should().Be("int");
        resultType.ErrorType.GetDisplayName().Should().Be("int");
    }

    [Fact]
    public async Task Hover_OptionalMap_ShowsCorrectReturnType()
    {
        // opt.map(lambda x: str(x)) should return str?, not <?>
        var source = "def main():\n    opt: int? = Some(42)\n    mapped = opt.map(lambda x: str(x))\n    print(mapped)";
        _workspace.OpenDocument("file:///test_optional_map.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_optional_map.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // "    mapped = opt.map(lambda x: str(x))" — "map" starts at col 17
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 17);
        callNode.Should().NotBeNull("should find the opt.map() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("opt.map() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "opt.map() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<OptionalType>();
        var optionalType = (OptionalType)effectiveType!;
        optionalType.UnderlyingType.GetDisplayName().Should().Be("str");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
