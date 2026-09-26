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
/// <item><c>c</c> merged module <c>thing.spy</c> + <c>class Thing</c> → <c>from thing import thing_fn</c>
/// → <c>6</c>. It was SPY0300 while the merged module class carried no <c>[SharpyModule]</c>; #2006
/// (cb5df187f) stamps it, so discovery finds the module. P14c (#2039) lifts the merge and must keep
/// this cell running.</item>
/// <item><c>d</c> module with a type → <c>from shapes import Foo, mk</c> → <c>9</c>/<c>9</c> (nested-type
/// discovery; the positive control that the harness reaches a library's types).</item>
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
        // A merged file==class module (thing.spy declaring `class Thing`): its module class now carries
        // [SharpyModule] (#2006, cb5df187f), so reference discovery finds it and the import resolves.
        // It was SPY0300 at P5's original base 7bb1511a7; P6's layout keeps it running.
        new object[] { "c_merged_module",
            new[] { ("thing.spy", "class Thing:\n    pass\n\ndef thing_fn() -> int:\n    return 6\n") },
            "from thing import thing_fn\n\ndef main() -> None:\n    print(thing_fn())\n", "6", null! },
        new object[] { "d_module_with_type",
            new[] { ("shapes.spy", "class Foo:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n\ndef mk() -> Foo:\n    return Foo(9)\n") },
            "from shapes import Foo, mk\n\ndef main() -> None:\n    print(Foo(9).v)\n    print(mk().v)\n", "9\n9", null! },
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
    public void Matrix_IsTotal() => Cells().Should().HaveCount(4);
}
