using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Standing inventory of hand-rolled AST dispatch switches in the compiler.
/// Every site must carry a rostered justification; a new unguarded dispatch
/// fails this test, preventing the #1694/#1709 vacuity class from recurring.
/// </summary>
public class DispatchSiteInventoryTests
{
    private readonly ITestOutputHelper _output;

    public DispatchSiteInventoryTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex SwitchPattern =
        new(@"switch\s*\((node|stmt|pattern|expr)\)", RegexOptions.Compiled);

    /// <summary>
    /// Maps "relative_path::method_or_enclosing_context" → justification category.
    /// The key uses "::" as a separator, making it resilient to line drift.
    /// </summary>
    private static readonly Dictionary<string, string> Roster = new()
    {
        // ── guarded-by: totality tests that assert scan-vs-roster ──
        ["Parser/Ast/AstVisitor.cs::Visit"] = "guarded-by:AstVisitorTotalityTests",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::BuildStatement"] = "guarded-by:CfgStatementTotalityTests",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::CollectPatternBindingKeysInto"] = "guarded-by:CfgPatternBindingTotalityTests",
        ["Semantic/ExecutionOrderAnalyzer.cs::CollectReferencedIdentifiers"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",
        ["Semantic/ExecutionOrderAnalyzer.cs::CollectDeclarationNames"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/TypeChecker.cs"] = "guarded-by:TypeCheckerClrTypeTotalityTests",
        ["Semantic/TypeChecker.Statements.cs"] = "guarded-by:TypeCheckerClrTypeTotalityTests",
        ["Semantic/TypeChecker.Statements.Patterns.cs"] = "guarded-by:TypeCheckerClrTypeTotalityTests",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs"] = "guarded-by:TypeCheckerClrTypeTotalityTests",
        ["Shared/ExhaustivenessHelper.cs::CollectCoveredCases"] = "guarded-by:ExhaustivenessHelperTotalityTests",
        ["Semantic/IntegerConstantEvaluator.cs"] = "guarded-by:IntegerConstantEvaluatorTotalityTests",

        // ── documented-by-design: deliberately partial dispatch ──
        ["Lowering/IrTreeRewriter.cs::RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~18-25",
        ["Lowering/Passes/ComprehensionFusionPass.cs"] = "documented-by-design:Parser/Ast/Types.cs:~18-25",
        ["Lowering/Passes/ConstFoldPass.cs"] = "documented-by-design:Parser/Ast/Types.cs:~18-25",
        ["Lowering/Passes/StackCollectionsPass.cs"] = "documented-by-design:Parser/Ast/Types.cs:~18-25",
        ["Parser/Ast/Types.cs"] = "documented-by-design:Parser/Ast/Types.cs:~18-25",

        // ── walker-default-contract: validators whose default-ignore is contractual ──
        ["Semantic/Validation/DefaultParameterValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/EqualityContractValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/EventValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/FinalFieldValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/GeneratorValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/InterfaceConflictValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/ModuleLevelValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/PropertyValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/SignatureValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/UnusedVariableValidator.cs"] = "walker-default-contract",
        ["Semantic/Validation/VarianceValidator.cs"] = "walker-default-contract",

        // ── refusal-net: conformance tests cover the dispatch ──
        ["CodeGen/CodeValidator.cs"] = "refusal-net:EmitterCarrierOnlyConformanceTests",
        ["CodeGen/RoslynEmitter.ClassMembers.cs"] = "refusal-net:EmitterCarrierOnlyConformanceTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs"] = "refusal-net:EmitterCarrierOnlyConformanceTests",
        ["CodeGen/RoslynEmitter.Operators.cs"] = "refusal-net:EmitterCarrierOnlyConformanceTests",
        ["CodeGen/RoslynEmitter.Patterns.cs"] = "refusal-net:EmitterCarrierOnlyConformanceTests",
        ["Parser/Parser.Primaries.cs"] = "refusal-net:parser-tests",
    };

    [Fact]
    public void AllDispatchSites_AreRostered()
    {
        var repoRoot = FindRepoRoot();
        var compilerDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler");
        var csFiles = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("bin", "")) && !f.Contains(Path.Combine("obj", "")))
            .ToList();

        var unrostered = new List<string>();

        foreach (var file in csFiles)
        {
            var relativePath = Path.GetRelativePath(compilerDir, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!SwitchPattern.IsMatch(lines[i]))
                    continue;

                var matched = Roster.Keys.Any(key => relativePath.StartsWith(key.Split("::")[0])
                    && (key.Contains("::") ? LinesNearMethodContaining(lines, i, key.Split("::")[1]) : true));

                if (!matched)
                {
                    unrostered.Add($"{relativePath}:{i + 1} — {lines[i].Trim()}");
                }
            }
        }

        foreach (var site in unrostered)
            _output.WriteLine($"UNROSTERED: {site}");

        Assert.Empty(unrostered);
    }

    [Fact]
    public void EveryJustificationCategory_HasAtLeastOneSite()
    {
        var categories = Roster.Values.Select(v => v.Split(':')[0]).Distinct().ToList();
        Assert.Contains("guarded-by", categories);
        Assert.Contains("documented-by-design", categories);
        Assert.Contains("walker-default-contract", categories);
        Assert.Contains("refusal-net", categories);
    }

    private static bool LinesNearMethodContaining(string[] lines, int switchLine, string methodHint)
    {
        for (int i = Math.Max(0, switchLine - 30); i <= switchLine; i++)
        {
            if (lines[i].Contains(methodHint))
                return true;
        }
        return false;
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
