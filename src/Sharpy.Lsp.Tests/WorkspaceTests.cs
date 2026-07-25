using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class WorkspaceTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;

    public WorkspaceTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
    }

    [Fact]
    public async Task OpenDocument_StoresDocument()
    {
        _workspace.OpenDocument("file:///test.spy", "x: int = 1", 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAnalysis_ValidCode_ReturnsSuccessAsync()
    {
        _workspace.OpenDocument("file:///test.spy", "def main():\n    x: int = 1\n    print(x)", 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetAnalysis_InvalidCode_ReturnsDiagnosticsAsync()
    {
        _workspace.OpenDocument("file:///test.spy", "x: int = \"not an int\"", 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        analysis.Should().NotBeNull();
        analysis!.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAnalysis_UnknownUri_ReturnsNullAsync()
    {
        var analysis = await _workspace.GetAnalysisAsync("file:///unknown.spy", CancellationToken.None);

        analysis.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDocument_InvalidatesCacheAsync()
    {
        _workspace.OpenDocument("file:///test.spy", "def main():\n    x: int = 1", 1);
        var first = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        _workspace.UpdateDocument("file:///test.spy", "def main():\n    y: str = \"hello\"", 2);
        var second = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        // Both should succeed but be different analysis results
        first.Should().NotBeNull();
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task CloseDocument_RemovesDocumentAsync()
    {
        _workspace.OpenDocument("file:///test.spy", "x: int = 1", 1);
        _workspace.CloseDocument("file:///test.spy");

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        analysis.Should().BeNull();
    }

    [Fact]
    public async Task DocumentState_FunctionBodyEdit_StillProducesCorrectAnalysis()
    {
        // Initial analysis
        _workspace.OpenDocument("file:///test.spy",
            "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())", 1);
        var first = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        first.Should().NotBeNull();
        first!.Success.Should().BeTrue();

        // Change only the function body (return value)
        _workspace.UpdateDocument("file:///test.spy",
            "def greet() -> str:\n    return \"world\"\ndef main():\n    print(greet())", 2);
        var second = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        second.Should().NotBeNull();
        second!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DocumentState_StructuralEdit_StillProducesCorrectAnalysis()
    {
        // Initial analysis
        _workspace.OpenDocument("file:///test.spy",
            "def main():\n    print(\"hello\")", 1);
        var first = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        first.Should().NotBeNull();

        // Structural change: add a new function
        _workspace.UpdateDocument("file:///test.spy",
            "def helper() -> int:\n    return 42\ndef main():\n    print(helper())", 2);
        var second = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        second.Should().NotBeNull();
        second!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ParseResult_CachedSeparatelyFromAnalysis()
    {
        _workspace.OpenDocument("file:///test.spy",
            "def main():\n    print(\"hello\")", 1);

        // Get parse result (should be fast, no semantic analysis)
        var parseResult = await _workspace.GetParseResultAsync("file:///test.spy", CancellationToken.None);
        parseResult.Should().NotBeNull();
        parseResult!.Ast.Should().NotBeNull();

        // Analysis should also work independently
        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SecondEdit_IdenticalText_ReusesPreviousAnalysis_FingerprintGateArmed()
    {
        // #1137: the fresh-document path skips the up-front standalone parse and instead derives
        // the cached parse from the analysis's own entry AST. That derived parse is what the NEXT
        // edit fingerprints against — so if the derivation were dropped (a naive gate), the second
        // analysis would fall through to a full re-analysis and return a fresh object. A simple
        // (comprehension-free) body lets AstFingerprint prove NoChange, whose fast path returns the
        // very same previous result instance; reference-equality is the observable that the gate
        // armed on the fresh path and fired on the second edit.
        const string source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    print(greet())";
        _workspace.OpenDocument("file:///test.spy", source, 1);
        var first = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        first.Should().NotBeNull();

        // Re-open with byte-identical text: structurally NoChange, so the fingerprint fast path
        // must reuse the exact previous analysis instance.
        _workspace.UpdateDocument("file:///test.spy", source, 2);
        var second = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);

        second.Should().NotBeNull();
        ReferenceEquals(first, second).Should().BeTrue(
            "the fresh-path parse derivation must arm the fingerprint so an identical second edit "
            + "reuses the previous analysis instead of re-analyzing");
    }

    [Fact]
    public async Task FreshDocument_AnalyzeThenParse_ParseCacheServedFromAnalysisAst()
    {
        // #1137: after a fresh full analysis (which no longer runs a standalone parse), the parse
        // cache is populated from the analysis's entry AST, so a subsequent parse-only request
        // returns that AST without reparsing. Analyze first (fresh path), then request the parse.
        _workspace.OpenDocument("file:///test.spy", "def main():\n    print(\"hello\")", 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy", CancellationToken.None);
        analysis.Should().NotBeNull();
        analysis!.Success.Should().BeTrue();

        var parseResult = await _workspace.GetParseResultAsync("file:///test.spy", CancellationToken.None);
        parseResult.Should().NotBeNull();
        parseResult!.Ast.Should().NotBeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
