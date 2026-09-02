using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Self-tests for <see cref="DispatchSiteScan"/>: the typed-scrutinee census that replaces
/// the name-scoped identifier scan. Each fact verifies a property of the scan instrument
/// itself, not a property of the codebase it scans.
///
/// Recorded mutations (Phase 0 acceptance):
/// (a) Drop SharpyRT alias from the alias map → unresolved > 0 (sites that reference
///     SharpyRT types lose resolution).
/// (b) Suppress synthesized global usings → unresolved > 0 (types from implicit usings
///     like System.Collections.Generic become unresolved).
/// (c) Remove IrNode from the root set → IrTreeRewriter.RewriteNode vanishes from sites.
/// (d) Positive: CollectBindingKeysInto (non-identifier scrutinee "target") is found.
///     Negative: Lexer switch (threeChar) is NOT found.
/// </summary>
public class DispatchSiteScanTests
{
    private readonly ITestOutputHelper _output;

    public DispatchSiteScanTests(ITestOutputHelper output) => _output = output;

    private static DispatchSiteScan.ScanResult ScanCompiler(
        Dictionary<string, string>? aliasOverrides = null,
        bool suppressGlobalUsings = false,
        bool includeIrNodeRoot = true)
    {
        return DispatchSiteScan.Scan(
            "src/Sharpy.Compiler",
            "src/Sharpy.Compiler/Sharpy.Compiler.csproj",
            keyPrefix: null,
            aliasOverrides: aliasOverrides,
            suppressGlobalUsings: suppressGlobalUsings,
            includeIrNodeRoot: includeIrNodeRoot);
    }

    private static DispatchSiteScan.ScanResult ScanLsp(
        Dictionary<string, string>? aliasOverrides = null,
        bool suppressGlobalUsings = false)
    {
        return DispatchSiteScan.Scan(
            "src/Sharpy.Lsp",
            "src/Sharpy.Lsp/Sharpy.Lsp.csproj",
            keyPrefix: "Sharpy.Lsp",
            aliasOverrides: aliasOverrides,
            suppressGlobalUsings: suppressGlobalUsings);
    }

    /// <summary>
    /// Known residue: 2 switches on .Count (int type) that the compilation cannot
    /// fully resolve due to reference-chain depth through Microsoft.CodeAnalysis types.
    /// Plan-950124 Current State: "the residue is 2 unresolved scrutinees, both int
    /// (.Count)". They are NOT AST dispatch sites — the scrutinee text ends with ".Count"
    /// and the enclosing methods do not dispatch on AST kinds.
    /// </summary>
    private static readonly HashSet<(string File, string EnclosingContext)> KnownIntCountResidue = new()
    {
        ("CodeGen/RoslynEmitter.Statements.cs", "RoslynEmitter.GenerateBodyStatement"),
        ("Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs", "TypeChecker.CollectionSignatureSatisfies"),
    };

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_NoUnresolvedScrutinees()
    {
        var result = ScanCompiler();

        var unexpected = result.Unresolved
            .Where(u => !KnownIntCountResidue.Contains((u.File, u.EnclosingContext)))
            .ToList();

        foreach (var u in result.Unresolved)
        {
            var label = KnownIntCountResidue.Contains((u.File, u.EnclosingContext))
                ? "KNOWN-INT" : "UNRESOLVED";
            _output.WriteLine($"{label}: {u.File}:{u.Line} ({u.ScrutineeText}) in {u.EnclosingContext}");
        }

        unexpected.Should().BeEmpty(
            "every switch scrutinee in the compiler must resolve to a type — " +
            "unresolved scrutinees hide dispatch sites from the inventory " +
            "(2 known .Count int residue switches are excluded)");

        result.Sites.Should().NotBeEmpty("the compiler must contain AST/IrNode dispatch sites");
        _output.WriteLine($"Compiler: {result.Sites.Count} sites, {result.SiteCountByKey.Count} distinct keys, " +
            $"{result.TotalSwitchCount} total switches, {result.Unresolved.Count} known-int residue");
    }

    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LspScan_NoUnresolvedScrutinees()
    {
        var result = ScanLsp();

        foreach (var u in result.Unresolved)
            _output.WriteLine($"UNRESOLVED: {u.File}:{u.Line} ({u.ScrutineeText}) in {u.EnclosingContext}");

        result.Unresolved.Should().BeEmpty(
            "every switch scrutinee in the LSP must resolve — " +
            "unresolved scrutinees hide dispatch sites");

        result.Sites.Should().NotBeEmpty("the LSP must contain AST dispatch sites");
        _output.WriteLine($"LSP: {result.Sites.Count} sites, {result.SiteCountByKey.Count} distinct keys, " +
            $"{result.TotalSwitchCount} total switches");
    }

