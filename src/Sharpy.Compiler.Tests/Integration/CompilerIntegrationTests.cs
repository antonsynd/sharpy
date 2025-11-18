using Sharpy.Compiler;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for the full compilation pipeline with module support
/// </summary>
public class CompilerIntegrationTests
{
    [Fact]
    public void Compiler_WithDefaultConstructor_CompilesSuccessfully()
    {
        var code = @"
x = 5
y = 10
z = x + y
";
        var compiler = new Compiler();
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Module);
    }

    [Fact]
    public void Compiler_WithCompilerOptions_LoadsBuiltins()
    {
        var code = @"
result = range(5)
";
        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("builtins", result.ModuleRegistry.GetLoadedModules());
    }

    [Fact]
    public void Compiler_WithSampleModule_LoadsSuccessfully()
    {
        var code = @"
x = 5
";
        // Get the path to SampleModule.dll
        var sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Skip test if SampleModule hasn't been built
        if (!File.Exists(sampleModulePath))
        {
            Assert.True(true, $"Test skipped: SampleModule not found at {sampleModulePath}");
            return;
        }

        var options = new CompilerOptions
        {
            References = new[] { sampleModulePath }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("samplemodule", result.ModuleRegistry.GetLoadedModules());

        // Verify functions were discovered
        var functions = result.ModuleRegistry.GetModuleFunctions("samplemodule");
        Assert.NotEmpty(functions);
        Assert.Contains(functions, f => f.Name == "square");
        Assert.Contains(functions, f => f.Name == "cube");
        Assert.Contains(functions, f => f.Name == "average");
        Assert.Contains(functions, f => f.Name == "is_prime");
        Assert.Contains(functions, f => f.Name == "factorial");
    }

    [Fact]
    public void Compiler_WithInvalidReference_ReportsError()
    {
        var code = @"
x = 5
";
        var options = new CompilerOptions
        {
            References = new[] { "NonExistent.dll" }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("NonExistent.dll"));
    }

    [Fact]
    public void Compiler_WithModulePath_AddsSuccessfully()
    {
        var code = @"
x = 5
";
        var tempPath = Path.GetTempPath();
        var options = new CompilerOptions
        {
            ModulePaths = new[] { tempPath }
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Compiler_WithMultipleReferences_LoadsAll()
    {
        var code = @"
x = 5
";
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        var sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Only include SampleModule if it exists
        var references = File.Exists(sampleModulePath)
            ? new[] { sharpyCoreAssembly, sampleModulePath }
            : new[] { sharpyCoreAssembly };

        var options = new CompilerOptions
        {
            References = references
        };

        var compiler = new Compiler(options);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
        Assert.NotNull(result.ModuleRegistry);
        Assert.Contains("builtins", result.ModuleRegistry.GetLoadedModules());

        if (File.Exists(sampleModulePath))
        {
            Assert.Contains("samplemodule", result.ModuleRegistry.GetLoadedModules());
        }
    }

    [Fact]
    public void Compiler_WithLogger_LogsModuleLoading()
    {
        var code = @"
x = 5
";
        var logOutput = new StringWriter();
        var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, logOutput, logOutput);

        var options = new CompilerOptions
        {
            References = new[] { typeof(Sharpy.Core.Exports).Assembly.Location }
        };

        var compiler = new Compiler(options, logger);
        var result = compiler.Compile(code, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Errors));

        var log = logOutput.ToString();
        Assert.Contains("Loaded module reference", log);
    }
}
