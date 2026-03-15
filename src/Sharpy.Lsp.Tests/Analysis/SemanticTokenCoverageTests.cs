using System.Text.Json;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp.Handlers;
using Xunit;
using Xunit.Abstractions;
using static Sharpy.Lsp.Handlers.SharpySemanticTokensHandler;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.Analysis;

[Trait("Category", "GapDiscovery")]
public class SemanticTokenCoverageTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    private static readonly string FixturesPath = IOPath.GetFullPath(
        IOPath.Combine(
            IOPath.GetDirectoryName(typeof(SemanticTokenCoverageTests).Assembly.Location)!,
            "..", "..", "..", "..", "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    private static readonly string[] TokenTypeNames =
    [
        "TFunction", "TClass", "TStruct", "TInterface", "TEnum",
        "TEnumMember", "TParameter", "TVariable", "TDecorator",
        "TType", "TProperty", "TMethod", "TKeyword"
    ];

    public SemanticTokenCoverageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SemanticTokenCoverage_AllFixtures_NoCrashes()
    {
        Assert.True(Directory.Exists(FixturesPath),
            $"TestFixtures directory not found at {FixturesPath}");

        var spyFiles = Directory.GetFiles(FixturesPath, "*.spy", SearchOption.AllDirectories)
            .Where(f => !File.Exists(IOPath.ChangeExtension(f, ".skip")))
            .OrderBy(f => f)
            .ToList();

        Assert.True(spyFiles.Count > 0, "No .spy files found");

        var crashes = new List<string>();
        var parseFailures = 0;
        var tokenTypeUsage = new Dictionary<string, int>();
        var allAstNodeTypes = new Dictionary<string, int>();
        var astNodeTypesWithTokens = new HashSet<string>();
        var lowCoverageFiles = new List<(string file, double coverage)>();
        var totalIdentifiers = 0;
        var totalIdentifiersWithTokens = 0;

        foreach (var file in spyFiles)
        {
            var relativePath = IOPath.GetRelativePath(FixturesPath, file);
            var source = File.ReadAllText(file);

            ParseResult parseResult;
            try
            {
                parseResult = _api.Parse(source);
            }
            catch (Exception ex)
            {
                crashes.Add($"PARSE CRASH: {relativePath}: {ex.Message}");
                continue;
            }

            if (!parseResult.Success || parseResult.Ast == null)
            {
                parseFailures++;
                continue;
            }

            // Collect AST node types
            var nodeTypes = AstNodeTypeCollector.CollectNodeTypes(parseResult.Ast);
            foreach (var (nodeType, count) in nodeTypes)
            {
                if (allAstNodeTypes.TryGetValue(nodeType, out var existing))
                    allAstNodeTypes[nodeType] = existing + count;
                else
                    allAstNodeTypes[nodeType] = count;
            }

            // Collect semantic tokens
            var tokens = new List<RawToken>();
            try
            {
                CollectTokens(parseResult.Ast.Body, tokens);
            }
            catch (Exception ex)
            {
                crashes.Add($"TOKEN CRASH: {relativePath}: {ex.Message}");
                continue;
            }

            // Track token type usage
            foreach (var token in tokens)
            {
                var typeName = token.TokenType < TokenTypeNames.Length
                    ? TokenTypeNames[token.TokenType]
                    : $"Unknown({token.TokenType})";
                if (tokenTypeUsage.TryGetValue(typeName, out var c))
                    tokenTypeUsage[typeName] = c + 1;
                else
                    tokenTypeUsage[typeName] = 1;
            }

            // Calculate identifier coverage for this file
            var identifierCount = nodeTypes.GetValueOrDefault("Identifier");
            if (identifierCount > 0)
            {
                // Count tokens that cover identifier-like positions
                // A rough proxy: count distinct (line, col) positions in tokens
                var tokenPositions = new HashSet<(int, int)>();
                foreach (var token in tokens)
                    tokenPositions.Add((token.Line, token.Col));

                // Count identifiers that have a token at their position
                var identifiersWithTokens = CountIdentifiersWithTokens(
                    parseResult.Ast, tokenPositions);

                totalIdentifiers += identifierCount;
                totalIdentifiersWithTokens += identifiersWithTokens;

                var coverage = (double)identifiersWithTokens / identifierCount;
                if (coverage < 0.3)
                {
                    lowCoverageFiles.Add((relativePath, coverage));
                }
            }

            // Track which AST node types got tokens
            if (tokens.Count > 0)
            {
                foreach (var nodeType in nodeTypes.Keys)
                    astNodeTypesWithTokens.Add(nodeType);
            }
        }

        // Build unused token types list
        var unusedTokenTypes = TokenTypeNames
            .Where(t => !tokenTypeUsage.ContainsKey(t))
            .ToList();

        // Build AST node types that never appeared in files that had tokens
        var astNodeTypesWithNoTokens = allAstNodeTypes.Keys
            .Where(k => !astNodeTypesWithTokens.Contains(k))
            .OrderBy(k => k)
            .ToList();

        var avgIdentifierCoverage = totalIdentifiers > 0
            ? (double)totalIdentifiersWithTokens / totalIdentifiers
            : 0.0;

        // Build report
        var report = new
        {
            totalFixtures = spyFiles.Count,
            crashes,
            parseFailures,
            tokenTypeUsage = tokenTypeUsage.OrderByDescending(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            unusedTokenTypes,
            astNodeTypesWithNoTokens,
            lowCoverageFiles = lowCoverageFiles
                .OrderBy(f => f.coverage)
                .Take(20)
                .Select(f => new { file = f.file, identifierCoverage = System.Math.Round(f.coverage, 3) })
                .ToList(),
            averageIdentifierCoverage = System.Math.Round(avgIdentifierCoverage, 4)
        };

        // Write report
        var reportDir = IOPath.GetFullPath(IOPath.Combine(
            IOPath.GetDirectoryName(typeof(SemanticTokenCoverageTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "..", ".claude", "tmp"));
        Directory.CreateDirectory(reportDir);
        var reportPath = IOPath.Combine(reportDir, "semantic-token-coverage-report.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(reportPath, json);

        // Output summary
        _output.WriteLine($"Total fixtures: {spyFiles.Count}");
        _output.WriteLine($"Parse failures: {parseFailures}");
        _output.WriteLine($"Crashes: {crashes.Count}");
        _output.WriteLine($"Average identifier coverage: {avgIdentifierCoverage:P1}");
        _output.WriteLine($"Unused token types: {string.Join(", ", unusedTokenTypes)}");
        _output.WriteLine($"Report written to: {reportPath}");

        foreach (var (file, coverage) in lowCoverageFiles.OrderBy(f => f.coverage).Take(10))
        {
            _output.WriteLine($"  Low coverage: {file} ({coverage:P0})");
        }

        // Hard assertion: no crashes
        Assert.Empty(crashes);
    }

    private static int CountIdentifiersWithTokens(Module module, HashSet<(int, int)> tokenPositions)
    {
        var count = 0;
        CountIdentifiersRecursive(module, tokenPositions, ref count);
        return count;
    }

    private static void CountIdentifiersRecursive(
        Node node,
        HashSet<(int, int)> tokenPositions,
        ref int count)
    {
        if (node is Identifier id)
        {
            // Tokens use 0-based positions, AST uses 1-based
            if (tokenPositions.Contains((id.LineStart - 1, id.ColumnStart - 1)))
                count++;
        }

        foreach (var child in node.GetChildNodes())
        {
            CountIdentifiersRecursive(child, tokenPositions, ref count);
        }
    }
}
