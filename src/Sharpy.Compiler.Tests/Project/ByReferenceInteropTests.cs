using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// A8 of plan-775c50 (D10): a module imported by reference exports the names its source exports and
/// loads at its import's position; a C# <c>ref</c> static property imports as Plain. Two specimens:
/// a hand-written C# module with every by-reference member kind discovery maps (a <c>ref</c> and a
/// <c>ref readonly</c> property, a ref-returning method, a <c>ref</c> indexer and instance property,
/// and a public member marked <c>[CompilerGenerated]</c>), and a Sharpy library compiled to a dll.
/// The order control's expected output is CPython 3.15.2's for its twin (the library a module beside
/// main): <c>m1, lib.x, m2, main 1 2</c>.
/// </summary>
public class ByReferenceInteropTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_byref_{Guid.NewGuid():N}");

    public ByReferenceInteropTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private const string RefShapesSource = @"
using System.Runtime.CompilerServices;
namespace RefShapes
{
    [Sharpy.SharpyModule(""refshapes"")]
    public static class RefShapesModule
    {
        private static int _x = 5;
        private static int _y = 7;
        private static int _z = 9;
        public static int P = 3;
        public static ref int X => ref _x;
        public static ref readonly int Y => ref _y;
        public static ref int Z() => ref _z;
        [CompilerGenerated] public static int Hidden = 1;
        [CompilerGenerated] public static int HiddenCount => 2;
        [CompilerGenerated] public static int HiddenCall() => 2;
    }

    public sealed class Cells
    {
        private readonly int[] _a = { 1, 2, 3 };
        private int _n = 4;
        private static int _s = 11;
        public ref int this[int i] => ref _a[i];
        public ref int Count => ref _n;
        public static ref int Shared => ref _s;
        public ref int At(int i) => ref _a[i];
    }
}";

    private string BuildRefShapes()
    {
        var path = Path.Combine(_dir, "RefShapes.dll");
        var compilation = CSharpCompilation.Create(
            "RefShapes",
            new[] { CSharpSyntaxTree.ParseText(RefShapesSource) },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        return path;
    }

    private const string Trace = "def trace(s: str) -> int:\n    print(s)\n    return 1\n";

    /// <summary>A Sharpy library: a Guarded value (traced), a Plain one (no initializer), a function.</summary>
    private ProjectCompilationHelper BuildLibrary(out string dll)
    {
        var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ByRefLib").WithOutputType("library")
            .AddSourceFile("lib.spy", Trace + "lx: int = trace(\"lib.x\")\nplain: int\ndef f() -> int:\n    return 2\n");
        helper.CreateProjectFile();
        var result = helper.Compile();
        helper.AssertCompilationSucceeded(result);
        dll = result.OutputAssemblyPath!;
        return helper;
    }

    private CachedModuleDiscovery Discover(string dll)
    {
        var discovery = new CachedModuleDiscovery(new OverloadIndexCache(Path.Combine(_dir, "cache")));
        discovery.LoadAssembly(Assembly.LoadFile(dll));
        return discovery;
    }

    [Fact]
    public void AHandWrittenModule_ExportsItsOwnNames_AndNoGeneratedMember()
    {
        var discovery = Discover(BuildRefShapes());

        discovery.GetModuleFields("refshapes").Select(f => f.Name).Should().BeEquivalentTo(new[] { "p", "x", "y" },
            "a member marked [CompilerGenerated] is no export (D10)");
        discovery.GetModuleFunctions("refshapes").Select(f => f.Name).Distinct().Should().BeEquivalentTo(new[] { "z" });
    }

    [Fact]
    public void AByReferenceMember_ReadsAsItsElementType()
    {
        var discovery = Discover(BuildRefShapes());
        var fields = discovery.GetModuleFields("refshapes").ToDictionary(f => f.Name, f => f.Type);

        fields["x"].Should().Be(fields["p"], "a `ref int` property reads as an int");
        fields["y"].Should().Be(fields["p"], "a `ref readonly int` property reads as an int");
        discovery.GetModuleFunctions("refshapes").Single(f => f.Name == "z").ReturnType.Should().Be(fields["p"],
            "a ref-returning method returns an int");
    }

    [Fact]
    public void Storage_ComesFromTheAttribute_NeverTheClrShape()
    {
        // Observed where discovery holds it (lead ruling): CachedModuleDiscovery.IsModuleFieldGuarded.
        var csharp = Discover(BuildRefShapes());
        csharp.IsModuleFieldGuarded("refshapes", "x").Should().BeFalse("a C# `ref` static property is Plain (A8)");
        csharp.IsModuleFieldGuarded("refshapes", "y").Should().BeFalse();
        csharp.GetModuleLoadEntry("refshapes").Should().BeNull("a hand-written module declares no load entry");

        using var lib = BuildLibrary(out var dll);
        var sharpy = Discover(dll);
        sharpy.IsModuleFieldGuarded("lib", "lx").Should().BeTrue("its SharpyStaticStorage attribute records Guarded");
        sharpy.IsModuleFieldGuarded("lib", "plain").Should().BeFalse("a value with no initializer is Plain");
        sharpy.GetModuleLoadEntry("lib").Should().Be(new ModuleLoadEntry("ByRefLib.Lib.LibModule", true));
        sharpy.GetModuleFields("lib").Select(f => f.Name).Should().BeEquivalentTo(new[] { "lx", "plain" },
            "the library exports its source names, none of D4's helpers");
        sharpy.GetModuleFunctions("lib").Select(f => f.Name).Distinct().Should().BeEquivalentTo(new[] { "trace", "f" });
    }

    [Fact]
    public void EveryByReferenceMemberKind_ReadsAndWritesThroughTheReference()
    {
        var dll = BuildRefShapes();
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ByRefUse").WithEntryPoint("main.spy").WithOutputType("exe").WithAssemblyReference(dll);
        helper.AddSourceFile("main.spy",
            "import refshapes\nfrom refshapes import Cells\n"
            + "def main() -> None:\n"
            + "    refshapes.x = refshapes.x + 1\n"
            + "    print(refshapes.x, refshapes.y, refshapes.z(), refshapes.p)\n"
            + "    c = Cells()\n"
            + "    c.count = c.count + 1\n"
            + "    print(c[0], c.count, Cells.shared, c.at(1))\n", isEntryPoint: true);
        helper.CreateProjectFile();

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors) + result.Exception);
        result.StandardOutput.Should().Be("6 7 9 3\n1 5 11 2\n");
    }

    [Fact]
    public void ALibraryImportedByReference_LoadsAtItsImportsPosition()
    {
        using var lib = BuildLibrary(out var dll);
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("ByRefOrder").WithEntryPoint("main.spy").WithOutputType("exe").WithAssemblyReference(dll);
        helper.AddSourceFile("main.spy",
            Trace + "m1: int = trace(\"m1\")\nimport lib\nm2: int = trace(\"m2\") + lib.lx\n"
            + "def main() -> None:\n    print(\"main\", m1, m2)\n", isEntryPoint: true);
        helper.CreateProjectFile();

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors) + result.Exception);
        result.StandardOutput.Should().Be("m1\nlib.x\nm2\nmain 1 2\n");
    }
}
