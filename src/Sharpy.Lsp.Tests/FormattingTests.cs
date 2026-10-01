using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class FormattingTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly SharpyFormattingHandler _handler;

    public FormattingTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _handler = new SharpyFormattingHandler(_workspace);
    }

    private async Task<string?> FormatAsync(string source, int tabSize = 4, bool insertSpaces = true)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Options = new FormattingOptions
            {
                TabSize = tabSize,
                InsertSpaces = insertSpaces
            }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        if (result is null || !result.Any())
            return null; // no changes needed

        return result.Single().NewText;
    }

    [Fact]
    public async Task AlreadyFormatted_ReturnsNullAsync()
    {
        // The pretty formatter always emits a trailing newline, so already-
        // formatted source must include it to be a no-op.
        var source = "def foo():\n    x: int = 1\n    return x\n";
        var formatted = await FormatAsync(source);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task EmptyFile_ReturnsNullAsync()
    {
        var formatted = await FormatAsync("");
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task SingleLine_NoChangeAsync()
    {
        var source = "x: int = 42\n";
        var formatted = await FormatAsync(source);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task BlankLines_InsideFunctionAreNormalizedAsync()
    {
        // The pretty formatter normalizes blank lines inside function bodies
        // (using the BlankLinesBetweenStatements rule = 0 by default), so a
        // stray blank line between statements is removed.
        var source = "def foo():\n    x: int = 1\n\n    return x\n";
        var formatted = await FormatAsync(source);

        formatted.Should().NotBeNull();
        formatted.Should().Be("def foo():\n    x: int = 1\n    return x\n");
    }

    [Fact]
    public async Task TabSize2_FourSpaceDocument_GetsNoEditsAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n    x: int = 1\n    return x\n";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task TabSize2_Nested_ReindentsToFourSpacesAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        if True:\n                x: int = 1\n";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().Be("def foo():\n    if True:\n        x: int = 1\n");
    }

    [Fact]
    public async Task InsertTabs_StillWritesFourSpacesAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        x: int = 1\n";
        var formatted = await FormatAsync(source, insertSpaces: false);

        formatted.Should().Be("def foo():\n    x: int = 1\n");
    }

    [Fact]
    public async Task ParseError_TwoSpaceDocument_TabSize2_FallbackWritesFourSpacesAsync()
    {
        // Positive control on the indent-only fallback: an UNPARSEABLE document indented with 2
        // spaces, formatted with tabSize 2, is re-indented to 4 spaces (the fallback ignores the
        // editor's tabSize too).
        var source = "def foo():\n  x: int = 1\nclass: # missing name";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted!.Split('\n')[1].Should().Be("    x: int = 1");
    }

    [Fact]
    public async Task StringWithColon_DoesNotAffectIndentAsync()
    {
        // A colon inside a string should not be confused with a block-starting colon
        var source = "def foo():\n    x: str = \"hello: world\"\n    return x\n";
        var formatted = await FormatAsync(source);

        // Already correctly formatted
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task IfElifElse_CorrectlyIndentedAsync()
    {
        // Over-indented source, editor tabSize=2: every clause and body lands on 4-space levels.
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        if True:\n                x: int = 1\n        elif False:\n                x = 2\n        else:\n                x = 3";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().Be("def foo():\n    if True:\n        x: int = 1\n    elif False:\n        x = 2\n    else:\n        x = 3\n");
    }

    [Fact]
    public async Task ClassWithDecorator_FormattedCorrectlyAsync()
    {
        // Over-indented source, editor tabSize=2: decorator indent tracking at 4-space levels.
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "class Foo:\n        @staticmethod\n        def bar() -> int:\n                return 1";
        var formatted = await FormatAsync(source, tabSize: 2);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("\n    @staticmethod\n    def bar() -> int:\n        return 1");
    }

    [Fact]
    public async Task MultipleTopLevelDeclarations_StayAtLevel0Async()
    {
        // Pretty formatter inserts two blank lines around top-level declarations.
        var source = "class A:\n    x: int = 1\n\n\nclass B:\n    y: int = 2\n";
        var formatted = await FormatAsync(source);

        // Already correctly formatted with blank-line rule applied.
        formatted.Should().BeNull();
    }

    [Fact]
    public async Task MultipleTopLevelDeclarations_AddsBlankLinesAsync()
    {
        // When the blank-line rule is not satisfied, the formatter inserts them.
        var source = "class A:\n    x: int = 1\nclass B:\n    y: int = 2\n";
        var formatted = await FormatAsync(source);

        formatted.Should().NotBeNull();
        // Two blank lines between top-level class declarations.
        formatted.Should().Contain("x: int = 1\n\n\nclass B");
    }

    [Fact]
    public async Task ExtraWhitespace_NormalizedAsync()
    {
        // Source has 8-space indent where 4 is expected (valid to lexer since it's a multiple of 4)
        var source = "def foo():\n        x: int = 1";
        var formatted = await FormatAsync(source);

        // The lexer sees one INDENT (8 spaces = one indent level), formatter normalizes to 4 spaces
        formatted.Should().NotBeNull();
        formatted.Should().Contain("    x: int = 1");
    }

    [Fact]
    public async Task ParseError_FallsBackToIndentOnlyAsync()
    {
        // Source has a syntax error (missing body) — FormatterService bails out
        // and we fall back to the lexer-based indent-only formatter.
        // The over-indented body is normalized to 4 spaces (one indent level).
        var source = "def foo():\n        x: int = 1\nclass: # missing name";
        var formatted = await FormatAsync(source);

        formatted.Should().NotBeNull();
        formatted.Should().Contain("    x: int = 1");
        // Fallback preserves the trailing line verbatim — no pretty-printer
        // restructuring, just indent normalization.
        formatted.Should().Contain("class: # missing name");
    }

    [Fact]
    public async Task ParseError_Fallback_KeepsWhitespaceOnlyLineInsideAStringAsync()
    {
        // #2062: the indent-only fallback blanked a whitespace-only line INSIDE a triple-quoted string
        // (changing the string's value). Positive control: the over-indented body line IS re-indented.
        var source = "def foo() -> str:\n        s = \"\"\"a\n   \nb\"\"\"\n        return s\nclass: # missing name";
        var formatted = await FormatAsync(source);

        formatted.Should().NotBeNull();
        formatted!.Split('\n').Should().StartWith(new[] { "def foo() -> str:", "    s = \"\"\"a", "   ", "b\"\"\"" });
    }

    // P22b Phase 2 (refs #2077): formatting this PARSEABLE document would drop the bracket comment,
    // so the formatter declines (SPY0912) and the handler returns NO edits — the indent-only
    // fallback is for documents that fail to parse, and it WOULD re-indent this one (the body is
    // over-indented; asserted below, so the no-edits result is not vacuous).
    // This cell flips to "edits keep the comment" in Phase 4, when the trivia cursor anchors
    // bracket comments (refs #2077).
    private const string DeclinedDocument = "def main():\n        xs = [1,  # inner\n            2]\n        print(xs)\n";

    [Fact]
    public async Task DeclinedFormatting_Spy0912_ReturnsNoEditsAsync()
    {
        var direct = Sharpy.Compiler.Formatting.FormatterService.Format(DeclinedDocument);
        direct.Diagnostics.Should().ContainSingle(d => d.Code == "SPY0912");
        FormattingFallback.ReindentDocument(DeclinedDocument).Should().NotBe(DeclinedDocument);

        var formatted = await FormatAsync(DeclinedDocument);

        formatted.Should().BeNull();
    }

    [Fact]
    public async Task DeclinedFormatting_UnparseableTwin_StillTakesTheIndentOnlyFallbackAsync()
    {
        // Positive control for the SPY0912 branch: the same document made unparseable takes the
        // indent-only fallback, which keeps the comment and re-indents the body.
        var formatted = await FormatAsync(DeclinedDocument + "class: # missing name");

        formatted.Should().NotBeNull();
        formatted.Should().Contain("    xs = [1,  # inner");
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNullAsync()
    {
        var request = new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier("file:///nonexistent.spy"),
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            }
        };

        var result = await _handler.Handle(request, CancellationToken.None);
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
