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

    [Fact]
    public void PerModuleAssemblies_ExistInModulesOutputDir()
    {
        if (!Directory.Exists(PerModuleDir))
        {
            _output.WriteLine($"Per-module output dir not found: {PerModuleDir}");
            _output.WriteLine("Run 'dotnet build sharpy.sln' first.");
            return;
        }

        var perModuleDlls = Directory.GetFiles(PerModuleDir, "Sharpy.Stdlib.*.dll");
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
        if (!Directory.Exists(PerModuleDir))
            return;

        var mathDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Math.dll");
        if (!File.Exists(mathDll))
        {
            _output.WriteLine("Sharpy.Stdlib.Math.dll not found");
            return;
        }

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
        if (!Directory.Exists(PerModuleDir))
            return;

        var mathDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Math.dll");
        var randomDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Random.dll");
        if (!File.Exists(mathDll) || !File.Exists(randomDll))
        {
            _output.WriteLine("Per-module DLLs not found");
            return;
        }

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
        if (!Directory.Exists(PerModuleDir))
            return;

        var numpyDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Numpy.dll");
        if (!File.Exists(numpyDll))
        {
            _output.WriteLine("Sharpy.Stdlib.Numpy.dll not found");
            return;
        }

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
        if (!Directory.Exists(PerModuleDir))
            return;

        var osDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Os.dll");
        if (!File.Exists(osDll))
        {
            _output.WriteLine("Sharpy.Stdlib.Os.dll not found");
            return;
        }

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
        var cliDir = CoreDir;
        var mathNetDll = Path.Combine(cliDir, "MathNet.Numerics.dll");
        if (!File.Exists(mathNetDll))
        {
            _output.WriteLine("MathNet.Numerics.dll not found in CLI output dir");
            return;
        }

        var numpyAssembly = "Sharpy.Stdlib.Numpy.dll";
        var numpyPath = Path.Combine(cliDir, numpyAssembly);
        if (!File.Exists(numpyPath) && Directory.Exists(PerModuleDir))
            numpyPath = Path.Combine(PerModuleDir, numpyAssembly);

        if (!File.Exists(numpyPath))
        {
            _output.WriteLine("Sharpy.Stdlib.Numpy.dll not found");
            return;
        }

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(numpyPath);
        registry.GetModuleFunctions("numpy");
        var used = registry.GetUsedAssemblyPaths();

        Assert.Contains(used, p => Path.GetFileName(p).Contains("Numpy", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"Numpy assembly tracked, MathNet.Numerics.dll present at: {mathNetDll}");
    }

    [Fact]
    public void MultipleModuleImports_TracksAllUsedAssemblies()
    {
        if (!Directory.Exists(PerModuleDir))
            return;

        var mathDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Math.dll");
        var randomDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Random.dll");
        var osDll = Path.Combine(PerModuleDir, "Sharpy.Stdlib.Os.dll");
        if (!File.Exists(mathDll) || !File.Exists(randomDll) || !File.Exists(osDll))
        {
            _output.WriteLine("Required per-module DLLs not found");
            return;
        }

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
