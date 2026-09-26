using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The class-defense matrix for #1683/#1802 universal <c>global::</c> qualification: every emitted
/// reference to a module-level member (and every same-file/cross-module type reference) is spelled
/// so it binds the RIGHT declaration regardless of a name collision with the root namespace, a CLR
/// type, or a using directive — and NO <c>using static</c> directive is emitted (the #1683 close
/// criterion, plan-d35e69 line 305).
///
/// <para>Settled spellings this matrix locks (measured @ HEAD with the built sharpyc):
/// <list type="bullet">
///   <item><b>Value position</b> (module function callee, const/variable/static-field read, nested
///     enum member, parameter default): <c>global::&lt;Module&gt;.&lt;member&gt;</c>.</item>
///   <item><b>Construction</b> (<c>new</c>): <c>global::&lt;Module&gt;.&lt;Type&gt;</c>, cross-module
///     too.</item>
///   <item><b>Type position, same-file, no collision</b> (annotation, catch-clause, isinstance /
///     type-test — the family-4 cell): the SHORT in-scope name, deliberately NOT qualified.</item>
///   <item><b>Type position, same-file, name collides with a CLR type</b> (<c>class Console</c>):
///     <c>global::&lt;Module&gt;.&lt;Type&gt;</c> — qualified to dodge <c>System.Console</c>.</item>
///   <item><b>Cross-module type annotation</b>: <c>&lt;RootNs&gt;.&lt;Module&gt;.&lt;Type&gt;</c>
///     (namespace-qualified; the <c>new</c> form additionally carries <c>global::</c>).</item>
///   <item><b>User-module import</b> in every form (<c>import m</c>, <c>import m as x</c>,
///     <c>from m import f</c>, <c>from m import f as g</c>, <c>import pkg.sub</c>): the member is
///     emitted INLINE as <c>global::&lt;RootNs&gt;.&lt;Module&gt;.&lt;Member&gt;</c> — no directive.</item>
///   <item><b>Stdlib whole-module import</b> (<c>import os</c>): a <c>using os =
///     global::Sharpy.OsModule.OsModuleModule;</c> ALIAS directive (a <c>using X = Y;</c>, NOT a
///     <c>using static</c>) + <c>os.Getcwd()</c>; a stdlib member/wildcard import inlines the
///     <c>global::Sharpy.&lt;Stem&gt;.&lt;X&gt;.&lt;member&gt;</c> reference — a spy-sourced stdlib
///     module is a namespace holding its members class <c>&lt;X&gt;</c> (#2039).</item>
/// </list></para>
///
/// <para>Every cell EXECUTES through the real pipeline (a raw emitter cannot emit a reference at
/// all, #1622), asserts its settled spelling as a positive control, and rides a non-vacuous
/// <c>DoesNotContain("using static")</c> whose positive control is that same <c>global::</c>
/// assertion. Reverting the qualifier makes the <c>global::</c> reference go bare — the assertion
/// reds — without re-introducing a directive (mutation recorded in the commit body).</para>
///
/// <para>This file's single-file cells use <see cref="IntegrationTestBase.CompileAndExecute"/>
/// (module class = PascalCase of the file stem, here <c>Prog</c>); the cross-module, collision, and
/// import-form cells use <see cref="ProjectCompilationHelper"/> (a real <c>.spyproj</c> build).</para>
/// </summary>
public class ModuleMemberQualificationMatrixTests : StdlibAwareIntegrationTestBase
{
    public ModuleMemberQualificationMatrixTests(ITestOutputHelper output) : base(output) { }

    // The #1683 close-criterion invariant, applied to every cell: no `using static` is ever emitted.
    // Guarded against vacuity by each cell's positive `global::` assertion (a real reference is
    // present to be mis-qualified). `using static ` (trailing space) is exact — a `using System;`
    // namespace-import or a `using os = ...;` alias is a different construct and stays legal.
    private const string UsingStatic = "using static ";

    // ═══════════════════════════════════════════════════════════════════════
    // Single-file cells — module class `Prog`. member × position, plus the
    // same-file type-position cells (annotation, construction, isinstance,
    // catch) that stay SHORT, and the CLR-collision cell that qualifies.
    // ═══════════════════════════════════════════════════════════════════════

    private const string SingleFileModule = """
        const LIMIT: int = 5

        count: int = 0

        def helper() -> int:
            return LIMIT

        class Widget:
            @static
            total: int = 3

            def make() -> int:
                return 7

            enum Color:
                Red = 1
                Green = 2

            class Inner:
                v: int
                def __init__(self, v: int):
                    self.v = v

        def with_default(x: int = LIMIT) -> int:
            return x

        def main() -> None:
            print(helper())
            print(LIMIT)
            print(count)
            count = 4
            print(count)
            print(Widget.make())
            print(Widget.total)
            c: Widget.Color = Widget.Color.Green
            i: Widget.Inner = Widget.Inner(9)
            print(i.v)
            print(with_default())
        """;

    public static IEnumerable<object[]> SingleFileValueCells()
    {
        // member callee, const read, module-variable read, static method callee, static field read,
        // nested enum member (value position), and a parameter default that references a module
        // const — all VALUE positions → global::. (The write-through store is exercised separately
        // below: within the module class its target binds in scope and is emitted bare, so it is not
        // a global:: value cell.)
        yield return new object[] { "module_function_callee", "global::Prog.ProgModule.Helper()" };
        yield return new object[] { "module_const_read", "global::Prog.ProgModule.LIMIT" };
        yield return new object[] { "module_variable_read", "global::Prog.ProgModule.Count" };
        yield return new object[] { "static_method_callee", "global::Prog.Widget.Make()" };
        yield return new object[] { "static_field_read", "global::Prog.Widget.Total" };
        yield return new object[] { "nested_enum_member", "global::Prog.Widget.Color.Green" };
        yield return new object[] { "parameter_default_module_const", "= global::Prog.ProgModule.LIMIT" };
    }

    [Theory]
    [MemberData(nameof(SingleFileValueCells))]
    public void SingleFile_ValuePosition_IsGlobalQualified(string id, string expectedSpelling)
    {
        Output.WriteLine($"[single-file value] {id} → {expectedSpelling}");
        var result = CompileAndExecute(SingleFileModule, "prog.spy");

        result.Success.Should().BeTrue(
            $"{id}: the specimen must compile and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("5\n5\n0\n4\n7\n3\n9\n5\n",
            $"{id}: the discriminating printed values (python3-matching)");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain(expectedSpelling,
            $"{id}: a module-level member in value position must be emitted global::-qualified so it "
            + "binds its declaration regardless of a namespace/CLR collision (#1683, #1802)");
        cs.Should().NotContain(UsingStatic,
            $"{id}: #1683 close criterion — no `using static` is emitted (plan line 305)");
    }

