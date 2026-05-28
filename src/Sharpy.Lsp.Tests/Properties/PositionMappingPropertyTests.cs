using System;
using System.Linq;
using CsCheck;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Lsp.Tests.Properties;

[Trait("Category", "Property")]
public class PositionMappingPropertyTests
{
    private readonly ITestOutputHelper _output;

    public PositionMappingPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ToCompiler_ToLsp_RoundTrip()
    {
        // For positive compiler coordinates, ToCompiler(ToLsp(line, col)) == (line, col)
        Gen.Select(Gen.Int[1, 10000], Gen.Int[1, 500]).Sample((line, col) =>
        {
            var lsp = PositionConverter.ToLsp(line, col);
            var (rtLine, rtCol) = PositionConverter.ToCompiler(lsp);
            if (rtLine != line || rtCol != col)
                throw new Exception(
                    $"Round-trip failed: ({line}, {col}) -> LSP({lsp.Line}, {lsp.Character}) -> ({rtLine}, {rtCol})");
        }, iter: 200);
    }

    [Fact]
    public void ToLsp_ToCompiler_ToLsp_IsIdempotent()
    {
        // ToLsp(ToCompiler(pos)) == pos for valid (non-negative) LSP positions
        Gen.Select(Gen.Int[0, 9999], Gen.Int[0, 499]).Sample((line, character) =>
        {
            var pos = new Position(line, character);
            var (compLine, compCol) = PositionConverter.ToCompiler(pos);
            var roundTripped = PositionConverter.ToLsp(compLine, compCol);
            if (roundTripped.Line != pos.Line || roundTripped.Character != pos.Character)
                throw new Exception(
                    $"Idempotence failed: LSP({line}, {character}) -> ({compLine}, {compCol}) -> LSP({roundTripped.Line}, {roundTripped.Character})");
        }, iter: 200);
    }

    [Fact]
    public void ToLsp_ClampsToZero()
    {
        // ToLsp with zero or negative compiler coords should clamp to 0
        Gen.Select(Gen.Int[-10, 0], Gen.Int[-10, 0]).Sample((line, col) =>
        {
            var pos = PositionConverter.ToLsp(line, col);
            if (pos.Line < 0 || pos.Character < 0)
                throw new Exception(
                    $"ToLsp({line}, {col}) produced negative: LSP({pos.Line}, {pos.Character})");
        }, iter: 100);
    }

    [Fact]
    public void ToLspRange_ProducesValidRange()
    {
        // Generate source text and valid spans, verify the resulting range is well-formed.
        Gen.Select(Gen.Int[1, 50], Gen.Int[1, 80]).SelectMany((lines, cols) =>
        {
            // Build a source text with known dimensions.
            var text = string.Join("\n", Enumerable.Range(0, lines).Select(_ => new string('x', cols)));
            return Gen.Select(Gen.Int[0, text.Length - 1], Gen.Int[0, text.Length - 1])
                .Select((a, b) => (text, start: Math.Min(a, b), length: Math.Abs(a - b)));
        }).Sample(tuple =>
        {
            var (text, start, length) = tuple;
            if (start + length > text.Length)
                return; // skip invalid spans

            var sourceText = new SourceText(text);
            var span = new TextSpan(start, length);
            var range = PositionConverter.ToLspRange(span, sourceText);

            if (range.Start.Line < 0 || range.Start.Character < 0 ||
                range.End.Line < 0 || range.End.Character < 0)
                throw new Exception($"Range has negative values: {range}");

            if (range.Start.Line > range.End.Line ||
                (range.Start.Line == range.End.Line && range.Start.Character > range.End.Character))
                throw new Exception($"Range start is after end: {range}");
        }, iter: 200);
    }
}
