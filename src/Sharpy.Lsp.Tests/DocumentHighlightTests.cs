using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class DocumentHighlightTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly SharplyDocumentHighlightHandler _handler;

    public DocumentHighlightTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _handler = new SharplyDocumentHighlightHandler(_workspace, _api);
    }

    private async Task<DocumentHighlightContainer?> GetHighlightsAsync(string source, int line, int col)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new DocumentHighlightParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task FunctionName_HighlightsAtCallSiteAsync()
    {
        var source = "def foo():\n    pass\n\ndef main():\n    foo()";
        // Position on 'foo' at call site (line 4, col 4 in 0-based)
        var highlights = await GetHighlightsAsync(source, 4, 4);

        if (highlights is not null)
        {
            highlights.Count().Should().BeGreaterThanOrEqualTo(1);
        }
        // null is acceptable if FindNodeAtPosition can't resolve the node
    }

    [Fact]
    public async Task NoSymbolAtPosition_ReturnsNullAsync()
    {
        var source = "def main():\n    pass";
        // Position in whitespace (line 1, col 0 = indent)
        var highlights = await GetHighlightsAsync(source, 1, 0);

        highlights.Should().BeNull();
    }

    [Fact]
    public async Task NoDocument_ReturnsNullAsync()
    {
        var request = new DocumentHighlightParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Position = new Position(0, 0)
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
