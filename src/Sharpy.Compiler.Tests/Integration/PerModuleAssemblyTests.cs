extern alias SharpyRT;

using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

public class PerModuleAssemblyTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string CoreDir = Path.GetDirectoryName(
        typeof(SharpyRT::Sharpy.Builtins).Assembly.Location)!;

    private static readonly string PerModuleDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Sharpy.Stdlib", "modules", "bin", "Debug", "net10.0"));

    public PerModuleAssemblyTests(ITestOutputHelper output) => _output = output;

    private static string[] GetStdlibReferences()
    {
        var corePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var refs = new List<string> { corePath };
        var monolith = Path.Combine(CoreDir, "Sharpy.Stdlib.dll");
        if (File.Exists(monolith))
        {
            refs.Add(monolith);
        }
        else
        {
            refs.AddRange(Directory.GetFiles(CoreDir, "Sharpy.Stdlib.*.dll"));
        }
        return refs.ToArray();
    }

    /// <summary>
    /// The per-module output directory, asserted rather than assumed. This suite reasons about
    /// artifacts built by a DIFFERENT project — the 60 csprojs under <c>src/Sharpy.Stdlib/modules/</c>
    /// — and nothing in this test project's graph makes them exist.
    ///
    /// <para>Seven of the nine tests used to <c>return</c> silently when the directory was absent,
    /// which a project-scoped <c>dotnet test</c> produces. A silent return is indistinguishable
    /// from a pass, so the suite's outcome was decided by how the tree happened to be built, and it
    /// could have been one build-configuration change away from testing nothing, permanently and
    /// invisibly (#1479). Unlike #1432 there was not even a 0/0 counter to tip anyone off.</para>
    ///
    /// <para>Declaring the 60 projects as build-order references was measured first and rejected:
    /// it tripled a project-scoped build of this project, 5.8s to 17.8s, on a build lock that is a
    /// shared machine-wide bottleneck — to guarantee something <c>dotnet build sharpy.sln</c>
    /// already guarantees, since all 60 are in the solution. Failing loudly costs nothing and says
    /// the same thing.</para>
    /// </summary>
    private static string RequirePerModuleDir()
    {
        Assert.True(Directory.Exists(PerModuleDir),
            $"Per-module output directory not found: {PerModuleDir}\n"
            + "These tests assert on assemblies built by src/Sharpy.Stdlib/modules/*.csproj, which "
            + "a project-scoped build does not produce. Run 'dotnet build sharpy.sln' first.\n"
            + "This is deliberately a failure and not a silent skip: passing here would mean "
            + "reporting success for assertions that never ran (#1479).");

        return PerModuleDir;
    }

    /// <summary>
    /// One per-module assembly, asserted present. Covers the narrow third state that produced an
    /// intermittent-looking red in five-project gates: the directory exists but holds no
    /// <c>Sharpy.Stdlib.*.dll</c>, from a cleaned or half-built modules output.
    /// </summary>
    private static string RequireModuleAssembly(string fileName)
    {
        var path = Path.Combine(RequirePerModuleDir(), fileName);
        Assert.True(File.Exists(path),
            $"{fileName} not found in {PerModuleDir}\n"
            + "The per-module output directory exists but does not hold this assembly — a cleaned "
            + "or half-built modules output. Run 'dotnet build sharpy.sln'.");

        return path;
    }

    [Fact]
    public void PerModuleAssemblies_ExistInModulesOutputDir()
    {
        var perModuleDlls = Directory.GetFiles(RequirePerModuleDir(), "Sharpy.Stdlib.*.dll");
        Assert.NotEmpty(perModuleDlls);
        _output.WriteLine($"Found {perModuleDlls.Length} per-module assemblies in {PerModuleDir}");

        foreach (var dll in perModuleDlls.OrderBy(p => p))
        {
            _output.WriteLine($"  {Path.GetFileName(dll)}");
        }
    }

    [Fact]
    public void PerModuleAssemblies_LoadIndependently()
    {
        var mathDll = RequireModuleAssembly("Sharpy.Stdlib.Math.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        Assert.True(registry.LoadReference(mathDll));
        Assert.True(registry.IsModuleLoaded("math"));
        Assert.False(registry.IsModuleLoaded("random"));

        var functions = registry.GetModuleFunctions("math");
        Assert.NotEmpty(functions);
        _output.WriteLine($"math module has {functions.Count} functions");
    }

    [Fact]
    public void UsedAssemblyPaths_TracksOnlyAccessedModules()
    {
        var mathDll = RequireModuleAssembly("Sharpy.Stdlib.Math.dll");
        var randomDll = RequireModuleAssembly("Sharpy.Stdlib.Random.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(mathDll);
        registry.LoadReference(randomDll);

        Assert.Empty(registry.GetUsedAssemblyPaths());

        registry.GetModuleFunctions("math");
        var used = registry.GetUsedAssemblyPaths();
        Assert.Single(used);
        Assert.Contains(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Math.dll");

        registry.GetModuleFunctions("random");
        used = registry.GetUsedAssemblyPaths();
        Assert.Equal(2, used.Count);
    }

    [Fact]
    public void CompileResult_IncludesUsedAssemblyPaths_WhenStdlibImported()
    {
        var defaultRefs = GetStdlibReferences();
        var api = new CompilerApi(NullLogger.Instance, defaultRefs);

        var source = @"
import math

def main():
    x: int = int(math.factorial(5))
    print(x)
";
        var result = api.Compile(source);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));

        Assert.NotEmpty(result.UsedAssemblyPaths);
        _output.WriteLine($"Used assemblies: {string.Join(", ", result.UsedAssemblyPaths.Select(Path.GetFileName))}");

        Assert.Contains(result.UsedAssemblyPaths,
            p => Path.GetFileName(p).Contains("Math", StringComparison.OrdinalIgnoreCase)
              || Path.GetFileName(p).Contains("Stdlib", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompileResult_NoStdlibUsed_WhenNoImports()
    {
        var defaultRefs = GetStdlibReferences();
        var api = new CompilerApi(NullLogger.Instance, defaultRefs);

        var source = @"
def main():
    print(42)
";
        var result = api.Compile(source);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));

        var stdlibPaths = result.UsedAssemblyPaths
            .Where(p => Path.GetFileName(p).StartsWith("Sharpy.Stdlib", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(stdlibPaths);
    }

    [Fact]
    public void GroupedModule_NumpyLinalg_LoadsNumpyAssembly()
    {
        var numpyDll = RequireModuleAssembly("Sharpy.Stdlib.Numpy.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(numpyDll);

        Assert.True(registry.IsModuleLoaded("numpy"));
        Assert.True(registry.IsModuleLoaded("numpy.linalg"));
        Assert.True(registry.IsModuleLoaded("numpy.random"));

        registry.GetModuleFunctions("numpy.linalg");
        var used = registry.GetUsedAssemblyPaths();
        Assert.Single(used);
        Assert.Contains(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Numpy.dll");
    }

    [Fact]
    public void GroupedModule_OsPath_LoadsOsAssembly()
    {
        var osDll = RequireModuleAssembly("Sharpy.Stdlib.Os.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(osDll);

        Assert.True(registry.IsModuleLoaded("os"));
        Assert.True(registry.IsModuleLoaded("os.path"));

        registry.GetModuleFunctions("os");
        registry.GetModuleFunctions("os.path");
        var used = registry.GetUsedAssemblyPaths();
        Assert.Single(used);
        Assert.Contains(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Os.dll");
    }

    [Fact]
    public void NuGetDepMapping_NumpyRequiresMathNet()
    {
        // The hand-maintained numpy→MathNet NuGet map is gone (#1084); the dependency is now
        // derived mechanically. MathNet.Numerics must appear in the transitive managed closure
        // of the stdlib references (numpy is implemented on top of MathNet).
        // Unlike the per-module directory, this one IS guaranteed by the project graph: numpy's
        // MathNet dependency flows through the Sharpy.Stdlib ProjectReference into this test's own
        // output. Asserted rather than skipped for the same reason as the rest (#1479).
        var mathNetDll = Path.Combine(CoreDir, "MathNet.Numerics.dll");
        Assert.True(File.Exists(mathNetDll),
            $"MathNet.Numerics.dll not found in the test host output dir: {CoreDir}\n"
            + "It should arrive transitively via the Sharpy.Stdlib project reference.");

        var references = GetStdlibReferences();
        var closure = RuntimeClosureResolver.Resolve(references);

        Assert.Contains(closure.ManagedAssemblies,
            p => Path.GetFileName(p).Equals("MathNet.Numerics.dll", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"MathNet.Numerics.dll resolved in stdlib closure: {mathNetDll}");
    }

    [Fact]
    public void MultipleModuleImports_TracksAllUsedAssemblies()
    {
        var mathDll = RequireModuleAssembly("Sharpy.Stdlib.Math.dll");
        var randomDll = RequireModuleAssembly("Sharpy.Stdlib.Random.dll");
        var osDll = RequireModuleAssembly("Sharpy.Stdlib.Os.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(mathDll);
        registry.LoadReference(randomDll);
        registry.LoadReference(osDll);

        registry.GetModuleFunctions("math");
        registry.GetModuleFunctions("os");

        var used = registry.GetUsedAssemblyPaths();
        Assert.Equal(2, used.Count);
        Assert.Contains(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Math.dll");
        Assert.Contains(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Os.dll");
        Assert.DoesNotContain(used, p => Path.GetFileName(p) == "Sharpy.Stdlib.Random.dll");
    }
}
