using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

public class RangeFormattingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly SharpyRangeFormattingHandler _handler;

    public RangeFormattingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _handler = new SharpyRangeFormattingHandler(_workspace);
    }

    private async Task<TextEditContainer> FormatRangeAsync(
        string source, int startLine, int startChar, int endLine, int endChar,
        int tabSize = 4, bool insertSpaces = true)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new DocumentRangeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Range = new LspRange(
                new Position(startLine, startChar),
                new Position(endLine, endChar)),
            Options = new FormattingOptions
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces
            }
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task SingleMisIndentedLine_FixedAsync()
    {
        // Source with 8-space indent where 4 is expected (valid to lexer as multiple of 4)
        var source = "def foo():\n        x: int = 1\n    return x";
        // Format only line 1 (the over-indented line)
        var edits = await FormatRangeAsync(source, 1, 0, 1, 20);

        edits.Should().NotBeEmpty();
        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("    x: int = 1");
    }

    [Fact]
    public async Task MultipleLines_ReformattedAsync()
    {
        // Valid source but reformat with tabSize=2
        var source = "def foo():\n    x: int = 1\n    y: int = 2\n    return x";
        // Format lines 1-2
        var edits = await FormatRangeAsync(source, 1, 0, 2, 20, tabSize: 2);

        edits.Should().HaveCount(2);
    }

    [Fact]
    public async Task RangeInsideMultiLineString_NoChangesAsync()
    {
        var source = "x: str = \"\"\"line1\n  indented\n    more\n\"\"\"";
        // Format all lines (multi-line string interior should be preserved)
        var edits = await FormatRangeAsync(source, 0, 0, 3, 3);

        edits.Should().BeEmpty();
    }

    [Fact]
    public async Task EntireDocument_ReformatsWithTabSize2Async()
    {
        var source = "def foo():\n    x: int = 1\n    return x";
        var lines = source.Split('\n');
        var edits = await FormatRangeAsync(source, 0, 0, lines.Length - 1, lines[^1].Length, tabSize: 2);

        edits.Should().NotBeEmpty();
        foreach (var edit in edits)
        {
            edit.NewText.Should().StartWith("  ");
        }
    }

    [Fact]
    public async Task AlreadyFormatted_ReturnsEmptyAsync()
    {
        var source = "def foo():\n    x: int = 1\n    return x";
        var edits = await FormatRangeAsync(source, 1, 0, 2, 12);

        edits.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstLine_FormattedAsync()
    {
        var source = "  x: int = 1\ndef foo():\n    pass";
        // Format only first line (top-level should be at indent 0)
        var edits = await FormatRangeAsync(source, 0, 0, 0, 14);

        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("x: int = 1");
    }

    [Fact]
    public async Task LastLine_ReformattedAsync()
    {
        // Valid 4-space source, reformat last line with tabSize=2
        var source = "def foo():\n    x: int = 1\n    return x";
        var lines = source.Split('\n');
        var lastLine = lines.Length - 1;
        var edits = await FormatRangeAsync(source, lastLine, 0, lastLine, lines[lastLine].Length, tabSize: 2);

        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("  return x");
    }

    [Fact]
    public async Task TabsPreference_UsesTabsInRangeAsync()
    {
        // Valid 4-space source, reformat with tabs
        var source = "def foo():\n    x: int = 1";
        var edits = await FormatRangeAsync(source, 1, 0, 1, 20, insertSpaces: false);

        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("\tx: int = 1");
    }

    [Fact]
    public async Task UnknownDocument_ReturnsEmptyAsync()
    {
        var request = new DocumentRangeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Range = new LspRange(new Position(0, 0), new Position(0, 10)),
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
