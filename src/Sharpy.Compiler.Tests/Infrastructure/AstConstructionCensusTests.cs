using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Census guard: every concrete AST kind must be constructed in <c>Parser/</c> or carry
/// a checked synthesized-by roster row. A kind nobody constructs is dead code that inflates
/// switch-dispatch obligations and hides missing arms behind default paths.
///
/// Universe: reflection over <c>typeof(Node).Assembly</c> — every concrete, non-abstract
/// public record whose namespace is <c>Sharpy.Compiler.Parser.Ast</c>.
///
/// Roster: kinds synthesized outside <c>Parser/</c> with a checked justification (the named
/// file must contain at least one construction site for that kind — phantom rows fail).
/// </summary>
public class AstConstructionCensusTests
{
    private readonly ITestOutputHelper _output;

    public AstConstructionCensusTests(ITestOutputHelper output) => _output = output;

    private static readonly Dictionary<string, string> SynthesizedOutsideParser = new()
    {
        ["BreakWithFlagStatement"] = "CodeGen/RoslynEmitter.Operators.cs",
    };

    private static HashSet<string> GetUniverse()
    {
        var assembly = typeof(Node).Assembly;

        return assembly.GetTypes()
            .Where(t => t.Namespace == "Sharpy.Compiler.Parser.Ast"
                     && t.IsPublic
                     && !t.IsAbstract
                     && t.IsClass
                     && t.GetMethod("<Clone>$") != null)
            .Select(t => t.Name)
            .ToHashSet();
    }

    private static AstConstructionScan.ScanResult ScanCompiler(
        Dictionary<string, string>? aliasOverrides = null)
    {
        return AstConstructionScan.Scan(
            "src/Sharpy.Compiler",
            "src/Sharpy.Compiler/Sharpy.Compiler.csproj",
            aliasOverrides: aliasOverrides);
    }

    private static AstConstructionScan.ScanResult ScanLsp()
    {
        return AstConstructionScan.Scan(
            "src/Sharpy.Lsp",
            "src/Sharpy.Lsp/Sharpy.Lsp.csproj");
    }

    private static AstConstructionScan.ScanResult ScanTests()
    {
        return AstConstructionScan.Scan(
            "src/Sharpy.Compiler.Tests",
            "src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj");
    }

