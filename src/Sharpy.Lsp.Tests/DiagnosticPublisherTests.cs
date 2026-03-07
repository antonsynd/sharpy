using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class DiagnosticPublisherTests
{
    [Theory]
    [InlineData(CompilerDiagnosticSeverity.Error, DiagnosticSeverity.Error)]
    [InlineData(CompilerDiagnosticSeverity.Warning, DiagnosticSeverity.Warning)]
    [InlineData(CompilerDiagnosticSeverity.Info, DiagnosticSeverity.Information)]
    [InlineData(CompilerDiagnosticSeverity.Hint, DiagnosticSeverity.Hint)]
    public void ConvertSeverity_MapsAllLevels(CompilerDiagnosticSeverity input, DiagnosticSeverity expected)
    {
        DiagnosticPublisher.ConvertSeverity(input).Should().Be(expected);
    }

    [Fact]
    public void ConvertDiagnostic_SetsMessageAndSource()
    {
        var diag = new CompilerDiagnostic(
            Message: "unexpected token",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 5,
            Column: 10,
            Code: "SPY0100"
        );

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        result.Message.Should().Be("unexpected token");
        result.Source.Should().Be("sharpy");
    }

    [Fact]
    public void ConvertDiagnostic_ConvertsPositionTo0Based()
    {
        var diag = new CompilerDiagnostic(
            Message: "error",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 3,
            Column: 7
        );

        var result = DiagnosticPublisher.ConvertDiagnostic(diag, null);

        // Line 3, Column 7 (1-based) → Line 2, Character 6 (0-based)
        result.Range.Start.Line.Should().Be(2);
        result.Range.Start.Character.Should().Be(6);
    }

    [Fact]
    public void ConvertDiagnostics_HandlesEmptyList()
    {
        var result = DiagnosticPublisher.ConvertDiagnostics(
            Array.Empty<CompilerDiagnostic>(), null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ConvertDiagnostics_ConvertsMultiple()
    {
        var diagnostics = new[]
        {
            new CompilerDiagnostic("err1", CompilerDiagnosticSeverity.Error, Line: 1, Column: 1),
            new CompilerDiagnostic("warn1", CompilerDiagnosticSeverity.Warning, Line: 2, Column: 5),
        };

        var result = DiagnosticPublisher.ConvertDiagnostics(diagnostics, null);

        result.Should().HaveCount(2);
        result[0].Severity.Should().Be(DiagnosticSeverity.Error);
        result[1].Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
