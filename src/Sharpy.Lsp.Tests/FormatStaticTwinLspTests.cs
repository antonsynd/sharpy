using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// The #1956 static twin and the #1955 steer on the path the language server uses: a
/// <see cref="SharpyWorkspace"/> built over a server-style <see cref="CompilerApi"/> (Core + Stdlib
/// referenced, as <c>Program.cs</c> does), on first analysis AND on the re-analysis an edit
/// triggers. The facts they read — <c>ParameterSymbol.FormatSpecOf</c> (overload-index cache) and
/// <c>FunctionSymbol.IsFormatTemplateReceiver</c>/<c>KeywordSteer</c> (read from live reflection by
/// every <c>BuiltinRegistry</c>) — must reach this front door exactly as they reach the CLI.
/// </summary>
public class FormatStaticTwinLspTests
{
    private const string Steer =
        "str.format takes positional fields only; use an f-string (f\"{...}\") or format_map({...})";

    private const string Source =
        "def main() -> None:\n"
        + "    s: str = \"ab\"\n"
        + "    print(\"{:=5}\".format(s))\n"
        + "    print(format(s, \"=5\"))\n"
        + "    print(\"{name}\".format(name=1))\n";

    private static string[] ServerDefaultReferences()
    {
        var baseDir = AppContext.BaseDirectory;
        var corePath = IOPath.Combine(baseDir, "Sharpy.Core.dll");
        var stdlibPath = IOPath.Combine(baseDir, "Sharpy.Stdlib.dll");
        Assert.True(File.Exists(corePath), $"Sharpy.Core.dll not found next to the test assembly: {corePath}");
        Assert.True(File.Exists(stdlibPath), $"Sharpy.Stdlib.dll not found next to the test assembly: {stdlibPath}");
        return new[] { corePath, stdlibPath };
    }

    [Fact]
    public async Task LiteralSpecsAndKeywordSteer_ReachTheLanguageServer_OnFirstAndWarmAnalysis_1956_1955()
    {
        using var workspace = new SharpyWorkspace(
            new CompilerApi(null, ServerDefaultReferences()), NullLogger<SharpyWorkspace>.Instance);
        const string uri = "file:///format_static_twin_lsp.spy";

        workspace.OpenDocument(uri, Source, 1);
        AssertFacts(await workspace.GetAnalysisAsync(uri, CancellationToken.None), "first analysis");

        // An edit re-analyzes the document through the workspace's warm path.
        workspace.UpdateDocument(uri, Source + "\n", 2);
        AssertFacts(await workspace.GetAnalysisAsync(uri, CancellationToken.None), "re-analysis");
    }

    private static void AssertFacts(SemanticResult? analysis, string pass)
    {
        Assert.NotNull(analysis);
        var all = string.Join("; ", analysis!.Diagnostics.Select(d => $"{d.Code}@{d.Span?.Start}: {d.Message}"));

        var spy0609 = analysis.Diagnostics
            .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
            .Select(d => d.Span?.Start)
            .OrderBy(s => s)
            .ToList();
        // One at the str.format template literal, one at the format() spec literal.
        var expected = new int?[]
        {
            Source.IndexOf("\"{:=5}\"", StringComparison.Ordinal),
            Source.IndexOf("\"=5\"", StringComparison.Ordinal),
        }.OrderBy(s => s).ToList();
        Assert.True(expected.SequenceEqual(spy0609), $"{pass}: expected SPY0609 at {string.Join(",", expected)}, got: {all}");

        Assert.True(
            analysis.Diagnostics.Count(d => d.Code == DiagnosticCodes.Semantic.UnknownKeywordArgument
                && d.Message.EndsWith(Steer, StringComparison.Ordinal)) == 1,
            $"{pass}: expected one steered SPY0234, got: {all}");
    }
}
