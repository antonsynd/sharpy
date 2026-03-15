using System.Text.Json;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.Analysis;

[Trait("Category", "GapDiscovery")]
public class HoverFuzzTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    private static readonly string FixturesPath = IOPath.GetFullPath(IOPath.Combine(
        IOPath.GetDirectoryName(typeof(HoverFuzzTests).Assembly.Location)!,
        "..", "..", "..", "..", "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    public HoverFuzzTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllFixtureIdentifiers_NoHoverCrashes()
    {
        var crashes = new List<CrashInfo>();
        var nullSymbol = new List<IdentifierInfo>();
        var nullType = new List<IdentifierInfo>();
        var unknownType = new List<IdentifierInfo>();

        int totalFixtures = 0;
        int analyzedFixtures = 0;
        int skippedFixtures = 0;
        int totalIdentifiers = 0;
        int identifiersWithFullInfo = 0;

        var fixtureFiles = Directory.GetFiles(FixturesPath, "*.spy", SearchOption.AllDirectories);

        foreach (var file in fixtureFiles)
        {
            var relativePath = IOPath.GetRelativePath(FixturesPath, file);
            var expectedFile = IOPath.ChangeExtension(file, ".expected");
            var errorFile = IOPath.ChangeExtension(file, ".error");
            var skipFile = IOPath.ChangeExtension(file, ".skip");

            // Only test positive fixtures (have .expected or no .error-only)
            if (File.Exists(skipFile))
            {
                skippedFixtures++;
                continue;
            }

            if (!File.Exists(expectedFile) && File.Exists(errorFile))
            {
                skippedFixtures++;
                continue;
            }

            totalFixtures++;

            var source = File.ReadAllText(file);
            SemanticResult result;
            try
            {
                result = _api.Analyze(source);
            }
            catch
            {
                skippedFixtures++;
                continue;
            }

            if (!result.Success || result.Ast == null || result.SemanticInfo == null)
            {
                skippedFixtures++;
                continue;
            }

            analyzedFixtures++;

            var positions = IdentifierPositionCollector.CollectPositions(result.Ast);
            foreach (var pos in positions)
            {
                totalIdentifiers++;

                Node? node;
                try
                {
                    node = _api.FindNodeAtPosition(result.Ast, pos.Line, pos.Column);
                }
                catch (Exception ex)
                {
                    crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "FindNodeAtPosition", ex.Message));
                    continue;
                }

                if (node == null)
                    continue;

                // Probe symbol resolution for Identifier nodes
                if (node is Identifier id)
                {
                    try
                    {
                        var symbol = result.SemanticInfo.GetIdentifierSymbol(id);
                        if (symbol == null)
                            nullSymbol.Add(new IdentifierInfo(relativePath, pos.Line, pos.Column, pos.Name));
                    }
                    catch (Exception ex)
                    {
                        crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "GetIdentifierSymbol", ex.Message));
                    }
                }

                // Probe type resolution for Expression nodes
                if (node is Expression expr)
                {
                    try
                    {
                        var type = result.SemanticInfo.GetEffectiveType(expr);
                        if (type == null)
                            nullType.Add(new IdentifierInfo(relativePath, pos.Line, pos.Column, pos.Name));
                        else if (type is UnknownType)
                            unknownType.Add(new IdentifierInfo(relativePath, pos.Line, pos.Column, pos.Name));
                        else
                            identifiersWithFullInfo++;
                    }
                    catch (Exception ex)
                    {
                        crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "GetEffectiveType", ex.Message));
                    }
                }
            }
        }

        var coveragePercent = totalIdentifiers > 0
            ? System.Math.Round(100.0 * identifiersWithFullInfo / totalIdentifiers, 1)
            : 0.0;

        var report = new
        {
            totalFixtures,
            analyzedFixtures,
            skippedFixtures,
            totalIdentifiers,
            crashes = crashes.Select(c => new { c.File, c.Line, c.Column, c.Name, c.Method, c.Error }),
            nullSymbol = nullSymbol.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            nullType = nullType.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            unknownType = unknownType.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            nullSymbolCount = nullSymbol.Count(),
            nullTypeCount = nullType.Count(),
            unknownTypeCount = unknownType.Count(),
            identifiersWithFullInfo,
            coveragePercent
        };

        var reportPath = IOPath.GetFullPath(IOPath.Combine(
            IOPath.GetDirectoryName(typeof(HoverFuzzTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "..", ".claude", "tmp", "hover-fuzz-report.json"));
        Directory.CreateDirectory(IOPath.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to {reportPath}");

        _output.WriteLine($"Fixtures: {analyzedFixtures} analyzed, {skippedFixtures} skipped");
        _output.WriteLine($"Identifiers: {totalIdentifiers} total, {identifiersWithFullInfo} with full info ({coveragePercent}%)");
        _output.WriteLine($"Gaps: {nullSymbol.Count()} null symbol, {nullType.Count()} null type, {unknownType.Count()} unknown type");
        _output.WriteLine($"Crashes: {crashes.Count()}");

        Assert.Empty(crashes);
    }

    private record CrashInfo(string File, int Line, int Column, string Name, string Method, string Error);
    private record IdentifierInfo(string File, int Line, int Column, string Name);
}
