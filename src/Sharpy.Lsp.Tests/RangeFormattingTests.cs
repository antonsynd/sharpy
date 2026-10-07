using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Formatting;
using Sharpy.Lsp.Handlers;
using Sharpy.Lsp.Tests.Conformance;
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

    /// <summary>The text the range edits produce, applied the way a client must (<see cref="LspFormattingDriver.ApplyStrict"/>).</summary>
    private async Task<string> ApplyRangeAsync(
        string source, int startLine, int startChar, int endLine, int endChar,
        int tabSize = 4, bool insertSpaces = true)
    {
        var edits = await FormatRangeAsync(source, startLine, startChar, endLine, endChar, tabSize, insertSpaces);
        return LspFormattingDriver.ApplyStrict(source, edits.ToList());
    }

    [Fact]
    public async Task SingleMisIndentedLine_FixedAsync()
    {
        // Source with 8-space indent where 4 is expected. It does not lex — `return x` dedents to a
        // width no enclosing block used (SPY0014) — so this exercises the indent-only fallback, which
        // re-indents line 1 and repairs the document (SPY0014 1 → 0; P22e decision 8, #2168).
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
        // Over-indented source, editor tabSize=2: lines 1-2 are re-indented to 4 spaces.
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        // The body's three lines and the missing final line break are ONE hunk of Format(source), so
        // selecting lines 1-2 applies all of it: re-indenting only lines 1-2 leaves `return x` at 8
        // spaces under a 4-space block, a text that does not parse (P22e, #2168).
        var source = "def foo():\n        x: int = 1\n        y: int = 2\n        return x";
        var applied = await ApplyRangeAsync(source, 1, 0, 2, 20, tabSize: 2);

        applied.Should().Be("def foo():\n    x: int = 1\n    y: int = 2\n    return x\n")
            .And.Be(FormatterService.Format(source).FormattedText);
    }

    [Fact]
    public async Task RangeInsideMultiLineString_NoChangesAsync()
    {
        var source = "x: str = \"\"\"line1\n  indented\n    more\n\"\"\"";
        // Format all lines: a whole-document selection applies what Format Document applies (P22e
        // cell 4, #2168) — here only the final line break — and the string's interior is untouched.
        var applied = await ApplyRangeAsync(source, 0, 0, 3, 3);

        applied.Should().Be(FormatterService.Format(source).FormattedText);
        applied.Split('\n')[1..4].Should().Equal("  indented", "    more", "\"\"\"");
    }

    [Fact]
    public async Task EntireDocument_TabSize2_FourSpaceDocument_GetsNoEditsAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n    x: int = 1\n    return x";
        // A whole-document selection applies what Format Document applies (P22e cell 4, #2168): the
        // indentation is kept at 4 spaces, and the missing final line break is added.
        var lines = source.Split('\n');
        var applied = await ApplyRangeAsync(source, 0, 0, lines.Length - 1, lines[^1].Length, tabSize: 2);

        applied.Should().Be(source + "\n").And.Be(FormatterService.Format(source).FormattedText);
    }

    [Fact]
    public async Task ParseError_TwoSpaceDocument_TabSize2_FallbackWritesFourSpacesAsync()
    {
        // Positive control on the indent-only range fallback: an UNPARSEABLE 2-space document with
        // tabSize 2 is re-indented to 4 spaces.
        var source = "def foo():\n  x: int = 1\nclass: # missing name";
        var edits = await FormatRangeAsync(source, 1, 0, 1, 20, tabSize: 2);

        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("    x: int = 1");
    }

    [Fact]
    public async Task AlreadyFormatted_ReturnsEmptyAsync()
    {
        // Pretty formatter emits a trailing newline; include it in the source
        // to make this a true no-op.
        var source = "def foo():\n    x: int = 1\n    return x\n";
        var edits = await FormatRangeAsync(source, 1, 0, 2, 12);

        edits.Should().BeEmpty();
    }

    /// <summary>
    /// #2227: Format Selection over a canonical match-expression statement makes no edit. The
    /// selection route maps the formatter's lines back one by one, so the blank line the unparser
    /// used to invent after the arms shifted every later line of the range.
    /// </summary>
    [Fact]
    public async Task MatchExpressionStatement_AlreadyFormatted_ReturnsEmptyAsync()
    {
        var source = "def foo(n: int) -> str:\n    s = match n:\n        case 3: \"three\"\n        case _: \"other\"\n    print(s)\n    return s\n";
        var edits = await FormatRangeAsync(source, 1, 0, 5, 12);

        edits.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseErrorFallback_WholeLineSelectionEndingAtColumnZero_LeavesTheNextLineAloneAsync()
    {
        // Decision 2's selection rule holds on the fallback arm as on the primary one: an editor's
        // whole-line selection of line 1 ends at (2,0), and line 2 is NOT selected. Found at
        // /verify-implementation of P22e @ 92af80222 (the fallback passed the raw End.Line).
        var source = "def main():\n  x = 1\n  y = 2\n  z = (\n";
        var edits = await FormatRangeAsync(source, 1, 0, 2, 0);

        edits.Should().ContainSingle();
        edits.First().Range.Start.Line.Should().Be(1);
        edits.First().NewText.Should().Be("    x = 1");

        // Positive control: a selection that reaches into line 2 edits both lines.
        var both = await FormatRangeAsync(source, 1, 0, 2, 7);
        both.Select(e => e.Range.Start.Line).Should().Equal(1, 2);
    }

    [Fact]
    public async Task ParseError_FallsBackToIndentOnlyAsync()
    {
        // Document has a syntax error (missing body) — should fall back to
        // the indent-only formatter for lines inside the requested range.
        var source = "def foo():\n        x: int = 1\nclass: # missing name";
        var edits = await FormatRangeAsync(source, 1, 0, 1, 30);

        edits.Should().ContainSingle();
        edits.First().NewText.Should().Be("    x: int = 1");
    }

    // P22b Phase 2: a PARSEABLE document whose formatting would change meaning is declined (SPY0912)
    // and range formatting returns NO edits rather than the indent-only fallback (which would
    // re-indent the over-indented body — the unparseable twin below shows it does). The declined
    // shape is a still-open #2169 cell, the constraint intersection `[T: A & B]` on line 0 (its
    // output does not re-parse); until #2166 it was the escaped `case `_`:` in the body, which now
    // formats, and until Phase 4 a dropped bracket comment (BracketComment_EditsKeepTheCommentAsync).
    // When #2169's intersection cell is fixed this document needs another declined shape.
    private const string DeclinedDocument = "def g[T: A & B](y: T):\n        x = 1\n        match x:\n            case `_`:\n                print(x)\n";

    [Fact]
    public async Task DeclinedFormatting_Spy0912_ReturnsNoEditsAsync()
    {
        Sharpy.Compiler.Formatting.FormatterService.Format(DeclinedDocument)
            .Diagnostics.Should().ContainSingle(d => d.Code == "SPY0912");

        var edits = await FormatRangeAsync(DeclinedDocument, 1, 0, 4, 24);

        edits.Should().BeEmpty();
    }

    [Fact]
    public async Task DeclinedFormatting_UnparseableTwin_StillTakesTheIndentOnlyFallbackAsync()
    {
        // Positive control for the SPY0912 branch: the same range of the document made unparseable
        // gets the indent-only fallback's edits.
        var edits = await FormatRangeAsync(DeclinedDocument + "class: # missing name", 1, 0, 4, 24);

        edits.Should().NotBeEmpty();
        edits.First().NewText.Should().Be("    x = 1");
    }

    // The Phase 2 declined cell, flipped by the Phase 4 trivia cursor (refs #2077): range formatting
    // of the bracket statement keeps its comment and moves the continuation line with it.
    [Fact]
    public async Task BracketComment_EditsKeepTheCommentAsync()
    {
        const string source = "def main():\n        xs = [1,  # inner\n            2]\n        print(xs)\n";

        var edits = await FormatRangeAsync(source, 1, 0, 3, 17);

        edits.Should().NotBeEmpty();
        string.Join("\n", edits.Select(e => e.NewText)).Should().Contain("    xs = [1,  # inner").And.Contain("        2]");
    }

    // P22b Phase 4 (refs #2077): range formatting of an `else:` clause keeps its header comment on
    // the clause line (the pre-cursor unparser wrote it on the `if` line).
    [Fact]
    public async Task ClauseComment_EditsKeepTheCommentOnItsClauseLineAsync()
    {
        const string source = "def main():\n        x = 0\n        if x > 0:\n            print(x)\n        else:  # else header\n            print(0)\n";

        var edits = await FormatRangeAsync(source, 1, 0, 5, 20);

        var text = string.Join("\n", edits.Select(e => e.NewText));
        text.Should().Contain("    else:  # else header").And.NotContain("if x > 0:  # else header");
    }

    [Fact]
    public async Task MultiLineHole_InteriorIsNotReindentedAsync()
    {
        // PEP 701 (#2022): a replacement field may span lines; its interior is the hole's source text
        // (a t-string's Interpolation.expression), so formatting never re-indents it.
        var source = "def foo(x: int) -> Template:\n    v = t\"\"\"{\n  x\n      + 1\n}\"\"\"\n    return v";
        var edits = await FormatRangeAsync(source, 0, 0, 5, 12);

        edits.Should().NotContain(e => e.Range.Start.Line >= 2 && e.Range.Start.Line <= 3,
            "the hole's interior lines are its source text");
    }

    [Fact]
    public async Task MultiLineHole_ParseErrorFallback_InteriorIsNotReindentedAsync()
    {
        // The indent-only fallback (the document fails to parse) must skip a multi-line hole's
        // interior and closing line like a multi-line string's (#2022). Positive control: line 1,
        // over-indented, IS re-indented by the same request.
        var source = "def foo(x: int) -> Template:\n        v = t\"{\n  x\n      + 1\n}\"\n    return v\nclass: # missing name";
        var edits = await FormatRangeAsync(source, 0, 0, 5, 12);

        edits.Should().Contain(e => e.Range.Start.Line == 1 && e.NewText == "    v = t\"{");
        edits.Should().NotContain(e => e.Range.Start.Line >= 2 && e.Range.Start.Line <= 4,
            "the hole's interior and closing lines are its source text");
    }

    [Fact]
    public async Task TripleQuotedString_InnerTrailingSpacesAreKeptAsync()
    {
        // #2062: the formatter stripped trailing spaces on a line that ends INSIDE a triple-quoted
        // string, changing its value ("p  " -> "p"). Positive control: line 1's trailing spaces,
        // outside any string, ARE stripped by the same request.
        var source = "def foo() -> str:\n    t = 1  \n    s = \"\"\"p  \nq\"\"\"\n    return s";
        var edits = await FormatRangeAsync(source, 0, 0, 4, 12);

        edits.Should().Contain(e => e.Range.Start.Line == 1 && e.NewText == "    t = 1");
        edits.Should().NotContain(e => e.Range.Start.Line == 2 || e.Range.Start.Line == 3,
            "a line that ends or starts inside a string literal is its data");
    }

    [Fact]
    public async Task ParseErrorFallback_WhitespaceOnlyLineInsideAString_IsNotBlankedAsync()
    {
        // #2062 twin on the indent-only fallback's blank-line arm. Positive control: line 1,
        // over-indented, IS re-indented.
        var source = "def foo() -> str:\n        s = \"\"\"a\n   \nb\"\"\"\n        return s\nclass: # missing name";
        var edits = await FormatRangeAsync(source, 0, 0, 4, 20);

        edits.Should().Contain(e => e.Range.Start.Line == 1 && e.NewText == "    s = \"\"\"a");
        edits.Should().NotContain(e => e.Range.Start.Line == 2 || e.Range.Start.Line == 3,
            "the string's inner lines are its data");
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
        // Over-indented last line, editor tabSize=2: re-indented to 4 spaces.
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        var source = "def foo():\n        x: int = 1\n        return x";
        var lines = source.Split('\n');
        // The body's two lines and the missing final line break are ONE hunk of Format(source), so
        // selecting the last line applies all of it: re-indenting only `return x` leaves it dedented
        // below `x: int = 1`'s 8 spaces, a text that does not lex (P22e, #2168).
        var lastLine = lines.Length - 1;
        var applied = await ApplyRangeAsync(source, lastLine, 0, lastLine, lines[lastLine].Length, tabSize: 2);

        applied.Should().Be("def foo():\n    x: int = 1\n    return x\n")
            .And.Be(FormatterService.Format(source).FormattedText);
    }

    [Fact]
    public async Task TabsPreference_StillWritesFourSpacesInRangeAsync()
    {
        // Owner ruling 2026-09-30 (P22b, indentation.md): the editor's tabSize/insertSpaces never choose
        // the indentation — Sharpy is exactly 4 spaces per level; a 2-space or tab re-indent does not lex.
        // The last line's selection takes the missing final line break with it (P22e decision 2, #2168).
        var source = "def foo():\n        x: int = 1";
        var applied = await ApplyRangeAsync(source, 1, 0, 1, 20, insertSpaces: false);

        applied.Should().Be("def foo():\n    x: int = 1\n");
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
