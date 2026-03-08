using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Base class for tests that verify parity between single-file and multi-file compilation modes.
/// </summary>
public abstract class DualModeIntegrationTestBase : IntegrationTestBase
{
    protected DualModeIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Compiles and executes source code using single-file compilation (via Compiler.Compile).
    /// </summary>
    protected ExecutionResult CompileAndExecuteSingleFile(string source)
    {
        return CompileAndExecute(source);
    }

    /// <summary>
    /// Compiles and executes source code using multi-file project compilation
    /// (via ProjectCompilationHelper with a single main.spy file).
    /// </summary>
    protected ExecutionResult CompileAndExecuteMultiFile(string source)
    {
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("DualModeTest")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", source)
            .CreateProjectFile();

        var helperResult = helper.CompileAndExecute();

        // Map ProjectCompilationHelper's ExecutionResult to IntegrationTestBase's ExecutionResult
        return new IntegrationTestBase.ExecutionResult
        {
            Success = helperResult.Success,
            StandardOutput = helperResult.StandardOutput,
            StandardError = helperResult.StandardError,
            CompilationErrors = helperResult.CompilationErrors
        };
    }

    /// <summary>
    /// Compiles and executes source code in both single-file and multi-file modes,
    /// asserting they produce identical results.
    /// </summary>
    protected ExecutionResult CompileAndExecuteBothModes(string source)
    {
        var singleFileResult = CompileAndExecuteSingleFile(source);
        var multiFileResult = CompileAndExecuteMultiFile(source);

        Assert.Equal(singleFileResult.Success, multiFileResult.Success);
        Assert.Equal(singleFileResult.StandardOutput, multiFileResult.StandardOutput);

        return singleFileResult;
    }
}