    [Fact]
    public void SingleFile_ModuleVariableStore_BindsInScope_AndRoundTrips()
    {
        // The write-through store position (#1786): a module-level variable assigned from the
        // module's own entry point. The READ is global::-qualified (global::Prog.Count, asserted in
        // the value theory); the store TARGET binds in scope inside the module class and is emitted
        // bare (`Count = 4`) — correct, because Main is a static method of the same Prog class. The
        // store is verified by the round-trip: print(count) is 0 before and 4 after.
        var result = CompileAndExecute(SingleFileModule, "prog.spy");
        result.Success.Should().BeTrue(
            "the specimen must compile and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain("0\n4\n",
            "the module variable reads 0, is stored 4, then reads back 4 — the store took effect");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("Count = 4;",
            "the store target of a module variable, written from the module's own class, binds in "
            + "scope and is emitted bare (#1786 is a CROSS-class-member concern, not this same-class "
            + "store)");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    // ── Same-file type-position cells: annotation, construction, isinstance, catch. These stay
    //    SHORT (the in-scope name), EXCEPT a name that collides with a CLR type. The single positive
    //    control that MUST carry global:: (construction of a nested type) is checked too.

    [Fact]
    public void SingleFile_SameFileTypeAnnotationAndConstruction_StayShort()
    {
        // A same-file nested-type ANNOTATION and CONSTRUCTION with no CLR collision keep the short
        // in-scope name — universal qualification does not gratuitously qualify a same-file type
        // reference that already binds correctly. (The value-position reads in the same program are
        // the global:: positive control, asserted above.)
        var result = CompileAndExecute(SingleFileModule, "prog.spy");
        result.Success.Should().BeTrue(
            "the specimen must compile. Errors:\n" + string.Join("\n", result.CompilationErrors));

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("Widget.Inner i = new global::Prog.Widget.Inner(9)",
            "a same-file nested-type annotation + construction with no collision stays short");
        cs.Should().Contain("Widget.Color c = global::Prog.Widget.Color.Green",
            "the annotation is short (Widget.Color) while the enum MEMBER read in value position is "
            + "global::-qualified — position, not identity, decides");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void SingleFile_IsInstanceOperand_StaysShort_Family4()
    {
        // family-4 (plan lines 441/437): a same-file type in a type-TEST position (isinstance /
        // `b is Box`) binds in module context and needs no directive, so it stays SHORT. LOCK it —
        // a regression that "universally" qualified type tests would change this.
        const string source = """
            class Box:
                v: int
                def __init__(self, v: int):
                    self.v = v

            def check(o: object) -> bool:
                return isinstance(o, Box)

            def main() -> None:
                b: Box = Box(1)
                print(check(b))
            """;
        var result = CompileAndExecute(source, "prog.spy");
        result.Success.Should().BeTrue(
            "the isinstance specimen must compile and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("True\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("is Box",
            "a same-file type test stays short (family 4) — it binds in module context (#1683)");
        cs.Should().NotContain("is global::Prog.Box",
            "the type-test operand must NOT be gratuitously qualified");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void SingleFile_CatchClauseSameFileType_StaysShort_ButConstructionIsQualified()
    {
        // A same-file exception type in a catch clause is a type position → SHORT (`catch (MyError`),
        // while `raise`/construction is a value/new position → global::-qualified. The two together
        // are the plan line 434 catch-clause cell (QualifySameFileTypeName gives the short name for a
        // same-file type; only a collision would qualify it).
        const string source = """
            class MyError(Exception):
                def __init__(self, msg: str):
                    super().__init__(msg)

            def risky(n: int) -> int:
                if n < 0:
                    raise MyError("neg")
                return n

            def main() -> None:
                try:
                    print(risky(-1))
                except MyError as e:
                    print("caught")
            """;
        var result = CompileAndExecute(source, "prog.spy");
        result.Success.Should().BeTrue(
            "the catch specimen must compile and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("caught\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("catch (MyError",
            "a same-file exception type in a catch clause stays short (#1683, plan line 434)");
        cs.Should().Contain("new global::Prog.MyError(",
            "construction of the same-file exception is global::-qualified — the positive control "
            + "that the catch-clause short name is a POSITION decision, not a missed qualification");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void SingleFile_TypeNameCollidingWithClrType_IsQualified()
    {
        // A same-file type whose name collides with an ambient CLR type (`Console` vs System.Console)
        // MUST be global::-qualified even in type position, or it would bind System.Console. This is
        // the collision arm of #1683 applied to a same-file type.
        const string source = """
            class Console:
                v: int
                def __init__(self, v: int):
                    self.v = v

            def main() -> None:
                c: Console = Console(3)
                print(c.v)
            """;
        var result = CompileAndExecute(source, "prog.spy");
        result.Success.Should().BeTrue(
            "the Console-collision specimen must compile and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("3\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("global::Prog.Console c = new global::Prog.Console(3)",
            "a same-file type whose name collides with System.Console must be global::-qualified in "
            + "BOTH annotation and construction, or it binds the CLR type (#1683)");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    // ── Stdlib access cells: the 18-fixture regression (plan line 431). Whole-module `import os`
    //    binds a `using os = global::Sharpy.OsModule.OsModuleModule;` ALIAS (not `using static`); a
    //    member/wildcard import inlines the global::Sharpy.<Stem>.<X>.<member> reference (#2039: a
    //    spy-sourced stdlib module is a namespace holding its members class <X>).

    [Fact]
    public void StdlibWholeModuleImport_BindsAGlobalQualifiedAlias_NotUsingStatic()
    {
        const string source = """
            import os

            def main() -> None:
                p: str = os.getcwd()
                print(len(p) >= 0)
            """;
        var result = CompileAndExecute(source, "prog.spy");
        result.Success.Should().BeTrue(
            "stdlib whole-module access must compile and run — the 18-fixture regression (#1683). "
            + "Errors:\n" + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("True\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("using os = global::Sharpy.OsModule.OsModuleModule;",
            "a stdlib whole-module import binds a global::-qualified using-ALIAS to the real module "
            + "class (global::Sharpy.OsModule.OsModuleModule, #2039), not a bare `global::Os` "
            + "(#1683, fix 4a6f9c4e5)");
        cs.Should().NotContain(UsingStatic,
            "the alias is a `using X = Y;`, never a `using static` — #1683 close criterion");
    }

    [Fact]
    public void StdlibMemberAndWildcardImport_InlineGlobalQualifiedReference()
    {
        const string memberImport = """
            from os import getcwd

            def main() -> None:
                print(len(getcwd()) >= 0)
            """;
        var member = CompileAndExecute(memberImport, "prog.spy");
        member.Success.Should().BeTrue(
            "stdlib member import must compile and run. Errors:\n"
            + string.Join("\n", member.CompilationErrors));
        member.StandardOutput.Should().Be("True\n");
        member.GeneratedCSharp!.Should().Contain("global::Sharpy.OsModule.OsModuleModule.Getcwd()",
            "a stdlib member import inlines the fully-qualified reference (#1683)");
        member.GeneratedCSharp!.Should().NotContain(UsingStatic, "#1683 close criterion");

        const string wildcardImport = """
            from math import *

            def main() -> None:
                print(sqrt(16.0))
            """;
        var wildcard = CompileAndExecute(wildcardImport, "prog.spy");
        wildcard.Success.Should().BeTrue(
            "stdlib wildcard import must compile and run — #1896 (wildcard member identity). "
            + "Errors:\n" + string.Join("\n", wildcard.CompilationErrors));
        wildcard.StandardOutput.Should().Be("4.0\n");
        wildcard.GeneratedCSharp!.Should().Contain("global::Sharpy.MathModule.MathModuleModule.Sqrt(",
            "a wildcard-imported stdlib member inlines the fully-qualified reference (#1683, #1896)");
        wildcard.GeneratedCSharp!.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Cross-module / project cells (ProjectCompilationHelper) — a real .spyproj
    // build. Root namespace `Test`, lib module class `Lib`.
    // ═══════════════════════════════════════════════════════════════════════

    private sealed record ProjectRun(
        Sharpy.Compiler.Tests.Helpers.ExecutionResult Exec, string GeneratedCSharp);

    /// <summary>
    /// Builds and executes a two-or-more-file project through the real ProjectCompiler, returning
    /// the execution result and the concatenated generated C# (read from the compilation result the
    /// same run produced — no second compile).
    /// </summary>
    private ProjectRun RunProject(string rootNamespace, params (string name, string content)[] files)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace(rootNamespace).WithEntryPoint("main.spy");
        foreach (var (name, content) in files)
            helper.AddSourceFile(name, content);
        helper.CreateProjectFile();

        var exec = helper.CompileAndExecute();
        var cs = helper.LastCompilationResult != null
            ? string.Concat(helper.LastCompilationResult.GeneratedCSharpFiles.Values)
            : string.Empty;
        return new ProjectRun(exec, cs);
    }

    private const string CrossModuleLib = """
        const CAP: int = 8

        def compute() -> int:
            return CAP

        class Box:
            tag: int
            def __init__(self, tag: int):
                self.tag = tag

            enum Kind:
                A = 1
                B = 2
        """;

    public static IEnumerable<object[]> CrossModuleValueCells()
    {
        // cross-module function callee + const read → global::<RootNs>.<LibModule>.<member>.
        yield return new object[] { "cross_module_function_callee", "global::Test.Lib.LibModule.Compute()" };
        yield return new object[] { "cross_module_const_read", "global::Test.Lib.LibModule.CAP" };
    }

    [Theory]
    [MemberData(nameof(CrossModuleValueCells))]
    public void CrossModule_ValuePosition_IsGlobalQualified(string id, string expectedSpelling)
    {
        Output.WriteLine($"[cross-module value] {id} → {expectedSpelling}");
        var main = """
            from lib import compute, CAP

            def main() -> None:
                print(compute())
                print(CAP)
            """;
        var run = RunProject("Test", ("lib.spy", CrossModuleLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            $"{id}: the cross-module project must compile and run. Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("8\n8\n");

        run.GeneratedCSharp.Should().Contain(expectedSpelling,
            $"{id}: a cross-module member reference (defining module ≠ current) must be emitted "
            + "global::-qualified so no using set can shadow it (#1683, #1802)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, $"{id}: #1683 close criterion");
    }

    [Fact]
    public void CrossModule_NestedEnumMember_IsGlobalQualifiedToItsDefiningModule()
    {
        // Post-#1899 (fix 5a8c35559): a cross-module nested-enum member rides the enclosing TYPE's
        // cross-module qualification, which is now global::-rooted (global::Test.Lib.Box.Kind.A) —
        // so a same-named local entity cannot shadow the namespace path (the #1899 collision).
        var main = """
            from lib import Box

            def main() -> None:
                print(Box.Kind.A == Box.Kind.A)
            """;
        var run = RunProject("Test", ("lib.spy", CrossModuleLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "the cross-module nested-enum cell must compile and run. Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("True\n");

        run.GeneratedCSharp.Should().Contain("global::Test.Lib.Box.Kind.A",
            "a cross-module nested-enum member is global::-qualified to its defining module "
            + "(#1683, #1802, #1899)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void CrossModule_TypeAnnotationAndConstruction_AreQualified()
    {
        var main = """
            from lib import Box

            def main() -> None:
                b: Box = Box(4)
                print(b.tag)
            """;
        var run = RunProject("Test", ("lib.spy", CrossModuleLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "the cross-module type cell must compile and run. Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("4\n");

        // Post-#1899 (fix 5a8c35559): a cross-module type is global::-rooted in BOTH annotation and
        // construction, so no same-named local entity can shadow the qualification path.
        run.GeneratedCSharp.Should().Contain("global::Test.Lib.Box b = new global::Test.Lib.Box(4)",
            "a cross-module type is global::-qualified to its defining module in both annotation and "
            + "construction (#1683, #1802, #1899)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    // ── #1683 collision cells: a module-level member whose name equals the root namespace, or a CLR
    //    namespace/type, must still bind its own declaration across the module boundary — the CS0118
    //    (function/class == RootNamespace) and namespace-collision cases the deleted using-static
    //    used to mask.

    [Fact]
    public void Collision_FunctionNameEqualsRootNamespace_BindsAcrossModules()
    {
        // The #1683 p1 repro: RootNamespace `Poison`, `def poison()` in lib, called bare through a
        // from-import in main. Before universal qualification this was `using static Poison.Lib;` +
        // `Poison()` → CS0118 (the namespace `Poison` and the method collided).
        var lib = """
            def poison() -> str:
                return "poisoned"
            """;
        var main = """
            from lib import poison

            def main() -> None:
                print(poison())
            """;
        var run = RunProject("Poison", ("lib.spy", lib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "func==RootNamespace must build and run (no CS0118). Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("poisoned\n");
        run.GeneratedCSharp.Should().Contain("global::Poison.Lib.LibModule.Poison()",
            "the call is global::-qualified through the module class, so the root-namespace "
            + "collision cannot bind the namespace instead of the method (#1683)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void Collision_ClassNameEqualsRootNamespace_BindsAcrossModules()
    {
        // Class name == RootNamespace, imported and constructed across the module boundary.
        var lib = """
            class Widget:
                tag: int
                def __init__(self, tag: int):
                    self.tag = tag
            """;
        var main = """
            from lib import Widget

            def main() -> None:
                w: Widget = Widget(5)
                print(w.tag)
            """;
        var run = RunProject("Widget", ("lib.spy", lib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "class==RootNamespace must build and run. Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("5\n");
        run.GeneratedCSharp.Should().Contain("new global::Widget.Lib.Widget(5)",
            "the cross-module construction is global::-qualified through the defining module, so the "
            + "root-namespace/type name collision resolves to the class (#1683)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void CrossModuleType_UnderRootNamespaceSegmentCollision_BindsThroughGlobalRoot()
    {
        // The TYPE analog of the #1683 poison-MEMBER repro (#1899): root namespace `Poison`, lib
        // defines `class Box` (+ nested enum Kind), and the referencing module declares a
        // `Poison`-named class that SHADOWS the root-namespace segment. A cross-module type
        // ANNOTATION (`b: Box`) and nested-enum MEMBER (`Box.Kind.A`) emit `Poison.Lib.Box[.Kind.A]`
        // WITHOUT global:: — so the local `Poison` class shadows the namespace and the reference
        // binds `Program.Poison.Lib` → CS0426/CS0117 (SPY0908). The CONSTRUCTION path is already
        // global::-rooted and safe; only the annotation + nested-enum-member arms were missed.
        //
        // Asserts the CORRECT post-#1899 behavior (compiles, runs, both paths global::-rooted); it is
        // Skip'd until the codegen fix (TypeSyntaxMapper annotation path + nested-enum-member access →
        // global::, mirroring dded46723's union-host BuildNestedTypeName fix) lands. Unskip then.
        var lib = """
            class Box:
                tag: int
                def __init__(self, tag: int):
                    self.tag = tag

                enum Kind:
                    A = 1
                    B = 2
            """;
        var main = """
            from lib import Box

            class Poison:
                x: int
                def __init__(self, x: int):
                    self.x = x

            def main() -> None:
                p: Poison = Poison(1)
                b: Box = Box(4)
                print(b.tag)
                print(Box.Kind.A == Box.Kind.A)
                print(p.x)
            """;
        var run = RunProject("Poison", ("lib.spy", lib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "a cross-module type reference must bind through the global:: root even when a same-named "
            + "entity shadows the root-namespace segment in the referencing scope (#1899, #1683). "
            + "Errors:\n" + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("4\nTrue\n1\n");

        run.GeneratedCSharp.Should().Contain("global::Poison.Lib.Box b",
            "the cross-module type ANNOTATION must be global::-rooted so the local Poison class "
            + "cannot shadow the namespace segment (#1899)");
        run.GeneratedCSharp.Should().Contain("global::Poison.Lib.Box.Kind.A",
            "the cross-module nested-enum MEMBER must be global::-rooted too (#1899)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    // ── Import-form cells: every USER-module import form inlines the reference as
    //    global::<RootNs>.<Module>.<Member> (no directive), including dotted-package and aliased
    //    forms — the import kinds the class-2 deletion relocated here (plan lines 437/444).

    public static IEnumerable<object[]> UserImportFormCells()
    {
        // (main body, expected qualified reference). The lib has `compute`/`other`; pkg/sub has
        // `helper`. Each form must inline global::Test.<Module>.<Member> with NO directive.
        yield return new object[]
        {
            "whole_module_import",
            "import lib\n\ndef main() -> None:\n    print(lib.compute())\n",
            "global::Test.Lib.LibModule.Compute()", "8\n"
        };
        yield return new object[]
        {
            "whole_module_import_as_alias",
            "import lib as l\n\ndef main() -> None:\n    print(l.other())\n",
            "global::Test.Lib.LibModule.Other()", "9\n"
        };
        yield return new object[]
        {
            "from_import_member_as_alias",
            "from lib import compute as c\n\ndef main() -> None:\n    print(c())\n",
            "global::Test.Lib.LibModule.Compute()", "8\n"
        };
        yield return new object[]
        {
            "dotted_package_import",
            "import pkg.sub\n\ndef main() -> None:\n    print(pkg.sub.helper())\n",
            "global::Test.Pkg.Sub.SubModule.Helper()", "42\n"
        };
        yield return new object[]
        {
            "dotted_from_import",
            "from pkg.sub import helper\n\ndef main() -> None:\n    print(helper())\n",
            "global::Test.Pkg.Sub.SubModule.Helper()", "42\n"
        };
    }

    [Theory]
    [MemberData(nameof(UserImportFormCells))]
    public void UserImportForm_InlinesGlobalQualifiedReference_NoDirective(
        string id, string main, string expectedSpelling, string expectedStdout)
    {
        Output.WriteLine($"[import form] {id} → {expectedSpelling}");
        var run = RunProject(
            "Test",
            ("lib.spy", "def compute() -> int:\n    return 8\n\n\ndef other() -> int:\n    return 9\n"),
            ("pkg/__init__.spy", ""),
            ("pkg/sub.spy", "def helper() -> int:\n    return 42\n"),
            ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            $"{id}: the import-form project must compile and run. Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be(expectedStdout);

        run.GeneratedCSharp.Should().Contain(expectedSpelling,
            $"{id}: a user-module import (every form) inlines the reference global::-qualified to its "
            + "defining module — no using-directive to be shadowed (#1683, #1802)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, $"{id}: #1683 close criterion");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // #1802 file == class merge + the remaining name-collision cells.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FileNamedClass_MemberReference_IsTheNamespaceThenTheClass()
    {
        // #1802 → #2039: foo.spy declaring `class Foo` no longer merges (M2) — the module is namespace
        // Foo, its members class FooModule, and `class Foo` a sibling. A reference to the class's
        // const / static field / static method is `global::Foo.Foo.<member>`: the namespace, then
        // the class. The single-segment `global::Foo.K` spelled the retired merge.
        const string source = """
            class Foo:
                const K: int = 10

                @static
                count: int = 0

                def make() -> int:
                    return 7

            def main() -> None:
                print(Foo.K)
                print(Foo.count)
                print(Foo.make())
            """;
        var result = CompileAndExecute(source, "foo.spy");
        result.Success.Should().BeTrue(
            "the file==class specimen must compile and run (no CS0117). Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("10\n0\n7\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("global::Foo.Foo.K",
            "the const of a class named like its file is reached through the module namespace (#2039)");
        cs.Should().Contain("global::Foo.Foo.Make()",
            "the static method likewise (#2039)");
        cs.Should().NotContain("global::Foo.K",
            "the merged single-segment spelling is retired with the merge (#2039, M2)");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    public static IEnumerable<object[]> CollisionFunctionNameCells()
    {
        // A module function whose name equals a CLR namespace/type: it must be emitted
        // global::<Module>.<Name>() so it binds the user function, not System / System.String.
        yield return new object[] { "def_system", "system", "System", "global::Coll.CollModule.System()" };
        yield return new object[] { "def_string", "string", "String", "global::Coll.CollModule.String()" };
    }

    [Theory]
    [MemberData(nameof(CollisionFunctionNameCells))]
    public void Collision_FunctionNameEqualsClrName_IsGlobalQualified(
        string id, string spyName, string csName, string expectedSpelling)
    {
        Output.WriteLine($"[collision] {id} → {expectedSpelling}");
        var source =
            $"def {spyName}() -> str:\n    return \"ok\"\n\n\ndef main() -> None:\n    print({spyName}())\n";
        var result = CompileAndExecute(source, "coll.spy");

        result.Success.Should().BeTrue(
            $"{id}: a function whose name collides with a CLR name must build and run. Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("ok\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain(expectedSpelling,
            $"{id}: the call is global::-qualified through the module class, so it binds the user "
            + $"function `{csName}`, not the CLR namespace/type of the same name (#1683)");
        cs.Should().NotContain(UsingStatic, $"{id}: #1683 close criterion");
    }

    // ── #1907: the module-qualified UNION CASE CONSTRUCTOR takes the same lowering as the bare
    //    spelling. `from lib import U` + `U.A()` emitted `new global::<Ns>.Lib.U.A()` and ran, while
    //    `import lib` + `lib.U.A()` emitted a plain INVOCATION and reached Roslyn as CS1955
    //    ("Non-invocable member 'Lib.U.A' cannot be used like a method") behind SPY0908 — the
    //    emitter re-derived the union from the callee's SHAPE, and that derivation answers only for
    //    shapes whose root is a type name. The checker resolved both spellings identically all
    //    along (both reach CheckUnionCaseConstruction with the same case and union symbols), so the
    //    cure is the recorded fact the emitter now reads (SemanticInfo.GetUnionCaseConstruction).

    private const string QualifiedUnionLib = """
        union U:
            case A
            case B(x: int)

        class Outer:
            union V:
                case X
                case Y(n: int)
        """;

    [Fact]
    public void QualifiedUnionCase_NoPayload_ConstructsAndMatches()
    {
        var main = """
            import lib

            def main() -> None:
                a: lib.U = lib.U.A()
                match a:
                    case A:
                        print("A")
                    case B(x):
                        print(x)
            """;
        var run = RunProject("Test", ("lib.spy", QualifiedUnionLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "the module-qualified no-payload case constructor must compile and run (#1907). Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("A\n");

        run.GeneratedCSharp.Should().Contain("new global::Test.Lib.U.A()",
            "the qualified spelling lowers to a CONSTRUCTION, the same one the bare spelling takes "
            + "— a plain invocation here is CS1955 behind SPY0908 (#1907)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void QualifiedUnionCase_WithPayload_ConstructsAndMatches()
    {
        var main = """
            import lib

            def main() -> None:
                b: lib.U = lib.U.B(7)
                match b:
                    case A:
                        print("A")
                    case B(x):
                        print(x)
            """;
        var run = RunProject("Test", ("lib.spy", QualifiedUnionLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "the module-qualified payload case constructor must compile and run (#1907). Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("7\n");

        run.GeneratedCSharp.Should().Contain("new global::Test.Lib.U.B(7)");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void QualifiedUnionCase_NestedInAClass_ConstructsAndMatches()
    {
        // Three qualification segments (module → class → union). A COVERAGE cell, not a guard for
        // the recorded fact: this spelling's access object (`lib.Outer.V`) is one the shape
        // derivation CAN resolve (module → exported type → nested type), so it stays green when the
        // emitter's fact read is reverted — recorded here so it is not mistaken for a falsifiable
        // guard. The three cells that red under that mutation are the two `lib.U.<case>()` cells
        // and the qualified-vs-bare comparison.
        var main = """
            import lib

            def main() -> None:
                n: lib.Outer.V = lib.Outer.V.X()
                match n:
                    case X:
                        print("X")
                    case Y(k):
                        print(k)
            """;
        var run = RunProject("Test", ("lib.spy", QualifiedUnionLib), ("main.spy", main));

        run.Exec.Success.Should().BeTrue(
            "a module-qualified NESTED union's case constructor must compile and run (#1907). Errors:\n"
            + string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Should().Be("X\n");

        run.GeneratedCSharp.Should().Contain("new global::Test.Lib.Outer.V.X()");
        run.GeneratedCSharp.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    [Fact]
    public void QualifiedAndBareUnionCase_EmitTheSameConstruction()
    {
        // The class statement itself: the two spellings of ONE construction differ only in how the
        // union symbol was reached, so they must produce byte-identical lowerings. A cell that
        // asserted only the qualified spelling would pass again if the bare one regressed to match
        // it.
        var qualified = RunProject("Test", ("lib.spy", QualifiedUnionLib), ("main.spy", """
            import lib

            def main() -> None:
                a: lib.U = lib.U.A()
                print(1)
            """));
        var bare = RunProject("Test", ("lib.spy", QualifiedUnionLib), ("main.spy", """
            from lib import U

            def main() -> None:
                a: U = U.A()
                print(1)
            """));

        qualified.Exec.Success.Should().BeTrue(string.Join("\n", qualified.Exec.CompilationErrors));
        bare.Exec.Success.Should().BeTrue(string.Join("\n", bare.Exec.CompilationErrors));

        const string construction = "new global::Test.Lib.U.A()";
        qualified.GeneratedCSharp.Should().Contain(construction);
        bare.GeneratedCSharp.Should().Contain(construction,
            "the bare spelling is the control: both routes take ONE lowering (#1907)");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Subdirectory axis (#1932 → #1948, R-AX phase c): each directory above a file is a C# NAMESPACE
    // segment and the file's module class is declared in it, so a directory and a module (or a
    // directory and a directory) spelled alike never nest a class in a same-named class. The helper
    // lays sources out under `src/`, so the segments are measured from `src/` (the common source
    // root), not the project dir. Prior commit (direction): 6 rows + both adjacent-directory cells
    // were SPY0526 (CS0542 behind SPY0908 before #1932) — refused → runs.
    // ═══════════════════════════════════════════════════════════════════════

    private const string LibF = "def f() -> int:\n    return 7\n";
    private const string M7Main = "def main() -> None:\n    print(7)\n";

    public static IEnumerable<object[]> SubdirectoryLayouts() => new[]
    {
        // name, files (path → content); every layout runs and prints 7
        new object[] { "dir_differs_from_file", new[] { ("pkg/lib.spy", LibF), ("main.spy", "from pkg.lib import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "dir_equals_file", new[] { ("lib/lib.spy", LibF), ("main.spy", "from lib.lib import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "dir_equals_file_with_init", new[] { ("lib/__init__.spy", "X: int = 1\n"), ("lib/lib.spy", LibF), ("main.spy", "from lib.lib import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "nested_dir_equals_file", new[] { ("a/b/b.spy", LibF), ("main.spy", "from a.b.b import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "unimported_dir_equals_file", new[] { ("lib/lib.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") } },
        new object[] { "init_only", new[] { ("lib/__init__.spy", LibF), ("main.spy", "from lib import f\n\ndef main() -> None:\n    print(f())\n") } },
        // Discriminates the ROOT the segments are measured from: under the common source root
        // (`src/`) a root-level src.spy has no namespace segment.
        new object[] { "file_named_like_source_root", new[] { ("src.spy", LibF), ("main.spy", "from src import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "non_adjacent_equal_dirs", new[] { ("a/b/a/x.spy", LibF), ("main.spy", "from a.b.a.x import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "init_in_equal_dirs", new[] { ("a/a/__init__.spy", LibF), ("main.spy", "from a.a import f\n\ndef main() -> None:\n    print(f())\n") } },
        new object[] { "mangling_equal", new[] { ("my_lib/myLib.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") } },
    };

    [Theory]
    [MemberData(nameof(SubdirectoryLayouts))]
    public void Subdirectory_EveryLayout_Runs(string name, (string, string)[] files)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        foreach (var (path, content) in files)
            helper.AddSourceFile(path, content);
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeTrue($"[{name}] {string.Join("\n", exec.CompilationErrors)}");
        exec.StandardOutput.Trim().Should().Be("7", $"[{name}]");
    }

    /// <summary>
    /// Adjacent directories that mangle alike (audit R3): `a/a/x.spy` is namespace `A.A` — legal C#
    /// (SPY0526 while directories were wrapper classes, CS0542 behind SPY0908 before that).
    /// </summary>
    [Theory]
    [InlineData("a/a/x.spy", "a.a.x")]
    [InlineData("my_pkg/myPkg/x.spy", "my_pkg.myPkg.x")]
    public void Subdirectory_AdjacentEqualDirectories_Run(string path, string module)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile(path, LibF);
        helper.AddSourceFile("main.spy", $"from {module} import f\n\ndef main() -> None:\n    print(f())\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeTrue($"[{path}] {string.Join("\n", exec.CompilationErrors)}");
        exec.StandardOutput.Trim().Should().Be("7", path);
    }

    [Fact]
    public void Subdirectory_Axis_IsTotal()
        => SubdirectoryLayouts().Should().HaveCount(10);

    /// <summary>
    /// The `__init__` member FQN cell (Decision 28 (a)(b), ruling X3): a package's module class is
    /// <c>&lt;X&gt;</c> = <c>PkgModule</c> declared INSIDE <c>namespace Simple.Pkg</c>, and a submodule
    /// of the package is its own namespace <c>Simple.Pkg.Lib</c> holding <c>LibModule</c> (#2039).
    /// Literals, not the helper: the spelling is the contract.
    /// </summary>
    [Fact]
    public void InitMembers_LiveInPkgModuleInsideThePackageNamespace()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile("pkg/__init__.spy", "def init_fn() -> int:\n    return 5\n");
        helper.AddSourceFile("pkg/lib.spy", LibF);
        helper.AddSourceFile("main.spy", "import pkg\nfrom pkg import init_fn\nfrom pkg.lib import f\n\n"
            + "def main() -> None:\n    print(init_fn())\n    print(pkg.init_fn())\n    print(f())\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeTrue(string.Join("\n", exec.CompilationErrors));
        exec.StandardOutput.Trim().Should().Be("5\n5\n7");

        var files = helper.LastCompilationResult!.GeneratedCSharpFiles;
        var classes = files.Values
            .SelectMany(cs => CSharpSyntaxTree.ParseText(cs).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Select(c => ((c.Parent as BaseNamespaceDeclarationSyntax)?.Name.ToString(), c.Identifier.Text))
            .ToList();
        classes.Should().Contain(("Simple.Pkg", "PkgModule"), "the __init__ module class is <X> in its package namespace");
        classes.Should().Contain(("Simple.Pkg.Lib", "LibModule"), "a submodule is a namespace with its own <X> (#2039)");
        classes.Should().NotContain(c => c.Item2 == "Pkg", "no directory is a class any more");
        var main = files.Single(kv => Path.GetFileName(kv.Key) == "main.cs").Value;
        main.Should().Contain("global::Simple.Pkg.PkgModule.InitFn()",
            "both the from-imported and the module-qualified reference reach <X>");
        main.Should().NotContain("global::Simple.Pkg.InitFn");
    }

    /// <summary>
    /// Every reader of a package's container spells <c>&lt;X&gt;</c> (#1948): the from-import member
    /// qualifier (F6), the __init__ re-export forwarder of a SUBPACKAGE's member (F13: <c>from .sub
    /// import g</c> reaches <c>Pkg.Sub.SubModule</c>), and __init__ types — the package's own and a
    /// subpackage's re-exported by the parent (<c>Crate</c>) — in an annotation and a construction (the
    /// type-naming seam spells both from the defining file). F6/F13 spelled the package from its dotted
    /// name — the directory, now a namespace — before; the recorded import file is what finds the
    /// members class. python3: 7 / 3 / 4 / 5.
    /// </summary>
    [Fact]
    public void PackageReExportsAndTypes_ReachTheMembersClass()
    {
        var run = RunProject("Simple",
            ("pkg/__init__.spy", "from .lib import f\nfrom .sub import g, Crate\n\nclass Box:\n    v: int\n\n"
                + "    def __init__(self, v: int) -> None:\n        self.v = v\n"),
            ("pkg/lib.spy", LibF),
            ("pkg/sub/__init__.spy", "def g() -> int:\n    return 3\n\nclass Crate:\n    n: int\n\n"
                + "    def __init__(self, n: int) -> None:\n        self.n = n\n"),
            ("main.spy", "from pkg import f, g, Box, Crate\n\ndef main() -> None:\n    b: Box = Box(4)\n"
                + "    c: Crate = Crate(5)\n    print(f())\n    print(g())\n    print(b.v)\n    print(c.n)\n"));

        run.Exec.Success.Should().BeTrue(string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Trim().Should().Be("7\n3\n4\n5");
        run.GeneratedCSharp.Should().Contain("Simple.Pkg.Sub.SubModule.G()",
            "the __init__ forwarder of a subpackage member names the subpackage's members class");
    }

    /// <summary>
    /// Ruling 15 in its P14b cell: a package's <c>__init__</c> function spelled like its members class
    /// (<c>def pkg_module</c> → <c>PkgModule</c> inside class <c>PkgModule</c>, CS0542) is SPY0523 — the
    /// module-class collision rule reading the same authority. Prior commit: the class was <c>Pkg</c>,
    /// so this ran (worked → refused, a consequence of X3). The positive control is the FQN cell above.
    /// </summary>
    [Fact]
    public void InitFunctionSpelledLikeTheMembersClass_IsSpy0523()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile("pkg/__init__.spy", "def pkg_module() -> int:\n    return 1\n");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(7)\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeFalse();
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        errors.Should().ContainSingle(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
            string.Join("\n", exec.CompilationErrors)).Which.Message.Should().Contain("'PkgModule'");
    }

    /// <summary>
    /// Fixture classes of a package module join the module's namespace as siblings of its module
    /// class (F14, #1948): <c>namespace Simple.Pkg { Lib; GreetingFixture }</c> — they were siblings
    /// inside the wrapper class <c>Pkg</c>. The test class is emitted only for the test host
    /// (<c>ProjectConfig.TestHost</c>) and lands in the same member list; the Stdlib spy-test corpus
    /// (119 package test classes, regenerated with this layout) compiles and runs it.
    /// </summary>
    [Fact]
    public void PackageFixtureClasses_JoinTheModuleNamespace()
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile("pkg/lib.spy", LibF + "\n@test.fixture\ndef greeting() -> str:\n    return \"hi\"\n\n"
            + "@test\ndef test_f(greeting: str):\n    assert f() == 7\n");
        helper.AddSourceFile("main.spy", M7Main);
        helper.CreateProjectFile();
        var result = helper.Compile();

        var lib = result.GeneratedCSharpFiles.Single(kv => Path.GetFileName(kv.Key) == "lib.cs").Value;
        var ns = CSharpSyntaxTree.ParseText(lib).GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Single();
        ns.Name.ToString().Should().Be("Simple.Pkg.Lib");
        ns.Members.OfType<ClassDeclarationSyntax>().Select(c => c.Identifier.Text)
            .Should().BeEquivalentTo(new[] { "LibModule", "GreetingFixture" });
    }

    /// <summary>
    /// Two modules each declaring <c>class Foo</c> stay two types (python: <c>a.Foo is b.Foo</c> is
    /// False) — the cell a flat "types into the project namespace" layout would break.
    /// </summary>
    [Fact]
    public void SameNamedTypesInTwoModules_StayDistinct()
    {
        var run = RunProject("Simple",
            ("a.spy", "class Foo:\n    def who(self) -> str:\n        return \"a\"\n"),
            ("b.spy", "class Foo:\n    def who(self) -> str:\n        return \"b\"\n"),
            ("main.spy", "import a\nimport b\n\ndef main() -> None:\n    print(a.Foo().who())\n    print(b.Foo().who())\n"));

        run.Exec.Success.Should().BeTrue(string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Trim().Should().Be("a\nb");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // The sibling axis (#2039, P14c): every module is a namespace, its members live in <X> and its
    // types are declared beside <X>. Each cell runs; the refused ones name the collision.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A sibling type's method reads a module variable its own field shadows (#1786: python resolves
    /// the bare name to the module). The reference is emitted <c>global::</c>-rooted through the
    /// members class — bare, <c>Lib.V</c> would bind the NAMESPACE <c>Lib</c> (CS0234).
    /// </summary>
    [Fact]
    public void SiblingTypeBody_ReachesAShadowedModuleMember_ThroughTheMembersClass()
    {
        var run = RunProject("Simple",
            ("lib.spy", "v: int = 99\n\nclass C:\n    v: int = 1\n\n    def get(self) -> int:\n        return v\n"),
            ("main.spy", "from lib import C\n\ndef main() -> None:\n    print(C().get())\n"));

        run.Exec.Success.Should().BeTrue(string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Trim().Should().Be("99");
        run.GeneratedCSharp.Should().Contain("global::Simple.Lib.LibModule.V");
    }

    /// <summary>A nested class of another module: <c>global::Simple.A.Outer.C</c> (the outer type is a sibling of <c>AModule</c>).</summary>
    [Fact]
    public void NestedTypeOfAnotherModule_IsQualifiedThroughItsOuterSibling()
    {
        var run = RunProject("Simple",
            ("a.spy", "class Outer:\n    class C:\n        def who(self) -> str:\n            return \"c\"\n"),
            ("main.spy", "from a import Outer\n\ndef main() -> None:\n    x: Outer.C = Outer.C()\n    print(x.who())\n"));

        run.Exec.Success.Should().BeTrue(string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Trim().Should().Be("c");
        run.GeneratedCSharp.Should().Contain("global::Simple.A.Outer.C");
    }

    /// <summary>
    /// A function and a type of one emitted name (<c>def foo_bar</c> / <c>class FooBar</c>) live in two
    /// scopes — the members class and the namespace — so they coexist and both are importable
    /// (was SPY0522 while both were members of one module class; refused → runs).
    /// </summary>
    [Fact]
    public void FunctionAndTypeOfOneEmittedName_Coexist()
    {
        var run = RunProject("Simple",
            ("lib.spy", "class FooBar:\n    def who(self) -> str:\n        return \"type\"\n\ndef foo_bar() -> str:\n    return \"function\"\n"),
            ("main.spy", "from lib import FooBar, foo_bar\n\ndef main() -> None:\n    print(FooBar().who())\n    print(foo_bar())\n"));

        run.Exec.Success.Should().BeTrue(string.Join("\n", run.Exec.CompilationErrors));
        run.Exec.StandardOutput.Trim().Should().Be("type\nfunction");
    }

    /// <summary>
    /// The module namespace is seeded with its classes (Decision 28 (h)): a declaration spelled like
    /// the members class <c>ThingModule</c> is SPY0523 (ruling 15), a type spelled like a fixture class
    /// the module emits is SPY0522. Direction (run @ 855beadb6): every cell compiled and ran —
    /// worked → refused.
    /// </summary>
    [Theory]
    [InlineData("def thing_module() -> int:\n    return 1\n", "which is this module's members class")] // SPY0523
    [InlineData("class ThingModule:\n    pass\n", "which is this module's members class")] // SPY0523
    [InlineData("class GreetingFixture:\n    pass\n\n@test.fixture\ndef greeting() -> str:\n    return \"hi\"\n", "which is a test fixture class")] // SPY0522
    public void DeclarationSpelledLikeANamespaceSeed_IsRefused(string thing, string message)
    {
        var run = RunProject("Simple",
            ("thing.spy", thing),
            ("main.spy", "def main() -> None:\n    print(7)\n"));

        run.Exec.Success.Should().BeFalse();
        run.Exec.CompilationErrors.Should().Contain(e => e.Contains(message, StringComparison.Ordinal),
            string.Join("\n", run.Exec.CompilationErrors));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // The two SPY0526 refusals of module-as-namespace (#1948, Decision 28 (d)). Refusal 1:
    // a module file beside a same-named package directory — python imports only one of them (the
    // package with __init__, the module without), so the other is unreachable. Refusal 2: an
    // __init__ top-level name whose emitted identifier is one of the package's own submodules or
    // subpackages (`pkg.lib` would name both). Prior commit (direction, measured with sharpyc @
    // 5a0135370): file_beside_dir and file_beside_dir_mangled built and ran (worked → refused,
    // python-consistent); file_beside_package was SPY0908 CS0579; the three __init__ cells were
    // SPY0908 CS0260 / CS0102 (ICE → refused).
    // ═══════════════════════════════════════════════════════════════════════

    public static IEnumerable<object[]> PackageShadowingLayouts() => new[]
    {
        // name, files, refused file, message fragment
        new object[] { "file_beside_dir",
            new[] { ("pkg.spy", "def g() -> int:\n    return 8\n"), ("pkg/x.spy", LibF), ("main.spy", "from pkg import g\n\ndef main() -> None:\n    print(g())\n") },
            "pkg.spy", "Module 'pkg.spy' and the package directory 'pkg' beside it both emit the C# identifier 'Pkg'" },
        new object[] { "file_beside_package",
            new[] { ("pkg.spy", "def g() -> int:\n    return 8\n"), ("pkg/__init__.spy", "version: int = 1\n"), ("pkg/x.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") },
            "pkg.spy", "Module 'pkg.spy' and the package directory 'pkg' beside it both emit the C# identifier 'Pkg'" },
        new object[] { "file_beside_dir_mangled",
            new[] { ("my_pkg.spy", "def g() -> int:\n    return 8\n"), ("myPkg/x.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") },
            "my_pkg.spy", "Module 'my_pkg.spy' and the package directory 'myPkg' beside it both emit the C# identifier 'MyPkg'" },
        new object[] { "init_def_shadows_submodule",
            new[] { ("pkg/__init__.spy", "def lib() -> int:\n    return 1\n"), ("pkg/lib.spy", LibF), ("main.spy", "from pkg.lib import f\n\ndef main() -> None:\n    print(f())\n") },
            "__init__.spy", "'lib' in the package's __init__.spy emits the C# identifier 'Lib', which its submodule 'lib.spy' also emits" },
        new object[] { "init_class_shadows_submodule",
            new[] { ("pkg/__init__.spy", "class Lib:\n    pass\n"), ("pkg/lib.spy", LibF), ("main.spy", "from pkg.lib import f\n\ndef main() -> None:\n    print(f())\n") },
            "__init__.spy", "'Lib' in the package's __init__.spy emits the C# identifier 'Lib', which its submodule 'lib.spy' also emits" },
        new object[] { "init_variable_shadows_subpackage",
            new[] { ("pkg/__init__.spy", "sub: int = 1\n"), ("pkg/sub/x.spy", LibF), ("main.spy", "from pkg.sub.x import f\n\ndef main() -> None:\n    print(f())\n") },
            "__init__.spy", "'sub' in the package's __init__.spy emits the C# identifier 'Sub', which its subpackage 'sub' also emits" },
        // Emitted-identifier rule (plan Decision 28 (d)): `X` beside x.spy is refused although python
        // tells pkg.X from pkg.x — no python reason is printed. TODO(#2086): drains when refusal 2 is
        // narrowed to real C# clashes (P14c) — this row then runs.
        new object[] { "init_constant_spelled_like_submodule_2086",
            new[] { ("pkg/__init__.spy", "X: int = 1\n"), ("pkg/x.spy", LibF), ("main.spy", "from pkg.x import f\n\ndef main() -> None:\n    print(f())\n") },
            "__init__.spy", "'X' in the package's __init__.spy emits the C# identifier 'X', which its submodule 'x.spy' also emits. Rename" },
    };

    [Theory]
    [MemberData(nameof(PackageShadowingLayouts))]
    public void PackageShadowing_IsSpy0526(string name, (string, string)[] files, string refusedFile, string message)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        foreach (var (path, content) in files)
            helper.AddSourceFile(path, content);
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeFalse($"[{name}] must be refused");
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{name}] the refusal is SPY0526, not a C# error behind SPY0908");
        var refusal = errors.Should().ContainSingle(
            d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.PackageModuleNameCollision,
            $"[{name}] {string.Join("\n", exec.CompilationErrors)}").Subject;
        refusal.Message.Should().StartWith(message, $"[{name}]");
        Path.GetFileName(refusal.FilePath).Should().Be(refusedFile, $"[{name}]");
    }

    /// <summary>
    /// Positive controls for the two refusals: a package with no same-named module beside it, and an
    /// __init__ whose names differ from its children, both run.
    /// </summary>
    [Theory]
    [InlineData("package_without_twin_module", "pkg/x.spy", "q.spy")]
    [InlineData("init_names_differ_from_children", "pkg/lib.spy", "pkg/__init__.spy")]
    public void PackageWithoutShadowing_Runs(string name, string libPath, string otherPath)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile(libPath, LibF);
        helper.AddSourceFile(otherPath, "def lib_version() -> int:\n    return 2\n");
        var importPath = libPath[..^4].Replace('/', '.');
        helper.AddSourceFile("main.spy", $"from {importPath} import f\n\ndef main() -> None:\n    print(f())\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeTrue($"[{name}] {string.Join("\n", exec.CompilationErrors)}");
        exec.StandardOutput.Trim().Should().Be("7", $"[{name}]");
    }

    [Fact]
    public void PackageShadowing_Axis_IsTotal()
        => PackageShadowingLayouts().Should().HaveCount(7);

    /// <summary>
    /// A child spelled like the package's module class <c>&lt;X&gt;</c> (<c>pkg/pkg_module.spy</c> or
    /// <c>pkg/pkg_module/</c> beside <c>pkg/__init__.spy</c>'s <c>PkgModule</c>) would be a second
    /// <c>PkgModule</c> in <c>namespace Simple.Pkg</c> (CS0101): the <c>&lt;X&gt;</c> collision of owner
    /// ruling 15, SPY0523 — the code a <c>def pkg_module</c> in the __init__ gets. Prior commit: while the
    /// __init__ class was <c>Pkg</c> these ran (worked → refused, a consequence of X3).
    /// </summary>
    [Theory]
    [InlineData("pkg/pkg_module.spy", "from pkg.pkg_module import f", "The submodule 'pkg_module.spy' compiles to 'PkgModule'")]
    [InlineData("pkg/pkg_module/x.spy", "from pkg.pkg_module.x import f", "The subpackage 'pkg_module' compiles to 'PkgModule'")]
    public void ChildSpelledLikeTheMembersClass_IsSpy0523(string childPath, string import, string message)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        helper.AddSourceFile("pkg/__init__.spy", "VERSION: int = 1\n");
        helper.AddSourceFile(childPath, LibF);
        helper.AddSourceFile("main.spy", $"{import}\n\ndef main() -> None:\n    print(f())\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        exec.Success.Should().BeFalse(childPath);
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        errors.Should().NotContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.PackageModuleNameCollision);
        var refusal = errors.Should().ContainSingle(
            d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
            string.Join("\n", exec.CompilationErrors)).Subject;
        refusal.Message.Should().StartWith(message);
        refusal.Message.Should().Contain("module class of the package's __init__.spy");
        Path.GetFileName(refusal.FilePath).Should().Be("__init__.spy");
    }
}
