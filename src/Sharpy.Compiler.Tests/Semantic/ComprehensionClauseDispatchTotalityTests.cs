using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Family guard for dispatch sites over <see cref="ComprehensionClause"/> kinds.
/// The universe is {ForClause, IfClause} — the only concrete subtypes.
/// Every member site's arms are pinned; a new ComprehensionClause kind fails all sites at once.
///
/// Family members: CheckComprehensionClauses (type checker), QualifiesForProductPreallocation
/// (lowering), DumpComprehensionClause (AST dumper), GenerateDictSpreadComprehension and
/// GenerateImperativeComprehension (emitter). The LSP member,
/// <c>SharpySemanticTokensHandler.CollectComprehensionClauseTokens</c>, is pinned against the
/// same {ForClause, IfClause} set in <c>Lsp/SemanticTokensDispatchTotalityTests</c>
/// (<c>CollectComprehensionClauseTokens_Arms</c>) and is not duplicated here.
/// </summary>
public class ComprehensionClauseDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public ComprehensionClauseDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcreteClauseNames()
    {
        var baseType = typeof(ComprehensionClause);
        return baseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly HashSet<string> Universe = new()
    {
        nameof(ForClause),
        nameof(IfClause),
    };

    [Fact]
    public void Universe_MatchesReflection()
    {
        var actual = GetConcreteClauseNames();
        _output.WriteLine($"Concrete ComprehensionClause subtypes: {actual.Count}");
        foreach (var name in actual)
            _output.WriteLine($"  {name}");

        Assert.True(Universe.SetEquals(new HashSet<string>(actual)),
            $"Universe mismatch.\n" +
            $"  Extra in universe: {string.Join(", ", Universe.Except(actual))}\n" +
            $"  Missing from universe: {string.Join(", ", actual.Except(Universe))}");
    }

    // --- CheckComprehensionClauses ---
    [Fact]
    public void CheckComprehensionClauses_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs",
            "CheckComprehensionClauses");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CheckComprehensionClauses arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    // --- QualifiesForProductPreallocation ---
    [Fact]
    public void QualifiesForProductPreallocation_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Lowering/Passes/ComprehensionFusionPass.cs",
            "QualifiesForProductPreallocation");
        Assert.NotEmpty(arms);
        _output.WriteLine($"QualifiesForProductPreallocation arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    // --- DumpComprehensionClause ---
    [Fact]
    public void DumpComprehensionClause_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Parser/AstDumper.cs",
            "DumpComprehensionClause");
        Assert.NotEmpty(arms);
        _output.WriteLine($"DumpComprehensionClause arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    // --- Emitter sites (verify-round finding P4.3: named by the plan, previously unpinned) ---
    private const string EmitterComprehensionsFile =
        "src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Comprehensions.cs";

    // --- GenerateDictSpreadComprehension: one switch over dictSpreadComp.Clauses[i] ---
    [Fact]
    public void GenerateDictSpreadComprehension_Arms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            EmitterComprehensionsFile,
            "GenerateDictSpreadComprehension");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GenerateDictSpreadComprehension arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    /// <summary>
    /// GenerateImperativeComprehension holds TWO <c>switch (clauses[i])</c> statements under one
    /// roster key (forward pass, then reverse assembly). <see cref="SwitchArmScan.CaseTypeNames(string, string)"/>
    /// UNIONS arms across every switch in the method, so this union pin catches a new clause kind
    /// (absent from both switches) but NOT one arm deleted from one of the two switches — that
    /// cell is guarded by <see cref="GenerateImperativeComprehension_EachSwitch_CoversUniverse"/>.
    /// </summary>
    [Fact]
    public void GenerateImperativeComprehension_UnionArms_CoverUniverse()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            EmitterComprehensionsFile,
            "GenerateImperativeComprehension");
        Assert.NotEmpty(arms);
        _output.WriteLine($"GenerateImperativeComprehension union arms: {string.Join(", ", arms)}");
        Assert.True(arms.SetEquals(Universe),
            $"Arms differ from universe.\n" +
            $"  Extra: {string.Join(", ", arms.Except(Universe))}\n" +
            $"  Missing: {string.Join(", ", Universe.Except(arms))}");
    }

    /// <summary>
    /// Per-switch pin for the double-switch method: exactly two clause switches, and EACH one
    /// names every clause kind. Uses a label-text probe (<c>case Kind</c>) rather than
    /// SwitchArmScan because SwitchArmScan deliberately reports per method, not per switch.
    /// </summary>
    [Fact]
    public void GenerateImperativeComprehension_EachSwitch_CoversUniverse()
    {
        var (statements, expressions) = SwitchArmScan.DispatchFormCounts(
            EmitterComprehensionsFile, "GenerateImperativeComprehension", "RoslynEmitter");
        _output.WriteLine($"GenerateImperativeComprehension: {statements} switch statements, {expressions} switch expressions");
        Assert.Equal(2, statements);
        Assert.Equal(0, expressions);

        var perSwitch = PerSwitchClauseKinds(EmitterComprehensionsFile, "GenerateImperativeComprehension");
        Assert.Equal(2, perSwitch.Count);
        for (int i = 0; i < perSwitch.Count; i++)
        {
            _output.WriteLine($"  switch #{i + 1} arms: {string.Join(", ", perSwitch[i])}");
            Assert.True(perSwitch[i].SetEquals(Universe),
                $"Switch #{i + 1} in GenerateImperativeComprehension differs from universe.\n" +
                $"  Extra: {string.Join(", ", perSwitch[i].Except(Universe))}\n" +
                $"  Missing: {string.Join(", ", Universe.Except(perSwitch[i]))}");
        }
    }

    /// <summary>
    /// For every switch statement inside the named method(s), the set of universe kinds named
    /// by a <c>case Kind …:</c> label (declaration form <c>case ForClause f:</c> and bare form
    /// <c>case IfClause:</c> alike). One set per switch, in source order.
    /// </summary>
    private static List<HashSet<string>> PerSwitchClauseKinds(string repoRelativePath, string methodName)
    {
        var fullPath = Path.Combine(DispatchSiteScan.FindRepoRoot(), repoRelativePath);
        Assert.True(File.Exists(fullPath), $"source file not found: {fullPath}");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath)).GetCompilationUnitRoot();

        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == methodName)
            .ToList();
        Assert.NotEmpty(methods);

        var result = new List<HashSet<string>>();
        foreach (var method in methods)
        {
            foreach (var switchStmt in method.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                var kinds = new HashSet<string>();
                foreach (var label in switchStmt.Sections.SelectMany(s => s.Labels))
                {
                    var text = label.ToString();
                    foreach (var kind in Universe)
                    {
                        if (Regex.IsMatch(text, $@"^\s*case\s+{Regex.Escape(kind)}\b"))
                            kinds.Add(kind);
                    }
                }
                result.Add(kinds);
            }
        }
        return result;
    }
}
