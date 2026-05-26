using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;
using Sharpy.Lsp;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests;

public class PositionConverterTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(5, 10, 6, 11)]
    [InlineData(0, 5, 1, 6)]
    public void ToCompiler_Converts0BasedTo1Based(int lspLine, int lspChar, int expectedLine, int expectedCol)
    {
        var position = new Position(lspLine, lspChar);
        var (line, col) = PositionConverter.ToCompiler(position);

        line.Should().Be(expectedLine);
        col.Should().Be(expectedCol);
    }

    [Theory]
    [InlineData(1, 1, 0, 0)]
    [InlineData(6, 11, 5, 10)]
    [InlineData(1, 6, 0, 5)]
    public void ToLsp_Converts1BasedTo0Based(int compilerLine, int compilerCol, int expectedLine, int expectedChar)
    {
        var position = PositionConverter.ToLsp(compilerLine, compilerCol);

        position.Line.Should().Be(expectedLine);
        position.Character.Should().Be(expectedChar);
    }

    [Fact]
    public void ToLsp_ClampsNegativeValues()
    {
        var position = PositionConverter.ToLsp(0, 0);

        position.Line.Should().Be(0);
        position.Character.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_LspToCompilerAndBack()
    {
        var original = new Position(3, 7);
        var (line, col) = PositionConverter.ToCompiler(original);
        var result = PositionConverter.ToLsp(line, col);

        result.Line.Should().Be(original.Line);
        result.Character.Should().Be(original.Character);
    }

    // --- ToLspRange tests ---

    [Fact]
    public void ToLspRange_SingleLineSpan()
    {
        var source = new SourceText("hello world\n");
        var span = new TextSpan(6, 5); // "world"

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(6);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(11);
    }

    [Fact]
    public void ToLspRange_MultiLineSpan()
    {
        var source = new SourceText("line1\nline2\nline3\n");
        // Span from start of line2 (offset 6) to end of line3 content (offset 17)
        var span = new TextSpan(6, 11);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(1); // line2 is 0-based line 1
        range.Start.Character.Should().Be(0);
        range.End.Line.Should().Be(2); // line3 is 0-based line 2
        range.End.Character.Should().Be(5);
    }

    [Fact]
    public void ToLspRange_ZeroLengthSpan()
    {
        var source = new SourceText("hello\n");
        var span = new TextSpan(3, 0); // cursor position in "hello"

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(3);
        range.End.Should().Be(range.Start);
    }

    [Fact]
    public void ToLspRange_SingleCharacterSpan()
    {
        var source = new SourceText("abc\n");
        var span = new TextSpan(1, 1); // "b"

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(1);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(2);
    }

    [Fact]
    public void ToLspRange_SpanAtEndOfFile()
    {
        var source = new SourceText("abc");
        var span = new TextSpan(3, 0); // EOF position

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(3);
    }

    [Fact]
    public void ToLspRange_SpanExceedingLength_ClampedToEnd()
    {
        var source = new SourceText("abc");
        var span = new TextSpan(1, 100); // Length exceeds source

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(1);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(3); // clamped to source length
    }

    // --- DiagnosticToRange tests ---

    [Fact]
    public void DiagnosticToRange_WithSpanAndSourceText()
    {
        var source = new SourceText("hello world\n");
        var span = new TextSpan(6, 5); // "world"
        var diagnostic = new CompilerDiagnostic("test", CompilerDiagnosticSeverity.Error, Span: span);

        var range = PositionConverter.DiagnosticToRange(diagnostic, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(6);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(11);
    }

    [Fact]
    public void DiagnosticToRange_WithLineColumnFallback()
    {
        var diagnostic = new CompilerDiagnostic("test", CompilerDiagnosticSeverity.Error, Line: 5, Column: 10);

        var range = PositionConverter.DiagnosticToRange(diagnostic, sourceText: null);

        range.Start.Line.Should().Be(4); // 5-1 = 4 (0-based)
        range.Start.Character.Should().Be(9); // 10-1 = 9
        range.End.Should().Be(range.Start); // zero-width
    }

    [Fact]
    public void DiagnosticToRange_NoSpanNoSourceText_UsesLineColumn()
    {
        var diagnostic = new CompilerDiagnostic("test", CompilerDiagnosticSeverity.Warning, Line: 3, Column: 1);

        var range = PositionConverter.DiagnosticToRange(diagnostic, sourceText: null);

        range.Start.Line.Should().Be(2);
        range.Start.Character.Should().Be(0);
    }

    [Fact]
    public void DiagnosticToRange_NullLineColumn_DefaultsToOrigin()
    {
        var diagnostic = new CompilerDiagnostic("test", CompilerDiagnosticSeverity.Error);

        var range = PositionConverter.DiagnosticToRange(diagnostic, sourceText: null);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(0);
    }

    [Fact]
    public void DiagnosticToRange_HasSpanButNullSourceText_FallsBackToLineColumn()
    {
        var span = new TextSpan(10, 5);
        var diagnostic = new CompilerDiagnostic("test", CompilerDiagnosticSeverity.Error, Line: 2, Column: 3, Span: span);

        var range = PositionConverter.DiagnosticToRange(diagnostic, sourceText: null);

        range.Start.Line.Should().Be(1); // fallback to Line/Column
        range.Start.Character.Should().Be(2);
    }

    // --- Boundary tests ---

    [Fact]
    public void ToLspRange_EmptyFile()
    {
        var source = new SourceText("");
        var span = new TextSpan(0, 0);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(0);
        range.End.Should().Be(range.Start);
    }

    [Fact]
    public void ToLspRange_SingleCharacterFile()
    {
        var source = new SourceText("x");
        var span = new TextSpan(0, 1);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(0);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(1);
    }

    [Fact]
    public void ToLspRange_FileWithOnlyNewlines()
    {
        var source = new SourceText("\n\n\n");
        var span = new TextSpan(1, 1); // second newline

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(1); // after first \n
        range.Start.Character.Should().Be(0);
    }

    // --- Line ending tests ---

    [Fact]
    public void ToLspRange_CrlfLineEndings()
    {
        var source = new SourceText("ab\r\ncd\r\nef");
        // "cd" starts at offset 4, length 2
        var span = new TextSpan(4, 2);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(1);
        range.Start.Character.Should().Be(0);
        range.End.Line.Should().Be(1);
        range.End.Character.Should().Be(2);
    }

    [Fact]
    public void ToLspRange_MixedLineEndings()
    {
        var source = new SourceText("a\nb\r\nc");
        // "c" starts at offset 5, length 1
        var span = new TextSpan(5, 1);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(2);
        range.Start.Character.Should().Be(0);
        range.End.Line.Should().Be(2);
        range.End.Character.Should().Be(1);
    }

    // --- Encoding tests ---

    [Fact]
    public void ToLspRange_MultiByteUtf8_CjkCharacters()
    {
        // CJK characters are single .NET chars (BMP: U+4E00-U+9FFF)
        var source = new SourceText("你好世界\n");
        var span = new TextSpan(2, 2); // "世界"

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(2);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(4);
    }

    [Fact]
    public void ToLspRange_SurrogatePairs_Emoji()
    {
        // 😀 (U+1F600) is a surrogate pair = 2 .NET chars
        var source = new SourceText("a😀b\n");
        // "b" starts at offset 3 (a=0, 😀=1-2, b=3), length 1
        var span = new TextSpan(3, 1);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(3); // .NET string position
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(4);
    }

    // --- BOM test ---

    [Fact]
    public void ToLspRange_FileWithBom()
    {
        // UTF-8 BOM (U+FEFF) is a single .NET char at offset 0
        var source = new SourceText("﻿abc\n");
        // "bc" starts at offset 2 (BOM=0, a=1, b=2), length 2
        var span = new TextSpan(2, 2);

        var range = PositionConverter.ToLspRange(span, source);

        range.Start.Line.Should().Be(0);
        range.Start.Character.Should().Be(2);
        range.End.Line.Should().Be(0);
        range.End.Character.Should().Be(4);
    }

    // --- Additional edge cases ---

    [Fact]
    public void ToLsp_LargeLineNumbers()
    {
        var position = PositionConverter.ToLsp(10000, 500);

        position.Line.Should().Be(9999);
        position.Character.Should().Be(499);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(100, 50)]
    [InlineData(1, 100)]
    public void RoundTrip_CompilerToLspAndBack(int compilerLine, int compilerCol)
    {
        var lspPos = PositionConverter.ToLsp(compilerLine, compilerCol);
        var (line, col) = PositionConverter.ToCompiler(lspPos);

        line.Should().Be(compilerLine);
        col.Should().Be(compilerCol);
    }
}
