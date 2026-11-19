using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 (review 6, F1): a nested class's base list is evaluated in the scope that encloses the
/// class statement, as CPython's is: the enclosing class body's names bound BEFORE it (an earlier
/// sibling), then the module, then the builtins — never the class being defined, a later sibling, nor
/// an enclosing class body further out. The checker binds that base, the emitter spells it, and a
/// cycle is refused instead of walked forever.
/// <para><b>Python.</b> Each run cell's output is CPython 3.15's for the same program with
/// <c>@static</c> dropped (<c>n1</c>–<c>n11</c> under the round's probes); the refused cells have no
/// twin: Python neither checks an annotation (<c>o: C = Outer.D()</c>) nor reads an absent attribute
/// at compile time, and a cyclic base is a NameError at run time.</para>
/// </summary>
public class NestedClassBaseScopeTests
{
    private readonly ITestOutputHelper _output;

    public NestedClassBaseScopeTests(ITestOutputHelper output) => _output = output;

    private const string ModuleC =
        "class C:\n    @static\n    s: int = 1\n    def who(self) -> str:\n        return \"module C\"\n";

    private const string OuterC =
        "    class C:\n        @static\n        s: int = 2\n        def who(self) -> str:\n            return \"Outer.C\"\n";

    public static IEnumerable<object[]> RunCells() => new[]
    {
        // An earlier sibling is in the enclosing body's scope.
        new object[] { "EarlierSibling", ModuleC + "class Outer:\n" + OuterC + "    class D(C):\n        pass\n"
            + "def main() -> None:\n    print(Outer.D.s, Outer.D().who())\n", "2 Outer.C" },
        // An enclosing class body further out is not: the module's C.
        new object[] { "OuterBodyOfTheEnclosing", ModuleC + "class Outer:\n" + OuterC + "    class Mid:\n        class D(C):\n            pass\n"
            + "def main() -> None:\n    print(Outer.Mid.D.s, Outer.Mid.D().who())\n", "1 module C" },
        // A later sibling is not bound yet.
        new object[] { "LaterSibling", ModuleC + "class Outer:\n    class D(C):\n        pass\n" + OuterC
            + "def main() -> None:\n    print(Outer.D.s, Outer.D().who())\n", "1 module C" },
        // A nested type the enclosing class inherits is no name of its body.
        new object[] { "InheritedNested", ModuleC + "class Base:\n" + OuterC.Replace("Outer.C", "Base.C") + "class Outer(Base):\n    class D(C):\n        pass\n"
            + "def main() -> None:\n    print(Outer.D.s, Outer.D().who())\n", "1 module C" },
        // The class being defined is not bound yet: `class C(C)` derives from the module's C.
        new object[] { "OwnName", ModuleC + "class Outer:\n    class C(C):\n        def who2(self) -> str:\n            return \"Outer.C\"\n"
            + "def main() -> None:\n    o = Outer.C()\n    print(o.who(), o.who2(), Outer.C.s)\n", "module C Outer.C 1" },
        // A cousin of the same name further out is not either.
        new object[] { "Cousin", "class Base:\n    def who(self) -> str:\n        return \"module Base\"\n"
            + "class Outer:\n    class Base:\n        def who(self) -> str:\n            return \"Outer.Base\"\n"
            + "    class Inner:\n        class Base(Base):\n            pass\n"
            + "def main() -> None:\n    print(Outer.Inner.Base().who())\n", "module Base" },
    };

    [Theory]
    [MemberData(nameof(RunCells))]
    public void NestedBase_BindsTheEnclosingScopesName(string cell, string source, string expected)
    {
        var exec = Run(cell, source);
        exec.CompilationErrors.Should().BeEmpty(cell);
        exec.Success.Should().BeTrue(exec.StandardError);
        exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Should().Be(expected, cell);
    }

    public static IEnumerable<object[]> RefusedCells() => new[]
    {
        // The sibling base is not the module's class of the same name: no subtype of it.
        new object[] { "NotTheModulesSubtype", ModuleC + "class Outer:\n" + OuterC + "    class D(C):\n        pass\n"
            + "def main() -> None:\n    o: C = Outer.D()\n    print(o.who())\n", "Cannot assign type 'D' to variable of type 'C'" },
        // A member only the module's class declares is absent from the sibling's chain.
        new object[] { "AbsentFromTheSiblingsChain", "class C:\n    @static\n    s: int = 1\n"
            + "class Outer:\n    class C:\n        @static\n        t: int = 2\n    class D(C):\n        pass\n"
            + "def main() -> None:\n    print(Outer.D.t)\n    print(Outer.D.s)\n", "Type 'D' has no member 's'" },
        // A cycle through the module's classes, and through nested siblings.
        new object[] { "ModuleCycle", "class A(B):\n    pass\nclass B(A):\n    pass\ndef main() -> None:\n    print(A().zz)\n",
            "Circular inheritance detected" },
        new object[] { "NestedCycle", "class Outer:\n    class A(B):\n        pass\n    class B(A):\n        pass\n"
            + "def main() -> None:\n    print(Outer.A().zz)\n", "Circular inheritance detected" },
    };

    /// <summary>A refused program is refused with its diagnostic, in bounded time (a cyclic base list
    /// walked by a member lookup never returned).</summary>
    [Theory(Timeout = 120000)]
    [MemberData(nameof(RefusedCells))]
    public async Task NestedBase_RefusesWhatTheBoundBaseRefuses(string cell, string source, string diagnostic)
    {
        var exec = await Task.Run(() => Run(cell, source));
        exec.Success.Should().BeFalse(cell);
        exec.CompilationErrors.Should().Contain(e => e.Contains(diagnostic, StringComparison.Ordinal), cell);
    }

    private ExecutionResult Run(string cell, string source)
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("NestedBase" + cell)
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", source);
        helper.CreateProjectFile();
        return helper.CompileAndExecute();
    }
}
