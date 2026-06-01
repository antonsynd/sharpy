extern alias SharpyRT;

using System.Reflection;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Validates that the native pipeline (sharpyc project stdlib.spyproj) produces a correct
/// assembly from .spy stdlib modules.
/// </summary>
public class NativeStdlibCompilationTests
{
    private readonly ITestOutputHelper _output;
    private readonly ICompilerLogger _logger;

    private static readonly string StdlibSpyDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Sharpy.Stdlib", "spy"));

    private static readonly string SpyprojPath = Path.Combine(StdlibSpyDir, "stdlib.spyproj");

    private static readonly string[] ExpectedModules = new[]
    {
        "base64_module",
        "bisect_module",
        "fnmatch_module",
        "functools",
        "hashlib_module",
        "heapq",
        "hmac_module",
        "itertools",
        "math_module",
        "os_module",
        "os_path_module",
        "random_module",
        "secrets_module",
        "shutil_module",
        "statistics",
        "string_module",
        "tempfile_module",
        "textwrap",
        "uuid_module",
        "platform_module",
        "urllib_module",
        "struct_module",
        "zlib_module",
        "gzip_module",
        "zipfile_module",
        "shlex_module",
        "subprocess_module",
        "configparser_module",
        "ipaddress_module",
        "xml_module",
        "html_module",
    };

    public NativeStdlibCompilationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestHelpers.OutputTestLogger(output);
    }

    [Fact]
    public void StdlibSpyproj_Exists()
    {
        Assert.True(File.Exists(SpyprojPath),
            $"stdlib.spyproj not found at {SpyprojPath}");
    }

    [Fact]
    public void StdlibSpyproj_AllSpyFilesPresent()
    {
        foreach (var module in ExpectedModules)
        {
            var spyFile = Path.Combine(StdlibSpyDir, $"{module}.spy");
            Assert.True(File.Exists(spyFile), $"Missing .spy file: {module}.spy");
        }
    }

    [Fact]
    public void NativeCompilation_Succeeds()
    {
        var result = CompileStdlibProject();

        foreach (var diag in result.Diagnostics.GetErrors())
            _output.WriteLine($"ERROR: {diag}");

        Assert.True(result.Success, "Native stdlib compilation failed");
        Assert.NotNull(result.OutputAssemblyPath);
        Assert.True(File.Exists(result.OutputAssemblyPath),
            $"Output assembly not found at {result.OutputAssemblyPath}");
    }

    [Fact]
    public void NativeCompilation_ProducesAllModuleClasses()
    {
        var result = CompileStdlibProject();
        Assert.True(result.Success, "Compilation must succeed first");

        var assembly = Assembly.LoadFrom(result.OutputAssemblyPath!);
        var types = assembly.GetExportedTypes();

        _output.WriteLine($"Found {types.Length} exported types:");
        foreach (var type in types.OrderBy(t => t.FullName))
            _output.WriteLine($"  {type.FullName}");

        Assert.True(types.Length >= ExpectedModules.Length,
            $"Expected at least {ExpectedModules.Length} types but found {types.Length}");
    }

    [Fact]
    public void NativeCompilation_HasSharpyModuleAttributes()
    {
        var result = CompileStdlibProject();
        Assert.True(result.Success, "Compilation must succeed first");

        var assembly = Assembly.LoadFrom(result.OutputAssemblyPath!);

        var modulesWithAttribute = new List<(Type Type, string ModuleName)>();
        foreach (var type in assembly.GetExportedTypes())
        {
            var attr = type.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "SharpyModuleAttribute");
            if (attr != null)
            {
                var moduleName = (string)attr.GetType()
                    .GetProperty("ModuleName")!
                    .GetValue(attr)!;
                modulesWithAttribute.Add((type, moduleName));
            }
        }

        _output.WriteLine($"Found {modulesWithAttribute.Count} [SharpyModule] types:");
        foreach (var (type, name) in modulesWithAttribute.OrderBy(x => x.ModuleName))
            _output.WriteLine($"  [{name}] -> {type.FullName}");

        foreach (var expectedModule in ExpectedModules)
        {
            Assert.True(
                modulesWithAttribute.Any(m => m.ModuleName == expectedModule),
                $"Missing [SharpyModule(\"{expectedModule}\")] attribute");
        }
    }

    [Fact]
    public void NativeCompilation_GeneratesCSharpFiles()
    {
        var result = CompileStdlibProject();
        Assert.True(result.Success, "Compilation must succeed first");
        Assert.NotEmpty(result.GeneratedCSharpFiles);

        _output.WriteLine($"Generated {result.GeneratedCSharpFiles.Count} C# files:");
        foreach (var (path, _) in result.GeneratedCSharpFiles.OrderBy(kv => kv.Key))
            _output.WriteLine($"  {Path.GetFileName(path)}");

        Assert.Equal(ExpectedModules.Length, result.GeneratedCSharpFiles.Count);
    }

    private ProjectCompilationResult CompileStdlibProject()
    {
        var config = ProjectFileParser.Load(SpyprojPath, "Debug");

        // Inject Sharpy.Core and Sharpy.Stdlib references (same as CLI does)
        var corePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        if (!config.References.Contains(corePath))
            config.References.Add(corePath);

        var coreDir = Path.GetDirectoryName(corePath)!;
        var stdlibPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        if (File.Exists(stdlibPath) && !config.References.Contains(stdlibPath))
            config.References.Add(stdlibPath);

        var options = new CompilerOptions
        {
            References = config.References.ToArray(),
            WarningsAsErrors = false
        };

        var compiler = new Compiler(options, _logger);
        return compiler.CompileProject(config);
    }
}
