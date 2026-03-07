using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class WorkspaceSymbolTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharplyWorkspace _workspace;
    private readonly SharplyWorkspaceSymbolHandler _handler;

    public WorkspaceSymbolTests()
    {
        _workspace = new SharplyWorkspace(_api, NullLogger<SharplyWorkspace>.Instance);
        _handler = new SharplyWorkspaceSymbolHandler(_workspace);
    }

    private async Task<Container<WorkspaceSymbol>?> SearchSymbolsAsync(string query)
    {
        var request = new WorkspaceSymbolParams { Query = query };
        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task EmptyQuery_ReturnsAllSymbolsAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);
        _workspace.OpenDocument("file:///b.spy", "class Bar:\n    x: int = 1", 1);

        var results = await SearchSymbolsAsync("");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
        results.Should().Contain(s => s.Name == "main");
        results.Should().Contain(s => s.Name == "Bar");
    }

    [Fact]
    public async Task FilteredQuery_ReturnsMatchingSymbolsAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);
        _workspace.OpenDocument("file:///b.spy", "class Bar:\n    x: int = 1", 1);

        var results = await SearchSymbolsAsync("foo");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
        results.Should().NotContain(s => s.Name == "Bar");
    }

    [Fact]
    public async Task SubstringSearch_FindsMatchAsync()
    {
        _workspace.OpenDocument("file:///a.spy", "def foo():\n    pass\n\ndef main():\n    foo()", 1);

        var results = await SearchSymbolsAsync("fo");

        results.Should().NotBeNull();
        results!.Should().Contain(s => s.Name == "foo");
    }

    [Fact]
    public async Task NoDocuments_ReturnsEmptyAsync()
    {
        var results = await SearchSymbolsAsync("");

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
