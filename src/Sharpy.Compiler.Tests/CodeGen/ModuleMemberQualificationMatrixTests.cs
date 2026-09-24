using FluentAssertions;
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
///     global::Sharpy.OsModule;</c> ALIAS directive (a <c>using X = Y;</c>, NOT a <c>using
///     static</c>) + <c>os.Getcwd()</c>; a stdlib member/wildcard import inlines the
///     <c>global::Sharpy.&lt;Module&gt;.&lt;member&gt;</c> reference.</item>
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
        yield return new object[] { "module_function_callee", "global::Prog.Helper()" };
        yield return new object[] { "module_const_read", "global::Prog.LIMIT" };
        yield return new object[] { "module_variable_read", "global::Prog.Count" };
        yield return new object[] { "static_method_callee", "global::Prog.Widget.Make()" };
        yield return new object[] { "static_field_read", "global::Prog.Widget.Total" };
        yield return new object[] { "nested_enum_member", "global::Prog.Widget.Color.Green" };
        yield return new object[] { "parameter_default_module_const", "= global::Prog.LIMIT" };
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
        cs.Should().Contain("Widget.Inner i = new Widget.Inner(9)",
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
    //    binds a `using os = global::Sharpy.OsModule;` ALIAS (not `using static`); a member/wildcard
    //    import inlines the global::Sharpy.<Module>.<member> reference.

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
        cs.Should().Contain("using os = global::Sharpy.OsModule;",
            "a stdlib whole-module import binds a global::-qualified using-ALIAS to the real module "
            + "class (global::Sharpy.OsModule), not a bare `global::Os` (#1683, fix 4a6f9c4e5)");
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
        member.GeneratedCSharp!.Should().Contain("global::Sharpy.OsModule.Getcwd()",
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
        wildcard.GeneratedCSharp!.Should().Contain("global::Sharpy.MathModule.Sqrt(",
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
        yield return new object[] { "cross_module_function_callee", "global::Test.Lib.Compute()" };
        yield return new object[] { "cross_module_const_read", "global::Test.Lib.CAP" };
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
        run.GeneratedCSharp.Should().Contain("global::Poison.Lib.Poison()",
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
            "global::Test.Lib.Compute()", "8\n"
        };
        yield return new object[]
        {
            "whole_module_import_as_alias",
            "import lib as l\n\ndef main() -> None:\n    print(l.other())\n",
            "global::Test.Lib.Other()", "9\n"
        };
        yield return new object[]
        {
            "from_import_member_as_alias",
            "from lib import compute as c\n\ndef main() -> None:\n    print(c())\n",
            "global::Test.Lib.Compute()", "8\n"
        };
        yield return new object[]
        {
            "dotted_package_import",
            "import pkg.sub\n\ndef main() -> None:\n    print(pkg.sub.helper())\n",
            "global::Test.Pkg.Sub.Helper()", "42\n"
        };
        yield return new object[]
        {
            "dotted_from_import",
            "from pkg.sub import helper\n\ndef main() -> None:\n    print(helper())\n",
            "global::Test.Pkg.Sub.Helper()", "42\n"
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
    public void FileNamedClassMerge_MemberReference_IsSingleSegmentQualified_NotDoubled()
    {
        // #1802 core: foo.spy declaring `class Foo` merges the class INTO the module class Foo, so a
        // reference to its const / static field / static method must be `global::Foo.<member>` — a
        // SINGLE Foo segment. The defect emitted `Foo.Foo.K` (CS0117) because the merge was not seen
        // at the reference site. `Foo.Foo` must be absent.
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
            "the file==class merge specimen must compile and run (no CS0117). Errors:\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("10\n0\n7\n");

        var cs = result.GeneratedCSharp!;
        cs.Should().Contain("global::Foo.K",
            "the const of a file-named (merged) class is referenced through the single merged class "
            + "segment (#1802)");
        cs.Should().Contain("global::Foo.Make()",
            "the static method of the merged class is single-segment-qualified (#1802)");
        cs.Should().NotContain("Foo.Foo",
            "the #1802 defect spelling — the reference site re-derived a module-class segment on top "
            + "of the class that IS the module class (`Foo.Foo.K` → CS0117) — must be gone");
        cs.Should().NotContain(UsingStatic, "#1683 close criterion");
    }

    public static IEnumerable<object[]> CollisionFunctionNameCells()
    {
        // A module function whose name equals a CLR namespace/type: it must be emitted
        // global::<Module>.<Name>() so it binds the user function, not System / System.String.
        yield return new object[] { "def_system", "system", "System", "global::Coll.System()" };
        yield return new object[] { "def_string", "string", "String", "global::Coll.String()" };
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
    // Subdirectory axis (#1932, R-AX phase a): each directory above a file is a wrapper class and
    // the file is the module class nested in it. When the innermost wrapper and the module class
    // mangle to one identifier (`lib/lib.spy` → wrapper `Lib` holding module class `Lib`) C# refuses
    // it (CS0542); the project refuses it by name first (SPY0526), from the SAME helper the emitter
    // spells both names with (ModuleIdentifiers). The helper lays sources out under `src/`, so the
    // wrapper segments are measured from `src/` (the common source root), not the project dir.
    // Prior commit: every refused layout was CS0542 behind SPY0908; the running layouts ran.
    // ═══════════════════════════════════════════════════════════════════════

    private const string LibF = "def f() -> int:\n    return 7\n";

    public static IEnumerable<object[]> SubdirectoryLayouts() => new[]
    {
        // name, files (path → content), refused file (null = runs and prints 7), expected identifier
        new object[] { "dir_differs_from_file", new[] { ("pkg/lib.spy", LibF), ("main.spy", "from pkg.lib import f\n\ndef main() -> None:\n    print(f())\n") }, null!, null! },
        new object[] { "dir_equals_file", new[] { ("lib/lib.spy", LibF), ("main.spy", "from lib.lib import f\n\ndef main() -> None:\n    print(f())\n") }, "lib.spy", "Lib" },
        new object[] { "dir_equals_file_with_init", new[] { ("lib/__init__.spy", "X: int = 1\n"), ("lib/lib.spy", LibF), ("main.spy", "from lib.lib import f\n\ndef main() -> None:\n    print(f())\n") }, "lib.spy", "Lib" },
        new object[] { "nested_dir_equals_file", new[] { ("a/b/b.spy", LibF), ("main.spy", "from a.b.b import f\n\ndef main() -> None:\n    print(f())\n") }, "b.spy", "B" },
        new object[] { "unimported_dir_equals_file", new[] { ("lib/lib.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") }, "lib.spy", "Lib" },
        new object[] { "init_only", new[] { ("lib/__init__.spy", LibF), ("main.spy", "from lib import f\n\ndef main() -> None:\n    print(f())\n") }, null!, null! },
        // Discriminates the ROOT the segments are measured from: under the common source root
        // (`src/`) a root-level src.spy has no wrapper; measured from the project dir it would get
        // wrapper `Src` and collide with its own module class `Src`.
        new object[] { "file_named_like_source_root", new[] { ("src.spy", LibF), ("main.spy", "from src import f\n\ndef main() -> None:\n    print(f())\n") }, null!, null! },
        new object[] { "mangling_equal", new[] { ("my_lib/myLib.spy", LibF), ("main.spy", "def main() -> None:\n    print(7)\n") }, "myLib.spy", "MyLib" },
    };

    [Theory]
    [MemberData(nameof(SubdirectoryLayouts))]
    public void Subdirectory_DirEqualsModule_IsSpy0526_ElseRuns(
        string name, (string, string)[] files, string? refusedFile, string? identifier)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("Simple").WithEntryPoint("main.spy");
        foreach (var (path, content) in files)
            helper.AddSourceFile(path, content);
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();

        if (refusedFile == null)
        {
            exec.Success.Should().BeTrue($"[{name}] {string.Join("\n", exec.CompilationErrors)}");
            exec.StandardOutput.Trim().Should().Be("7", $"[{name}]");
            return;
        }

        exec.Success.Should().BeFalse($"[{name}] must be refused");
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{name}] the refusal is SPY0526, not CS0542 behind SPY0908");
        var refusal = errors.Should().ContainSingle(
            d => d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.CodeGen.PackageModuleNameCollision,
            $"[{name}] {string.Join("\n", exec.CompilationErrors)}").Subject;
        refusal.Message.Should().Contain($"module '{refusedFile}' both emit the C# identifier '{identifier}'");
        refusal.Message.Should().EndWith("Rename the file or the directory.");
        Path.GetFileName(refusal.FilePath).Should().Be(refusedFile);
    }

    [Fact]
    public void Subdirectory_Axis_IsTotal()
        => SubdirectoryLayouts().Should().HaveCount(8);
}
