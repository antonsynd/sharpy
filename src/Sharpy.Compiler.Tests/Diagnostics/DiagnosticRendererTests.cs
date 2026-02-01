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
            Code: "SHP0201",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("error[SHP0201]: Type 'str' is not assignable to 'int'");
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
            Code: "SHP0200");

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("error[SHP0200]: Undefined variable 'x'");
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
            Code: "SHP0450",
            Span: span);

        var result = _renderer.Render(diagnostic, sourceText);

        result.Should().Contain("warning[SHP0450]: Unreachable code detected");
        result.Should().Contain("--> test.spy:4:1");
        result.Should().Contain("z = 99");
    }

    [Fact]
    public void Render_NoLocationInfo_ShowsOnlyHeader()
    {
        var diagnostic = new CompilerDiagnostic(
            "Internal compiler error",
            CompilerDiagnosticSeverity.Error,
            Code: "SHP0599");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("error[SHP0599]: Internal compiler error");
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
            Code: "SHP0201",
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
            Code: "SHP0201",
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
            Code: "SHP0200");

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
            Code: "SHP0001");

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
            Code: "SHP0001");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("info[SHP0001]: Informational message");
    }

    [Fact]
    public void Render_HintSeverity_ShowsHintPrefix()
    {
        var diagnostic = new CompilerDiagnostic(
            "Consider using...",
            CompilerDiagnosticSeverity.Hint,
            Code: "SHP0001");

        var result = _renderer.Render(diagnostic);

        result.Should().Contain("hint[SHP0001]: Consider using...");
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
            Code: "SHP0200");

        var result = _renderer.Render(diagnostic, sourceText: null);

        result.Should().Contain("error[SHP0200]: No source available");
        result.Should().Contain("--> unknown.spy:5:3");
        // No source context since no source text
        result.Should().NotContain("|");
    }
}
