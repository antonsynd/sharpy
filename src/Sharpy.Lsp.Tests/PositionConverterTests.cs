using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Lsp;
using Xunit;

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
}