    /// <summary>
    /// Mutation (a): dropping the SharpyRT alias makes types referenced via that alias
    /// unresolvable, so the scan reports unresolved scrutinees. This is the instrument
    /// health check — without the alias, 16+ AST sites go dark.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_DroppingSharpyRtAlias_IncreasesUnresolved()
    {
        var aliasesWithoutSharpyRt = DispatchSiteScan.ReadAliasMap(
            Path.Combine(DispatchSiteScan.FindRepoRoot(),
                "src/Sharpy.Compiler/Sharpy.Compiler.csproj"));
        aliasesWithoutSharpyRt.Remove("Sharpy.Core");

        var result = ScanCompiler(aliasOverrides: aliasesWithoutSharpyRt);

        result.Unresolved.Should().NotBeEmpty(
            "dropping SharpyRT alias must produce unresolved scrutinees — " +
            "this proves the alias is load-bearing for the census");

        foreach (var u in result.Unresolved.Take(10))
            _output.WriteLine($"  UNRESOLVED (no alias): {u.File}:{u.Line} ({u.ScrutineeText})");
    }

    /// <summary>
    /// Mutation (b): suppressing synthesized global usings makes types from implicit
    /// usings unresolvable.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_SuppressingGlobalUsings_IncreasesUnresolved()
    {
        var result = ScanCompiler(suppressGlobalUsings: true);

        result.Unresolved.Should().NotBeEmpty(
            "suppressing global usings must produce unresolved scrutinees — " +
            "this proves the synthesized usings are load-bearing");

        foreach (var u in result.Unresolved.Take(10))
            _output.WriteLine($"  UNRESOLVED (no usings): {u.File}:{u.Line} ({u.ScrutineeText})");
    }

    /// <summary>
    /// The four roster rows that exist only because the IrNode root is scanned.
    /// </summary>
    private static readonly string[] IrNodeRewriteKeys =
    {
        "Lowering/IrTreeRewriter.cs::IrTreeRewriter.RewriteNode",
        "Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.RewriteNode",
        "Lowering/Passes/ConstFoldPass.cs::ConstFoldPass.RewriteNode",
        "Lowering/Passes/StackCollectionsPass.cs::StackCollectionsPass.RewriteNode",
    };