    private static HashSet<string> ParserConstructedKinds(AstConstructionScan.ScanResult compilerScan)
    {
        return compilerScan.SitesByType
            .Where(kvp => kvp.Value.Any(s => s.File.StartsWith("Parser/")))
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void EveryConcreteAstKind_IsParserConstructedOrRostered()
    {
        var universe = GetUniverse();
        var compilerScan = ScanCompiler();
        var parserConstructed = ParserConstructedKinds(compilerScan);

        var unconstructed = new List<string>();
        foreach (var kind in universe.Order())
        {
            if (parserConstructed.Contains(kind))
                continue;
            if (SynthesizedOutsideParser.ContainsKey(kind))
                continue;
            unconstructed.Add(kind);
        }

        _output.WriteLine($"Universe: {universe.Count} concrete AST kinds");
        _output.WriteLine($"Parser-constructed: {parserConstructed.Count} kinds");
        _output.WriteLine($"Rostered (synthesized outside Parser/): {SynthesizedOutsideParser.Count} kinds");
        foreach (var kind in unconstructed)
            _output.WriteLine($"  UNCONSTRUCTED: {kind}");

        unconstructed.Should().BeEmpty(
            "every concrete AST kind must be constructed in Parser/ or be in the " +
            "SynthesizedOutsideParser roster with a checked justification");

        parserConstructed.Count.Should().BeGreaterThanOrEqualTo(100,
            "the scan must find at least 100 parser-constructed AST kinds — " +
            "a scan that binds nothing must not pass");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void RosterRows_AreCheckedBothDirections()
    {
        var universe = GetUniverse();
        var compilerScan = ScanCompiler();
        var parserConstructed = ParserConstructedKinds(compilerScan);

        foreach (var (kind, file) in SynthesizedOutsideParser)
        {
            universe.Should().Contain(kind,
                $"roster entry '{kind}' must name a real AST kind in the universe");

            parserConstructed.Should().NotContain(kind,
                $"roster entry '{kind}' must NOT already be parser-constructed — " +
                "if it is, the roster row is redundant and should be removed");

            compilerScan.SitesByType.Should().ContainKey(kind,
                $"roster entry '{kind}' must have construction sites in the scan");

            compilerScan.SitesByType[kind].Should().Contain(
                s => s.File == file,
                $"roster entry '{kind}' names file '{file}' which must contain " +
                "at least one construction site for that kind");
        }
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Generators_ConstructOnlyReachableKinds()
    {
        var compilerScan = ScanCompiler();
        var testScan = ScanTests();

        var parserConstructed = ParserConstructedKinds(compilerScan);
        var rostered = SynthesizedOutsideParser.Keys.ToHashSet();

        var reachable = new HashSet<string>(parserConstructed);
        reachable.UnionWith(rostered);

        var generatorKinds = testScan.SitesByType
            .Where(kvp => kvp.Value.Any(s => s.File.StartsWith("Properties/Generators/")))
            .Select(kvp => kvp.Key)
            .ToHashSet();

        var unreachable = generatorKinds.Except(reachable).OrderBy(k => k).ToList();

        _output.WriteLine($"Generator-constructed kinds: {generatorKinds.Count}");
        foreach (var k in unreachable)
            _output.WriteLine($"  UNREACHABLE: {k}");

        unreachable.Should().BeEmpty(
            "generators must only construct AST kinds that are parser-constructed or rostered — " +
            "a generator constructing an unreachable kind produces tests for dead AST variants");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void InstrumentHealth_UnresolvedIsZero()
    {
        var compilerScan = ScanCompiler();
        var lspScan = ScanLsp();

        _output.WriteLine($"Compiler: {compilerScan.TotalCreationCount} creations, " +
            $"{compilerScan.UnresolvedCount} unresolved, " +
            $"{compilerScan.SitesByType.Count} AST kinds found");
        _output.WriteLine($"LSP: {lspScan.TotalCreationCount} creations, " +
            $"{lspScan.UnresolvedCount} unresolved, " +
            $"{lspScan.SitesByType.Count} AST kinds found");

        compilerScan.UnresolvedCount.Should().Be(0,
            "all creation expressions in the compiler must resolve — " +
            "unresolved creations mean the compilation is missing references or aliases");
        lspScan.UnresolvedCount.Should().Be(0,
            "all creation expressions in the LSP must resolve");
    }

    /// <summary>
    /// Mutation (c): dropping the SharpyRT alias makes types referenced via extern alias
    /// unresolvable, so the scan reports unresolved creations.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void DroppingSharpyRtAlias_IncreasesUnresolved()
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var aliases = DispatchSiteScan.ReadAliasMap(
            Path.Combine(repoRoot, "src/Sharpy.Compiler/Sharpy.Compiler.csproj"));
        aliases.Remove("Sharpy.Core");

        var result = ScanCompiler(aliasOverrides: aliases);

        result.UnresolvedCount.Should().BeGreaterThan(0,
            "dropping the SharpyRT alias must produce unresolved creations — " +
            "this proves the alias is load-bearing for the construction census");

        _output.WriteLine($"Without SharpyRT: {result.UnresolvedCount} unresolved " +
            $"(from {result.TotalCreationCount} total creations)");
    }

    /// <summary>
    /// Universe sanity: the reflection universe contains known concrete kinds and excludes the
    /// abstract bases. This is a positive control for the universe, NOT the falsification of the
    /// census — that is the manual mutation the contract requires (§2): add
    /// <c>public record PhantomPattern : Pattern;</c> to <c>Parser/Ast/Pattern.cs</c>, rebuild so the
    /// universe sees it → <see cref="EveryConcreteAstKind_IsParserConstructedOrRostered"/> goes red
    /// naming it (recorded red/green in the commit body); delete the <c>BreakWithFlagStatement</c>
    /// roster row → red naming it.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Universe_ContainsKnownTypes_ExcludesAbstract()
    {
        var universe = GetUniverse();

        var knownTypes = new[]
        {
            "Module", "Identifier", "FunctionDef", "IfStatement", "BinaryOp",
            "TypeAnnotation", "FunctionType", "ElifClause", "Parameter", "Decorator",
            "WildcardPattern", "BreakWithFlagStatement", "MatchStatement",
        };
        foreach (var known in knownTypes)
            universe.Should().Contain(known,
                $"'{known}' is a well-known AST kind and must be in the universe");

        var abstractTypes = new[]
        {
            "Node", "Statement", "Expression", "Pattern",
            "ComprehensionClause", "ConstraintClause",
        };
        foreach (var abs in abstractTypes)
            universe.Should().NotContain(abs,
                $"'{abs}' is abstract and must not be in the universe");
    }

    /// <summary>
    /// Mutation (d) positive control: generators DO construct a known subset of
    /// parser-constructed kinds — if this fails, the generator scan is broken.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Generators_ConstructKnownParserKinds()
    {
        var testScan = ScanTests();

        var generatorKinds = testScan.SitesByType
            .Where(kvp => kvp.Value.Any(s => s.File.StartsWith("Properties/Generators/")))
            .Select(kvp => kvp.Key)
            .ToHashSet();

        _output.WriteLine($"Generator-constructed kinds ({generatorKinds.Count}): " +
            string.Join(", ", generatorKinds.Order()));

        var expectedSubset = new[] { "Identifier", "IntegerLiteral", "FunctionDef", "BinaryOp" };
        foreach (var expected in expectedSubset)
        {
            generatorKinds.Should().Contain(expected,
                $"generators must construct '{expected}' — " +
                "positive control for the generator reachability check");
        }
    }
}
