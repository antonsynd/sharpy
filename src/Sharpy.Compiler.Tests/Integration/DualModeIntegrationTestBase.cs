using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

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
    /// (via CompileAndExecuteProject with a single main.spy file in a temp directory).
    /// </summary>
    protected ExecutionResult CompileAndExecuteMultiFile(string source)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_dual_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), source);
            return CompileAndExecuteProject(tempDir, "main.spy");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
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
