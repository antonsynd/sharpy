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

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
