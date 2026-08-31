using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Standing inventory of hand-rolled AST dispatch switches in the compiler.
/// Every site must carry a rostered justification; a new unguarded dispatch
/// fails this test, preventing the #1694/#1709 vacuity class from recurring.
///
/// The scan is a Roslyn parse (plan-e31e76 Phase 3 Task 5), not a line regex:
/// it finds every switch STATEMENT and switch EXPRESSION whose scrutinee is an
/// identifier named node/stmt/pattern/expr, keyed "path::Type.Method" with
/// metadata-style type names (arity-suffixed), so line drift is harmless, the
/// expression form (`node switch`) is visible, and the two AstVisitor.Visit
/// overloads are separate sites. Roster matching is exact-key, both directions:
/// an unrostered site fails, and a phantom roster row (site gone) fails —
/// entries drain on fix.
///
/// Scope: identifier-named scrutinees in src/Sharpy.Compiler only. AST dispatch
/// over differently-named scrutinees and src/Sharpy.Lsp sites are tracked by the
/// widening issue filed from the plan-e31e76 verification round.
/// </summary>
public class DispatchSiteInventoryTests
{
    private readonly ITestOutputHelper _output;

    public DispatchSiteInventoryTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> ScrutineeNames = new() { "node", "stmt", "pattern", "expr" };

