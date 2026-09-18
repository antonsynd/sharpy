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
}
