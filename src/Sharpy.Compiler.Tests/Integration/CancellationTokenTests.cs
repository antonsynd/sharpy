using System.Linq;
using System.Threading;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests that CancellationToken support works correctly in single-file
/// and project compilation pipelines.
/// </summary>
public class CancellationTokenTests
{
    private const string SimpleSource = @"
def main():
    x: int = 42
    y: int = x + 1
";

    [Fact]
    public void Compile_WithNonCancelledToken_Succeeds()
    {
        var compiler = new Compiler();
        var result = compiler.Compile(SimpleSource, "test.spy", CancellationToken.None);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
    }

    [Fact]
    public void Compile_WithAlreadyCancelledToken_ReturnsCancelledResult()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var compiler = new Compiler();
        var result = compiler.Compile(SimpleSource, "test.spy", cts.Token);

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
        var errors = result.Diagnostics.GetErrors().ToList();
        Assert.Contains(errors, d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    [Fact]
    public void Compile_WithDefaultOverload_UsesNoneToken()
    {
        // Verifying the convenience overload without CancellationToken works
        var compiler = new Compiler();
        var result = compiler.Compile(SimpleSource, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
    }

    [Fact]
    public void ProjectCompiler_WithAlreadyCancelledToken_ReturnsCancelledResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cancel_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, SimpleSource);

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "CancelTest",
                SourceFiles = new List<string> { mainPath }
            };

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var projectCompiler = new ProjectCompiler();
            var result = projectCompiler.Compile(config, cts.Token);

            Assert.False(result.Success);
            Assert.True(result.Diagnostics.HasErrors);
            var errors = result.Diagnostics.GetErrors().ToList();
            Assert.Contains(errors, d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectCompiler_WithNonCancelledToken_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cancel_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, SimpleSource);

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "CancelTest",
                SourceFiles = new List<string> { mainPath }
            };

            var projectCompiler = new ProjectCompiler();
            var result = projectCompiler.Compile(config, CancellationToken.None);

            Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectCompiler_WithDefaultOverload_UsesNoneToken()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cancel_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, SimpleSource);

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "CancelTest",
                SourceFiles = new List<string> { mainPath }
            };

            var projectCompiler = new ProjectCompiler();
            var result = projectCompiler.Compile(config);

            Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