    /// <summary>
    /// Mutation (c), made executable and falsifiable in both directions: WITH the IrNode root
    /// the four RewriteNode keys are present; WITHOUT it (<c>includeIrNodeRoot: false</c>)
    /// they are ABSENT and no site is IrNode-typed, while the Node-typed census is unchanged.
    /// The absence half is the recorded red state the plan asked for, turned into a positive
    /// control: if dropping the root did not remove the keys, the four rows could be phantoms
    /// resolved by some other path.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_IrNodeRoot_RewriteNodeKeysPresentWithRoot_AbsentWithout()
    {
        var withRoot = ScanCompiler();
        var withoutRoot = ScanCompiler(includeIrNodeRoot: false);

        var keysWith = withRoot.Sites.Select(s => s.Key).ToHashSet();
        var keysWithout = withoutRoot.Sites.Select(s => s.Key).ToHashSet();

        foreach (var site in withRoot.Sites.Where(s => s.Root == "IrNode"))
            _output.WriteLine($"  IrNode site (with root): {site.Key} ({site.ScrutineeText})");
        _output.WriteLine($"  with root: {withRoot.Sites.Count} sites; without root: {withoutRoot.Sites.Count} sites");

        foreach (var key in IrNodeRewriteKeys)
        {
            keysWith.Should().Contain(key,
                $"'{key}' is an IrNode-typed dispatch and must be found when the IrNode root is scanned");
            keysWithout.Should().NotContain(key,
                $"'{key}' must vanish when the IrNode root is not scanned — otherwise the root is not what finds it");
        }

        withRoot.Sites.Should().Contain(s => s.Root == "IrNode",
            "the IrNode root must contribute at least the RewriteNode sites");
        withoutRoot.Sites.Should().NotContain(s => s.Root == "IrNode",
            "no site can be IrNode-typed when the IrNode root is not resolved");

        withoutRoot.Sites.Count(s => s.Root == "Node").Should().Be(
            withRoot.Sites.Count(s => s.Root == "Node"),
            "dropping the IrNode root must not disturb the Node-typed census");
    }

    /// <summary>
    /// Positive control (d): CollectBindingKeysInto switches on a parameter named "target"
    /// (not one of the old identifier names node/stmt/pattern/expr), so the old name-scoped
    /// scan missed it. The typed scan must find it.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_FindsNonIdentifierScrutinee_CollectBindingKeysInto()
    {
        var result = ScanCompiler();

        result.Sites.Should().Contain(s =>
            s.Key.Contains("ControlFlowGraphBuilder") && s.Key.Contains("CollectBindingKeysInto"),
            "CollectBindingKeysInto (scrutinee 'target') must be found by the typed scan — " +
            "it was invisible to the name-scoped scan (#1715)");
    }

    /// <summary>
    /// Negative control (d): the Lexer's <c>switch (threeChar)</c> dispatches on a char,
    /// not an AST node. It must NOT appear in the results.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_DoesNotFindNonAstSwitch()
    {
        var result = ScanCompiler();

        result.Sites.Should().NotContain(s =>
            s.Key.Contains("Lexer.cs") && s.ScrutineeText.Contains("threeChar"),
            "non-AST switches (like Lexer's char dispatch) must not appear");
    }

    /// <summary>
    /// Per-key multiplicity: RoslynEmitter.GenerateImperativeComprehension has two
    /// switch sites dispatching on clauses[i], both under the same key. The key count
    /// must be 1 but the site count must be 2.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_MultiSwitchMethodHasCorrectMultiplicity()
    {
        var result = ScanCompiler();

        var key = result.SiteCountByKey.Keys
            .FirstOrDefault(k => k.Contains("RoslynEmitter") && k.Contains("GenerateImperativeComprehension"));

        key.Should().NotBeNull(
            "GenerateImperativeComprehension must appear as a dispatch site");

        result.SiteCountByKey[key!].Should().BeGreaterThanOrEqualTo(2,
            "GenerateImperativeComprehension has two clause-dispatch switches under one key");
    }

    /// <summary>
    /// LSP positive control: CodeLensHandler.Handle uses a <c>foreach (var stmt …)</c>
    /// pattern that was unresolved without global usings in the prototype.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LspScan_FindsCodeLensHandler()
    {
        var result = ScanLsp();

        result.Sites.Should().Contain(s =>
            s.Key.Contains("Sharpy.Lsp/") && s.Key.Contains("CodeLensHandler"),
            "CodeLensHandler dispatch sites must be visible through the LSP scan");
    }

    /// <summary>
    /// LSP keys are prefixed with "Sharpy.Lsp/" so they never collide with compiler keys.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void LspScan_AllKeysArePrefixed()
    {
        var result = ScanLsp();

        foreach (var site in result.Sites)
        {
            site.Key.Should().StartWith("Sharpy.Lsp/",
                $"LSP site key '{site.Key}' must be prefixed with 'Sharpy.Lsp/'");
        }
    }

