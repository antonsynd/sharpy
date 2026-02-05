using System.Linq;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests;

public class ProjectCompilationTests
{
    private readonly ICompilerLogger _logger = NullLogger.Instance;

    [Fact]
    public void ProjectFileParser_Load_ParsesValidProject()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <Configuration>Debug</Configuration>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/main.spy"" />
        <SpyFile Include=""src/utils.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "main.spy"), "# main");
            File.WriteAllText(Path.Combine(tempDir, "src", "utils.spy"), "# utils");

            // Act
            var config = ProjectFileParser.Load(projectPath);

            // Assert
            Assert.Equal("TestApp", config.RootNamespace);
            Assert.Equal("exe", config.OutputType);
            Assert.Equal("Debug", config.Configuration);
            Assert.Equal(2, config.SourceFiles.Count);
            Assert.Contains(config.SourceFiles, f => f.EndsWith("src/main.spy") || f.EndsWith("src\\main.spy"));
            Assert.Contains(config.SourceFiles, f => f.EndsWith("src/utils.spy") || f.EndsWith("src\\utils.spy"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_ExpandsGlobPatterns()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "utils"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");
        var mainPath = Path.Combine(tempDir, "src", "main.spy");
        var utilsPath = Path.Combine(tempDir, "src", "utils", "helpers.spy");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(mainPath, "# Main file");
            File.WriteAllText(utilsPath, "# Utils file");

            // Act
            var config = ProjectFileParser.Load(projectPath);

            // Assert
            Assert.Equal(2, config.SourceFiles.Count);
            Assert.Contains(config.SourceFiles, f => f.EndsWith("main.spy"));
            Assert.Contains(config.SourceFiles, f => f.EndsWith("helpers.spy"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_FindProjectFile_FindsSingleProject()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>Test</RootNamespace>
    </PropertyGroup>
</Project>");

            // Act
            var foundPath = ProjectFileParser.FindProjectFile(tempDir);

            // Assert
            Assert.Equal(projectPath, foundPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_FindProjectFile_ErrorsOnMultipleProjects()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var project1Path = Path.Combine(tempDir, "project1.spyproj");
        var project2Path = Path.Combine(tempDir, "project2.spyproj");

        try
        {
            File.WriteAllText(project1Path, @"<?xml version=""1.0"" encoding=""utf-8""?><Project></Project>");
            File.WriteAllText(project2Path, @"<?xml version=""1.0"" encoding=""utf-8""?><Project></Project>");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => ProjectFileParser.FindProjectFile(tempDir));
            Assert.Contains("Multiple .spyproj files found", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Compiler_CompileProject_CompilesMultipleFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");
        var mainPath = Path.Combine(tempDir, "src", "main.spy");
        var utilsPath = Path.Combine(tempDir, "src", "utils.spy");

        try
        {
            File.WriteAllText(projectPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>");

            File.WriteAllText(mainPath, @"
def greet(name: str) -> str:
    return f'Hello, {name}!'

def main():
    message: str = greet('World')
    print(message)
");

            File.WriteAllText(utilsPath, @"
def add_numbers(a: int, b: int) -> int:
    return a + b
");

            var config = ProjectFileParser.Load(projectPath);
            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Compiler_CompileProject_DetectsCrossFileImportErrors()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");
        var mainPath = Path.Combine(tempDir, "src", "main.spy");

        try
        {
            File.WriteAllText(projectPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>");

            File.WriteAllText(mainPath, @"
from nonexistent import something

def main():
    print(something)
");

            var config = ProjectFileParser.Load(projectPath);
            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.False(result.Success);
            Assert.True(result.Diagnostics.HasErrors);
            Assert.Contains(result.Diagnostics.GetErrors(), d => d.Message.Contains("Cannot find module 'nonexistent'"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Compiler_CompileProject_GeneratesCorrectNamespaces()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "utils"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");
        var mainPath = Path.Combine(tempDir, "src", "main.spy");
        var utilsPath = Path.Combine(tempDir, "src", "utils", "helpers.spy");

        try
        {
            File.WriteAllText(projectPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>MyApp</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>");

            File.WriteAllText(mainPath, @"
def main():
    print('Hello')
");

            File.WriteAllText(utilsPath, @"
def helper() -> int:
    return 42
");

            var config = ProjectFileParser.Load(projectPath);
            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.NotNull(result.OutputAssemblyPath);

            // Check that output was created
            Assert.True(File.Exists(result.OutputAssemblyPath), $"Expected output file at {result.OutputAssemblyPath}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ImportResolver_ResolveModulePath_Finds__init__spy()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "mypackage"));

        var initPath = Path.Combine(tempDir, "mypackage", "__init__.spy");

        try
        {
            File.WriteAllText(initPath, @"
def package_func() -> str:
    return 'from package'
");

            var resolver = new ImportResolver(_logger);
            resolver.SetCurrentModule(tempDir);

            // Act - try to import the package
            var mainContent = @"
from mypackage import package_func

def main():
    print(package_func())
";
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, mainContent);

            // Parse and resolve
            var lexer = new Sharpy.Compiler.Lexer.Lexer(mainContent, _logger);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();

            resolver.SetCurrentModule(mainPath);
            var fromImport = module.Body.OfType<Sharpy.Compiler.Parser.Ast.FromImportStatement>().First();
            var moduleInfo = resolver.ResolveFromImport(fromImport, tempDir);

            // Assert
            Assert.NotNull(moduleInfo);
            Assert.Contains("__init__.spy", moduleInfo.Path);
            Assert.True(moduleInfo.ExportedSymbols.ContainsKey("package_func"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_ParsesEntryPoint()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <EntryPoint>app.spy</EntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "app.spy"), "def main(): pass");

            // Act
            var config = ProjectFileParser.Load(projectPath);

            // Assert
            Assert.Equal("app.spy", config.EntryPoint);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_SupportsExcludePatterns()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" Exclude=""src/test/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "test"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "main.spy"), "# Main file");
            File.WriteAllText(Path.Combine(tempDir, "src", "test", "test_main.spy"), "# Test file");

            // Act
            var config = ProjectFileParser.Load(projectPath);

            // Assert
            Assert.Single(config.SourceFiles);
            Assert.Contains(config.SourceFiles, f => f.EndsWith("main.spy"));
            Assert.DoesNotContain(config.SourceFiles, f => f.EndsWith("test_main.spy"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_SupportsSourceFileElement()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
    </PropertyGroup>
    <ItemGroup>
        <SourceFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "main.spy"), "# Main file");

            // Act
            var config = ProjectFileParser.Load(projectPath);

            // Assert
            Assert.Single(config.SourceFiles);
            Assert.Contains(config.SourceFiles, f => f.EndsWith("main.spy"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SpyProjectLoader_Load_ParsesFullProject()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>MyApp</RootNamespace>
        <OutputType>Exe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <AssemblyName>MyApplication</AssemblyName>
        <EntryPoint>startup.spy</EntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <SourceFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "startup.spy"), "def main(): pass");

            // Act
            var project = SpyProjectLoader.Load(projectPath);

            // Assert
            Assert.Equal("MyApp", project.RootNamespace);
            Assert.Equal("Exe", project.OutputType);
            Assert.Equal("net8.0", project.TargetFramework);
            Assert.Equal("MyApplication", project.AssemblyName);
            Assert.Equal("startup.spy", project.EntryPoint);
            Assert.True(project.IsExecutable);
            Assert.NotNull(project.GetEntryPointPath());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SpyProject_ToProjectConfig_ConvertsCorrectly()
    {
        // Arrange
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <EntryPoint>main.spy</EntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        var projectPath = Path.Combine(tempDir, "test.spyproj");

        try
        {
            File.WriteAllText(projectPath, projectContent);
            File.WriteAllText(Path.Combine(tempDir, "src", "main.spy"), "def main(): pass");

            // Act
            var spyProject = SpyProjectLoader.Load(projectPath);
            var projectConfig = spyProject.ToProjectConfig();

            // Assert
            Assert.Equal(spyProject.RootNamespace, projectConfig.RootNamespace);
            Assert.Equal(spyProject.OutputType, projectConfig.OutputType);
            Assert.Equal(spyProject.TargetFramework, projectConfig.TargetFramework);
            Assert.Equal(spyProject.EntryPoint, projectConfig.EntryPoint);
            Assert.Equal(spyProject.SourceFiles, projectConfig.SourceFiles);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Compiler_CompileProject_UsesCustomEntryPoint()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        var projectPath = Path.Combine(tempDir, "test.spyproj");
        var startupPath = Path.Combine(tempDir, "src", "startup.spy");
        var utilsPath = Path.Combine(tempDir, "src", "utils.spy");

        try
        {
            File.WriteAllText(projectPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <EntryPoint>startup.spy</EntryPoint>
    </PropertyGroup>
    <ItemGroup>
        <SpyFile Include=""src/**/*.spy"" />
    </ItemGroup>
</Project>");

            File.WriteAllText(startupPath, @"
def main():
    print('Hello from startup!')
");

            File.WriteAllText(utilsPath, @"
def helper() -> str:
    return 'utility'
");

            var config = ProjectFileParser.Load(projectPath);
            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.NotNull(result.OutputAssemblyPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #region Dependency Graph Integration Tests

    [Fact]
    public void CompileProject_BuildsDependencyGraph()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
def main():
    print('Hello')
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                SourceFiles = new List<string> { Path.Combine(tempDir, "main.spy") }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.NotNull(result.DependencyGraph);
            Assert.Single(result.DependencyGraph.AllFiles);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CompileProject_DependencyGraphHasCorrectDependencies()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create utils.spy (no dependencies)
            var utilsPath = Path.Combine(tempDir, "utils.spy");
            File.WriteAllText(utilsPath, @"
def helper() -> int:
    return 42
");

            // Create main.spy (depends on utils)
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, @"
import utils

def main():
    print(utils.helper())
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                SourceFiles = new List<string> { mainPath, utilsPath }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.NotNull(result.DependencyGraph);

            // Verify main depends on utils
            var mainDeps = result.DependencyGraph.GetDirectDependencies(mainPath);
            Assert.Single(mainDeps);
            Assert.Contains(mainDeps, p => p.EndsWith("utils.spy", StringComparison.OrdinalIgnoreCase));

            // Verify utils has no dependencies
            var utilsDeps = result.DependencyGraph.GetDirectDependencies(utilsPath);
            Assert.Empty(utilsDeps);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CompileProject_CircularDependency_ReportsError()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a.spy (imports b)
            var aPath = Path.Combine(tempDir, "a.spy");
            File.WriteAllText(aPath, @"
import b

def a_func() -> int:
    return 1
");

            // Create b.spy (imports a - circular!)
            var bPath = Path.Combine(tempDir, "b.spy");
            File.WriteAllText(bPath, @"
import a

def b_func() -> int:
    return 2
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                SourceFiles = new List<string> { aPath, bPath }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics.GetErrors(), d => d.Message.Contains("Circular dependency"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CompileProject_CircularDependency_ErrorShowsChain()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a 3-file circular dependency: a -> b -> c -> a
            var aPath = Path.Combine(tempDir, "a.spy");
            File.WriteAllText(aPath, @"
import b

def a_func():
    pass
");

            var bPath = Path.Combine(tempDir, "b.spy");
            File.WriteAllText(bPath, @"
import c

def b_func():
    pass
");

            var cPath = Path.Combine(tempDir, "c.spy");
            File.WriteAllText(cPath, @"
import a

def c_func():
    pass
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                SourceFiles = new List<string> { aPath, bPath, cPath }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics.GetErrors(), d => d.Message.Contains("Circular dependency"));
            // The error should show the cycle chain with file names
            Assert.Contains(result.Diagnostics.GetErrors(), d =>
                d.Message.Contains("a.spy") && d.Message.Contains("b.spy") && d.Message.Contains("c.spy"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CompileProject_TransitiveDependencies_TrackedCorrectly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create base.spy (no dependencies)
            var basePath = Path.Combine(tempDir, "base.spy");
            File.WriteAllText(basePath, @"
BASE_VALUE: int = 42
");

            // Create utils.spy (depends on base)
            var utilsPath = Path.Combine(tempDir, "utils.spy");
            File.WriteAllText(utilsPath, @"
import base
def get_base() -> int:
    return base.BASE_VALUE
");

            // Create main.spy (depends on utils, transitively on base)
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, @"
import utils

def main():
    print(utils.get_base())
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                SourceFiles = new List<string> { mainPath, utilsPath, basePath }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.NotNull(result.DependencyGraph);

            // Verify build order: base -> utils -> main
            var buildOrder = result.DependencyGraph.GetBuildOrder();
            Assert.Equal(3, buildOrder.Count);

            // Find indices (using normalized paths)
            int baseIndex = -1, utilsIndex = -1, mainIndex = -1;
            for (int i = 0; i < buildOrder.Count; i++)
            {
                if (buildOrder[i].EndsWith("base.spy", StringComparison.OrdinalIgnoreCase))
                    baseIndex = i;
                if (buildOrder[i].EndsWith("utils.spy", StringComparison.OrdinalIgnoreCase))
                    utilsIndex = i;
                if (buildOrder[i].EndsWith("main.spy", StringComparison.OrdinalIgnoreCase))
                    mainIndex = i;
            }

            Assert.True(baseIndex >= 0, "base.spy should be in build order");
            Assert.True(utilsIndex >= 0, "utils.spy should be in build order");
            Assert.True(mainIndex >= 0, "main.spy should be in build order");
            Assert.True(baseIndex < utilsIndex, "base should be built before utils");
            Assert.True(utilsIndex < mainIndex, "utils should be built before main");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Transitive Import Tests

    /// <summary>
    /// Tests that types re-exported from a package __init__.spy file resolve correctly
    /// in downstream modules.
    ///
    /// Structure:
    /// - mypackage/__init__.spy: re-exports SomeClass from submodule
    /// - mypackage/submodule.spy: defines SomeClass
    /// - main.spy: imports from mypackage and uses SomeClass
    /// </summary>
    [Fact]
    public void TransitiveImports_ReExportedTypes_ResolveCorrectly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "mypackage"));

        try
        {
            // Define a class in the submodule
            File.WriteAllText(Path.Combine(tempDir, "mypackage", "submodule.spy"), @"
class SomeClass:
    value: int

    def __init__(self, value: int):
        self.value = value

    def get_value(self) -> int:
        return self.value
");

            // Re-export the class from the package __init__.spy
            File.WriteAllText(Path.Combine(tempDir, "mypackage", "__init__.spy"), @"
from mypackage.submodule import SomeClass
");

            // Use the re-exported class in main
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
from mypackage import SomeClass

def main():
    obj = SomeClass(42)
    print(obj.get_value())
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                EntryPoint = "main.spy",
                SourceFiles = new List<string>
                {
                    Path.Combine(tempDir, "main.spy"),
                    Path.Combine(tempDir, "mypackage", "__init__.spy"),
                    Path.Combine(tempDir, "mypackage", "submodule.spy")
                }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that types are visible through nested package re-exports.
    ///
    /// Structure:
    /// - pkg/subpkg/__init__.spy: re-exports Widget from module
    /// - pkg/subpkg/widgets.spy: defines Widget
    /// - pkg/__init__.spy: re-exports everything from subpkg
    /// - main.spy: imports from pkg and uses Widget
    /// </summary>
    [Fact]
    public void TransitiveImports_NestedPackages_TypesVisible()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "pkg"));
        Directory.CreateDirectory(Path.Combine(tempDir, "pkg", "subpkg"));

        try
        {
            // Define Widget in the deepest module
            File.WriteAllText(Path.Combine(tempDir, "pkg", "subpkg", "widgets.spy"), @"
class Widget:
    name: str

    def __init__(self, name: str):
        self.name = name
");

            // Re-export from subpkg
            File.WriteAllText(Path.Combine(tempDir, "pkg", "subpkg", "__init__.spy"), @"
from pkg.subpkg.widgets import Widget
");

            // Re-export from pkg (second level of re-export)
            File.WriteAllText(Path.Combine(tempDir, "pkg", "__init__.spy"), @"
from pkg.subpkg import Widget
");

            // Use the re-exported class in main
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
from pkg import Widget

def main():
    w = Widget(""test"")
    print(w.name)
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                EntryPoint = "main.spy",
                SourceFiles = new List<string>
                {
                    Path.Combine(tempDir, "main.spy"),
                    Path.Combine(tempDir, "pkg", "__init__.spy"),
                    Path.Combine(tempDir, "pkg", "subpkg", "__init__.spy"),
                    Path.Combine(tempDir, "pkg", "subpkg", "widgets.spy")
                }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that symbols are accessible through a three-level import chain:
    /// main.spy -> utils.spy -> base.spy
    ///
    /// main.spy uses utils.get_base_value() which internally uses base.BASE_VALUE.
    /// The type of BASE_VALUE should be correctly resolved.
    /// </summary>
    [Fact]
    public void TransitiveImports_ThreeLevelChain_SymbolsAccessible()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // base.spy - the root of the dependency chain
            File.WriteAllText(Path.Combine(tempDir, "base.spy"), @"
class BaseConfig:
    debug: bool = True
    max_items: int = 100

    def __init__(self):
        pass
");

            // utils.spy - imports from base and re-exports/wraps
            File.WriteAllText(Path.Combine(tempDir, "utils.spy"), @"
from base import BaseConfig

def get_config() -> BaseConfig:
    return BaseConfig()

def get_max_items() -> int:
    config = BaseConfig()
    return config.max_items
");

            // main.spy - imports from utils, transitively depends on base
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
from utils import get_config, get_max_items

def main():
    config = get_config()
    print(config.debug)
    print(get_max_items())
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                EntryPoint = "main.spy",
                SourceFiles = new List<string>
                {
                    Path.Combine(tempDir, "main.spy"),
                    Path.Combine(tempDir, "utils.spy"),
                    Path.Combine(tempDir, "base.spy")
                }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that a class can inherit from a type that was transitively imported
    /// through a re-export chain.
    ///
    /// Structure:
    /// - base_types.spy: defines BaseClass
    /// - exports.spy: re-exports BaseClass
    /// - derived.spy: imports from exports, defines DerivedClass(BaseClass)
    /// </summary>
    [Fact]
    public void TransitiveImports_InheritanceFromReExportedType_Works()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // base_types.spy - defines the base class
            File.WriteAllText(Path.Combine(tempDir, "base_types.spy"), @"
class BaseClass:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def describe(self) -> str:
        return f""Base: {self.name}""
");

            // exports.spy - re-exports the base class
            File.WriteAllText(Path.Combine(tempDir, "exports.spy"), @"
from base_types import BaseClass
");

            // derived.spy - imports the re-exported class and inherits from it
            File.WriteAllText(Path.Combine(tempDir, "derived.spy"), @"
from exports import BaseClass

class DerivedClass(BaseClass):
    extra: int

    def __init__(self, name: str, extra: int):
        super().__init__(name)
        self.extra = extra

    @override
    def describe(self) -> str:
        return f""Derived: {self.name}, extra={self.extra}""
");

            // main.spy - uses the derived class
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
from derived import DerivedClass

def main():
    obj = DerivedClass(""test"", 42)
    print(obj.describe())
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                EntryPoint = "main.spy",
                SourceFiles = new List<string>
                {
                    Path.Combine(tempDir, "main.spy"),
                    Path.Combine(tempDir, "derived.spy"),
                    Path.Combine(tempDir, "exports.spy"),
                    Path.Combine(tempDir, "base_types.spy")
                }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Assert
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that type annotations using transitively imported types resolve correctly.
    ///
    /// A function parameter or return type that references a transitively imported
    /// type should be properly resolved for type checking and code generation.
    /// </summary>
    [Fact]
    public void TransitiveImports_TypeAnnotationsResolve_Correctly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // models.spy - defines the model class
            File.WriteAllText(Path.Combine(tempDir, "models.spy"), @"
class User:
    id: int
    name: str

    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name
");

            // repository.spy - re-exports and provides factory functions
            File.WriteAllText(Path.Combine(tempDir, "repository.spy"), @"
from models import User

def create_user(id: int, name: str) -> User:
    return User(id, name)
");

            // service.spy - uses User type in function signatures
            File.WriteAllText(Path.Combine(tempDir, "service.spy"), @"
from repository import User, create_user

def process_user(user: User) -> str:
    return f""Processing user {user.name}""

def get_user(id: int) -> User:
    return create_user(id, ""default"")
");

            // main.spy - uses the service
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), @"
from service import process_user, get_user

def main():
    user = get_user(1)
    result = process_user(user)
    print(result)
");

            var config = new ProjectConfig
            {
                ProjectDirectory = tempDir,
                RootNamespace = "TestApp",
                OutputType = "exe",
                EntryPoint = "main.spy",
                SourceFiles = new List<string>
                {
                    Path.Combine(tempDir, "main.spy"),
                    Path.Combine(tempDir, "service.spy"),
                    Path.Combine(tempDir, "repository.spy"),
                    Path.Combine(tempDir, "models.spy")
                }
            };

            var compiler = new Compiler(_logger);

            // Act
            var result = compiler.CompileProject(config);

            // Build error message with generated C# if compilation failed
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine($"Compilation failed with errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
            if (result.GeneratedCSharpFiles != null)
            {
                foreach (var (fileName, content) in result.GeneratedCSharpFiles)
                {
                    errorDetails.AppendLine($"\n=== {fileName} ===\n{content}");
                }
            }

            // Assert
            Assert.True(result.Success, errorDetails.ToString());
            Assert.False(result.Diagnostics.HasErrors, $"Expected no errors but got: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Project Config Warnings/Errors Settings

    [Fact]
    public void ProjectFileParser_Load_ParsesWarningsAsErrors()
    {
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <WarningsAsErrors>true</WarningsAsErrors>
    </PropertyGroup>
    <ItemGroup>
        <SourceFile Include=""*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), "def main():\n    pass\n");
            File.WriteAllText(Path.Combine(tempDir, "test.spyproj"), projectContent);

            var config = ProjectFileParser.Load(Path.Combine(tempDir, "test.spyproj"));

            Assert.True(config.WarningsAsErrors);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_ParsesNoWarnCodes()
    {
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
        <NoWarn>SPY0451,SPY0452;SPY0453</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <SourceFile Include=""*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), "def main():\n    pass\n");
            File.WriteAllText(Path.Combine(tempDir, "test.spyproj"), projectContent);

            var config = ProjectFileParser.Load(Path.Combine(tempDir, "test.spyproj"));

            Assert.Contains("SPY0451", config.SuppressedWarnings);
            Assert.Contains("SPY0452", config.SuppressedWarnings);
            Assert.Contains("SPY0453", config.SuppressedWarnings);
            Assert.Equal(3, config.SuppressedWarnings.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectFileParser_Load_DefaultsWarningsAsErrorsFalse()
    {
        var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <RootNamespace>TestApp</RootNamespace>
        <OutputType>exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <SourceFile Include=""*.spy"" />
    </ItemGroup>
</Project>";
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "main.spy"), "def main():\n    pass\n");
            File.WriteAllText(Path.Combine(tempDir, "test.spyproj"), projectContent);

            var config = ProjectFileParser.Load(Path.Combine(tempDir, "test.spyproj"));

            Assert.False(config.WarningsAsErrors);
            Assert.Empty(config.SuppressedWarnings);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}

