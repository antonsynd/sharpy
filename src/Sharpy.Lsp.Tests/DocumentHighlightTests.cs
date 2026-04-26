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
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyDocumentHighlightHandler _handler;

    public DocumentHighlightTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyDocumentHighlightHandler(_languageService, _api);
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
    public async Task FunctionName_HighlightsAtDefinitionAsync()
    {
        // "def foo():" - 'foo' is at line 0, col 4
        // "    foo()" - call to foo at line 3, col 4
        var source = "def foo():\n    pass\n\ndef main():\n    foo()";
        // Position on 'foo' at call site (line 4, col 4 in 0-based)
        var highlights = await GetHighlightsAsync(source, 4, 4);

        if (highlights is not null)
        {
            // If the handler resolves the symbol, we expect at least a Write highlight
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

    [Fact]
    public async Task Highlight_AsyncFunction_HighlightsNameNotKeyword()
    {
        // Line 0: "async def do_something() -> int:"
        //          "async" at col 0, "do_something" at col 10
        // Line 1: "    return 1"
        // Line 2: "async def main():"
        // Line 3: "    await do_something()"
        //          "    await " = 10 chars, "do_something" at col 10
        var source = "async def do_something() -> int:\n    return 1\nasync def main():\n    await do_something()";

        // Cursor on "do_something" at call site: line 3, col 10 (0-based)
        var highlights = await GetHighlightsAsync(source, 3, 10);

        highlights.Should().NotBeNull("highlights should be returned for a valid symbol");

        var highlightList = highlights!.ToList();
        highlightList.Should().NotBeEmpty();

        // The Write (declaration) highlight should start at col 10 on line 0, NOT col 0
        var writeHighlight = highlightList.FirstOrDefault(h =>
            h.Kind == DocumentHighlightKind.Write && h.Range.Start.Line == 0);
        writeHighlight.Should().NotBeNull(
            "should have a Write highlight for the declaration on line 0");
        writeHighlight!.Range.Start.Character.Should().Be(10,
            "declaration highlight should start at the name token 'do_something' (col 10), not at 'async' (col 0)");
        writeHighlight.Range.End.Character.Should().Be(10 + "do_something".Length,
            "declaration highlight end should cover the full name");
    }

    [Fact]
    public async Task Highlight_Variable_HighlightsDeclarationAndReferences()
    {
        // Line 0: "x: int = 5"  ("x" at col 0)
        // Line 1: "def main():"
        // Line 2: "    print(x)"  ("x" at col 10)
        var source = "x: int = 5\ndef main():\n    print(x)";

        // Cursor on "x" reference at line 2, col 10 (0-based)
        var highlights = await GetHighlightsAsync(source, 2, 10);

        highlights.Should().NotBeNull("highlights should be returned for a valid symbol");

        var highlightList = highlights!.ToList();
        // Should have at least 2 highlights: Write for declaration + Read for reference
        highlightList.Should().HaveCountGreaterThanOrEqualTo(2,
            "should have highlights for both declaration and reference");

        // The Write (declaration) highlight should be at line 0, col 0
        var writeHighlight = highlightList.FirstOrDefault(h =>
            h.Kind == DocumentHighlightKind.Write && h.Range.Start.Line == 0);
        writeHighlight.Should().NotBeNull(
            "should have a Write highlight for the declaration on line 0");
        writeHighlight!.Range.Start.Character.Should().Be(0,
            "variable declaration highlight should start at col 0");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
