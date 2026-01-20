using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class ProjectCompilerModelIntegrationTests
{
    [Fact]
    public void Compile_PopulatesProjectModel()
    {
        // Create a simple test project
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "x: int = 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        // Verify ProjectModel is populated
        Assert.NotNull(result.ProjectModel);
        Assert.Equal(1, result.ProjectModel.UnitCount);

        var unit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(unit);
        Assert.Equal("main", unit.ModulePath);
        Assert.NotNull(unit.Ast);
        Assert.NotNull(unit.Tokens);
        Assert.NotEmpty(unit.ContentHash);

        if (result.Success)
        {
            Assert.Equal(CompilationPhase.CodeGenerated, unit.Phase);
            Assert.NotNull(unit.GeneratedCSharp);
        }
    }

    [Fact]
    public void Compile_MultiFile_AllUnitsPopulated()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", @"from utils import helper
x = helper()");
        var utilsSpy = tempDir.CreateFile("utils.spy", @"def helper() -> int:
    return 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy, utilsSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.NotNull(result.ProjectModel);
        Assert.Equal(2, result.ProjectModel.UnitCount);

        Assert.NotNull(result.ProjectModel.GetUnit(mainSpy));
        Assert.NotNull(result.ProjectModel.GetUnit(utilsSpy));
    }

    [Fact]
    public void Compile_Error_UnitHasDiagnostics()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "x: int = \"not an int\"");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.False(result.Success);
        Assert.NotNull(result.ProjectModel);

        var unit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(unit);
        Assert.True(unit.HasErrors);
    }

    [Fact]
    public void Compile_ParseError_UnitHasFailedPhase()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "def foo( -> int:");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.False(result.Success);
        Assert.NotNull(result.ProjectModel);

        var unit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(unit);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }

    [Fact]
    public void Compile_ExtractsImports()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", @"import math
from utils import helper
x = 1");
        var utilsSpy = tempDir.CreateFile("utils.spy", @"def helper() -> int:
    return 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy, utilsSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.NotNull(result.ProjectModel);

        var mainUnit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(mainUnit);
        Assert.Single(mainUnit.Imports);
        Assert.Single(mainUnit.FromImports);
    }

    [Fact]
    public void Compile_StoresDependencyGraph()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", @"from utils import helper
x = helper()");
        var utilsSpy = tempDir.CreateFile("utils.spy", @"def helper() -> int:
    return 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy, utilsSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.NotNull(result.ProjectModel);
        Assert.NotNull(result.ProjectModel.DependencyGraph);
    }

    [Fact]
    public void Compile_StoresGlobalSymbols()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "x: int = 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.NotNull(result.ProjectModel);
        Assert.NotNull(result.ProjectModel.GlobalSymbols);
        Assert.NotNull(result.ProjectModel.SemanticInfo);
    }
}

/// <summary>
/// Helper class for creating temporary directories in tests.
/// </summary>
internal class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
