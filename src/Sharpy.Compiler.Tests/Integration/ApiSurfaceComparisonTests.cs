extern alias SharpyRT;
extern alias SharpyStdlib;

using System.Reflection;
using System.Text;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Compares the public API surface of the native-compiled Sharpy.Stdlib.Spy.dll
/// against the MSBuild-compiled Sharpy.Stdlib.dll to ensure parity for .spy modules.
/// </summary>
public class ApiSurfaceComparisonTests
{
    private readonly ITestOutputHelper _output;
    private readonly ICompilerLogger _logger;

    private static readonly string StdlibSpyDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Sharpy.Stdlib", "spy"));

    private static readonly string SpyprojPath = Path.Combine(StdlibSpyDir, "stdlib.spyproj");

    // Maps native module names (from filename) to MSBuild module names (from __Init__.cs)
    private static readonly Dictionary<string, string> NativeToMSBuildModuleNames = new()
    {
        ["bisect_module"] = "bisect",
        ["fnmatch_module"] = "fnmatch",
        ["functools"] = "functools",
        ["hashlib_module"] = "hashlib",
        ["heapq"] = "heapq",
        ["itertools"] = "itertools",
        ["math_module"] = "math",
        ["os_module"] = "os",
        ["os_path_module"] = "os.path",
        ["random_module"] = "random",
        ["shutil_module"] = "shutil",
        ["statistics"] = "statistics",
        ["string_module"] = "string",
        ["tempfile_module"] = "tempfile",
        ["textwrap"] = "textwrap",
        ["shlex_module"] = "shlex",
    };

    public ApiSurfaceComparisonTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestHelpers.OutputTestLogger(output);
    }

    [Fact]
    public void ApiSurface_NativeMethodsExistInMSBuild()
    {
        var nativeResult = CompileStdlibProject();
        Assert.True(nativeResult.Success, "Native compilation must succeed");

        var nativeAssembly = Assembly.LoadFrom(nativeResult.OutputAssemblyPath!);
        var msbuildAssembly = typeof(SharpyStdlib::Sharpy.Textwrap).Assembly;

        var nativeModules = GetSharpyModuleTypes(nativeAssembly);
        var msbuildModules = GetSharpyModuleTypes(msbuildAssembly);

        var mismatches = new List<string>();
        var matchedMethods = 0;

        foreach (var (nativeModuleName, nativeType) in nativeModules)
        {
            if (!NativeToMSBuildModuleNames.TryGetValue(nativeModuleName, out var msbuildModuleName))
            {
                _output.WriteLine($"WARNING: No module name mapping for native module '{nativeModuleName}'");
                continue;
            }

            if (!msbuildModules.TryGetValue(msbuildModuleName, out var msbuildType))
            {
                mismatches.Add($"Module '{msbuildModuleName}' (native: '{nativeModuleName}') not found in MSBuild assembly");
                continue;
            }

            var nativeMethods = GetPublicMethodSignatures(nativeType);
            var msbuildMethods = GetPublicMethodSignatures(msbuildType);

            foreach (var (sig, _) in nativeMethods)
            {
                if (msbuildMethods.ContainsKey(sig))
                {
                    matchedMethods++;
                }
                else
                {
                    mismatches.Add($"[{nativeModuleName}] Method '{sig}' in native but not in MSBuild '{msbuildModuleName}'");
                }
            }
        }

        _output.WriteLine($"Matched {matchedMethods} methods across {nativeModules.Count} modules");

        if (mismatches.Count > 0)
        {
            _output.WriteLine($"\n{mismatches.Count} mismatch(es):");
            foreach (var m in mismatches)
                _output.WriteLine($"  - {m}");
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void ApiSurface_ReportsExpectedSubsetMethods()
    {
        var nativeResult = CompileStdlibProject();
        Assert.True(nativeResult.Success, "Native compilation must succeed");

        var nativeAssembly = Assembly.LoadFrom(nativeResult.OutputAssemblyPath!);
        var msbuildAssembly = typeof(SharpyStdlib::Sharpy.Textwrap).Assembly;

        var nativeModules = GetSharpyModuleTypes(nativeAssembly);
        var msbuildModules = GetSharpyModuleTypes(msbuildAssembly);

        var msbuildOnlyMethods = 0;

        foreach (var (nativeModuleName, nativeType) in nativeModules)
        {
            if (!NativeToMSBuildModuleNames.TryGetValue(nativeModuleName, out var msbuildModuleName))
                continue;
            if (!msbuildModules.TryGetValue(msbuildModuleName, out var msbuildType))
                continue;

            var nativeMethods = GetPublicMethodSignatures(nativeType);
            var msbuildMethods = GetPublicMethodSignatures(msbuildType);

            foreach (var (sig, _) in msbuildMethods)
            {
                if (!nativeMethods.ContainsKey(sig))
                {
                    _output.WriteLine($"[{msbuildModuleName}] MSBuild-only: {sig}");
                    msbuildOnlyMethods++;
                }
            }
        }

        _output.WriteLine($"\n{msbuildOnlyMethods} methods exist only in MSBuild (expected subset behavior)");
        // This test documents the subset behavior — it always passes
    }

    private static Dictionary<string, MethodInfo> GetPublicMethodSignatures(Type type)
    {
        var result = new Dictionary<string, MethodInfo>();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            if (method.Name.StartsWith("<") || method.IsSpecialName)
                continue;

            var sig = FormatMethodSignature(method);
            result[sig] = method;
        }

        return result;
    }

    private static string FormatMethodSignature(MethodInfo method)
    {
        var sb = new StringBuilder();
        sb.Append(method.Name);

        if (method.IsGenericMethod)
        {
            var typeParams = method.GetGenericArguments();
            sb.Append('<');
            sb.Append(string.Join(", ", typeParams.Select(t => t.Name)));
            sb.Append('>');
        }

        sb.Append('(');
        var parameters = method.GetParameters();
        sb.Append(string.Join(", ", parameters.Select(p => FormatTypeName(p.ParameterType))));
        sb.Append(") -> ");
        sb.Append(FormatTypeName(method.ReturnType));

        return sb.ToString();
    }

    private static string FormatTypeName(Type type)
    {
        if (type == typeof(void))
            return "void";
        if (type == typeof(int))
            return "int";
        if (type == typeof(long))
            return "long";
        if (type == typeof(double))
            return "double";
        if (type == typeof(float))
            return "float";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(string))
            return "string";
        if (type == typeof(object))
            return "object";

        if (type.IsGenericType)
        {
            var name = type.Name;
            var backtick = name.IndexOf('`');
            if (backtick > 0)
                name = name.Substring(0, backtick);
            var args = type.GetGenericArguments().Select(FormatTypeName);
            return $"{name}<{string.Join(", ", args)}>";
        }

        if (type.IsArray)
            return $"{FormatTypeName(type.GetElementType()!)}[]";

        return type.Name;
    }

    private static Dictionary<string, Type> GetSharpyModuleTypes(Assembly assembly)
    {
        var result = new Dictionary<string, Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            var attr = type.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "SharpyModuleAttribute");
            if (attr != null)
            {
                var moduleName = (string)attr.GetType()
                    .GetProperty("ModuleName")!
                    .GetValue(attr)!;
                result[moduleName] = type;
            }
        }
        return result;
    }

    private ProjectCompilationResult CompileStdlibProject()
    {
        var config = ProjectFileParser.Load(SpyprojPath, "Debug");

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