    /// <summary>
    /// Maps "relative_path::Type.Method" → justification category. Categories:
    ///   guarded-by:&lt;TestClass&gt;      — a totality test asserts scan-vs-roster for the site
    ///   documented-by-design:&lt;where&gt; — deliberately partial dispatch, contract recorded there
    ///   walker-default-contract      — validator whose default-ignore is contractual
    ///   refusal-net:&lt;TestClass&gt;      — a conformance net covers the dispatch
    ///   pending-guard:#&lt;issue&gt;       — no guard/contract/net yet; the row cites its tracking
    ///                                  issue and drains when one lands (allowlist discipline)
    /// </summary>
    private static readonly Dictionary<string, string> Roster = new()
    {
        // ── guarded-by: a totality test asserts scan-vs-roster for the method ──
        ["Parser/Ast/AstVisitor.cs::AstVisitor.Visit"] = "guarded-by:AstVisitorTotalityTests",
        ["Parser/Ast/AstVisitor.cs::AstVisitor`1.Visit"] = "guarded-by:AstVisitorTotalityTests",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.BuildStatement"] = "guarded-by:CfgStatementTotalityTests",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.CollectPatternBindingKeysInto"] = "guarded-by:CfgPatternBindingTotalityTests",
        ["Semantic/ExecutionOrderAnalyzer.cs::ExecutionOrderAnalyzer.CollectReferencedIdentifiers"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",
        ["Semantic/ExecutionOrderAnalyzer.cs::ExecutionOrderAnalyzer.CollectDeclarationNames"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.ComputeForModule"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.DetectModuleLevelCollisions"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.ProcessTypeMembers"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.CollectCoveredCases"] = "guarded-by:ExhaustivenessHelperTotalityTests",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.IsIrrefutable"] = "guarded-by:ExhaustivenessHelperTotalityTests",

        // ── documented-by-design: deliberately partial dispatch, contract recorded at the cited location ──
        ["Lowering/IrTreeRewriter.cs::IrTreeRewriter.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/ConstFoldPass.cs::ConstFoldPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/StackCollectionsPass.cs::StackCollectionsPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Semantic/TypeChecker.Expressions.cs::TypeChecker.CheckExpression"] = "documented-by-design:HandleUnrecognizedExpression-default",

        // ── walker-default-contract: validators whose default-ignore is contractual ──
        ["Semantic/Validation/DecoratorValidator.cs::DecoratorValidator.IsCompileTimeConstant"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.CollectIdentifierNamesInto"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.IsCompileTimeConstant"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.IsMutableDefault"] = "walker-default-contract",
        ["Semantic/Validation/EqualityContractValidator.cs::EqualityContractValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/EventValidator.cs::EventValidator.TypeStatementName"] = "walker-default-contract",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateTypeStatement"] = "walker-default-contract",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.GetChildStatements"] = "walker-default-contract",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.ValidateModuleStatement"] = "walker-default-contract",
        ["Semantic/Validation/GeneratorValidator.cs::GeneratorValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/InterfaceConflictValidator.cs::InterfaceConflictValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.CoversItsRecordedType"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.GetPatternRecordedType"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.IsTypeTotalPattern"] = "walker-default-contract",
        ["Semantic/Validation/ModuleLevelValidator.cs::ModuleLevelValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.GetChildStatements"] = "walker-default-contract",
        ["Semantic/Validation/SignatureValidator.cs::SignatureValidator.ValidateTopLevelStatement"] = "walker-default-contract",
        ["Semantic/Validation/UnusedVariableValidator.cs::UnusedVariableValidator.CollectDefinitionsFromPattern"] = "walker-default-contract",
        ["Semantic/Validation/UnusedVariableValidator.cs::UnusedVariableValidator.CollectFromStatement"] = "walker-default-contract",
        ["Semantic/Validation/VarianceValidator.cs::VarianceValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/VarianceValidator.cs::VarianceValidator.WalkFunctionsForVariance"] = "walker-default-contract",

        // ── refusal-net: a named conformance/behavioral net covers the dispatch ──
        ["CodeGen/CodeValidator.cs::CodeValidator.ValidateNode"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ClassMembers.cs::RoslynEmitter.GenerateClassMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ClassMembers.cs::RoslynEmitter.GenerateInterfaceMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Access.Calls.cs::RoslynEmitter.IsMethodGroup"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Access.Calls.cs::RoslynEmitter.IsMethodGroupOrLambda"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.cs::RoslynEmitter.GenerateExpressionCore"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Literals.cs::RoslynEmitter.DeriveExpressionText"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateModuleMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateParametrizeMemberDataProperties"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.CollectReferencedIdentifiers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.ContainsSuperExpressionInExpression"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.ContainsSuperExpressionInStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.TransformStatementForLoopElse"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Patterns.cs::RoslynEmitter.GenerateMatchPattern"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.Assignments.cs::RoslynEmitter.IsRepeatableOperand"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.GenerateBodyStatements"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.IsCompileTimeLiteral"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.TypeDeclarations.cs::RoslynEmitter.GenerateAttributeArgumentExpression"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.cs::Parser.ParseDecoratedStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.Primaries.cs::Parser.ContainsPlaceholderIdentifier"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.Primaries.cs::Parser.ReplacePlaceholders"] = "refusal-net:FileBasedIntegrationTests",
        ["Pretty/UnparseVisitor.cs::UnparseVisitor.GetExpressionPrecedence"] = "refusal-net:UnparseIdempotencePropertyTests",
        ["Semantic/IntegerConstantEvaluator.cs::IntegerConstantEvaluator.TryGetConstantInteger"] = "refusal-net:IntegerConstantEvaluatorTests",

        // ── pending-guard: no guard, no documented contract, no named net — tracked by the
        //    cited issue; a row drains when one of the above lands (#1716) ──
        ["Project/GeneratorContextBuilder.cs::GeneratorContextBuilder.ExtractLiteralValue"] = "pending-guard:#1716",
        ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.IntegrateGeneratedSource"] = "pending-guard:#1716",
        ["Semantic/ModuleLoader.cs::ModuleLoader.ExtractNestedTypes"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.cs::TypeChecker.ReferencesUnfoldedConst"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.DescribeTypeOperand"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.IsLiteralStringExpression"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.InferParamTypesFromSubExpression"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.TryResolveExpressionType"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.cs::TypeChecker.CheckDeferBodyControlFlow"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.Patterns.cs::TypeChecker.CheckPattern"] = "pending-guard:#1716",
        ["Services/CompilerInvariants.cs::CompilerInvariants.WarnIfUnknownTypes"] = "pending-guard:#1716",
        ["Shared/AstHelper.cs::AstHelper.ContainsWalrusExpression"] = "pending-guard:#1716",
        ["Shared/AstHelper.cs::AstHelper.ExtractNarrowingKey"] = "pending-guard:#1716",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.DescribeIrrefutable"] = "pending-guard:#1716",
    };

    [Fact]
    public void AllDispatchSites_MatchRosterExactly()
    {
        var sites = ScanSites();

        var missing = sites.Except(Roster.Keys).OrderBy(s => s).ToList();
        var phantom = Roster.Keys.Except(sites).OrderBy(s => s).ToList();

        foreach (var site in missing)
            _output.WriteLine($"UNROSTERED: {site}");
        foreach (var row in phantom)
            _output.WriteLine($"PHANTOM ROSTER ROW: {row}");

        Assert.True(missing.Count == 0 && phantom.Count == 0,
            $"Dispatch-site scan and roster differ.\n" +
            $"Unrostered sites (add a row with a justification):\n  {string.Join("\n  ", missing)}\n" +
            $"Phantom roster rows (site no longer exists — drain the row):\n  {string.Join("\n  ", phantom)}");
    }

    [Fact]
    public void EveryJustificationCategory_HasAtLeastOneSite_AndAllJustificationsWellFormed()
    {
        // Positive control with an external expectation: the four categories are
        // enumerated HERE, not derived from the roster, so an emptied category or
        // a free-text typo in a justification fails.
        string[] required = { "guarded-by", "documented-by-design", "walker-default-contract", "refusal-net", "pending-guard" };

        var byCategory = Roster.Values
            .GroupBy(v => v.Split(':')[0])
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var category in required)
            Assert.True(byCategory.ContainsKey(category), $"Category '{category}' has no rostered site.");

        foreach (var (site, justification) in Roster)
        {
            var category = justification.Split(':')[0];
            Assert.True(required.Contains(category),
                $"Roster row '{site}' has malformed justification '{justification}'.");
            if (category is "guarded-by" or "refusal-net" or "documented-by-design")
            {
                Assert.True(justification.Contains(':') && justification.Split(':', 2)[1].Length > 0,
                    $"Roster row '{site}' category '{category}' must name its guard/contract.");
            }
            if (category is "pending-guard")
            {
                Assert.True(justification.Contains(":#"),
                    $"Roster row '{site}' is pending-guard and must cite its tracking issue (e.g. pending-guard:#1716).");
            }
        }
    }

