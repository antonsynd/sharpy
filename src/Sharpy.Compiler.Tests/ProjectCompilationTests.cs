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
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
            Assert.Empty(result.Errors);
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
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("Cannot find module 'nonexistent'"));
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
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
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
            Assert.True(result.Success, $"Compilation failed with errors: {string.Join(", ", result.Errors)}");
            Assert.Empty(result.Errors);
            Assert.NotNull(result.OutputAssemblyPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

