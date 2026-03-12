using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class FoldingRangeTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharplyFoldingRangeHandler _handler;

    public FoldingRangeTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharplyFoldingRangeHandler(_languageService);
    }

    private async Task<Container<FoldingRange>?> GetFoldingRangesAsync(string source)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier(uri)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task FunctionDef_ProducesFoldingRangeAsync()
    {
        var source = "def foo():\n    x: int = 1\n    return x";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges.Should().ContainSingle();
        var range = ranges!.Single();
        range.StartLine.Should().Be(0); // 0-based
        range.EndLine.Should().Be(2);
        range.Kind.Should().Be(FoldingRangeKind.Region);
    }

    [Fact]
    public async Task ClassDef_ProducesFoldingRangeAsync()
    {
        var source = "class Foo:\n    x: int = 1\n    def bar(self):\n        pass";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Class + method inside
        ranges!.Count().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task IfStatement_ProducesFoldingRangeAsync()
    {
        var source = "def foo():\n    if True:\n        x: int = 1\n        y: int = 2";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Function + if statement
        ranges!.Count().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SingleLineStatements_NoFoldingRangesAsync()
    {
        var source = "x: int = 1";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        ranges.Should().BeEmpty();
    }

    [Fact]
    public async Task NestedBlocks_ProduceMultipleRangesAsync()
    {
        var source = "class Outer:\n    class Inner:\n        def method(self):\n            pass";
        var ranges = await GetFoldingRangesAsync(source);

        ranges.Should().NotBeNull();
        // Outer class + Inner class + method
        ranges!.Count().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task NullAst_ReturnsNullAsync()
    {
        // Non-existent document
        var request = new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy")
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