    /// <summary>
    /// The EnclosingContext for a switch inside a local function must use the enclosing
    /// METHOD's key, not the local function's — otherwise the 75 existing keys churn.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void CompilerScan_ExistingKeysStillPresent()
    {
        var result = ScanCompiler();
        var keys = result.Sites.Select(s => s.Key).ToHashSet();

        var sampleExistingKeys = new[]
        {
            "Parser/Ast/AstVisitor.cs::AstVisitor.Visit",
            "CodeGen/RoslynEmitter.Expressions.cs::RoslynEmitter.GenerateExpressionCore",
            "Semantic/TypeChecker.Statements.cs::TypeChecker.CheckDeferBodyControlFlow",
        };

        foreach (var existingKey in sampleExistingKeys)
        {
            keys.Should().Contain(existingKey,
                $"existing roster key '{existingKey}' must still be found by the typed scan");
        }
    }

    /// <summary>
    /// Census reconciliation (Phase 0 acceptance / verify-round finding P0.4): one run of the
    /// shipped instrument reports, per root, every number the two prototype readings disagreed
    /// on (compiler: 161 sites / 153 keys / 88 non-identifier scrutinees vs 164 / 127; LSP: 30).
    /// The numbers are written to the test output for the lead to reconcile and are NOT pinned —
    /// they change as switches come and go. Only what must hold is asserted: non-zero
    /// switches/sites/keys for both roots, a non-zero IrNode census and both scrutinee spellings
    /// for the compiler, and the partition identities.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void Census_ReportsCounts()
    {
        var compiler = ReportCensus("Compiler", ScanCompiler());
        var lsp = ReportCensus("LSP", ScanLsp());

        compiler.IrNodeSites.Should().BeGreaterThan(0, "the compiler has IrNode-typed rewriters");
        compiler.CanonicalIdentifierSites.Should().BeGreaterThan(0,
            "the compiler has switches on identifiers spelled node/stmt/pattern/expr");
        compiler.OtherSpellingSites.Should().BeGreaterThan(0,
            "the compiler has switches on other spellings (e.g. CollectBindingKeysInto's 'target', #1715)");
        lsp.IrNodeSites.Should().Be(0, "the LSP does not dispatch on IrNode");
    }

    private sealed record Census(
        int TotalSwitches, int Sites, int NodeSites, int IrNodeSites, int DistinctKeys,
        int CanonicalIdentifierSites, int OtherSpellingSites,
        int SwitchStatements, int SwitchExpressions, int NoDefaultArm, int Unresolved);

