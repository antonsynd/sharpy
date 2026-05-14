using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class OnTypeFormattingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly SharpyOnTypeFormattingHandler _handler;

    public OnTypeFormattingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _handler = new SharpyOnTypeFormattingHandler(_workspace);
    }

    private async Task<TextEditContainer?> OnTypeAsync(
        string source, int line, int character, string ch,
        int tabSize = 4, bool insertSpaces = true)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new DocumentOnTypeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, character),
            Character = ch,
            Options = new FormattingOptions
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces
            }
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task OverIndentedLine_NormalizedToOneLevelAsync()
    {
        // The line was typed with too much indent for its block level; the
        // indent map (from lexer INDENT/DEDENT) says level 1, so we expect
        // 4 spaces.
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 9, ch: "\n");

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("    ");
    }

    [Fact]
    public async Task AlreadyCorrect_ReturnsNullAsync()
    {
        // Line is already at the expected indent — no edit emitted.
        var source = "def foo():\n    x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 5, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task TopLevelAlreadyCorrect_NoEditAsync()
    {
        // A correctly unindented top-level line should yield no edit.
        var source = "x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 0, character: 0, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task BlankLine_NoEditAsync()
    {
        var source = "def foo():\n    x: int = 1\n\n";
        // Cursor on the blank line — nothing to align.
        var edits = await OnTypeAsync(source, line: 2, character: 0, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task NestedOverIndent_UsesNestedLevelAsync()
    {
        // Two block levels — expected indent is 8 spaces. The current line
        // has 16 spaces, so the handler should collapse it back to 8.
        var source = "def foo():\n    if True:\n                x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 2, character: 17, ch: "\n");

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("        ");
    }

    [Fact]
    public async Task TabsPreference_UsesTabsAsync()
    {
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(
            source, line: 1, character: 9, ch: "\n", insertSpaces: false);

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("\t");
    }

    [Fact]
    public async Task TabSize2_UsesTwoSpacesAsync()
    {
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 9, ch: "\n", tabSize: 2);

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("  ");
    }

    [Fact]
    public async Task ColonTrigger_AlignsCurrentLineAsync()
    {
        // The user just finished typing 'else:' but the editor inserted it
        // with extra indent relative to its block level. The indent map
        // (from lexer-emitted INDENT tokens) classifies it at the inner block
        // level — assert we use that level uniformly regardless of trigger.
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 8, ch: ":");

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("    ");
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNullAsync()
    {
        var request = new DocumentOnTypeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Position = new Position(0, 0),
            Character = "\n",
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task PositionOutOfBounds_ReturnsNullAsync()
    {
        var source = "def foo():\n    pass\n";
        var edits = await OnTypeAsync(source, line: 100, character: 0, ch: "\n");
        edits.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
