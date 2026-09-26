using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// A Sharpy LIBRARY project consumed by another project through an assembly reference — the CLI's
/// <c>-r</c> (#1948, the <c>libref</c> cells of plan-0ca7b7). No other standing test builds a user
/// library and imports from it: discovery reads the library's <c>[SharpyModule]</c> classes, so the
/// cells pin what the emitted layout lets a consumer find.
/// </summary>
/// <remarks>
/// <para>Cells (library layout → consumer import → verdict), each cold and — when it runs — warm (the
/// importing unit served from the incremental cache):</para>
/// <list type="bullet">
/// <item><c>a</c> flat module <c>lib.spy</c> → <c>from lib import lib_fn</c> → <c>4</c>.</item>
/// <item><c>b</c> package module <c>pkg/lib.spy</c> → <c>from pkg.lib import lib_fn</c> → <c>5</c>.
/// Prior commit: SPY0908 CS0234 — the module class was nested in wrapper class <c>Pkg</c>, and
/// discovery (<c>OverloadIndexBuilder</c>) records the nested class's namespace without the wrapper.
/// A namespace segment is what discovery reads.</item>
/// <item><c>c</c> a module with a type named like its file, <c>thing.spy</c> + <c>class Thing</c> →
/// <c>from thing import thing_fn</c> → <c>6</c>. It was SPY0300 at P5's base 7bb1511a7 (the merged module
/// class carried no <c>[SharpyModule]</c>); #2006 (cb5df187f) stamped the merged class; with no merge
/// (#2039, M2) the members class <c>ThingModule</c> is stamped like every other module's — c keeps
/// running.</item>
/// <item><c>d</c> module with a type → <c>from shapes import Foo, mk</c> → <c>9</c>/<c>9</c>: the type
/// is a sibling of <c>ShapesModule</c> stamped <c>[SharpyModuleType("shapes", "Foo")]</c> (#2039), and
/// discovery finds it by attribute.</item>
/// <item><c>e</c>/<c>f</c> a module holding ONLY a type → <c>from things import Box</c> / <c>import
/// things; things.Box</c>: resolves through the type's <c>[SharpyModuleType]</c> stamp (#2039).</item>
/// </list>
/// </remarks>
[Collection("HeavyCompilation")]
public class LibraryReferenceMatrixTests
{
    private readonly ITestOutputHelper _output;

    public LibraryReferenceMatrixTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> Cells() => new[]
    {
        new object[] { "a_flat_module",
            new[] { ("lib.spy", "def lib_fn() -> int:\n    return 4\n") },
            "from lib import lib_fn\n\ndef main() -> None:\n    print(lib_fn())\n", "4", null! },
        new object[] { "b_package_module",
            // util.spy keeps the common source root at src/, so the module is pkg.lib (a library of
            // pkg/lib.spy alone roots at src/pkg/ and names it lib).
            new[] { ("pkg/lib.spy", "def lib_fn() -> int:\n    return 5\n"), ("util.spy", "def u() -> int:\n    return 0\n") },
            "from pkg.lib import lib_fn\n\ndef main() -> None:\n    print(lib_fn())\n", "5", null! },
        // A module declaring a type named like its file (thing.spy + `class Thing`): #2006 stamped the
        // merged module class [SharpyModule]; with no merge (#2039) its members class ThingModule is.
        new object[] { "c_type_named_like_its_module",
            new[] { ("thing.spy", "class Thing:\n    pass\n\ndef thing_fn() -> int:\n    return 6\n") },
            "from thing import thing_fn\n\ndef main() -> None:\n    print(thing_fn())\n", "6", null! },
        new object[] { "d_module_with_type",
            new[] { ("shapes.spy", "class Foo:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n\ndef mk() -> Foo:\n    return Foo(9)\n") },
            "from shapes import Foo, mk\n\ndef main() -> None:\n    print(Foo(9).v)\n    print(mk().v)\n", "9\n9", null! },
        new object[] { "e_types_only_module",
            new[] { ("things.spy", "class Box:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n") },
            "from things import Box\n\ndef main() -> None:\n    print(Box(3).v)\n", "3", null! },
        new object[] { "f_types_only_module_whole_import",
            new[] { ("things.spy", "class Box:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n") },
            "import things\n\ndef main() -> None:\n    print(things.Box(4).v)\n", "4", null! },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void ReferencedLibrary_IsImportable_ColdAndWarm(
        string name, (string Path, string Content)[] library, string consumer, string? expected, string? refusal)
    {
        using var lib = new ProjectCompilationHelper(_output);
        lib.WithRootNamespace("Deps").WithOutputType("library");
        // One assembly name per cell: the process loads a referenced assembly by identity, so a second
        // "Deps" would be served the first cell's.
        lib.Options.AssemblyName = "Deps_" + name;
        foreach (var (path, content) in library)
            lib.AddSourceFile(path, content);
        lib.CreateProjectFile();
        var libResult = lib.Compile();
        lib.AssertCompilationSucceeded(libResult);

        using var app = new ProjectCompilationHelper(_output);
        app.WithRootNamespace("App").WithEntryPoint("main.spy").WithIncremental();
        app.WithAssemblyReference(libResult.OutputAssemblyPath!);
        app.AddSourceFile("main.spy", consumer);
        app.AddSourceFile("other.spy", "def value() -> int:\n    return 1\n");
        app.CreateProjectFile();

        var cold = app.CompileAndExecute();
        if (refusal != null)
        {
            cold.Success.Should().BeFalse($"[{name}] is refused");
            app.LastCompilationResult!.Diagnostics.GetErrors().Select(d => d.Code)
                .Should().Contain(refusal, $"[{name}] {string.Join("\n", cold.CompilationErrors)}");
            return;
        }

        cold.Success.Should().BeTrue($"[{name}] cold: {string.Join("\n", cold.CompilationErrors)}");
        cold.StandardOutput.Trim().Should().Be(expected, $"[{name}] cold");

        // An edit to a DIFFERENT file makes the importing unit cache-served.
        app.UpdateSourceFile("other.spy", "def value() -> int:\n    return 2\n");
        var warm = app.CompileAndExecute();
        warm.Success.Should().BeTrue($"[{name}] warm: {string.Join("\n", warm.CompilationErrors)}");
        app.AssertWarmBuildSkipped(app.LastCompilationResult!, "main.spy");
        warm.StandardOutput.Trim().Should().Be(expected, $"[{name}] warm ≡ cold");
    }

    [Fact]
    public void Matrix_IsTotal() => Cells().Should().HaveCount(6);

    /// <summary>
    /// The same consumer reading a SINGLE-FILE library (<c>sharpyc build X.spy -t library</c>, P14c
    /// ruling 12's uniform layout): the module is namespace <c>X</c> holding
    /// <c>[SharpyModule("x")] XModule</c>, so the import name also spells a CLR namespace of the
    /// referenced assembly. Prior commit: import resolution took the namespace path first, which
    /// drops the members class — every function reference was SPY0908 CS0103/CS0234, a module
    /// variable was SPY0301, and the generic <c>Shapes[T]</c> cell moved from SPY0300 (BASE) to
    /// SPY0908. A project library's module lives under its root namespace and never collided.
    /// </summary>
    public static IEnumerable<object[]> SingleFileCells() => new[]
    {
        new object[] { "sf_a_functions", "util.spy",
            "def helper() -> int:\n    return 42\n",
            "from util import helper\nimport util\n\ndef main() -> None:\n    print(helper(), util.helper())\n",
            "42 42" },
        new object[] { "sf_b_types_only", "things.spy",
            "class Box:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n",
            "from things import Box\nimport things\n\ndef main() -> None:\n    print(Box(3).v, things.Box(4).v)\n",
            "3 4" },
        new object[] { "sf_c_generic_named_like_module", "shapes.spy",
            "class Shapes[T]:\n    v: T\n    def __init__(self, v: T) -> None:\n        self.v = v\n\ndef helper() -> int:\n    return 42\n",
            "from shapes import Shapes, helper\nimport shapes\n\ndef main() -> None:\n    print(Shapes[int](3).v, helper(), shapes.helper())\n",
            "3 42 42" },
        new object[] { "sf_d_enum", "colors.spy",
            "enum Col:\n    RED = 1\n    GREEN = 2\n\ndef pick() -> Col:\n    return Col.GREEN\n",
            "from colors import Col, pick\nimport colors\n\ndef main() -> None:\n    print(Col.RED, pick(), colors.pick())\n",
            "Col.RED Col.GREEN Col.GREEN" },
        // A flat module (no package, no __init__) exporting a module variable, a class without
        // __init__ and a function reading the variable.
        new object[] { "sf_e_module_variable", "consts.spy",
            "limit: int = 7\n\nclass Plain:\n    def m(self) -> int:\n        return limit\n\ndef twice() -> int:\n    return limit * 2\n",
            "from consts import limit, Plain, twice\nimport consts\n\ndef main() -> None:\n    print(limit, Plain().m(), twice(), consts.limit)\n",
            "7 7 14 7" },
    };

    [Theory]
    [MemberData(nameof(SingleFileCells))]
    public void ReferencedSingleFileLibrary_IsImportable_ColdAndWarm(
        string name, string libraryFile, string library, string consumer, string expected)
    {
        var libDir = Path.Combine(Path.GetTempPath(), "sharpy_sflib_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(libDir);
        try
        {
            var libPath = Path.Combine(libDir, libraryFile);
            File.WriteAllText(libPath, library);
            // The CLI's `build -t library` options; one assembly name per cell (see above).
            var assemblyName = "SfLib_" + name;
            var libResult = new CompilerApi().Compile(library,
                CompilerOptionsFactory.ForCli(outputType: "library", assemblyName: assemblyName,
                    outputAssemblyPath: Path.Combine(libDir, assemblyName + ".dll")),
                libPath);
            libResult.Success.Should().BeTrue(
                $"[{name}] library: {string.Join("\n", libResult.Diagnostics.Select(d => d.ToString()))}");

            using var app = new ProjectCompilationHelper(_output);
            app.WithRootNamespace("App").WithEntryPoint("main.spy").WithIncremental();
            app.WithAssemblyReference(libResult.OutputAssemblyPath!);
            app.AddSourceFile("main.spy", consumer);
            app.AddSourceFile("other.spy", "def value() -> int:\n    return 1\n");
            app.CreateProjectFile();

            var cold = app.CompileAndExecute();
            cold.Success.Should().BeTrue($"[{name}] cold: {string.Join("\n", cold.CompilationErrors)}");
            cold.StandardOutput.Trim().Should().Be(expected, $"[{name}] cold");

            app.UpdateSourceFile("other.spy", "def value() -> int:\n    return 2\n");
            var warm = app.CompileAndExecute();
            warm.Success.Should().BeTrue($"[{name}] warm: {string.Join("\n", warm.CompilationErrors)}");
            app.AssertWarmBuildSkipped(app.LastCompilationResult!, "main.spy");
            warm.StandardOutput.Trim().Should().Be(expected, $"[{name}] warm ≡ cold");
        }
        finally
        {
            try
            { Directory.Delete(libDir, recursive: true); }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    [Fact]
    public void SingleFileMatrix_IsTotal() => SingleFileCells().Should().HaveCount(5);
}
