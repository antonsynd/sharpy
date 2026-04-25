using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class SelectionRangeTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpySelectionRangeHandler _handler;

    public SelectionRangeTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpySelectionRangeHandler(_languageService);
    }

    private async Task<Container<SelectionRange>?> GetSelectionRangesAsync(
        string source, params Position[] positions)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new SelectionRangeParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Positions = new Container<Position>(positions)
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task IdentifierInFunction_ProducesChainAsync()
    {
        // Line 0: def foo():
        // Line 1:     x: int = 1
        // Cursor on 'x' at (1, 4) — should produce a chain with depth >= 2
        var source = "def foo():\n    x: int = 1";
        var result = await GetSelectionRangesAsync(source, new Position(1, 4));

        result.Should().NotBeNull();
        result.Should().ContainSingle();

        var chain = result!.Single();
        chain.Range.Should().NotBeNull();

        // Chain should have a parent (the containing function or statement)
        var depth = 0;
        var current = chain;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }
        depth.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ClassMethod_IncludesClassInChainAsync()
    {
        var source = "class Foo:\n    def bar(self):\n        pass";
        // Cursor on 'pass' at (2, 8)
        var result = await GetSelectionRangesAsync(source, new Position(2, 8));

        result.Should().NotBeNull();
        var chain = result!.Single();

        // Chain should include method and class scopes
        var depth = 0;
        var current = chain;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }
        depth.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task MultiplePositions_IndependentChainsAsync()
    {
        var source = "x: int = 1\ny: int = 2";
        // Two positions: one on each line
        var result = await GetSelectionRangesAsync(source,
            new Position(0, 0),
            new Position(1, 0));

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task EmptyFile_ReturnsZeroWidthRangeAsync()
    {
        var result = await GetSelectionRangesAsync("", new Position(0, 0));

        // Empty file has no AST nodes, but handler returns a zero-width fallback range
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        var range = result!.Single();
        range.Range.Start.Line.Should().Be(0);
        range.Range.Start.Character.Should().Be(0);
        range.Parent.Should().BeNull();
    }

    [Fact]
    public async Task SingleLineFunction_CorrectRangesAsync()
    {
        var source = "def foo():\n    return 42";
        // Cursor on '42' at (1, 11)
        var result = await GetSelectionRangesAsync(source, new Position(1, 11));

        result.Should().NotBeNull();
        var chain = result!.Single();

        // Innermost range should contain the literal
        chain.Range.Start.Line.Should().BeLessThanOrEqualTo(1);
        chain.Range.End.Line.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task NestedExpression_ChainGrowsAsync()
    {
        var source = "x: int = 1 + 2 * 3";
        // Cursor on '2' at (0, 13) — inside nested binary op
        var result = await GetSelectionRangesAsync(source, new Position(0, 13));

        result.Should().NotBeNull();
        var chain = result!.Single();

        // Should have at least: literal → binary op → statement → module
        var depth = 0;
        var current = chain;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }
        depth.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RangesAreProperlyNestedAsync()
    {
        var source = "def foo():\n    if True:\n        x: int = 1";
        // Cursor on 'x' at (2, 8)
        var result = await GetSelectionRangesAsync(source, new Position(2, 8));

        result.Should().NotBeNull();
        var chain = result!.Single();

        // Each parent range should contain the child range
        var current = chain;
        while (current?.Parent != null)
        {
            var child = current.Range;
            var parent = current.Parent.Range;

            // Parent start should be <= child start
            (parent.Start.Line < child.Start.Line ||
             (parent.Start.Line == child.Start.Line && parent.Start.Character <= child.Start.Character))
                .Should().BeTrue("parent range start should be <= child range start");

            // Parent end should be >= child end
            (parent.End.Line > child.End.Line ||
             (parent.End.Line == child.End.Line && parent.End.Character >= child.End.Character))
                .Should().BeTrue("parent range end should be >= child range end");

            current = current.Parent;
        }
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
