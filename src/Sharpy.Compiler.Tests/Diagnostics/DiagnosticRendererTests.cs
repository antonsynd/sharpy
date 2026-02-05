using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class DiagnosticRendererTests
{
    private readonly DiagnosticRenderer _renderer = new(useColor: false);

    [Fact]
    public void Render_ErrorWithSpan_ShowsSourceContextAndUnderline()
    {
        var source = "x: int = \"hello\"";
        var sourceText = new SourceText(source, "file.spy");
        var span = new TextSpan(9, 7); // "hello" including quotes

        var diagnostic = new CompilerDiagnostic(
            "Type 'str' is not assignable to 'int'",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 10,
            FilePath: "file.spy",
            Code: "SPY0201",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("error[SPY0201]: Type 'str' is not assignable to 'int'");
        result.Should().Contain("--> file.spy:1:10");
        result.Should().Contain("x: int = \"hello\"");
        result.Should().Contain("^^^^^^^");
    }

    [Fact]
    public void Render_ErrorWithLineColumnOnly_ShowsCaret()
    {
        var source = "x: int = \"hello\"";
        var sourceText = new SourceText(source, "file.spy");

        var diagnostic = new CompilerDiagnostic(
            "Undefined variable 'x'",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 1,
            FilePath: "file.spy",
            Code: "SPY0200");

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("error[SPY0200]: Undefined variable 'x'");
        result.Should().Contain("--> file.spy:1:1");
        result.Should().Contain("x: int = \"hello\"");
        result.Should().Contain("^");
        // Should be a single caret, not multiple
        result.Should().NotContain("^^");
    }

    [Fact]
    public void Render_WarningWithSpan_ShowsWarningFormatting()
    {
        var source = "x = 42\ny = 10\nreturn x\nz = 99";
        var sourceText = new SourceText(source, "test.spy");
        // Span for "z = 99" on line 4
        var span = new TextSpan(source.IndexOf("z = 99"), 6);

        var diagnostic = new CompilerDiagnostic(
            "Unreachable code detected",
            CompilerDiagnosticSeverity.Warning,
            Line: 4, Column: 1,
            FilePath: "test.spy",
            Code: "SPY0450",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("warning[SPY0450]: Unreachable code detected");
        result.Should().Contain("--> test.spy:4:1");
        result.Should().Contain("z = 99");
    }

    [Fact]
    public void Render_NoLocationInfo_ShowsOnlyHeader()
    {
        var diagnostic = new CompilerDiagnostic(
            "Internal compiler error",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0599");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("error[SPY0599]: Internal compiler error");
        // Should not have location arrow or source context
        result.Should().NotContain("-->");
        result.Should().NotContain("|");
    }

    [Fact]
    public void Render_NoCode_ShowsSeverityWithoutBrackets()
    {
        var diagnostic = new CompilerDiagnostic(
            "Something went wrong",
            CompilerDiagnosticSeverity.Error);

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("error: Something went wrong");
        result.Should().NotContain("[");
    }

    [Fact]
    public void Render_MultiLineSource_CorrectLineDisplayed()
    {
        var source = "def greet(name: str) -> str:\n    x: int = \"hello\"\n    return x";
        var sourceText = new SourceText(source, "example.spy");
        // Span for "hello" on line 2
        var spanStart = source.IndexOf("\"hello\"");
        var span = new TextSpan(spanStart, 7);

        var diagnostic = new CompilerDiagnostic(
            "Type 'str' is not assignable to 'int'",
            CompilerDiagnosticSeverity.Error,
            FilePath: "example.spy",
            Code: "SPY0201",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("--> example.spy:2:");
        result.Should().Contain("x: int = \"hello\"");
        result.Should().Contain("^^^^^^^");
    }

    [Fact]
    public void Render_SpanDerivesLineColumn_WhenDiagnosticHasNone()
    {
        var source = "x = 1\ny = \"bad\"";
        var sourceText = new SourceText(source, "test.spy");
        var span = new TextSpan(source.IndexOf("\"bad\""), 5);

        var diagnostic = new CompilerDiagnostic(
            "Type mismatch",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0201",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        // Should derive line 2, column 5 from span
        result.Should().Contain("--> test.spy:2:");
        result.Should().Contain("y = \"bad\"");
        result.Should().Contain("^^^^^");
    }

    [Fact]
    public void Render_FilePathFromSourceText_WhenDiagnosticHasNone()
    {
        var source = "x = 1";
        var sourceText = new SourceText(source, "fallback.spy");

        var diagnostic = new CompilerDiagnostic(
            "Some error",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 1);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("--> fallback.spy:1:1");
    }

    [Fact]
    public void Render_FilePathOnly_ShowsFileLocation()
    {
        var diagnostic = new CompilerDiagnostic(
            "File error",
            CompilerDiagnosticSeverity.Error,
            FilePath: "bad_file.spy");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("--> bad_file.spy");
        // No source context since no line info
        result.Should().NotContain("|");
    }

    [Fact]
    public void Render_DoubleDigitLineNumbers_PaddedCorrectly()
    {
        // Create source with 15 lines
        var lines = Enumerable.Range(1, 15).Select(i => $"line_{i} = {i}").ToList();
        var source = string.Join("\n", lines);
        var sourceText = new SourceText(source, "test.spy");

        var diagnostic = new CompilerDiagnostic(
            "Error on line 12",
            CompilerDiagnosticSeverity.Error,
            Line: 12, Column: 1,
            FilePath: "test.spy",
            Code: "SPY0200");

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("12 |");
        result.Should().Contain("line_12 = 12");
    }

    [Fact]
    public void Render_WithColorEnabled_ContainsAnsiCodes()
    {
        var colorRenderer = new DiagnosticRenderer(useColor: true);

        var diagnostic = new CompilerDiagnostic(
            "Test error",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0001");

        var result = colorRenderer.Render(diagnostic);

        // Should contain ANSI escape codes
        result.Should().Contain("\x1b[");
    }

    [Fact]
    public void Render_InfoSeverity_ShowsInfoPrefix()
    {
        var diagnostic = new CompilerDiagnostic(
            "Informational message",
            CompilerDiagnosticSeverity.Info,
            Code: "SPY0001");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("info[SPY0001]: Informational message");
    }

    [Fact]
    public void Render_HintSeverity_ShowsHintPrefix()
    {
        var diagnostic = new CompilerDiagnostic(
            "Consider using...",
            CompilerDiagnosticSeverity.Hint,
            Code: "SPY0001");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("hint[SPY0001]: Consider using...");
    }

    [Fact]
    public void Render_ColumnPastEndOfLine_ClampedCorrectly()
    {
        var source = "short";
        var sourceText = new SourceText(source, "test.spy");

        var diagnostic = new CompilerDiagnostic(
            "Past end",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 100,
            FilePath: "test.spy");

        var result = _renderer.Render(diagnostic, sourceText);

        // Should not throw, should render something reasonable
        result.Should().Contain("short");
        result.Should().Contain("^");
    }

    [Fact]
    public void Render_EmptySourceLine_HandledGracefully()
    {
        var source = "line1\n\nline3";
        var sourceText = new SourceText(source, "test.spy");

        var diagnostic = new CompilerDiagnostic(
            "Empty line issue",
            CompilerDiagnosticSeverity.Error,
            Line: 2, Column: 1,
            FilePath: "test.spy");

        var result = _renderer.Render(diagnostic, sourceText);

        // Should not throw for empty line
        result.Should().Contain("--> test.spy:2:1");
    }

    [Fact]
    public void Render_NullSourceText_FallsBackToSimpleFormat()
    {
        var diagnostic = new CompilerDiagnostic(
            "No source available",
            CompilerDiagnosticSeverity.Error,
            Line: 5, Column: 3,
            FilePath: "unknown.spy",
            Code: "SPY0200");

        var result = _renderer.Render(diagnostic, sourceText: null);

        result.Should().Contain("error[SPY0200]: No source available");
        result.Should().Contain("--> unknown.spy:5:3");
        // No source context since no source text
        result.Should().NotContain("|");
    }

    [Fact]
    public void Render_MultiLineSpan_ShowsFirstLineOnly()
    {
        // A span that covers multiple lines should only underline the first line's portion
        var source = "if x > 0:\n    y = 1\n    z = 2";
        var sourceText = new SourceText(source, "test.spy");
        // Span covers "x > 0:\n    y = 1\n    z = 2" (from column 4 to end)
        var span = new TextSpan(3, source.Length - 3);

        var diagnostic = new CompilerDiagnostic(
            "Multi-line expression",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0100",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        // Should show the first line of the span
        result.Should().Contain("if x > 0:");
        // Underline should only cover the first line, not extend beyond
        result.Should().Contain("^");
        // Should not crash or produce garbage
        result.Should().Contain("--> test.spy:1:");
    }

    [Fact]
    public void Render_TabsInSourceLine_ExpandedAndAligned()
    {
        // Source: "\tif x > 0:" — tab before 'if'
        var source = "\tif x > 0:\n\t\ty = 1";
        var sourceText = new SourceText(source, "test.spy");

        var diagnostic = new CompilerDiagnostic(
            "Tab test",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 2,
            FilePath: "test.spy",
            Code: "SPY0100");

        var result = _renderer.Render(diagnostic, sourceText);

        // Tabs should be expanded to spaces in the displayed source line
        result.Should().Contain("    if x > 0:");
        // The caret should be at the expanded position (column 2 = after 1 tab = position 4)
        result.Should().Contain("    ^");
    }

    [Fact]
    public void Render_TabsInSourceLine_SpanUnderlineAligned()
    {
        // Source: "\tx = \"bad\"" — tab before 'x', span on "bad" (including quotes)
        var source = "\tx = \"bad\"";
        var sourceText = new SourceText(source, "test.spy");
        // Span covers "bad" (chars 5..10 = the "bad" portion)
        var span = new TextSpan(5, 5);

        var diagnostic = new CompilerDiagnostic(
            "Span after tab",
            CompilerDiagnosticSeverity.Error,
            Code: "SPY0100",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        // Tab expanded: "    x = \"bad\""
        // Span starts at char offset 4 (after tab) in original = display column 8
        result.Should().Contain("    x = \"bad\"");
        result.Should().Contain("        ^^^^^");
    }

    [Fact]
    public void Render_MultipleDiagnostics_EachRenderedIndependently()
    {
        var source = "x = 1\ny = 'bad'\nz = true";
        var sourceText = new SourceText(source, "test.spy");

        var diag1 = new CompilerDiagnostic(
            "First error",
            CompilerDiagnosticSeverity.Error,
            Line: 1, Column: 1,
            FilePath: "test.spy",
            Code: "SPY0001");

        var diag2 = new CompilerDiagnostic(
            "Second error",
            CompilerDiagnosticSeverity.Error,
            Line: 2, Column: 5,
            FilePath: "test.spy",
            Code: "SPY0002");

        var result1 = _renderer.Render(diag1, sourceText);
        var result2 = _renderer.Render(diag2, sourceText);

        // Each should be independent and contain its own info
        result1.Should().Contain("First error");
        result1.Should().Contain("x = 1");
        result2.Should().Contain("Second error");
        result2.Should().Contain("y = 'bad'");
    }
}
