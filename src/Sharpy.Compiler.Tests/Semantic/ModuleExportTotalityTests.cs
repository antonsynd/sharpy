using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// P5.1 (#1674, #1906): a module-level <c>union</c> or <c>delegate</c> declaration used to fall
/// to <c>default:</c> in both <c>ModuleLoader.ExtractExportedSymbol</c> and
/// <c>ModuleLoader.CreateStubModuleInfo</c> (never exported), and the checker's export-TYPE
/// switch (<c>TypeChecker.Expressions.Access.cs</c>, inside <c>CheckMemberAccessCore</c>) had a
/// <c>_ =&gt; Unknown</c> arm marked <see cref="Sharpy.Compiler.Semantic.ErrorRecoveryReason.DeliberatelyPermissive"/>
/// that no real export could ever reach — the only <see cref="Symbol"/> subtype it could see,
/// <c>TypeParameterSymbol</c>, is never constructed as a module export.
///
/// <para>
/// <see cref="ModuleLoaderTotalityTests"/> pins the two export switches against every concrete
/// <see cref="Statement"/> subtype (a strict superset of "module-level"), and was updated in this
/// same change to move <c>UnionDef</c>/<c>DelegateDef</c> from its Skipped to its Handled sets.
/// This file adds the two comparisons that guard cross-family: the export switches against
/// <see cref="DeclarationKindDispatchTotalityTests.ModuleLevelKinds"/> (so the "totality" family
/// and the "export" family cannot silently diverge on what counts as a module-level kind), and
/// the export-TYPE switch against the literal <see cref="Symbol"/>-subclass roster — plus the
/// executing cells that prove a module-level union/delegate export actually resolves and RUNS on
/// both import routes (p7b: <c>import lib</c> + qualified use; p11: <c>from lib import ...</c>).
/// </para>
/// </summary>
public class ModuleExportTotalityTests
{
    private const string ModuleLoaderFile = "src/Sharpy.Compiler/Semantic/ModuleLoader.cs";
    private const string ExportTypeSwitchFile = "src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs";

    private readonly ITestOutputHelper _output;

    public ModuleExportTotalityTests(ITestOutputHelper output) => _output = output;

    // ═══════════════════════════════════════════════════════════════════════
    // Export switch(es) vs ModuleLevelKinds (cross-family with
    // DeclarationKindDispatchTotalityTests, whose AuthorityKinds are read from
    // NameResolver.ResolveDeclaration — the actual module-level declaration authority).
    // ═══════════════════════════════════════════════════════════════════════

    // ModuleLevelKinds includes two kinds ExtractExportedSymbol does not (and should not) export:
    //   - ImportStatement / FromImportStatement: module-level, but statements, not declarations —
    //     ImportResolver handles them (ModuleLoader.cs's `default:` comment says so explicitly).
    //   - PropertyDef: a module-level property (#844) IS resolved by NameResolver.ResolveDeclaration
    //     but ExtractExportedSymbol does not export it — a pre-existing gap ModuleLoaderTotalityTests'
    //     own Skipped set already documents, tracked separately and out of P5.1's scope.
    private static readonly HashSet<string> NotExportedDeclarations = new()
    {
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(PropertyDef),
    };

    [Fact]
    public void ExtractExportedSymbol_Arms_MatchModuleLevelDeclarationKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(ModuleLoaderFile, "ExtractExportedSymbol");
        var expected = new HashSet<string>(DeclarationKindDispatchTotalityTests.ModuleLevelKinds);
        expected.ExceptWith(NotExportedDeclarations);

        _output.WriteLine($"ExtractExportedSymbol arms: {string.Join(", ", arms.OrderBy(n => n, StringComparer.Ordinal))}");
        _output.WriteLine($"Expected (ModuleLevelKinds minus documented exclusions): {string.Join(", ", expected.OrderBy(n => n, StringComparer.Ordinal))}");

