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
    public async Task TabsPreference_StillUsesFourSpacesAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(
            source, line: 1, character: 9, ch: "\n", insertSpaces: false);

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("    ");
    }

    [Fact]
    public async Task TabSize2_StillUsesFourSpacesAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 9, ch: "\n", tabSize: 2);

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("    ");
    }

    [Fact]
    public async Task TabSize2_FourSpaceLine_GetsNoEditAsync()
    {
        // Positive control: a correctly indented line with editor tabSize=2 is left alone.
        var source = "def foo():\n    x: int = 1\n";
        var edits = await OnTypeAsync(source, line: 1, character: 5, ch: "\n", tabSize: 2);

        edits.Should().BeNull();
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

    // ---- P22e decision 6 (#2168): on-type edits only a line that starts a logical line, outside any
    // literal, and only when the indent-only check passes. Each cell states what the handler applied
    // before (@ d97821ff2); now it applies nothing.

    [Fact]
    public async Task Cell11_ReindentThatMovesAStatementOutOfItsBlock_NoEditAsync()
    {
        // Before: `print(2)` 16 → 8 spaces, out of `if False:` — the program then printed 2.
        var source = "def main():\n        if False:\n                print(1)\n                print(2)\n";
        var edits = await OnTypeAsync(source, line: 3, character: 24, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell12_LineInsideAClosedTripleQuotedString_NoEditAsync()
    {
        // Before: the string line lost its 8 spaces — the string's value changed.
        var source = "s = \"\"\"\n        key: value\n\"\"\"\nprint(s)\n";
        var edits = await OnTypeAsync(source, line: 1, character: 11, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell13_LineInsideAnUnterminatedTripleQuotedString_NoEditAsync()
    {
        // Before: the string line 8 → 4 spaces.
        var source = "def main():\n    s = \"\"\"\n        key: value\n";
        var edits = await OnTypeAsync(source, line: 2, character: 11, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell17_BracketContinuationLineOfAFormattedDocument_NoEditAsync()
    {
        // Before: `2]` snapped from column 10 to column 4 on a document that is Format's fixed point.
        var source = "def main():\n    xs = [1,  # one\n          2]\n    print(xs)\n";
        var edits = await OnTypeAsync(source, line: 2, character: 12, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell18_MultiLineFStringHoleLine_NoEditAsync()
    {
        // Before: the hole's `n` line snapped to the block's level.
        var source = "def main():\n    n = 3\n    s = f\"\"\"a{\n            n\n    }b\"\"\"\n    print(s)\n";
        var edits = await OnTypeAsync(source, line: 3, character: 12, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell18_BackslashContinuationLine_NoEditAsync()
    {
        // Before: `2` snapped from column 12 to the block's level.
        var source = "def main():\n    x = 1 + \\\n            2\n    print(x)\n";
        var edits = await OnTypeAsync(source, line: 2, character: 12, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell18_DictLiteralEntryLine_NoEditAsync()
    {
        // Before: the `"a": 1,` entry snapped from column 12 to the block's level.
        var source = "def main():\n    d = {\n            \"a\": 1,\n    }\n    print(d)\n";
        var edits = await OnTypeAsync(source, line: 2, character: 16, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell19_DocstringOpenedAboveAMultiLineString_NoEditAsync()
    {
        // The new `"""` in `f` pairs with `g`'s opener: `g`'s string content lexes as code. Before:
        // `        key: value` → `    key: value`, so `g`'s string lost 4 spaces once the docstring was closed.
        var source = "def f() -> int:\n    \"\"\"\n    return 1\ndef g() -> str:\n    s = \"\"\"\n        key: value\n    \"\"\"\n    return s\n";
        var edits = await OnTypeAsync(source, line: 5, character: 11, ch: ":");

        edits.Should().BeNull();
    }

    [Theory]
    [InlineData("f", "}")]
    [InlineData("b", "é")]
    public async Task Cell20_LexerAbortsInsideAClosedLiteral_NoEditAsync(string prefix, string abortLine)
    {
        // The lexer aborts INSIDE the literal (`Unmatched '}'`; non-ASCII in a byte string) and resumes on the
        // next line as code. Before: `        key: value` re-indented 8 → 4.
        var source = $"def main():\n    s = {prefix}\"\"\"\n        {abortLine}\n        key: value\n    \"\"\"\n    print(s)\n";
        var edits = await OnTypeAsync(source, line: 3, character: 11, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task Cell21_BlockOpenerMovedBetweenTwoWidths_NoEditAsync()
    {
        // Before: `if a == 1:` 8 → 4 while `a = 1` stays at 8 — the lexer then reports SPY0014.
        var source = "def main():\n        a = 1\n        if a == 1:\n";
        var edits = await OnTypeAsync(source, line: 2, character: 18, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task OverIndentedElse_WouldLeaveTheBlockTheLexerOpened_NoEditAsync()
    {
        // A typed `else:` deeper than the line above it: the lexer opens a block for it, the colon rule does
        // not. Before: 12 → 8 spaces, a block change (lexer depth 3 → 2). Lead ruling at P3T3 (option a):
        // the indent-only routes never re-nest, on-type's own line included. An `else:` typed AT the `if`
        // body's width is not dedented, here or before (@ d97821ff2): the indent map has no keyword rule.
        var source = "def main():\n    x = True\n    if x:\n        print(1)\n            else:\n";
        var edits = await OnTypeAsync(source, line: 4, character: 17, ch: ":");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task UnexpectedIndentOnTheRequestedLine_NoEditAsync()
    {
        // The document does not parse and `y = 2` is an unexpected indent (the lexer opens a block for it).
        // Before: 8 → 4 spaces, a block change (lexer depth 2 → 1). Lead ruling at P3T3 (option a): no edit.
        var source = "def main():\n    x = 1\n        y = 2\n    print(x, y)\n";
        var edits = await OnTypeAsync(source, line: 2, character: 13, ch: "\n");

        edits.Should().BeNull();
    }

    [Fact]
    public async Task UnparseableDocument_WidthAtConstantDepth_IsReindentedAsync()
    {
        // Positive control for the indent-only check's arm: the document does not parse (`y = (` is open),
        // and the re-indent changes a width, not a block — the check does not refuse everything.
        var source = "def foo():\n        x: int = 1\ny = (\n";
        var edits = await OnTypeAsync(source, line: 1, character: 9, ch: "\n");

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("    ");
    }

    [Fact]
    public async Task MisIndentedSoleLineOfANestedBlock_ParseableDocument_IsReindentedAsync()
    {
        // Positive control for the net's arm: the document parses, the re-indent keeps its AST.
        var source = "def main():\n    if True:\n                print(1)\n    print(2)\n";
        var edits = await OnTypeAsync(source, line: 2, character: 24, ch: "\n");

        edits.Should().NotBeNull();
        edits!.Should().ContainSingle();
        edits.First().NewText.Should().Be("        ");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