    private Census ReportCensus(string root, DispatchSiteScan.ScanResult result)
    {
        static bool IsCanonicalIdentifier(string scrutinee)
            => scrutinee is "node" or "stmt" or "pattern" or "expr";

        var census = new Census(
            TotalSwitches: result.TotalSwitchCount,
            Sites: result.Sites.Count,
            NodeSites: result.Sites.Count(s => s.Root == "Node"),
            IrNodeSites: result.Sites.Count(s => s.Root == "IrNode"),
            DistinctKeys: result.SiteCountByKey.Count,
            CanonicalIdentifierSites: result.Sites.Count(s => IsCanonicalIdentifier(s.ScrutineeText)),
            OtherSpellingSites: result.Sites.Count(s => !IsCanonicalIdentifier(s.ScrutineeText)),
            SwitchStatements: result.Sites.Count(s => s.Form == "SwitchStatement"),
            SwitchExpressions: result.Sites.Count(s => s.Form == "SwitchExpression"),
            NoDefaultArm: result.Sites.Count(s => !s.HasDefaultArm),
            Unresolved: result.Unresolved.Count);

        _output.WriteLine($"CENSUS {root}: total-switches={census.TotalSwitches} sites={census.Sites} " +
            $"node-sites={census.NodeSites} irnode-sites={census.IrNodeSites} distinct-keys={census.DistinctKeys} " +
            $"canonical-identifier(node/stmt/pattern/expr)={census.CanonicalIdentifierSites} other-spelling={census.OtherSpellingSites} " +
            $"switch-statements={census.SwitchStatements} switch-expressions={census.SwitchExpressions} " +
            $"no-default-arm={census.NoDefaultArm} unresolved={census.Unresolved}");

        var otherSpellings = result.Sites
            .Where(s => !IsCanonicalIdentifier(s.ScrutineeText))
            .Select(s => s.ScrutineeText)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
        _output.WriteLine($"CENSUS {root}: other-spelling scrutinees ({otherSpellings.Count} distinct): " +
            string.Join(" | ", otherSpellings));

        census.TotalSwitches.Should().BeGreaterThan(0, $"{root} has switches");
        census.Sites.Should().BeGreaterThan(0, $"{root} has AST dispatch sites");
        census.NodeSites.Should().BeGreaterThan(0, $"{root} has Node-typed dispatch sites");
        census.DistinctKeys.Should().BeGreaterThan(0, $"{root} has dispatch keys");
        census.DistinctKeys.Should().BeLessThanOrEqualTo(census.Sites, "a key covers one or more sites");
        (census.NodeSites + census.IrNodeSites).Should().Be(census.Sites, "every site has exactly one root");
        (census.SwitchStatements + census.SwitchExpressions).Should().Be(census.Sites, "every site has exactly one form");
        (census.CanonicalIdentifierSites + census.OtherSpellingSites).Should().Be(census.Sites, "spelling is a partition");
        census.NoDefaultArm.Should().BeLessThanOrEqualTo(census.Sites);
        census.Sites.Should().BeLessThanOrEqualTo(census.TotalSwitches - census.Unresolved,
            "sites are the typed subset of resolved switches");

        return census;
    }

    /// <summary>
    /// Enclosing-member fallback (plan-950124 Phase 0 Task 1): a switch inside a constructor
    /// keys as <c>Type..ctor</c>; inside a property accessor or an expression-bodied property
    /// as <c>Type.PropertyName</c>; inside an indexer as <c>Type.this[]</c>; inside a local
    /// function as the enclosing METHOD (the existing rule, kept). Before the fallback all of
    /// the first four keyed as <c>Type.&lt;no-method&gt;</c>. Parses a snippet, so it is a unit
    /// fact on the instrument, independent of whether the codebase currently has such a switch.
    /// </summary>
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void EnclosingContext_FallsBackToConstructorThenPropertyThenNoMethod()
    {
        const string source = @"
class C
{
    private readonly int _f;

    public C(object o)
    {
        switch (o) { case string: _f = 1; break; default: _f = 0; break; }
    }

    public int P
    {
        get { switch (_f) { case 1: return 1; default: return 0; } }
    }

    public int Q => _f switch { 1 => 1, _ => 0 };

    public int this[int i]
    {
        get { switch (i) { default: return i; } }
    }

    public int M(object o)
    {
        int Local(object x) { switch (x) { default: return 0; } }
        return Local(o);
    }
}

class Outer<T>
{
    public Outer() { _ = 1 switch { _ => 0 }; }
}";
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var contexts = root.DescendantNodes()
            .Where(n => n is SwitchStatementSyntax or SwitchExpressionSyntax)
            .Select(DispatchSiteScan.EnclosingContext)
            .ToList();

        foreach (var c in contexts)
            _output.WriteLine($"  {c}");

        contexts.Should().Equal(
            "C..ctor",
            "C.P",
            "C.Q",
            "C.this[]",
            "C.M",
            "Outer`1..ctor");
        contexts.Should().NotContain(c => c.Contains("<no-method>"),
            "every switch in the snippet has an enclosing constructor, property, indexer, or method");
    }
}