    /// <summary>
    /// A justification is a claim, not a label: `guarded-by:X`/`refusal-net:X` must name a test
    /// class that EXISTS, and a `guarded-by` test must actually scan the site it is credited
    /// with (its source references the site's file and method). Before this fact the roster
    /// carried a `guarded-by:IntegerConstantEvaluatorTotalityTests` row — a class that never
    /// existed — which is the #1709 laundering defect reproduced inside its own closing harness.
    /// </summary>
    [Fact]
    public void GuardCitations_ResolveToRealTests_AndGuardedByTestsScanTheirSite()
    {
        var repoRoot = FindRepoRoot();
        var testsDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler.Tests");
        var testAssemblyTypes = typeof(DispatchSiteInventoryTests).Assembly.GetTypes()
            .Select(t => t.Name)
            .ToHashSet();

        var testSourceCache = new Dictionary<string, string?>();
        string? FindTestSource(string className)
        {
            if (testSourceCache.TryGetValue(className, out var cached))
                return cached;
            var hit = Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .FirstOrDefault(f => File.ReadAllText(f).Contains($"class {className}"));
            testSourceCache[className] = hit;
            return hit;
        }

        var violations = new List<string>();
        foreach (var (site, justification) in Roster)
        {
            var parts = justification.Split(':', 2);
            if (parts[0] is not ("guarded-by" or "refusal-net"))
                continue;
            var cited = parts[1];

            if (!testAssemblyTypes.Contains(cited))
            {
                violations.Add($"{site} cites '{cited}' — no such test class in the assembly");
                continue;
            }

            if (parts[0] == "guarded-by")
            {
                var sourcePath = FindTestSource(cited);
                if (sourcePath == null)
                {
                    violations.Add($"{site} cites '{cited}' — test source not found under Compiler.Tests");
                    continue;
                }
                var testSource = File.ReadAllText(sourcePath);
                var pathPart = site.Split("::")[0];
                var fileName = Path.GetFileName(pathPart);
                var methodName = site.Split("::")[1].Split('.')[^1];
                if (!testSource.Contains(fileName) || !testSource.Contains($"\"{methodName}\""))
                {
                    violations.Add($"{site} cites '{cited}' — that test's source does not scan "
                        + $"{fileName} :: \"{methodName}\" (unbacked guarded-by claim)");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Unbacked guard citations:\n  {string.Join("\n  ", violations)}");
    }

    private static HashSet<string> ScanSites()
    {
        var repoRoot = FindRepoRoot();
        var compilerDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler");
        var sites = new HashSet<string>();

        foreach (var file in Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var relativePath = Path.GetRelativePath(compilerDir, file).Replace('\\', '/');
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();

            foreach (var stmt in root.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                if (stmt.Expression is IdentifierNameSyntax id && ScrutineeNames.Contains(id.Identifier.Text))
                    sites.Add($"{relativePath}::{EnclosingContext(stmt)}");
            }

            foreach (var expr in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                if (expr.GoverningExpression is IdentifierNameSyntax id && ScrutineeNames.Contains(id.Identifier.Text))
                    sites.Add($"{relativePath}::{EnclosingContext(expr)}");
            }
        }

        return sites;
    }

    private static string EnclosingContext(Microsoft.CodeAnalysis.SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        var typeName = type == null
            ? "<top-level>"
            : type.TypeParameterList is { Parameters.Count: > 0 }
                ? $"{type.Identifier.Text}`{type.TypeParameterList.Parameters.Count}"
                : type.Identifier.Text;

        return $"{typeName}.{method?.Identifier.Text ?? "<no-method>"}";
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