        Assert.True(arms.SetEquals(expected),
            "ExtractExportedSymbol arms differ from ModuleLevelKinds minus the documented exclusions.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    [Fact]
    public void CreateStubModuleInfo_Arms_AreSubsetOf_ModuleLevelDeclarationKinds()
    {
        var arms = SwitchArmScan.CaseTypeNames(ModuleLoaderFile, "CreateStubModuleInfo");
        var notModuleLevel = arms.Except(DeclarationKindDispatchTotalityTests.ModuleLevelKinds).ToList();

        Assert.True(notModuleLevel.Count == 0,
            $"CreateStubModuleInfo handles kinds ModuleLevelKinds does not recognize as module-level: " +
            $"{string.Join(", ", notModuleLevel)}");
    }

    [Fact]
    public void UnionAndDelegate_AreExportedDeclarations_NotExcluded()
    {
        // The two kinds this phase joins to the export switches — pinned directly so a future
        // edit to NotExportedDeclarations cannot silently exclude them again.
        Assert.DoesNotContain(nameof(UnionDef), NotExportedDeclarations);
        Assert.DoesNotContain(nameof(DelegateDef), NotExportedDeclarations);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Export-TYPE switch (TypeChecker.Expressions.Access.cs, inside CheckMemberAccessCore) vs the
    // literal concrete Symbol-subclass roster (reflection, mirrors
    // ModuleLoaderTotalityTests.GetConcreteStatementNames for Statement).
    // ═══════════════════════════════════════════════════════════════════════

    private static List<string> GetConcreteSymbolSubtypeNames()
    {
        var symbolBaseType = typeof(Symbol);
        return symbolBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(symbolBaseType) && !t.IsAbstract && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void ExportTypeSwitch_Arms_MatchConcreteSymbolSubtypeRoster()
    {
        var roster = GetConcreteSymbolSubtypeNames();
        Assert.NotEmpty(roster);

        // CheckMemberAccessCore has exactly two switches: the module-export one (this switch's
        // arms are all Symbol subtypes) and a `memberAccess.Member switch { "result" or ... }`
        // string-constant switch a few hundred lines away — its arms are string literals, which
        // SwitchArmScan's pattern collector does not record as type names, so the scan below sees
        // only the export switch's arms without needing a second scoping mechanism.
        var arms = SwitchArmScan.CaseTypeNames(ExportTypeSwitchFile, "CheckMemberAccessCore");

        _output.WriteLine($"Export-type switch arms: {string.Join(", ", arms.OrderBy(n => n, StringComparer.Ordinal))}");
        _output.WriteLine($"Concrete Symbol subtypes: {string.Join(", ", roster)}");

        Assert.True(arms.SetEquals(roster),
            "The export-type switch (module-qualified member access, #1674, #1906) must name every " +
            "concrete Symbol subtype exactly once — no `_` discard among the typed arms, so a new " +
            "Symbol subtype added without a matching arm here throws loudly at the first module " +
            "export of that kind, instead of silently falling into a DeliberatelyPermissive mark no " +
            "export could ever reach.\n" +
            $"  Extra: {string.Join(", ", arms.Except(roster))}\n" +
            $"  Missing: {string.Join(", ", roster.Except(arms))}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Executing cells (p7b, p11): a module-level union and delegate resolve and RUN on both
    // import routes now that the export switches are total.
    // ═══════════════════════════════════════════════════════════════════════

    private const string UnionAndDelegateLib = """
        union Shape:
            case Circle(radius: int)
            case Square(side: int)

        delegate Scorer(x: int) -> int

        def make_circle(r: int) -> Shape:
            return Shape.Circle(r)
        """;

    /// <summary>
    /// p7b: <c>import lib</c> + the qualified route. The union is annotated and matched through
    /// its module-qualified spelling (<c>lib.Shape</c>) and constructed via a same-file helper
    /// (<c>lib.make_circle</c>) — the qualified UNION-CASE constructor call <c>lib.Shape.Circle(3)</c>
    /// is a separate, narrower CodeGen defect filed as #1907 (sibling cell found while verifying
    /// this phase; not spot-fixed here, out of the Semantic swimlane). The delegate is exercised
    /// through the qualified spelling directly (<c>lib.Scorer</c>), which has no such gap.
    /// </summary>
    [Fact]
    public void CrossModuleImport_QualifiedRoute_UnionAndDelegateExports_RunAndPrint()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("P7b").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", UnionAndDelegateLib);
        helper.AddSourceFile("main.spy", """
            import lib

            def main() -> None:
                s: lib.Shape = lib.make_circle(3)
                match s:
                    case Circle(radius):
                        print(radius)
                    case Square(side):
                        print(side)

                scorer: lib.Scorer = lambda x: x * 2
                print(scorer(5))
            """);
        helper.CreateProjectFile();

        var run = helper.CompileAndExecute();

        run.Success.Should().BeTrue(
            "p7b: import lib + qualified union/delegate use must compile and run (#1674, #1906). " +
            "Errors:\n" + string.Join("\n", run.CompilationErrors));
        run.StandardOutput.Should().Be("3\n10\n");
    }

    /// <summary>
    /// p11: <c>from lib import Shape, Scorer</c> — the bare route, including a DIRECT
    /// case-constructor call (<c>Shape.Circle(3)</c>), which is unaffected by #1907 (that defect
    /// is specific to the qualified <c>module.Union.Case(...)</c> spelling).
    /// </summary>
    [Fact]
    public void CrossModuleImport_BareFromImportRoute_UnionAndDelegateExports_RunAndPrint()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("P11").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", UnionAndDelegateLib);
        helper.AddSourceFile("main.spy", """
            from lib import Shape, Scorer

            def main() -> None:
                s: Shape = Shape.Circle(3)
                match s:
                    case Circle(radius):
                        print(radius)
                    case Square(side):
                        print(side)

                scorer: Scorer = lambda x: x * 2
                print(scorer(5))
            """);
        helper.CreateProjectFile();

        var run = helper.CompileAndExecute();

        run.Success.Should().BeTrue(
            "p11: from lib import Shape, Scorer (bare route) must compile and run (#1674, #1906). " +
            "Errors:\n" + string.Join("\n", run.CompilationErrors));
        run.StandardOutput.Should().Be("3\n10\n");
    }
}
