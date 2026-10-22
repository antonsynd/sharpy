using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// The LSP route of the <c>--max-errors</c> matrix (#2311, ruling R-HD a): the editor reports at
/// most the cap's errors, plus at most one SPY0905 note, on the analysis it publishes
/// (<see cref="SharpyWorkspace.GetAnalysisAsync"/>) and on the parse the syntax handlers read
/// (<see cref="SharpyWorkspace.GetParseResultAsync"/>).
/// </summary>
/// <remarks>
/// The LSP has no cap option: its options come from <c>CompilerOptionsFactory.ForLsp</c>, which no
/// LSP configuration reaches with a <c>maxErrors</c>, so only each phase's default (parser 25,
/// semantic 100) is reachable and the N axis is that one value here. The CLI routes ×
/// N {1, 2, default} are <c>Sharpy.Cli.Tests/Commands/MaxErrorsCapMatrixTests</c>, whose shape axis
/// this mirrors (enum and union bodies for the parser phase only, as there). Each program has the
/// default + 2 errors; every parser row but the statement one reported 26 at e7e0a0ef7 — the suite
/// left at the cap reported its definition's <c>Expected Dedent</c> (SPY0104).
/// </remarks>
public sealed class MaxErrorsCapLspRouteTests : IDisposable
{
    private const int ParserDefault = 25;
    private const int SemanticDefault = 100;

    private static readonly string[] SharedShapes = { "statement", "def", "class", "nested" };
    private static readonly string[] ParserOnlyShapes = { "enum", "union" };

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "sharpy_lsp_maxerrors_" + Guid.NewGuid().ToString("N"));

    public MaxErrorsCapLspRouteTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    public static IEnumerable<object[]> Cells()
    {
        foreach (var phase in new[] { "parser", "semantic" })
            foreach (var shape in phase == "parser" ? SharedShapes.Concat(ParserOnlyShapes) : SharedShapes)
                foreach (var route in new[] { "analysis", "parse" })
                {
                    if (phase == "semantic" && route == "parse")
                        continue; // The parse route runs no semantic phase.
                    yield return new object[] { phase, shape, route };
                }
    }

    // Anchored to a literal: parser 6 shapes × 2 routes + semantic 4 shapes × the analysis route.
    [Fact]
    public void Matrix_HasEveryCell() => Cells().Should().HaveCount(16);

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task Lsp_ReportsAtMostTheDefaultCapErrors(string phase, string shape, string route)
    {
        var cap = phase == "parser" ? ParserDefault : SemanticDefault;
        var path = Path.Combine(_dir, $"{phase}_{shape}.spy");
        var source = Program(phase, shape, cap + 2);
        File.WriteAllText(path, source);
        var uri = new Uri(path).ToString();

        using var workspace = new SharpyWorkspace(new CompilerApi(), NullLogger<SharpyWorkspace>.Instance);
        workspace.OpenDocument(uri, source, 1);
        var diagnostics = route == "analysis"
            ? (await workspace.GetAnalysisAsync(uri))!.Diagnostics
            : (await workspace.GetParseResultAsync(uri))!.Diagnostics;

        var rendered = string.Join("\n", diagnostics.Select(d => $"{d.Severity} {d.Code} {d.Line}:{d.Column} {d.Message}"));
        diagnostics.Count(d => d.IsError).Should().Be(cap,
            $"the {route} route must report exactly the default cap's {cap} errors of the {cap + 2}+:\n{rendered}");
        diagnostics.Count(d => d.Code == DiagnosticCodes.Infrastructure.TooManyErrors).Should().BeLessThanOrEqualTo(1,
            $"the cap is noted at most once:\n{rendered}");
    }

    /// <summary>The CLI matrix's program generator, verbatim: one error per item of the shape, plus a <c>main</c>.</summary>
    private static string Program(string phase, string shape, int count)
    {
        var sb = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            var bad = phase == "parser" ? $"x{i} = = {i}" : $"x{i}: int = undefined_{i}";
            sb.Append(shape switch
            {
                "statement" => $"{bad}\n",
                "def" => $"def f{i}() -> None:\n    {bad}\n\n",
                "class" => $"class C{i}:\n    {bad}\n\n",
                "nested" => $"def f{i}() -> None:\n    if True:\n        {bad}\n\n",
                "enum" => $"enum E{i}:\n    A = = {i}\n\n",
                "union" => $"union U{i}:\n    case A(x: = int)\n\n",
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
            });
        }
        sb.Append("\ndef main() -> None:\n    pass\n");
        return sb.ToString();
    }
}
