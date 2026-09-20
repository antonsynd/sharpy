using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
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

    // ═══════════════════════════════════════════════════════════════════════
    // Typed-export roster: every export KIND, probed by a use whose refusal must NAME the export's
    // real type. An untyped export is not a compile error — it is silent wrong output (the reason
    // this axis exists): `lib.K + "a"` on an untyped int const BUILT and printed `3a` where Python
    // raises TypeError, `b: bool = lib.K` was CS0029 behind SPY0908 instead of SPY0220 naming
    // int32, and `lib.K.nonexistent()` was CS1061 instead of SPY0203.
    //
    // The const was the one kind that lost its type on the export seam: its VariableSymbol is built
    // by NameResolver with no Type, and the declared type was written only to the DECLARING file's
    // SemanticBinding — materialized onto the symbol at the end of the whole compilation, long
    // after every importing file was checked. In the project pipeline ModuleSymbol.Exports points
    // at that very symbol (ProjectCompiler.ResolveOwnExportedSymbol, #1366/#1407/#1410), so the
    // importer read Unknown. Reproduces ONLY under `project`: the single-file front door has no
    // OwnSymbolResolver, so the ModuleLoader extraction — typed from the annotation — stands.
    //
    // The roster is anchored to the export switch's own arms (read from ModuleLoader.cs), so a new
    // export kind added without a typed-export cell here fails loudly instead of joining the
    // untyped set silently.
    // ═══════════════════════════════════════════════════════════════════════

    private const string EveryExportKindLib = """
        const K: int = 3
        v: int = 4

        def f() -> int:
            return 5

        class C:
            def __init__(self) -> None:
                pass

        struct S:
            x: int

        interface I:
            def go(self) -> int:
                ...

        enum E:
            A = 1
            B = 2

        union U:
            case A
            case B(x: int)

        delegate D(x: int) -> int

        type Alias = int
        """;

    /// <summary>
    /// One row per export kind: the <c>ExtractExportedSymbol</c> arm it comes from, the probe body
    /// (a use of the qualified export), and the type name the refusal must contain. Every row is a
    /// measured outcome, not a predicted one.
    /// </summary>
    public static TheoryData<string, string, string> TypedExportProbes() => new()
    {
        // The defect: a const export. Both spellings of the read — qualified and bare — because
        // both go through Exports.
        { nameof(VariableDeclaration), "b: bool = lib.K", "int32" },
        // Its non-const twin, the control that was already typed (a module VARIABLE is not
        // resolved by NameResolver at all, so the typed ModuleLoader extraction stands).
        { nameof(VariableDeclaration), "b: bool = lib.v", "int32" },
        { nameof(FunctionDef), "b: bool = lib.f()", "int32" },
        { nameof(ClassDef), "b: bool = lib.C()", "C" },
        { nameof(StructDef), "b: bool = lib.S(1)", "S" },
        { nameof(InterfaceDef), "x: lib.I = 3", "I" },
        { nameof(EnumDef), "b: bool = lib.E.A", "E" },
        // Also the #1907 qualified union-case construction: it must RESOLVE (to name U here) and,
        // per ModuleMemberQualificationMatrixTests, emit as a construction.
        { nameof(UnionDef), "b: bool = lib.U.A()", "U" },
        { nameof(DelegateDef), "d: lib.D = lambda x: x * 2\n    b: bool = d", "D" },
        { nameof(TypeAlias), "x: lib.Alias = \"s\"", "int32" },
    };

    [Theory]
    [MemberData(nameof(TypedExportProbes))]
    public void EveryExportKind_IsTypedAcrossTheModuleSeam(string arm, string probe, string expectedTypeName)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Kinds" + Math.Abs(probe.GetHashCode())).WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", EveryExportKindLib);
        helper.AddSourceFile("main.spy", $"""
            import lib

            def main() -> None:
                {probe}
            """);
        helper.CreateProjectFile();

        var result = helper.Compile();
        var errors = result.Diagnostics.GetErrors().ToList();

        Assert.False(result.Success, $"The {arm} probe must be refused, not accepted.");
        Assert.Contains(errors, d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains(expectedTypeName));
        // The whole point: a NAMED semantic refusal, never the generated-C# ICE an untyped export
        // produces when the mismatch reaches Roslyn instead.
        Assert.DoesNotContain(errors,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void TypedExportRoster_CoversEveryExportSwitchArm()
    {
        var arms = SwitchArmScan.CaseTypeNames(ModuleLoaderFile, "ExtractExportedSymbol");
        var covered = TypedExportProbes().Select(row => (string)row[0]!).ToHashSet(StringComparer.Ordinal);

        _output.WriteLine($"Export arms: {string.Join(", ", arms.OrderBy(n => n, StringComparer.Ordinal))}");
        _output.WriteLine($"Rows cover: {string.Join(", ", covered.OrderBy(n => n, StringComparer.Ordinal))}");

        Assert.True(covered.SetEquals(arms),
            "Every kind ExtractExportedSymbol exports needs a typed-export probe, or the next kind " +
            "joins the untyped set the way `const` did (#1674).\n" +
            $"  Arms with no probe: {string.Join(", ", arms.Except(covered))}\n" +
            $"  Probes for no arm: {string.Join(", ", covered.Except(arms))}");
    }

    [Fact]
    public void ConstExport_UsedInAWrongOperandSlot_IsRefused_NotSilentlyBuilt()
    {
        // The silent-wrong-output cell itself. python3: `3 + "a"` raises
        // TypeError: unsupported operand type(s) for +: 'int' and 'str'. Before the fix this
        // project BUILT and printed `3a`.
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ConstOperand").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", "const K: int = 3\n");
        helper.AddSourceFile("main.spy", """
            import lib

            def main() -> None:
                print(lib.K + "a")
            """);
        helper.CreateProjectFile();

        var result = helper.Compile();

        Assert.False(result.Success, "An int const + a str must be refused, as it is in Python.");
        Assert.Contains(result.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);
    }

    [Fact]
    public void ConstExport_MemberAccessOnIt_IsSpy0203_NotAnIce()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ConstMember").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", "const K: int = 3\n");
        helper.AddSourceFile("main.spy", """
            import lib

            def main() -> None:
                lib.K.nonexistent()
            """);
        helper.CreateProjectFile();

        var result = helper.Compile();
        var errors = result.Diagnostics.GetErrors().ToList();

        Assert.False(result.Success);
        Assert.Contains(errors, d => d.Code == DiagnosticCodes.Semantic.UndefinedMember);
        Assert.DoesNotContain(errors,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void ConstExport_ReadThroughFromImport_IsTypedToo()
    {
        // The bare route reaches the same symbol through ResolveImportSymbol rather than Exports,
        // so it is a second observation of the same fact, not a restatement.
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ConstBare").WithEntryPoint("main.spy");
        helper.AddSourceFile("lib.spy", "const K: int = 3\n");
        helper.AddSourceFile("main.spy", """
            from lib import K

            def main() -> None:
                b: bool = K
                print(b)
            """);
        helper.CreateProjectFile();

        var result = helper.Compile();

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains("int32"));
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
