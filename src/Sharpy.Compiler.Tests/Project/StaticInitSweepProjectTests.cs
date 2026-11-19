using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Project controls of plan-775c50 D5 (A5): a module is required by <c>Main</c> for its own module,
/// by an importer's sweep at the import's position, and a non-generic nested class by the sweep
/// around it at its line. Each expected output is the one CPython 3.15.2 prints for the program's
/// python twin (the twins are in the comments; run with <c>main()</c> appended to the entry module).
/// </summary>
public class StaticInitSweepProjectTests
{
    private readonly ITestOutputHelper _output;

    public StaticInitSweepProjectTests(ITestOutputHelper output) => _output = output;

    private ProjectCompilationHelper NewHelper(string ns)
    {
        var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace(ns).WithEntryPoint("main.spy").WithOutputType("exe");
        return helper;
    }

    private const string Trace = "def trace(s: str) -> int:\n    print(s)\n    return 1\n";

    [Fact]
    public void ThreeModules_RunInPythonsImportOrder()
    {
        // python3: a.x, b.x, main.x, then main — `import b` runs b, whose `import a` runs a first.
        using var helper = NewHelper("SweepThree");
        helper.AddSourceFile("a.spy", Trace + "ax: int = trace(\"a.x\")\n");
        helper.AddSourceFile("b.spy", "import a\n" + Trace + "bx: int = trace(\"b.x\") + a.ax\n");
        helper.AddSourceFile("main.spy",
            "import b\n" + Trace + "mx: int = trace(\"main.x\") + b.bx\n"
            + "def main() -> None:\n    print(\"main\", mx)\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("a.x\nb.x\nmain.x\nmain 3\n");
    }

    [Fact]
    public void AnImportBetweenTwoInitializers_Interleaves()
    {
        // python3: m1, lib.x, m2, then main.
        using var helper = NewHelper("SweepInterleave");
        helper.AddSourceFile("lib.spy", Trace + "lx: int = trace(\"lib.x\")\n");
        helper.AddSourceFile("main.spy",
            Trace + "m1: int = trace(\"m1\")\nimport lib\nm2: int = trace(\"m2\") + lib.lx\n"
            + "def main() -> None:\n    print(\"main\", m1, m2)\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("m1\nlib.x\nm2\nmain 1 2\n");
    }

    [Fact]
    public void APackageImport_RunsItsPackageThenItsSubmodule_AtItsLine()
    {
        // python3 3.15.2: m1, pkg, pkg.sub, m2, pkg.other, m3, then main — `import pkg.sub` runs the
        // package first; `from pkg.other import ox` runs pkg.other only (pkg already ran).
        using var helper = NewHelper("SweepPackage");
        helper.AddSourceFile("pkg/__init__.spy", Trace + "px: int = trace(\"pkg\")\n");
        helper.AddSourceFile("pkg/sub.spy", Trace + "sx: int = trace(\"pkg.sub\")\n");
        helper.AddSourceFile("pkg/other.spy", Trace + "ox: int = trace(\"pkg.other\")\n");
        helper.AddSourceFile("main.spy",
            Trace + "m1: int = trace(\"m1\")\nimport pkg.sub\nm2: int = trace(\"m2\") + pkg.sub.sx\n"
            + "from pkg.other import ox\nm3: int = trace(\"m3\") + ox\n"
            + "def main() -> None:\n    print(\"main\", m1, m2, m3)\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("m1\npkg\npkg.sub\nm2\npkg.other\nm3\nmain 1 2 2\n");
    }

    [Fact]
    public void ANestedClass_SweepsAtItsLine()
    {
        // python3 (W7): O.a, C.v, O.b — the nested class body runs at its line in the outer one.
        using var helper = NewHelper("SweepNested");
        helper.AddSourceFile("main.spy",
            Trace + "class O:\n    @static\n    a: int = trace(\"O.a\")\n"
            + "    class C:\n        @static\n        v: int = trace(\"C.v\")\n"
            + "    @static\n    b: int = trace(\"O.b\")\n"
            + "def main() -> None:\n    print(O.a, O.C.v, O.b)\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("O.a\nC.v\nO.b\n1 1 1\n");
    }

    [Fact]
    public void MainRequiresItsModule_AndAFailedSweepStopsBeforeMain()
    {
        // The #2588 cell (plan-775c50 execution evidence): the module's sweep raises ZeroDivisionError
        // before the user's main runs; python3 raises it at the module's line, before `main()`.
        using var helper = NewHelper("SweepMainFirst");
        helper.AddSourceFile("main.spy",
            "def zero() -> float:\n    return 0.0\nx: float = 1.0 / zero()\n"
            + "def main() -> None:\n    raise ValueError(\"main ran\")\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        // The helper invokes Main by reflection and reports the program's unhandled exception as an
        // execution failure, DISCARDING the program's stdout with it: a print in main proves nothing
        // here. The order is read from which exception escapes instead: main raises its own, so the
        // sweep ran first exactly when the one that escapes is the initializer's. It is the ORIGINAL
        // exception (D3; Q3), the direct cause of the reflection wrapper, in no TypeInitializationException.
        result.Success.Should().BeFalse();
        result.Exception.Should().BeOfType<System.Reflection.TargetInvocationException>();
        result.Exception!.InnerException.Should().NotBeNull();
        result.Exception.InnerException!.GetType().Name.Should().Be("ZeroDivisionError",
            "the module's sweep raises before the user's main body runs, and nothing wraps what it raises");
    }
}
